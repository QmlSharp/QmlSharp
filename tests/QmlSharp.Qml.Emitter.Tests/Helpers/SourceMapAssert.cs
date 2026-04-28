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

        public static string Slice(string text, OutputSpan span)
        {
            int start = OffsetOf(text, span.StartLine, span.StartColumn);
            int end = OffsetOf(text, span.EndLine, span.EndColumn) + 1;

            return text[start..end];
        }

        private static int OffsetOf(string text, int line, int column)
        {
            int currentLine = 1;
            int currentColumn = 1;

            for (int index = 0; index < text.Length; index++)
            {
                if (currentLine == line && currentColumn == column)
                {
                    return index;
                }

                char character = text[index];
                if (character == '\r')
                {
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }

                    currentLine++;
                    currentColumn = 1;
                    continue;
                }

                if (character == '\n')
                {
                    currentLine++;
                    currentColumn = 1;
                    continue;
                }

                currentColumn++;
            }

            if (currentLine == line && currentColumn == column)
            {
                return text.Length;
            }

            throw new ArgumentOutOfRangeException(nameof(line), line, "Position is outside the supplied text.");
        }
    }
}
