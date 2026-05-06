namespace QmlSharp.DevTools.Tests.Infrastructure
{
    public sealed class ConsoleCapture : IConsoleWriter
    {
        private readonly StringWriter writer = new();

        public void Write(string text)
        {
            writer.Write(text);
        }

        public void WriteLine(string text)
        {
            writer.WriteLine(text);
        }

        public string GetOutput()
        {
            return writer.ToString();
        }
    }
}
