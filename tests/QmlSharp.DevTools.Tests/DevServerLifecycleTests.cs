#pragma warning disable MA0048

namespace QmlSharp.DevTools.Tests
{
    public sealed class DevServerLifecycleTests
    {
        [Fact]
        public async Task StartAsync_DSV_01_TransitionsFromIdleThroughInitialBuildToRunning()
        {
            ServerHarness harness = CreateHarness();
            List<DevServerStatusChangedEvent> events = CaptureEvents(harness.Server);

            await harness.Server.StartAsync();

            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
            Assert.Equal(
                new[]
                {
                    DevServerStatus.Starting,
                    DevServerStatus.Building,
                    DevServerStatus.Running,
                },
                events.Select(static statusChanged => statusChanged.Current).ToArray());
            Assert.Equal(DevServerStatus.Idle, events[0].Previous);
            _ = Assert.Single(harness.BuildPipeline.Requests);
            Assert.Equal(1, harness.FileWatcher.StartCalls);
            Assert.Equal(FileWatcherStatus.Running, harness.FileWatcher.Status);
            Assert.Equal(1, harness.Console.BannerCalls);
            Assert.Equal(1, harness.Console.BuildStartCalls);
            Assert.Equal(1, harness.Console.BuildSuccessCalls);
            Assert.Equal(1, harness.Console.WatchStartedCalls);
            Assert.False(harness.Overlay.IsVisible);
            Assert.Equal(1, harness.Server.Stats.BuildCount);
        }

        [Fact]
        public async Task StartAsync_DSV_02_FailedInitialBuildTransitionsToErrorAndShowsOverlay()
        {
            ServerHarness harness = CreateHarness();
            harness.BuildPipeline.QueueResult(FailedBuildResult());
            List<DevServerStatusChangedEvent> events = CaptureEvents(harness.Server);

            await harness.Server.StartAsync();

            Assert.Equal(DevServerStatus.Error, harness.Server.Status);
            Assert.Equal(
                new[]
                {
                    DevServerStatus.Starting,
                    DevServerStatus.Building,
                    DevServerStatus.Error,
                },
                events.Select(static statusChanged => statusChanged.Current).ToArray());
            _ = Assert.Single(harness.BuildPipeline.Requests);
            Assert.Equal(0, harness.FileWatcher.StartCalls);
            Assert.Equal(1, harness.Overlay.ShowCalls);
            Assert.True(harness.Overlay.IsVisible);
            Assert.Equal("QMLSHARP-B010: compile failed", harness.Overlay.Errors[0].Message);
            Assert.Equal(1, harness.Console.ErrorCalls);
            Assert.Equal(1, harness.Server.Stats.BuildCount);
            Assert.Equal(1, harness.Server.Stats.ErrorCount);
        }

        [Fact]
        public async Task StopAsync_DSV_03_FromRunningTransitionsThroughStoppingToIdleAndCleansResources()
        {
            ServerHarness harness = CreateHarness();
            await harness.Server.StartAsync();
            List<DevServerStatusChangedEvent> events = CaptureEvents(harness.Server);
            int hideCallsBeforeStop = harness.Overlay.HideCalls;

            await harness.Server.StopAsync();

            Assert.Equal(DevServerStatus.Idle, harness.Server.Status);
            Assert.Equal(
                new[]
                {
                    DevServerStatus.Stopping,
                    DevServerStatus.Idle,
                },
                events.Select(static statusChanged => statusChanged.Current).ToArray());
            Assert.Equal(DevServerStatus.Running, events[0].Previous);
            Assert.Equal(1, harness.FileWatcher.StopCalls);
            Assert.Equal(FileWatcherStatus.Idle, harness.FileWatcher.Status);
            Assert.Equal(hideCallsBeforeStop + 1, harness.Overlay.HideCalls);
            Assert.Equal(1, harness.Console.ServerStoppedCalls);
            Assert.Equal(TimeSpan.Zero, harness.Server.Stats.Uptime);
        }

        [Fact]
        public async Task StopAsync_DSV_04_FromErrorTransitionsThroughStoppingToIdleAndHidesOverlay()
        {
            ServerHarness harness = CreateHarness();
            harness.BuildPipeline.QueueResult(FailedBuildResult());
            await harness.Server.StartAsync();
            List<DevServerStatusChangedEvent> events = CaptureEvents(harness.Server);

            await harness.Server.StopAsync();

            Assert.Equal(DevServerStatus.Idle, harness.Server.Status);
            Assert.Equal(
                new[]
                {
                    DevServerStatus.Stopping,
                    DevServerStatus.Idle,
                },
                events.Select(static statusChanged => statusChanged.Current).ToArray());
            Assert.Equal(DevServerStatus.Error, events[0].Previous);
            Assert.Equal(0, harness.FileWatcher.StopCalls);
            Assert.False(harness.Overlay.IsVisible);
            Assert.Equal(1, harness.Console.ServerStoppedCalls);
        }

