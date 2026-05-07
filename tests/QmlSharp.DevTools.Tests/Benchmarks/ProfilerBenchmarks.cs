namespace QmlSharp.DevTools.Tests.Benchmarks
{
    [Collection(PerformanceTestCollection.Name)]
    public sealed class ProfilerBenchmarks
    {
        [Fact]
        [Trait("Category", DevToolsTestCategories.Performance)]
        [Trait("TestId", "PBM-09")]
        public void PerfProfilerSpanRecord_PBM_09_MeetsLatencyBudget()
        {
            PerfProfiler profiler = new();
            for (int index = 0; index < 1_000; index++)
            {
                using IPerfSpan span = profiler.StartSpan("warmup", PerfCategory.HotReload);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            bool noGc = TryStartNoGcRegion();

            TimeSpan[] samples = new TimeSpan[100_000];
            try
            {
                for (int index = 0; index < samples.Length; index++)
                {
                    samples[index] = BenchmarkAssert.Measure(() =>
                    {
                        using IPerfSpan span = profiler.StartSpan("span", PerfCategory.HotReload);
                    });
                }
            }
            finally
            {
                if (noGc)
                {
                    try
                    {
                        GC.EndNoGCRegion();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }

            BenchmarkAssert.Under(
                BenchmarkAssert.Percentile99(samples),
                TimeSpan.FromMilliseconds(0.01),
                "PerfProfiler span record P99");
        }

        [Fact]
        [Trait("Category", DevToolsTestCategories.Performance)]
        [Trait("TestId", "PBM-10")]
        public void ChromeTraceExport_OneThousandSpans_PBM_10_MeetsLatencyBudgetAndClosesFile()
        {
            PerfProfiler profiler = new();
            for (int index = 0; index < 1_000; index++)
            {
                using IPerfSpan span = profiler.StartSpan("span-" + index, PerfCategory.HotReload);
                span.AddMetadata("index", index);
            }

            string outputPath = Path.Join(
                Path.GetTempPath(),
                "qmlsharp-trace-" + Path.GetRandomFileName() + ".json");
            try
            {
                TimeSpan elapsed = BenchmarkAssert.Measure(() => profiler.ExportChromeTrace(outputPath));

                BenchmarkAssert.Under(
                    elapsed,
                    TimeSpan.FromMilliseconds(100),
                    "Chrome Trace export for 1,000 spans");
                Assert.True(File.Exists(outputPath));

                File.Delete(outputPath);
                Assert.False(File.Exists(outputPath));
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        private static bool TryStartNoGcRegion()
        {
            try
            {
                return GC.TryStartNoGCRegion(128 * 1024 * 1024);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
