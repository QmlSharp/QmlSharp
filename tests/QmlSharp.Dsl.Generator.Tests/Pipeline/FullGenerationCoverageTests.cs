using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using QmlSharp.Dsl.Generator.Tests.Fixtures;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Pipeline
{
    public sealed partial class FullGenerationCoverageTests
    {
        private static readonly string[] P0Modules =
        [
            "QtQml",
            "QtQuick",
            "QtQuick.Controls",
            "QtQuick.Layouts",
        ];

        [Fact]
        public async Task FullGeneration_P0Modules_GeneratesExpectedScaleWithoutUnexpectedDiagnostics()
        {
            GenerationResult result = await GenerateP0Async();

            Assert.Equal(P0Modules, result.Packages.Select(package => package.ModuleUri).ToArray());
            Assert.Equal(P0Modules.Length, result.Stats.TotalPackages);
            Assert.True(result.Stats.TotalTypes >= 120, $"Expected realistic P0 type coverage, got {result.Stats.TotalTypes}.");
            Assert.True(result.Stats.TotalFiles >= result.Stats.TotalTypes + (P0Modules.Length * 2));
            Assert.Empty(result.Warnings);
            Assert.Empty(result.SkippedTypes);
        }

        [Fact]
        public async Task FullGeneration_P0Modules_CoversCreatableNonCreatableAttachedAndGroupedSurfaces()
        {
            GenerationResult result = await GenerateP0Async();
            ImmutableArray<GeneratedPackage> packages = result.Packages;

            Assert.True(packages.Sum(static package => package.Stats.CreatableTypes) >= 100);
            Assert.True(packages.Sum(static package => package.Stats.NonCreatableTypes) > 0);
            Assert.True(packages.Sum(static package => package.Stats.AttachedTypeCount) > 0);
            Assert.Contains(packages.SelectMany(static package => package.Files), file => file.Content.Contains("BorderBuilder", StringComparison.Ordinal));
            Assert.Contains(packages.SelectMany(static package => package.Files), file => file.Content.Contains("FontBuilder", StringComparison.Ordinal));
            Assert.Contains(packages.SelectMany(static package => package.Files), file => file.Content.Contains("LayoutAttachedBuilder", StringComparison.Ordinal));
        }

        [Fact]
        public async Task FullGeneration_P0Modules_GeneratesPerModuleIndexAndProjectFiles()
        {
            GenerationResult result = await GenerateP0Async();

            foreach (GeneratedPackage package in result.Packages)
            {
                Assert.Contains(package.Files, file => file.Kind == GeneratedFileKind.IndexFile && file.RelativePath == "Index.cs");
                Assert.Contains(package.Files, file => file.Kind == GeneratedFileKind.ProjectFile && file.RelativePath == $"{package.PackageName}.csproj");
                Assert.True(package.Files.Count(file => file.Kind == GeneratedFileKind.TypeFile) == package.Types);
            }
        }

        [Fact]
        public async Task FullGeneration_P0Modules_RepeatedRunsProduceByteIdenticalFileSets()
        {
            GenerationResult first = await GenerateP0Async();
            GenerationResult second = await GenerateP0Async();

            Assert.Equal(SerializeFileSet(first.Packages), SerializeFileSet(second.Packages));
            Assert.Equal(first.Warnings, second.Warnings);
            Assert.Equal(first.SkippedTypes, second.SkippedTypes);
        }

        [Fact]
        public async Task FullGeneration_P0Modules_HasNoQmlTsTypeScriptOrNpmLeftovers()
        {
            GenerationResult result = await GenerateP0Async();
            string generatedText = SerializeFileSet(result.Packages);

            Assert.DoesNotContain("QmlTS", generatedText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("@qmlts", generatedText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("TypeScript", generatedText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("npm", generatedText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("package.json", generatedText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task WrittenOutputValidation_P0Modules_WritesAllFilesWithContentAndHeaders()
        {
            GenerationResult result = await GenerateP0Async();
            ModulePackager packager = new();

            using GeneratedOutputTempDirectory temp = DslTestFixtures.CreateGeneratedOutputTempDirectory();
            foreach (GeneratedPackage package in result.Packages)
            {
                WrittenPackageInfo written = await packager.WritePackage(package, temp.Path);

                Assert.Equal(package.PackageName, written.PackageName);
                Assert.Equal(package.Files.Length, written.FileCount);
                Assert.True(written.TotalBytes > 0);
                Assert.True(Directory.Exists(written.OutputPath));

                foreach (GeneratedFile file in package.Files)
                {
                    string path = Path.Join(written.OutputPath, file.RelativePath);
                    Assert.True(File.Exists(path), $"Expected generated file to be written: {path}");
                    string content = await File.ReadAllTextAsync(path);
                    Assert.False(string.IsNullOrWhiteSpace(content), $"Generated file was empty: {path}");
                    Assert.Equal(file.Content, content);
                    if (file.RelativePath.EndsWith(".cs", StringComparison.Ordinal))
                    {
                        Assert.StartsWith("// <auto-generated />", content, StringComparison.Ordinal);
                    }
                }
            }
        }

        [Fact]
        public async Task WrittenOutputValidation_P0Modules_HasPortableRelativePaths()
        {
            GenerationResult result = await GenerateP0Async();

            foreach (GeneratedFile file in result.Packages.SelectMany(static package => package.Files))
            {
                Assert.False(Path.IsPathRooted(file.RelativePath), $"Path should be relative: {file.RelativePath}");
                Assert.DoesNotContain('\\', file.RelativePath);
                Assert.DoesNotContain("::", file.RelativePath, StringComparison.Ordinal);
                Assert.DoesNotContain("..", file.RelativePath.Split('/'), StringComparer.Ordinal);
                Assert.All(
                    file.RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries),
                    segment => Assert.DoesNotContain(segment, Path.GetInvalidFileNameChars()));
            }
        }

        [Fact]
        public async Task Validation_P0Modules_UsesValidQmlSharpPackageNames()
        {
            GenerationResult result = await GenerateP0Async();

            foreach (GeneratedPackage package in result.Packages)
            {
                Assert.Matches(ValidQmlSharpPackageNameRegex(), package.PackageName);
                Assert.DoesNotContain('-', package.PackageName);
                Assert.DoesNotContain('@', package.PackageName);
                Assert.DoesNotContain(" ", package.PackageName, StringComparison.Ordinal);
                Assert.Equal($"QmlSharp.{package.ModuleUri}", package.PackageName);
            }
        }

        [Fact]
        public async Task Validation_P0Modules_ProjectFilesAreParseableAndReferenceRuntimePackages()
        {
            GenerationResult result = await GenerateP0Async();

            foreach (GeneratedPackage package in result.Packages)
            {
                GeneratedFile projectFile = Assert.Single(package.Files, file => file.Kind == GeneratedFileKind.ProjectFile);
                XDocument project = XDocument.Parse(projectFile.Content, LoadOptions.PreserveWhitespace);
                Assert.Equal("Project", project.Root?.Name.LocalName);
                Assert.Contains(project.Descendants("PackageReference"), reference => string.Equals(reference.Attribute("Include")?.Value, "QmlSharp.Core", StringComparison.Ordinal));
                Assert.Contains(project.Descendants("PackageReference"), reference => string.Equals(reference.Attribute("Include")?.Value, "QmlSharp.Dsl", StringComparison.Ordinal));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public async Task FullGeneration_P0GeneratedPackageProjects_Compile()
        {
            GenerationResult result = await GenerateP0Async();
            string solutionRoot = FindSolutionRoot();
            using GeneratedOutputTempDirectory temp = GeneratedOutputTempDirectory.CreateUnder(Path.Join(solutionRoot, "artifacts", "dsl-full-generation-tests"));
            string packageRoot = Path.Join(temp.Path, "packages");
            string consumerRoot = Path.Join(temp.Path, "consumer");
            ModulePackager packager = new();

            Dictionary<string, string> packageProjects = new(StringComparer.Ordinal);
            foreach (GeneratedPackage package in result.Packages)
            {
                WrittenPackageInfo written = await packager.WritePackage(package, packageRoot);
                packageProjects.Add(package.PackageName, Path.Join(written.OutputPath, $"{package.PackageName}.csproj"));
            }

            RewriteGeneratedProjectsToLocalReferences(packageProjects, solutionRoot);
            WriteConsumerProject(consumerRoot, packageProjects.Values.Order(StringComparer.Ordinal).ToImmutableArray());

            DotnetResult restore = await RunDotnetAsync("restore GeneratedConsumer.csproj", consumerRoot);
            Assert.True(restore.ExitCode == 0, CreateFailureMessage("restore", consumerRoot, restore));

            DotnetResult build = await RunDotnetAsync("build GeneratedConsumer.csproj --configuration Debug --no-restore", consumerRoot);
            Assert.True(build.ExitCode == 0, CreateFailureMessage("build --no-restore", consumerRoot, build));
        }

        private static async Task<GenerationResult> GenerateP0Async()
        {
            IRegistryQuery registry = DslTestFixtures.CreateP0ScaleFixture();
            GenerationPipeline pipeline = new();
            GenerationResult result = await pipeline.Generate(registry, DslTestFixtures.DefaultOptions);

            Assert.NotEmpty(result.Packages);
            return result;
        }

        private static string SerializeFileSet(ImmutableArray<GeneratedPackage> packages)
        {
            StringBuilder builder = new();
            foreach (GeneratedPackage package in packages
                         .OrderBy(static package => package.PackageName, StringComparer.Ordinal))
            {
                _ = builder.AppendLine(package.PackageName);
                foreach (GeneratedFile file in package.Files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal))
                {
                    _ = builder.AppendLine(file.RelativePath);
                    _ = builder.AppendLine(file.Content);
                }
            }

            return builder.ToString();
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
                foreach (XElement packageReference in document.Descendants("PackageReference").ToList())
                {
                    string? include = packageReference.Attribute("Include")?.Value;
                    if (include is null || !references.TryGetValue(include, out string? referencePath))
                    {
                        continue;
                    }

                    string projectDirectory = Path.GetDirectoryName(projectPath)
                        ?? throw new DirectoryNotFoundException($"Project path does not include a directory: {projectPath}");
                    packageReference.AddAfterSelf(new XElement(
                        "ProjectReference",
                        new XAttribute("Include", ToProjectReferenceInclude(projectDirectory, referencePath))));
                    packageReference.Remove();
                }

                document.Save(projectPath);
            }
        }

        private static void WriteConsumerProject(string consumerRoot, ImmutableArray<string> packageProjectPaths)
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
            File.WriteAllText(
                Path.Join(consumerRoot, "Usage.cs"),
                """
                using QmlSharp.QtQml;
                using QmlSharp.QtQuick;
                using QmlSharp.QtQuick.Controls;
                using QmlSharp.QtQuick.Layouts;

                namespace GeneratedConsumer
                {
                    public static class Usage
                    {
                        public static object[] CreateAll()
                        {
                            return
                            [
                                QtObjectFactory.QtObject().Build(),
                                RectangleFactory.Rectangle().Width(100).Height(50).Color("blue").Build(),
                                ButtonFactory.Button().Text("Run").OnClicked("run()").Build(),
                                RowLayoutFactory.RowLayout().Spacing(8).Build(),
                            ];
                        }
                    }
                }
                """);
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

        private static string ToProjectReferenceInclude(string projectDirectory, string referencePath)
        {
            string fullProjectDirectory = Path.GetFullPath(projectDirectory);
            string fullReferencePath = Path.GetFullPath(referencePath);
            return Path.GetRelativePath(fullProjectDirectory, fullReferencePath);
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
                """;
        }

        [GeneratedRegex(@"^QmlSharp(\.[A-Z][A-Za-z0-9]*)+$", RegexOptions.CultureInvariant)]
        private static partial Regex ValidQmlSharpPackageNameRegex();

        private sealed record DotnetResult(int ExitCode, string Stdout, string Stderr);
    }
}