        [Fact]
        public async Task OnStatusChanged_DSV_13_PublishesPreviousCurrentTimestampAndReason()
        {
            ManualDevToolsClock clock = new()
            {
                UtcNow = DateTimeOffset.Parse(
                    "2026-05-06T12:00:00Z",
                    null,
                    System.Globalization.DateTimeStyles.AssumeUniversal),
            };
            ServerHarness harness = CreateHarness(clock: clock);
            List<DevServerStatusChangedEvent> events = CaptureEvents(harness.Server);

            await harness.Server.StartAsync();
            await harness.Server.StopAsync();

            Assert.Equal(5, events.Count);
            Assert.Collection(
                events,
                statusChanged => AssertTransition(statusChanged, DevServerStatus.Idle, DevServerStatus.Starting, clock.UtcNow),
                statusChanged => AssertTransition(statusChanged, DevServerStatus.Starting, DevServerStatus.Building, clock.UtcNow),
                statusChanged => AssertTransition(statusChanged, DevServerStatus.Building, DevServerStatus.Running, clock.UtcNow),
                statusChanged => AssertTransition(statusChanged, DevServerStatus.Running, DevServerStatus.Stopping, clock.UtcNow),
                statusChanged => AssertTransition(statusChanged, DevServerStatus.Stopping, DevServerStatus.Idle, clock.UtcNow));
            Assert.All(events, static statusChanged => Assert.False(string.IsNullOrWhiteSpace(statusChanged.Reason)));
        }

        [Fact]
        public async Task Stats_DSV_14_CountsInitialBuildRebuildsHotReloadsErrorsAndUptime()
        {
            ManualDevToolsClock clock = new();
            ServerHarness harness = CreateHarness(clock: clock);
            harness.BuildPipeline.QueueResult(SuccessfulBuildResult(TimeSpan.FromMilliseconds(17), filesCompiled: 3));
            harness.BuildPipeline.QueueResult(SuccessfulBuildResult(TimeSpan.FromMilliseconds(5), filesCompiled: 1));
            harness.BuildPipeline.QueueResult(SuccessfulBuildResult(TimeSpan.FromMilliseconds(7), filesCompiled: 2));
            await harness.Server.StartAsync();
            clock.Advance(TimeSpan.FromSeconds(3));

            HotReloadResult firstRebuild = await harness.Server.RebuildAsync();
            HotReloadResult secondRebuild = await harness.Server.RebuildAsync();

            ServerStats stats = harness.Server.Stats;
            Assert.True(firstRebuild.Success);
            Assert.True(secondRebuild.Success);
            Assert.Equal(1, stats.BuildCount);
            Assert.Equal(2, stats.RebuildCount);
            Assert.Equal(0, stats.HotReloadCount);
            Assert.Equal(0, stats.ErrorCount);
            Assert.Equal(TimeSpan.FromMilliseconds(29), stats.TotalBuildTime);
            Assert.Equal(TimeSpan.Zero, stats.TotalHotReloadTime);
            Assert.Equal(TimeSpan.FromSeconds(3), stats.Uptime);
            Assert.Equal(3, harness.Console.BuildSuccessCalls);
            Assert.Equal(0, harness.Console.HotReloadSuccessCalls);
            Assert.Equal(3, harness.BuildPipeline.Requests.Count);
            Assert.Equal(3, harness.Profiler.GetRecords().Count);
        }

        [Fact]
        public async Task RebuildAsync_RunningServer_RunsBuildPipelineBeforeReportingSuccess()
        {
            ServerHarness harness = CreateHarness();
            harness.BuildPipeline.QueueResult(SuccessfulBuildResult(TimeSpan.FromMilliseconds(11), filesCompiled: 2));
            harness.BuildPipeline.QueueResult(SuccessfulBuildResult(TimeSpan.FromMilliseconds(13), filesCompiled: 4));
            await harness.Server.StartAsync();

            HotReloadResult result = await harness.Server.RebuildAsync();

            Assert.True(result.Success);
            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
            Assert.Equal(2, harness.BuildPipeline.Requests.Count);
            Assert.Equal(1, harness.Server.Stats.BuildCount);
            Assert.Equal(1, harness.Server.Stats.RebuildCount);
            Assert.Equal(0, harness.Server.Stats.HotReloadCount);
            Assert.Equal(TimeSpan.FromMilliseconds(24), harness.Server.Stats.TotalBuildTime);
            Assert.Equal(2, harness.Console.BuildSuccessCalls);
            Assert.Equal(0, harness.Console.HotReloadSuccessCalls);
            Assert.False(harness.Overlay.IsVisible);
        }

