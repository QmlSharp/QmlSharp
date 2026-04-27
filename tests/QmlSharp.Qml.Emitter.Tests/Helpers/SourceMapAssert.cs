namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    internal static class SourceMapAssert
    {
        public static void ValidSpan(OutputSpan span)
        {
            Assert.True(span.StartLine >= 1);
            Assert.True(span.StartColumn >= 1);
            Assert.True(span.EndLine >= span.StartLine);
            Assert.True(span.EndColumn >= 1);
        }
    }
}
