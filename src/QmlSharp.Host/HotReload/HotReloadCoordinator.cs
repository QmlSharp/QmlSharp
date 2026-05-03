using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using QmlSharp.Host.Commands;
using QmlSharp.Host.Effects;
using QmlSharp.Host.InstanceRegistry;
using QmlSharp.Host.Instances;
using QmlSharp.Host.Interop;
using QmlSharp.Host.StateSynchronization;
using ManagedInstanceRegistry = QmlSharp.Host.Instances.InstanceRegistry;

namespace QmlSharp.Host.HotReload
{
    /// <summary>Coordinates the managed side of the capture, reload, hydrate, and restore protocol.</summary>
    public sealed class HotReloadCoordinator
    {
        private readonly ManagedInstanceRegistry registry;
        private readonly INativeHostInterop interop;
        private readonly StateSync stateSync;
        private readonly CommandRouter? commandRouter;
        private readonly EffectRouter? effectRouter;
        private readonly Lock syncRoot = new();

        internal HotReloadCoordinator(
            ManagedInstanceRegistry registry,
            INativeHostInterop interop,
            StateSync? stateSync = null,
            CommandRouter? commandRouter = null,
            EffectRouter? effectRouter = null)
        {
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentNullException.ThrowIfNull(interop);

            this.registry = registry;
            this.interop = interop;
            this.stateSync = stateSync ?? new StateSync(registry, interop);
            this.commandRouter = commandRouter;
            this.effectRouter = effectRouter;
        }

        /// <summary>Fires when a protocol phase starts, giving tests and diagnostics an ordered trace.</summary>
        public event EventHandler<HotReloadStepStartedEventArgs>? StepStarted;

        /// <summary>Executes one full hot reload attempt against the native host.</summary>
        public Task<HotReloadResult> ReloadAsync(
            IntPtr engineHandle,
            string qmlSourcePath,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(qmlSourcePath);

            lock (syncRoot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                HotReloadResult result = ReloadCore(engineHandle, qmlSourcePath, cancellationToken);
                registry.RecordHotReload(result.Success, result.TotalTime);
                return Task.FromResult(result);
            }
        }

        private HotReloadResult ReloadCore(
            IntPtr engineHandle,
            string qmlSourcePath,
            CancellationToken cancellationToken)
        {
            HotReloadAttemptData attemptData = CaptureAttemptData();

            HotReloadPhaseResult<string?> capture = CaptureNativeSnapshot(engineHandle);
            if (!capture.Success || string.IsNullOrWhiteSpace(capture.Value))
            {
                return CaptureFailure(attemptData, capture);
            }

            cancellationToken.ThrowIfCancellationRequested();
            HotReloadPhaseResult<int> reload = ReloadNativeQml(engineHandle, qmlSourcePath);
            if (!reload.Success || reload.Value != 0)
            {
                return ReloadFailure(attemptData, capture, reload);
            }

            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<ManagedViewModelInstance> newInstances = registry.GetAll();
            InstanceMatchResult matchResult = InstanceMatcher.Match(attemptData.OldSnapshots, newInstances);
            HotReloadPhaseResult<int> hydrate = HydrateMatchedInstances(
                matchResult,
                attemptData.OldInfoById,
                attemptData.CommandSnapshots,
                attemptData.EffectSnapshots);
            if (!hydrate.Success)
            {
                return HydrateFailure(attemptData, newInstances, capture, reload, hydrate);
            }

            cancellationToken.ThrowIfCancellationRequested();
            HotReloadPhaseResult<int> restore = RestoreNativeSnapshot(engineHandle, capture.Value);
            if (!restore.Success)
            {
                return RestoreFailure(attemptData, newInstances, capture, reload, hydrate, restore);
            }

            return Success(matchResult, capture, reload, hydrate, restore);
        }

        private HotReloadAttemptData CaptureAttemptData()
        {
            IReadOnlyList<InstanceSnapshot> oldSnapshots = registry.CaptureInstanceSnapshots();
            return new HotReloadAttemptData(
                oldSnapshots,
                CaptureInfoById(oldSnapshots),
                CaptureCommandSnapshots(oldSnapshots),
                CaptureEffectSnapshots(oldSnapshots));
        }

