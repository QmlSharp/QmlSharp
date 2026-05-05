using System.Diagnostics;
using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class InitCommandTests
    {
        [Fact]
        public async Task IN01_InitWithDefaultTemplate_CreatesApplicationProject()
        {
            using TempDirectory root = new("qmlsharp-init-in01");
            string targetDirectory = Path.Join(root.Path, "MyApp");
            InitService service = CreateService();

            CommandServiceResult result = await service.InitAsync(new InitCommandOptions
            {
                TargetDir = targetDirectory,
            });

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Join(targetDirectory, "qmlsharp.json")));
            Assert.True(File.Exists(Path.Join(targetDirectory, "MyApp.csproj")));
            Assert.True(File.Exists(Path.Join(targetDirectory, "src", "Program.cs")));
            Assert.True(File.Exists(Path.Join(targetDirectory, "src", "AppViewModel.cs")));
            Assert.True(File.Exists(Path.Join(targetDirectory, "src", "AppView.cs")));
        }

        [Fact]
        public async Task IN02_InitWithCounterTemplate_CreatesCounterSourceFiles()
        {
            using TempDirectory root = new("qmlsharp-init-in02");
            string targetDirectory = Path.Join(root.Path, "Counter App");
            InitService service = CreateService();

            CommandServiceResult result = await service.InitAsync(new InitCommandOptions
            {
                Template = "counter",
                TargetDir = targetDirectory,
            });

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Join(targetDirectory, "CounterApp.csproj")));
            Assert.True(File.Exists(Path.Join(targetDirectory, "src", "CounterViewModel.cs")));
            Assert.True(File.Exists(Path.Join(targetDirectory, "src", "CounterView.cs")));
            string viewModel = File.ReadAllText(Path.Join(targetDirectory, "src", "CounterViewModel.cs"));
            Assert.Contains("[State]", viewModel, StringComparison.Ordinal);
            Assert.Contains("[Command]", viewModel, StringComparison.Ordinal);
            Assert.Contains("Count++;", viewModel, StringComparison.Ordinal);
        }

        [Fact]
        public async Task IN03_InitInNonEmptyDirectory_ReturnsCommandError()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("in03-init");
            File.WriteAllText(Path.Join(project.Path, "existing.txt"), "already here");
            InitService service = CreateService();

            CommandServiceResult result = await service.InitAsync(new InitCommandOptions
            {
                TargetDir = project.Path,
            });

            Assert.False(result.Success);
            Assert.Equal(CommandResultStatus.ConfigOrCommandError, result.Status);
            Assert.True(File.Exists(Path.Join(project.Path, "existing.txt")));
            Assert.False(File.Exists(Path.Join(project.Path, "qmlsharp.json")));
        }

        [Fact]
        public async Task IN04_InitCreatesValidQmlsharpJson_ConfigLoaderAcceptsGeneratedConfig()
        {
            using TempDirectory root = new("qmlsharp-init-in04");
            string targetDirectory = Path.Join(root.Path, "ConfigApp");
            InitService service = CreateService();

            CommandServiceResult result = await service.InitAsync(new InitCommandOptions
            {
                TargetDir = targetDirectory,
            });
            QmlSharpConfig config = new ConfigLoader().Load(targetDirectory);
            ImmutableArray<ConfigDiagnostic> diagnostics = new ConfigLoader().Validate(config);

            Assert.True(result.Success);
            Assert.Empty(diagnostics);
            Assert.Equal(Path.Join(targetDirectory, "src", "Program.cs"), config.Entry);
            Assert.Equal(Path.Join(targetDirectory, "dist"), config.OutDir);
            Assert.Equal("QmlSharp.ConfigApp", config.Module.Prefix);
        }

        [Fact]
        public async Task IN05_InitCreatesValidCsproj_DotnetRestoreSucceeds()
        {
            using TempDirectory root = new("qmlsharp-init-in05");
            string targetDirectory = Path.Join(root.Path, "RestoreApp");
            InitService service = CreateService();

            CommandServiceResult result = await service.InitAsync(new InitCommandOptions
            {
                TargetDir = targetDirectory,
            });
            string projectPath = Path.Join(targetDirectory, "RestoreApp.csproj");
            ProcessResult restore = await RunDotnetRestoreAsync(projectPath);

            Assert.True(result.Success);
            Assert.Equal(0, restore.ExitCode);
        }

        [Fact]
        public async Task InitWithLibraryTemplate_CreatesLibraryProjectWithoutProgramEntry()
        {
            using TempDirectory root = new("qmlsharp-init-library");
            string targetDirectory = Path.Join(root.Path, "SharedControls");
            InitService service = CreateService();

            CommandServiceResult result = await service.InitAsync(new InitCommandOptions
            {
                Template = "library",
                TargetDir = targetDirectory,
            });

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Join(targetDirectory, "SharedControls.csproj")));
            Assert.True(File.Exists(Path.Join(targetDirectory, "src", "LibraryViewModel.cs")));
            Assert.True(File.Exists(Path.Join(targetDirectory, "src", "LibraryView.cs")));
            Assert.False(File.Exists(Path.Join(targetDirectory, "src", "Program.cs")));
            string configJson = File.ReadAllText(Path.Join(targetDirectory, "qmlsharp.json"));
            Assert.DoesNotContain("\"entry\"", configJson, StringComparison.Ordinal);
            Assert.Contains("\"outDir\": \"./lib\"", configJson, StringComparison.Ordinal);
        }

        [Fact]
        public async Task InitWithTargetDirectoryContainingSpaces_CreatesRestorableProject()
        {
            using TempDirectory root = new("qmlsharp-init-spaces");
            string targetDirectory = Path.Join(root.Path, "Path With Spaces");
            InitService service = CreateService();

            CommandServiceResult result = await service.InitAsync(new InitCommandOptions
            {
                TargetDir = targetDirectory,
            });

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Join(targetDirectory, "PathWithSpaces.csproj")));
            Assert.True(File.Exists(Path.Join(targetDirectory, "src", "AppView.cs")));
        }

        [Fact]
        public async Task InitWithUnknownTemplate_ReturnsCommandError()
        {
            using TempDirectory root = new("qmlsharp-init-unknown");
            InitService service = CreateService();

            CommandServiceResult result = await service.InitAsync(new InitCommandOptions
            {
                Template = "unknown",
                TargetDir = Path.Join(root.Path, "UnknownTemplate"),
            });

            Assert.False(result.Success);
            Assert.Equal(CommandResultStatus.ConfigOrCommandError, result.Status);
            Assert.Contains("Unknown QmlSharp template", result.Message, StringComparison.Ordinal);
        }

        private static InitService CreateService()
        {
            string repositoryRoot = BuildTestFixtures.FindRepositoryRoot();
            return new InitService(Path.Join(repositoryRoot, "templates"), repositoryRoot);
        }

        private static async Task<ProcessResult> RunDotnetRestoreAsync(string projectPath)
        {
            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            process.StartInfo.ArgumentList.Add("restore");
            process.StartInfo.ArgumentList.Add(projectPath);
            process.StartInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
            process.StartInfo.Environment["QT_DIR"] = Environment.GetEnvironmentVariable("QT_DIR") ?? "C:\\Qt\\6.11.0\\msvc2022_64";

            _ = process.Start();
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            Task waitTask = process.WaitForExitAsync();
            Task completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromMinutes(2)));
            if (completed != waitTask)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("dotnet restore for generated QmlSharp project timed out.");
            }

            return new ProcessResult(process.ExitCode, await stdout, await stderr);
        }

        private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
    }
}
