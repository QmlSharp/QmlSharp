namespace QmlSharp.DevTools.Tests
{
    public sealed class DependencyDirectionTests
    {
        [Fact]
        public void LowerModules_DoNotReferenceDevTools()
        {
            string repositoryRoot = DevToolsTestFixtures.FindRepositoryRoot();
            string sourceRoot = Path.Join(repositoryRoot, "src");
            IEnumerable<string> projectFiles = Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(static path => IsLowerModuleProject(path));

            foreach (string projectText in projectFiles.Select(File.ReadAllText))
            {
                Assert.DoesNotContain("QmlSharp.DevTools", projectText, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void CliProject_ReferencesDevToolsForDevCommandOnly()
        {
            string repositoryRoot = DevToolsTestFixtures.FindRepositoryRoot();
            string cliProject = Path.Join(repositoryRoot, "src", "QmlSharp.Cli", "QmlSharp.Cli.csproj");
            string projectText = File.ReadAllText(cliProject);

            Assert.Contains("QmlSharp.DevTools", projectText, StringComparison.Ordinal);
        }

        private static bool IsLowerModuleProject(string path)
        {
            string fileName = Path.GetFileName(path);
            return fileName is
                "QmlSharp.Core.csproj" or
                "QmlSharp.Registry.csproj" or
                "QmlSharp.Qml.Ast.csproj" or
                "QmlSharp.Qml.Emitter.csproj" or
                "QmlSharp.Qt.Tools.csproj" or
                "QmlSharp.Dsl.csproj" or
                "QmlSharp.Dsl.Generator.csproj" or
                "QmlSharp.Compiler.csproj" or
                "QmlSharp.Host.csproj" or
                "QmlSharp.Build.csproj";
        }
    }
}
