namespace QmlSharp.Registry.Tests.Helpers
{
    internal sealed class ScannerTestWorkspace : IDisposable
    {
        private readonly string rootDirectory;

        public ScannerTestWorkspace()
        {
            rootDirectory = Path.Combine(
                AppContext.BaseDirectory,
                "scanner-workspaces",
                Guid.NewGuid().ToString("N"));

            _ = Directory.CreateDirectory(rootDirectory);
        }

        public string RootDirectory => rootDirectory;

        public string CreateFile(string relativePath, string? contents = null)
        {
            string fullPath = GetPath(relativePath);
            string? directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, contents ?? string.Empty);
            return fullPath;
        }

        public string CreateDirectory(string relativePath)
        {
            string fullPath = GetPath(relativePath);
            _ = Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        public string GetPath(string relativePath)
        {
            string normalizedRelativePath = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            return Path.Combine(rootDirectory, normalizedRelativePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }
}
