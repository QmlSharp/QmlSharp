using System.Xml.Linq;
using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Pipeline
{
    public sealed class ProjectReferenceBoundaryTests
    {
        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void ProjectReferences_EmitterDependsOnAstOnlyWithinQmlSharpModules()
        {
            string repositoryRoot = FindRepositoryRoot();
            string projectPath = Path.Combine(
                repositoryRoot,
                "src",
                "QmlSharp.Qml.Emitter",
                "QmlSharp.Qml.Emitter.csproj");
            XDocument project = XDocument.Load(projectPath);
            ImmutableArray<string> referencedProjects = project
                .Descendants("ProjectReference")
                .Select(reference => Path.GetFileNameWithoutExtension((string?)reference.Attribute("Include") ?? string.Empty))
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();

            Assert.Equal(["QmlSharp.Qml.Ast"], referencedProjects.ToArray());
            Assert.DoesNotContain("QmlSharp.Compiler", referencedProjects);
            Assert.DoesNotContain("QmlSharp.Dsl.Generator", referencedProjects);
            Assert.DoesNotContain("QmlSharp.Build", referencedProjects);
            Assert.DoesNotContain("QmlSharp.DevTools", referencedProjects);
            Assert.DoesNotContain("QmlSharp.Host", referencedProjects);
            Assert.DoesNotContain("QmlSharp.Qt.Tools", referencedProjects);
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);

            while (directory is not null)
            {
                string solutionPath = Path.Combine(directory.FullName, "QmlSharp.slnx");
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
