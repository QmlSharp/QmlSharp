using QmlSharp.Compiler;
using QmlSharp.Host.Instances;

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Coordinates the four-step hot reload protocol for the dev-tools layer.
    /// </summary>
    public sealed class HotReloadOrchestrator : IHotReloadOrchestrator
    {
        private static readonly HotReloadPhases EmptyPhases = new(
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.Zero);

        private readonly IDevToolsNativeHost nativeHost;
        private readonly IPerfProfiler profiler;
        private readonly IInstanceMatcher instanceMatcher;
        private readonly IDevToolsClock clock;

        /// <summary>
        /// Initializes a new hot reload orchestrator.
        /// </summary>
        /// <param name="nativeHost">Native-host facade used for QML reload and state sync.</param>
        /// <param name="profiler">Performance profiler for hot reload spans.</param>
        public HotReloadOrchestrator(IDevToolsNativeHost nativeHost, IPerfProfiler profiler)
            : this(nativeHost, profiler, new InstanceMatcher(), SystemDevToolsClock.Instance)
        {
        }

        internal HotReloadOrchestrator(
            IDevToolsNativeHost nativeHost,
            IPerfProfiler profiler,
            IInstanceMatcher instanceMatcher,
            IDevToolsClock clock)
        {
            ArgumentNullException.ThrowIfNull(nativeHost);
            ArgumentNullException.ThrowIfNull(profiler);
            ArgumentNullException.ThrowIfNull(instanceMatcher);
            ArgumentNullException.ThrowIfNull(clock);

            this.nativeHost = nativeHost;
            this.profiler = profiler;
            this.instanceMatcher = instanceMatcher;
            this.clock = clock;
        }

        /// <inheritdoc />
        public event Action<HotReloadStartingEvent> OnBefore = static _ => { };

        /// <inheritdoc />
        public event Action<HotReloadCompletedEvent> OnAfter = static _ => { };

        /// <inheritdoc />
        public async Task<HotReloadResult> ReloadAsync(
            CompilationResult result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(result);

            long totalStart = clock.GetTimestamp();
            using IDisposable totalSpan = profiler.StartSpan("hot_reload", PerfCategory.HotReload);

            try
            {
                HotReloadResult resultValue = await ExecuteProtocolAsync(
                    result,
                    totalStart,
                    cancellationToken).ConfigureAwait(false);
                return Complete(resultValue);
            }
            catch (OperationCanceledException exception)
            {
                HotReloadResult canceledResult = new(
                    Success: false,
                    InstancesMatched: 0,
                    InstancesOrphaned: 0,
                    InstancesNew: 0,
                    EmptyPhases,
                    TotalTime: ElapsedOrOneTick(totalStart),
                    ErrorMessage: exception.Message,
                    FailedStep: HotReloadStep.Capture);
                return Complete(canceledResult);
            }
        }

        private async Task<HotReloadResult> ExecuteProtocolAsync(
            CompilationResult result,
            long totalStart,
            CancellationToken cancellationToken)
        {
            int oldInstanceCount = await GetOldInstanceCountAsync(cancellationToken).ConfigureAwait(false);
            OnBefore(new HotReloadStartingEvent(oldInstanceCount, clock.UtcNow));

            HotReloadPhaseResult<IReadOnlyList<InstanceSnapshot>> capture =
                await CaptureAsync(cancellationToken).ConfigureAwait(false);
            if (!capture.Success || capture.Value is null)
            {
                return CreateCaptureFailure(capture, totalStart);
            }

            return await ExecuteAfterCaptureAsync(
                result,
                capture,
                totalStart,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<HotReloadResult> ExecuteAfterCaptureAsync(
            CompilationResult result,
            HotReloadPhaseResult<IReadOnlyList<InstanceSnapshot>> capture,
            long totalStart,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<InstanceSnapshot> oldSnapshots = capture.Value ?? EmptyInstanceSnapshots();
            string qmlSourcePath = ResolveQmlSourcePath(result);
            HotReloadPhaseResult<object?> nukeLoad =
                await NukeLoadAsync(qmlSourcePath, cancellationToken).ConfigureAwait(false);
            if (!nukeLoad.Success)
            {
                return CreateNukeLoadFailure(oldSnapshots, capture, nukeLoad, totalStart);
            }

            HotReloadPhaseResult<MatchResult> hydrate =
                await HydrateAsync(oldSnapshots, cancellationToken).ConfigureAwait(false);
            if (!hydrate.Success || hydrate.Value is null)
            {
                return CreateHydrateFailure(oldSnapshots, capture, nukeLoad, hydrate, totalStart);
            }

            return await ExecuteRestoreAsync(
                oldSnapshots,
                capture,
                nukeLoad,
                hydrate,
                totalStart,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<HotReloadResult> ExecuteRestoreAsync(
            IReadOnlyList<InstanceSnapshot> oldSnapshots,
            HotReloadPhaseResult<IReadOnlyList<InstanceSnapshot>> capture,
            HotReloadPhaseResult<object?> nukeLoad,
            HotReloadPhaseResult<MatchResult> hydrate,
            long totalStart,
            CancellationToken cancellationToken)
        {
            MatchResult hydrateResult = hydrate.Value ?? new MatchResult(
                EmptyMatchedPairs(),
                oldSnapshots,
                EmptyInstanceInfo());
            HotReloadPhaseResult<object?> restore =
                await RestoreAsync(oldSnapshots, cancellationToken).ConfigureAwait(false);

            return restore.Success
                ? CreateSuccess(
                    hydrateResult,
                    capture.Elapsed,
                    nukeLoad.Elapsed,
                    hydrate.Elapsed,
                    restore.Elapsed,
                    totalStart)
                : CreateFailure(
                    HotReloadStep.Restore,
                    hydrateResult,
                    capture.Elapsed,
                    nukeLoad.Elapsed,
                    hydrate.Elapsed,
                    restore.Elapsed,
                    restore.ErrorMessage ?? "Native snapshot restoration failed.",
                    totalStart);
        }

        private async Task<int> GetOldInstanceCountAsync(CancellationToken cancellationToken)
        {
            try
            {
                IReadOnlyList<InstanceInfo> instances =
                    await nativeHost.GetInstancesAsync(cancellationToken).ConfigureAwait(false);
                return instances.Count;
            }
            catch (Exception exception) when (!IsCriticalException(exception))
            {
                return 0;
            }
        }

        private Task<HotReloadPhaseResult<IReadOnlyList<InstanceSnapshot>>> CaptureAsync(
            CancellationToken cancellationToken)
        {
            return RunPhaseAsync(
                HotReloadStep.Capture,
                "capture_snapshot",
                PerfCategory.Capture,
                async token => await nativeHost.CaptureSnapshotsAsync(token).ConfigureAwait(false),
                cancellationToken);
        }

        private Task<HotReloadPhaseResult<object?>> NukeLoadAsync(
            string qmlSourcePath,
            CancellationToken cancellationToken)
        {
            return RunPhaseAsync<object?>(
                HotReloadStep.NukeLoad,
                "nuke_load",
                PerfCategory.HotReload,
                async token =>
                {
                    await nativeHost.ReloadQmlAsync(qmlSourcePath, token).ConfigureAwait(false);
                    return null;
                },
                cancellationToken);
        }

        private Task<HotReloadPhaseResult<MatchResult>> HydrateAsync(
            IReadOnlyList<InstanceSnapshot> oldSnapshots,
            CancellationToken cancellationToken)
        {
            return RunPhaseAsync(
                HotReloadStep.Hydrate,
                "hydrate",
                PerfCategory.HotReload,
                async token =>
                {
                    IReadOnlyList<InstanceInfo> newInstances =
                        await nativeHost.GetInstancesAsync(token).ConfigureAwait(false);
                    MatchResult matchResult = instanceMatcher.Match(oldSnapshots, newInstances);

                    foreach ((InstanceSnapshot oldSnapshot, InstanceInfo newInstance) in matchResult.Matched)
                    {
                        if (oldSnapshot.State.Count == 0)
                        {
                            continue;
                        }

                        await nativeHost
                            .SyncStateBatchAsync(newInstance.InstanceId, oldSnapshot.State, token)
                            .ConfigureAwait(false);
                    }

                    return matchResult;
                },
                cancellationToken);
        }

        private Task<HotReloadPhaseResult<object?>> RestoreAsync(
            IReadOnlyList<InstanceSnapshot> snapshots,
            CancellationToken cancellationToken)
        {
            return RunPhaseAsync<object?>(
                HotReloadStep.Restore,
                "restore_snapshot",
                PerfCategory.Restore,
                async token =>
                {
                    await nativeHost.RestoreSnapshotsAsync(snapshots, token).ConfigureAwait(false);
                    return null;
                },
                cancellationToken);
        }

        private async Task<HotReloadPhaseResult<T>> RunPhaseAsync<T>(
            HotReloadStep step,
            string spanName,
            PerfCategory category,
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken)
        {
            long phaseStart = clock.GetTimestamp();
            using IDisposable span = profiler.StartSpan(spanName, category);
            if (span is IPerfSpan perfSpan)
            {
                perfSpan.AddMetadata("step", step.ToString());
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                T result = await operation(cancellationToken).ConfigureAwait(false);
                return HotReloadPhaseResult<T>.Completed(result, ElapsedOrOneTick(phaseStart));
            }
            catch (OperationCanceledException exception)
            {
                return HotReloadPhaseResult<T>.Failed(ElapsedOrOneTick(phaseStart), exception.Message);
            }
            catch (Exception exception) when (!IsCriticalException(exception))
            {
                return HotReloadPhaseResult<T>.Failed(ElapsedOrOneTick(phaseStart), exception.Message);
            }
        }

        private HotReloadResult Complete(HotReloadResult result)
        {
            OnAfter(new HotReloadCompletedEvent(result, clock.UtcNow));
            return result;
        }

        private HotReloadResult CreateSuccess(
            MatchResult matchResult,
            TimeSpan captureTime,
            TimeSpan nukeLoadTime,
            TimeSpan hydrateTime,
            TimeSpan restoreTime,
            long totalStart)
        {
            return new HotReloadResult(
                Success: true,
                matchResult.Matched.Count,
                matchResult.Orphaned.Count,
                matchResult.Unmatched.Count,
                new HotReloadPhases(captureTime, nukeLoadTime, hydrateTime, restoreTime),
                ElapsedOrOneTick(totalStart),
                ErrorMessage: null,
                FailedStep: null);
        }

        private HotReloadResult CreateCaptureFailure(
            HotReloadPhaseResult<IReadOnlyList<InstanceSnapshot>> capture,
            long totalStart)
        {
            return CreateFailure(
                HotReloadStep.Capture,
                EmptyInstanceSnapshots(),
                EmptyInstanceInfo(),
                capture.Elapsed,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                capture.ErrorMessage ?? "Native snapshot capture failed.",
                totalStart);
        }

        private HotReloadResult CreateNukeLoadFailure(
            IReadOnlyList<InstanceSnapshot> oldSnapshots,
            HotReloadPhaseResult<IReadOnlyList<InstanceSnapshot>> capture,
            HotReloadPhaseResult<object?> nukeLoad,
            long totalStart)
        {
            return CreateFailure(
                HotReloadStep.NukeLoad,
                oldSnapshots,
                EmptyInstanceInfo(),
                capture.Elapsed,
                nukeLoad.Elapsed,
                TimeSpan.Zero,
                TimeSpan.Zero,
                nukeLoad.ErrorMessage ?? "Native QML reload failed.",
                totalStart);
        }

        private HotReloadResult CreateHydrateFailure(
            IReadOnlyList<InstanceSnapshot> oldSnapshots,
            HotReloadPhaseResult<IReadOnlyList<InstanceSnapshot>> capture,
            HotReloadPhaseResult<object?> nukeLoad,
            HotReloadPhaseResult<MatchResult> hydrate,
            long totalStart)
        {
            MatchResult matchResult = hydrate.Value ?? new MatchResult(
                EmptyMatchedPairs(),
                oldSnapshots,
                EmptyInstanceInfo());

            return CreateFailure(
                HotReloadStep.Hydrate,
                matchResult,
                capture.Elapsed,
                nukeLoad.Elapsed,
                hydrate.Elapsed,
                TimeSpan.Zero,
                hydrate.ErrorMessage ?? "State hydration failed.",
                totalStart);
        }

        private HotReloadResult CreateFailure(
            HotReloadStep failedStep,
            IReadOnlyList<InstanceSnapshot> oldSnapshots,
            IReadOnlyList<InstanceInfo> newInstances,
            TimeSpan captureTime,
            TimeSpan nukeLoadTime,
            TimeSpan hydrateTime,
            TimeSpan restoreTime,
            string errorMessage,
            long totalStart)
        {
            MatchResult matchResult = instanceMatcher.Match(oldSnapshots, newInstances);
            return CreateFailure(
                failedStep,
                matchResult,
                captureTime,
                nukeLoadTime,
                hydrateTime,
                restoreTime,
                errorMessage,
                totalStart);
        }

        private HotReloadResult CreateFailure(
            HotReloadStep failedStep,
            MatchResult matchResult,
            TimeSpan captureTime,
            TimeSpan nukeLoadTime,
            TimeSpan hydrateTime,
            TimeSpan restoreTime,
            string errorMessage,
            long totalStart)
        {
            return new HotReloadResult(
                Success: false,
                matchResult.Matched.Count,
                matchResult.Orphaned.Count,
                matchResult.Unmatched.Count,
                new HotReloadPhases(captureTime, nukeLoadTime, hydrateTime, restoreTime),
                ElapsedOrOneTick(totalStart),
                errorMessage,
                failedStep);
        }

        private static string ResolveQmlSourcePath(CompilationResult result)
        {
            CompilationUnit? unit = result.Units.FirstOrDefault(static candidate =>
                !string.IsNullOrWhiteSpace(candidate.QmlText));
            unit ??= result.Units.FirstOrDefault();

            if (unit is null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(unit.SourceMap?.OutputFilePath))
            {
                return unit.SourceMap.OutputFilePath;
            }

            if (Path.GetExtension(unit.SourceFilePath).Equals(".qml", StringComparison.OrdinalIgnoreCase))
            {
                return unit.SourceFilePath;
            }

            string fileName = string.IsNullOrWhiteSpace(unit.ViewClassName)
                ? Path.GetFileNameWithoutExtension(NormalizeSeparators(unit.SourceFilePath))
                : NormalizeSeparators(unit.ViewClassName);
            string safeFileName = Path.GetFileName(fileName);

            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                return string.Empty;
            }

            return safeFileName.EndsWith(".qml", StringComparison.OrdinalIgnoreCase)
                ? safeFileName
                : safeFileName + ".qml";
        }

        private TimeSpan ElapsedOrOneTick(long startTimestamp)
        {
            TimeSpan elapsed = clock.GetElapsedTime(startTimestamp);
            return elapsed <= TimeSpan.Zero ? TimeSpan.FromTicks(1) : elapsed;
        }

        private static IReadOnlyList<InstanceSnapshot> EmptyInstanceSnapshots()
        {
            return ImmutableArray<InstanceSnapshot>.Empty;
        }

        private static IReadOnlyList<InstanceInfo> EmptyInstanceInfo()
        {
            return ImmutableArray<InstanceInfo>.Empty;
        }

        private static IReadOnlyList<(InstanceSnapshot Old, InstanceInfo New)> EmptyMatchedPairs()
        {
            return ImmutableArray<(InstanceSnapshot Old, InstanceInfo New)>.Empty;
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

        private static string NormalizeSeparators(string path)
        {
            return path
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
        }

        private sealed record HotReloadPhaseResult<T>(
            bool Success,
            T? Value,
            TimeSpan Elapsed,
            string? ErrorMessage)
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
