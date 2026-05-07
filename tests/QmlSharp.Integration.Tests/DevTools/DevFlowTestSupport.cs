using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Security;
using System.Text.Json;
using QmlSharp.Build;
using QmlSharp.Compiler;
using QmlSharp.DevTools;
using QmlSharp.Host.InstanceRegistry;
using QmlSharp.Host.Instances;
using QmlSharp.Host.Metrics;
using QmlSharp.Qt.Tools;

namespace QmlSharp.Integration.Tests.DevTools
{
    internal enum DevFlowWatcherKind
    {
        Controlled,
        Polling,
    }

    internal sealed class DevFlowHarness : IAsyncDisposable
    {
        private DevFlowHarness(
            DevFlowFixtureProject fixture,
            DevServer server,
            IFileWatcher fileWatcher,
            DevFlowBuildPipelineAdapter buildPipeline,
            DevFlowCompilerAdapter compiler,
            RecordingNativeHost nativeHost,
            RecordingErrorOverlayNativeHost overlayHost,
            ErrorOverlay overlay,
            PerfProfiler profiler,
            StringWriter consoleOutput,
            CompilerOptions compilerOptions)
        {
            Fixture = fixture;
            Server = server;
            FileWatcher = fileWatcher;
            BuildPipeline = buildPipeline;
            Compiler = compiler;
            NativeHost = nativeHost;
            OverlayHost = overlayHost;
            Overlay = overlay;
            Profiler = profiler;
            ConsoleOutput = consoleOutput;
            CompilerOptions = compilerOptions;
        }

        public DevFlowFixtureProject Fixture { get; }

        public DevServer Server { get; }

        public IFileWatcher FileWatcher { get; }

        public DevFlowBuildPipelineAdapter BuildPipeline { get; }

        public DevFlowCompilerAdapter Compiler { get; }

        public RecordingNativeHost NativeHost { get; }

        public RecordingErrorOverlayNativeHost OverlayHost { get; }

        public ErrorOverlay Overlay { get; }

        public PerfProfiler Profiler { get; }

        public StringWriter ConsoleOutput { get; }

        public CompilerOptions CompilerOptions { get; }

        public ControlledFileWatcher ControlledWatcher =>
            FileWatcher as ControlledFileWatcher ??
            throw new InvalidOperationException("The harness was not created with a controlled watcher.");

        public static DevFlowHarness Create(DevFlowWatcherKind watcherKind = DevFlowWatcherKind.Controlled)
        {
            DevFlowFixtureProject fixture = DevFlowFixtureProject.Copy();
            QmlSharpConfig config = new ConfigLoader().Load(fixture.ProjectDirectory);
            BuildContext buildContext = new()
            {
                Config = config,
                ProjectDir = fixture.ProjectDirectory,
                OutputDir = config.OutDir,
                QtDir = config.Qt.Dir ?? fixture.QtDir,
                ForceRebuild = true,
                LibraryMode = false,
            };

            BuildPipeline innerBuildPipeline = DevFlowBuildPipelineFactory.Create(fixture);
            DevFlowBuildPipelineAdapter buildPipeline = new(innerBuildPipeline);
            CompilerOptions compilerOptions = CreateCompilerOptions(fixture, config);
            DevFlowCompilerAdapter compiler = new();
            PerfProfiler profiler = new();
            RecordingNativeHost nativeHost = new();
            HotReloadOrchestrator hotReload = new(nativeHost, profiler);
            RecordingErrorOverlayNativeHost overlayHost = new();
            ErrorOverlay overlay = new(overlayHost);
            StringWriter consoleOutput = new(CultureInfo.InvariantCulture);
            DevConsoleOptions consoleOptions = new(LogLevel.Debug, Color: false, ShowTimestamps: false, consoleOutput);
            DevConsole console = new(consoleOptions);
            IFileWatcher fileWatcher = CreateWatcher(watcherKind, config);
            DevServerOptions serverOptions = new(
                fixture.ProjectDirectory,
                CreateWatcherOptions(config, watcherKind),
                consoleOptions,
                EnableRepl: true,
                EnableProfiling: true,
                ConfigPath: fixture.ConfigPath,
                Headless: true);
            DevServer server = new(
                serverOptions,
                buildContext,
                fileWatcher,
                buildPipeline,
                compiler,
                compilerOptions,
                hotReload,
                console,
                overlay,
                profiler);

            return new DevFlowHarness(
                fixture,
                server,
                fileWatcher,
                buildPipeline,
                compiler,
                nativeHost,
                overlayHost,
                overlay,
                profiler,
                consoleOutput,
                compilerOptions);
        }

