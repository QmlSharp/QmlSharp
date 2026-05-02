namespace QmlSharp.Compiler.Tests.Fixtures
{
    internal sealed class TempOutputDirectory : IDisposable
    {
        public TempOutputDirectory()
        {
            Path = System.IO.Path.Join(System.IO.Path.GetTempPath(), "qmlsharp-compiler-tests", Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
