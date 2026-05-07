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
        private readonly IDevToolsConfigLoader? configLoader;
        private readonly Func<FileWatcherOptions, IFileWatcher>? fileWatcherFactory;
        private readonly IDevToolsBuildPipeline buildPipeline;
        private readonly IHotReloadOrchestrator hotReloadOrchestrator;
        private readonly SchemaDiffer schemaDiffer;
        private readonly IDevConsole console;
        private readonly IErrorOverlay errorOverlay;
        private readonly IPerfProfiler profiler;
        private readonly IDevToolsClock clock;
        private readonly IRepl? repl;
        private readonly bool refreshCompilerAdapterOnConfigReload;
        private readonly Lock rebuildSync = new();
        private DevServerOptions options;
        private BuildContext buildContext;
        private IFileWatcher fileWatcher;
        private IDevToolsCompiler compiler;
        private CompilerOptions compilerOptions;
        private CancellationTokenSource watcherRebuildCancellation = new();
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
                new SchemaDiffer(),
                new DevToolsConfigLoaderAdapter(new ConfigLoader()),
                static watcherOptions => new FileWatcher(watcherOptions),
                refreshCompilerAdapterOnConfigReload: true)
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
                new SchemaDiffer(),
                new DevToolsConfigLoaderAdapter(new ConfigLoader()),
                static watcherOptions => new FileWatcher(watcherOptions),
                refreshCompilerAdapterOnConfigReload: false)
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
            SchemaDiffer schemaDiffer,
            IDevToolsConfigLoader? configLoader = null,
            Func<FileWatcherOptions, IFileWatcher>? fileWatcherFactory = null,
            bool refreshCompilerAdapterOnConfigReload = false)
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
            this.configLoader = configLoader;
            this.fileWatcherFactory = fileWatcherFactory;
            this.refreshCompilerAdapterOnConfigReload = refreshCompilerAdapterOnConfigReload;
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
            ResetWatcherRebuildCancellation();
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

        private async Task StopCoreAsync(bool cancelQueuedWatcherRebuilds = true)
        {
            if (cancelQueuedWatcherRebuilds)
            {
                CancelQueuedWatcherRebuilds();
            }
            else
            {
                ClearPendingRebuilds();
            }

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

            CancellationToken cancellationToken = watcherRebuildCancellation.Token;
            if (disposed ||
                cancellationToken.IsCancellationRequested ||
                status is DevServerStatus.Idle or DevServerStatus.Stopping)
            {
                return;
            }

            console.FileChanged(batch);
            Task<HotReloadResult> rebuildTask = QueueRebuildAsync(
                RebuildRequest.FromBatch(batch, IsConfigFileChange(batch)),
                cancellationToken);
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
            HotReloadResult lastResult;

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
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
            DevServerRuntimeConfiguration? reloadedConfiguration;
            try
            {
                reloadedConfiguration = ReloadConfigurationForRestart();
            }
            catch (ConfigParseException exception)
            {
                return HandleConfigReloadFailure(exception, startTimestamp);
            }

            return await RestartCoreFromReloadingAsync(
                "Configuration restart failed",
                startTimestamp,
                cancellationToken,
                reloadedConfiguration).ConfigureAwait(false);
        }

        private async Task<HotReloadResult> RestartCoreFromReloadingAsync(
            string failurePrefix,
            long startTimestamp,
            CancellationToken cancellationToken,
            DevServerRuntimeConfiguration? reloadedConfiguration = null)
        {
            await StopCoreAsync(cancelQueuedWatcherRebuilds: false).ConfigureAwait(false);
            if (reloadedConfiguration is not null)
            {
                ApplyRuntimeConfiguration(reloadedConfiguration);
            }

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

        private DevServerRuntimeConfiguration? ReloadConfigurationForRestart()
        {
            if (configLoader is null)
            {
                return null;
            }

            string projectRoot = NormalizeProjectRoot(options.ProjectRoot);
            QmlSharpConfig loadedConfig = configLoader.Load(projectRoot);
            ImmutableArray<ConfigDiagnostic> diagnostics = configLoader.Validate(loadedConfig);
            ImmutableArray<ConfigDiagnostic> errors = diagnostics
                .Where(static diagnostic => diagnostic.Severity == ConfigDiagnosticSeverity.Error)
                .ToImmutableArray();
            if (!errors.IsEmpty)
            {
                throw new ConfigParseException(ConvertConfigDiagnostics(errors));
            }

            QmlSharpConfig normalizedConfig = ApplyEntryOverride(
                NormalizeConfigForDevServer(loadedConfig, projectRoot),
                projectRoot,
                options.EntryOverride);
            DevServerOptions reloadedOptions = options with
            {
                ProjectRoot = projectRoot,
                EntryOverride = string.IsNullOrWhiteSpace(options.EntryOverride) ? null : normalizedConfig.Entry,
                WatcherOptions = options.WatcherOptions with
                {
                    WatchPaths = normalizedConfig.Dev.WatchPaths,
                    DebounceMs = normalizedConfig.Dev.DebounceMs,
                },
            };
            BuildContext reloadedContext = buildContext with
            {
                Config = normalizedConfig,
                ProjectDir = projectRoot,
                OutputDir = normalizedConfig.OutDir,
                QtDir = normalizedConfig.Qt.Dir ?? string.Empty,
            };
            return new DevServerRuntimeConfiguration(
                reloadedOptions,
                reloadedContext,
                CreateDefaultCompilerOptions(reloadedContext));
        }

        private void ApplyRuntimeConfiguration(DevServerRuntimeConfiguration configuration)
        {
            bool watcherOptionsChanged = !FileWatcherOptionsEqual(options.WatcherOptions, configuration.Options.WatcherOptions);
            options = configuration.Options;
            buildContext = configuration.BuildContext;
            compilerOptions = configuration.CompilerOptions;
            if (refreshCompilerAdapterOnConfigReload)
            {
                compiler = new BuildPipelineCompilerAdapter(buildPipeline, buildContext);
            }

            if (watcherOptionsChanged && fileWatcherFactory is not null)
            {
                fileWatcher.Dispose();
                fileWatcher = fileWatcherFactory(configuration.Options.WatcherOptions);
                fileWatcherSubscribed = false;
            }
        }

        private HotReloadResult HandleConfigReloadFailure(ConfigParseException exception, long startTimestamp)
        {
            errorCount++;
            ImmutableArray<BuildDiagnostic> diagnostics = exception.Diagnostics.IsDefault
                ? ImmutableArray<BuildDiagnostic>.Empty
                : exception.Diagnostics;
            TimeSpan elapsed = ElapsedOrOneTick(startTimestamp);
            errorOverlay.Show(CreateOverlayErrors(diagnostics));
            string errorMessage = CreateBuildFailureMessage("Configuration restart failed", diagnostics);
            console.Error(errorMessage);
            TransitionTo(DevServerStatus.Error, "Configuration reload failed.");
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
            watcherRebuildCancellation.Dispose();
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
            return batch.Changes
                .Select(static change => change.FilePath)
                .Select(ResolveProjectPath)
                .Any(changedPath => string.Equals(changedPath, configPath, PathComparison));
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

        private void CancelQueuedWatcherRebuilds()
        {
            try
            {
                watcherRebuildCancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Disposal wins during shutdown; queued watcher work is already no longer valid.
            }

            ClearPendingRebuilds();
        }

        private void ClearPendingRebuilds()
        {
            lock (rebuildSync)
            {
                pendingRebuild = null;
            }
        }

        private void ResetWatcherRebuildCancellation()
        {
            if (!watcherRebuildCancellation.IsCancellationRequested)
            {
                return;
            }

            watcherRebuildCancellation.Dispose();
            watcherRebuildCancellation = new CancellationTokenSource();
        }

        private static QmlSharpConfig NormalizeConfigForDevServer(QmlSharpConfig config, string projectRoot)
        {
            return config with
            {
                OutDir = ResolveProjectRootedPath(projectRoot, config.OutDir),
                Dev = config.Dev with
                {
                    WatchPaths = ResolveWatchPaths(projectRoot, config.Dev.WatchPaths),
                },
            };
        }

        private static QmlSharpConfig ApplyEntryOverride(
            QmlSharpConfig config,
            string projectRoot,
            string? entryOverride)
        {
            return string.IsNullOrWhiteSpace(entryOverride)
                ? config
                : config with { Entry = ResolveProjectRootedPath(projectRoot, entryOverride) };
        }

        private static ImmutableArray<string> ResolveWatchPaths(string projectRoot, ImmutableArray<string> watchPaths)
        {
            ImmutableArray<string> paths = watchPaths.IsDefaultOrEmpty
                ? ImmutableArray.Create("./src")
                : watchPaths;
            return paths
                .Select(path => ResolveProjectRootedPath(projectRoot, path))
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static string ResolveProjectRootedPath(string projectRoot, string path)
        {
            if (IsRootedPath(path))
            {
                return path;
            }

            return IsWindowsDriveRootedPath(projectRoot)
                ? CombineWindowsDriveRootedPath(projectRoot, path)
                : Path.GetFullPath(Path.Join(projectRoot, path));
        }

        private static string NormalizeProjectRoot(string projectRoot)
        {
            return IsRootedPath(projectRoot) ? projectRoot : Path.GetFullPath(projectRoot);
        }

        private static bool IsWindowsDriveRootedPath(string path)
        {
            return path.Length >= 3 &&
                char.IsLetter(path[0]) &&
                path[1] == ':' &&
                (path[2] == '/' || path[2] == '\\');
        }

        private static string CombineWindowsDriveRootedPath(string projectRoot, string path)
        {
            string normalizedPath = path.Replace('\\', '/').TrimStart('/');
            while (normalizedPath.StartsWith("./", StringComparison.Ordinal))
            {
                normalizedPath = normalizedPath[2..];
            }

            return projectRoot.Replace('\\', '/').TrimEnd('/') + "/" + normalizedPath;
        }

        private static ImmutableArray<BuildDiagnostic> ConvertConfigDiagnostics(ImmutableArray<ConfigDiagnostic> diagnostics)
        {
            return diagnostics
                .Select(static diagnostic => new BuildDiagnostic(
                    diagnostic.Code,
                    ConvertConfigSeverity(diagnostic.Severity),
                    diagnostic.Message,
                    BuildPhase.ConfigLoading,
                    diagnostic.Field))
                .ToImmutableArray();
        }

        private static BuildDiagnosticSeverity ConvertConfigSeverity(ConfigDiagnosticSeverity severity)
        {
            return severity == ConfigDiagnosticSeverity.Warning
                ? BuildDiagnosticSeverity.Warning
                : BuildDiagnosticSeverity.Error;
        }

        private static bool FileWatcherOptionsEqual(FileWatcherOptions left, FileWatcherOptions right)
        {
            return left.DebounceMs == right.DebounceMs &&
                left.UsePolling == right.UsePolling &&
                left.PollIntervalMs == right.PollIntervalMs &&
                SequenceEqual(left.WatchPaths, right.WatchPaths) &&
                SequenceEqual(left.IncludePatterns, right.IncludePatterns) &&
                SequenceEqual(left.ExcludePatterns, right.ExcludePatterns);
        }

        private static bool SequenceEqual(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
        {
            IReadOnlyList<string> leftValues = left ?? ImmutableArray<string>.Empty;
            IReadOnlyList<string> rightValues = right ?? ImmutableArray<string>.Empty;
            return leftValues.SequenceEqual(rightValues, StringComparer.Ordinal);
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

        private sealed record DevServerRuntimeConfiguration(
            DevServerOptions Options,
            BuildContext BuildContext,
            CompilerOptions CompilerOptions);

        private sealed class DevToolsConfigLoaderAdapter : IDevToolsConfigLoader
        {
            private readonly IConfigLoader configLoader;

            public DevToolsConfigLoaderAdapter(IConfigLoader configLoader)
            {
                ArgumentNullException.ThrowIfNull(configLoader);

                this.configLoader = configLoader;
            }

            public QmlSharpConfig Load(string projectDir)
            {
                return configLoader.Load(projectDir);
            }

            public ImmutableArray<ConfigDiagnostic> Validate(QmlSharpConfig config)
            {
                return configLoader.Validate(config);
            }

            public QmlSharpConfig GetDefaults()
            {
                return configLoader.GetDefaults();
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