        public async ValueTask DisposeAsync()
        {
            await Server.DisposeAsync().ConfigureAwait(false);
            ConsoleOutput.Dispose();
            Fixture.Dispose();
        }

        public async Task StartAsync()
        {
            await Server.StartAsync().ConfigureAwait(false);
            Assert.Equal(DevServerStatus.Running, Server.Status);
            Assert.True(BuildPipeline.Requests.Count >= 1, "Initial dev-server build did not run.");
        }

        private static IFileWatcher CreateWatcher(DevFlowWatcherKind watcherKind, QmlSharpConfig config)
        {
            FileWatcherOptions watcherOptions = CreateWatcherOptions(config, watcherKind);
            return watcherKind == DevFlowWatcherKind.Polling
                ? new FileWatcher(watcherOptions)
                : new ControlledFileWatcher();
        }

        private static FileWatcherOptions CreateWatcherOptions(QmlSharpConfig config, DevFlowWatcherKind watcherKind)
        {
            return new FileWatcherOptions(
                config.Dev.WatchPaths,
                config.Dev.DebounceMs,
                IncludePatterns: ImmutableArray.Create("**/*.cs"),
                ExcludePatterns: ImmutableArray.Create("**/bin/**", "**/obj/**"),
                UsePolling: watcherKind == DevFlowWatcherKind.Polling,
                PollIntervalMs: 50);
        }

        private static CompilerOptions CreateCompilerOptions(DevFlowFixtureProject fixture, QmlSharpConfig config)
        {
            return new CompilerOptions
            {
                ProjectPath = fixture.ProjectFilePath,
                OutputDir = config.OutDir,
                SourceMapDir = Path.Join(config.OutDir, "source-maps"),
                GenerateSourceMaps = config.Build.SourceMaps,
                FormatQml = false,
                LintQml = false,
                ModuleUriPrefix = config.Module.Prefix,
                ModuleVersion = new QmlSharp.Compiler.QmlVersion(
                    config.Module.Version.Major,
                    config.Module.Version.Minor),
                Incremental = config.Build.Incremental,
            };
        }
    }

    internal sealed class DevFlowFixtureProject : IDisposable
    {
        private const string FixtureName = "dev-tools-counter-flow";
        private const string TempRootName = "qmlsharp-step09-11";

        private DevFlowFixtureProject(
            string sourceDirectory,
            string projectDirectory,
            string repositoryRoot,
            string qtDir)
        {
            SourceDirectory = sourceDirectory;
            ProjectDirectory = projectDirectory;
            RepositoryRoot = repositoryRoot;
            QtDir = qtDir;
        }

        public string SourceDirectory { get; }

        public string ProjectDirectory { get; }

        public string RepositoryRoot { get; }

        public string QtDir { get; }

        public string ConfigPath => Path.Join(ProjectDirectory, "qmlsharp.json");

        public string ProjectFilePath => Path.Join(ProjectDirectory, "DevToolsCounterFlow.csproj");

        public string CounterViewPath => Path.Join(ProjectDirectory, "src", "CounterView.cs");

        public string CounterViewModelPath => Path.Join(ProjectDirectory, "src", "CounterViewModel.cs");

        public string ReplHistoryPath => Path.Join(ProjectDirectory, ".qmlsharp", "repl-history.json");

