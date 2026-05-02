#pragma warning disable MA0048

using QmlSharp.Qml.Ast;

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Compiler output for one View source file.
    /// </summary>
    public sealed record CompilationUnit
    {
        /// <summary>Gets the source file path.</summary>
        public required string SourceFilePath { get; init; }

        /// <summary>Gets the View class name.</summary>
        public required string ViewClassName { get; init; }

        /// <summary>Gets the ViewModel class name.</summary>
        public required string ViewModelClassName { get; init; }

        /// <summary>Gets emitted QML text, if available.</summary>
        public string? QmlText { get; init; }

        /// <summary>Gets the generated ViewModel schema, if available.</summary>
        public ViewModelSchema? Schema { get; init; }

        /// <summary>Gets the generated QML AST document, if available.</summary>
        public QmlDocument? Document { get; init; }

        /// <summary>Gets the generated source map, if available.</summary>
        public SourceMap? SourceMap { get; init; }

        /// <summary>Gets unit diagnostics.</summary>
        public ImmutableArray<CompilerDiagnostic> Diagnostics { get; init; } = ImmutableArray<CompilerDiagnostic>.Empty;

        /// <summary>Gets unit stats.</summary>
        public CompilationUnitStats Stats { get; init; } = new();

        /// <summary>Gets a value indicating whether the unit has no error diagnostics.</summary>
        public bool Success => !Diagnostics.Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Fatal);
    }

    /// <summary>Statistics for one compilation unit.</summary>
    public sealed record CompilationUnitStats
    {
        /// <summary>Gets elapsed milliseconds for the unit.</summary>
        public long ElapsedMilliseconds { get; init; }

        /// <summary>Gets emitted QML byte count.</summary>
        public long QmlBytes { get; init; }
    }

    /// <summary>Aggregate compiler result.</summary>
    public sealed record CompilationResult
    {
        /// <summary>Gets compilation units.</summary>
        public ImmutableArray<CompilationUnit> Units { get; init; } = ImmutableArray<CompilationUnit>.Empty;

        /// <summary>Gets aggregate event binding metadata.</summary>
        public EventBindingsIndex EventBindings { get; init; } = EventBindingsIndex.Empty;

        /// <summary>Gets aggregate diagnostics.</summary>
        public ImmutableArray<CompilerDiagnostic> Diagnostics { get; init; } = ImmutableArray<CompilerDiagnostic>.Empty;

        /// <summary>Gets aggregate stats.</summary>
        public CompilationStats Stats { get; init; } = new();

        /// <summary>Gets a value indicating whether the compilation succeeded.</summary>
        public bool Success => !Diagnostics.Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Fatal)
            && Units.All(unit => unit.Success);

        /// <summary>
        /// Creates a deterministic aggregate result from per-file units.
        /// </summary>
        /// <param name="units">The compilation units to aggregate.</param>
        /// <param name="diagnostics">Additional project-level diagnostics.</param>
        /// <param name="eventBindings">Optional prebuilt event binding index.</param>
        /// <param name="elapsedMilliseconds">Total elapsed compilation time.</param>
        /// <returns>An aggregate compilation result.</returns>
        public static CompilationResult FromUnits(
            ImmutableArray<CompilationUnit> units,
            ImmutableArray<CompilerDiagnostic> diagnostics = default,
            EventBindingsIndex? eventBindings = null,
            long elapsedMilliseconds = 0)
        {
            ImmutableArray<CompilationUnit> normalizedUnits = units.IsDefault
                ? ImmutableArray<CompilationUnit>.Empty
                : units;
            ImmutableArray<CompilerDiagnostic> normalizedDiagnostics = diagnostics.IsDefault
                ? ImmutableArray<CompilerDiagnostic>.Empty
                : diagnostics;
            ImmutableArray<CompilerDiagnostic> allDiagnostics = normalizedUnits
                .SelectMany(static unit => unit.Diagnostics.IsDefault ? ImmutableArray<CompilerDiagnostic>.Empty : unit.Diagnostics)
                .Concat(normalizedDiagnostics)
                .OrderBy(static diagnostic => diagnostic.Location?.FilePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Location?.Line ?? 0)
                .ThenBy(static diagnostic => diagnostic.Location?.Column ?? 0)
                .ThenBy(static diagnostic => diagnostic.Severity)
                .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
                .ToImmutableArray();

            CompilationStats stats = new()
            {
                TotalFiles = normalizedUnits.Length,
                SuccessfulFiles = normalizedUnits.Count(static unit => unit.Success),
                FailedFiles = normalizedUnits.Count(static unit => !unit.Success),
                Warnings = allDiagnostics.Count(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning),
                Errors = allDiagnostics.Count(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Fatal),
                ElapsedMilliseconds = elapsedMilliseconds,
            };

            return new CompilationResult
            {
                Units = normalizedUnits
                    .OrderBy(static unit => unit.SourceFilePath, StringComparer.Ordinal)
                    .ThenBy(static unit => unit.ViewClassName, StringComparer.Ordinal)
                    .ToImmutableArray(),
                EventBindings = eventBindings ?? EventBindingsIndex.Empty,
                Diagnostics = allDiagnostics,
                Stats = stats,
            };
        }
    }

    /// <summary>Aggregate compiler statistics.</summary>
    public sealed record CompilationStats
    {
        /// <summary>Gets total source file count.</summary>
        public int TotalFiles { get; init; }

        /// <summary>Gets successful file count.</summary>
        public int SuccessfulFiles { get; init; }

        /// <summary>Gets failed file count.</summary>
        public int FailedFiles { get; init; }

        /// <summary>Gets warning count.</summary>
        public int Warnings { get; init; }

        /// <summary>Gets error count.</summary>
        public int Errors { get; init; }

        /// <summary>Gets elapsed milliseconds.</summary>
        public long ElapsedMilliseconds { get; init; }
    }
}

#pragma warning restore MA0048
