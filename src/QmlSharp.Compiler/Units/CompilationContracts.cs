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
