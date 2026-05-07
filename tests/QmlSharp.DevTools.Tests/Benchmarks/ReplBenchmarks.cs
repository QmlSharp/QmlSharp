namespace QmlSharp.DevTools.Tests.Benchmarks
{
    [Collection(PerformanceTestCollection.Name)]
    public sealed class ReplBenchmarks
    {
        [Fact]
        [Trait("Category", DevToolsTestCategories.Performance)]
        [Trait("TestId", "PBM-08")]
        public async Task ReplCSharpEval_PBM_08_MeetsLatencyBudget()
        {
            await using Repl repl = new(new ReplOptions(
                DefaultMode: ReplMode.CSharp,
                MaxHistory: 256,
                HistoryFilePath: null,
                EvaluationTimeout: TimeSpan.FromSeconds(5)));
            await repl.StartAsync();

            for (int index = 0; index < 5; index++)
            {
                ReplResult warmup = await repl.EvalAsync("1 + 1");
                Assert.True(warmup.Success, warmup.Error?.Message);
            }

            TimeSpan[] samples = new TimeSpan[100];
            for (int index = 0; index < samples.Length; index++)
            {
                string expression = "1 + " + (index % 10);
                samples[index] = await BenchmarkAssert.MeasureAsync(async () =>
                {
                    ReplResult result = await repl.EvalAsync(expression);
                    Assert.True(result.Success, result.Error?.Message);
                });
            }

            BenchmarkAssert.Under(
                BenchmarkAssert.Percentile99(samples),
                TimeSpan.FromMilliseconds(100),
                "C# REPL eval P99");
        }
    }
}
