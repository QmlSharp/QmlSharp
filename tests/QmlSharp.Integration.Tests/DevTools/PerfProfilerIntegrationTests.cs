using QmlSharp.DevTools;
using QmlSharp.Integration.Tests.Fixtures;

namespace QmlSharp.Integration.Tests.DevTools
{
    public sealed class PerfProfilerIntegrationTests
    {
        [Fact]
        [Trait("TestId", "INT-08")]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.Performance)]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task ProfilerRecordsAllPhases_INT_08()
        {
            await using DevFlowHarness harness = DevFlowHarness.Create();
            await harness.StartAsync();

            harness.Fixture.ApplyChangedView();
            harness.ControlledWatcher.Emit(harness.Fixture.CreateBatch(harness.Fixture.CounterViewPath));

            await DevFlowAssertions.WaitUntilAsync(
                () => harness.Server.Stats.HotReloadCount == 1 && harness.Server.Status == DevServerStatus.Running,
                "Profiler integration test did not complete the reload.");

            IReadOnlyList<PerfRecord> records = harness.Profiler.GetRecords();
            Assert.Contains(records, static record => record.Name == "devserver_initial_build" && record.Category == PerfCategory.Build);
            Assert.Contains(records, static record => record.Name == "devserver_rebuild_compile" && record.Category == PerfCategory.Build);
            Assert.Contains(records, static record => record.Name == "hot_reload" && record.Category == PerfCategory.HotReload);
            Assert.Contains(records, static record => record.Name == "capture_snapshot" && record.Category == PerfCategory.Capture);
            Assert.Contains(records, static record => record.Name == "nuke_load" && record.Category == PerfCategory.HotReload);
            Assert.Contains(records, static record => record.Name == "hydrate" && record.Category == PerfCategory.HotReload);
            Assert.Contains(records, static record => record.Name == "restore_snapshot" && record.Category == PerfCategory.Restore);

            PerfSummary summary = harness.Profiler.GetSummary();
            Assert.True(summary.TotalSpans >= 7);
            Assert.Contains(PerfCategory.Build, summary.Categories.Keys);
            Assert.Contains(PerfCategory.HotReload, summary.Categories.Keys);
            Assert.Contains(PerfCategory.Capture, summary.Categories.Keys);
            Assert.Contains(PerfCategory.Restore, summary.Categories.Keys);
        }
    }
}