        [Fact]
        public async Task RebuildAsync_FailedBuild_ReturnsFailureAndTransitionsToError()
        {
            ServerHarness harness = CreateHarness();
            harness.BuildPipeline.QueueResult(SuccessfulBuildResult(TimeSpan.FromMilliseconds(11), filesCompiled: 2));
            harness.BuildPipeline.QueueResult(FailedBuildResult());
            await harness.Server.StartAsync();

            HotReloadResult result = await harness.Server.RebuildAsync();

            Assert.False(result.Success);
            Assert.Equal(DevServerStatus.Error, harness.Server.Status);
            Assert.Equal(2, harness.BuildPipeline.Requests.Count);
            Assert.Equal(1, harness.Server.Stats.RebuildCount);
            Assert.Equal(1, harness.Server.Stats.ErrorCount);
            Assert.True(harness.Overlay.IsVisible);
            Assert.Contains("compile failed", result.ErrorMessage, StringComparison.Ordinal);
            Assert.Contains("Rebuild failed", harness.Console.Errors[0], StringComparison.Ordinal);
        }

        [Fact]
        public async Task RebuildAsync_AfterInitialBuildFailure_StartsRuntimeResourcesBeforeRunning()
        {
            ServerHarness harness = CreateHarness();
            harness.BuildPipeline.QueueResult(FailedBuildResult());
            harness.BuildPipeline.QueueResult(SuccessfulBuildResult(TimeSpan.FromMilliseconds(13), filesCompiled: 4));
            await harness.Server.StartAsync();

            HotReloadResult result = await harness.Server.RebuildAsync();

            Assert.True(result.Success);
            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
            Assert.Equal(1, harness.FileWatcher.StartCalls);
            Assert.Equal(FileWatcherStatus.Running, harness.FileWatcher.Status);
            Assert.False(harness.Overlay.IsVisible);
            Assert.Equal(1, harness.Server.Stats.BuildCount);
            Assert.Equal(1, harness.Server.Stats.RebuildCount);
            Assert.Equal(1, harness.Server.Stats.ErrorCount);
        }

        [Fact]
        public async Task StartAsync_WhenWatcherStartupFails_CleansResourcesAndReportsError()
        {
            RecordingFileWatcher fileWatcher = new()
            {
                StartException = new InvalidOperationException("watcher failed"),
            };
            ServerHarness harness = CreateHarness(fileWatcher: fileWatcher);
            List<DevServerStatusChangedEvent> events = CaptureEvents(harness.Server);

            await harness.Server.StartAsync();

            Assert.Equal(DevServerStatus.Error, harness.Server.Status);
            Assert.Equal(1, harness.FileWatcher.StartCalls);
            Assert.Equal(1, harness.FileWatcher.StopCalls);
            Assert.Equal(1, harness.Overlay.ShowCalls);
            Assert.True(harness.Overlay.IsVisible);
            Assert.Contains("watcher failed", harness.Overlay.Errors[0].Message, StringComparison.Ordinal);
            Assert.Equal(1, harness.Console.ErrorCalls);
            Assert.Equal(1, harness.Server.Stats.ErrorCount);
            Assert.Equal(DevServerStatus.Error, events[^1].Current);
        }

        [Fact]
        public async Task StartAsync_WhenInitialBuildIsCanceled_ReturnsToIdleAndAllowsRetry()
        {
            using CancellationTokenSource cancellation = new();
            FakeBuildPipeline buildPipeline = new()
            {
                OnBuild = _ => cancellation.Cancel(),
            };
            ServerHarness harness = CreateHarness(buildPipeline: buildPipeline);
            List<DevServerStatusChangedEvent> events = CaptureEvents(harness.Server);

            _ = await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await harness.Server.StartAsync(cancellation.Token));
            buildPipeline.OnBuild = null;
            await harness.Server.StartAsync();

            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
            Assert.Equal(
                new[]
                {
                    DevServerStatus.Starting,
                    DevServerStatus.Building,
                    DevServerStatus.Idle,
                    DevServerStatus.Starting,
                    DevServerStatus.Building,
                    DevServerStatus.Running,
                },
                events.Select(static statusChanged => statusChanged.Current).ToArray());
            Assert.Equal(2, buildPipeline.Requests.Count);
            Assert.Equal(1, harness.FileWatcher.StartCalls);
            Assert.Equal(0, harness.Server.Stats.ErrorCount);
        }

