namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    internal sealed class FakeQmlLinter : IEmitPipelineLinter
    {
        private readonly LintStageResult _result;
        private readonly Exception? _exception;

        public FakeQmlLinter()
            : this(valid: true)
        {
        }

        public FakeQmlLinter(bool valid, ImmutableArray<PipelineDiagnostic> diagnostics = default)
        {
            _result = new LintStageResult
            {
                Valid = valid,
                Diagnostics = diagnostics.IsDefault ? ImmutableArray<PipelineDiagnostic>.Empty : diagnostics,
                DurationMs = 0.1,
            };
        }

        public FakeQmlLinter(Exception exception)
        {
            _result = new LintStageResult
            {
                Valid = false,
                DurationMs = 0.1,
            };
            _exception = exception;
        }

        public LintStageResult LintValid()
        {
            return _result with
            {
                Valid = true,
            };
        }

        public LintStageResult LintInvalid(string message)
        {
            return _result with
            {
                Valid = false,
                Diagnostics =
                [
                    new PipelineDiagnostic
                    {
                        Severity = PipelineDiagnosticSeverity.Error,
                        Message = message,
                    },
                ],
            };
        }

        public Task<LintStageResult> LintAsync(string documentName, string text, CancellationToken ct = default)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            ct.ThrowIfCancellationRequested();
            return Task.FromResult(_result);
        }
    }
}
