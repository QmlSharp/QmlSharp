namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    internal static class LineEndingAssert
    {
        public static void ContainsOnlyLf(string text)
        {
            Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        }

        public static void ContainsOnlyCrLf(string text)
        {
            string withoutCrLf = text.Replace("\r\n", string.Empty, StringComparison.Ordinal);

            Assert.DoesNotContain('\n', withoutCrLf);
        }
    }
}
