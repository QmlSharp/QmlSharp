#pragma warning disable MA0048

namespace QmlSharp.DevTools.Tests
{
    public sealed class DevServerRebuildTests
    {
        [Fact]
        [Trait("TestId", "DSV-05")]
        public async Task FileChange_DSV_05_TriggersReloadingStateAndHotReload()
        {
            ServerHarness harness = CreateHarness();
            await harness.Server.StartAsync();
            List<DevServerStatusChangedEvent> events = CaptureEvents(harness.Server);

            harness.FileWatcher.Emit(DevToolsTestFixtures.FileChangeBatch());

            await WaitUntilAsync(() => harness.HotReload.Requests.Count == 1);

            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
            Assert.Contains(events, statusChanged => statusChanged.Current == DevServerStatus.Reloading);
            Assert.Contains(events, statusChanged => statusChanged.Current == DevServerStatus.Running);
            _ = Assert.Single(harness.Compiler.Requests);
            _ = Assert.Single(harness.HotReload.Requests);
            Assert.Equal(1, harness.Console.FileChangedCalls);
            Assert.False(harness.Overlay.IsVisible);
        }

        [Fact]
        [Trait("TestId", "DSV-06")]
        public async Task FileChange_DSV_06_CompileFailsTransitionsToErrorAndShowsDiagnostics()
        {
            ServerHarness harness = CreateHarness();
            harness.Compiler.QueueResult(DevToolsTestFixtures.FailedCompilationResult());
            await harness.Server.StartAsync();

            harness.FileWatcher.Emit(DevToolsTestFixtures.FileChangeBatch());

            await WaitUntilAsync(() => harness.Server.Status == DevServerStatus.Error);

            Assert.Equal(DevServerStatus.Error, harness.Server.Status);
            Assert.Equal(1, harness.Console.BuildErrorCalls);
            Assert.True(harness.Overlay.IsVisible);
            Assert.Contains("compile failed", harness.Overlay.Errors[0].Message, StringComparison.Ordinal);
            Assert.Equal(1, harness.Server.Stats.ErrorCount);
        }

        [Fact]
        [Trait("TestId", "DSV-07")]
        public async Task FileChange_DSV_07_InErrorStateRetriesReloadAndReturnsToRunning()
        {
            ServerHarness harness = CreateHarness();
            harness.Compiler.QueueResult(DevToolsTestFixtures.FailedCompilationResult());
            harness.Compiler.QueueResult(DevToolsTestFixtures.CompilationResultWithSchema());
            await harness.Server.StartAsync();

            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/Broken.cs"));
            await WaitUntilAsync(() => harness.Server.Status == DevServerStatus.Error);
            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/CounterView.cs"));

            await WaitUntilAsync(() => harness.Server.Status == DevServerStatus.Running && harness.HotReload.Requests.Count == 1);

            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
            Assert.False(harness.Overlay.IsVisible);
            Assert.Equal(2, harness.Compiler.Requests.Count);
            Assert.Equal(1, harness.Server.Stats.HotReloadCount);
        }

        [Fact]
        [Trait("TestId", "DSV-08")]
        public async Task ConcurrencyGuard_DSV_08_RebuildInProgressQueuesNextBatch()
        {
            ServerHarness harness = CreateHarness();
            TaskCompletionSource firstCompileStarted = NewSignal();
            TaskCompletionSource releaseFirstCompile = NewSignal();
            int compileCalls = 0;
            harness.Compiler.OnCompileAsync = async (_, token) =>
            {
                compileCalls++;
                if (compileCalls == 1)
                {
                    firstCompileStarted.SetResult();
                    await releaseFirstCompile.Task.WaitAsync(token).ConfigureAwait(false);
                }

                return DevToolsTestFixtures.CompilationResultWithSchema();
            };
            await harness.Server.StartAsync();

            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/First.cs"));
            await firstCompileStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/Second.cs"));

            _ = Assert.Single(harness.Compiler.Requests);
            releaseFirstCompile.SetResult();
            await WaitUntilAsync(() => harness.Compiler.Requests.Count == 2);

            Assert.Equal("C:/repo/src/Second.cs", harness.Compiler.Requests[1].Changes?.Changes[0].FilePath);
            Assert.Equal(2, harness.Server.Stats.RebuildCount);
            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
        }

