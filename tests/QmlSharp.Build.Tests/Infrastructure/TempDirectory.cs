namespace QmlSharp.Build.Tests.Infrastructure
{
    public sealed class TempDirectory : IDisposable
    {
        private bool disposed;

        public TempDirectory(string prefix)
        {
            string sanitizedPrefix = string.IsNullOrWhiteSpace(prefix) ? "qmlsharp-build" : prefix;
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{sanitizedPrefix}-{Guid.NewGuid():N}");
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
    }
}
