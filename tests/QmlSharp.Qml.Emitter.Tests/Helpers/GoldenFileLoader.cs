namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    internal static class GoldenFileLoader
    {
        public static string SourceGoldenDirectory { get; } = Path.Join(
            FindRepositoryRoot(),
            "tests",
            "QmlSharp.Qml.Emitter.Tests",
            "Fixtures",
            "Golden");

        public static string Load(string relativePath)
        {
            string path = ResolveSourcePath(relativePath);

            return File.ReadAllText(path);
        }

        public static byte[] LoadBytes(string relativePath)
        {
            string path = ResolveSourcePath(relativePath);

            return File.ReadAllBytes(path);
        }

        private static string ResolveSourcePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Path must be a non-empty relative path.", nameof(relativePath));
            }

            if (Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException("Path must be relative.", nameof(relativePath));
            }

            string sourceDirectory = Path.GetFullPath(SourceGoldenDirectory);
            string sourcePath = Path.GetFullPath(Path.Join(sourceDirectory, relativePath));
            string sourceDirectoryPrefix = sourceDirectory + Path.DirectorySeparatorChar;

            if (!sourcePath.StartsWith(sourceDirectoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Path must stay inside the golden fixture directory.", nameof(relativePath));
            }

            return sourcePath;
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                string solutionPath = Path.Join(directory.FullName, "QmlSharp.slnx");
                if (File.Exists(solutionPath))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find the QmlSharp repository root.");
        }
    }
}