        [Fact]
        [Trait("TestId", "DSV-09")]
        public async Task ConcurrencyGuard_DSV_09_PendingRebuildOnlyLatestBatchRuns()
        {
            ServerHarness harness = CreateHarness();
            TaskCompletionSource firstCompileStarted = NewSignal();
            TaskCompletionSource releaseFirstCompile = NewSignal();
            int compileCalls = 0;
            harness.Compiler.OnCompileAsync = async (_, token) =>
            {
                compileCalls++;
                if (compileCalls == 1)
                {
                    firstCompileStarted.SetResult();
                    await releaseFirstCompile.Task.WaitAsync(token).ConfigureAwait(false);
                }

                return DevToolsTestFixtures.CompilationResultWithSchema();
            };
            await harness.Server.StartAsync();

            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/First.cs"));
            await firstCompileStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/Second.cs"));
            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/Third.cs"));

            releaseFirstCompile.SetResult();
            await WaitUntilAsync(() => harness.Compiler.Requests.Count == 2);

            Assert.Equal("C:/repo/src/Third.cs", harness.Compiler.Requests[1].Changes?.Changes[0].FilePath);
            Assert.DoesNotContain(harness.Compiler.Requests, request =>
                request.Changes?.Changes[0].FilePath == "C:/repo/src/Second.cs");
        }

        [Fact]
        [Trait("TestId", "DSV-10")]
        public async Task ConcurrencyGuard_DSV_10_NoPendingCompletesAndGuardResets()
        {
            ServerHarness harness = CreateHarness();
            await harness.Server.StartAsync();

            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/First.cs"));
            await WaitUntilAsync(() => harness.Compiler.Requests.Count == 1);
            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/Second.cs"));
            await WaitUntilAsync(() => harness.Compiler.Requests.Count == 2);

            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
            Assert.Equal(2, harness.Server.Stats.RebuildCount);
            Assert.Equal(2, harness.Server.Stats.HotReloadCount);
        }

