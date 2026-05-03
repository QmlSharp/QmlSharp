namespace QmlSharp.Host.Tests.ArtifactValidation
{
    internal sealed class ArtifactFixture : IDisposable
    {
        private ArtifactFixture(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static ArtifactFixture Create()
        {
            string repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
            string source = System.IO.Path.Join(repositoryRoot, "tests", "fixtures", "native-host", "artifacts", "valid-dist");
            string destination = System.IO.Path.Join(System.IO.Path.GetTempPath(), "qmlsharp-artifacts-" + Guid.NewGuid().ToString("N"));
            CopyDirectory(source, destination);
            return new ArtifactFixture(destination);
        }

        public string InDist(params string[] relativeSegments)
        {
            return System.IO.Path.Join([Path, .. relativeSegments]);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            _ = Directory.CreateDirectory(destinationDirectory);
            foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativeDirectory = System.IO.Path.GetRelativePath(sourceDirectory, directory);
                _ = Directory.CreateDirectory(System.IO.Path.Join(destinationDirectory, relativeDirectory));
            }

            foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativeFile = System.IO.Path.GetRelativePath(sourceDirectory, file);
                string destinationFile = System.IO.Path.Join(destinationDirectory, relativeFile);
                File.Copy(file, destinationFile);
            }
        }

        private static string FindRepositoryRoot(string startDirectory)
        {
            DirectoryInfo? current = new(startDirectory);
            while (current is not null)
            {
                if (File.Exists(System.IO.Path.Join(current.FullName, "QmlSharp.slnx")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the QmlSharp repository root.");
        }
    }
}
