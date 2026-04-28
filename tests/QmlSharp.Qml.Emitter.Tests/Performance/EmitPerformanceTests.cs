using System.Diagnostics;
using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Performance
{
    [Collection(EmitPerformanceCollection.Name)]
    public sealed class EmitPerformanceTests
    {
        private const double SmallDocumentAverageBudgetMs = 5.0;
        private const double LargeDocumentSingleCallBudgetMs = 50.0;
        private const double SourceMapOverheadMultiplierBudget = 2.0;
        private const double FragmentAverageBudgetMs = 1.0;
        private const double SourceMapQueryAverageBudgetMs = 1.0;

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public void PF_01_EmitHundredNodeDocument_AveragesUnderFiveMilliseconds()
        {
            IQmlEmitter emitter = new QmlEmitter();
            QmlDocument document = BuildFlatDocument(bindingCount: 100);

            _ = emitter.Emit(document);

            double averageMs = MeasureAverage(iterations: 1_000, () => _ = emitter.Emit(document));

            Assert.True(
                averageMs < SmallDocumentAverageBudgetMs,
                $"Expected 100-node Emit() average below {SmallDocumentAverageBudgetMs} ms; actual {averageMs:0.###} ms.");
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public void PF_02_EmitThousandNodeDocument_CompletesUnderFiftyMilliseconds()
        {
            IQmlEmitter emitter = new QmlEmitter();
            QmlDocument document = BuildFlatDocument(bindingCount: 1_000);

            _ = emitter.Emit(document);

            double elapsedMs = MeasureSingle(() => _ = emitter.Emit(document));

            Assert.True(
                elapsedMs < LargeDocumentSingleCallBudgetMs,
                $"Expected 1000-node Emit() below {LargeDocumentSingleCallBudgetMs} ms; actual {elapsedMs:0.###} ms.");
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public void PF_03_EmitWithSourceMapHundredNodeDocument_StaysWithinTwoTimesPlainEmit()
        {
            IQmlEmitter emitter = new QmlEmitter();
            QmlDocument document = BuildFlatDocument(bindingCount: 100);

            _ = emitter.Emit(document);
            _ = emitter.EmitWithSourceMap(document).SourceMap.Entries.Count;

            double plainAverageMs = MeasureAverage(iterations: 500, () => _ = emitter.Emit(document));
            double mappedAverageMs = MeasureAverage(
                iterations: 500,
                () => _ = emitter.EmitWithSourceMap(document).SourceMap.Entries.Count);

            double allowedMs = Math.Max(plainAverageMs * SourceMapOverheadMultiplierBudget, plainAverageMs + 0.25);
            Assert.True(
                mappedAverageMs <= allowedMs,
                $"Expected source-map Emit() average <= {allowedMs:0.###} ms (plain {plainAverageMs:0.###} ms); actual {mappedAverageMs:0.###} ms.");
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public void PF_04_EmitFragmentSingleBinding_AveragesUnderOneMillisecond()
        {
            IQmlEmitter emitter = new QmlEmitter();
            BindingNode binding = new()
            {
                PropertyName = "width",
                Value = Values.Number(100),
            };

            _ = emitter.EmitFragment(binding);

            double averageMs = MeasureAverage(iterations: 10_000, () => _ = emitter.EmitFragment(binding));

            Assert.True(
                averageMs < FragmentAverageBudgetMs,
                $"Expected binding fragment EmitFragment() average below {FragmentAverageBudgetMs} ms; actual {averageMs:0.###} ms.");
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public void PF_05_SourceMapThousandNodeInnermostQuery_AveragesUnderOneMillisecond()
        {
            IQmlEmitter emitter = new QmlEmitter();
            QmlDocument document = BuildFlatDocument(bindingCount: 1_000);
            EmitResult result = emitter.EmitWithSourceMap(document);

            _ = result.SourceMap.GetInnermostNodeAt(502, 5);

            double averageMs = MeasureAverage(iterations: 1_000, () => _ = result.SourceMap.GetInnermostNodeAt(502, 5));

            Assert.True(
                averageMs < SourceMapQueryAverageBudgetMs,
                $"Expected source-map innermost query average below {SourceMapQueryAverageBudgetMs} ms; actual {averageMs:0.###} ms.");
        }

        private static QmlDocument BuildFlatDocument(int bindingCount)
        {
            ImmutableArray<AstNode>.Builder members = ImmutableArray.CreateBuilder<AstNode>(bindingCount);
            for (int index = 0; index < bindingCount; index++)
            {
                members.Add(new BindingNode
                {
                    PropertyName = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"value{index}"),
                    Value = Values.Number(index),
                });
            }

            return new QmlDocument
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members = members.ToImmutable(),
                },
            };
        }

        private static double MeasureSingle(Action action)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();

            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static double MeasureAverage(int iterations, Action action)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int index = 0; index < iterations; index++)
            {
                action();
            }

            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds / iterations;
        }
    }
}
