namespace QmlSharp.DevTools.Tests
{
    public sealed class HotReloadOrchestratorTests
    {
        [Fact]
        public async Task ReloadAsync_SuccessfulReload_AllFourStepsExecuted()
        {
            FakeNativeHost nativeHost = new()
            {
                Snapshots = ImmutableArray.Create(Snapshot("old-1", "CounterViewModel", "CounterView::__qmlsharp_vm0", ("count", 42))),
                Instances = ImmutableArray.Create(Info("new-1", "CounterViewModel", "CounterView::__qmlsharp_vm0")),
            };
            HotReloadOrchestrator orchestrator = CreateOrchestrator(nativeHost);

            HotReloadResult result = await orchestrator.ReloadAsync(CompilationResult());

            Assert.True(result.Success);
            Assert.Equal(1, result.InstancesMatched);
            Assert.Equal(0, result.InstancesOrphaned);
            Assert.Equal(0, result.InstancesNew);
            Assert.Equal("C:\\repo\\src\\CounterView.qml", nativeHost.ReloadedQmlPaths[0]);
            Assert.Equal("new-1", nativeHost.SyncedInstanceIds[0]);
            Assert.True(nativeHost.RestoreSnapshotsCalled);
            AssertSubsequence(nativeHost.CallOrder, "capture", "reload", "sync", "restore");
        }

        [Fact]
        public async Task ReloadAsync_SuccessfulReload_PhasesTimingPopulated()
        {
            ManualDevToolsClock clock = new();
            PerfProfiler profiler = new(clock);
            FakeNativeHost nativeHost = new()
            {
                Snapshots = ImmutableArray.Create(Snapshot("old-1", "CounterViewModel", "CounterView::__qmlsharp_vm0", ("count", 42))),
                Instances = ImmutableArray.Create(Info("new-1", "CounterViewModel", "CounterView::__qmlsharp_vm0")),
                BeforeCapture = () => clock.Advance(TimeSpan.FromMilliseconds(1)),
                BeforeReload = () => clock.Advance(TimeSpan.FromMilliseconds(2)),
                BeforeSync = () => clock.Advance(TimeSpan.FromMilliseconds(3)),
                BeforeRestore = () => clock.Advance(TimeSpan.FromMilliseconds(4)),
            };
            HotReloadOrchestrator orchestrator = CreateOrchestrator(nativeHost, clock, profiler);

            HotReloadResult result = await orchestrator.ReloadAsync(CompilationResult());

            Assert.True(result.Phases.CaptureTime > TimeSpan.Zero);
            Assert.True(result.Phases.NukeLoadTime > TimeSpan.Zero);
            Assert.True(result.Phases.HydrateTime > TimeSpan.Zero);
            Assert.True(result.Phases.RestoreTime > TimeSpan.Zero);
            Assert.True(result.TotalTime >= TimeSpan.FromMilliseconds(10));
            Assert.Contains(profiler.GetRecords(), record => record.Name == "hot_reload");
            Assert.Contains(profiler.GetRecords(), record => record.Name == "capture_snapshot");
            Assert.Contains(profiler.GetRecords(), record => record.Name == "nuke_load");
            Assert.Contains(profiler.GetRecords(), record => record.Name == "hydrate");
            Assert.Contains(profiler.GetRecords(), record => record.Name == "restore_snapshot");
        }

