using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.Diagnostics
{
    [Trait("Category", TestCategories.Diagnostic)]
    public sealed class DiagnosticParserTests
    {
        private readonly QtDiagnosticParser parser = new();

        [Fact]
        public void DP_001_ParseSingleStderrDiagnosticLine()
        {
            ImmutableArray<QtDiagnostic> diagnostics = parser.ParseStderr("test.qml:10:5: error: Unknown property \"foo\"");

            QtDiagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Equal("test.qml", diagnostic.File);
            Assert.Equal(10, diagnostic.Line);
            Assert.Equal(5, diagnostic.Column);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("Unknown property \"foo\"", diagnostic.Message);
        }

        [Fact]
        public void DP_002_ParseMultiLineStderrPreservesDiagnosticOrder()
        {
            string stderr = string.Join(
                Environment.NewLine,
                "first.qml:3:1: warning: Unqualified access",
                "second.qml:9:7: error: Expected token \"}\"",
                "third.qml:12:3: info: Import was not used");

            ImmutableArray<QtDiagnostic> diagnostics = parser.ParseStderr(stderr);

            Assert.Equal(3, diagnostics.Length);
            Assert.Equal("first.qml", diagnostics[0].File);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostics[0].Severity);
            Assert.Equal("second.qml", diagnostics[1].File);
            Assert.Equal(DiagnosticSeverity.Error, diagnostics[1].Severity);
            Assert.Equal("third.qml", diagnostics[2].File);
            Assert.Equal(DiagnosticSeverity.Info, diagnostics[2].Severity);
        }

        [Fact]
        public void DP_003_ParseStderrWithoutFilenameUsesOverrideWhenProvided()
        {
            ImmutableArray<QtDiagnostic> diagnostics = parser.ParseStderr("10:5: error: Unknown property \"foo\"", "<string>");

            QtDiagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Equal("<string>", diagnostic.File);
            Assert.Equal(10, diagnostic.Line);
            Assert.Equal(5, diagnostic.Column);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        }

        [Fact]
        public void DP_003a_ParseBracketStderrWithoutFilenameUsesOverride()
        {
            ImmutableArray<QtDiagnostic> diagnostics = parser.ParseStderr(
                "[Dom][QmlFile][Parsing] Error: Expected token `}`",
                "fallback.qml");

            QtDiagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Equal("fallback.qml", diagnostic.File);
            Assert.Null(diagnostic.Line);
            Assert.Null(diagnostic.Column);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("Expected token `}`", diagnostic.Message);
        }

        [Fact]
        public void DP_004_ParseQmlLintJsonArrayOutput()
        {
            string json = ReadFixture("mock-qmllint-json.json");

            ImmutableArray<QtDiagnostic> diagnostics = parser.ParseJson(json);

            Assert.Equal(3, diagnostics.Length);
            Assert.Equal("syntax-error.qml", diagnostics[0].File);
            Assert.Equal(4, diagnostics[0].Line);
            Assert.Equal(1, diagnostics[0].Column);
            Assert.Equal(DiagnosticSeverity.Error, diagnostics[0].Severity);
            Assert.Equal("syntax", diagnostics[0].Category);
            Assert.Equal("lint-warnings.qml", diagnostics[1].File);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostics[1].Severity);
            Assert.Equal("unqualified", diagnostics[1].Category);
            Assert.Equal("Qualify the access through an id", diagnostics[1].Suggestion);
            Assert.Equal(DiagnosticSeverity.Info, diagnostics[2].Severity);
        }

        [Fact]
        public void DP_004_ParseQmlTsQmlLintJsonShape()
        {
            string json = """
                {
                  "files": [
                    {
                      "filename": "test.qml",
                      "success": false,
                      "warnings": [
                        {
                          "charOffset": 39,
                          "column": 12,
                          "id": "incompatible-type",
                          "length": 14,
                          "line": 4,
                          "message": "Cannot assign string to double",
                          "suggestions": [],
                          "type": "warning"
                        }
                      ]
                    }
                  ],
                  "revision": 4
                }
                """;

            ImmutableArray<QtDiagnostic> diagnostics = parser.ParseJson(json);

            QtDiagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Equal("test.qml", diagnostic.File);
            Assert.Equal(4, diagnostic.Line);
            Assert.Equal(12, diagnostic.Column);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Equal("incompatible-type", diagnostic.Category);
            Assert.Equal("Cannot assign string to double", diagnostic.Message);
        }

        [Fact]
        public void DP_005_ParseJsonWithSuggestionsPreservesFirstSuggestion()
        {
            string json = """
                {
                  "files": [
                    {
                      "filename": "fix.qml",
                      "warnings": [
                        {
                          "column": 5,
                          "id": "unused-imports",
                          "line": 2,
                          "message": "Unused import",
                          "suggestions": [
                            {
                              "message": "Remove import",
                              "replacement": "",
                              "filename": "fix.qml",
                              "line": 2,
                              "column": 1,
                              "length": 20
                            }
                          ],
                          "type": "info"
                        }
                      ]
                    }
                  ]
                }
                """;

            ImmutableArray<QtDiagnostic> diagnostics = parser.ParseJson(json);

            QtDiagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Equal("unused-imports", diagnostic.Category);
            Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
            Assert.Equal("Remove import", diagnostic.Suggestion);
        }

        [Fact]
        public void DP_006_EmptyInputReturnsNoDiagnostics()
        {
            Assert.Empty(parser.ParseStderr(string.Empty));
            Assert.Empty(parser.ParseStderr("  \r\n  "));
            Assert.Empty(parser.ParseJson(string.Empty));
            Assert.Empty(parser.ParseJson("  \r\n  "));
        }

        [Fact]
        public void DiagnosticParser_MalformedNonEmptyOutputReturnsNoDiagnostics()
        {
            Assert.Empty(parser.ParseJson("{ not json"));
            Assert.Empty(parser.ParseJson("""{"unexpected":true}"""));
            Assert.Empty(parser.ParseStderr("this is not a diagnostic"));
        }

        [Theory]
        [InlineData("info", DiagnosticSeverity.Info)]
        [InlineData("warning", DiagnosticSeverity.Warning)]
        [InlineData("error", DiagnosticSeverity.Error)]
        [InlineData("hint", DiagnosticSeverity.Hint)]
        [InlineData("disabled", DiagnosticSeverity.Disabled)]
        public void DiagnosticParser_MapsAllSeverityValues(string severity, DiagnosticSeverity expected)
        {
            string json = $$"""
                [
                  {
                    "filename": "severity.qml",
                    "line": 1,
                    "column": 1,
                    "type": "{{severity}}",
                    "message": "severity"
                  }
                ]
                """;

            ImmutableArray<QtDiagnostic> diagnostics = parser.ParseJson(json);

            QtDiagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Equal(expected, diagnostic.Severity);
        }

        [Fact]
        public void DiagnosticParser_PreservesUnknownCategoryStrings()
        {
            string json = """
                [
                  {
                    "filename": "future.qml",
                    "line": 1,
                    "column": 1,
                    "type": "warning",
                    "message": "Future warning",
                    "category": "future-category"
                  }
                ]
                """;

            ImmutableArray<QtDiagnostic> diagnostics = parser.ParseJson(json);

            QtDiagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Equal("future-category", diagnostic.Category);
            Assert.False(QmlLintCategoryExtensions.TryParseCliName(diagnostic.Category, out QmlLintCategory _));
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public void Performance_ParseJson_HandlesLargeDiagnosticPayloadWithLinearBudget()
        {
            string json = CreateLargeQmlLintJson(diagnosticCount: 1_000);
            Stopwatch stopwatch = Stopwatch.StartNew();

            ImmutableArray<QtDiagnostic> diagnostics = parser.ParseJson(json);

            stopwatch.Stop();
            Assert.Equal(1_000, diagnostics.Length);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(2),
                $"Expected parser to handle 1,000 diagnostics within a generous CI budget, actual: {stopwatch.Elapsed}.");
        }

        private static string CreateLargeQmlLintJson(int diagnosticCount)
        {
            StringBuilder builder = new();
            _ = builder.Append("""{"files":[{"filename":"large.qml","warnings":[""");
            for (int index = 0; index < diagnosticCount; index++)
            {
                if (index > 0)
                {
                    _ = builder.Append(',');
                }

                _ = builder.Append(
                    $$"""{"line":{{index + 1}},"column":1,"type":"warning","message":"warning {{index}}","id":"unqualified"}""");
            }

            _ = builder.Append("]}]}");
            return builder.ToString();
        }

        private static string ReadFixture(string fileName)
        {
            return File.ReadAllText(Path.Join(AppContext.BaseDirectory, "Fixtures", "qt-tools", fileName));
        }
    }
}
