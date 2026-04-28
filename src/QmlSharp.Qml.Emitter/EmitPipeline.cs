using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using QmlSharp.Qml.Ast;

namespace QmlSharp.Qml.Emitter
{
    /// <summary>
    /// Default emit pipeline implementation. It orchestrates pure emission and optional
    /// injected formatter/linter services; it never invokes external tools directly.
    /// </summary>
    public sealed class EmitPipeline : IEmitPipeline
    {
        private const string DefaultDocumentName = "<document>";
        private readonly IQmlEmitter _emitter;
        private readonly EmitPipelineConfig _config;
        private readonly IEmitPipelineFormatter? _formatter;
        private readonly IEmitPipelineLinter? _linter;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmitPipeline"/> class.
        /// </summary>
        /// <param name="emitter">Emitter used for the emit stage.</param>
        /// <param name="config">Pipeline configuration. Null uses defaults.</param>
        /// <param name="formatter">Optional formatter adapter.</param>
        /// <param name="linter">Optional linter adapter.</param>
        public EmitPipeline(
            IQmlEmitter emitter,
            EmitPipelineConfig? config = null,
            IEmitPipelineFormatter? formatter = null,
            IEmitPipelineLinter? linter = null)
        {
            _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
            _config = config ?? new EmitPipelineConfig();
            _formatter = formatter;
            _linter = linter;
        }

        /// <inheritdoc/>
        public Task<PipelineResult> ProcessAsync(QmlDocument document, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(document);

            return ProcessCoreAsync(DefaultDocumentName, document, ct);
        }

