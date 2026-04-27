namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    internal static class GoldenFileLoader
    {
        public static string Load(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Path must be a non-empty relative path.", nameof(relativePath));
            }

            if (Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException("Path must be relative.", nameof(relativePath));
            }

            string path = Path.Join(AppContext.BaseDirectory, "Fixtures", "Golden", relativePath);

            return File.ReadAllText(path);
        }
    }
}
