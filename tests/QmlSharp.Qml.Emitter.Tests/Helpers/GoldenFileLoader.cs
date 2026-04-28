namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    internal static class GoldenFileLoader
    {
        public static string Load(string relativePath)
        {
            string path = ResolvePath(relativePath);

            return File.ReadAllText(path);
        }

        public static byte[] LoadBytes(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Path must be a non-empty relative path.", nameof(relativePath));
            }

            if (Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException("Path must be relative.", nameof(relativePath));
            }

            string path = ResolvePath(relativePath);

            return File.ReadAllBytes(path);
        }

        private static string ResolvePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Path must be a non-empty relative path.", nameof(relativePath));
            }

            if (Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException("Path must be relative.", nameof(relativePath));
            }

            return Path.Join(AppContext.BaseDirectory, "Fixtures", "Golden", relativePath);
        }
    }
}