        public static DevFlowFixtureProject Copy()
        {
            string repositoryRoot = FindRepositoryRoot();
            string sourceDirectory = Path.Join(repositoryRoot, "tests", "fixtures", "projects", FixtureName);
            if (!Directory.Exists(sourceDirectory))
            {
                throw new DirectoryNotFoundException($"Step 09.11 fixture project was not found at '{sourceDirectory}'.");
            }

            string qtDir = RequireQtDir();
            string tempRoot = Path.Join(
                Path.GetTempPath(),
                TempRootName,
                FixtureName + "-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            CopyDirectory(sourceDirectory, tempRoot);
            ReplaceQtDirToken(tempRoot, qtDir);
            WriteRepositoryRootProps(tempRoot, repositoryRoot);
            return new DevFlowFixtureProject(sourceDirectory, tempRoot, repositoryRoot, qtDir);
        }

        public void ApplyChangedView()
        {
            ApplyVariant("CounterView.changed.cs.txt", CounterViewPath);
        }

        public void ApplyBrokenViewModel()
        {
            ApplyVariant("CounterViewModel.broken.cs.txt", CounterViewModelPath);
        }

        public void ApplyFixedViewModel()
        {
            ApplyVariant("CounterViewModel.fixed.cs.txt", CounterViewModelPath);
        }

        public void ApplySchemaChangeViewModel()
        {
            ApplyVariant("CounterViewModel.schema-change.cs.txt", CounterViewModelPath);
        }

        public void AppendRapidViewChange(int sequence)
        {
            File.AppendAllText(
                CounterViewPath,
                Environment.NewLine + "// rapid-change-" + sequence.ToString(CultureInfo.InvariantCulture));
        }

        public void AppendConcurrentViewChange(string marker)
        {
            File.AppendAllText(CounterViewPath, Environment.NewLine + "// " + marker);
        }

        public FileChangeBatch CreateBatch(string filePath)
        {
            string fullPath = Path.GetFullPath(filePath);
            DateTimeOffset timestamp = DateTimeOffset.UtcNow;
            return new FileChangeBatch(
                ImmutableArray.Create(new FileChange(fullPath, FileChangeKind.Modified, timestamp)),
                timestamp,
                timestamp);
        }

        public void Dispose()
        {
            if (!Directory.Exists(ProjectDirectory))
            {
                return;
            }

            string tempRoot = Path.GetFullPath(Path.Join(Path.GetTempPath(), TempRootName));
            string projectRoot = Path.GetFullPath(ProjectDirectory);
            string rootPrefix = tempRoot.EndsWith(Path.DirectorySeparatorChar)
                ? tempRoot
                : tempRoot + Path.DirectorySeparatorChar;
            if (!projectRoot.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to delete a Step 09.11 fixture directory outside the test temp root.");
            }

            Directory.Delete(projectRoot, recursive: true);
        }

        private void ApplyVariant(string variantFileName, string targetPath)
        {
            string sourcePath = Path.Join(ProjectDirectory, "variants", variantFileName);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Step 09.11 fixture variant was not found.", sourcePath);
            }

            File.Copy(sourcePath, targetPath, overwrite: true);
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            _ = Directory.CreateDirectory(destinationDirectory);
            foreach (string directory in Directory
                .EnumerateDirectories(sourceDirectory)
                .OrderBy(static path => path, StringComparer.Ordinal))
            {
                CopyDirectory(directory, Path.Join(destinationDirectory, Path.GetFileName(directory)));
            }

            foreach (string file in Directory
                .EnumerateFiles(sourceDirectory)
                .OrderBy(static path => path, StringComparer.Ordinal))
            {
                File.Copy(file, Path.Join(destinationDirectory, Path.GetFileName(file)), overwrite: true);
            }
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? current = new(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Join(current.FullName, "QmlSharp.slnx")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root containing QmlSharp.slnx.");
        }

        private static string RequireQtDir()
        {
            string? qtDir = Environment.GetEnvironmentVariable("QT_DIR");
            if (string.IsNullOrWhiteSpace(qtDir))
            {
                throw new InvalidOperationException("QT_DIR must be set for Step 09.11 dev-flow integration fixtures.");
            }

            string fullQtDir = Path.GetFullPath(qtDir);
            if (!Directory.Exists(fullQtDir))
            {
                throw new DirectoryNotFoundException($"QT_DIR does not exist for Step 09.11 dev-flow integration fixtures: '{fullQtDir}'.");
            }

            return fullQtDir;
        }

        private static void ReplaceQtDirToken(string projectDirectory, string qtDir)
        {
            string escapedQtDir = JsonSerializer.Serialize(qtDir).Trim('"');
            foreach (string filePath in Directory.EnumerateFiles(projectDirectory, "*.json", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(filePath);
                if (text.Contains("__QT_DIR__", StringComparison.Ordinal))
                {
                    File.WriteAllText(filePath, text.Replace("__QT_DIR__", escapedQtDir, StringComparison.Ordinal));
                }
            }
        }

        private static void WriteRepositoryRootProps(string projectDirectory, string repositoryRoot)
        {
            string escapedRoot = SecurityElement.Escape(repositoryRoot) ??
                throw new InvalidOperationException("Repository root could not be escaped for MSBuild props.");
            File.WriteAllText(
                Path.Join(projectDirectory, "Directory.Build.props"),
                $"""
                <Project>
                  <PropertyGroup>
                    <QmlSharpRepoRoot>{escapedRoot}</QmlSharpRepoRoot>
                  </PropertyGroup>
                </Project>
                """);
        }
    }

    internal sealed class ControlledFileWatcher : IFileWatcher
    {
        public event Action<FileChangeBatch> OnChange = static _ => { };

        public int StartCalls { get; private set; }

        public int StopCalls { get; private set; }

        public int DisposeCalls { get; private set; }

        public FileWatcherStatus Status { get; private set; } = FileWatcherStatus.Idle;

        public void Start()
        {
            ObjectDisposedException.ThrowIf(Status == FileWatcherStatus.Disposed, this);
            StartCalls++;
            Status = FileWatcherStatus.Running;
        }

        public void Stop()
        {
            if (Status == FileWatcherStatus.Disposed)
            {
                return;
            }

            StopCalls++;
            Status = FileWatcherStatus.Idle;
        }

        public void Dispose()
        {
            if (Status == FileWatcherStatus.Disposed)
            {
                return;
            }

            DisposeCalls++;
            Status = FileWatcherStatus.Disposed;
        }

        public void Emit(FileChangeBatch batch)
        {
            ArgumentNullException.ThrowIfNull(batch);
            if (Status != FileWatcherStatus.Running)
            {
                throw new InvalidOperationException("Controlled watcher must be running before emitting changes.");
            }

            OnChange(batch);
        }
    }

    internal sealed class DevFlowCompilerAdapter : IDevToolsCompiler
    {
        private readonly object syncRoot = new();
        private readonly QmlCompiler compiler = new();
        private readonly List<DevFlowCompileRequest> requests = new();
        private int activeCompiles;

        public Func<DevFlowCompileRequest, CancellationToken, Task>? BeforeCompileAsync { get; set; }

        public IReadOnlyList<DevFlowCompileRequest> Requests
        {
            get
            {
                lock (syncRoot)
                {
                    return requests.ToImmutableArray();
                }
            }
        }

        public int ActiveCompiles => Volatile.Read(ref activeCompiles);

        public Task<CompilationResult> CompileAsync(
            CompilerOptions options,
            CancellationToken cancellationToken = default)
        {
            return CompileCoreAsync(new DevFlowCompileRequest(options, Changes: null), cancellationToken);
        }

        public Task<CompilationResult> CompileChangedAsync(
            CompilerOptions options,
            FileChangeBatch changes,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(changes);
            return CompileCoreAsync(new DevFlowCompileRequest(options, changes), cancellationToken);
        }

        private async Task<CompilationResult> CompileCoreAsync(
            DevFlowCompileRequest request,
            CancellationToken cancellationToken)
        {
            lock (syncRoot)
            {
                requests.Add(request);
            }

            _ = Interlocked.Increment(ref activeCompiles);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (BeforeCompileAsync is not null)
                {
                    await BeforeCompileAsync(request, cancellationToken).ConfigureAwait(false);
                }

                return await Task.Run(
                    () => CompileAndWrite(request.Options),
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ = Interlocked.Decrement(ref activeCompiles);
            }
        }

        private CompilationResult CompileAndWrite(CompilerOptions options)
        {
            CompilationResult result = compiler.Compile(options);
            if (!result.Success)
            {
                return result;
            }

            OutputResult outputResult = compiler.WriteOutput(result, options);
            if (outputResult.Success)
            {
                return result;
            }

            ImmutableArray<CompilerDiagnostic> diagnostics = result.Diagnostics
                .AddRange(outputResult.Diagnostics);
            return CompilationResult.FromUnits(
                result.Units,
                diagnostics,
                result.EventBindings,
                result.Stats.ElapsedMilliseconds);
        }
    }

    internal sealed record DevFlowCompileRequest(
        CompilerOptions Options,
        FileChangeBatch? Changes);

    internal sealed class DevFlowBuildPipelineAdapter : IDevToolsBuildPipeline
    {
        private readonly object syncRoot = new();
        private readonly IBuildPipeline inner;
        private readonly List<DevFlowBuildRequest> requests = new();

        public DevFlowBuildPipelineAdapter(IBuildPipeline inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            this.inner = inner;
        }

        public IReadOnlyList<DevFlowBuildRequest> Requests
        {
            get
            {
                lock (syncRoot)
                {
                    return requests.ToImmutableArray();
                }
            }
        }

        public async Task<BuildResult> BuildAsync(
            BuildContext context,
            CancellationToken cancellationToken = default)
        {
            RecordRequest(context, ImmutableArray<BuildPhase>.Empty);
            return await inner.BuildAsync(context, cancellationToken).ConfigureAwait(false);
        }

        public async Task<BuildResult> BuildPhasesAsync(
            BuildContext context,
            ImmutableArray<BuildPhase> phases,
            CancellationToken cancellationToken = default)
        {
            RecordRequest(context, phases);
            return await inner.BuildPhasesAsync(context, phases, cancellationToken).ConfigureAwait(false);
        }

        public void OnProgress(Action<BuildProgress> callback)
        {
            inner.OnProgress(callback);
        }

        private void RecordRequest(BuildContext context, ImmutableArray<BuildPhase> phases)
        {
            lock (syncRoot)
            {
                requests.Add(new DevFlowBuildRequest(context, phases));
            }
        }
    }

    internal sealed record DevFlowBuildRequest(
        BuildContext Context,
        ImmutableArray<BuildPhase> Phases);

    internal sealed class RecordingNativeHost : IDevToolsNativeHost
    {
        private readonly object syncRoot = new();
        private readonly List<string> reloadedQmlPaths = new();
        private readonly List<string> syncedInstanceIds = new();
        private readonly List<IReadOnlyDictionary<string, object?>> syncedStates = new();
        private readonly List<IReadOnlyList<InstanceSnapshot>> restoredSnapshots = new();
        private readonly List<string> callOrder = new();
        private int activeOperations;
        private int getInstancesCalls;

        public RecordingNativeHost()
        {
            OldInstances = ImmutableArray.Create(CreateInstance("old-counter"));
            NewInstances = ImmutableArray.Create(CreateInstance("new-counter"));
            Snapshots = ImmutableArray.Create(CreateSnapshot());
        }

        public IReadOnlyList<InstanceInfo> OldInstances { get; set; }

        public IReadOnlyList<InstanceInfo> NewInstances { get; set; }

        public IReadOnlyList<InstanceSnapshot> Snapshots { get; set; }

        public string QmlEvaluationResult { get; set; } = "ok";

        public RuntimeMetrics Metrics { get; set; } = new(
            ActiveInstanceCount: 1,
            TotalInstancesCreated: 1,
            TotalInstancesDestroyed: 0,
            TotalStateSyncs: 0,
            TotalCommandsDispatched: 0,
            TotalEffectsEmitted: 0,
            Uptime: TimeSpan.FromSeconds(1));

        public IReadOnlyList<string> ReloadedQmlPaths
        {
            get
            {
                lock (syncRoot)
                {
                    return reloadedQmlPaths.ToImmutableArray();
                }
            }
        }

        public IReadOnlyList<string> SyncedInstanceIds
        {
            get
            {
                lock (syncRoot)
                {
                    return syncedInstanceIds.ToImmutableArray();
                }
            }
        }

        public IReadOnlyList<IReadOnlyDictionary<string, object?>> SyncedStates
        {
            get
            {
                lock (syncRoot)
                {
                    return syncedStates.ToImmutableArray();
                }
            }
        }

        public IReadOnlyList<IReadOnlyList<InstanceSnapshot>> RestoredSnapshots
        {
            get
            {
                lock (syncRoot)
                {
                    return restoredSnapshots.ToImmutableArray();
                }
            }
        }

        public IReadOnlyList<string> CallOrder
        {
            get
            {
                lock (syncRoot)
                {
                    return callOrder.ToImmutableArray();
                }
            }
        }

        public int ActiveOperations => Volatile.Read(ref activeOperations);

        public Task<IReadOnlyList<InstanceSnapshot>> CaptureSnapshotsAsync(CancellationToken cancellationToken = default)
        {
            return RunAsync("capture", () => Snapshots, cancellationToken);
        }

        public Task ReloadQmlAsync(string qmlSourcePath, CancellationToken cancellationToken = default)
        {
            return RunAsync(
                "reload",
                () =>
                {
                    lock (syncRoot)
                    {
                        reloadedQmlPaths.Add(qmlSourcePath);
                    }
                },
                cancellationToken);
        }

        public Task SyncStateBatchAsync(
            string instanceId,
            IReadOnlyDictionary<string, object?> state,
            CancellationToken cancellationToken = default)
        {
            return RunAsync(
                "sync",
                () =>
                {
                    lock (syncRoot)
                    {
                        syncedInstanceIds.Add(instanceId);
                        syncedStates.Add(state);
                    }
                },
                cancellationToken);
        }

        public Task RestoreSnapshotsAsync(
            IReadOnlyList<InstanceSnapshot> snapshots,
            CancellationToken cancellationToken = default)
        {
            return RunAsync(
                "restore",
                () =>
                {
                    lock (syncRoot)
                    {
                        restoredSnapshots.Add(snapshots);
                    }
                },
                cancellationToken);
        }

        public Task<IReadOnlyList<InstanceInfo>> GetInstancesAsync(CancellationToken cancellationToken = default)
        {
            return RunAsync(
                "instances",
                () =>
                {
                    int calls = Interlocked.Increment(ref getInstancesCalls);
                    return calls == 1 ? OldInstances : NewInstances;
                },
                cancellationToken);
        }

        public Task ShowOverlayAsync(OverlayError error, CancellationToken cancellationToken = default)
        {
            return RunAsync("show-overlay", () => { }, cancellationToken);
        }

        public Task HideOverlayAsync(CancellationToken cancellationToken = default)
        {
            return RunAsync("hide-overlay", () => { }, cancellationToken);
        }

        public Task<string> EvaluateQmlAsync(string input, CancellationToken cancellationToken = default)
        {
            return RunAsync("eval-qml", () => QmlEvaluationResult, cancellationToken);
        }

        public Task<RuntimeMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
        {
            return RunAsync("metrics", () => Metrics, cancellationToken);
        }

        private static InstanceInfo CreateInstance(string instanceId)
        {
            return new InstanceInfo(
                instanceId,
                "CounterViewModel",
                "CounterViewModel",
                "CounterView::__qmlsharp_vm0",
                InstanceState.Active,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["count"] = 41,
                },
                QueuedCommandCount: 0,
                CommandsDispatched: 0,
                EffectsEmitted: 0,
                CreatedAt: DateTimeOffset.UnixEpoch,
                DisposedAt: null);
        }

        private static InstanceSnapshot CreateSnapshot()
        {
            return new InstanceSnapshot(
                "old-counter",
                "CounterViewModel",
                "CounterViewModel",
                "CounterView::__qmlsharp_vm0",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["count"] = 41,
                },
                CreatedAt: DateTimeOffset.UnixEpoch,
                DisposedAt: null);
        }

