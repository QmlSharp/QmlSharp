namespace QmlSharp.DevTools.Tests
{
    public sealed class FileWatcherTests
    {
        [Fact]
        [Trait("TestId", "FWA-01")]
        public void Start_WatchPath_StatusBecomesRunning()
        {
            FileWatcherHarness harness = FileWatcherHarness.Create();

            using FileWatcher watcher = harness.CreateWatcher();
            watcher.Start();

            Assert.Equal(FileWatcherStatus.Running, watcher.Status);
            Assert.True(harness.RawWatcher.Started);
            Assert.Equal(1, harness.RawWatcherFactory.CreateCalls);
        }

        [Fact]
        [Trait("TestId", "FWA-02")]
        public void Stop_AfterStart_StatusBecomesDisposed()
        {
            FileWatcherHarness harness = FileWatcherHarness.Create();

            using FileWatcher watcher = harness.CreateWatcher();
            watcher.Start();
            watcher.Stop();

            Assert.Equal(FileWatcherStatus.Disposed, watcher.Status);
            Assert.Equal(1, harness.RawWatcher.StopCalls);
            Assert.Equal(1, harness.RawWatcher.DisposeCalls);
        }

        [Fact]
        [Trait("TestId", "FWA-03")]
        public void OnChange_SingleFileModified_BatchContainsFile()
        {
            FileWatcherHarness harness = FileWatcherHarness.Create();
            List<FileChangeBatch> batches = new();

            using FileWatcher watcher = harness.CreateWatcher();
            watcher.OnChange += batches.Add;
            watcher.Start();
            harness.Emit("src/App.cs", FileChangeKind.Modified);

            Assert.Empty(batches);
            harness.FireDebounceTimer();

            FileChangeBatch batch = Assert.Single(batches);
            FileChange change = Assert.Single(batch.Changes);
            Assert.Equal(harness.GetPath("src/App.cs"), change.FilePath);
            Assert.Equal(FileChangeKind.Modified, change.Kind);
        }

        [Fact]
        [Trait("TestId", "FWA-04")]
        public void OnChange_MultipleFilesWithinDebounce_SingleBatch()
        {
            FileWatcherHarness harness = FileWatcherHarness.Create();
            List<FileChangeBatch> batches = new();

            using FileWatcher watcher = harness.CreateWatcher();
            watcher.OnChange += batches.Add;
            watcher.Start();
            harness.Emit("src/App.cs", FileChangeKind.Modified);
            harness.Emit("src/Counter.cs", FileChangeKind.Modified);
            harness.FireDebounceTimer();

            FileChangeBatch batch = Assert.Single(batches);
            Assert.Equal(2, batch.Changes.Count);
            Assert.Contains(batch.Changes, change => change.FilePath == harness.GetPath("src/App.cs"));
            Assert.Contains(batch.Changes, change => change.FilePath == harness.GetPath("src/Counter.cs"));
        }

        [Fact]
        [Trait("TestId", "FWA-05")]
        public void OnChange_FilesAcrossDebounceWindows_SeparateBatches()
        {
            FileWatcherHarness harness = FileWatcherHarness.Create();
            List<FileChangeBatch> batches = new();

            using FileWatcher watcher = harness.CreateWatcher();
            watcher.OnChange += batches.Add;
            watcher.Start();
            harness.Emit("src/App.cs", FileChangeKind.Modified);
            harness.FireDebounceTimer();
            harness.Emit("src/Counter.cs", FileChangeKind.Modified);
            harness.FireDebounceTimer();

            Assert.Equal(2, batches.Count);
            Assert.Equal(harness.GetPath("src/App.cs"), Assert.Single(batches[0].Changes).FilePath);
            Assert.Equal(harness.GetPath("src/Counter.cs"), Assert.Single(batches[1].Changes).FilePath);
        }

        [Fact]
        [Trait("TestId", "FWA-06")]
        public void OnChange_FileCreated_KindIsCreated()
        {
            FileChangeKind kind = EmitSingleKind(FileChangeKind.Created);

            Assert.Equal(FileChangeKind.Created, kind);
        }

        [Fact]
        [Trait("TestId", "FWA-07")]
        public void OnChange_FileDeleted_KindIsDeleted()
        {
            FileChangeKind kind = EmitSingleKind(FileChangeKind.Deleted);

            Assert.Equal(FileChangeKind.Deleted, kind);
        }

        [Fact]
        [Trait("TestId", "FWA-08")]
        public void OnChange_FileRenamed_KindIsRenamed()
        {
            FileChangeKind kind = EmitSingleKind(FileChangeKind.Renamed);

            Assert.Equal(FileChangeKind.Renamed, kind);
        }

        [Fact]
        [Trait("TestId", "FWA-09")]
        public void IncludePatterns_OnlyCsFiles_IgnoresTxt()
        {
            FileWatcherHarness harness = FileWatcherHarness.Create(
                includePatterns: ImmutableArray.Create("**/*.cs"));
            List<FileChangeBatch> batches = new();

            using FileWatcher watcher = harness.CreateWatcher();
            watcher.OnChange += batches.Add;
            watcher.Start();
            harness.Emit("src/App.cs", FileChangeKind.Modified);
            harness.Emit("src/readme.txt", FileChangeKind.Modified);
            harness.FireDebounceTimer();

            FileChangeBatch batch = Assert.Single(batches);
            Assert.Equal(harness.GetPath("src/App.cs"), Assert.Single(batch.Changes).FilePath);
        }

        [Fact]
        [Trait("TestId", "FWA-10")]
        public void ExcludePatterns_ObjDirectory_Ignored()
        {
            FileWatcherHarness harness = FileWatcherHarness.Create(
                includePatterns: ImmutableArray.Create("**/*.cs"),
                excludePatterns: ImmutableArray.Create("**/obj/**"));
            List<FileChangeBatch> batches = new();

            using FileWatcher watcher = harness.CreateWatcher();
            watcher.OnChange += batches.Add;
            watcher.Start();
            harness.Emit("src/App.cs", FileChangeKind.Modified);
            harness.Emit("obj/Generated.cs", FileChangeKind.Modified);
            harness.FireDebounceTimer();

            FileChangeBatch batch = Assert.Single(batches);
            Assert.Equal(harness.GetPath("src/App.cs"), Assert.Single(batch.Changes).FilePath);
        }

        [Fact]
        [Trait("TestId", "FWA-11")]
        public void UsePolling_True_DetectsChanges()
        {
            using TemporaryDirectory temporaryDirectory = new();
            ManualDevToolsClock clock = new()
            {
                UtcNow = DateTimeOffset.Parse(
                    "2026-05-06T00:00:00Z",
                    null,
                    System.Globalization.DateTimeStyles.AssumeUniversal),
            };
            FakeDevToolsTimerFactory timerFactory = new();
            FileWatcherOptions options = new(
                ImmutableArray.Create(temporaryDirectory.Path),
                DebounceMs: 25,
                UsePolling: true,
                PollIntervalMs: 10);
            List<FileChangeBatch> batches = new();

            using FileWatcher watcher = new(options, null, timerFactory, clock);
            watcher.OnChange += batches.Add;
            watcher.Start();
            string changedFile = temporaryDirectory.GetPath("src", "App.cs");
            _ = Directory.CreateDirectory(Path.GetDirectoryName(changedFile) ?? temporaryDirectory.Path);
            File.WriteAllText(changedFile, "public sealed class App { }");
            timerFactory.Timers[0].Fire();
            timerFactory.Timers[1].Fire();

            FileChangeBatch batch = Assert.Single(batches);
            FileChange change = Assert.Single(batch.Changes);
            Assert.Equal(Path.GetFullPath(changedFile), change.FilePath);
            Assert.Equal(FileChangeKind.Created, change.Kind);
        }

        [Fact]
        [Trait("TestId", "FWA-12")]
        public void DebounceMs_CustomValue_RespectedByTimer()
        {
            FileWatcherHarness harness = FileWatcherHarness.Create(debounceMs: 75);

            using FileWatcher watcher = harness.CreateWatcher();
            watcher.Start();
            harness.Emit("src/App.cs", FileChangeKind.Modified);

            FakeDevToolsTimer timer = Assert.Single(harness.TimerFactory.Timers);
            Assert.Equal(TimeSpan.FromMilliseconds(75), timer.DueTime);
        }

        [Fact]
        public void Stop_WithPendingDebounce_ClearsPendingChangesAndDisposesTimer()
        {
            FileWatcherHarness harness = FileWatcherHarness.Create(debounceMs: 500);
            List<FileChangeBatch> batches = new();

            using FileWatcher watcher = harness.CreateWatcher();
            watcher.OnChange += batches.Add;
            watcher.Start();
            harness.Emit("src/Pending.cs", FileChangeKind.Modified);
            FakeDevToolsTimer timer = Assert.Single(harness.TimerFactory.Timers);
            watcher.Stop();
            timer.Fire();
            harness.Emit("src/Late.cs", FileChangeKind.Modified);

            Assert.Empty(batches);
            Assert.True(timer.Disposed);
            Assert.Equal(FileWatcherStatus.Disposed, watcher.Status);
        }

        [Fact]
        public void Start_CalledTwice_DoesNotCreateDuplicateRawWatchers()
        {
            FileWatcherHarness harness = FileWatcherHarness.Create();

            using FileWatcher watcher = harness.CreateWatcher();
            watcher.Start();
            watcher.Start();

            Assert.Equal(1, harness.RawWatcherFactory.CreateCalls);
            Assert.Equal(1, harness.RawWatcher.StartCalls);
        }

        [Fact]
        public void OnChange_HandlerRemoved_DoesNotReceiveBatch()
        {
            FileWatcherHarness harness = FileWatcherHarness.Create();
            List<FileChangeBatch> batches = new();

            using FileWatcher watcher = harness.CreateWatcher();
            watcher.OnChange += batches.Add;
            watcher.OnChange -= batches.Add;
            watcher.Start();
            harness.Emit("src/App.cs", FileChangeKind.Modified);
            harness.FireDebounceTimer();

            Assert.Empty(batches);
        }

        [Fact]
        public void OnChange_DuplicateChangesToSameFile_DeduplicatesBatch()
        {
            FileWatcherHarness harness = FileWatcherHarness.Create();
            List<FileChangeBatch> batches = new();

            using FileWatcher watcher = harness.CreateWatcher();
            watcher.OnChange += batches.Add;
            watcher.Start();
            harness.Emit("src/Dedup.cs", FileChangeKind.Created);
            harness.Emit("src/Dedup.cs", FileChangeKind.Modified);
            harness.FireDebounceTimer();

            FileChange change = Assert.Single(Assert.Single(batches).Changes);
            Assert.Equal(harness.GetPath("src/Dedup.cs"), change.FilePath);
            Assert.Equal(FileChangeKind.Modified, change.Kind);
        }

        [Fact]
        public async Task FileSystemWatcher_PathContainingSpaces_EmitsBatch()
        {
            using TemporaryDirectory temporaryDirectory = new("qmlsharp fw spaced");
            string watchedDirectory = temporaryDirectory.GetPath("folder with spaces");
            _ = Directory.CreateDirectory(watchedDirectory);
            FileWatcherOptions options = new(
                ImmutableArray.Create(watchedDirectory),
                DebounceMs: 25,
                IncludePatterns: ImmutableArray.Create("**/*.cs"),
                ExcludePatterns: ImmutableArray<string>.Empty);
            TaskCompletionSource<FileChangeBatch> batchSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

            using FileWatcher watcher = new(options);
            watcher.OnChange += batch => batchSource.TrySetResult(batch);
            watcher.Start();
            string changedFile = Path.Join(watchedDirectory, "App With Spaces.cs");
            await File.WriteAllTextAsync(changedFile, "public sealed class AppWithSpaces { }");

            FileChangeBatch batch = await batchSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Contains(batch.Changes, change => change.FilePath == Path.GetFullPath(changedFile));
        }

        private static FileChangeKind EmitSingleKind(FileChangeKind kind)
        {
            FileWatcherHarness harness = FileWatcherHarness.Create();
            List<FileChangeBatch> batches = new();

            using FileWatcher watcher = harness.CreateWatcher();
            watcher.OnChange += batches.Add;
            watcher.Start();
            harness.Emit("src/App.cs", kind);
            harness.FireDebounceTimer();

            return Assert.Single(Assert.Single(batches).Changes).Kind;
        }

        private sealed class FileWatcherHarness
        {
            private readonly FileWatcherOptions options;
            private readonly ManualDevToolsClock clock;

            private FileWatcherHarness(
                FileWatcherOptions options,
                ManualFileSystemWatcherFactory rawWatcherFactory,
                FakeDevToolsTimerFactory timerFactory,
                ManualDevToolsClock clock)
            {
                this.options = options;
                RawWatcherFactory = rawWatcherFactory;
                TimerFactory = timerFactory;
                this.clock = clock;
            }

            public ManualFileSystemWatcherFactory RawWatcherFactory { get; }

            public ManualFileSystemWatcher RawWatcher => RawWatcherFactory.Watcher;

            public FakeDevToolsTimerFactory TimerFactory { get; }

            public static FileWatcherHarness Create(
                int debounceMs = 200,
                ImmutableArray<string> includePatterns = default,
                ImmutableArray<string> excludePatterns = default)
            {
                string root = Path.Combine(Path.GetTempPath(), "qmlsharp-fw-tests");
                FileWatcherOptions options = new(
                    ImmutableArray.Create(root),
                    debounceMs,
                    includePatterns.IsDefault ? null : includePatterns,
                    excludePatterns.IsDefault ? ImmutableArray<string>.Empty : excludePatterns);
                ManualDevToolsClock clock = new()
                {
                    UtcNow = DateTimeOffset.Parse(
                        "2026-05-06T00:00:00Z",
                        null,
                        System.Globalization.DateTimeStyles.AssumeUniversal),
                };
                return new FileWatcherHarness(
                    options,
                    new ManualFileSystemWatcherFactory(),
                    new FakeDevToolsTimerFactory(),
                    clock);
            }

            public FileWatcher CreateWatcher()
            {
                return new FileWatcher(options, RawWatcherFactory, TimerFactory, clock);
            }

            public string GetPath(string relativePath)
            {
                return Path.GetFullPath(Path.Join(options.WatchPaths[0], relativePath));
            }

            public void Emit(string relativePath, FileChangeKind kind)
            {
                RawWatcher.Emit(GetPath(relativePath), kind, clock.UtcNow);
            }

            public void FireDebounceTimer()
            {
                clock.UtcNow = clock.UtcNow.AddMilliseconds(options.DebounceMs);
                FakeDevToolsTimer timer = Assert.Single(TimerFactory.Timers);
                timer.Fire();
            }
        }

        private sealed class ManualFileSystemWatcherFactory : IFileSystemWatcherFactory
        {
            public ManualFileSystemWatcher Watcher { get; } = new();

            public int CreateCalls { get; private set; }

            public IFileSystemWatcher Create(FileWatcherOptions options)
            {
                ArgumentNullException.ThrowIfNull(options);

                CreateCalls++;
                return Watcher;
            }
        }

        private sealed class ManualFileSystemWatcher : IFileSystemWatcher
        {
            public event Action<FileChange> Changed = static _ => { };

            public bool Started { get; private set; }

            public int StartCalls { get; private set; }

            public int StopCalls { get; private set; }

            public int DisposeCalls { get; private set; }

            public void Start()
            {
                StartCalls++;
                Started = true;
            }

            public void Stop()
            {
                StopCalls++;
                Started = false;
            }

            public void Dispose()
            {
                DisposeCalls++;
                Started = false;
            }

            public void Emit(string filePath, FileChangeKind kind, DateTimeOffset timestamp)
            {
                if (Started)
                {
                    Changed(new FileChange(filePath, kind, timestamp));
                }
            }
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory(string prefix = "qmlsharp-fw")
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    prefix + "-" + Guid.NewGuid().ToString("N"));
                _ = Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public string GetPath(params string[] parts)
            {
                string[] allParts = new string[parts.Length + 1];
                allParts[0] = Path;
                Array.Copy(parts, 0, allParts, 1, parts.Length);
                return System.IO.Path.Combine(allParts);
            }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
        }
    }
}