        [Fact]
        [Trait("TestId", "DSV-11")]
        public async Task SchemaStructuralChange_DSV_11_TriggersRestartInsteadOfHotReload()
        {
            using TempSchemaFile tempSchema = TempSchemaFile.Create(DevToolsTestFixtures.ViewModelSchema());
            ServerHarness harness = CreateHarness();
            harness.BuildPipeline.QueueResult(DevToolsTestFixtures.SuccessfulBuildResultWithSchema(tempSchema.Path));
            harness.BuildPipeline.QueueResult(DevToolsTestFixtures.SuccessfulBuildResult());
            ViewModelSchema changedSchema = DevToolsTestFixtures.ViewModelSchema(
                properties: ImmutableArray.Create(
                    DevToolsTestFixtures.State("count", "int"),
                    DevToolsTestFixtures.State("title", "string")));
            harness.Compiler.QueueResult(DevToolsTestFixtures.CompilationResultWithSchema(changedSchema));
            await harness.Server.StartAsync();

            harness.FileWatcher.Emit(DevToolsTestFixtures.FileChangeBatch());

            await WaitUntilAsync(() => harness.BuildPipeline.Requests.Count == 2 && harness.Server.Status == DevServerStatus.Running);

            _ = Assert.Single(harness.Compiler.Requests);
            Assert.Empty(harness.HotReload.Requests);
            Assert.Equal(2, harness.FileWatcher.StartCalls);
            Assert.Equal(1, harness.FileWatcher.StopCalls);
            Assert.Equal(1, harness.Console.RestartRequiredCalls);
            Assert.Contains("state property added: title", harness.Console.RestartReasons[0], StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DSV-12")]
        public async Task ConfigFileChange_DSV_12_TriggersRestartWithoutCompiler()
        {
            ServerHarness harness = CreateHarness();
            harness.BuildPipeline.QueueResult(DevToolsTestFixtures.SuccessfulBuildResult());
            harness.BuildPipeline.QueueResult(DevToolsTestFixtures.SuccessfulBuildResult());
            await harness.Server.StartAsync();

            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/qmlsharp.json"));

            await WaitUntilAsync(() => harness.BuildPipeline.Requests.Count == 2 && harness.Server.Status == DevServerStatus.Running);

            Assert.Empty(harness.Compiler.Requests);
            Assert.Empty(harness.HotReload.Requests);
            Assert.Equal(2, harness.FileWatcher.StartCalls);
            Assert.Equal(1, harness.FileWatcher.StopCalls);
            Assert.Equal(1, harness.Console.RestartRequiredCalls);
        }

        [Fact]
        [Trait("TestId", "DSV-12")]
        public async Task ConfigFileChange_DSV_12_ReloadsBuildCompilerAndWatcherConfiguration()
        {
            TestFileWatcher initialWatcher = new();
            List<TestFileWatcher> createdWatchers = new();
            List<FileWatcherOptions> createdWatcherOptions = new();
            FakeConfigLoader configLoader = new(ReloadedConfig());
            ServerHarness harness = CreateHarness(
                fileWatcher: initialWatcher,
                configLoader: configLoader,
                fileWatcherFactory: watcherOptions =>
                {
                    createdWatcherOptions.Add(watcherOptions);
                    TestFileWatcher watcher = new();
                    createdWatchers.Add(watcher);
                    return watcher;
                });
            harness.BuildPipeline.QueueResult(DevToolsTestFixtures.SuccessfulBuildResult());
            harness.BuildPipeline.QueueResult(DevToolsTestFixtures.SuccessfulBuildResult());
            await harness.Server.StartAsync();

            initialWatcher.Emit(FileChangeBatch("C:/repo/qmlsharp.json"));

            await WaitUntilAsync(() => createdWatchers.Count == 1 && harness.Server.Status == DevServerStatus.Running);

            Assert.Equal(1, configLoader.LoadCalls);
            Assert.Equal("Reloaded.App", harness.BuildPipeline.Requests[1].Context.Config.Module.Prefix);
            Assert.Equal("C:/repo/build/dev", harness.BuildPipeline.Requests[1].Context.OutputDir);
            FileWatcherOptions watcherOptions = Assert.Single(createdWatcherOptions);
            Assert.Equal(333, watcherOptions.DebounceMs);
            Assert.Equal(new[] { "C:/repo/src", "C:/repo/ui" }, watcherOptions.WatchPaths);
            Assert.Equal(1, initialWatcher.StopCalls);
            Assert.Equal(1, initialWatcher.DisposeCalls);
            Assert.Equal(1, createdWatchers[0].StartCalls);

            createdWatchers[0].Emit(FileChangeBatch("C:/repo/src/App.cs"));
            await WaitUntilAsync(() => harness.Compiler.Requests.Count == 1);

            Assert.Equal("Reloaded.App", harness.Compiler.Requests[0].Options.ModuleUriPrefix);
            Assert.Equal("C:/repo/build/dev", harness.Compiler.Requests[0].Options.OutputDir);
            Assert.Equal(new QmlSharp.Compiler.QmlVersion(2, 1), harness.Compiler.Requests[0].Options.ModuleVersion);
        }

        [Fact]
        [Trait("TestId", "DSV-12")]
        public async Task ConfigFileChange_DSV_12_PreservesEntryOverrideOnConfigReload()
        {
            TestFileWatcher initialWatcher = new();
            FakeConfigLoader configLoader = new(ReloadedConfig() with
            {
                Entry = "./src/ConfigProgram.cs",
            });
            DevServerOptions options = ServerOptions() with
            {
                EntryOverride = "./src/OverrideProgram.cs",
            };
            ServerHarness harness = CreateHarness(
                options: options,
                fileWatcher: initialWatcher,
                configLoader: configLoader);
            harness.BuildPipeline.QueueResult(DevToolsTestFixtures.SuccessfulBuildResult());
            harness.BuildPipeline.QueueResult(DevToolsTestFixtures.SuccessfulBuildResult());
            await harness.Server.StartAsync();

            initialWatcher.Emit(FileChangeBatch("C:/repo/qmlsharp.json"));

            await WaitUntilAsync(() => harness.BuildPipeline.Requests.Count == 2 && harness.Server.Status == DevServerStatus.Running);

            Assert.Equal(1, configLoader.LoadCalls);
            Assert.Equal("C:/repo/src/OverrideProgram.cs", harness.BuildPipeline.Requests[1].Context.Config.Entry);
            Assert.Equal("C:/repo/build/dev", harness.BuildPipeline.Requests[1].Context.OutputDir);
        }

        [Fact]
        [Trait("TestId", "DSV-12")]
        public async Task ConfigFileChange_DSV_12_FailedRestartLeavesServerInError()
        {
            ServerHarness harness = CreateHarness();
            harness.BuildPipeline.QueueResult(DevToolsTestFixtures.SuccessfulBuildResult());
            harness.BuildPipeline.QueueResult(FailedBuildResult());
            await harness.Server.StartAsync();

            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/qmlsharp.json"));

            await WaitUntilAsync(() => harness.Server.Status == DevServerStatus.Error);

            Assert.Equal(DevServerStatus.Error, harness.Server.Status);
            Assert.Empty(harness.Compiler.Requests);
            Assert.True(harness.Overlay.IsVisible);
            Assert.Contains("compile failed", harness.Overlay.Errors[0].Message, StringComparison.Ordinal);
            Assert.Contains(harness.Console.Errors, error => error.Contains("Configuration restart failed", StringComparison.Ordinal));
        }

        [Fact]
        [Trait("TestId", "DSV-14")]
        public async Task Stats_DSV_14_AfterMixedRebuildsCountsActualResults()
        {
            ServerHarness harness = CreateHarness();
            harness.BuildPipeline.QueueResult(SuccessfulBuildResult(TimeSpan.FromMilliseconds(10), filesCompiled: 2));
            harness.Compiler.QueueResult(DevToolsTestFixtures.CompilationResultWithSchema(elapsedMilliseconds: 5));
            harness.Compiler.QueueResult(DevToolsTestFixtures.FailedCompilationResult());
            harness.Compiler.QueueResult(DevToolsTestFixtures.CompilationResultWithSchema(elapsedMilliseconds: 7));
            harness.HotReload.QueueResult(FakeHotReloadOrchestrator.SuccessfulResult() with
            {
                TotalTime = TimeSpan.FromMilliseconds(3),
            });
            harness.HotReload.QueueResult(FakeHotReloadOrchestrator.SuccessfulResult() with
            {
                TotalTime = TimeSpan.FromMilliseconds(4),
            });
            await harness.Server.StartAsync();

            _ = await harness.Server.RebuildAsync();
            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/Broken.cs"));
            await WaitUntilAsync(() => harness.Server.Status == DevServerStatus.Error);
            _ = await harness.Server.RebuildAsync();

            ServerStats stats = harness.Server.Stats;
            Assert.Equal(1, stats.BuildCount);
            Assert.Equal(3, stats.RebuildCount);
            Assert.Equal(2, stats.HotReloadCount);
            Assert.Equal(1, stats.ErrorCount);
            Assert.Equal(TimeSpan.FromMilliseconds(22) + TimeSpan.FromTicks(1), stats.TotalBuildTime);
            Assert.Equal(TimeSpan.FromMilliseconds(7), stats.TotalHotReloadTime);
        }

        [Fact]
        [Trait("TestId", "DSV-15")]
        public async Task RebuildAsync_DSV_15_ManualRebuildBypassesWatcherAndReloads()
        {
            ServerHarness harness = CreateHarness();
            await harness.Server.StartAsync();

            HotReloadResult result = await harness.Server.RebuildAsync();

            Assert.True(result.Success);
            FakeCompilerRequest request = Assert.Single(harness.Compiler.Requests);
            Assert.Null(request.Changes);
            _ = Assert.Single(harness.HotReload.Requests);
            Assert.Equal(0, harness.Console.FileChangedCalls);
            Assert.Equal(1, harness.Server.Stats.RebuildCount);
        }

        [Fact]
        [Trait("TestId", "DSV-16")]
        public async Task RestartAsync_DSV_16_FullCycleStopsAndStartsCleanly()
        {
            ServerHarness harness = CreateHarness();
            harness.BuildPipeline.QueueResult(SuccessfulBuildResult(TimeSpan.FromMilliseconds(5), filesCompiled: 1));
            harness.BuildPipeline.QueueResult(SuccessfulBuildResult(TimeSpan.FromMilliseconds(7), filesCompiled: 2));
            await harness.Server.StartAsync();

            await harness.Server.RestartAsync();

            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
            Assert.Equal(2, harness.BuildPipeline.Requests.Count);
            Assert.Equal(2, harness.FileWatcher.StartCalls);
            Assert.Equal(1, harness.FileWatcher.StopCalls);
            Assert.Equal(2, harness.Server.Stats.BuildCount);
            Assert.Equal(TimeSpan.FromMilliseconds(12), harness.Server.Stats.TotalBuildTime);
        }

        private static ServerHarness CreateHarness(
            DevServerOptions? options = null,
            TestFileWatcher? fileWatcher = null,
            FakeBuildPipeline? buildPipeline = null,
            FakeDevToolsCompiler? compiler = null,
            FakeHotReloadOrchestrator? hotReload = null,
            TestDevConsole? console = null,
            TestErrorOverlay? overlay = null,
            ManualDevToolsClock? clock = null,
            IDevToolsConfigLoader? configLoader = null,
            Func<FileWatcherOptions, IFileWatcher>? fileWatcherFactory = null)
        {
            DevServerOptions serverOptions = options ?? ServerOptions();
            TestFileWatcher serverFileWatcher = fileWatcher ?? new TestFileWatcher();
            FakeBuildPipeline serverBuildPipeline = buildPipeline ?? new FakeBuildPipeline();
            FakeDevToolsCompiler serverCompiler = compiler ?? new FakeDevToolsCompiler();
            FakeHotReloadOrchestrator serverHotReload = hotReload ?? new FakeHotReloadOrchestrator();
            TestDevConsole serverConsole = console ?? new TestDevConsole();
            TestErrorOverlay serverOverlay = overlay ?? new TestErrorOverlay();
            ManualDevToolsClock serverClock = clock ?? new ManualDevToolsClock();
            PerfProfiler profiler = new(serverClock);
            DevServer server = new(
                serverOptions,
                DevToolsTestFixtures.BuildContext(),
                serverFileWatcher,
                serverBuildPipeline,
                serverCompiler,
                DevToolsTestFixtures.CompilerOptions(),
                serverHotReload,
                serverConsole,
                serverOverlay,
                profiler,
                repl: null,
                serverClock,
                new SchemaDiffer(),
                configLoader,
                fileWatcherFactory);

            return new ServerHarness(
                server,
                serverFileWatcher,
                serverBuildPipeline,
                serverCompiler,
                serverHotReload,
                serverConsole,
                serverOverlay,
                profiler,
                serverClock);
        }

        private static List<DevServerStatusChangedEvent> CaptureEvents(IDevServer server)
        {
            List<DevServerStatusChangedEvent> events = new();
            server.OnStatusChanged += events.Add;
            return events;
        }

        private static DevServerOptions ServerOptions()
        {
            return new DevServerOptions(
                "C:/repo",
                new FileWatcherOptions(ImmutableArray.Create("C:/repo/src")),
                new DevConsoleOptions(LogLevel.Silent, Color: false, ShowTimestamps: false),
                EnableRepl: false,
                EnableProfiling: true);
        }

        private static FileChangeBatch FileChangeBatch(string path)
        {
            DateTimeOffset timestamp = DateTimeOffset.Parse(
                "2026-05-06T00:00:00Z",
                null,
                System.Globalization.DateTimeStyles.AssumeUniversal);
            FileChange change = new(path, FileChangeKind.Modified, timestamp);
            return new FileChangeBatch(ImmutableArray.Create(change), timestamp, timestamp);
        }

        private static BuildResult SuccessfulBuildResult(TimeSpan totalDuration, int filesCompiled)
        {
            return DevToolsTestFixtures.SuccessfulBuildResult() with
            {
                Stats = new BuildStats(
                    totalDuration,
                    filesCompiled,
                    SchemasGenerated: 0,
                    CppFilesGenerated: 0,
                    AssetsCollected: 0,
                    NativeLibBuilt: false),
            };
        }

        private static BuildResult FailedBuildResult()
        {
            BuildDiagnostic diagnostic = new(
                BuildDiagnosticCode.CompilationFailed,
                BuildDiagnosticSeverity.Error,
                "compile failed",
                BuildPhase.CSharpCompilation,
                "C:/repo/src/App.cs");
            return new BuildResult
            {
                Success = false,
                PhaseResults = ImmutableArray.Create(new PhaseResult(
                    BuildPhase.CSharpCompilation,
                    Success: false,
                    Duration: TimeSpan.FromMilliseconds(3),
                    Diagnostics: ImmutableArray.Create(diagnostic))),
                Diagnostics = ImmutableArray.Create(diagnostic),
                Stats = new BuildStats(
                    TimeSpan.FromMilliseconds(3),
                    FilesCompiled: 0,
                    SchemasGenerated: 0,
                    CppFilesGenerated: 0,
                    AssetsCollected: 0,
                    NativeLibBuilt: false),
            };
        }

        private static QmlSharpConfig ReloadedConfig()
        {
            return new QmlSharpConfig
            {
                Entry = "src/App.cs",
                OutDir = "./build/dev",
                Qt = new QtConfig { Dir = "C:/Qt/6.11.0/msvc2022_64" },
                Module = new ModuleConfig
                {
                    Prefix = "Reloaded.App",
                    Version = new QmlSharp.Build.QmlVersion(2, 1),
                },
                Build = new BuildConfig
                {
                    Format = false,
                    Lint = false,
                    SourceMaps = false,
                    Incremental = false,
                },
                Dev = new DevConfig
                {
                    WatchPaths = ImmutableArray.Create("./ui", "./src"),
                    DebounceMs = 333,
                },
            };
        }

        private static TaskCompletionSource NewSignal()
        {
            return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static async Task WaitUntilAsync(Func<bool> predicate)
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            while (!predicate())
            {
                await Task.Delay(10, timeout.Token).ConfigureAwait(false);
            }
        }

        private sealed record ServerHarness(
            DevServer Server,
            TestFileWatcher FileWatcher,
            FakeBuildPipeline BuildPipeline,
            FakeDevToolsCompiler Compiler,
            FakeHotReloadOrchestrator HotReload,
            TestDevConsole Console,
            TestErrorOverlay Overlay,
            PerfProfiler Profiler,
            ManualDevToolsClock Clock);

        private sealed class TestFileWatcher : IFileWatcher
        {
            public event Action<FileChangeBatch> OnChange = static _ => { };

            public int StartCalls { get; private set; }

            public int StopCalls { get; private set; }

            public int DisposeCalls { get; private set; }

            public FileWatcherStatus Status { get; private set; } = FileWatcherStatus.Idle;

            public void Start()
            {
                StartCalls++;
                Status = FileWatcherStatus.Running;
            }

            public void Stop()
            {
                StopCalls++;
                Status = FileWatcherStatus.Idle;
            }

            public void Dispose()
            {
                DisposeCalls++;
                Status = FileWatcherStatus.Disposed;
            }

            public void Emit(FileChangeBatch batch)
            {
                OnChange(batch);
            }
        }

        private sealed class TestDevConsole : IDevConsole
        {
            public int FileChangedCalls { get; private set; }

            public int BuildErrorCalls { get; private set; }

            public int RestartRequiredCalls { get; private set; }

            public List<string> RestartReasons { get; } = new();

            public List<string> Errors { get; } = new();

            public void Banner(string version, DevServerOptions options)
            {
            }

            public void WatchStarted(int fileCount, IReadOnlyList<string> paths)
            {
            }

            public void FileChanged(FileChangeBatch batch)
            {
                FileChangedCalls++;
            }

            public void BuildStart(int fileCount)
            {
            }

            public void BuildSuccess(TimeSpan elapsed, int fileCount)
            {
            }

            public void BuildError(IReadOnlyList<CompilerDiagnostic> errors)
            {
                BuildErrorCalls++;
            }

            public void HotReloadSuccess(HotReloadResult result)
            {
            }

            public void HotReloadError(string message)
            {
                Errors.Add(message);
            }

            public void RestartRequired(string reason)
            {
                RestartRequiredCalls++;
                RestartReasons.Add(reason);
            }

            public void ServerStopped()
            {
            }

            public void Info(string message)
            {
            }

            public void Warn(string message)
            {
            }

            public void Error(string message)
            {
                Errors.Add(message);
            }
        }

        private sealed class TestErrorOverlay : IErrorOverlay
        {
            public bool IsVisible { get; private set; }

            public IReadOnlyList<OverlayError> Errors { get; private set; } = ImmutableArray<OverlayError>.Empty;

            public void Show(OverlayError error)
            {
                Show(ImmutableArray.Create(error));
            }

            public void Show(IReadOnlyList<OverlayError> errors)
            {
                IsVisible = true;
                Errors = errors.ToImmutableArray();
            }

            public void Hide()
            {
                IsVisible = false;
            }
        }

        private sealed class TempSchemaFile : IDisposable
        {
            private readonly string directory;

            private TempSchemaFile(string directory, string path)
            {
                this.directory = directory;
                Path = path;
            }

            public string Path { get; }

            public static TempSchemaFile Create(ViewModelSchema schema)
            {
                string directory = System.IO.Path.Join(System.IO.Path.GetTempPath(), "qmlsharp-schema-" + System.IO.Path.GetRandomFileName());
                _ = Directory.CreateDirectory(directory);
                string path = System.IO.Path.Join(directory, schema.ClassName + ".schema.json");
                File.WriteAllText(path, new ViewModelSchemaSerializer().Serialize(schema));
                return new TempSchemaFile(directory, path);
            }

            public void Dispose()
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private sealed class FakeConfigLoader : IDevToolsConfigLoader
        {
            private readonly QmlSharpConfig config;

            public FakeConfigLoader(QmlSharpConfig config)
            {
                this.config = config;
            }

            public int LoadCalls { get; private set; }

            public QmlSharpConfig Load(string projectDir)
            {
                LoadCalls++;
                return config;
            }

            public ImmutableArray<ConfigDiagnostic> Validate(QmlSharpConfig loadedConfig)
            {
                return ImmutableArray<ConfigDiagnostic>.Empty;
            }

            public QmlSharpConfig GetDefaults()
            {
                return config;
            }
        }
    }
}

#pragma warning restore MA0048
