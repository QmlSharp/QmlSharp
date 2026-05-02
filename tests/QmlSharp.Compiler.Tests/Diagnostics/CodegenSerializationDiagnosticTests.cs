using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.Diagnostics
{
    public sealed class CodegenSerializationDiagnosticTests
    {
        [Theory]
        [InlineData(DiagnosticCodes.SchemaSerializationFailed, "CounterViewModel.schema.json")]
        [InlineData(DiagnosticCodes.SourceMapWriteFailed, "CounterView.qml.map")]
        [Trait("Category", TestCategories.Unit)]
        public void CodegenSerializationDiagnostics_FormatWithArtifactDetails(string code, string artifactName)
        {
            DiagnosticReporter reporter = new();

            CompilerDiagnostic diagnostic = reporter.Report(
                code,
                DiagnosticSeverity.Error,
                SourceLocation.FileOnly(artifactName),
                "WritingArtifacts",
                "Serialization did not complete.");

            string formatted = reporter.Format(diagnostic);

            Assert.Contains(code, formatted, StringComparison.Ordinal);
            Assert.Contains(artifactName, formatted, StringComparison.Ordinal);
            Assert.Contains("Serialization did not complete.", formatted, StringComparison.Ordinal);
        }
    }
}
