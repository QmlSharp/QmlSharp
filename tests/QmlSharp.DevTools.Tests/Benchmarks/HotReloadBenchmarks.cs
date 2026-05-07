namespace QmlSharp.DevTools.Tests.Benchmarks
{
    [Collection(PerformanceTestCollection.Name)]
    public sealed class HotReloadBenchmarks
    {
        [Fact]
        [Trait("Category", DevToolsTestCategories.Performance)]
        [Trait("TestId", "PBM-01")]
        public async Task HotReload_Total_PBM_01_MeetsLatencyBudget()
        {
            IReadOnlyList<HotReloadResult> results = await MeasureHotReloadAsync(
                iterations: 100,
                instanceCount: 1,
                propertiesPerInstance: 1);

            BenchmarkAssert.Under(
                BenchmarkAssert.Percentile99(results.Select(static result => result.TotalTime).ToArray()),
                TimeSpan.FromMilliseconds(70),
                "Hot reload total P99");
        }

        [Fact]
        [Trait("Category", DevToolsTestCategories.Performance)]
        [Trait("TestId", "PBM-02")]
        public async Task SnapshotCapture_PBM_02_MeetsLatencyBudget()
        {
            IReadOnlyList<HotReloadResult> results = await MeasureHotReloadAsync(
                iterations: 1_000,
                instanceCount: 1,
                propertiesPerInstance: 1);

            BenchmarkAssert.Under(
                BenchmarkAssert.Percentile99(results.Select(static result => result.Phases.CaptureTime).ToArray()),
                TimeSpan.FromMilliseconds(5),
                "Snapshot capture P99");
        }

        [Fact]
        [Trait("Category", DevToolsTestCategories.Performance)]
        [Trait("TestId", "PBM-03")]
        public async Task NukeAndLoad_PBM_03_MeetsLatencyBudget()
        {
            IReadOnlyList<HotReloadResult> results = await MeasureHotReloadAsync(
                iterations: 100,
                instanceCount: 1,
                propertiesPerInstance: 1);

            BenchmarkAssert.Under(
                BenchmarkAssert.Percentile99(results.Select(static result => result.Phases.NukeLoadTime).ToArray()),
                TimeSpan.FromMilliseconds(50),
                "Nuke and load P99");
        }

        [Fact]
        [Trait("Category", DevToolsTestCategories.Performance)]
        [Trait("TestId", "PBM-04")]
        public async Task Hydration_PBM_04_MeetsLatencyBudget()
        {
            IReadOnlyList<HotReloadResult> results = await MeasureHotReloadAsync(
                iterations: 1_000,
                instanceCount: 10,
                propertiesPerInstance: 5);

            BenchmarkAssert.Under(
                BenchmarkAssert.Percentile99(results.Select(static result => result.Phases.HydrateTime).ToArray()),
                TimeSpan.FromMilliseconds(10),
                "Hydration P99");
        }

        [Fact]
        [Trait("Category", DevToolsTestCategories.Performance)]
        [Trait("TestId", "PBM-05")]
        public async Task Restore_PBM_05_MeetsLatencyBudget()
        {
            IReadOnlyList<HotReloadResult> results = await MeasureHotReloadAsync(
                iterations: 1_000,
                instanceCount: 1,
                propertiesPerInstance: 1);

            BenchmarkAssert.Under(
                BenchmarkAssert.Percentile99(results.Select(static result => result.Phases.RestoreTime).ToArray()),
                TimeSpan.FromMilliseconds(5),
                "Restore P99");
        }

        private static async Task<IReadOnlyList<HotReloadResult>> MeasureHotReloadAsync(
            int iterations,
            int instanceCount,
            int propertiesPerInstance)
        {
            BenchmarkNativeHost nativeHost = new(instanceCount, propertiesPerInstance);
            HotReloadOrchestrator orchestrator = new(
                nativeHost,
                new PerfProfiler(),
                new InstanceMatcher(),
                SystemDevToolsClock.Instance);
            CompilationResult result = DevToolsTestFixtures.CompilationResultWithSchema();

            for (int index = 0; index < 10; index++)
            {
                _ = await orchestrator.ReloadAsync(result);
            }

            HotReloadResult[] results = new HotReloadResult[iterations];
            for (int index = 0; index < iterations; index++)
            {
                results[index] = await orchestrator.ReloadAsync(result);
                Assert.True(results[index].Success, results[index].ErrorMessage);
            }

            return results;
        }

        private sealed class BenchmarkNativeHost : IDevToolsNativeHost
        {
            private readonly ImmutableArray<InstanceSnapshot> snapshots;
            private readonly ImmutableArray<InstanceInfo> instances;

            public BenchmarkNativeHost(int instanceCount, int propertiesPerInstance)
            {
                ImmutableArray<InstanceSnapshot>.Builder snapshotBuilder =
                    ImmutableArray.CreateBuilder<InstanceSnapshot>(instanceCount);
                ImmutableArray<InstanceInfo>.Builder instanceBuilder =
                    ImmutableArray.CreateBuilder<InstanceInfo>(instanceCount);
                for (int index = 0; index < instanceCount; index++)
                {
                    string className = "CounterViewModel";
                    string slotKey = "CounterView::__qmlsharp_vm" + index;
                    ImmutableDictionary<string, object?> state = Enumerable
                        .Range(0, propertiesPerInstance)
                        .ToImmutableDictionary(
                            propertyIndex => "property" + propertyIndex,
                            propertyIndex => (object?)propertyIndex,
                            StringComparer.Ordinal);
                    snapshotBuilder.Add(new InstanceSnapshot(
                        "old-" + index,
                        className,
                        className + ".schema",
                        slotKey,
                        state,
                        DateTimeOffset.UnixEpoch,
                        DisposedAt: null));
                    instanceBuilder.Add(new InstanceInfo(
                        "new-" + index,
                        className,
                        className + ".schema",
                        slotKey,
                        InstanceState.Active,
                        ImmutableDictionary<string, object?>.Empty,
                        QueuedCommandCount: 0,
                        CommandsDispatched: 0,
                        EffectsEmitted: 0,
                        DateTimeOffset.UnixEpoch,
                        DisposedAt: null));
                }

                snapshots = snapshotBuilder.ToImmutable();
                instances = instanceBuilder.ToImmutable();
            }

            public Task<IReadOnlyList<InstanceSnapshot>> CaptureSnapshotsAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult((IReadOnlyList<InstanceSnapshot>)snapshots);
            }

            public Task ReloadQmlAsync(string qmlSourcePath, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task SyncStateBatchAsync(
                string instanceId,
                IReadOnlyDictionary<string, object?> state,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task RestoreSnapshotsAsync(
                IReadOnlyList<InstanceSnapshot> snapshots,
                CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<InstanceInfo>> GetInstancesAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult((IReadOnlyList<InstanceInfo>)instances);
            }

            public Task ShowOverlayAsync(OverlayError error, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task HideOverlayAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<string> EvaluateQmlAsync(string input, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(string.Empty);
            }

            public Task<RuntimeMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(DevToolsTestFixtures.RuntimeMetrics());
            }
        }
    }
}
