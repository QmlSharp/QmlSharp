namespace QmlSharp.DevTools.Tests.Benchmarks
{
    [Collection(PerformanceTestCollection.Name)]
    public sealed class InstanceMatchBenchmarks
    {
        [Fact]
        [Trait("Category", DevToolsTestCategories.Performance)]
        [Trait("TestId", "PBM-06")]
        public void InstanceMatching_50Instances_PBM_06_MeetsLatencyBudget()
        {
            InstanceMatcher matcher = new();
            ImmutableArray<InstanceSnapshot> oldSnapshots = CreateSnapshots(50);
            ImmutableArray<InstanceInfo> newInstances = CreateInstances(50);

            for (int index = 0; index < 100; index++)
            {
                MatchResult warmup = matcher.Match(oldSnapshots, newInstances);
                Assert.Equal(50, warmup.Matched.Count);
            }

            TimeSpan[] samples = new TimeSpan[10_000];
            for (int index = 0; index < samples.Length; index++)
            {
                samples[index] = BenchmarkAssert.Measure(() =>
                {
                    MatchResult result = matcher.Match(oldSnapshots, newInstances);
                    Assert.Equal(50, result.Matched.Count);
                });
            }

            BenchmarkAssert.Under(
                BenchmarkAssert.Percentile99(samples),
                TimeSpan.FromMilliseconds(1),
                "Instance matching for 50 instances P99");
        }

        private static ImmutableArray<InstanceSnapshot> CreateSnapshots(int count)
        {
            ImmutableArray<InstanceSnapshot>.Builder builder = ImmutableArray.CreateBuilder<InstanceSnapshot>(count);
            for (int index = 0; index < count; index++)
            {
                builder.Add(new InstanceSnapshot(
                    "old-" + index,
                    "CounterViewModel",
                    "CounterViewModel.schema",
                    "CounterView::__qmlsharp_vm" + index,
                    ImmutableDictionary<string, object?>.Empty,
                    DateTimeOffset.UnixEpoch,
                    DisposedAt: null));
            }

            return builder.ToImmutable();
        }

        private static ImmutableArray<InstanceInfo> CreateInstances(int count)
        {
            ImmutableArray<InstanceInfo>.Builder builder = ImmutableArray.CreateBuilder<InstanceInfo>(count);
            for (int index = 0; index < count; index++)
            {
                builder.Add(new InstanceInfo(
                    "new-" + index,
                    "CounterViewModel",
                    "CounterViewModel.schema",
                    "CounterView::__qmlsharp_vm" + index,
                    InstanceState.Active,
                    ImmutableDictionary<string, object?>.Empty,
                    QueuedCommandCount: 0,
                    CommandsDispatched: 0,
                    EffectsEmitted: 0,
                    DateTimeOffset.UnixEpoch,
                    DisposedAt: null));
            }

            return builder.ToImmutable();
        }
    }
}
