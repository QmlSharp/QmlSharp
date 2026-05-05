using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class CleanCommandTests
    {
        [Fact]
        public async Task CN01_CleanRemovesDistDirectory()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cn01-clean");
            _ = WriteConfig(project.Path, "./dist");
            string distDirectory = Path.Join(project.Path, "dist");
            _ = Directory.CreateDirectory(distDirectory);
            File.WriteAllText(Path.Join(distDirectory, "manifest.json"), "{}");
            CleanService service = new();

            CommandServiceResult result = await service.CleanAsync(new CleanCommandOptions
            {
                ProjectDir = project.Path,
            });

            Assert.True(result.Success);
            Assert.False(Directory.Exists(distDirectory));
        }

        [Fact]
        public async Task CN02_CleanWithCacheFlag_RemovesDistAndCompilerCache()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cn02-clean");
            _ = WriteConfig(project.Path, "./dist");
            string distDirectory = Path.Join(project.Path, "dist");
            string cacheDirectory = Path.Join(project.Path, ".compiler-cache");
            _ = Directory.CreateDirectory(distDirectory);
            _ = Directory.CreateDirectory(cacheDirectory);
            File.WriteAllText(Path.Join(distDirectory, "manifest.json"), "{}");
            File.WriteAllText(Path.Join(cacheDirectory, "cache.json"), "{}");
            CleanService service = new();

            CommandServiceResult result = await service.CleanAsync(new CleanCommandOptions
            {
                Cache = true,
                ProjectDir = project.Path,
            });

            Assert.True(result.Success);
            Assert.False(Directory.Exists(distDirectory));
            Assert.False(Directory.Exists(cacheDirectory));
        }

        [Fact]
        public async Task CN03_CleanWhenDistDoesNotExist_ReturnsSuccess()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cn03-clean");
            _ = WriteConfig(project.Path, "./dist");
            CleanService service = new();

            CommandServiceResult result = await service.CleanAsync(new CleanCommandOptions
            {
                ProjectDir = project.Path,
            });

            Assert.True(result.Success);
            Assert.Equal(CommandResultStatus.Success, result.Status);
            Assert.Contains("No build artifacts", result.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CN04_CleanPreservesSourceFilesAndConfig()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cn04-clean");
            string configPath = WriteConfig(project.Path, "./dist");
            string sourcePath = Path.Join(project.Path, "src", "CounterViewModel.cs");
            File.WriteAllText(sourcePath, "source");
            string distDirectory = Path.Join(project.Path, "dist");
            _ = Directory.CreateDirectory(distDirectory);
            File.WriteAllText(Path.Join(distDirectory, "manifest.json"), "{}");
            CleanService service = new();

            CommandServiceResult result = await service.CleanAsync(new CleanCommandOptions
            {
                Cache = true,
                ProjectDir = project.Path,
            });

            Assert.True(result.Success);
            Assert.True(File.Exists(configPath));
            Assert.True(File.Exists(sourcePath));
            Assert.True(Directory.Exists(Path.Join(project.Path, "src")));
        }

        [Fact]
        public async Task CleanWithProjectPathContainingSpaces_RemovesConfiguredOutput()
        {
            using TempDirectory root = new("qmlsharp-clean-spaces");
            string projectDirectory = Path.Join(root.Path, "Project With Spaces");
            _ = Directory.CreateDirectory(Path.Join(projectDirectory, "src"));
            _ = WriteConfig(projectDirectory, "./dist with spaces");
            string distDirectory = Path.Join(projectDirectory, "dist with spaces");
            _ = Directory.CreateDirectory(distDirectory);
            File.WriteAllText(Path.Join(distDirectory, "manifest.json"), "{}");
            CleanService service = new();

            CommandServiceResult result = await service.CleanAsync(new CleanCommandOptions
            {
                ProjectDir = projectDirectory,
            });

            Assert.True(result.Success);
            Assert.False(Directory.Exists(distDirectory));
        }

        [Fact]
        public async Task CleanRefusesToDeleteOutDirOutsideProjectRoot()
        {
            using TempDirectory root = new("qmlsharp-clean-outside");
            string projectDirectory = Path.Join(root.Path, "project");
            string outsideDirectory = Path.Join(root.Path, "outside-dist");
            _ = Directory.CreateDirectory(projectDirectory);
            _ = Directory.CreateDirectory(outsideDirectory);
            File.WriteAllText(Path.Join(outsideDirectory, "manifest.json"), "{}");
            _ = WriteConfig(projectDirectory, "../outside-dist");
            CleanService service = new();

            CommandServiceResult result = await service.CleanAsync(new CleanCommandOptions
            {
                ProjectDir = projectDirectory,
            });

            Assert.False(result.Success);
            Assert.Equal(CommandResultStatus.BuildError, result.Status);
            Assert.True(Directory.Exists(outsideDirectory));
            Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == BuildDiagnosticCode.OutputValidationFailed);
        }

        [Fact]
        public async Task CleanRefusesToDeleteProjectRootWhenOutDirIsDot()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("clean-root");
            _ = WriteConfig(project.Path, ".");
            CleanService service = new();

            CommandServiceResult result = await service.CleanAsync(new CleanCommandOptions
            {
                ProjectDir = project.Path,
            });

            Assert.False(result.Success);
            Assert.True(Directory.Exists(project.Path));
            Assert.True(File.Exists(Path.Join(project.Path, "qmlsharp.json")));
        }

        [Fact]
        public void CleanPathContainmentUsesCaseSensitiveComparisonWhenRequested()
        {
            string root = Path.Join(Path.GetTempPath(), "qmlsharp-clean-case", "project");
            string target = Path.Join(Path.GetTempPath(), "qmlsharp-clean-case", "Project", "dist");

            Assert.False(CleanService.IsDirectoryBelowProjectRoot(
                root,
                target,
                StringComparison.Ordinal));
        }

        private static string WriteConfig(string projectDirectory, string outDir)
        {
            _ = Directory.CreateDirectory(projectDirectory);
            string configPath = Path.Join(projectDirectory, "qmlsharp.json");
            File.WriteAllText(
                configPath,
                $$"""
                {
                  "outDir": "{{ToJsonPath(outDir)}}",
                  "qt": { "dir": null },
                  "module": { "prefix": "QmlSharp.CleanTests" }
                }
                """);
            return configPath;
        }

        private static string ToJsonPath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
