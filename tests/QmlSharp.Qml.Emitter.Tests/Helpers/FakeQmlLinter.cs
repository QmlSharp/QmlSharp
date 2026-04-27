namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    internal sealed class FakeQmlLinter
    {
        public LintStageResult LintValid()
        {
            return new LintStageResult
            {
                Valid = true,
                DurationMs = 0.1,
            };
        }

        public LintStageResult LintInvalid(string message)
        {
            return new LintStageResult
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
                DurationMs = 0.1,
            };
        }
    }
}