        [Fact]
        public async Task ReloadAsync_CaptureFailure_AbortsWithFailedStepCapture()
        {
            FakeNativeHost nativeHost = new()
            {
                CaptureException = new InvalidOperationException("capture failed"),
            };
            HotReloadOrchestrator orchestrator = CreateOrchestrator(nativeHost);

            HotReloadResult result = await orchestrator.ReloadAsync(CompilationResult());

            Assert.False(result.Success);
            Assert.Equal(HotReloadStep.Capture, result.FailedStep);
            Assert.Contains("capture failed", result.ErrorMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("reload", nativeHost.CallOrder);
            Assert.DoesNotContain("sync", nativeHost.CallOrder);
            Assert.DoesNotContain("restore", nativeHost.CallOrder);
        }

        [Fact]
        public async Task ReloadAsync_NukeLoadFailure_AbortsWithFailedStepNukeLoad()
        {
            FakeNativeHost nativeHost = new()
            {
                Snapshots = ImmutableArray.Create(Snapshot("old-1", "CounterViewModel", "CounterView::__qmlsharp_vm0", ("count", 42))),
                ReloadException = new InvalidOperationException("qml load failed"),
            };
            HotReloadOrchestrator orchestrator = CreateOrchestrator(nativeHost);

            HotReloadResult result = await orchestrator.ReloadAsync(CompilationResult());

            Assert.False(result.Success);
            Assert.Equal(HotReloadStep.NukeLoad, result.FailedStep);
            Assert.Equal(1, result.InstancesOrphaned);
            Assert.Contains("qml load failed", result.ErrorMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("sync", nativeHost.CallOrder);
            Assert.DoesNotContain("restore", nativeHost.CallOrder);
        }

        [Fact]
        public async Task ReloadAsync_HydratePartialMatch_ReportsOrphanedAndNew()
        {
            FakeNativeHost nativeHost = new()
            {
                Snapshots = ImmutableArray.Create(
                    Snapshot("old-1", "CounterViewModel", "CounterView::__qmlsharp_vm0", ("count", 42)),
                    Snapshot("old-2", "TodoViewModel", "TodoView::__qmlsharp_vm0", ("title", "old"))),
                Instances = ImmutableArray.Create(
                    Info("new-1", "CounterViewModel", "CounterView::__qmlsharp_vm0"),
                    Info("new-2", "FreshViewModel", "FreshView::__qmlsharp_vm0")),
            };
            HotReloadOrchestrator orchestrator = CreateOrchestrator(nativeHost);

            HotReloadResult result = await orchestrator.ReloadAsync(CompilationResult());

            Assert.True(result.Success);
            Assert.Equal(1, result.InstancesMatched);
            Assert.Equal(1, result.InstancesOrphaned);
            Assert.Equal(1, result.InstancesNew);
            Assert.Equal("new-1", nativeHost.SyncedInstanceIds[0]);
        }

        [Fact]
        public async Task ReloadAsync_RestoreFailure_SuccessTrue()
        {
            FakeNativeHost nativeHost = new()
            {
                Snapshots = ImmutableArray.Create(Snapshot("old-1", "CounterViewModel", "CounterView::__qmlsharp_vm0", ("count", 42))),
                Instances = ImmutableArray.Create(Info("new-1", "CounterViewModel", "CounterView::__qmlsharp_vm0")),
                RestoreException = new InvalidOperationException("restore failed"),
            };
            HotReloadOrchestrator orchestrator = CreateOrchestrator(nativeHost);

            HotReloadResult result = await orchestrator.ReloadAsync(CompilationResult());

            Assert.True(result.Success);
            Assert.Equal(HotReloadStep.Restore, result.FailedStep);
            Assert.Contains("restore failed", result.ErrorMessage, StringComparison.Ordinal);
            Assert.True(nativeHost.RestoreSnapshotsCalled is false);
        }

        [Fact]
        public async Task OnBefore_FiresBeforeStep1()
        {
            List<string> order = new();
            FakeNativeHost nativeHost = new()
            {
                Snapshots = ImmutableArray.Create(Snapshot("old-1", "CounterViewModel", "CounterView::__qmlsharp_vm0", ("count", 42))),
                Instances = ImmutableArray.Create(
                    Info("old-1", "CounterViewModel", "CounterView::__qmlsharp_vm0"),
                    Info("old-2", "TodoViewModel", "TodoView::__qmlsharp_vm0")),
                BeforeCapture = () => order.Add("capture"),
            };
            HotReloadOrchestrator orchestrator = CreateOrchestrator(nativeHost);
            HotReloadStartingEvent? startingEvent = null;
            orchestrator.OnBefore += starting =>
            {
                order.Add("before");
                startingEvent = starting;
            };

            _ = await orchestrator.ReloadAsync(CompilationResult());

            Assert.Equal(2, startingEvent?.OldInstanceCount);
            Assert.Equal("before", order[0]);
            Assert.Equal("capture", order[1]);
        }

        [Fact]
        public async Task OnAfter_FiresAfterStep4()
        {
            List<string> order = new();
            FakeNativeHost nativeHost = new()
            {
                Snapshots = ImmutableArray.Create(Snapshot("old-1", "CounterViewModel", "CounterView::__qmlsharp_vm0", ("count", 42))),
                Instances = ImmutableArray.Create(Info("new-1", "CounterViewModel", "CounterView::__qmlsharp_vm0")),
                BeforeRestore = () => order.Add("restore"),
            };
            HotReloadOrchestrator orchestrator = CreateOrchestrator(nativeHost);
            HotReloadCompletedEvent? completedEvent = null;
            orchestrator.OnAfter += completed =>
            {
                order.Add("after");
                completedEvent = completed;
            };

            HotReloadResult result = await orchestrator.ReloadAsync(CompilationResult());

            Assert.Same(result, completedEvent?.Result);
            Assert.Equal("restore", order[0]);
            Assert.Equal("after", order[1]);
        }

        [Fact]
        public async Task ReloadAsync_Cancellation_AbortsCleanly()
        {
            FakeNativeHost nativeHost = new()
            {
                Snapshots = ImmutableArray.Create(Snapshot("old-1", "CounterViewModel", "CounterView::__qmlsharp_vm0", ("count", 42))),
                ReloadException = new OperationCanceledException("reload canceled"),
            };
            HotReloadOrchestrator orchestrator = CreateOrchestrator(nativeHost);

            HotReloadResult result = await orchestrator.ReloadAsync(CompilationResult());

            Assert.False(result.Success);
            Assert.Equal(HotReloadStep.NukeLoad, result.FailedStep);
            Assert.Contains("reload canceled", result.ErrorMessage, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ReloadAsync_StateHydration_OldStatePushedToNewInstance()
        {
            FakeNativeHost nativeHost = new()
            {
                Snapshots = ImmutableArray.Create(Snapshot("old-1", "CounterViewModel", "CounterView::__qmlsharp_vm0", ("count", 42))),
                Instances = ImmutableArray.Create(Info("new-1", "CounterViewModel", "CounterView::__qmlsharp_vm0")),
            };
            HotReloadOrchestrator orchestrator = CreateOrchestrator(nativeHost);

            HotReloadResult result = await orchestrator.ReloadAsync(CompilationResult());

            Assert.True(result.Success);
            _ = Assert.Single(nativeHost.SyncedStates);
            Assert.Equal(42, nativeHost.SyncedStates[0]["count"]);
            Assert.Equal("new-1", nativeHost.SyncedInstanceIds[0]);
        }

        private static HotReloadOrchestrator CreateOrchestrator(FakeNativeHost nativeHost)
        {
            return CreateOrchestrator(nativeHost, new ManualDevToolsClock(), new PerfProfiler());
        }

        private static HotReloadOrchestrator CreateOrchestrator(
            FakeNativeHost nativeHost,
            ManualDevToolsClock clock,
            PerfProfiler profiler)
        {
            return new HotReloadOrchestrator(nativeHost, profiler, new InstanceMatcher(), clock);
        }

        private static CompilationResult CompilationResult()
        {
            CompilationUnit unit = new()
            {
                SourceFilePath = "C:\\repo\\src\\CounterView.cs",
                ViewClassName = "CounterView",
                ViewModelClassName = "CounterViewModel",
                QmlText = "import QtQuick\nItem {}",
            };

            return QmlSharp.Compiler.CompilationResult.FromUnits(ImmutableArray.Create(unit));
        }

        private static InstanceSnapshot Snapshot(
            string instanceId,
            string className,
            string compilerSlotKey,
            params (string Key, object? Value)[] state)
        {
            ImmutableDictionary<string, object?> properties = state
                .ToImmutableDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.Ordinal);

            return new InstanceSnapshot(
                instanceId,
                className,
                SchemaId: className + ".schema",
                compilerSlotKey,
                properties,
                DateTimeOffset.UnixEpoch,
                DisposedAt: null);
        }

        private static InstanceInfo Info(
            string instanceId,
            string className,
            string compilerSlotKey)
        {
            return new InstanceInfo(
                instanceId,
                className,
                SchemaId: className + ".schema",
                compilerSlotKey,
                InstanceState.Active,
                ImmutableDictionary<string, object?>.Empty,
                QueuedCommandCount: 0,
                CommandsDispatched: 0,
                EffectsEmitted: 0,
                DateTimeOffset.UnixEpoch,
                DisposedAt: null);
        }

        private static void AssertSubsequence(
            IReadOnlyList<string> actual,
            params string[] expected)
        {
            int searchStart = 0;
            foreach (string expectedItem in expected)
            {
                int foundIndex = -1;
                for (int index = searchStart; index < actual.Count; index++)
                {
                    if (actual[index] == expectedItem)
                    {
                        foundIndex = index;
                        break;
                    }
                }

                Assert.True(foundIndex >= 0, "Expected call order item was not found: " + expectedItem);
                searchStart = foundIndex + 1;
            }
        }
    }
}