        private Task RunAsync(string callName, Action action, CancellationToken cancellationToken)
        {
            return RunAsync<object?>(
                callName,
                () =>
                {
                    action();
                    return null;
                },
                cancellationToken);
        }

        private Task<T> RunAsync<T>(string callName, Func<T> action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = Interlocked.Increment(ref activeOperations);
            try
            {
                lock (syncRoot)
                {
                    callOrder.Add(callName);
                }

                return Task.FromResult(action());
            }
            finally
            {
                _ = Interlocked.Decrement(ref activeOperations);
            }
        }
    }

    internal sealed class RecordingErrorOverlayNativeHost : IErrorOverlayNativeHost
    {
        private readonly object syncRoot = new();
        private readonly List<OverlayError> shownErrors = new();

        public int HideCalls { get; private set; }

        public IReadOnlyList<OverlayError> ShownErrors
        {
            get
            {
                lock (syncRoot)
                {
                    return shownErrors.ToImmutableArray();
                }
            }
        }

        public void ShowError(string title, string message, string? filePath, int line, int column)
        {
            lock (syncRoot)
            {
                shownErrors.Add(new OverlayError(
                    title,
                    message,
                    filePath,
                    line <= 0 ? null : line,
                    column <= 0 ? null : column));
            }
        }

        public void HideError()
        {
            lock (syncRoot)
            {
                HideCalls++;
            }
        }
    }

