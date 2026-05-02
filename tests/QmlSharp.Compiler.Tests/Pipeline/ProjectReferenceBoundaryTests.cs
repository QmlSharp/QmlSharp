using System.Xml.Linq;
using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.Pipeline
{
    public sealed class ProjectReferenceBoundaryTests
    {
        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void CompilerProject_HasOnlyApprovedProductionReferences()
        {
            string repositoryRoot = FindRepositoryRoot();
            string projectPath = Path.Join(repositoryRoot, "src", "QmlSharp.Compiler", "QmlSharp.Compiler.csproj");

            string[] actualProjectNames = LoadProjectReferenceNames(projectPath);
            string[] allowedProjectNames =
            [
                "QmlSharp.Core",
                "QmlSharp.Dsl",
                "QmlSharp.Dsl.Generator",
                "QmlSharp.Qml.Ast",
                "QmlSharp.Qml.Emitter",
                "QmlSharp.Qt.Tools",
                "QmlSharp.Registry",
            ];

            Assert.Equal(allowedProjectNames.Order(StringComparer.Ordinal), actualProjectNames);
            Assert.DoesNotContain("QmlSharp.Host", actualProjectNames);
            Assert.DoesNotContain("QmlSharp.Build", actualProjectNames);
            Assert.DoesNotContain("QmlSharp.DevTools", actualProjectNames);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void CoreProject_DoesNotDependOnDslOrCompiler()
        {
            string repositoryRoot = FindRepositoryRoot();
            string projectPath = Path.Join(repositoryRoot, "src", "QmlSharp.Core", "QmlSharp.Core.csproj");

            Assert.Empty(LoadProjectReferenceNames(projectPath));
        }

        private static string[] LoadProjectReferenceNames(string projectPath)
        {
            XDocument project = XDocument.Load(projectPath);
            return project
                .Descendants("ProjectReference")
                .Select(reference => GetProjectReferenceName((string?)reference.Attribute("Include") ?? string.Empty))
                .Order(StringComparer.Ordinal)
                .ToArray();
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
