using System.Diagnostics;
using System.Globalization;

namespace QmlSharp.DevTools.Tests.Benchmarks
{
    internal static class BenchmarkAssert
    {
        public static TimeSpan Measure(Action action)
        {
            long start = Stopwatch.GetTimestamp();
            action();
            return Stopwatch.GetElapsedTime(start);
        }

        public static async Task<TimeSpan> MeasureAsync(Func<Task> action)
        {
            long start = Stopwatch.GetTimestamp();
            await action();
            return Stopwatch.GetElapsedTime(start);
        }

        public static TimeSpan Percentile99(IReadOnlyList<TimeSpan> samples)
        {
            if (samples.Count == 0)
            {
                throw new ArgumentException("At least one sample is required.", nameof(samples));
            }

            TimeSpan[] sorted = samples
                .OrderBy(static sample => sample)
                .ToArray();
            int index = Math.Clamp(
                (int)Math.Ceiling(sorted.Length * 0.99d) - 1,
                0,
                sorted.Length - 1);
            return sorted[index];
        }

        public static void Under(
            TimeSpan actual,
            TimeSpan budget,
            string operation)
        {
            Assert.True(
                actual < budget,
                operation + " took " + Format(actual) + ", budget is " + Format(budget) + ".");
        }

        public static void Within(
            TimeSpan actual,
            TimeSpan expected,
            TimeSpan tolerance,
            string operation)
        {
            TimeSpan delta = actual >= expected
                ? actual - expected
                : expected - actual;
            Assert.True(
                delta <= tolerance,
                operation + " took " + Format(actual) + ", expected " + Format(expected) +
                " +/- " + Format(tolerance) + ".");
        }

        private static string Format(TimeSpan value)
        {
            return value.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) + " ms";
        }
    }
}
