using System.Diagnostics;

namespace QmlSharp.DevTools.Tests.Benchmarks
{
    [Collection(PerformanceTestCollection.Name)]
    public sealed class StartupBenchmarks
    {
        [Fact]
        [Trait("Category", DevToolsTestCategories.Performance)]
        [Trait("TestId", "PBM-11")]
        public async Task FileWatcherDebounceAccuracy_PBM_11_StaysWithinConfiguredTolerance()
        {
            string tempDirectory = Path.Join(Path.GetTempPath(), "qmlsharp-debounce-" + Path.GetRandomFileName());
            _ = Directory.CreateDirectory(tempDirectory);
            BenchmarkRawWatcher rawWatcher = new();
            FileWatcherOptions options = new(
                ImmutableArray.Create(tempDirectory),
                DebounceMs: 20,
                IncludePatterns: ImmutableArray.Create("**/*.cs"),
                ExcludePatterns: ImmutableArray<string>.Empty,
                UsePolling: false,
                PollIntervalMs: 100);
            using FileWatcher watcher = new(
                options,
                new BenchmarkRawWatcherFactory(rawWatcher),
                new SystemDevToolsTimerFactory(),
                SystemDevToolsClock.Instance);

            try
            {
                watcher.Start();
                TimeSpan[] samples = new TimeSpan[100];
                for (int index = 0; index < samples.Length; index++)
                {
                    TaskCompletionSource<TimeSpan> emitted = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    long start = Stopwatch.GetTimestamp();
                    void OnChange(FileChangeBatch batch)
                    {
                        _ = emitted.TrySetResult(Stopwatch.GetElapsedTime(start));
                    }

                    watcher.OnChange += OnChange;
                    rawWatcher.Emit(new FileChange(
                        Path.Join(tempDirectory, "File" + index + ".cs"),
                        FileChangeKind.Modified,
                        DateTimeOffset.UtcNow));
                    samples[index] = await emitted.Task.WaitAsync(TimeSpan.FromSeconds(2));
                    watcher.OnChange -= OnChange;
                }

                TimeSpan debounce = TimeSpan.FromMilliseconds(options.DebounceMs);
                TimeSpan p99 = BenchmarkAssert.Percentile99(samples);
                Assert.True(
                    p99 >= debounce - TimeSpan.FromMilliseconds(5),
                    "File watcher debounce P99 fired before the configured debounce window.");
                BenchmarkAssert.Under(
                    p99,
                    TimeSpan.FromMilliseconds(250),
                    "File watcher debounce P99");
            }
            finally
            {
                watcher.Stop();
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        [Trait("Category", DevToolsTestCategories.Performance)]
        [Trait("TestId", "PBM-12")]
        public async Task DevServerColdStartup_PBM_12_MeetsLatencyBudget()
        {
            TimeSpan[] samples = new TimeSpan[10];
            for (int index = 0; index < samples.Length; index++)
            {
                await using DevServer server = CreateServer();
                samples[index] = await BenchmarkAssert.MeasureAsync(
                    async () => await server.StartAsync());
                Assert.Equal(DevServerStatus.Running, server.Status);
            }

            BenchmarkAssert.Under(
                BenchmarkAssert.Percentile99(samples),
                TimeSpan.FromSeconds(5),
                "DevServer cold startup P99");
        }

        private static DevServer CreateServer()
        {
            ManualDevToolsClock clock = new();
            return new DevServer(
                new DevServerOptions(
                    "C:/repo",
                    new FileWatcherOptions(ImmutableArray.Create("C:/repo/src")),
                    new DevConsoleOptions(LogLevel.Silent, Color: false, ShowTimestamps: false),
                    EnableRepl: false,
                    EnableProfiling: true),
                DevToolsTestFixtures.BuildContext(),
                new FakeFileWatcher(),
                new FakeBuildPipeline(),
                new FakeDevToolsCompiler(),
                DevToolsTestFixtures.CompilerOptions(),
                new FakeHotReloadOrchestrator(),
                new SilentDevConsole(),
                new SilentErrorOverlay(),
                new PerfProfiler(clock),
                repl: null,
                clock,
                new SchemaDiffer());
        }

        private sealed class BenchmarkRawWatcher : IFileSystemWatcher
        {
            public event Action<FileChange> Changed = static _ => { };

            public void Start()
            {
            }

            public void Stop()
            {
            }

            public void Dispose()
            {
            }

            public void Emit(FileChange change)
            {
                Changed(change);
            }
        }

        private sealed class BenchmarkRawWatcherFactory : IFileSystemWatcherFactory
        {
            private readonly IFileSystemWatcher watcher;

            public BenchmarkRawWatcherFactory(IFileSystemWatcher watcher)
            {
                this.watcher = watcher;
            }

            public IFileSystemWatcher Create(FileWatcherOptions options)
            {
                return watcher;
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

        private sealed class SilentErrorOverlay : IErrorOverlay
        {
            public bool IsVisible => false;

            public void Show(OverlayError error)
            {
            }

            public void Show(IReadOnlyList<OverlayError> errors)
            {
            }

            public void Hide()
            {
            }
        }
    }
}
