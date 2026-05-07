namespace QmlSharp.DevTools.Tests.Benchmarks
{
    [Collection(PerformanceTestCollection.Name)]
    public sealed class OverlayBenchmarks
    {
        [Fact]
        [Trait("Category", DevToolsTestCategories.Performance)]
        [Trait("TestId", "PBM-07")]
        public void ErrorOverlayShow_PBM_07_MeetsLatencyBudget()
        {
            ErrorOverlay overlay = new(new BenchmarkOverlayNativeHost());
            OverlayError error = new(
                "Compilation Error",
                "QMLSHARP-C001: test error",
                "C:/repo/src/CounterView.cs",
                Line: 12,
                Column: 8);

            for (int index = 0; index < 10; index++)
            {
                overlay.Show(error);
                overlay.Hide();
            }

            TimeSpan[] samples = new TimeSpan[1_000];
            for (int index = 0; index < samples.Length; index++)
            {
                samples[index] = BenchmarkAssert.Measure(() => overlay.Show(error));
                overlay.Hide();
            }

            BenchmarkAssert.Under(
                BenchmarkAssert.Percentile99(samples),
                TimeSpan.FromMilliseconds(10),
                "Error overlay show P99");
        }

        private sealed class BenchmarkOverlayNativeHost : IErrorOverlayNativeHost
        {
            public void ShowError(string title, string message, string? filePath, int line, int column)
            {
            }

            public void HideError()
            {
            }
        }
    }
}
