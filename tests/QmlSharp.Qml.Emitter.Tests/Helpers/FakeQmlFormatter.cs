namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    internal sealed class FakeQmlFormatter
    {
        public FormatStageResult Format(string text)
        {
            return new FormatStageResult
            {
                Text = text,
                DurationMs = 0.1,
            };
        }
    }
}