    internal static class DevFlowBuildPipelineFactory
    {
        public static BuildPipeline Create(DevFlowFixtureProject fixture)
        {
            PackageResolver packageResolver = new();
            ResourceBundler resourceBundler = new(
                fixture.ProjectDirectory,
                ImmutableArray.Create("assets"),
                reportMissingRoots: false,
                generateQrcOnBundle: false);
            return new BuildPipeline(ImmutableArray.Create<IBuildStage>(
                new StaticBuildStage(BuildPhase.ConfigLoading, BuildStageResult.Succeeded()),
                new CSharpCompilationBuildStage(),
                new ModuleMetadataBuildStage(),
                new PackageResolutionBuildStage(packageResolver),
                new ResourceBundlingBuildStage(resourceBundler),
                new QmlValidationBuildStage(new RecordingQmlFormat(), new RecordingQmlLint(), packageResolver),
                new RuntimeArtifactStage(),
                new OutputAssemblyBuildStage()));
        }

        private sealed class StaticBuildStage : IBuildStage
        {
            private readonly BuildStageResult result;

            public StaticBuildStage(BuildPhase phase, BuildStageResult result)
            {
                Phase = phase;
                this.result = result;
            }

            public BuildPhase Phase { get; }

            public Task<BuildStageResult> ExecuteAsync(BuildContext context, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(result);
            }
        }

