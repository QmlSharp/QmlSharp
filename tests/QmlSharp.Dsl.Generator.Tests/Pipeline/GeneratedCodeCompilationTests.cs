using System.Diagnostics;
using System.Xml.Linq;
using QmlSharp.Dsl.Generator.Tests.Fixtures;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Pipeline
{
    public sealed class GeneratedCodeCompilationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public async Task GeneratedCodeCompile_RectanglePackage_BuildsConsumerProject()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();

            await CompileGeneratedConsumerAsync(
                registry,
                """
                using QmlSharp.QtQuick;

                namespace GeneratedConsumer;

                public static class Usage
                {
                    public static object Create()
                    {
                        return RectangleFactory.Rectangle()
                            .Width(100)
                            .Height(50)
                            .Color("red")
                            .Border(border => border.Width(2).Color("black"))
                            .OnColorChanged("console.log(\"changed\")")
                            .Build();
                    }
                }
                """);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task GeneratedCodeCompile_TextPackage_BuildsConsumerProject()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();

            await CompileGeneratedConsumerAsync(
                registry,
                """
                using QmlSharp.QtQuick;

                namespace GeneratedConsumer;

                public static class Usage
                {
                    public static object Create()
                    {
                        return TextFactory.Text()
                            .Text("Hello")
                            .TextBind("model.title")
                            .Width(320)
                            .OnTextChanged("console.log(\"text\")")
                            .Build();
                    }
                }
                """);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task GeneratedCodeCompile_ButtonPackage_BuildsConsumerProject()
        {
            IRegistryQuery registry = DslTestFixtures.CreateQtQuickControlsFixture();

            await CompileGeneratedConsumerAsync(
                registry,
                """
                using QmlSharp.QtQuick.Controls;

                namespace GeneratedConsumer;

                public static class Usage
                {
                    public static object Create()
                    {
                        return ButtonFactory.Button()
                            .Text("Submit")
                            .@checked(false)
                            .OnClicked("submitForm()")
                            .Build();
                    }
                }
                """);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task GeneratedCodeCompile_P0PackageReferences_BuildsConsumerProject()
        {
            IRegistryQuery registry = DslTestFixtures.CreateP0Fixture();

            await CompileGeneratedConsumerAsync(
                registry,
                """
                using QmlSharp.QtQml;
                using QmlSharp.QtQuick;
                using QmlSharp.QtQuick.Controls;
                using QmlSharp.QtQuick.Layouts;

                namespace GeneratedConsumer;

                public static class Usage
                {
                    public static object[] CreateAll()
                    {
                        return
                        [
                            QtObjectFactory.QtObject().Build(),
                            RectangleFactory.Rectangle().Width(100).Color("blue").Build(),
                            ButtonFactory.Button().Text("Run").OnClicked("run()").Build(),
                            LayoutFactory.Layout().Spacing(12).Build(),
                        ];
                    }
                }
                """);
        }

        [Fact]
        public void GeneratedCode_NonCreatableType_OmitsFactoryMethod()
        {
            CodeEmitter emitter = new();
            GeneratedTypeCode helper = new(
                QmlName: "Helper",
                ModuleUri: "QtQuick",
                FactoryName: "Helper",
                PropsInterfaceName: "IHelperProps",
                BuilderInterfaceName: "IHelperBuilder",
                FactoryMethodCode: null,
                Properties: ImmutableArray<GeneratedProperty>.Empty,
                Signals: ImmutableArray<GeneratedSignal>.Empty,
                Methods: ImmutableArray<GeneratedMethod>.Empty,
                Enums: ImmutableArray<GeneratedEnum>.Empty,
                AttachedTypes: ImmutableArray<GeneratedAttachedType>.Empty,
                DefaultProperty: null,
                IsCreatable: false,
                IsDeprecated: false);

            string source = emitter.EmitTypeFile(helper, DslTestFixtures.DefaultEmitOptions);

            Assert.Contains("public interface IHelperBuilder", source, StringComparison.Ordinal);
            Assert.DoesNotContain("public static class HelperFactory", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ObjectFactory.Create<IHelperBuilder>", source, StringComparison.Ordinal);
        }

        [Fact]
        public void ProjectReferenceInclude_IsRelativeToGeneratedProjectDirectory()
        {
            using GeneratedOutputTempDirectory temp = DslTestFixtures.CreateGeneratedOutputTempDirectory();
            string projectDirectory = Path.Join(temp.Path, "consumer");
            _ = Directory.CreateDirectory(projectDirectory);
            string referencePath = Path.Join(FindSolutionRoot(), "src", "QmlSharp.Core", "QmlSharp.Core.csproj");

            string include = ToProjectReferenceInclude(projectDirectory, referencePath);

            Assert.False(Path.IsPathRooted(include), $"ProjectReference include should be relative: {include}");
            Assert.Equal(
                Path.GetFullPath(referencePath),
                Path.GetFullPath(Path.Join(projectDirectory, include)));
        }

        private static async Task CompileGeneratedConsumerAsync(IRegistryQuery registry, string usageSource)
        {
            using GeneratedOutputTempDirectory temp = DslTestFixtures.CreateGeneratedOutputTempDirectory();
            string solutionRoot = FindSolutionRoot();
            string packageRoot = Path.Join(temp.Path, "packages");
            string consumerRoot = Path.Join(temp.Path, "consumer");
            GenerationPipeline pipeline = new();
            ModulePackager packager = new();

            GenerationResult result = await pipeline.Generate(registry, DslTestFixtures.DefaultOptions);
            Assert.NotEmpty(result.Packages);

            Dictionary<string, string> packageProjects = new(StringComparer.Ordinal);
            foreach (GeneratedPackage package in result.Packages)
            {
                WrittenPackageInfo written = await packager.WritePackage(package, packageRoot);
                string projectPath = Path.Join(written.OutputPath, $"{package.PackageName}.csproj");
                Assert.True(File.Exists(projectPath), $"Generated package project was not written: {projectPath}");
                packageProjects.Add(package.PackageName, projectPath);
            }

            RewriteGeneratedProjectsToLocalReferences(packageProjects, solutionRoot);
            WriteConsumerProject(consumerRoot, packageProjects.Values.Order(StringComparer.Ordinal).ToImmutableArray(), usageSource);

            DotnetResult restore = await RunDotnetAsync("restore GeneratedConsumer.csproj", consumerRoot);
            Assert.True(restore.ExitCode == 0, CreateFailureMessage("restore", consumerRoot, restore));

            DotnetResult build = await RunDotnetAsync("build GeneratedConsumer.csproj --configuration Debug --no-restore", consumerRoot);
            Assert.True(build.ExitCode == 0, CreateFailureMessage("build --no-restore", consumerRoot, build));
        }

        private static void RewriteGeneratedProjectsToLocalReferences(
            IReadOnlyDictionary<string, string> packageProjects,
            string solutionRoot)
        {
            Dictionary<string, string> references = new(StringComparer.Ordinal)
            {
                ["QmlSharp.Core"] = Path.Join(solutionRoot, "src", "QmlSharp.Core", "QmlSharp.Core.csproj"),
                ["QmlSharp.Dsl"] = Path.Join(solutionRoot, "src", "QmlSharp.Dsl", "QmlSharp.Dsl.csproj"),
            };

            foreach (KeyValuePair<string, string> packageProject in packageProjects)
            {
                references[packageProject.Key] = packageProject.Value;
            }

            foreach (string projectPath in packageProjects.Values.Order(StringComparer.Ordinal))
            {
                XDocument document = XDocument.Load(projectPath, LoadOptions.PreserveWhitespace);
                List<XElement> packageReferences = document
                    .Descendants("PackageReference")
                    .ToList();

                foreach (XElement packageReference in packageReferences)
                {
                    string? include = packageReference.Attribute("Include")?.Value;
                    if (include is null || !references.TryGetValue(include, out string? referencePath))
                    {
                        continue;
                    }

                    string projectDirectory = Path.GetDirectoryName(projectPath)
                        ?? throw new DirectoryNotFoundException($"Project path does not include a directory: {projectPath}");
                    XElement projectReference = new("ProjectReference", new XAttribute("Include", ToProjectReferenceInclude(projectDirectory, referencePath)));
                    packageReference.AddAfterSelf(projectReference);
                    packageReference.Remove();
                }

                document.Save(projectPath);
            }
        }

        private static void WriteConsumerProject(
            string consumerRoot,
            ImmutableArray<string> packageProjectPaths,
            string usageSource)
        {
            _ = Directory.CreateDirectory(consumerRoot);
            string references = string.Join(
                Environment.NewLine,
                packageProjectPaths.Select(path => $"    <ProjectReference Include=\"{ToProjectReferenceInclude(consumerRoot, path)}\" />"));
            string project = $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>
                  <ItemGroup>
                {{references}}
                  </ItemGroup>
                </Project>
                """;

            File.WriteAllText(Path.Join(consumerRoot, "GeneratedConsumer.csproj"), project);
            File.WriteAllText(Path.Join(consumerRoot, "Usage.cs"), usageSource);
        }

        private static string ToProjectReferenceInclude(string projectDirectory, string referencePath)
        {
            string fullProjectDirectory = Path.GetFullPath(projectDirectory);
            string fullReferencePath = Path.GetFullPath(referencePath);
            return Path.GetRelativePath(fullProjectDirectory, fullReferencePath);
        }

        private static async Task<DotnetResult> RunDotnetAsync(string arguments, string workingDirectory)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
            string? qtDir = Environment.GetEnvironmentVariable("QT_DIR");
            if (!string.IsNullOrWhiteSpace(qtDir))
            {
                startInfo.Environment["QT_DIR"] = qtDir;
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start dotnet process.");
            string stdout = await process.StandardOutput.ReadToEndAsync();
            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return new DotnetResult(process.ExitCode, stdout, stderr);
        }

        private static string FindSolutionRoot()
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

            throw new DirectoryNotFoundException("Could not locate QmlSharp.slnx from the test assembly directory.");
        }

        private static string CreateFailureMessage(string command, string workingDirectory, DotnetResult result)
        {
            return $"""
                dotnet {command} failed in {workingDirectory}
                Exit code: {result.ExitCode}
                STDOUT:
                {result.Stdout}
                STDERR:
                {result.Stderr}
                Preserve output with QMLSHARP_PRESERVE_DSL_COMPILE_OUTPUT=1.
                """;
        }

        private sealed record DotnetResult(int ExitCode, string Stdout, string Stderr);
    }
}
