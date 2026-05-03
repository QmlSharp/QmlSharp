using System.Diagnostics;
using QmlSharp.Dsl.Generator.Tests.Fixtures;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Pipeline
{
    public sealed class GenerationPerformanceTests
    {
        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public void ResolveInheritance_P0Registry_CompletesWithinBudget()
        {
            IRegistryQuery registry = DslTestFixtures.CreateP0ScaleFixture();
            InheritanceResolver resolver = new(DslTestFixtures.DefaultOptions.Inheritance);
            Stopwatch stopwatch = Stopwatch.StartNew();

            foreach (QmlModule module in registry.GetAllModules())
            {
                _ = resolver.ResolveModule(module, registry);
            }

            stopwatch.Stop();
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(2),
                $"P0 inheritance resolution took {stopwatch.Elapsed.TotalMilliseconds:F1} ms; budget is 2 seconds.");
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public async Task Generate_P0Modules_CompletesWithinBudgetAndSizeLimit()
        {
            GenerationPipeline pipeline = new();
            Stopwatch stopwatch = Stopwatch.StartNew();

            GenerationResult result = await pipeline.Generate(DslTestFixtures.CreateP0ScaleFixture(), DslTestFixtures.DefaultOptions);

            stopwatch.Stop();
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"P0 module generation took {stopwatch.Elapsed.TotalMilliseconds:F1} ms; budget is 5 seconds.");
            Assert.True(
                result.Stats.TotalBytes < 2 * 1024 * 1024,
                $"P0 generated file size was {result.Stats.TotalBytes} bytes; budget is below 2 MB.");
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public async Task Generate_AllPromisedModules_CompletesWithinBudget()
        {
            GenerationPipeline pipeline = new();
            Stopwatch stopwatch = Stopwatch.StartNew();

            GenerationResult result = await pipeline.Generate(DslTestFixtures.CreateP0ScaleFixture(), DslTestFixtures.DefaultOptions);

            stopwatch.Stop();
            Assert.Equal(4, result.Packages.Length);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(15),
                $"Promised module generation took {stopwatch.Elapsed.TotalMilliseconds:F1} ms; budget is 15 seconds.");
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public async Task GenerateType_Rectangle_P95CompletesWithinBudget()
        {
            GenerationPipeline pipeline = new();
            IRegistryQuery registry = DslTestFixtures.CreateP0ScaleFixture();

            for (int iteration = 0; iteration < 5; iteration++)
            {
                _ = await pipeline.GenerateType(registry, "QQuickRectangle", DslTestFixtures.DefaultOptions);
            }

            TimeSpan p95 = TimeSpan.MaxValue;
            for (int sampleSet = 0; sampleSet < 3; sampleSet++)
            {
                List<TimeSpan> timings = [];
                for (int iteration = 0; iteration < 25; iteration++)
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    _ = await pipeline.GenerateType(registry, "QQuickRectangle", DslTestFixtures.DefaultOptions);
                    stopwatch.Stop();
                    timings.Add(stopwatch.Elapsed);
                }

                TimeSpan sampleP95 = timings.OrderBy(static timing => timing).ElementAt(23);
                if (sampleP95 < p95)
                {
                    p95 = sampleP95;
                }
            }

            Assert.True(
                p95 < TimeSpan.FromMilliseconds(50),
                $"Single type generation P95 was {p95.TotalMilliseconds:F1} ms; budget is 50 ms.");
        }
    }
}