        private sealed class RuntimeArtifactStage : IBuildStage
        {
            public BuildPhase Phase => BuildPhase.CppCodeGenAndBuild;

            public Task<BuildStageResult> ExecuteAsync(BuildContext context, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (context.LibraryMode || context.DryRun)
                {
                    return Task.FromResult(BuildStageResult.Succeeded());
                }

                string nativePath = Path.Join(context.OutputDir, "native", NativeLibraryNames.GetFileName("qmlsharp_native"));
                string managedPath = Path.Join(context.OutputDir, "managed", ProjectName(context) + ".dll");
                string generatedDir = Path.Join(context.OutputDir, "native", "generated");
                _ = Directory.CreateDirectory(Path.GetDirectoryName(nativePath) ?? context.OutputDir);
                _ = Directory.CreateDirectory(Path.GetDirectoryName(managedPath) ?? context.OutputDir);
                _ = Directory.CreateDirectory(generatedDir);
                File.WriteAllText(nativePath, "native");
                File.WriteAllText(managedPath, "managed");
                File.WriteAllText(Path.Join(generatedDir, "CMakeLists.txt"), "cmake_minimum_required(VERSION 3.20)\n");
                File.WriteAllText(Path.Join(generatedDir, "type_registration.cpp"), "// generated fixture\n");
                return Task.FromResult(BuildStageResult.Succeeded(
                    new BuildStatsDelta
                    {
                        CppFilesGenerated = 2,
                        NativeLibBuilt = true,
                    },
                    new BuildArtifacts
                    {
                        NativeLibraryPath = nativePath,
                        AssemblyPath = managedPath,
                    }));
            }

