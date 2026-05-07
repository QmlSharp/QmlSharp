using QmlSharp.DevTools;
using QmlSharp.Integration.Tests.Fixtures;

namespace QmlSharp.Integration.Tests.DevTools
{
    public sealed class ReplIntegrationTests
    {
        [Fact]
        [Trait("TestId", "INT-10")]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.QmlRepl)]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task ReplCSharpEvaluationWorksWhileServerIsRunning_INT_10()
        {
            await using DevFlowHarness harness = DevFlowHarness.Create();
            await harness.StartAsync();
            await using Repl repl = new(
                new ReplOptions(
                    ReplMode.CSharp,
                    MaxHistory: 20,
                    HistoryFilePath: harness.Fixture.ReplHistoryPath,
                    EvaluationTimeout: TimeSpan.FromSeconds(10)),
                harness.NativeHost,
                harness.Server,
                harness.Profiler);

            await repl.StartAsync();
            ReplResult result = await repl.EvalAsync("int count = 40 + 2; count");

            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal("42", result.Output);
            Assert.Equal("Int32", result.ReturnType);
            Assert.Contains("int count = 40 + 2; count", repl.History);
        }
    }
}
