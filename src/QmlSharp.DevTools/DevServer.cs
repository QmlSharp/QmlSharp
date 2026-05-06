using QmlSharp.Build;

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Owns the dev-tools lifecycle and coordinates the initial build, watcher, console, overlay, profiler, and REPL.
    /// </summary>
    public sealed class DevServer : IDevServer
    {
        private static readonly HotReloadPhases EmptyHotReloadPhases = new(
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.Zero);

        private readonly SemaphoreSlim lifecycleGate = new(1, 1);
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
        private int hotReloadCount;
        private int errorCount;
        private TimeSpan totalBuildTime;
        private TimeSpan totalHotReloadTime;
        private DateTimeOffset? runningSince;
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
            hotReloadCount,
            errorCount,
            totalBuildTime,
            totalHotReloadTime,
            GetUptime());

        /// <inheritdoc />
        public IRepl? Repl => repl;

        /// <inheritdoc />
        public event Action<DevServerStatusChangedEvent> OnStatusChanged = static _ => { };

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await StartCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ = lifecycleGate.Release();
            }
        }

        /// <inheritdoc />
        public async Task StopAsync()
        {
            if (disposed)
            {
                return;
            }

            await lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await StopCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                _ = lifecycleGate.Release();
            }
        }

        /// <inheritdoc />
        public async Task<HotReloadResult> RebuildAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureCanRebuild();
                cancellationToken.ThrowIfCancellationRequested();

                long startTimestamp = clock.GetTimestamp();
                using IDisposable span = profiler.StartSpan("devserver_rebuild_placeholder", PerfCategory.HotReload);
                TimeSpan elapsed = ElapsedOrOneTick(startTimestamp);
                HotReloadResult result = new(
                    Success: true,
                    InstancesMatched: 0,
                    InstancesOrphaned: 0,
                    InstancesNew: 0,
                    EmptyHotReloadPhases,
                    elapsed,
                    ErrorMessage: null,
                    FailedStep: null);

                rebuildCount++;
                hotReloadCount++;
                totalHotReloadTime += result.TotalTime;
                errorOverlay.Hide();
                console.HotReloadSuccess(result);
                return result;
            }
            finally
            {
                _ = lifecycleGate.Release();
            }
        }

        /// <inheritdoc />
        public async Task RestartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await StopCoreAsync().ConfigureAwait(false);
                await StartCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ = lifecycleGate.Release();
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (disposed)
            {
                return;
            }

            await lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await StopCoreAsync().ConfigureAwait(false);
                await DisposeOwnedResourcesAsync().ConfigureAwait(false);
                disposed = true;
            }
            finally
            {
                _ = lifecycleGate.Release();
                lifecycleGate.Dispose();
            }
        }

        private async Task StartCoreAsync(CancellationToken cancellationToken)
        {
            EnsureCanStart();
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

            try
            {
                await StartRuntimeResourcesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (!IsCriticalException(exception))
            {
                await HandleStartupFailureAsync(exception).ConfigureAwait(false);
                return;
            }

            runningSince = clock.UtcNow;
            errorOverlay.Hide();
            TransitionTo(DevServerStatus.Running, "Initial build succeeded.");
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
            long buildStart = clock.GetTimestamp();
            console.BuildStart(fileCount: 0);
            using IDisposable span = profiler.StartSpan("devserver_initial_build", PerfCategory.Build);
            BuildResult buildResult = await buildPipeline
                .BuildAsync(buildContext, cancellationToken)
                .ConfigureAwait(false);

            TimeSpan measuredBuildTime = ElapsedOrOneTick(buildStart);
            TimeSpan buildTime = buildResult.Stats.TotalDuration > TimeSpan.Zero
                ? buildResult.Stats.TotalDuration
                : measuredBuildTime;

            buildCount++;
            totalBuildTime += buildTime;

            if (buildResult.Success)
            {
                console.BuildSuccess(buildTime, buildResult.Stats.FilesCompiled);
            }

            return buildResult;
        }

        private async Task StartRuntimeResourcesAsync(CancellationToken cancellationToken)
        {
            fileWatcher.Start();
            console.WatchStarted(fileCount: 0, options.WatcherOptions.WatchPaths);

            if (options.EnableRepl && repl is not null)
            {
                await repl.StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private void HandleInitialBuildFailure(BuildResult buildResult)
        {
            errorCount++;
            ImmutableArray<BuildDiagnostic> diagnostics = buildResult.Diagnostics.IsDefault
                ? ImmutableArray<BuildDiagnostic>.Empty
                : buildResult.Diagnostics;
            errorOverlay.Show(CreateOverlayErrors(diagnostics));
            console.Error(CreateBuildFailureMessage(diagnostics));
            TransitionTo(DevServerStatus.Error, "Initial build failed.");
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

        private async Task CleanupRuntimeResourcesAsync()
        {
            if (options.EnableRepl && repl is not null)
            {
                await repl.StopAsync().ConfigureAwait(false);
            }

            fileWatcher.Stop();
            errorOverlay.Hide();
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

        private static string CreateBuildFailureMessage(ImmutableArray<BuildDiagnostic> diagnostics)
        {
            if (diagnostics.IsDefaultOrEmpty)
            {
                return "Initial build failed.";
            }

            BuildDiagnostic primary = diagnostics
                .OrderByDescending(static diagnostic => diagnostic.Severity)
                .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .First();
            return "Initial build failed: " + primary.Code + ": " + primary.Message;
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
    }
}
