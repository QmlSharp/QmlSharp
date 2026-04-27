namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    internal static class GoldenFileLoader
    {
        public static string Load(string relativePath)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Golden", relativePath);

            return File.ReadAllText(path);
        }
    }
}