        private HotReloadResult CaptureFailure(HotReloadAttemptData attemptData, HotReloadPhaseResult<string?> capture)
        {
            return Failure(
                HotReloadStep.Capture,
                attemptData.OldSnapshots,
                newInstances: [],
                capture.Elapsed,
                reloadTime: TimeSpan.Zero,
                hydrateTime: TimeSpan.Zero,
                restoreTime: TimeSpan.Zero,
                capture.Value,
                capture.ErrorMessage ?? LastErrorOrDefault("Native snapshot capture failed."));
        }

        private HotReloadResult ReloadFailure(
            HotReloadAttemptData attemptData,
            HotReloadPhaseResult<string?> capture,
            HotReloadPhaseResult<int> reload)
        {
            return Failure(
                HotReloadStep.Reload,
                attemptData.OldSnapshots,
                registry.GetAll(),
                capture.Elapsed,
                reload.Elapsed,
                hydrateTime: TimeSpan.Zero,
                restoreTime: TimeSpan.Zero,
                capture.Value,
                reload.ErrorMessage ?? LastErrorOrDefault(
                    string.Format(CultureInfo.InvariantCulture, "Native QML reload failed with error code {0}.", reload.Value)));
        }

        private static HotReloadResult Success(
            InstanceMatchResult matchResult,
            HotReloadPhaseResult<string?> capture,
            HotReloadPhaseResult<int> reload,
            HotReloadPhaseResult<int> hydrate,
            HotReloadPhaseResult<int> restore)
        {
            return new HotReloadResult(
                Success: true,
                matchResult.Matched.Count,
                matchResult.Orphaned.Count,
                capture.Elapsed,
                reload.Elapsed,
                hydrate.Elapsed,
                restore.Elapsed,
                ErrorMessage: null)
            {
                InstancesNew = matchResult.Unmatched.Count,
                NativeSnapshotJson = capture.Value
            };
        }

        private HotReloadResult HydrateFailure(
            HotReloadAttemptData attemptData,
            IReadOnlyList<ManagedViewModelInstance> newInstances,
            HotReloadPhaseResult<string?> capture,
            HotReloadPhaseResult<int> reload,
            HotReloadPhaseResult<int> hydrate)
        {
            return Failure(
                HotReloadStep.Hydrate,
                attemptData.OldSnapshots,
                newInstances,
                capture.Elapsed,
                reload.Elapsed,
                hydrate.Elapsed,
                restoreTime: TimeSpan.Zero,
                capture.Value,
                hydrate.ErrorMessage ?? "Managed hot reload hydration failed.");
        }

        private HotReloadResult RestoreFailure(
            HotReloadAttemptData attemptData,
            IReadOnlyList<ManagedViewModelInstance> newInstances,
            HotReloadPhaseResult<string?> capture,
            HotReloadPhaseResult<int> reload,
            HotReloadPhaseResult<int> hydrate,
            HotReloadPhaseResult<int> restore)
        {
            return Failure(
                HotReloadStep.Restore,
                attemptData.OldSnapshots,
                newInstances,
                capture.Elapsed,
                reload.Elapsed,
                hydrate.Elapsed,
                restore.Elapsed,
                capture.Value,
                restore.ErrorMessage ?? "Native snapshot restoration failed.");
        }

        private sealed record HotReloadAttemptData(
            IReadOnlyList<InstanceSnapshot> OldSnapshots,
            IReadOnlyDictionary<string, InstanceInfo> OldInfoById,
            IReadOnlyDictionary<string, CommandRouterSnapshot> CommandSnapshots,
            IReadOnlyDictionary<string, EffectRouterSnapshot> EffectSnapshots);

        private HotReloadPhaseResult<string?> CaptureNativeSnapshot(IntPtr engineHandle)
        {
            return RunPhase(HotReloadStep.Capture, () => ExecuteNative(() => interop.CaptureSnapshot(engineHandle)));
        }

        private HotReloadPhaseResult<int> ReloadNativeQml(IntPtr engineHandle, string qmlSourcePath)
        {
            return RunPhase(HotReloadStep.Reload, () => ExecuteNative(() => interop.ReloadQml(engineHandle, qmlSourcePath)));
        }