            private static string ProjectName(BuildContext context)
            {
                return context.Config.Name ?? context.Config.Module.Prefix;
            }
        }

        private sealed class RecordingQmlFormat : IQmlFormat
        {
            public Task<QmlFormatResult> FormatFileAsync(
                string filePath,
                QmlFormatOptions? options = null,
                CancellationToken ct = default)
            {
                return Task.FromResult(CreateResult("qmlformat " + filePath));
            }

            public Task<QmlFormatResult> FormatStringAsync(
                string qmlSource,
                QmlFormatOptions? options = null,
                CancellationToken ct = default)
            {
                return Task.FromResult(CreateResult("qmlformat"));
            }

            public Task<ImmutableArray<QmlFormatResult>> FormatBatchAsync(
                ImmutableArray<string> filePaths,
                QmlFormatOptions? options = null,
                CancellationToken ct = default)
            {
                ImmutableArray<QmlFormatResult> results = filePaths
                    .Select(static filePath => CreateResult("qmlformat " + filePath))
                    .ToImmutableArray();
                return Task.FromResult(results);
            }

            private static QmlFormatResult CreateResult(string command)
            {
                return new QmlFormatResult
                {
                    ToolResult = CreateToolResult(command),
                    HasChanges = false,
                };
            }
        }

        private sealed class RecordingQmlLint : IQmlLint
        {
            public Task<QmlLintResult> LintFileAsync(
                string filePath,
                QmlLintOptions? options = null,
                CancellationToken ct = default)
            {
                return Task.FromResult(CreateResult("qmllint " + filePath));
            }

