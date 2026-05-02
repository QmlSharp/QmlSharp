namespace QmlSharp.Compiler
{
    /// <summary>
    /// Default in-memory diagnostic reporter used by the compiler pipeline.
    /// </summary>
    public sealed class DiagnosticReporter : IDiagnosticReporter
    {
        private readonly List<DiagnosticEntry> diagnostics = [];
        private long nextSequence;

        /// <inheritdoc />
        public bool HasErrors => diagnostics.Any(static entry =>
            entry.Diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Fatal);

        /// <summary>Gets a value indicating whether a fatal diagnostic has been reported.</summary>
        public bool HasFatal => diagnostics.Any(static entry => entry.Diagnostic.Severity == DiagnosticSeverity.Fatal);

        /// <inheritdoc />
        public void Report(CompilerDiagnostic diagnostic)
        {
            ArgumentNullException.ThrowIfNull(diagnostic);

            diagnostics.Add(new DiagnosticEntry(diagnostic, nextSequence));
            nextSequence++;
        }

        /// <summary>
        /// Reports a diagnostic from a known diagnostic code and message template.
        /// </summary>
        /// <param name="code">The diagnostic code.</param>
        /// <param name="severity">The diagnostic severity.</param>
        /// <param name="location">Optional source location.</param>
        /// <param name="phase">Optional compiler phase.</param>
        /// <param name="details">Optional detail text appended to the code template.</param>
        /// <returns>The reported diagnostic.</returns>
        public CompilerDiagnostic Report(
            string code,
            DiagnosticSeverity severity,
            SourceLocation? location = null,
            string? phase = null,
            string? details = null)
        {
            CompilerDiagnostic diagnostic = new(
                code,
                severity,
                DiagnosticMessageCatalog.FormatMessage(code, details),
                location,
                phase);
            Report(diagnostic);
            return diagnostic;
        }

        /// <inheritdoc />
        public ImmutableArray<CompilerDiagnostic> GetDiagnostics()
        {
            return OrderedEntries().Select(static entry => entry.Diagnostic).ToImmutableArray();
        }

        /// <inheritdoc />
        public ImmutableArray<CompilerDiagnostic> GetDiagnostics(DiagnosticSeverity severity)
        {
            return OrderedEntries()
                .Where(entry => entry.Diagnostic.Severity == severity)
                .Select(static entry => entry.Diagnostic)
                .ToImmutableArray();
        }

        /// <summary>
        /// Gets diagnostics at or above the requested severity.
        /// </summary>
        /// <param name="minimumSeverity">The minimum severity to include.</param>
        /// <returns>Diagnostics sorted in deterministic order.</returns>
        public ImmutableArray<CompilerDiagnostic> GetDiagnosticsAtOrAbove(DiagnosticSeverity minimumSeverity)
        {
            return OrderedEntries()
                .Where(entry => entry.Diagnostic.Severity >= minimumSeverity)
                .Select(static entry => entry.Diagnostic)
                .ToImmutableArray();
        }

        /// <summary>
        /// Gets a value indicating whether reported diagnostics exceed the configured severity policy.
        /// </summary>
        /// <param name="options">The compiler options that define the severity policy.</param>
        /// <returns><see langword="true"/> when compilation must stop.</returns>
        public bool HasBlockingDiagnostics(CompilerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            return diagnostics.Any(entry => options.ShouldStopOn(entry.Diagnostic.Severity));
        }

        /// <inheritdoc />
        public string Format(CompilerDiagnostic diagnostic)
        {
            ArgumentNullException.ThrowIfNull(diagnostic);

            string location = FormatLocation(diagnostic.Location);
            string message = string.IsNullOrWhiteSpace(diagnostic.Message)
                ? DiagnosticMessageCatalog.GetTemplate(diagnostic.Code)
                : diagnostic.Message;
            string phase = string.IsNullOrWhiteSpace(diagnostic.Phase)
                ? string.Empty
                : $" [{diagnostic.Phase}]";
            string body = $"{diagnostic.Code} {diagnostic.Severity}{phase}: {message}";

            if (location.Length == 0)
            {
                return body;
            }

            return $"{location}: {body}";
        }

        /// <inheritdoc />
        public void Clear()
        {
            diagnostics.Clear();
            nextSequence = 0;
        }

        private IEnumerable<DiagnosticEntry> OrderedEntries()
        {
            return diagnostics
                .OrderBy(static entry => entry.Diagnostic.Location?.FilePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static entry => entry.Diagnostic.Location?.Line ?? 0)
                .ThenBy(static entry => entry.Diagnostic.Location?.Column ?? 0)
                .ThenBy(static entry => entry.Diagnostic.Severity)
                .ThenBy(static entry => entry.Diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(static entry => entry.Diagnostic.Message, StringComparer.Ordinal)
                .ThenBy(static entry => entry.Sequence);
        }

        private static string FormatLocation(SourceLocation? location)
        {
            if (location is null)
            {
                return string.Empty;
            }

            bool hasFile = !string.IsNullOrWhiteSpace(location.FilePath);
            bool hasLine = location.Line.HasValue;
            bool hasColumn = location.Column.HasValue;

            if (hasFile && hasLine && hasColumn)
            {
                return $"{location.FilePath}({location.Line},{location.Column})";
            }

            if (hasFile && hasLine)
            {
                return $"{location.FilePath}({location.Line})";
            }

            if (hasFile)
            {
                return location.FilePath!;
            }

            if (hasLine && hasColumn)
            {
                return $"line {location.Line}, column {location.Column}";
            }

            if (hasLine)
            {
                return $"line {location.Line}";
            }

            if (hasColumn)
            {
                return $"column {location.Column}";
            }

            return string.Empty;
        }

        private sealed record DiagnosticEntry(CompilerDiagnostic Diagnostic, long Sequence);
    }
}
