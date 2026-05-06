namespace QmlSharp.DevTools.Tests
{
    public sealed class DependencyDirectionTests
    {
        [Fact]
        public void LowerModules_DoNotReferenceDevTools()
        {
            string repositoryRoot = DevToolsTestFixtures.FindRepositoryRoot();
            string sourceRoot = Path.Combine(repositoryRoot, "src");
            IEnumerable<string> projectFiles = Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(static path => !path.Contains("QmlSharp.DevTools", StringComparison.Ordinal));

            foreach (string projectFile in projectFiles)
            {
                string projectText = File.ReadAllText(projectFile);

                Assert.DoesNotContain("QmlSharp.DevTools", projectText, StringComparison.Ordinal);
            }
        }
    }
}
