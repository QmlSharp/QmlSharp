namespace QmlSharp.DevTools.Tests
{
    public sealed class TestHarnessSmokeTests
    {
        [Fact]
        public async Task FakeCompiler_QueuedResult_IsReturnedAndRequestCaptured()
        {
            FakeDevToolsCompiler compiler = new();
            CompilationResult expected = DevToolsTestFixtures.SuccessfulCompilationResult();
            CompilerOptions options = DevToolsTestFixtures.CompilerOptions();
            FileChangeBatch batch = DevToolsTestFixtures.FileChangeBatch();

            compiler.QueueResult(expected);
            CompilationResult result = await compiler.CompileChangedAsync(options, batch, CancellationToken.None);

            Assert.Same(expected, result);
            FakeCompilerRequest request = Assert.Single(compiler.Requests);
            Assert.Same(options, request.Options);
            Assert.Same(batch, request.Changes);
        }

        [Fact]
        public async Task FakeNativeHost_CapturesCallsAndReturnsQueuedState()
        {
            FakeNativeHost host = new()
            {
                Snapshots = ImmutableArray.Create(DevToolsTestFixtures.InstanceSnapshot()),
                Instances = ImmutableArray.Create(DevToolsTestFixtures.InstanceInfo()),
                QmlEvaluationResult = "4",
            };
            OverlayError error = new("Compilation Error", "broken", null, null, null);

            IReadOnlyList<InstanceSnapshot> snapshots = await host.CaptureSnapshotsAsync(CancellationToken.None);
            await host.ReloadQmlAsync("dist/Main.qml", CancellationToken.None);
            await host.ShowOverlayAsync(error, CancellationToken.None);
            string qmlResult = await host.EvaluateQmlAsync("2 + 2", CancellationToken.None);

            _ = Assert.Single(snapshots);
            Assert.Equal("dist/Main.qml", Assert.Single(host.ReloadedQmlPaths));
            Assert.Same(error, Assert.Single(host.ShownErrors));
            Assert.Equal("4", qmlResult);
        }

        [Fact]
        public async Task FakeBuildPipeline_QueuedResult_IsReturnedAndProgressRegistered()
        {
            FakeBuildPipeline pipeline = new();
            BuildResult expected = DevToolsTestFixtures.SuccessfulBuildResult();
            BuildContext context = DevToolsTestFixtures.BuildContext();

            pipeline.QueueResult(expected);
            pipeline.OnProgress(static _ => { });
            BuildResult result = await pipeline.BuildPhasesAsync(
                context,
                ImmutableArray.Create(BuildPhase.CSharpCompilation),
                CancellationToken.None);

            Assert.Same(expected, result);
            _ = Assert.Single(pipeline.ProgressCallbacks);
            FakeBuildRequest request = Assert.Single(pipeline.Requests);
            Assert.Same(context, request.Context);
            Assert.Equal(BuildPhase.CSharpCompilation, Assert.Single(request.Phases));
        }

        [Fact]
        public void FakeFileWatcherTimerAndConsoleCapture_ExposeDeterministicHooks()
        {
            FakeFileWatcher watcher = new();
            FileChangeBatch batch = DevToolsTestFixtures.FileChangeBatch();
            FileChangeBatch? observedBatch = null;
            FakeDevToolsTimer timer = new();
            bool timerFired = false;
            ConsoleCapture console = new();

            watcher.OnChange += value => observedBatch = value;
            watcher.Start();
            watcher.Emit(batch);
            timer.Tick += () => timerFired = true;
            timer.Change(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2));
            timer.Fire();
            console.Write("hello");
            console.WriteLine(" world");

            Assert.Equal(FileWatcherStatus.Running, watcher.Status);
            Assert.Same(batch, observedBatch);
            Assert.True(timerFired);
            Assert.Equal(TimeSpan.FromMilliseconds(1), timer.DueTime);
            Assert.Contains("hello world", console.GetOutput(), StringComparison.Ordinal);
        }

        [Fact]
        public void PerformanceTimingHelper_CreatesProfilerRecord()
        {
            PerfRecord record = PerformanceTimingHelper.CreateRecord(duration: TimeSpan.FromMilliseconds(3));

            Assert.Equal("compile", record.Name);
            Assert.Equal(PerfCategory.Compile, record.Category);
            Assert.Equal(TimeSpan.FromMilliseconds(3), record.Duration);
        }
    }
}
