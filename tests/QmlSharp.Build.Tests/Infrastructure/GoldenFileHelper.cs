namespace QmlSharp.Build.Tests.Infrastructure
{
    public static class GoldenFileHelper
    {
        public const string UpdateWorkflow =
            "$env:MSBUILDDISABLENODEREUSE = \"1\"; $env:QMLSHARP_UPDATE_GOLDENS = \"1\"; dotnet test QmlSharp.slnx --configuration Debug --no-build --filter \"Golden\" --verbosity normal -m:1 -- UpdateGoldens=true";

        public static string GoldenRoot =>
            System.IO.Path.Join(AppContext.BaseDirectory, "testdata", "golden");

        public static string GetPath(string relativePath)
        {
            ValidateRelativePath(relativePath, nameof(relativePath));
            return System.IO.Path.Join(GoldenRoot, relativePath);
        }

        public static string ReadRequiredText(string relativePath)
        {
            string sourcePath = GetSourcePath(relativePath);
            if (File.Exists(sourcePath))
            {
                return File.ReadAllText(sourcePath);
            }

            string path = GetPath(relativePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Golden file was not found.", path);
            }

            return File.ReadAllText(path);
        }

        public static void AssertMatchesOrUpdate(string relativePath, string actual)
        {
            if (ShouldUpdateGoldens())
            {
                string sourcePath = GetSourcePath(relativePath);
                string? directory = System.IO.Path.GetDirectoryName(sourcePath);
                if (directory is not null)
                {
                    _ = Directory.CreateDirectory(directory);
                }

                File.WriteAllText(sourcePath, actual);
                return;
            }

            string expected = ReadRequiredText(relativePath);
            Assert.Equal(expected, actual);
        }

        private static string GetSourcePath(string relativePath)
        {
            ValidateRelativePath(relativePath, nameof(relativePath));
            return System.IO.Path.Join(
                BuildTestFixtures.FindRepositoryRoot(),
                "tests",
                "QmlSharp.Build.Tests",
                "testdata",
                "golden",
                relativePath);
        }

        private static bool ShouldUpdateGoldens()
        {
            return IsTruthy(Environment.GetEnvironmentVariable("QMLSHARP_UPDATE_GOLDENS")) ||
                IsTruthy(Environment.GetEnvironmentVariable("UpdateGoldens")) ||
                IsTruthy(AppContext.GetData("UpdateGoldens")?.ToString());
        }

        private static bool IsTruthy(string? value)
        {
            return string.Equals(value, "1", StringComparison.Ordinal) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateRelativePath(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Path must not be empty.", paramName);
            }

            if (System.IO.Path.IsPathRooted(value) || ContainsParentTraversal(value))
            {
                throw new ArgumentException("Path must be relative.", paramName);
            }
        }

        private static bool ContainsParentTraversal(string value)
        {
            string[] parts = value.Split(
                [System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            return parts.Any(static part => part == "..");
        }
    }
}
