namespace QmlSharp.Build.Tests.Infrastructure
{
    public static class GoldenFileHelper
    {
        public static string GoldenRoot =>
            System.IO.Path.Combine(AppContext.BaseDirectory, "testdata", "golden");

        public static string GetPath(string relativePath)
        {
            return System.IO.Path.Combine(GoldenRoot, relativePath);
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
    }
}