            public Task<QmlLintResult> LintStringAsync(
                string qmlSource,
                QmlLintOptions? options = null,
                CancellationToken ct = default)
            {
                return Task.FromResult(CreateResult("qmllint"));
            }

            public Task<ImmutableArray<QmlLintResult>> LintBatchAsync(
                ImmutableArray<string> filePaths,
                QmlLintOptions? options = null,
                CancellationToken ct = default)
            {
                ImmutableArray<QmlLintResult> results = filePaths
                    .Select(static filePath => CreateResult("qmllint " + filePath))
                    .ToImmutableArray();
                return Task.FromResult(results);
            }

            public Task<QmlLintResult> LintModuleAsync(
                string modulePath,
                QmlLintOptions? options = null,
                CancellationToken ct = default)
            {
                return Task.FromResult(CreateResult("qmllint " + modulePath));
            }

            public Task<ImmutableArray<string>> ListPluginsAsync(CancellationToken ct = default)
            {
                return Task.FromResult(ImmutableArray<string>.Empty);
            }

            private static QmlLintResult CreateResult(string command)
            {
                return new QmlLintResult
                {
                    ToolResult = CreateToolResult(command),
                    ErrorCount = 0,
                    WarningCount = 0,
                    InfoCount = 0,
                };
            }
        }

        private static ToolResult CreateToolResult(string command)
        {
            return new ToolResult
            {
                ExitCode = 0,
                Stdout = string.Empty,
                Stderr = string.Empty,
                DurationMs = 0,
                Command = command,
            };
        }
    }

    internal static class DevFlowAssertions
    {
        public static async Task WaitUntilAsync(
            Func<bool> condition,
            string failureMessage,
            TimeSpan? timeout = null)
        {
            ArgumentNullException.ThrowIfNull(condition);
            Stopwatch stopwatch = Stopwatch.StartNew();
            TimeSpan effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
            while (!condition())
            {
                if (stopwatch.Elapsed > effectiveTimeout)
                {
                    Assert.Fail(failureMessage);
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }
    }
}
