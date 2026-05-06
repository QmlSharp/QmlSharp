using QmlSharp.Build;
using QmlSharp.Compiler;

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
        private readonly IDevToolsCompiler compiler;
        private readonly CompilerOptions compilerOptions;
        private readonly IHotReloadOrchestrator hotReloadOrchestrator;
        private readonly SchemaDiffer schemaDiffer;
        private readonly IDevConsole console;
        private readonly IErrorOverlay errorOverlay;
        private readonly IPerfProfiler profiler;
        private readonly IDevToolsClock clock;
        private readonly IRepl? repl;
        private readonly Lock rebuildSync = new();
        private DevServerStatus status = DevServerStatus.Idle;
        private int buildCount;
        private int rebuildCount;
        private int hotReloadCount;
        private int errorCount;
        private TimeSpan totalBuildTime;
        private TimeSpan totalHotReloadTime;
        private DateTimeOffset? runningSince;
        private CompilationResult? lastCompilationResult;
        private RebuildRequest? pendingRebuild;
        private Task<HotReloadResult>? activeRebuildTask;
        private bool rebuildInProgress;
        private bool runtimeResourcesStarted;
        private bool fileWatcherSubscribed;
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
                new BuildPipelineCompilerAdapter(buildPipeline, buildContext),
                CreateDefaultCompilerOptions(buildContext),
                NoOpHotReloadOrchestrator.Instance,
                console,
                errorOverlay,
                profiler,
                repl: null,
                SystemDevToolsClock.Instance,
                new SchemaDiffer())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DevServer"/> class with compiler and hot-reload services.
        /// </summary>
        /// <param name="options">Dev server options.</param>
        /// <param name="buildContext">Build-system context used for initial builds.</param>
        /// <param name="fileWatcher">Source file watcher owned by the server.</param>
        /// <param name="buildPipeline">Build pipeline facade from 08-build-system.</param>
        /// <param name="compiler">Compiler facade used for manual and incremental rebuilds.</param>
        /// <param name="compilerOptions">Compiler options for the current project.</param>
        /// <param name="hotReloadOrchestrator">Hot reload protocol coordinator.</param>
        /// <param name="console">Developer console output.</param>
        /// <param name="errorOverlay">Error overlay controller.</param>
        /// <param name="profiler">Performance profiler.</param>
        public DevServer(
            DevServerOptions options,
            BuildContext buildContext,
            IFileWatcher fileWatcher,
            IDevToolsBuildPipeline buildPipeline,
            IDevToolsCompiler compiler,
            CompilerOptions compilerOptions,
            IHotReloadOrchestrator hotReloadOrchestrator,
            IDevConsole console,
            IErrorOverlay errorOverlay,
            IPerfProfiler profiler)
            : this(
                options,
                buildContext,
                fileWatcher,
                buildPipeline,
                compiler,
                compilerOptions,
                hotReloadOrchestrator,
                console,
                errorOverlay,
                profiler,
                repl: null,
                SystemDevToolsClock.Instance,
                new SchemaDiffer())
        {
        }

        internal DevServer(
            DevServerOptions options,
            BuildContext buildContext,
            IFileWatcher fileWatcher,
            IDevToolsBuildPipeline buildPipeline,
            IDevToolsCompiler compiler,
            CompilerOptions compilerOptions,
            IHotReloadOrchestrator hotReloadOrchestrator,
            IDevConsole console,
            IErrorOverlay errorOverlay,
            IPerfProfiler profiler,
            IRepl? repl,
            IDevToolsClock clock,
            SchemaDiffer schemaDiffer)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(buildContext);
            ArgumentNullException.ThrowIfNull(fileWatcher);
            ArgumentNullException.ThrowIfNull(buildPipeline);
            ArgumentNullException.ThrowIfNull(compiler);
            ArgumentNullException.ThrowIfNull(compilerOptions);
            ArgumentNullException.ThrowIfNull(hotReloadOrchestrator);
            ArgumentNullException.ThrowIfNull(console);
            ArgumentNullException.ThrowIfNull(errorOverlay);
            ArgumentNullException.ThrowIfNull(profiler);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(schemaDiffer);
            if (string.IsNullOrWhiteSpace(options.ProjectRoot))
            {
                throw new ArgumentException("Project root must be provided.", nameof(options));
            }

            this.options = options;
            this.buildContext = buildContext;
            this.fileWatcher = fileWatcher;
            this.buildPipeline = buildPipeline;
            this.compiler = compiler;
            this.compilerOptions = compilerOptions;
            this.hotReloadOrchestrator = hotReloadOrchestrator;
            this.console = console;
            this.errorOverlay = errorOverlay;
            this.profiler = profiler;
            this.repl = repl;
            this.clock = clock;
            this.schemaDiffer = schemaDiffer;
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
            return await QueueRebuildAsync(RebuildRequest.ManualRequest, cancellationToken).ConfigureAwait(false);
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

                lastCompilationResult = CreateCompilationResultFromBuildResult(buildResult);
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

        private async Task<CompilationResult> RunRebuildCompilationAsync(
            RebuildRequest request,
            CancellationToken cancellationToken)
        {
            long buildStart = clock.GetTimestamp();
            int fileCount = request.Batch?.Changes.Count ?? 0;
            console.BuildStart(fileCount);
            using IDisposable span = profiler.StartSpan("devserver_rebuild_compile", PerfCategory.Build);
            CompilationResult compilationResult = request.Batch is null
                ? await compiler.CompileAsync(compilerOptions, cancellationToken).ConfigureAwait(false)
                : await compiler.CompileChangedAsync(compilerOptions, request.Batch, cancellationToken).ConfigureAwait(false);

            RecordCompilationResult(compilationResult, buildStart);
            return compilationResult;
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

            if (!fileWatcherSubscribed)
            {
                fileWatcher.OnChange += OnFileWatcherChanged;
                fileWatcherSubscribed = true;
            }

            fileWatcher.Start();
            console.WatchStarted(fileCount: 0, options.WatcherOptions.WatchPaths);

            if (options.EnableRepl && repl is not null)
            {
                await repl.StartAsync(cancellationToken).ConfigureAwait(false);
            }

            runtimeResourcesStarted = true;
        }

        private void OnFileWatcherChanged(FileChangeBatch batch)
        {
            ArgumentNullException.ThrowIfNull(batch);

            console.FileChanged(batch);
            Task<HotReloadResult> rebuildTask = QueueRebuildAsync(
                RebuildRequest.FromBatch(batch, IsConfigFileChange(batch)),
                CancellationToken.None);
            _ = ObserveQueuedRebuildAsync(rebuildTask);
        }

        private async Task ObserveQueuedRebuildAsync(Task<HotReloadResult> rebuildTask)
        {
            try
            {
                _ = await rebuildTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is surfaced to the caller for manual rebuilds; watcher work is simply abandoned.
            }
            catch (Exception exception) when (!IsCriticalException(exception))
            {
                errorCount++;
                console.Error("Queued rebuild failed unexpectedly: " + exception.Message);
                TransitionTo(DevServerStatus.Error, "Queued rebuild failed unexpectedly.");
            }
        }

        private Task<HotReloadResult> QueueRebuildAsync(
            RebuildRequest request,
            CancellationToken cancellationToken)
        {
            lock (rebuildSync)
            {
                if (rebuildInProgress)
                {
                    if (!request.Manual)
                    {
                        pendingRebuild = request;
                    }

                    return activeRebuildTask ?? Task.FromResult(CreateRebuildResult(
                        success: false,
                        TimeSpan.Zero,
                        "A rebuild is already in progress."));
                }

                rebuildInProgress = true;
                activeRebuildTask = ProcessRebuildQueueAsync(request, cancellationToken);
                return activeRebuildTask;
            }
        }

        private async Task<HotReloadResult> ProcessRebuildQueueAsync(
            RebuildRequest initialRequest,
            CancellationToken cancellationToken)
        {
            RebuildRequest currentRequest = initialRequest;
            HotReloadResult lastResult = CreateRebuildResult(success: true, TimeSpan.Zero, errorMessage: null);

            try
            {
                while (true)
                {
                    lastResult = await ExecuteRebuildRequestAsync(currentRequest, cancellationToken).ConfigureAwait(false);

                    lock (rebuildSync)
                    {
                        if (pendingRebuild is null)
                        {
                            rebuildInProgress = false;
                            activeRebuildTask = null;
                            return lastResult;
                        }

                        currentRequest = pendingRebuild;
                        pendingRebuild = null;
                    }
                }
            }
            catch
            {
                lock (rebuildSync)
                {
                    rebuildInProgress = false;
                    pendingRebuild = null;
                    activeRebuildTask = null;
                }

                throw;
            }
        }

        private async Task<HotReloadResult> ExecuteRebuildRequestAsync(
            RebuildRequest request,
            CancellationToken cancellationToken)
        {
            using AsyncGate.Releaser gate = await lifecycleGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
            EnsureCanRebuild();
            cancellationToken.ThrowIfCancellationRequested();

            DevServerStatus statusBeforeReload = status;
            long startTimestamp = clock.GetTimestamp();
            TransitionTo(DevServerStatus.Reloading, request.ConfigChanged ? "Config change detected." : "Rebuild starting.");

            try
            {
                if (request.ConfigChanged)
                {
                    return await RestartForConfigChangeAsync(startTimestamp, cancellationToken).ConfigureAwait(false);
                }

                CompilationResult compilationResult = await RunRebuildCompilationAsync(request, cancellationToken)
                    .ConfigureAwait(false);
                TimeSpan elapsed = ElapsedOrOneTick(startTimestamp);
                if (!compilationResult.Success)
                {
                    return HandleCompilationFailure(compilationResult, elapsed);
                }

                if (!runtimeResourcesStarted)
                {
                    return await HandleSuccessfulRebuildBeforeRuntimeStartAsync(
                        compilationResult,
                        elapsed,
                        cancellationToken).ConfigureAwait(false);
                }

                SchemaDiffResult schemaDiff = schemaDiffer.Compare(lastCompilationResult, compilationResult);
                if (schemaDiff.HasStructuralChanges)
                {
                    return await RestartForSchemaChangeAsync(
                        compilationResult,
                        schemaDiff,
                        startTimestamp,
                        cancellationToken).ConfigureAwait(false);
                }

                return await ApplyHotReloadAsync(compilationResult, elapsed, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TransitionTo(statusBeforeReload, "Rebuild canceled.");
                throw;
            }
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

        private HotReloadResult HandleCompilationFailure(CompilationResult compilationResult, TimeSpan elapsed)
        {
            errorCount++;
            ImmutableArray<CompilerDiagnostic> diagnostics = compilationResult.Diagnostics.IsDefault
                ? ImmutableArray<CompilerDiagnostic>.Empty
                : compilationResult.Diagnostics;
            string errorMessage = CreateCompilationFailureMessage("Rebuild failed", diagnostics);
            errorOverlay.Show(CreateOverlayErrors(diagnostics));
            console.BuildError(diagnostics);
            TransitionTo(DevServerStatus.Error, "Rebuild compilation failed.");
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

        private async Task<HotReloadResult> HandleSuccessfulRebuildBeforeRuntimeStartAsync(
            CompilationResult compilationResult,
            TimeSpan elapsed,
            CancellationToken cancellationToken)
        {
            try
            {
                await StartRuntimeResourcesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (!IsCriticalException(exception))
            {
                return await HandleRebuildStartupFailureAsync(exception, elapsed).ConfigureAwait(false);
            }

            lastCompilationResult = compilationResult;
            runningSince = clock.UtcNow;
            errorOverlay.Hide();
            TransitionTo(DevServerStatus.Running, "Rebuild succeeded.");
            return CreateRebuildResult(success: true, elapsed, errorMessage: null);
        }

        private async Task<HotReloadResult> ApplyHotReloadAsync(
            CompilationResult compilationResult,
            TimeSpan elapsed,
            CancellationToken cancellationToken)
        {
            HotReloadResult reloadResult = await hotReloadOrchestrator
                .ReloadAsync(compilationResult, cancellationToken)
                .ConfigureAwait(false);
            if (!reloadResult.Success)
            {
                errorCount++;
                string errorMessage = string.IsNullOrWhiteSpace(reloadResult.ErrorMessage)
                    ? "Hot reload failed."
                    : reloadResult.ErrorMessage;
                errorOverlay.Show(new OverlayError("Hot Reload Error", errorMessage, null, null, null));
                console.HotReloadError(errorMessage);
                TransitionTo(DevServerStatus.Error, "Hot reload failed.");
                return reloadResult;
            }

            lastCompilationResult = compilationResult;
            hotReloadCount++;
            totalHotReloadTime += reloadResult.TotalTime > TimeSpan.Zero ? reloadResult.TotalTime : elapsed;
            errorOverlay.Hide();
            console.HotReloadSuccess(reloadResult);
            runningSince ??= clock.UtcNow;
            TransitionTo(DevServerStatus.Running, "Hot reload succeeded.");
            return reloadResult;
        }

        private async Task<HotReloadResult> RestartForSchemaChangeAsync(
            CompilationResult compilationResult,
            SchemaDiffResult schemaDiff,
            long startTimestamp,
            CancellationToken cancellationToken)
        {
            console.RestartRequired(schemaDiff.RestartReason);
            HotReloadResult restartResult = await RestartCoreFromReloadingAsync(
                "Schema change restart failed",
                startTimestamp,
                cancellationToken).ConfigureAwait(false);
            if (restartResult.Success)
            {
                lastCompilationResult = compilationResult;
            }

            return restartResult;
        }

        private async Task<HotReloadResult> RestartForConfigChangeAsync(
            long startTimestamp,
            CancellationToken cancellationToken)
        {
            console.RestartRequired("Configuration file changed.");
            return await RestartCoreFromReloadingAsync(
                "Configuration restart failed",
                startTimestamp,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<HotReloadResult> RestartCoreFromReloadingAsync(
            string failurePrefix,
            long startTimestamp,
            CancellationToken cancellationToken)
        {
            await StopCoreAsync().ConfigureAwait(false);
            await StartCoreAsync(cancellationToken).ConfigureAwait(false);

            TimeSpan elapsed = ElapsedOrOneTick(startTimestamp);
            if (status != DevServerStatus.Running)
            {
                string message = failurePrefix + ".";
                console.Error(message);
                return CreateRebuildResult(success: false, elapsed, message);
            }

            errorOverlay.Hide();
            return CreateRebuildResult(success: true, elapsed, errorMessage: null);
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

        private void RecordCompilationResult(CompilationResult compilationResult, long buildStart)
        {
            TimeSpan measuredBuildTime = ElapsedOrOneTick(buildStart);
            TimeSpan buildTime = compilationResult.Stats.ElapsedMilliseconds > 0
                ? TimeSpan.FromMilliseconds(compilationResult.Stats.ElapsedMilliseconds)
                : measuredBuildTime;

            rebuildCount++;
            totalBuildTime += buildTime;

            if (compilationResult.Success)
            {
                console.BuildSuccess(buildTime, compilationResult.Stats.TotalFiles);
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
                UnsubscribeFileWatcher();
                errorOverlay.Hide();
                return;
            }

            if (options.EnableRepl && repl is not null)
            {
                await repl.StopAsync().ConfigureAwait(false);
            }

            UnsubscribeFileWatcher();
            fileWatcher.Stop();
            errorOverlay.Hide();
            runtimeResourcesStarted = false;
        }

        private async Task CleanupRuntimeResourcesForFailedStartupAsync()
        {
            try
            {
                UnsubscribeFileWatcher();
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

        private void UnsubscribeFileWatcher()
        {
            if (!fileWatcherSubscribed)
            {
                return;
            }

            fileWatcher.OnChange -= OnFileWatcherChanged;
            fileWatcherSubscribed = false;
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

        private static ImmutableArray<OverlayError> CreateOverlayErrors(ImmutableArray<CompilerDiagnostic> diagnostics)
        {
            ImmutableArray<CompilerDiagnostic> normalizedDiagnostics = diagnostics.IsDefault
                ? ImmutableArray<CompilerDiagnostic>.Empty
                : diagnostics;
            ImmutableArray<OverlayError> errors = ErrorOverlayDiagnosticMapper.MapDiagnostics(normalizedDiagnostics);
            return errors.IsEmpty
                ? ImmutableArray.Create(new OverlayError("Compilation Error", "Compilation failed.", null, null, null))
                : errors;
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

        private static string CreateCompilationFailureMessage(
            string failurePrefix,
            ImmutableArray<CompilerDiagnostic> diagnostics)
        {
            if (diagnostics.IsDefaultOrEmpty)
            {
                return failurePrefix + ".";
            }

            CompilerDiagnostic primary = diagnostics
                .OrderByDescending(static diagnostic => diagnostic.Severity)
                .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
                .First();
            return failurePrefix + ": " + primary.Code + ": " + primary.Message;
        }

        private bool IsConfigFileChange(FileChangeBatch batch)
        {
            string configPath = ResolveProjectPath(options.ConfigPath ?? "qmlsharp.json");
            foreach (FileChange change in batch.Changes)
            {
                string changedPath = ResolveProjectPath(change.FilePath);
                if (string.Equals(changedPath, configPath, PathComparison))
                {
                    return true;
                }
            }

            return false;
        }

        private string ResolveProjectPath(string path)
        {
            return Path.GetFullPath(IsRootedPath(path) ? path : Path.Join(options.ProjectRoot, path));
        }

        private static bool IsRootedPath(string path)
        {
            return Path.IsPathRooted(path) ||
                (path.Length >= 3 &&
                char.IsLetter(path[0]) &&
                path[1] == ':' &&
                (path[2] == '/' || path[2] == '\\'));
        }

        private static StringComparison PathComparison => OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        private static CompilerOptions CreateDefaultCompilerOptions(BuildContext buildContext)
        {
            return new CompilerOptions
            {
                ProjectPath = Path.Join(buildContext.ProjectDir, "qmlsharp.csproj"),
                OutputDir = buildContext.OutputDir,
                ModuleUriPrefix = buildContext.Config.Module.Prefix,
                ModuleVersion = new QmlSharp.Compiler.QmlVersion(
                    buildContext.Config.Module.Version.Major,
                    buildContext.Config.Module.Version.Minor),
                GenerateSourceMaps = buildContext.Config.Build.SourceMaps,
                FormatQml = buildContext.Config.Build.Format,
                LintQml = buildContext.Config.Build.Lint,
                Incremental = buildContext.Config.Build.Incremental,
            };
        }

        private static CompilationResult CreateCompilationResultFromBuildResult(BuildResult buildResult)
        {
            ImmutableArray<CompilerDiagnostic> diagnostics = ConvertDiagnostics(buildResult.Diagnostics);
            if (!buildResult.Success)
            {
                return CompilationResult.FromUnits(ImmutableArray<CompilationUnit>.Empty, diagnostics);
            }

            ImmutableArray<string> schemaFiles = buildResult.Artifacts.SchemaFiles.IsDefault
                ? ImmutableArray<string>.Empty
                : buildResult.Artifacts.SchemaFiles;
            if (schemaFiles.IsEmpty)
            {
                return CompilationResult.FromUnits(ImmutableArray<CompilationUnit>.Empty, diagnostics);
            }

            ViewModelSchemaSerializer serializer = new();
            ImmutableArray<CompilationUnit>.Builder units = ImmutableArray.CreateBuilder<CompilationUnit>();
            ImmutableArray<CompilerDiagnostic>.Builder allDiagnostics = ImmutableArray.CreateBuilder<CompilerDiagnostic>();
            allDiagnostics.AddRange(diagnostics);

            foreach (string schemaFile in schemaFiles.OrderBy(static path => path, StringComparer.Ordinal))
            {
                try
                {
                    ViewModelSchema schema = serializer.Deserialize(File.ReadAllText(schemaFile));
                    units.Add(new CompilationUnit
                    {
                        SourceFilePath = schemaFile,
                        ViewClassName = schema.ClassName,
                        ViewModelClassName = schema.ClassName,
                        Schema = schema,
                    });
                }
                catch (Exception exception) when (!IsCriticalException(exception))
                {
                    allDiagnostics.Add(new CompilerDiagnostic(
                        DiagnosticCodes.SchemaSerializationFailed,
                        DiagnosticSeverity.Error,
                        "Schema file could not be read for dev-server schema diffing: " + exception.Message,
                        SourceLocation.FileOnly(schemaFile),
                        Phase: "DevServer"));
                }
            }

            return CompilationResult.FromUnits(units.ToImmutable(), allDiagnostics.ToImmutable());
        }

        private static ImmutableArray<CompilerDiagnostic> ConvertDiagnostics(ImmutableArray<BuildDiagnostic> diagnostics)
        {
            if (diagnostics.IsDefaultOrEmpty)
            {
                return ImmutableArray<CompilerDiagnostic>.Empty;
            }

            return diagnostics
                .Select(static diagnostic => new CompilerDiagnostic(
                    diagnostic.Code,
                    ConvertSeverity(diagnostic.Severity),
                    diagnostic.Message,
                    string.IsNullOrWhiteSpace(diagnostic.FilePath) ? null : SourceLocation.FileOnly(diagnostic.FilePath),
                    diagnostic.Phase?.ToString()))
                .ToImmutableArray();
        }

        private static DiagnosticSeverity ConvertSeverity(BuildDiagnosticSeverity severity)
        {
            return severity switch
            {
                BuildDiagnosticSeverity.Info => DiagnosticSeverity.Info,
                BuildDiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
                BuildDiagnosticSeverity.Error => DiagnosticSeverity.Error,
                BuildDiagnosticSeverity.Fatal => DiagnosticSeverity.Fatal,
                _ => DiagnosticSeverity.Error,
            };
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

        private sealed record RebuildRequest(FileChangeBatch? Batch, bool ConfigChanged, bool Manual)
        {
            public static RebuildRequest ManualRequest { get; } = new(Batch: null, ConfigChanged: false, Manual: true);

            public static RebuildRequest FromBatch(FileChangeBatch batch, bool configChanged)
            {
                return new RebuildRequest(batch, configChanged, Manual: false);
            }
        }

        private sealed class BuildPipelineCompilerAdapter : IDevToolsCompiler
        {
            private readonly IDevToolsBuildPipeline buildPipeline;
            private readonly BuildContext buildContext;

            public BuildPipelineCompilerAdapter(IDevToolsBuildPipeline buildPipeline, BuildContext buildContext)
            {
                ArgumentNullException.ThrowIfNull(buildPipeline);
                ArgumentNullException.ThrowIfNull(buildContext);

                this.buildPipeline = buildPipeline;
                this.buildContext = buildContext;
            }

            public Task<CompilationResult> CompileAsync(
                CompilerOptions options,
                CancellationToken cancellationToken = default)
            {
                return CompileWithBuildPipelineAsync(cancellationToken);
            }

            public Task<CompilationResult> CompileChangedAsync(
                CompilerOptions options,
                FileChangeBatch changes,
                CancellationToken cancellationToken = default)
            {
                ArgumentNullException.ThrowIfNull(changes);

                return CompileWithBuildPipelineAsync(cancellationToken);
            }

            private async Task<CompilationResult> CompileWithBuildPipelineAsync(CancellationToken cancellationToken)
            {
                BuildResult buildResult = await buildPipeline
                    .BuildAsync(buildContext, cancellationToken)
                    .ConfigureAwait(false);
                return CreateCompilationResultFromBuildResult(buildResult);
            }
        }

        private sealed class NoOpHotReloadOrchestrator : IHotReloadOrchestrator
        {
            public static NoOpHotReloadOrchestrator Instance { get; } = new();

            public event Action<HotReloadStartingEvent> OnBefore = static _ => { };

            public event Action<HotReloadCompletedEvent> OnAfter = static _ => { };

            public Task<HotReloadResult> ReloadAsync(
                CompilationResult result,
                CancellationToken cancellationToken = default)
            {
                ArgumentNullException.ThrowIfNull(result);
                cancellationToken.ThrowIfCancellationRequested();

                OnBefore(new HotReloadStartingEvent(OldInstanceCount: 0, DateTimeOffset.UtcNow));
                HotReloadResult reloadResult = new(
                    Success: true,
                    InstancesMatched: 0,
                    InstancesOrphaned: 0,
                    InstancesNew: 0,
                    new HotReloadPhases(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
                    TotalTime: TimeSpan.Zero,
                    ErrorMessage: null,
                    FailedStep: null);
                OnAfter(new HotReloadCompletedEvent(reloadResult, DateTimeOffset.UtcNow));
                return Task.FromResult(reloadResult);
            }
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