        private HotReloadPhaseResult<int> HydrateMatchedInstances(
            InstanceMatchResult matchResult,
            IReadOnlyDictionary<string, InstanceInfo> oldInfoById,
            IReadOnlyDictionary<string, CommandRouterSnapshot> commandSnapshots,
            IReadOnlyDictionary<string, EffectRouterSnapshot> effectSnapshots)
        {
            return RunPhase(HotReloadStep.Hydrate, () =>
            {
                Hydrate(matchResult, oldInfoById, commandSnapshots, effectSnapshots);
                return 0;
            });
        }

        private HotReloadPhaseResult<int> RestoreNativeSnapshot(IntPtr engineHandle, string nativeSnapshotJson)
        {
            return RunPhase(HotReloadStep.Restore, () => ExecuteNative(() =>
            {
                interop.RestoreSnapshot(engineHandle, nativeSnapshotJson);
                return 0;
            }));
        }

        private HotReloadPhaseResult<T> RunPhase<T>(HotReloadStep step, Func<T> operation)
        {
            try
            {
                (T result, TimeSpan elapsed) = Measure(() =>
                {
                    OnStepStarted(step);
                    return operation();
                });
                return HotReloadPhaseResult<T>.Completed(result, elapsed);
            }
            catch (Exception exception) when (!IsCriticalException(exception))
            {
                return HotReloadPhaseResult<T>.Failed(ElapsedOrOneTick(TimeSpan.Zero), exception.Message);
            }
        }

        private void Hydrate(
            InstanceMatchResult matchResult,
            IReadOnlyDictionary<string, InstanceInfo> oldInfoById,
            IReadOnlyDictionary<string, CommandRouterSnapshot> commandSnapshots,
            IReadOnlyDictionary<string, EffectRouterSnapshot> effectSnapshots)
        {
            foreach (InstanceMatch matched in matchResult.Matched)
            {
                if (matched.Old.State.Count > 0)
                {
                    stateSync.PushBatch(matched.New.InstanceId, matched.Old.State);
                }

                if (oldInfoById.TryGetValue(matched.Old.InstanceId, out InstanceInfo? oldInfo))
                {
                    RestoreReadyAndQueues(matched.New.InstanceId, oldInfo, commandSnapshots);
                }

                if (effectRouter is not null && effectSnapshots.TryGetValue(matched.Old.InstanceId, out EffectRouterSnapshot? effectSnapshot))
                {
                    effectRouter.RestoreForHotReload(matched.New.InstanceId, effectSnapshot);
                }
            }
        }

        private void RestoreReadyAndQueues(
            string newInstanceId,
            InstanceInfo oldInfo,
            IReadOnlyDictionary<string, CommandRouterSnapshot> commandSnapshots)
        {
            bool restoreQueuedCommands = oldInfo.State == InstanceState.Pending;
            if (commandRouter is not null && commandSnapshots.TryGetValue(oldInfo.InstanceId, out CommandRouterSnapshot? commandSnapshot))
            {
                commandRouter.RestoreForHotReload(newInstanceId, commandSnapshot, restoreQueuedCommands);
            }
            else if (restoreQueuedCommands && oldInfo.QueuedCommandCount > 0)
            {
                _ = registry.SetQueuedCommandCount(newInstanceId, oldInfo.QueuedCommandCount);
            }

            if (oldInfo.State == InstanceState.Active)
            {
                _ = registry.MarkReady(newInstanceId);
            }
        }

        private IReadOnlyDictionary<string, InstanceInfo> CaptureInfoById(IReadOnlyList<InstanceSnapshot> snapshots)
        {
            Dictionary<string, InstanceInfo> oldInfoById = new(StringComparer.Ordinal);
            foreach (InstanceSnapshot snapshot in snapshots)
            {
                InstanceInfo? info = registry.GetInfo(snapshot.InstanceId);
                if (info is not null)
                {
                    oldInfoById.Add(snapshot.InstanceId, info);
                }
            }

            return new ReadOnlyDictionary<string, InstanceInfo>(oldInfoById);
        }