        [Fact]
        public async Task RestartAsync_StopsAndStartsWithFreshInitialBuild()
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

        [Fact]
        public async Task DisposeAsync_StopsAndDisposesResourcesAndIsIdempotent()
        {
            RecordingRepl repl = new();
            ServerHarness harness = CreateHarness(
                options: ServerOptions(enableRepl: true),
                repl: repl);
            await harness.Server.StartAsync();

            await harness.Server.DisposeAsync();
            await harness.Server.DisposeAsync();

            Assert.Equal(DevServerStatus.Idle, harness.Server.Status);
            Assert.Equal(1, harness.FileWatcher.StopCalls);
            Assert.Equal(1, harness.FileWatcher.DisposeCalls);
            Assert.Equal(1, repl.StartCalls);
            Assert.Equal(1, repl.StopCalls);
            Assert.Equal(1, repl.DisposeCalls);
            _ = await Assert.ThrowsAsync<ObjectDisposedException>(() => harness.Server.StartAsync());
        }

        private static List<DevServerStatusChangedEvent> CaptureEvents(IDevServer server)
        {
            List<DevServerStatusChangedEvent> events = new();
            server.OnStatusChanged += events.Add;
            return events;
        }

        private static ServerHarness CreateHarness(
            DevServerOptions? options = null,
            RecordingFileWatcher? fileWatcher = null,
            FakeBuildPipeline? buildPipeline = null,
            RecordingDevConsole? console = null,
            RecordingErrorOverlay? overlay = null,
            ManualDevToolsClock? clock = null,
            RecordingRepl? repl = null)
        {
            DevServerOptions serverOptions = options ?? ServerOptions();
            RecordingFileWatcher serverFileWatcher = fileWatcher ?? new RecordingFileWatcher();
            FakeBuildPipeline serverBuildPipeline = buildPipeline ?? new FakeBuildPipeline();
            RecordingDevConsole serverConsole = console ?? new RecordingDevConsole();
            RecordingErrorOverlay serverOverlay = overlay ?? new RecordingErrorOverlay();
            ManualDevToolsClock serverClock = clock ?? new ManualDevToolsClock();
            PerfProfiler profiler = new(serverClock);
            DevServer server = new(
                serverOptions,
                DevToolsTestFixtures.BuildContext(),
                serverFileWatcher,
                serverBuildPipeline,
                serverConsole,
                serverOverlay,
                profiler,
                repl,
                serverClock);

            return new ServerHarness(
                server,
                serverFileWatcher,
                serverBuildPipeline,
                serverConsole,
                serverOverlay,
                profiler,
                serverClock,
                repl);
        }

        private static DevServerOptions ServerOptions(bool enableRepl = false)
        {
            return new DevServerOptions(
                "C:/repo",
                new FileWatcherOptions(ImmutableArray.Create("C:/repo/src")),
                new DevConsoleOptions(LogLevel.Silent, Color: false, ShowTimestamps: false),
                EnableRepl: enableRepl,
                EnableProfiling: true);
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
            return new BuildResult
            {
                Success = false,
                PhaseResults = ImmutableArray.Create(new PhaseResult(
                    BuildPhase.CSharpCompilation,
                    Success: false,
                    Duration: TimeSpan.FromMilliseconds(3),
                    Diagnostics: ImmutableArray.Create(BuildFailureDiagnostic()))),
                Diagnostics = ImmutableArray.Create(BuildFailureDiagnostic()),
                Stats = new BuildStats(
                    TimeSpan.FromMilliseconds(3),
                    FilesCompiled: 0,
                    SchemasGenerated: 0,
                    CppFilesGenerated: 0,
                    AssetsCollected: 0,
                    NativeLibBuilt: false),
            };
        }

        private static BuildDiagnostic BuildFailureDiagnostic()
        {
            return new BuildDiagnostic(
                BuildDiagnosticCode.CompilationFailed,
                BuildDiagnosticSeverity.Error,
                "compile failed",
                BuildPhase.CSharpCompilation,
                "C:/repo/src/App.cs");
        }

