using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class DevSessionTests
    {
        [Fact]
        public async Task DC01_StartAsync_PerformsInitialBuildAndTransitionsToRunning()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("dc01-dev-start");
            QmlSharpConfig config = CreateConfig(project.Path);
            RecordingBuildPipeline pipeline = new(CreateSuccessfulBuildResult());
            ManualWatcherFactory watcherFactory = new();
            ManualDebouncerFactory debouncerFactory = new();
            RecordingDevHostHook hostHook = new();
            await using DevSession session = CreateSession(config, pipeline, watcherFactory, debouncerFactory, hostHook);
            List<DevSessionState> states = [];
            List<BuildResult> builds = [];
            session.OnStateChanged(states.Add);
            session.OnBuildComplete(builds.Add);
            Task startTask = session.StartAsync(new DevCommandOptions { ProjectDir = project.Path, Headless = true });

            Assert.Contains(DevSessionState.Starting, states);
            Assert.Contains(DevSessionState.Building, states);
            Assert.Contains(DevSessionState.Running, states);
            Assert.Equal(DevSessionState.Running, session.State);
            Assert.Equal(1, pipeline.BuildCallCount);
            Assert.NotNull(session.LastBuild);
            _ = Assert.Single(builds);
            _ = Assert.Single(watcherFactory.Watchers);
            Assert.True(watcherFactory.Watchers[0].Started);
            Assert.Equal(0, hostHook.StartCallCount);

            await StopSessionAsync(session, startTask);
        }

        [Fact]
        public async Task DC02_FileChange_DebouncesAndRunsOneDeterministicRebuild()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("dc02-dev-rebuild");
            string sourceFile = Path.Join(project.Path, "src", "CounterViewModel.cs");
            File.WriteAllText(sourceFile, "public sealed class CounterViewModel {}\n");
            RecordingBuildPipeline pipeline = new(
                CreateSuccessfulBuildResult(),
                CreateSuccessfulBuildResult());
            ManualWatcherFactory watcherFactory = new();
            ManualDebouncerFactory debouncerFactory = new();
            RecordingDevHostHook hostHook = new();
            await using DevSession session = CreateSession(
                CreateConfig(project.Path),
                pipeline,
                watcherFactory,
                debouncerFactory,
                hostHook);
            List<DevSessionState> states = [];
            session.OnStateChanged(states.Add);
            Task startTask = session.StartAsync(new DevCommandOptions { ProjectDir = project.Path });

            watcherFactory.Watchers[0].RaiseChanged(sourceFile);
            watcherFactory.Watchers[0].RaiseChanged(sourceFile);
            await debouncerFactory.LastDebouncer.FlushAsync();

            Assert.Equal(2, pipeline.BuildCallCount);
            Assert.Equal(1, debouncerFactory.LastDebouncer.RunCount);
            Assert.Contains(DevSessionState.Rebuilding, states);
            Assert.Equal(DevSessionState.Running, session.State);
            Assert.Equal(1, hostHook.ReloadCallCount);
            _ = Assert.Single(hostHook.LastReloadRequest?.ChangedFiles ?? ImmutableArray<string>.Empty);
            Assert.Equal(Path.GetFullPath(sourceFile), hostHook.LastReloadRequest?.ChangedFiles[0]);

            await StopSessionAsync(session, startTask);
        }

        [Fact]
        public async Task DC02_OutputDirectoryChanges_DoNotTriggerRebuildLoops()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("dc02-output-ignore");
            string outputFile = Path.Join(project.Path, "dist", "generated.json");
            _ = Directory.CreateDirectory(Path.GetDirectoryName(outputFile) ?? project.Path);
            File.WriteAllText(outputFile, "{}\n");
            RecordingBuildPipeline pipeline = new(
                CreateSuccessfulBuildResult(),
                CreateSuccessfulBuildResult());
            ManualWatcherFactory watcherFactory = new();
            ManualDebouncerFactory debouncerFactory = new();
            await using DevSession session = CreateSession(
                CreateConfig(project.Path) with
                {
                    Dev = new DevConfig
                    {
                        WatchPaths = ImmutableArray.Create(project.Path),
                    },
                },
                pipeline,
                watcherFactory,
                debouncerFactory,
                new RecordingDevHostHook());
            Task startTask = session.StartAsync(new DevCommandOptions { ProjectDir = project.Path, Headless = true });

            watcherFactory.Watchers[0].RaiseChanged(outputFile);
            await debouncerFactory.LastDebouncer.FlushAsync();

            Assert.Equal(1, pipeline.BuildCallCount);
            Assert.Equal(0, debouncerFactory.LastDebouncer.RunCount);

            await StopSessionAsync(session, startTask);
        }

        [Fact]
        public async Task DC03_DC04_HeadlessAndEntryOverride_AreAppliedToBuildAndHostFlow()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("dc03-dc04-headless-entry");
            string altEntry = Path.Join(project.Path, "src", "AltProgram.cs");
            RecordingBuildPipeline pipeline = new(CreateSuccessfulBuildResult());
            RecordingDevHostHook hostHook = new();
            await using DevSession session = CreateSession(
                CreateConfig(project.Path) with
                {
                    Entry = Path.Join(project.Path, "src", "Program.cs"),
                },
                pipeline,
                new ManualWatcherFactory(),
                new ManualDebouncerFactory(),
                hostHook);
            Task startTask = session.StartAsync(new DevCommandOptions
            {
                ProjectDir = project.Path,
                Entry = altEntry,
                Headless = true,
            });

            Assert.Equal(1, pipeline.BuildCallCount);
            Assert.Equal(Path.GetFullPath(altEntry), pipeline.Contexts[0].Config.Entry);
            Assert.Equal(0, hostHook.StartCallCount);
            Assert.Equal(0, hostHook.ReloadCallCount);

            await StopSessionAsync(session, startTask);
        }

        [Fact]
        public async Task DC05_BuildError_DoesNotStopSessionAndNextChangeRecovers()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("dc05-error-recovery");
            string sourceFile = Path.Join(project.Path, "src", "CounterViewModel.cs");
            File.WriteAllText(sourceFile, "public sealed class CounterViewModel {}\n");
            BuildDiagnostic diagnostic = new(
                BuildDiagnosticCode.CompilationFailed,
                BuildDiagnosticSeverity.Error,
                "Compilation failed.",
                BuildPhase.CSharpCompilation,
                sourceFile);
            RecordingBuildPipeline pipeline = new(
                CreateFailedBuildResult(diagnostic),
                CreateSuccessfulBuildResult());
            ManualWatcherFactory watcherFactory = new();
            ManualDebouncerFactory debouncerFactory = new();
            await using DevSession session = CreateSession(
                CreateConfig(project.Path),
                pipeline,
                watcherFactory,
                debouncerFactory,
                new RecordingDevHostHook());
            List<DevSessionState> states = [];
            session.OnStateChanged(states.Add);
            Task startTask = session.StartAsync(new DevCommandOptions { ProjectDir = project.Path, Headless = true });

            Assert.Equal(DevSessionState.Error, session.State);
            Assert.False(session.LastBuild?.Success);

            watcherFactory.Watchers[0].RaiseChanged(sourceFile);
            await debouncerFactory.LastDebouncer.FlushAsync();

            Assert.Equal(2, pipeline.BuildCallCount);
            Assert.Contains(DevSessionState.Rebuilding, states);
            Assert.Equal(DevSessionState.Running, session.State);
            Assert.True(session.LastBuild?.Success);

            await StopSessionAsync(session, startTask);
        }

        [Fact]
        public async Task DC06_Cancellation_DisposesWatchersAndStopsSession()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("dc06-cancellation");
            ManualWatcherFactory watcherFactory = new();
            ManualDebouncerFactory debouncerFactory = new();
            RecordingDevHostHook hostHook = new();
            await using DevSession session = CreateSession(
                CreateConfig(project.Path),
                new RecordingBuildPipeline(CreateSuccessfulBuildResult()),
                watcherFactory,
                debouncerFactory,
                hostHook);
            using CancellationTokenSource cancellation = new();
            Task startTask = session.StartAsync(
                new DevCommandOptions { ProjectDir = project.Path },
                cancellation.Token);

            await cancellation.CancelAsync();
            _ = await Assert.ThrowsAsync<OperationCanceledException>(async () => await startTask);

            Assert.Equal(DevSessionState.Stopped, session.State);
            Assert.All(watcherFactory.Watchers, static watcher => Assert.True(watcher.Disposed));
            Assert.True(debouncerFactory.LastDebouncer.Disposed);
            Assert.Equal(1, hostHook.StopCallCount);
            Assert.Equal(1, hostHook.DisposeCallCount);
        }

        [Fact]
        public async Task DisposeAsync_StopsWatchersWithoutCancellingCallerToken()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("dev-dispose");
            ManualWatcherFactory watcherFactory = new();
            ManualDebouncerFactory debouncerFactory = new();
            RecordingDevHostHook hostHook = new();
            DevSession session = CreateSession(
                CreateConfig(project.Path),
                new RecordingBuildPipeline(CreateSuccessfulBuildResult()),
                watcherFactory,
                debouncerFactory,
                hostHook);
            Task startTask = session.StartAsync(new DevCommandOptions { ProjectDir = project.Path });

            await session.DisposeAsync();
            await startTask;

            Assert.Equal(DevSessionState.Stopped, session.State);
            Assert.All(watcherFactory.Watchers, static watcher => Assert.True(watcher.Disposed));
            Assert.True(debouncerFactory.LastDebouncer.Disposed);
            Assert.Equal(1, hostHook.StopCallCount);
            Assert.Equal(1, hostHook.DisposeCallCount);
        }

        private static DevSession CreateSession(
            QmlSharpConfig config,
            RecordingBuildPipeline pipeline,
            ManualWatcherFactory watcherFactory,
            ManualDebouncerFactory debouncerFactory,
            RecordingDevHostHook hostHook)
        {
            return new DevSession(
                new FakeConfigLoader(config),
                pipeline,
                watcherFactory,
                debouncerFactory,
                hostHook);
        }

        private static QmlSharpConfig CreateConfig(string projectDir)
        {
            return BuildTestFixtures.CreateDefaultConfig() with
            {
                Entry = Path.Join(projectDir, "src", "Program.cs"),
                OutDir = Path.Join(projectDir, "dist"),
                Dev = new DevConfig
                {
                    HotReload = true,
                    WatchPaths = ImmutableArray.Create(Path.Join(projectDir, "src")),
                    DebounceMs = 25,
                },
            };
        }

        private static BuildResult CreateSuccessfulBuildResult()
        {
            return BuildTestFixtures.CreateSuccessfulBuildResult();
        }

        private static BuildResult CreateFailedBuildResult(BuildDiagnostic diagnostic)
        {
            return new BuildResult
            {
                Success = false,
                PhaseResults = ImmutableArray<PhaseResult>.Empty,
                Diagnostics = ImmutableArray.Create(diagnostic),
                Stats = new BuildStats(TimeSpan.Zero, 0, 0, 0, 0, false),
            };
        }

        private static async Task StopSessionAsync(DevSession session, Task startTask)
        {
            await session.DisposeAsync();
            await startTask;
        }

        private sealed class FakeConfigLoader : IConfigLoader
        {
            private readonly QmlSharpConfig config;

            public FakeConfigLoader(QmlSharpConfig config)
            {
                this.config = config;
            }

            public QmlSharpConfig Load(string projectDir)
            {
                return config;
            }

            public ImmutableArray<ConfigDiagnostic> Validate(QmlSharpConfig config)
            {
                return ImmutableArray<ConfigDiagnostic>.Empty;
            }

            public QmlSharpConfig GetDefaults()
            {
                return config;
            }
        }

        private sealed class RecordingBuildPipeline : IBuildPipeline
        {
            private readonly Queue<BuildResult> results = new();

            public RecordingBuildPipeline(params BuildResult[] results)
            {
                foreach (BuildResult result in results)
                {
                    this.results.Enqueue(result);
                }
            }

            public int BuildCallCount { get; private set; }

            public List<BuildContext> Contexts { get; } = [];

            public Task<BuildResult> BuildAsync(BuildContext context, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BuildCallCount++;
                Contexts.Add(context);
                BuildResult result = results.Count == 0
                    ? CreateSuccessfulBuildResult()
                    : results.Dequeue();
                return Task.FromResult(result);
            }

            public Task<BuildResult> BuildPhasesAsync(
                BuildContext context,
                ImmutableArray<BuildPhase> phases,
                CancellationToken cancellationToken = default)
            {
                return BuildAsync(context, cancellationToken);
            }

            public void OnProgress(Action<BuildProgress> callback)
            {
            }
        }

        private sealed class ManualWatcherFactory : IDevFileWatcherFactory
        {
            public ImmutableArray<ManualWatcher> Watchers { get; private set; } =
                ImmutableArray<ManualWatcher>.Empty;

            public ImmutableArray<IDevFileWatcher> CreateWatchers(ImmutableArray<string> watchPaths)
            {
                Watchers = watchPaths
                    .Select(static path => new ManualWatcher(path))
                    .ToImmutableArray();
                return Watchers.Cast<IDevFileWatcher>().ToImmutableArray();
            }
        }

        private sealed class ManualWatcher : IDevFileWatcher
        {
            public ManualWatcher(string path)
            {
                Path = path;
            }

            public event EventHandler<DevFileChangedEventArgs>? Changed;

            public string Path { get; }

            public bool Started { get; private set; }

            public bool Disposed { get; private set; }

            public void Start()
            {
                Started = true;
            }

            public void RaiseChanged(string filePath)
            {
                Changed?.Invoke(this, new DevFileChangedEventArgs(filePath));
            }

            public ValueTask DisposeAsync()
            {
                Disposed = true;
                return ValueTask.CompletedTask;
            }
        }

        private sealed class ManualDebouncerFactory : IDevDebouncerFactory
        {
            public ManualDebouncer LastDebouncer { get; private set; } = new();

            public IDevDebouncer Create(TimeSpan delay)
            {
                LastDebouncer = new ManualDebouncer();
                return LastDebouncer;
            }
        }

        private sealed class ManualDebouncer : IDevDebouncer
        {
            private Func<CancellationToken, Task>? pending;

            public int ScheduleCount { get; private set; }

            public int RunCount { get; private set; }

            public bool Disposed { get; private set; }

            public void Schedule(Func<CancellationToken, Task> action)
            {
                ScheduleCount++;
                pending = action;
            }

            public async Task FlushAsync(CancellationToken cancellationToken = default)
            {
                if (pending is null)
                {
                    return;
                }

                Func<CancellationToken, Task> action = pending;
                pending = null;
                RunCount++;
                await action(cancellationToken);
            }

            public ValueTask DisposeAsync()
            {
                Disposed = true;
                pending = null;
                return ValueTask.CompletedTask;
            }
        }

        private sealed class RecordingDevHostHook : IDevHostHook
        {
            public int StartCallCount { get; private set; }

            public int ReloadCallCount { get; private set; }

            public int StopCallCount { get; private set; }

            public int DisposeCallCount { get; private set; }

            public DevHostStartRequest? LastStartRequest { get; private set; }

            public DevHostReloadRequest? LastReloadRequest { get; private set; }

            public Task StartAsync(DevHostStartRequest request, CancellationToken cancellationToken = default)
            {
                StartCallCount++;
                LastStartRequest = request;
                return Task.CompletedTask;
            }

            public Task ReloadAsync(DevHostReloadRequest request, CancellationToken cancellationToken = default)
            {
                ReloadCallCount++;
                LastReloadRequest = request;
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken = default)
            {
                StopCallCount++;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                DisposeCallCount++;
                return ValueTask.CompletedTask;
            }
        }
    }
}
