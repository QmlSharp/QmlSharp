namespace QmlSharp.Build.Tests.Infrastructure
{
    public sealed class TempDirectory : IDisposable
    {
        private bool disposed;

        public TempDirectory(string prefix)
        {
            string sanitizedPrefix = SanitizePrefix(prefix);
            Path = System.IO.Path.Join(System.IO.Path.GetTempPath(), $"{sanitizedPrefix}-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }

        private static string SanitizePrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return "qmlsharp-build";
            }

            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            char[] chars = prefix
                .Select(character => invalidChars.Contains(character) || character is '/' or '\\' ? '-' : character)
                .ToArray();

            string sanitized = new(chars);
            return string.IsNullOrWhiteSpace(sanitized) ? "qmlsharp-build" : sanitized;
        }
    }
}
