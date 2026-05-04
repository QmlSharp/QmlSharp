using System.Xml.Linq;
using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class ProjectReferenceTests
    {
        [Fact]
        public void CliProject_ReferencesBuildProject()
        {
            string repoRoot = BuildTestFixtures.FindRepositoryRoot();
            ImmutableArray<string> references = ReadProjectReferences(
                System.IO.Path.Join(repoRoot, "src", "QmlSharp.Cli", "QmlSharp.Cli.csproj"));

            Assert.Contains(@"..\QmlSharp.Build\QmlSharp.Build.csproj", references);
        }

        [Fact]
        public void CliProject_IsExecutable()
        {
            string repoRoot = BuildTestFixtures.FindRepositoryRoot();
            XDocument document = XDocument.Load(
                System.IO.Path.Join(repoRoot, "src", "QmlSharp.Cli", "QmlSharp.Cli.csproj"));

            Assert.Contains(
                document.Descendants("OutputType").Select(static element => element.Value),
                outputType => string.Equals(outputType, "Exe", StringComparison.Ordinal));
        }

        [Fact]
        public void BuildProject_DoesNotReferenceCliProject()
        {
            string repoRoot = BuildTestFixtures.FindRepositoryRoot();
            ImmutableArray<string> references = ReadProjectReferences(
                System.IO.Path.Join(repoRoot, "src", "QmlSharp.Build", "QmlSharp.Build.csproj"));

            Assert.DoesNotContain(references, reference =>
                reference.Contains("QmlSharp.Cli", StringComparison.OrdinalIgnoreCase));
        }

        private static ImmutableArray<string> ReadProjectReferences(string projectPath)
        {
            XDocument document = XDocument.Load(projectPath);
            return document
                .Descendants("ProjectReference")
                .Select(static element => (string?)element.Attribute("Include"))
                .Where(static include => include is not null)
                .Select(static include => include!)
                .ToImmutableArray();
        }
    }
}
