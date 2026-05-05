using System.Security;
using System.Text.Json;

namespace QmlSharp.Integration.Tests.Fixtures
{
    internal sealed class BuildFixtureProject : IDisposable
    {
        private BuildFixtureProject(
            string fixtureName,
            string sourceDirectory,
            string projectDirectory,
            string repositoryRoot,
            string qtDir)
        {
            FixtureName = fixtureName;
            SourceDirectory = sourceDirectory;
            ProjectDirectory = projectDirectory;
            RepositoryRoot = repositoryRoot;
            QtDir = qtDir;
        }

        public string FixtureName { get; }

        public string SourceDirectory { get; }

        public string ProjectDirectory { get; }

        public string RepositoryRoot { get; }

        public string QtDir { get; }

        public string OutputDirectory => Path.Join(ProjectDirectory, "dist");

        public string LibraryDirectory => Path.Join(ProjectDirectory, "lib");

        public static BuildFixtureProject Copy(string fixtureName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fixtureName);

            string safeFixtureName = NormalizeFixtureName(fixtureName);
            string repositoryRoot = FindRepositoryRoot();
            string sourceDirectory = Path.Join(repositoryRoot, "tests", "fixtures", "projects", safeFixtureName);
            if (!Directory.Exists(sourceDirectory))
            {
                throw new DirectoryNotFoundException($"Fixture project '{safeFixtureName}' was not found at '{sourceDirectory}'.");
            }

            string qtDir = RequireQtDir();
            string tempRoot = Path.Join(
                Path.GetTempPath(),
                "qmlsharp-step08-14",
                safeFixtureName + "-" + Guid.NewGuid().ToString("N"));
            CopyDirectory(sourceDirectory, tempRoot);
            ReplaceQtDirToken(tempRoot, qtDir);
            WriteRepositoryRootProps(tempRoot, repositoryRoot);

            return new BuildFixtureProject(safeFixtureName, sourceDirectory, tempRoot, repositoryRoot, qtDir);
        }

        public void Dispose()
        {
            if (!Directory.Exists(ProjectDirectory))
            {
                return;
            }

            string tempRoot = Path.GetFullPath(Path.Join(Path.GetTempPath(), "qmlsharp-step08-14"));
            string projectRoot = Path.GetFullPath(ProjectDirectory);
            string rootPrefix = tempRoot.EndsWith(Path.DirectorySeparatorChar)
                ? tempRoot
                : tempRoot + Path.DirectorySeparatorChar;
            if (!projectRoot.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to delete a fixture directory outside the test temp root.");
            }

            Directory.Delete(projectRoot, recursive: true);
        }

        private static string NormalizeFixtureName(string fixtureName)
        {
            string safeFixtureName = Path.GetFileName(fixtureName);
            if (string.IsNullOrWhiteSpace(safeFixtureName) ||
                Path.IsPathRooted(fixtureName) ||
                !string.Equals(fixtureName, safeFixtureName, StringComparison.Ordinal) ||
                safeFixtureName.Contains('/') ||
                safeFixtureName.Contains('\\'))
            {
                throw new ArgumentException("Fixture name must be a simple directory name.", nameof(fixtureName));
            }

            return safeFixtureName;
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            _ = Directory.CreateDirectory(destinationDirectory);
            foreach (string directory in Directory
                .EnumerateDirectories(sourceDirectory)
                .OrderBy(static path => path, StringComparer.Ordinal))
            {
                CopyDirectory(directory, Path.Join(destinationDirectory, Path.GetFileName(directory)));
            }

            foreach (string file in Directory
                .EnumerateFiles(sourceDirectory)
                .OrderBy(static path => path, StringComparer.Ordinal))
            {
                File.Copy(file, Path.Join(destinationDirectory, Path.GetFileName(file)), overwrite: true);
            }
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? current = new(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Join(current.FullName, "QmlSharp.slnx")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root containing QmlSharp.slnx.");
        }

        private static string RequireQtDir()
        {
            string? qtDir = Environment.GetEnvironmentVariable("QT_DIR");
            if (string.IsNullOrWhiteSpace(qtDir))
            {
                throw new InvalidOperationException("QT_DIR must be set for Step 08.14 build-system integration fixtures.");
            }

            string fullQtDir = Path.GetFullPath(qtDir);
            if (!Directory.Exists(fullQtDir))
            {
                throw new DirectoryNotFoundException($"QT_DIR does not exist: '{fullQtDir}'.");
            }

            return fullQtDir;
        }

        private static void ReplaceQtDirToken(string projectDirectory, string qtDir)
        {
            string escapedQtDir = JsonSerializer.Serialize(qtDir).Trim('"');
            foreach (string filePath in Directory.EnumerateFiles(projectDirectory, "*.json", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(filePath);
                if (text.Contains("__QT_DIR__", StringComparison.Ordinal))
                {
                    File.WriteAllText(filePath, text.Replace("__QT_DIR__", escapedQtDir, StringComparison.Ordinal));
                }
            }
        }

        private static void WriteRepositoryRootProps(string projectDirectory, string repositoryRoot)
        {
            string escapedRoot = SecurityElement.Escape(repositoryRoot) ??
                throw new InvalidOperationException("Repository root could not be escaped for MSBuild props.");
            File.WriteAllText(
                Path.Join(projectDirectory, "Directory.Build.props"),
                $"""
                <Project>
                  <PropertyGroup>
                    <QmlSharpRepoRoot>{escapedRoot}</QmlSharpRepoRoot>
                  </PropertyGroup>
                </Project>
                """);
        }
    }
}