        /// <inheritdoc/>
        public async Task<ImmutableArray<NamedPipelineResult>> ProcessBatchAsync(
            ImmutableArray<NamedDocument> documents,
            CancellationToken ct = default)
        {
            if (documents.IsDefaultOrEmpty)
            {
                return ImmutableArray<NamedPipelineResult>.Empty;
            }

            ImmutableArray<NamedPipelineResult>.Builder results = ImmutableArray.CreateBuilder<NamedPipelineResult>(documents.Length);
            for (int index = 0; index < documents.Length; index++)
            {
                ct.ThrowIfCancellationRequested();

                NamedDocument namedDocument = documents[index];
                string name = namedDocument?.Name ?? string.Empty;
                PipelineResult result;

                if (namedDocument is null)
                {
                    result = CreateDocumentFailure(name, "Named document cannot be null.");
                }
                else if (namedDocument.Document is null)
                {
                    result = CreateDocumentFailure(name, "Named document requires a QML document.");
                }
                else
                {
                    result = await ProcessCoreAsync(GetDocumentName(namedDocument.Name), namedDocument.Document, ct).ConfigureAwait(false);
                }

                results.Add(new NamedPipelineResult
                {
                    Name = name,
                    Result = result,
                });
            }

            return results.ToImmutable();
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Pipeline stages are isolated so batch processing can report per-document failures.")]
        private async Task<PipelineResult> ProcessCoreAsync(string documentName, QmlDocument document, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            Stopwatch total = Stopwatch.StartNew();
            ImmutableArray<PipelineDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<PipelineDiagnostic>();
            StageOutcome<EmitStageResult> emitOutcome = TryRunEmitStage(documentName, document);
            EmitStageResult emitResult = emitOutcome.Result;
            AddDiagnostics(diagnostics, emitResult.Diagnostics);
            if (!emitOutcome.Succeeded)
            {
                return CreateResult(string.Empty, valid: false, succeeded: false, emitResult, null, null, total, diagnostics.ToImmutable());
            }

            string currentText = emitResult.Text;

            ct.ThrowIfCancellationRequested();

            FormatStageResult? formatResult = null;
            if (_config.EnableFormat && _formatter is not null)
            {
                StageOutcome<FormatStageResult> formatOutcome = await TryRunFormatStageAsync(documentName, currentText, ct).ConfigureAwait(false);
                formatResult = formatOutcome.Result;
                currentText = formatResult.Text;
                AddDiagnostics(diagnostics, formatResult.Diagnostics);
                if (!formatOutcome.Succeeded)
                {
                    return CreateResult(currentText, valid: false, succeeded: false, emitResult, formatResult, null, total, diagnostics.ToImmutable());
                }
            }

            ct.ThrowIfCancellationRequested();

            LintStageResult? lintResult = null;
            if (_config.EnableLint && _linter is not null)
            {
                lintResult = await TryRunLintStageAsync(documentName, currentText, ct).ConfigureAwait(false);
                AddDiagnostics(diagnostics, lintResult.Diagnostics);
            }

            ImmutableArray<PipelineDiagnostic> aggregatedDiagnostics = diagnostics.ToImmutable();
            bool valid = (lintResult?.Valid ?? true) && !ContainsError(aggregatedDiagnostics);

            return CreateResult(
                currentText,
                valid,
                succeeded: valid,
                emitResult,
                formatResult,
                lintResult,
                total,
                aggregatedDiagnostics);
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Emit failures are converted into per-document pipeline diagnostics.")]
        private StageOutcome<EmitStageResult> TryRunEmitStage(string documentName, QmlDocument document)
        {
            try
            {
                return StageOutcome<EmitStageResult>.Success(RunEmitStage(document));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                PipelineDiagnostic diagnostic = CreateExceptionDiagnostic("PIPELINE-EMIT", "emit", documentName, exception);
                return StageOutcome<EmitStageResult>.Failure(new EmitStageResult
                {
                    Text = string.Empty,
                    DurationMs = MinimumDurationMs,
                    Diagnostics = [diagnostic],
                });
            }
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Formatter failures are converted into per-document pipeline diagnostics.")]
        private async Task<StageOutcome<FormatStageResult>> TryRunFormatStageAsync(string documentName, string text, CancellationToken ct)
        {
            try
            {
                return StageOutcome<FormatStageResult>.Success(await RunFormatStageAsync(documentName, text, ct).ConfigureAwait(false));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                PipelineDiagnostic diagnostic = CreateExceptionDiagnostic("PIPELINE-FORMAT", "format", documentName, exception);
                return StageOutcome<FormatStageResult>.Failure(new FormatStageResult
                {
                    Text = text,
                    DurationMs = MinimumDurationMs,
                    Diagnostics = [diagnostic],
                });
            }
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Linter failures are converted into per-document pipeline diagnostics.")]
        private async Task<LintStageResult> TryRunLintStageAsync(string documentName, string text, CancellationToken ct)
        {
            try
            {
                return await RunLintStageAsync(documentName, text, ct).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                PipelineDiagnostic diagnostic = CreateExceptionDiagnostic("PIPELINE-LINT", "lint", documentName, exception);
                return new LintStageResult
                {
                    Valid = false,
                    DurationMs = MinimumDurationMs,
                    Diagnostics = [diagnostic],
                };
            }
        }

        private EmitStageResult RunEmitStage(QmlDocument document)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (_config.EnableSourceMap)
            {
                EmitResult result = _emitter.EmitWithSourceMap(document, _config.EmitOptions);
                return new EmitStageResult
                {
                    Text = result.Text,
                    SourceMap = result.SourceMap,
                    DurationMs = StopAndGetDurationMs(stopwatch),
                };
            }

            string text = _emitter.Emit(document, _config.EmitOptions);
            return new EmitStageResult
            {
                Text = text,
                DurationMs = StopAndGetDurationMs(stopwatch),
            };
        }

        private async Task<FormatStageResult> RunFormatStageAsync(string documentName, string text, CancellationToken ct)
        {
            Debug.Assert(_formatter is not null, "Formatter stage requires an injected formatter.");

            Stopwatch stopwatch = Stopwatch.StartNew();
            FormatStageResult result = await _formatter.FormatAsync(documentName, text, ct).ConfigureAwait(false);
            return result with
            {
                DurationMs = NormalizeDuration(result.DurationMs, stopwatch),
            };
        }

        private async Task<LintStageResult> RunLintStageAsync(string documentName, string text, CancellationToken ct)
        {
            Debug.Assert(_linter is not null, "Linter stage requires an injected linter.");

            Stopwatch stopwatch = Stopwatch.StartNew();
            LintStageResult result = await _linter.LintAsync(documentName, text, ct).ConfigureAwait(false);
            return result with
            {
                DurationMs = NormalizeDuration(result.DurationMs, stopwatch),
            };
        }

        private static PipelineResult CreateDocumentFailure(string documentName, string message)
        {
            PipelineDiagnostic diagnostic = new()
            {
                Code = "PIPELINE-DOCUMENT",
                Severity = PipelineDiagnosticSeverity.Error,
                Message = message,
                File = string.IsNullOrWhiteSpace(documentName) ? null : documentName,
            };
            EmitStageResult emitResult = new()
            {
                Text = string.Empty,
                DurationMs = MinimumDurationMs,
                Diagnostics = [diagnostic],
            };

            return new PipelineResult
            {
                Text = string.Empty,
                Valid = false,
                Succeeded = false,
                EmitResult = emitResult,
                TotalDurationMs = MinimumDurationMs,
                Diagnostics = [diagnostic],
            };
        }

        private static PipelineResult CreateResult(
            string text,
            bool valid,
            bool succeeded,
            EmitStageResult emitResult,
            FormatStageResult? formatResult,
            LintStageResult? lintResult,
            Stopwatch total,
            ImmutableArray<PipelineDiagnostic> diagnostics)
        {
            return new PipelineResult
            {
                Text = text,
                Valid = valid,
                Succeeded = succeeded,
                EmitResult = emitResult,
                FormatResult = formatResult,
                LintResult = lintResult,
                TotalDurationMs = StopAndGetDurationMs(total),
                Diagnostics = diagnostics,
            };
        }

        private static PipelineDiagnostic CreateExceptionDiagnostic(
            string code,
            string stage,
            string documentName,
            Exception exception)
        {
            return new PipelineDiagnostic
            {
                Code = code,
                Severity = PipelineDiagnosticSeverity.Error,
                Message = $"Pipeline {stage} stage failed: {exception.Message}",
                File = string.Equals(documentName, DefaultDocumentName, StringComparison.Ordinal) ? null : documentName,
            };
        }

        private static void AddDiagnostics(
            ImmutableArray<PipelineDiagnostic>.Builder builder,
            ImmutableArray<PipelineDiagnostic> diagnostics)
        {
            for (int index = 0; index < diagnostics.Length; index++)
            {
                builder.Add(diagnostics[index]);
            }
        }

        private static bool ContainsError(ImmutableArray<PipelineDiagnostic> diagnostics)
        {
            for (int index = 0; index < diagnostics.Length; index++)
            {
                if (diagnostics[index].Severity == PipelineDiagnosticSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetDocumentName(string? name)
        {
            return string.IsNullOrWhiteSpace(name) ? DefaultDocumentName : name;
        }

        private static double NormalizeDuration(double reportedDurationMs, Stopwatch stopwatch)
        {
            return reportedDurationMs > 0 ? reportedDurationMs : StopAndGetDurationMs(stopwatch);
        }

        private static double StopAndGetDurationMs(Stopwatch stopwatch)
        {
            stopwatch.Stop();
            return Math.Max(stopwatch.Elapsed.TotalMilliseconds, MinimumDurationMs);
        }

        private const double MinimumDurationMs = 0.0001;

        private sealed record StageOutcome<TStageResult>(TStageResult Result, bool Succeeded)
        {
            public static StageOutcome<TStageResult> Success(TStageResult result)
            {
                return new StageOutcome<TStageResult>(result, Succeeded: true);
            }

            public static StageOutcome<TStageResult> Failure(TStageResult result)
            {
                return new StageOutcome<TStageResult>(result, Succeeded: false);
            }
        }
    }
}
