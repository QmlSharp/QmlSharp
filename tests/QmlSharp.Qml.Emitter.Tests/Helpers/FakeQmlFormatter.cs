namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    internal sealed class FakeQmlFormatter : IEmitPipelineFormatter
    {
        private readonly Func<string, string> _format;
        private readonly ImmutableArray<PipelineDiagnostic> _diagnostics;
        private readonly Exception? _exception;

        public FakeQmlFormatter()
            : this(static text => text)
        {
        }

        public FakeQmlFormatter(Func<string, string> format)
        {
            _format = format;
            _diagnostics = ImmutableArray<PipelineDiagnostic>.Empty;
        }

        public FakeQmlFormatter(Exception exception)
        {
            _format = static text => text;
            _diagnostics = ImmutableArray<PipelineDiagnostic>.Empty;
            _exception = exception;
        }

        public FakeQmlFormatter(Func<string, string> format, ImmutableArray<PipelineDiagnostic> diagnostics)
        {
            _format = format;
            _diagnostics = diagnostics;
        }

        public FormatStageResult Format(string text)
        {
            return new FormatStageResult
            {
                Text = _format(text),
                DurationMs = 0.1,
                Diagnostics = _diagnostics,
            };
        }

        public Task<FormatStageResult> FormatAsync(string documentName, string text, CancellationToken ct = default)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Format(text));
        }
    }
}
