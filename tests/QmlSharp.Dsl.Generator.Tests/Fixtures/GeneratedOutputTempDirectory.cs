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
            return CreateUnder(System.IO.Path.GetTempPath());
        }

        public static GeneratedOutputTempDirectory CreateUnder(string rootDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

            string path = System.IO.Path.Join(
                System.IO.Path.GetFullPath(rootDirectory),
                $"qmlsharp-dsl-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(path);

            return new GeneratedOutputTempDirectory(path);
        }

        public void Dispose()
        {
            if (string.Equals(
                    Environment.GetEnvironmentVariable("QMLSHARP_PRESERVE_DSL_COMPILE_OUTPUT"),
                    "1",
                    StringComparison.Ordinal))
            {
                return;
            }

            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