        private static void AssertTransition(
            DevServerStatusChangedEvent statusChanged,
            DevServerStatus previous,
            DevServerStatus current,
            DateTimeOffset timestamp)
        {
            Assert.Equal(previous, statusChanged.Previous);
            Assert.Equal(current, statusChanged.Current);
            Assert.Equal(timestamp, statusChanged.Timestamp);
        }

        private sealed record ServerHarness(
            DevServer Server,
            RecordingFileWatcher FileWatcher,
            FakeBuildPipeline BuildPipeline,
            RecordingDevConsole Console,
            RecordingErrorOverlay Overlay,
            PerfProfiler Profiler,
            ManualDevToolsClock Clock,
            RecordingRepl? Repl);

        private sealed class RecordingFileWatcher : IFileWatcher
        {
            public event Action<FileChangeBatch> OnChange = static _ => { };

            public int StartCalls { get; private set; }

            public int StopCalls { get; private set; }

            public int DisposeCalls { get; private set; }

            public Exception? StartException { get; init; }

            public FileWatcherStatus Status { get; private set; } = FileWatcherStatus.Idle;

            public void Start()
            {
                StartCalls++;
                if (StartException is not null)
                {
                    throw StartException;
                }

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

        private sealed class RecordingDevConsole : IDevConsole
        {
            public int BannerCalls { get; private set; }

            public int WatchStartedCalls { get; private set; }

            public int BuildStartCalls { get; private set; }

            public int BuildSuccessCalls { get; private set; }

            public int HotReloadSuccessCalls { get; private set; }

            public int ServerStoppedCalls { get; private set; }

            public int ErrorCalls { get; private set; }

            public List<string> Errors { get; } = new();

            public void Banner(string version, DevServerOptions options)
            {
                BannerCalls++;
            }

            public void WatchStarted(int fileCount, IReadOnlyList<string> paths)
            {
                WatchStartedCalls++;
            }

            public void FileChanged(FileChangeBatch batch)
            {
            }

            public void BuildStart(int fileCount)
            {
                BuildStartCalls++;
            }

            public void BuildSuccess(TimeSpan elapsed, int fileCount)
            {
                BuildSuccessCalls++;
            }

            public void BuildError(IReadOnlyList<CompilerDiagnostic> errors)
            {
            }

            public void HotReloadSuccess(HotReloadResult result)
            {
                HotReloadSuccessCalls++;
            }

            public void HotReloadError(string message)
            {
            }

            public void RestartRequired(string reason)
            {
            }

            public void ServerStopped()
            {
                ServerStoppedCalls++;
            }

            public void Info(string message)
            {
            }

            public void Warn(string message)
            {
            }

            public void Error(string message)
            {
                ErrorCalls++;
                Errors.Add(message);
            }
        }

        private sealed class RecordingErrorOverlay : IErrorOverlay
        {
            public int ShowCalls { get; private set; }

            public int HideCalls { get; private set; }

            public bool IsVisible { get; private set; }

            public IReadOnlyList<OverlayError> Errors { get; private set; } = ImmutableArray<OverlayError>.Empty;

            public void Show(OverlayError error)
            {
                Show(ImmutableArray.Create(error));
            }

            public void Show(IReadOnlyList<OverlayError> errors)
            {
                ShowCalls++;
                Errors = errors.ToImmutableArray();
                IsVisible = true;
            }

            public void Hide()
            {
                HideCalls++;
                IsVisible = false;
            }
        }

        private sealed class RecordingRepl : IRepl
        {
            public int StartCalls { get; private set; }

            public int StopCalls { get; private set; }

            public int DisposeCalls { get; private set; }

            public ReplMode Mode { get; set; }

            public bool IsRunning { get; private set; }

            public IReadOnlyList<string> History => ImmutableArray<string>.Empty;

            public Task StartAsync(CancellationToken cancellationToken = default)
            {
                StartCalls++;
                IsRunning = true;
                return Task.CompletedTask;
            }

            public Task StopAsync()
            {
                StopCalls++;
                IsRunning = false;
                return Task.CompletedTask;
            }

            public Task<ReplResult> EvalAsync(string input, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new ReplResult(
                    Success: true,
                    Output: string.Empty,
                    ReturnType: null,
                    Elapsed: TimeSpan.Zero,
                    Error: null));
            }

            public ValueTask DisposeAsync()
            {
                DisposeCalls++;
                return ValueTask.CompletedTask;
            }
        }
    }
}

#pragma warning restore MA0048
