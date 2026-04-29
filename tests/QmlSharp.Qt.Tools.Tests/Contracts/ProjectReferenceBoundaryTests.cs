using System.Xml.Linq;
using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.Contracts
{
    [Trait("Category", TestCategories.Smoke)]
    public sealed class ProjectReferenceBoundaryTests
    {
        [Fact]
        public void QtToolsProject_HasNoInvalidModuleReferences()
        {
            string repositoryRoot = FindRepositoryRoot();
            string projectPath = Path.Join(repositoryRoot, "src", "QmlSharp.Qt.Tools", "QmlSharp.Qt.Tools.csproj");
            XDocument project = XDocument.Load(projectPath);
            string[] projectReferences = project
                .Descendants("ProjectReference")
                .Select(static reference => (string?)reference.Attribute("Include") ?? string.Empty)
                .Order(StringComparer.Ordinal)
                .ToArray();
            string[] packageReferences = project
                .Descendants("PackageReference")
                .Select(static reference => (string?)reference.Attribute("Include") ?? string.Empty)
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Empty(projectReferences);
            Assert.Empty(packageReferences);
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
    }
}
