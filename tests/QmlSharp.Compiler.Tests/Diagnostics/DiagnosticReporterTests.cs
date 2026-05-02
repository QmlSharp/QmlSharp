using System.Reflection;
using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.Diagnostics
{
    public sealed class DiagnosticReporterTests
    {
        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DiagnosticReporter_DR01_ReportSingleDiagnostic_ReturnsOneEntry()
        {
            DiagnosticReporter reporter = new();

            reporter.Report(new CompilerDiagnostic(DiagnosticCodes.UnknownQmlType, DiagnosticSeverity.Error, "Unknown Rectangle."));

            CompilerDiagnostic diagnostic = Assert.Single(reporter.GetDiagnostics());
            Assert.Equal(DiagnosticCodes.UnknownQmlType, diagnostic.Code);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DiagnosticReporter_DR02_ReportMultipleDiagnostics_AccumulatesAndOrdersStably()
        {
            DiagnosticReporter reporter = new();
            reporter.Report(new CompilerDiagnostic(DiagnosticCodes.UnknownQmlType, DiagnosticSeverity.Error, "second", new SourceLocation("B.cs", 1, 1)));
            reporter.Report(new CompilerDiagnostic(DiagnosticCodes.ProjectLoadFailed, DiagnosticSeverity.Fatal, "third", new SourceLocation("A.cs", 4, 1)));
            reporter.Report(new CompilerDiagnostic(DiagnosticCodes.EmitFailed, DiagnosticSeverity.Error, "first", new SourceLocation("A.cs", 2, 1)));

            ImmutableArray<CompilerDiagnostic> diagnostics = reporter.GetDiagnostics();

            Assert.Equal(3, diagnostics.Length);
            Assert.Equal(DiagnosticCodes.EmitFailed, diagnostics[0].Code);
            Assert.Equal(DiagnosticCodes.ProjectLoadFailed, diagnostics[1].Code);
            Assert.Equal(DiagnosticCodes.UnknownQmlType, diagnostics[2].Code);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DiagnosticReporter_DR03_HasErrors_NoErrorsReported_ReturnsFalse()
        {
            DiagnosticReporter reporter = new();
            reporter.Report(new CompilerDiagnostic(DiagnosticCodes.ImportConflict, DiagnosticSeverity.Warning, "warning"));

            Assert.False(reporter.HasErrors);
            Assert.False(reporter.HasFatal);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DiagnosticReporter_DR04_HasErrors_ErrorReported_ReturnsTrue()
        {
            DiagnosticReporter reporter = new();
            reporter.Report(new CompilerDiagnostic(DiagnosticCodes.BindingTargetNotFound, DiagnosticSeverity.Error, "error"));

            Assert.True(reporter.HasErrors);
            Assert.False(reporter.HasFatal);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DiagnosticReporter_DR05_FilterBySeverity_WarningsOnly()
        {
            DiagnosticReporter reporter = new();
            reporter.Report(new CompilerDiagnostic(DiagnosticCodes.ImportConflict, DiagnosticSeverity.Warning, "warning"));
            reporter.Report(new CompilerDiagnostic(DiagnosticCodes.InternalError, DiagnosticSeverity.Fatal, "fatal"));
            reporter.Report(new CompilerDiagnostic(DiagnosticCodes.UnknownQmlType, DiagnosticSeverity.Error, "error"));

            ImmutableArray<CompilerDiagnostic> diagnostics = reporter.GetDiagnostics(DiagnosticSeverity.Warning);

            CompilerDiagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DiagnosticReporter_DR06_FormatProducesHumanReadableString_WithLocationAndPhase()
        {
            DiagnosticReporter reporter = new();
            CompilerDiagnostic diagnostic = new(
                DiagnosticCodes.UnknownQmlType,
                DiagnosticSeverity.Error,
                "QML type could not be resolved.",
                new SourceLocation("CounterView.cs", 12, 8),
                "TransformingDsl");

            string formatted = reporter.Format(diagnostic);

            Assert.Contains("CounterView.cs(12,8)", formatted, StringComparison.Ordinal);
            Assert.Contains(DiagnosticCodes.UnknownQmlType, formatted, StringComparison.Ordinal);
            Assert.Contains("Error", formatted, StringComparison.Ordinal);
            Assert.Contains("TransformingDsl", formatted, StringComparison.Ordinal);
            Assert.Contains("QML type could not be resolved.", formatted, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DiagnosticReporter_DR07_Clear_RemovesAllDiagnostics()
        {
            DiagnosticReporter reporter = new();
            reporter.Report(new CompilerDiagnostic(DiagnosticCodes.ImportConflict, DiagnosticSeverity.Warning, "warning"));

            reporter.Clear();

            Assert.Empty(reporter.GetDiagnostics());
            Assert.False(reporter.HasErrors);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DiagnosticReporter_DR08_AllAnalyzerCodes_ProduceValidMessages()
        {
            AssertCodesFormat("QMLSHARP-A");
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DiagnosticReporter_DR09_AllTransformCodes_ProduceValidMessages()
        {
            AssertCodesFormat("QMLSHARP-T");
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DiagnosticReporter_DR10_AllPostProcessCodegenAndGeneralCodes_ProduceValidMessages()
        {
            AssertCodesFormat("QMLSHARP-P");
            AssertCodesFormat("QMLSHARP-C");
            AssertCodesFormat("QMLSHARP-G");
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DiagnosticReporter_All38DiagnosticCodeConstants_HaveMessageTemplates()
        {
            ImmutableArray<string> codes = GetDiagnosticCodeConstants();

            Assert.Equal(38, codes.Length);
            Assert.Equal(codes.Order(StringComparer.Ordinal).ToArray(), DiagnosticMessageCatalog.AllCodes.ToArray());
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DiagnosticReporter_AnalyzerCodeNames_MatchApiDesignContract()
        {
            Assert.Equal("QMLSHARP-A001", DiagnosticCodes.InvalidStateAttribute);
            Assert.Equal("QMLSHARP-A002", DiagnosticCodes.InvalidCommandAttribute);
            Assert.Equal("QMLSHARP-A003", DiagnosticCodes.InvalidEffectAttribute);
            Assert.Equal("QMLSHARP-A004", DiagnosticCodes.ViewModelNotFound);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DiagnosticReporter_SeverityFilteringAndMaxAllowedSeverity_DecideBlockingDiagnostics()
        {
            DiagnosticReporter reporter = new();
            _ = reporter.Report(DiagnosticCodes.ImportConflict, DiagnosticSeverity.Warning);
            _ = reporter.Report(DiagnosticCodes.UnknownQmlType, DiagnosticSeverity.Error);

            CompilerOptions warningAllowed = CompilerTestFixtures.DefaultOptions with
            {
                MaxAllowedSeverity = DiagnosticSeverity.Warning,
            };
            CompilerOptions errorAllowed = CompilerTestFixtures.DefaultOptions with
            {
                MaxAllowedSeverity = DiagnosticSeverity.Error,
            };

            Assert.True(reporter.HasBlockingDiagnostics(warningAllowed));
            Assert.False(reporter.HasBlockingDiagnostics(errorAllowed));
            CompilerDiagnostic diagnostic = Assert.Single(reporter.GetDiagnosticsAtOrAbove(DiagnosticSeverity.Error));
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DiagnosticReporter_FatalDiagnostic_BlocksEvenWhenErrorsAreAllowed()
        {
            DiagnosticReporter reporter = new();
            _ = reporter.Report(DiagnosticCodes.InternalError, DiagnosticSeverity.Fatal);
            CompilerOptions options = CompilerTestFixtures.DefaultOptions with
            {
                MaxAllowedSeverity = DiagnosticSeverity.Error,
            };

            Assert.True(reporter.HasErrors);
            Assert.True(reporter.HasFatal);
            Assert.True(reporter.HasBlockingDiagnostics(options));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DiagnosticReporter_LocationFormatting_CoversMissingAndPartialLocations()
        {
            DiagnosticReporter reporter = new();

            string noLocation = reporter.Format(new CompilerDiagnostic(DiagnosticCodes.EmitFailed, DiagnosticSeverity.Error, string.Empty));
            string fileOnly = reporter.Format(new CompilerDiagnostic(DiagnosticCodes.EmitFailed, DiagnosticSeverity.Error, "failed", SourceLocation.FileOnly("View.qml")));
            string lineAndColumnOnly = reporter.Format(new CompilerDiagnostic(DiagnosticCodes.EmitFailed, DiagnosticSeverity.Error, "failed", SourceLocation.LineColumn(7, 3)));

            Assert.DoesNotContain(":", noLocation[..noLocation.IndexOf(DiagnosticCodes.EmitFailed, StringComparison.Ordinal)], StringComparison.Ordinal);
            Assert.StartsWith("View.qml:", fileOnly, StringComparison.Ordinal);
            Assert.StartsWith("line 7, column 3:", lineAndColumnOnly, StringComparison.Ordinal);
            Assert.Contains(DiagnosticMessageCatalog.GetTemplate(DiagnosticCodes.EmitFailed), noLocation, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void SourceLocation_PublicConstructor_PreservesThreeArgumentContract()
        {
            ConstructorInfo? constructor = typeof(SourceLocation).GetConstructor([typeof(string), typeof(int), typeof(int)]);
            SourceLocation location = new("CounterView.cs", 12, 8);

            Assert.NotNull(constructor);
            Assert.Equal("CounterView.cs", location.FilePath);
            Assert.Equal(12, location.Line);
            Assert.Equal(8, location.Column);
        }

        private static void AssertCodesFormat(string prefix)
        {
            DiagnosticReporter reporter = new();
            ImmutableArray<string> codes = GetDiagnosticCodeConstants()
                .Where(code => code.StartsWith(prefix, StringComparison.Ordinal))
                .ToImmutableArray();

            Assert.NotEmpty(codes);

            foreach (string code in codes)
            {
                CompilerDiagnostic diagnostic = reporter.Report(code, DiagnosticSeverity.Error, details: "detail.");
                string formatted = reporter.Format(diagnostic);

                Assert.Contains(code, formatted, StringComparison.Ordinal);
                Assert.Contains("detail.", formatted, StringComparison.Ordinal);
                Assert.DoesNotContain("Unknown compiler diagnostic", formatted, StringComparison.Ordinal);
            }
        }

        private static ImmutableArray<string> GetDiagnosticCodeConstants()
        {
            return typeof(DiagnosticCodes)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(static field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                .Select(static field => (string)field.GetRawConstantValue()!)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
        }
    }
}
