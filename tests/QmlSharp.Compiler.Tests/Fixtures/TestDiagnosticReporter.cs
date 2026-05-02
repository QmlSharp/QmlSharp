namespace QmlSharp.Compiler.Tests.Fixtures
{
    internal sealed class TestDiagnosticReporter : IDiagnosticReporter
    {
        private readonly List<CompilerDiagnostic> diagnostics = [];

        public bool HasErrors => diagnostics.Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Fatal);

        public void Clear()
        {
            diagnostics.Clear();
        }

        public string Format(CompilerDiagnostic diagnostic)
        {
            string location = diagnostic.Location is null
                ? string.Empty
                : $" {diagnostic.Location.FilePath}({diagnostic.Location.Line},{diagnostic.Location.Column})";

            return $"{diagnostic.Code} {diagnostic.Severity}:{location} {diagnostic.Message}";
        }

        public ImmutableArray<CompilerDiagnostic> GetDiagnostics()
        {
            return diagnostics.ToImmutableArray();
        }

        public ImmutableArray<CompilerDiagnostic> GetDiagnostics(DiagnosticSeverity severity)
        {
            return diagnostics.Where(diagnostic => diagnostic.Severity == severity).ToImmutableArray();
        }

        public void Report(CompilerDiagnostic diagnostic)
        {
            diagnostics.Add(diagnostic);
        }
    }
}
