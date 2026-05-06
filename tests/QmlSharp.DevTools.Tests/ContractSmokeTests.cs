namespace QmlSharp.DevTools.Tests
{
    public sealed class ContractSmokeTests
    {
        [Fact]
        public void FileWatcherContracts_DefaultOptionsAndEnums_AreConstructible()
        {
            FileWatcherOptions options = new(ImmutableArray.Create("src"));
            FileChange change = new("C:/repo/src/App.cs", FileChangeKind.Renamed, DateTimeOffset.UnixEpoch);
            FileChangeBatch batch = new(ImmutableArray.Create(change), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

            Assert.Equal(200, options.DebounceMs);
            Assert.Equal(500, options.PollIntervalMs);
            Assert.False(options.UsePolling);
            Assert.Equal(FileChangeKind.Renamed, batch.Changes[0].Kind);
            Assert.True(Enum.IsDefined(FileWatcherStatus.Idle));
        }

        [Fact]
        public void HotReloadContracts_DefaultResultShape_AreConstructible()
        {
            HotReloadPhases phases = new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
            HotReloadResult result = new(
                Success: true,
                InstancesMatched: 1,
                InstancesOrphaned: 0,
                InstancesNew: 0,
                phases,
                TotalTime: TimeSpan.Zero,
                ErrorMessage: null,
                FailedStep: null);
            HotReloadCompletedEvent completed = new(result, DateTimeOffset.UnixEpoch);

            Assert.True(result.Success);
            Assert.Null(result.FailedStep);
            Assert.Same(result, completed.Result);
            Assert.Equal(HotReloadStep.Hydrate, HotReloadStep.Hydrate);
        }

        [Fact]
        public void OverlayConsoleAndServerOptions_Defaults_AreConstructible()
        {
            FileWatcherOptions watcherOptions = new(ImmutableArray.Create("src"));
            DevConsoleOptions consoleOptions = new();
            DevServerOptions serverOptions = new("C:/repo", watcherOptions, consoleOptions);
            OverlayError error = new("Compilation Error", "broken", "C:/repo/App.cs", 1, 2);
            ServerStats stats = new(1, 2, 3, 4, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
            DevServerStatusChangedEvent statusChanged = new(
                DevServerStatus.Idle,
                DevServerStatus.Starting,
                DateTimeOffset.UnixEpoch,
                Reason: null);

            Assert.True(consoleOptions.Color);
            Assert.True(consoleOptions.ShowTimestamps);
            Assert.Equal(LogLevel.Info, consoleOptions.Level);
            Assert.True(serverOptions.EnableRepl);
            Assert.True(serverOptions.EnableProfiling);
            Assert.Equal(OverlaySeverity.Error, error.Severity);
            Assert.Equal(2, stats.RebuildCount);
            Assert.Equal(DevServerStatus.Starting, statusChanged.Current);
        }

        [Fact]
        public void ReplProfilerAndSchemaContracts_AreConstructible()
        {
            ReplError replError = new("bad input", ReplErrorKind.CompilationError, Line: 1, Column: 1);
            ReplResult replResult = new(false, "bad input", ReturnType: null, TimeSpan.Zero, replError);
            CategoryStats categoryStats = new(1, TimeSpan.FromMilliseconds(1), TimeSpan.Zero, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1));
            PerfSummary summary = new(
                ImmutableDictionary<PerfCategory, CategoryStats>.Empty.Add(PerfCategory.Compile, categoryStats),
                TotalSpans: 1,
                TotalTime: TimeSpan.FromMilliseconds(1));
            SchemaDiffResult schemaDiff = new(
                HasStructuralChanges: true,
                ImmutableArray.Create("CounterViewModel"),
                ImmutableArray.Create("state member added"));

            Assert.False(replResult.Success);
            Assert.Equal(ReplMode.CSharp, ReplMode.CSharp);
            Assert.Equal(1, summary.TotalSpans);
            Assert.True(schemaDiff.HasStructuralChanges);
        }
    }
}
