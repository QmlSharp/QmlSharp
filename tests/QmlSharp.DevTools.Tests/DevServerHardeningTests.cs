namespace QmlSharp.DevTools.Tests
{
    public sealed class DevServerHardeningTests
    {
        [Fact]
        public async Task RepeatedFileChanges_DuringCompile_OnlyLatestPendingBatchRuns()
        {
            ServerHarness harness = CreateHarness();
            TaskCompletionSource compileStarted = NewSignal();
            TaskCompletionSource releaseCompile = NewSignal();
            int compileCalls = 0;
            harness.Compiler.OnCompileAsync = async (_, token) =>
            {
                compileCalls++;
                if (compileCalls == 1)
                {
                    compileStarted.SetResult();
                    await releaseCompile.Task.WaitAsync(token);
                }

                return DevToolsTestFixtures.CompilationResultWithSchema();
            };
            await harness.Server.StartAsync();

            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/First.cs"));
            await compileStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            for (int index = 0; index < 25; index++)
            {
                harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/Pending" + index + ".cs"));
            }

            releaseCompile.SetResult();
            await WaitUntilAsync(() => harness.Compiler.Requests.Count == 2);

            Assert.Equal("C:/repo/src/Pending24.cs", harness.Compiler.Requests[1].Changes?.Changes[0].FilePath);
            Assert.Equal(2, harness.Server.Stats.RebuildCount);
            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
        }

        [Fact]
        public async Task RepeatedFileChanges_DuringHotReload_OnlyLatestPendingBatchRuns()
        {
            ServerHarness harness = CreateHarness();
            TaskCompletionSource reloadStarted = NewSignal();
            TaskCompletionSource releaseReload = NewSignal();
            int reloadCalls = 0;
            harness.HotReload.OnReloadAsync = async (_, token) =>
            {
                reloadCalls++;
                if (reloadCalls == 1)
                {
                    reloadStarted.SetResult();
                    await releaseReload.Task.WaitAsync(token);
                }

                return FakeHotReloadOrchestrator.SuccessfulResult();
            };
            await harness.Server.StartAsync();

            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/First.cs"));
            await reloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            for (int index = 0; index < 25; index++)
            {
                harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/ReloadPending" + index + ".cs"));
            }

            releaseReload.SetResult();
            await WaitUntilAsync(() => harness.Compiler.Requests.Count == 2 && harness.HotReload.Requests.Count == 2)
                ;

            Assert.Equal("C:/repo/src/ReloadPending24.cs", harness.Compiler.Requests[1].Changes?.Changes[0].FilePath);
            Assert.Equal(2, harness.Server.Stats.HotReloadCount);
            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
        }

        [Fact]
        public async Task RebuildAsync_CancellationDuringCompile_ClearsGuardAndAllowsRetry()
        {
            using CancellationTokenSource cancellation = new();
            ServerHarness harness = CreateHarness();
            TaskCompletionSource compileStarted = NewSignal();
            harness.Compiler.OnCompileAsync = async (_, token) =>
            {
                compileStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return DevToolsTestFixtures.CompilationResultWithSchema();
            };
            await harness.Server.StartAsync();

            Task<HotReloadResult> rebuild = harness.Server.RebuildAsync(cancellation.Token);
            await compileStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await rebuild);

            harness.Compiler.OnCompileAsync = null;
            HotReloadResult retry = await harness.Server.RebuildAsync();

            Assert.True(retry.Success);
            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
            Assert.Equal(2, harness.Compiler.Requests.Count);
            Assert.Equal(1, harness.Server.Stats.HotReloadCount);
        }

        [Fact]
        public async Task RebuildAsync_CancellationDuringHotReload_ClearsGuardAndAllowsRetry()
        {
            using CancellationTokenSource cancellation = new();
            ServerHarness harness = CreateHarness();
            TaskCompletionSource reloadStarted = NewSignal();
            harness.HotReload.OnReloadAsync = async (_, token) =>
            {
                reloadStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return FakeHotReloadOrchestrator.SuccessfulResult();
            };
            await harness.Server.StartAsync();

            Task<HotReloadResult> rebuild = harness.Server.RebuildAsync(cancellation.Token);
            await reloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await rebuild);

            harness.HotReload.OnReloadAsync = null;
            HotReloadResult retry = await harness.Server.RebuildAsync();

            Assert.True(retry.Success);
            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
            Assert.Equal(2, harness.HotReload.Requests.Count);
            Assert.Equal(1, harness.Server.Stats.HotReloadCount);
        }

        [Fact]
        public async Task RestartAsync_WhileWatcherRebuildPending_CancelsPendingWorkAndRestarts()
        {
            ServerHarness harness = CreateHarness();
            TaskCompletionSource compileStarted = NewSignal();
            harness.Compiler.OnCompileAsync = async (_, token) =>
            {
                compileStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return DevToolsTestFixtures.CompilationResultWithSchema();
            };
            await harness.Server.StartAsync();

            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/First.cs"));
            await compileStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/Pending.cs"));

            await harness.Server.RestartAsync().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
            Assert.Equal(2, harness.FileWatcher.StartCalls);
            Assert.Equal(1, harness.FileWatcher.StopCalls);
            Assert.Equal(2, harness.BuildPipeline.Requests.Count);
            _ = Assert.Single(harness.Compiler.Requests);
            Assert.Empty(harness.HotReload.Requests);
        }

        [Fact]
        public async Task StopAsync_WhileWatcherRebuildPending_CancelsPendingWorkAndStops()
        {
            ServerHarness harness = CreateHarness();
            TaskCompletionSource compileStarted = NewSignal();
            harness.Compiler.OnCompileAsync = async (_, token) =>
            {
                compileStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return DevToolsTestFixtures.CompilationResultWithSchema();
            };
            await harness.Server.StartAsync();

            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/First.cs"));
            await compileStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/Pending.cs"));

            await harness.Server.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/AfterStop.cs"));

            Assert.Equal(DevServerStatus.Idle, harness.Server.Status);
            Assert.Equal(1, harness.FileWatcher.StopCalls);
            _ = Assert.Single(harness.Compiler.Requests);
            Assert.Empty(harness.HotReload.Requests);
        }

        [Fact]
        public async Task StartStop_RepeatedRuns_DisposesLifecycleResourcesAndIgnoresPostStopChanges()
        {
            RecordingRepl repl = new();
            ServerHarness harness = CreateHarness(
                new DevServerOptions(
                    "C:/repo",
                    new FileWatcherOptions(ImmutableArray.Create("C:/repo/src")),
                    new DevConsoleOptions(LogLevel.Silent, Color: false, ShowTimestamps: false),
                    EnableRepl: true,
                    EnableProfiling: true),
                repl: repl);

            for (int iteration = 0; iteration < 20; iteration++)
            {
                await harness.Server.StartAsync();
                await harness.Server.StopAsync();
                int requestCount = harness.Compiler.Requests.Count;
                harness.FileWatcher.Emit(FileChangeBatch("C:/repo/src/AfterStop" + iteration + ".cs"));
                Assert.Equal(requestCount, harness.Compiler.Requests.Count);
            }

            await harness.Server.DisposeAsync();

            Assert.Equal(20, harness.FileWatcher.StartCalls);
            Assert.Equal(20, harness.FileWatcher.StopCalls);
            Assert.Equal(1, harness.FileWatcher.DisposeCalls);
            Assert.Equal(20, repl.StartCalls);
            Assert.Equal(20, repl.StopCalls);
            Assert.Equal(1, repl.DisposeCalls);
            Assert.Equal(DevServerStatus.Idle, harness.Server.Status);
        }

        private static ServerHarness CreateHarness(
            DevServerOptions? options = null,
            CountingFileWatcher? fileWatcher = null,
            FakeBuildPipeline? buildPipeline = null,
            FakeDevToolsCompiler? compiler = null,
            FakeHotReloadOrchestrator? hotReload = null,
            RecordingRepl? repl = null)
        {
            CountingFileWatcher serverFileWatcher = fileWatcher ?? new CountingFileWatcher();
            FakeBuildPipeline serverBuildPipeline = buildPipeline ?? new FakeBuildPipeline();
            FakeDevToolsCompiler serverCompiler = compiler ?? new FakeDevToolsCompiler();
            FakeHotReloadOrchestrator serverHotReload = hotReload ?? new FakeHotReloadOrchestrator();
            ManualDevToolsClock clock = new();
            DevServer server = new(
                options ?? ServerOptions(),
                DevToolsTestFixtures.BuildContext(),
                serverFileWatcher,
                serverBuildPipeline,
                serverCompiler,
                DevToolsTestFixtures.CompilerOptions(),
                serverHotReload,
                new SilentDevConsole(),
                new RecordingErrorOverlay(),
                new PerfProfiler(clock),
                repl,
                clock,
                new SchemaDiffer());

            return new ServerHarness(
                server,
                serverFileWatcher,
                serverBuildPipeline,
                serverCompiler,
                serverHotReload);
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

        private static TaskCompletionSource NewSignal()
        {
            return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static async Task WaitUntilAsync(Func<bool> predicate)
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            while (!predicate())
            {
                await Task.Delay(10, timeout.Token);
            }
        }

        private sealed record ServerHarness(
            DevServer Server,
            CountingFileWatcher FileWatcher,
            FakeBuildPipeline BuildPipeline,
            FakeDevToolsCompiler Compiler,
            FakeHotReloadOrchestrator HotReload);

        private sealed class CountingFileWatcher : IFileWatcher
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

        private sealed class SilentDevConsole : IDevConsole
        {
            public void Banner(string version, DevServerOptions options)
            {
            }

            public void WatchStarted(int fileCount, IReadOnlyList<string> paths)
            {
            }

            public void FileChanged(FileChangeBatch batch)
            {
            }

            public void BuildStart(int fileCount)
            {
            }

            public void BuildSuccess(TimeSpan elapsed, int fileCount)
            {
            }

            public void BuildError(IReadOnlyList<CompilerDiagnostic> errors)
            {
            }

            public void HotReloadSuccess(HotReloadResult result)
            {
            }

            public void HotReloadError(string message)
            {
            }

            public void RestartRequired(string reason)
            {
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
            }
        }

        private sealed class RecordingErrorOverlay : IErrorOverlay
        {
            public bool IsVisible { get; private set; }

            public void Show(OverlayError error)
            {
                IsVisible = true;
            }

            public void Show(IReadOnlyList<OverlayError> errors)
            {
                IsVisible = true;
            }

            public void Hide()
            {
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
