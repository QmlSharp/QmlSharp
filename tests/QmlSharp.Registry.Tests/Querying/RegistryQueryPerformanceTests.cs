using System.Diagnostics;
using QmlSharp.Registry.Querying;
using QmlSharp.Registry.Tests.Helpers;

namespace QmlSharp.Registry.Tests.Querying
{
    [Trait("Category", "Performance")]
    [Collection(RegistryQueryPerformanceCollection.Name)]
    public sealed class RegistryQueryPerformanceTests
    {
        private const string LeafQualifiedName = "QmlSharpPerformanceType127";
        private static readonly IRegistryQuery Query = new RegistryQuery(RegistryFixtures.CreatePerformanceFixture());

        [Fact]
        public void Single_type_query_p99_is_below_1ms()
        {
            TimeSpan p99 = MeasureP99(() => Query.FindTypeByQualifiedName(LeafQualifiedName));

            Assert.True(
                p99 < TimeSpan.FromMilliseconds(1),
                $"Single type query P99 was {p99.TotalMilliseconds:F4} ms; budget is 1.0000 ms.");
        }

        [Fact]
        public void Inheritance_chain_query_p99_is_below_1ms()
        {
            TimeSpan p99 = MeasureP99(() => Query.GetInheritanceChain(LeafQualifiedName));

            Assert.True(
                p99 < TimeSpan.FromMilliseconds(1),
                $"Inheritance chain query P99 was {p99.TotalMilliseconds:F4} ms; budget is 1.0000 ms.");
        }

        [Fact]
        public void All_properties_query_p99_is_below_1ms()
        {
            TimeSpan p99 = MeasureP99(() => Query.GetAllProperties(LeafQualifiedName));

            Assert.True(
                p99 < TimeSpan.FromMilliseconds(1),
                $"All properties query P99 was {p99.TotalMilliseconds:F4} ms; budget is 1.0000 ms.");
        }

        private static TimeSpan MeasureP99<TResult>(Func<TResult> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            const int warmupIterations = 32;
            const int sampleCount = 200;
            const int operationsPerSample = 128;

            TResult? result = default;
            for (int iteration = 0; iteration < warmupIterations; iteration++)
            {
                result = action();
            }

            GC.KeepAlive(result);

            double[] perOperationMilliseconds = new double[sampleCount];
            for (int sample = 0; sample < sampleCount; sample++)
            {
                long start = Stopwatch.GetTimestamp();
                for (int iteration = 0; iteration < operationsPerSample; iteration++)
                {
                    result = action();
                }

                long elapsed = Stopwatch.GetTimestamp() - start;
                GC.KeepAlive(result);
                perOperationMilliseconds[sample] = elapsed * 1000d / Stopwatch.Frequency / operationsPerSample;
            }

            Array.Sort(perOperationMilliseconds);
            double p99Milliseconds = perOperationMilliseconds[(int)Math.Ceiling(perOperationMilliseconds.Length * 0.99d) - 1];
            return TimeSpan.FromMilliseconds(p99Milliseconds);
        }
    }
}
