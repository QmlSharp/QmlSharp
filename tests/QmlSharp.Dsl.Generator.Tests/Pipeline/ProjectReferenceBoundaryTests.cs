using System.Xml.Linq;
using QmlSharp.Dsl.Generator.Tests.Fixtures;

namespace QmlSharp.Dsl.Generator.Tests.Pipeline
{
    public sealed class ProjectReferenceBoundaryTests
    {
        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void DslProjects_HaveOnlyAllowedProductionReferences()
        {
            string repositoryRoot = FindRepositoryRoot();

            AssertProjectReferences(
                Path.Join(repositoryRoot, "src", "QmlSharp.Dsl", "QmlSharp.Dsl.csproj"),
                ["QmlSharp.Core", "QmlSharp.Qml.Ast"]);
            AssertProjectReferences(
                Path.Join(repositoryRoot, "src", "QmlSharp.Dsl.Generator", "QmlSharp.Dsl.Generator.csproj"),
                ["QmlSharp.Qml.Ast", "QmlSharp.Registry"]);
        }

        private static void AssertProjectReferences(string projectPath, string[] allowedProjectNames)
        {
            XDocument project = XDocument.Load(projectPath);
            string[] actualProjectNames = project
                .Descendants("ProjectReference")
                .Select(reference => GetProjectReferenceName((string?)reference.Attribute("Include") ?? string.Empty))
                .Order(StringComparer.Ordinal)
                .ToArray();
            string[] sortedAllowedProjectNames = allowedProjectNames.Order(StringComparer.Ordinal).ToArray();
            string[] invalidProjectNames = actualProjectNames.Except(sortedAllowedProjectNames, StringComparer.Ordinal).ToArray();

            Assert.Equal(sortedAllowedProjectNames, actualProjectNames);
            Assert.DoesNotContain("QmlSharp.Qml.Emitter", invalidProjectNames);
            Assert.DoesNotContain("QmlSharp.Qt.Tools", invalidProjectNames);
            Assert.DoesNotContain("QmlSharp.Compiler", invalidProjectNames);
            Assert.DoesNotContain("QmlSharp.Build", invalidProjectNames);
            Assert.DoesNotContain("QmlSharp.DevTools", invalidProjectNames);
            Assert.DoesNotContain("QmlSharp.Host", invalidProjectNames);
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);

            while (directory is not null)
            {
                string solutionPath = Path.Join(directory.FullName, "QmlSharp.slnx");
                if (File.Exists(solutionPath))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not locate QmlSharp repository root.");
        }

        private static string GetProjectReferenceName(string include)
        {
            string fileName = include
                .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault() ?? string.Empty;

            return Path.GetFileNameWithoutExtension(fileName);
        }
    }
}
