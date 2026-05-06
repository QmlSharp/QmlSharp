using QmlSharp.Build;

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Owns the dev-tools lifecycle and coordinates the initial build, watcher, console, overlay, profiler, and REPL.
    /// </summary>
    public sealed class DevServer : IDevServer
    {
        private readonly AsyncGate lifecycleGate = new();
        private readonly DevServerOptions options;
        private readonly BuildContext buildContext;
        private readonly IFileWatcher fileWatcher;
        private readonly IDevToolsBuildPipeline buildPipeline;
        private readonly IDevConsole console;
        private readonly IErrorOverlay errorOverlay;
        private readonly IPerfProfiler profiler;
        private readonly IDevToolsClock clock;
        private readonly IRepl? repl;
        private DevServerStatus status = DevServerStatus.Idle;
        private int buildCount;
        private int rebuildCount;
        private int errorCount;
        private TimeSpan totalBuildTime;
        private DateTimeOffset? runningSince;
        private bool runtimeResourcesStarted;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DevServer"/> class.
        /// </summary>
        /// <param name="options">Dev server options.</param>
        /// <param name="buildContext">Build-system context used for initial builds.</param>
        /// <param name="fileWatcher">Source file watcher owned by the server.</param>
        /// <param name="buildPipeline">Build pipeline facade from 08-build-system.</param>
        /// <param name="console">Developer console output.</param>
        /// <param name="errorOverlay">Error overlay controller.</param>
        /// <param name="profiler">Performance profiler.</param>
        public DevServer(
            DevServerOptions options,
            BuildContext buildContext,
            IFileWatcher fileWatcher,
            IDevToolsBuildPipeline buildPipeline,
            IDevConsole console,
            IErrorOverlay errorOverlay,
            IPerfProfiler profiler)
            : this(
                options,
                buildContext,
                fileWatcher,
                buildPipeline,
                console,
                errorOverlay,
                profiler,
                repl: null,
                SystemDevToolsClock.Instance)
        {
        }

        internal DevServer(
            DevServerOptions options,
            BuildContext buildContext,
            IFileWatcher fileWatcher,
            IDevToolsBuildPipeline buildPipeline,
            IDevConsole console,
            IErrorOverlay errorOverlay,
            IPerfProfiler profiler,
            IRepl? repl,
            IDevToolsClock clock)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(buildContext);
            ArgumentNullException.ThrowIfNull(fileWatcher);
            ArgumentNullException.ThrowIfNull(buildPipeline);
            ArgumentNullException.ThrowIfNull(console);
            ArgumentNullException.ThrowIfNull(errorOverlay);
            ArgumentNullException.ThrowIfNull(profiler);
            ArgumentNullException.ThrowIfNull(clock);
            if (string.IsNullOrWhiteSpace(options.ProjectRoot))
            {
                throw new ArgumentException("Project root must be provided.", nameof(options));
            }

            this.options = options;
            this.buildContext = buildContext;
            this.fileWatcher = fileWatcher;
            this.buildPipeline = buildPipeline;
            this.console = console;
            this.errorOverlay = errorOverlay;
            this.profiler = profiler;
            this.repl = repl;
            this.clock = clock;
        }

        /// <inheritdoc />
        public DevServerStatus Status => status;

        /// <inheritdoc />
        public ServerStats Stats => new(
            buildCount,
            rebuildCount,
            HotReloadCount: 0,
            errorCount,
            totalBuildTime,
            TotalHotReloadTime: TimeSpan.Zero,
            GetUptime());

        /// <inheritdoc />
        public IRepl? Repl => repl;

        /// <inheritdoc />
        public event Action<DevServerStatusChangedEvent> OnStatusChanged = static _ => { };

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            using AsyncGate.Releaser gate = await lifecycleGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
            await StartCoreAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task StopAsync()
        {
            if (disposed)
            {
                return;
            }

            using AsyncGate.Releaser gate = await lifecycleGate.AcquireAsync().ConfigureAwait(false);
            await StopCoreAsync().ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<HotReloadResult> RebuildAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            using AsyncGate.Releaser gate = await lifecycleGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
            EnsureCanRebuild();
            cancellationToken.ThrowIfCancellationRequested();

            long startTimestamp = clock.GetTimestamp();
            BuildResult buildResult = await RunRebuildBuildAsync(cancellationToken).ConfigureAwait(false);
            TimeSpan elapsed = ElapsedOrOneTick(startTimestamp);
            if (!buildResult.Success)
            {
                return HandleRebuildFailure(buildResult, elapsed);
            }

            if (status == DevServerStatus.Error)
            {
                if (!runtimeResourcesStarted)
                {
                    try
                    {
                        await StartRuntimeResourcesAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (!IsCriticalException(exception))
                    {
                        return await HandleRebuildStartupFailureAsync(exception, elapsed).ConfigureAwait(false);
                    }
                }

                runningSince = clock.UtcNow;
                TransitionTo(DevServerStatus.Running, "Rebuild succeeded.");
            }

            errorOverlay.Hide();
            return CreateRebuildResult(success: true, elapsed, errorMessage: null);
        }

        /// <inheritdoc />
        public async Task RestartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            using AsyncGate.Releaser gate = await lifecycleGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
            await StopCoreAsync().ConfigureAwait(false);
            await StartCoreAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (disposed)
            {
                return;
            }

            using AsyncGate.Releaser gate = await lifecycleGate.AcquireAsync().ConfigureAwait(false);
            await StopCoreAsync().ConfigureAwait(false);
            await DisposeOwnedResourcesAsync().ConfigureAwait(false);
            disposed = true;
        }

        private async Task StartCoreAsync(CancellationToken cancellationToken)
        {
            EnsureCanStart();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                TransitionTo(DevServerStatus.Starting, "Start requested.");
                console.Banner("dev", options);

                TransitionTo(DevServerStatus.Building, "Initial build starting.");
                BuildResult buildResult = await RunInitialBuildAsync(cancellationToken).ConfigureAwait(false);
                if (!buildResult.Success)
                {
                    HandleInitialBuildFailure(buildResult);
                    return;
                }

                await StartRuntimeResourcesAsync(cancellationToken).ConfigureAwait(false);

                runningSince = clock.UtcNow;
                errorOverlay.Hide();
                TransitionTo(DevServerStatus.Running, "Initial build succeeded.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await HandleStartupCancellationAsync().ConfigureAwait(false);
                throw;
            }
            catch (Exception exception) when (!IsCriticalException(exception))
            {
                await HandleStartupFailureAsync(exception).ConfigureAwait(false);
            }
        }

        private async Task StopCoreAsync()
        {
            if (status == DevServerStatus.Idle)
            {
                return;
            }

            if (status != DevServerStatus.Stopping)
            {
                TransitionTo(DevServerStatus.Stopping, "Stop requested.");
            }

            await CleanupRuntimeResourcesAsync().ConfigureAwait(false);
            runningSince = null;
            console.ServerStopped();
            TransitionTo(DevServerStatus.Idle, "Cleanup complete.");
        }

        private async Task<BuildResult> RunInitialBuildAsync(CancellationToken cancellationToken)
        {
            return await RunBuildAsync(
                "devserver_initial_build",
                isRebuild: false,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<BuildResult> RunRebuildBuildAsync(CancellationToken cancellationToken)
        {
            return await RunBuildAsync(
                "devserver_rebuild",
                isRebuild: true,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<BuildResult> RunBuildAsync(
            string spanName,
            bool isRebuild,
            CancellationToken cancellationToken)
        {
            long buildStart = clock.GetTimestamp();
            console.BuildStart(fileCount: 0);
            using IDisposable span = profiler.StartSpan(spanName, PerfCategory.Build);
            BuildResult buildResult = await buildPipeline
                .BuildAsync(buildContext, cancellationToken)
                .ConfigureAwait(false);

            RecordBuildResult(buildResult, buildStart, isRebuild);
            return buildResult;
        }

        private async Task StartRuntimeResourcesAsync(CancellationToken cancellationToken)
        {
            if (runtimeResourcesStarted)
            {
                return;
            }

            fileWatcher.Start();
            console.WatchStarted(fileCount: 0, options.WatcherOptions.WatchPaths);

            if (options.EnableRepl && repl is not null)
            {
                await repl.StartAsync(cancellationToken).ConfigureAwait(false);
            }

            runtimeResourcesStarted = true;
        }

        private void HandleInitialBuildFailure(BuildResult buildResult)
        {
            errorCount++;
            ImmutableArray<BuildDiagnostic> diagnostics = buildResult.Diagnostics.IsDefault
                ? ImmutableArray<BuildDiagnostic>.Empty
                : buildResult.Diagnostics;
            errorOverlay.Show(CreateOverlayErrors(diagnostics));
            console.Error(CreateBuildFailureMessage("Initial build failed", diagnostics));
            TransitionTo(DevServerStatus.Error, "Initial build failed.");
        }

        private HotReloadResult HandleRebuildFailure(BuildResult buildResult, TimeSpan elapsed)
        {
            errorCount++;
            ImmutableArray<BuildDiagnostic> diagnostics = buildResult.Diagnostics.IsDefault
                ? ImmutableArray<BuildDiagnostic>.Empty
                : buildResult.Diagnostics;
            string errorMessage = CreateBuildFailureMessage("Rebuild failed", diagnostics);
            errorOverlay.Show(CreateOverlayErrors(diagnostics));
            console.Error(errorMessage);
            TransitionTo(DevServerStatus.Error, "Rebuild failed.");
            return CreateRebuildResult(success: false, elapsed, errorMessage);
        }

        private async Task<HotReloadResult> HandleRebuildStartupFailureAsync(
            Exception exception,
            TimeSpan elapsed)
        {
            errorCount++;
            await CleanupRuntimeResourcesForFailedStartupAsync().ConfigureAwait(false);
            string errorMessage = "Rebuild startup failed: " + exception.Message;
            errorOverlay.Show(new OverlayError(
                "Dev Server Startup Error",
                exception.Message,
                FilePath: null,
                Line: null,
                Column: null));
            console.Error(errorMessage);
            TransitionTo(DevServerStatus.Error, "Rebuild resource initialization failed.");
            return CreateRebuildResult(success: false, elapsed, errorMessage);
        }

        private void RecordBuildResult(BuildResult buildResult, long buildStart, bool isRebuild)
        {
            TimeSpan measuredBuildTime = ElapsedOrOneTick(buildStart);
            TimeSpan buildTime = buildResult.Stats.TotalDuration > TimeSpan.Zero
                ? buildResult.Stats.TotalDuration
                : measuredBuildTime;

            if (isRebuild)
            {
                rebuildCount++;
            }
            else
            {
                buildCount++;
            }

            totalBuildTime += buildTime;

            if (buildResult.Success)
            {
                console.BuildSuccess(buildTime, buildResult.Stats.FilesCompiled);
            }
        }

        private static HotReloadResult CreateRebuildResult(
            bool success,
            TimeSpan elapsed,
            string? errorMessage)
        {
            return new HotReloadResult(
                success,
                InstancesMatched: 0,
                InstancesOrphaned: 0,
                InstancesNew: 0,
                new HotReloadPhases(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
                elapsed,
                errorMessage,
                FailedStep: null);
        }

        private async Task HandleStartupFailureAsync(Exception exception)
        {
            errorCount++;
            await CleanupRuntimeResourcesForFailedStartupAsync().ConfigureAwait(false);
            errorOverlay.Show(new OverlayError(
                "Dev Server Startup Error",
                exception.Message,
                FilePath: null,
                Line: null,
                Column: null));
            console.Error("Dev server startup failed: " + exception.Message);
            TransitionTo(DevServerStatus.Error, "Startup resource initialization failed.");
        }

        private async Task HandleStartupCancellationAsync()
        {
            await CleanupRuntimeResourcesForFailedStartupAsync().ConfigureAwait(false);
            runningSince = null;
            TransitionTo(DevServerStatus.Idle, "Start canceled.");
        }

        private async Task CleanupRuntimeResourcesAsync()
        {
            if (!runtimeResourcesStarted)
            {
                errorOverlay.Hide();
                return;
            }

            if (options.EnableRepl && repl is not null)
            {
                await repl.StopAsync().ConfigureAwait(false);
            }

            fileWatcher.Stop();
            errorOverlay.Hide();
            runtimeResourcesStarted = false;
        }

        private async Task CleanupRuntimeResourcesForFailedStartupAsync()
        {
            try
            {
                fileWatcher.Stop();
            }
            catch (Exception exception) when (!IsCriticalException(exception))
            {
                console.Warn("Watcher cleanup after failed startup failed: " + exception.Message);
            }

            if (options.EnableRepl && repl is not null)
            {
                try
                {
                    await repl.StopAsync().ConfigureAwait(false);
                }
                catch (Exception exception) when (!IsCriticalException(exception))
                {
                    console.Warn("REPL cleanup after failed startup failed: " + exception.Message);
                }
            }

            runtimeResourcesStarted = false;
        }

        private async Task DisposeOwnedResourcesAsync()
        {
            fileWatcher.Dispose();

            if (repl is not null)
            {
                await repl.DisposeAsync().ConfigureAwait(false);
            }
        }

        private void EnsureCanStart()
        {
            if (status != DevServerStatus.Idle)
            {
                throw new InvalidOperationException("Cannot start DevServer while it is " + status + ".");
            }
        }

        private void EnsureCanRebuild()
        {
            if (status != DevServerStatus.Running && status != DevServerStatus.Error)
            {
                throw new InvalidOperationException("Cannot rebuild DevServer while it is " + status + ".");
            }
        }

        private void TransitionTo(DevServerStatus nextStatus, string reason)
        {
            if (status == nextStatus)
            {
                return;
            }

            DevServerStatus previous = status;
            status = nextStatus;
            OnStatusChanged(new DevServerStatusChangedEvent(
                previous,
                nextStatus,
                clock.UtcNow,
                reason));
        }

        private TimeSpan GetUptime()
        {
            if (!runningSince.HasValue)
            {
                return TimeSpan.Zero;
            }

            TimeSpan uptime = clock.UtcNow - runningSince.Value;
            return uptime < TimeSpan.Zero ? TimeSpan.Zero : uptime;
        }

        private TimeSpan ElapsedOrOneTick(long startTimestamp)
        {
            TimeSpan elapsed = clock.GetElapsedTime(startTimestamp);
            return elapsed <= TimeSpan.Zero ? TimeSpan.FromTicks(1) : elapsed;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        private static ImmutableArray<OverlayError> CreateOverlayErrors(ImmutableArray<BuildDiagnostic> diagnostics)
        {
            ImmutableArray<BuildDiagnostic> displayableDiagnostics = diagnostics.IsDefault
                ? ImmutableArray<BuildDiagnostic>.Empty
                : diagnostics
                    .Where(static diagnostic => diagnostic.Severity != BuildDiagnosticSeverity.Info)
                    .ToImmutableArray();

            if (displayableDiagnostics.IsEmpty)
            {
                return ImmutableArray.Create(new OverlayError(
                    "Build Error",
                    "Initial build failed.",
                    FilePath: null,
                    Line: null,
                    Column: null));
            }

            ImmutableArray<OverlayError>.Builder builder =
                ImmutableArray.CreateBuilder<OverlayError>(displayableDiagnostics.Length);
            foreach (BuildDiagnostic diagnostic in displayableDiagnostics)
            {
                OverlaySeverity severity = diagnostic.Severity is BuildDiagnosticSeverity.Error or BuildDiagnosticSeverity.Fatal
                    ? OverlaySeverity.Error
                    : OverlaySeverity.Warning;
                builder.Add(new OverlayError(
                    severity == OverlaySeverity.Error ? "Build Error" : "Build Warning",
                    diagnostic.Code + ": " + diagnostic.Message,
                    diagnostic.FilePath,
                    Line: null,
                    Column: null,
                    severity));
            }

            return builder.ToImmutable();
        }

        private static string CreateBuildFailureMessage(
            string failurePrefix,
            ImmutableArray<BuildDiagnostic> diagnostics)
        {
            if (diagnostics.IsDefaultOrEmpty)
            {
                return failurePrefix + ".";
            }

            BuildDiagnostic primary = diagnostics
                .OrderByDescending(static diagnostic => diagnostic.Severity)
                .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .First();
            return failurePrefix + ": " + primary.Code + ": " + primary.Message;
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

        private sealed class SystemDevToolsClock : IDevToolsClock
        {
            public static SystemDevToolsClock Instance { get; } = new();

            public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

            public long GetTimestamp()
            {
                return TimeProvider.System.GetTimestamp();
            }

            public TimeSpan GetElapsedTime(long startTimestamp)
            {
                return TimeProvider.System.GetElapsedTime(startTimestamp);
            }
        }

        private sealed class AsyncGate
        {
            private readonly SemaphoreSlim semaphore = new(1, 1);

            public async Task<Releaser> AcquireAsync(CancellationToken cancellationToken = default)
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new Releaser(semaphore);
            }

            public sealed class Releaser : IDisposable
            {
                private SemaphoreSlim? semaphore;

                internal Releaser(SemaphoreSlim semaphore)
                {
                    this.semaphore = semaphore;
                }

                public void Dispose()
                {
                    SemaphoreSlim? current = Interlocked.Exchange(ref semaphore, null);
                    if (current is not null)
                    {
                        _ = current.Release();
                    }
                }
            }
        }
    }
}
