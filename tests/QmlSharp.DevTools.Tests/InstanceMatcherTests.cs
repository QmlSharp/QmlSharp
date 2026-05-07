namespace QmlSharp.DevTools.Tests
{
    public sealed class InstanceMatcherTests
    {
        [Fact]
        [Trait("TestId", "HRO-07")]
        public void InstanceMatch_SameClassAndSlotKey_Matched()
        {
            InstanceMatcher matcher = new();
            InstanceSnapshot oldSnapshot = Snapshot("old-1", "CounterViewModel", "CounterView::__qmlsharp_vm0");
            InstanceInfo newInstance = Info("new-1", "CounterViewModel", "CounterView::__qmlsharp_vm0");

            MatchResult result = matcher.Match(
                ImmutableArray.Create(oldSnapshot),
                ImmutableArray.Create(newInstance));

            _ = Assert.Single(result.Matched);
            Assert.Same(oldSnapshot, result.Matched[0].Old);
            Assert.Same(newInstance, result.Matched[0].New);
            Assert.Empty(result.Orphaned);
            Assert.Empty(result.Unmatched);
        }

        [Fact]
        [Trait("TestId", "HRO-08")]
        public void InstanceMatch_DifferentSlotKey_NotMatched()
        {
            InstanceMatcher matcher = new();
            InstanceSnapshot oldSnapshot = Snapshot("old-1", "CounterViewModel", "CounterView::__qmlsharp_vm0");
            InstanceInfo newInstance = Info("new-1", "CounterViewModel", "CounterView::__qmlsharp_vm1");

            MatchResult result = matcher.Match(
                ImmutableArray.Create(oldSnapshot),
                ImmutableArray.Create(newInstance));

            Assert.Empty(result.Matched);
            _ = Assert.Single(result.Orphaned);
            _ = Assert.Single(result.Unmatched);
            Assert.Same(oldSnapshot, result.Orphaned[0]);
            Assert.Same(newInstance, result.Unmatched[0]);
        }

        [Fact]
        [Trait("TestId", "HRO-09")]
        public void InstanceMatch_MultipleInstancesSameClass_MatchedBySlotKey()
        {
            InstanceMatcher matcher = new();
            ImmutableArray<InstanceSnapshot> oldSnapshots = ImmutableArray.Create(
                Snapshot("old-1", "CounterViewModel", "CounterView::__qmlsharp_vm0"),
                Snapshot("old-2", "CounterViewModel", "CounterView::__qmlsharp_vm1"),
                Snapshot("old-3", "CounterViewModel", "CounterView::__qmlsharp_vm2"));
            ImmutableArray<InstanceInfo> newInstances = ImmutableArray.Create(
                Info("new-2", "CounterViewModel", "CounterView::__qmlsharp_vm1"),
                Info("new-3", "CounterViewModel", "CounterView::__qmlsharp_vm2"),
                Info("new-1", "CounterViewModel", "CounterView::__qmlsharp_vm0"));

            MatchResult result = matcher.Match(oldSnapshots, newInstances);

            Assert.Equal(3, result.Matched.Count);
            Assert.Empty(result.Orphaned);
            Assert.Empty(result.Unmatched);
            Assert.Equal("new-1", result.Matched[0].New.InstanceId);
            Assert.Equal("new-2", result.Matched[1].New.InstanceId);
            Assert.Equal("new-3", result.Matched[2].New.InstanceId);
        }

        [Fact]
        [Trait("TestId", "HRO-10")]
        public void InstanceMatch_NoOldInstances_AllNew()
        {
            InstanceMatcher matcher = new();
            ImmutableArray<InstanceInfo> newInstances = ImmutableArray.Create(
                Info("new-1", "CounterViewModel", "CounterView::__qmlsharp_vm0"),
                Info("new-2", "TodoViewModel", "TodoView::__qmlsharp_vm0"));

            MatchResult result = matcher.Match(
                ImmutableArray<InstanceSnapshot>.Empty,
                newInstances);

            Assert.Empty(result.Matched);
            Assert.Empty(result.Orphaned);
            Assert.Equal(2, result.Unmatched.Count);
        }

        private static InstanceSnapshot Snapshot(
            string instanceId,
            string className,
            string compilerSlotKey)
        {
            return new InstanceSnapshot(
                instanceId,
                className,
                SchemaId: className + ".schema",
                compilerSlotKey,
                ImmutableDictionary<string, object?>.Empty,
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
    }
}
