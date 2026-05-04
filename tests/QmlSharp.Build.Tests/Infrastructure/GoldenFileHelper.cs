namespace QmlSharp.Build.Tests.Infrastructure
{
    public static class GoldenFileHelper
    {
        public static string GoldenRoot =>
            System.IO.Path.Join(AppContext.BaseDirectory, "testdata", "golden");

        public static string GetPath(string relativePath)
        {
            ValidateRelativePath(relativePath, nameof(relativePath));
            return System.IO.Path.Join(GoldenRoot, relativePath);
        }

        public static string ReadRequiredText(string relativePath)
        {
            string path = GetPath(relativePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Golden file was not found.", path);
            }

            return File.ReadAllText(path);
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
