using System.Collections.Immutable;
using QmlSharp.Qml.Ast;

#pragma warning disable MA0048

namespace QmlSharp.Qml.Emitter
{
    /// <summary>
    /// Pipeline diagnostic severity independent of any concrete Qt tool wrapper.
    /// </summary>
    public enum PipelineDiagnosticSeverity
    {
        /// <summary>
        /// Informational diagnostic.
        /// </summary>
        Info,

        /// <summary>
        /// Warning diagnostic.
        /// </summary>
        Warning,

        /// <summary>
        /// Error diagnostic.
        /// </summary>
        Error,
    }

    /// <summary>
    /// Structured diagnostic emitted by a pipeline stage.
    /// </summary>
    public sealed record PipelineDiagnostic
    {
        /// <summary>
        /// Gets optional diagnostic code.
        /// </summary>
        public string? Code { get; init; }

        /// <summary>
        /// Gets diagnostic severity.
        /// </summary>
        public required PipelineDiagnosticSeverity Severity { get; init; }

        /// <summary>
        /// Gets diagnostic message.
        /// </summary>
        public required string Message { get; init; }

        /// <summary>
        /// Gets optional file path.
        /// </summary>
        public string? File { get; init; }

        /// <summary>
        /// Gets optional 1-based line number.
        /// </summary>
        public int? Line { get; init; }

        /// <summary>
        /// Gets optional 1-based column number.
        /// </summary>
        public int? Column { get; init; }
    }

    /// <summary>
    /// Pipeline orchestrating emit, optional format, and optional lint stages.
    /// </summary>
    public interface IEmitPipeline
    {
        /// <summary>
        /// Process a single QML document through the pipeline.
        /// </summary>
        /// <param name="document">The QML document AST.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Pipeline result.</returns>
        Task<PipelineResult> ProcessAsync(QmlDocument document, CancellationToken ct = default);

        /// <summary>
        /// Process multiple named QML documents through the pipeline.
        /// </summary>
        /// <param name="documents">Named documents to process.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Per-document pipeline results in deterministic order.</returns>
        Task<ImmutableArray<NamedPipelineResult>> ProcessBatchAsync(
            ImmutableArray<NamedDocument> documents,
            CancellationToken ct = default);
    }

    /// <summary>
    /// A QML document with a stable name, usually an output path.
    /// </summary>
    public sealed record NamedDocument
    {
        /// <summary>
        /// Gets document name or output path.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets QML document AST.
        /// </summary>
        public required QmlDocument Document { get; init; }
    }

    /// <summary>
    /// Configuration for the emit pipeline.
    /// </summary>
    public sealed record EmitPipelineConfig
    {
        /// <summary>
        /// Gets emit options for the emit stage.
        /// </summary>
        public EmitOptions EmitOptions { get; init; } = new();

        /// <summary>
        /// Gets a value indicating whether to run a formatter after emit.
        /// </summary>
        public bool EnableFormat { get; init; } = true;

        /// <summary>
        /// Gets a value indicating whether to run a linter after format.
        /// </summary>
        public bool EnableLint { get; init; } = true;

        /// <summary>
        /// Gets a value indicating whether to include source maps in the emit stage.
        /// </summary>
        public bool EnableSourceMap { get; init; }
    }

    /// <summary>
    /// Result of processing a single document through the pipeline.
    /// </summary>
    public sealed record PipelineResult
    {
        /// <summary>
        /// Gets final text output after all enabled stages.
        /// </summary>
        public required string Text { get; init; }

        /// <summary>
        /// Gets overall validity. True when lint passed or lint was disabled.
        /// </summary>
        public required bool Valid { get; init; }

        /// <summary>
        /// Gets emit-stage result.
        /// </summary>
        public required EmitStageResult EmitResult { get; init; }

        /// <summary>
        /// Gets format-stage result when formatting ran.
        /// </summary>
        public FormatStageResult? FormatResult { get; init; }

        /// <summary>
        /// Gets lint-stage result when linting ran.
        /// </summary>
        public LintStageResult? LintResult { get; init; }

        /// <summary>
        /// Gets total wall-clock time for all stages in milliseconds.
        /// </summary>
        public required double TotalDurationMs { get; init; }
    }

    /// <summary>
    /// Per-document pipeline result with document name.
    /// </summary>
    public sealed record NamedPipelineResult
    {
        /// <summary>
        /// Gets document name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets pipeline result.
        /// </summary>
        public required PipelineResult Result { get; init; }
    }

    /// <summary>
    /// Result from the emit stage.
    /// </summary>
    public sealed record EmitStageResult
    {
        /// <summary>
        /// Gets emitted QML text before formatting.
        /// </summary>
        public required string Text { get; init; }

        /// <summary>
        /// Gets source map when source maps were requested.
        /// </summary>
        public ISourceMap? SourceMap { get; init; }

        /// <summary>
        /// Gets emit-stage duration in milliseconds.
        /// </summary>
        public required double DurationMs { get; init; }
    }

    /// <summary>
    /// Result from the format stage.
    /// </summary>
    public sealed record FormatStageResult
    {
        /// <summary>
        /// Gets formatted QML text.
        /// </summary>
        public required string Text { get; init; }

        /// <summary>
        /// Gets format-stage duration in milliseconds.
        /// </summary>
        public required double DurationMs { get; init; }
    }

    /// <summary>
    /// Result from the lint stage.
    /// </summary>
    public sealed record LintStageResult
    {
        /// <summary>
        /// Gets a value indicating whether the linter accepted the QML text.
        /// </summary>
        public required bool Valid { get; init; }

        /// <summary>
        /// Gets structured lint diagnostics.
        /// </summary>
        public ImmutableArray<PipelineDiagnostic> Diagnostics { get; init; } = ImmutableArray<PipelineDiagnostic>.Empty;

        /// <summary>
        /// Gets lint-stage duration in milliseconds.
        /// </summary>
        public required double DurationMs { get; init; }
    }
}

#pragma warning restore MA0048
