namespace QmlSharp.Dsl.Generator.Tests.Fixtures
{
    internal sealed class GeneratedOutputTempDirectory : IDisposable
    {
        private GeneratedOutputTempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static GeneratedOutputTempDirectory Create()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"qmlsharp-dsl-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(path);

            return new GeneratedOutputTempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