        private IReadOnlyDictionary<string, CommandRouterSnapshot> CaptureCommandSnapshots(IReadOnlyList<InstanceSnapshot> snapshots)
        {
            Dictionary<string, CommandRouterSnapshot> snapshotsById = new(StringComparer.Ordinal);
            if (commandRouter is null)
            {
                return new ReadOnlyDictionary<string, CommandRouterSnapshot>(snapshotsById);
            }

            foreach (InstanceSnapshot snapshot in snapshots)
            {
                snapshotsById.Add(snapshot.InstanceId, commandRouter.CaptureForHotReload(snapshot.InstanceId));
            }

            return new ReadOnlyDictionary<string, CommandRouterSnapshot>(snapshotsById);
        }

        private IReadOnlyDictionary<string, EffectRouterSnapshot> CaptureEffectSnapshots(IReadOnlyList<InstanceSnapshot> snapshots)
        {
            Dictionary<string, EffectRouterSnapshot> snapshotsById = new(StringComparer.Ordinal);
            if (effectRouter is null)
            {
                return new ReadOnlyDictionary<string, EffectRouterSnapshot>(snapshotsById);
            }

            foreach (InstanceSnapshot snapshot in snapshots)
            {
                snapshotsById.Add(snapshot.InstanceId, effectRouter.CaptureForHotReload(snapshot.InstanceId));
            }

            return new ReadOnlyDictionary<string, EffectRouterSnapshot>(snapshotsById);
        }

        private HotReloadResult Failure(
            HotReloadStep failedStep,
            IReadOnlyList<InstanceSnapshot> oldSnapshots,
            IReadOnlyList<ManagedViewModelInstance> newInstances,
            TimeSpan captureTime,
            TimeSpan reloadTime,
            TimeSpan hydrateTime,
            TimeSpan restoreTime,
            string? nativeSnapshotJson,
            string errorMessage)
        {
            InstanceMatchResult matchResult = InstanceMatcher.Match(oldSnapshots, newInstances);
            return new HotReloadResult(
                Success: false,
                matchResult.Matched.Count,
                matchResult.Orphaned.Count,
                captureTime,
                reloadTime,
                hydrateTime,
                restoreTime,
                errorMessage)
            {
                InstancesNew = matchResult.Unmatched.Count,
                FailedStep = failedStep,
                NativeSnapshotJson = nativeSnapshotJson
            };
        }

        private T ExecuteNative<T>(Func<T> operation)
        {
            if (interop.IsOnMainThread)
            {
                return operation();
            }

            using ManualResetEventSlim completed = new();
            T? result = default;
            Exception? exception = null;
            interop.PostToMainThread(() =>
            {
                try
                {
                    result = operation();
                }
                catch (Exception capturedException) when (!IsCriticalException(capturedException))
                {
                    exception = capturedException;
                }
                finally
                {
                    completed.Set();
                }
            });
            completed.Wait();

            if (exception is not null)
            {
                throw exception;
            }

            return result!;
        }

        private string LastErrorOrDefault(string fallback)
        {
            return interop.GetLastError() ?? fallback;
        }

        private void OnStepStarted(HotReloadStep step)
        {
            StepStarted?.Invoke(this, new HotReloadStepStartedEventArgs(step));
        }

        private static (T Result, TimeSpan Elapsed) Measure<T>(Func<T> operation)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            T result = operation();
            stopwatch.Stop();
            return (result, ElapsedOrOneTick(stopwatch.Elapsed));
        }

        private static TimeSpan ElapsedOrOneTick(TimeSpan elapsed)
        {
            return elapsed <= TimeSpan.Zero ? TimeSpan.FromTicks(1) : elapsed;
        }

        private static bool IsCriticalException(Exception exception)
        {
            return exception is OutOfMemoryException
                or StackOverflowException
                or AccessViolationException
                or AppDomainUnloadedException
                or BadImageFormatException
                or CannotUnloadAppDomainException
                or InvalidProgramException
                or ThreadAbortException;
        }

        private sealed record HotReloadPhaseResult<T>(bool Success, T? Value, TimeSpan Elapsed, string? ErrorMessage)
        {
            internal static HotReloadPhaseResult<T> Completed(T value, TimeSpan elapsed)
            {
                return new HotReloadPhaseResult<T>(Success: true, value, elapsed, ErrorMessage: null);
            }

            internal static HotReloadPhaseResult<T> Failed(TimeSpan elapsed, string errorMessage)
            {
                return new HotReloadPhaseResult<T>(Success: false, Value: default, elapsed, errorMessage);
            }
        }
    }
}
