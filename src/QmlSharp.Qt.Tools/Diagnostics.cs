#pragma warning disable MA0048

namespace QmlSharp.Qt.Tools
{
    /// <summary>Structured diagnostic emitted by a Qt tool.</summary>
    public sealed record QtDiagnostic
    {
        /// <summary>Source file path.</summary>
        public string? File { get; init; }

        /// <summary>One-based line number.</summary>
        public int? Line { get; init; }

        /// <summary>One-based column number.</summary>
        public int? Column { get; init; }

        /// <summary>Diagnostic severity.</summary>
        public required DiagnosticSeverity Severity { get; init; }

        /// <summary>Human-readable diagnostic message.</summary>
        public required string Message { get; init; }

        /// <summary>qmllint warning category.</summary>
        public string? Category { get; init; }

        /// <summary>Suggested fix, when available.</summary>
        public string? Suggestion { get; init; }
    }
}

#pragma warning restore MA0048
