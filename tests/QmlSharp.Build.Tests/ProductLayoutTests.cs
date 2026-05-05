using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class ProductLayoutTests
    {
        [Fact]
        public void PL01_CreateDirectoryStructure_CreatesAllSubdirectories()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(PL01_CreateDirectoryStructure_CreatesAllSubdirectories));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ProductLayout layout = new();

            layout.CreateDirectoryStructure(context.OutputDir);

            Assert.True(Directory.Exists(Path.Join(context.OutputDir, "qml")));
            Assert.True(Directory.Exists(Path.Join(context.OutputDir, "schemas")));
            Assert.True(Directory.Exists(Path.Join(context.OutputDir, "native")));
            Assert.True(Directory.Exists(Path.Join(context.OutputDir, "native", "generated")));
            Assert.True(Directory.Exists(Path.Join(context.OutputDir, "managed")));
            Assert.True(Directory.Exists(Path.Join(context.OutputDir, "source-maps")));
            Assert.True(Directory.Exists(Path.Join(context.OutputDir, "assets")));
        }

        [Fact]
        public void PL02_Assemble_CopiesQmlFilesToDistQml()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(PL02_Assemble_CopiesQmlFilesToDistQml));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ProductLayout layout = new();
            FixtureArtifacts fixture = CreateFixtureArtifacts(project.Path);

            ProductAssemblyResult result = layout.Assemble(context, fixture.Artifacts);

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Join(context.OutputDir, "qml", "QmlSharp", "MyApp", "CounterView.qml")));
        }

        [Fact]
        public void PL03_Assemble_CopiesSchemasToDistSchemas()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(PL03_Assemble_CopiesSchemasToDistSchemas));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ProductLayout layout = new();
            FixtureArtifacts fixture = CreateFixtureArtifacts(project.Path);

            ProductAssemblyResult result = layout.Assemble(context, fixture.Artifacts);

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Join(context.OutputDir, "schemas", "CounterViewModel.schema.json")));
        }

        [Fact]
        public void PL04_Assemble_CopiesNativeLibToDistNative()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(PL04_Assemble_CopiesNativeLibToDistNative));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ProductLayout layout = new();
            FixtureArtifacts fixture = CreateFixtureArtifacts(project.Path);

            ProductAssemblyResult result = layout.Assemble(context, fixture.Artifacts);

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Join(context.OutputDir, "native", NativeLibraryNames.GetFileName("qmlsharp_native"))));
        }

        [Fact]
        public void PL05_GenerateManifest_ProducesValidJson()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(PL05_GenerateManifest_ProducesValidJson));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ProductLayout layout = new();
            FixtureArtifacts fixture = CreateFixtureArtifacts(project.Path);

            ProductAssemblyResult result = layout.Assemble(context, fixture.Artifacts);

            Assert.True(result.Success);
            Assert.NotNull(result.Manifest);
            Assert.Equal("MyApp", result.Manifest.ProjectName);
            Assert.Contains("CounterViewModel", result.Manifest.ViewModels);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Join(context.OutputDir, "manifest.json")));
            Assert.Equal("MyApp", document.RootElement.GetProperty("projectName").GetString());
            Assert.Equal("1.0.0", document.RootElement.GetProperty("version").GetString());
            Assert.True(document.RootElement.TryGetProperty("buildTimestamp", out JsonElement timestamp));
            Assert.NotEqual(default, timestamp.GetDateTimeOffset());
        }

        [Fact]
        public void PL06_ValidateOutput_CompleteOutputReturnsNoDiagnostics()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(PL06_ValidateOutput_CompleteOutputReturnsNoDiagnostics));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ProductLayout layout = new();
            FixtureArtifacts fixture = CreateFixtureArtifacts(project.Path);

            ProductAssemblyResult result = layout.Assemble(context, fixture.Artifacts);
            ImmutableArray<BuildDiagnostic> diagnostics = layout.ValidateOutput(context.OutputDir);

            Assert.True(result.Success);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void PL07_ValidateOutput_MissingNativeLibReportsB072()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(PL07_ValidateOutput_MissingNativeLibReportsB072));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ProductLayout layout = new();
            FixtureArtifacts fixture = CreateFixtureArtifacts(project.Path, includeNative: false);

            ProductAssemblyResult result = layout.Assemble(context, fixture.Artifacts);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, static diagnostic =>
                diagnostic.Code == BuildDiagnosticCode.OutputValidationFailed);
        }

        [Fact]
        public void ValidateOutput_EmptyApplicationOutputReportsAllRequiredArtifacts()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(ValidateOutput_EmptyApplicationOutputReportsAllRequiredArtifacts));
            string outputRoot = Path.Join(project.Path, "dist");
            _ = Directory.CreateDirectory(outputRoot);
            ProductLayout layout = new();

            ImmutableArray<BuildDiagnostic> diagnostics = layout.ValidateOutput(outputRoot);

            Assert.Contains(diagnostics, static diagnostic =>
                diagnostic.Message.Contains("manifest.json is missing", StringComparison.Ordinal));
            Assert.Contains(diagnostics, static diagnostic =>
                diagnostic.Message.Contains("no QML files", StringComparison.Ordinal));
            Assert.Contains(diagnostics, static diagnostic =>
                diagnostic.Message.Contains("no schema files", StringComparison.Ordinal));
            Assert.Contains(diagnostics, static diagnostic =>
                diagnostic.Code == BuildDiagnosticCode.OutputValidationFailed &&
                diagnostic.Message.Contains("native library is missing", StringComparison.Ordinal));
            Assert.Contains(diagnostics, static diagnostic =>
                diagnostic.Message.Contains("no managed assembly", StringComparison.Ordinal));
        }

        [Fact]
        public void ValidateOutput_InvalidLibraryOutputReportsForbiddenApplicationArtifacts()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(ValidateOutput_InvalidLibraryOutputReportsForbiddenApplicationArtifacts));
            string outputRoot = Path.Join(project.Path, "lib");
            _ = WriteFile(Path.Join(outputRoot, "qmlsharp.module.json"), "{}\n");
            _ = WriteFile(Path.Join(outputRoot, "event-bindings.json"), "{}\n");
            _ = WriteFile(Path.Join(outputRoot, "native", NativeLibraryNames.GetFileName("qmlsharp_native")), "native");
            _ = WriteFile(Path.Join(outputRoot, "managed", "MyApp.dll"), "managed");
            ProductLayout layout = new();

            ImmutableArray<BuildDiagnostic> diagnostics = layout.ValidateOutput(outputRoot);

            Assert.Contains(diagnostics, static diagnostic =>
                diagnostic.Message.Contains("must not contain application event-bindings", StringComparison.Ordinal));
            Assert.Contains(diagnostics, static diagnostic =>
                diagnostic.Message.Contains("no library QML files", StringComparison.Ordinal));
            Assert.Contains(diagnostics, static diagnostic =>
                diagnostic.Message.Contains("no library schema files", StringComparison.Ordinal));
            Assert.Contains(diagnostics, static diagnostic =>
                diagnostic.Message.Contains("must not contain native or managed", StringComparison.Ordinal));
        }

        [Fact]
        public void OutputAssembly_MissingSourceArtifactReportsB070()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(OutputAssembly_MissingSourceArtifactReportsB070));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ProductLayout layout = new();
            FixtureArtifacts fixture = CreateFixtureArtifacts(project.Path);
            BuildArtifacts artifacts = fixture.Artifacts with
            {
                QmlFiles = ImmutableArray.Create(Path.Join(project.Path, "missing", "CounterView.qml")),
            };

            ProductAssemblyResult result = layout.Assemble(context, artifacts);

            Assert.False(result.Success);
            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics, static item =>
                item.Code == BuildDiagnosticCode.OutputAssemblyFailed &&
                item.Message.Contains("source artifact", StringComparison.Ordinal));
            Assert.Equal(BuildPhase.OutputAssembly, diagnostic.Phase);
        }

        [Fact]
        public async Task BP09_OutputAssemblyStage_WritesManifestWithCorrectMetadata()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(BP09_OutputAssemblyStage_WritesManifestWithCorrectMetadata));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            _ = CreateStageInputs(context);
            OutputAssemblyBuildStage stage = new();

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.Manifest);
            Assert.Equal("MyApp", result.Manifest.ProjectName);
            Assert.Equal("development", result.Manifest.BuildMode);
            Assert.Equal("6.11.0", result.Manifest.QtVersion);
            Assert.Contains("QmlSharp.MyApp", result.Manifest.QmlModules);
            Assert.Contains("CounterViewModel", result.Manifest.ViewModels);
            Assert.True(File.Exists(Path.Join(context.OutputDir, "manifest.json")));
        }

        [Fact]
        public async Task BP10_OutputAssemblyStage_ValidatesCompleteOutput()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(BP10_OutputAssemblyStage_ValidatesCompleteOutput));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            _ = CreateStageInputs(context);
            ProductLayout layout = new();
            OutputAssemblyBuildStage stage = new(layout);

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Empty(layout.ValidateOutput(context.OutputDir));
        }

        [Fact]
        public void ApplicationLayout_CopiesEventBindingsWithoutModification()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(ApplicationLayout_CopiesEventBindingsWithoutModification));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ProductLayout layout = new();
            string eventBindingsJson = "{\"commands\":[{\"name\":\"increment\"}],\"effects\":[]}";
            FixtureArtifacts fixture = CreateFixtureArtifacts(project.Path, eventBindingsJson: eventBindingsJson);

            ProductAssemblyResult result = layout.Assemble(context, fixture.Artifacts);

            Assert.True(result.Success);
            Assert.Equal(eventBindingsJson, File.ReadAllText(Path.Join(context.OutputDir, "event-bindings.json")));
        }

        [Fact]
        public void ManifestJson_IsDeterministicExceptBuildTimestamp()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(ManifestJson_IsDeterministicExceptBuildTimestamp));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ProductLayout layout = new();
            FixtureArtifacts fixture = CreateFixtureArtifacts(project.Path);
            ProductAssemblyResult result = layout.Assemble(context, fixture.Artifacts);
            BuildResult buildResult = CreateBuildResult(result.Manifest!, context.OutputDir);

            string first = NormalizeTimestamp(layout.GenerateManifest(buildResult, context));
            string second = NormalizeTimestamp(layout.GenerateManifest(buildResult, context));

            Assert.Equal(first, second);
        }

        [Fact]
        public void ManifestMetadata_InvalidSchemaJsonFallsBackToSchemaFileName()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(ManifestMetadata_InvalidSchemaJsonFallsBackToSchemaFileName));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ProductLayout layout = new();
            FixtureArtifacts fixture = CreateFixtureArtifacts(project.Path);
            File.WriteAllText(fixture.Artifacts.SchemaFiles[0], "{");

            ProductAssemblyResult result = layout.Assemble(context, fixture.Artifacts);

            Assert.True(result.Success);
            Assert.NotNull(result.Manifest);
            Assert.Contains("CounterViewModel", result.Manifest.ViewModels);
        }

        [Fact]
        public void ManifestWriteFailure_ReportsB071()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(ManifestWriteFailure_ReportsB071));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ProductLayout layout = new();
            FixtureArtifacts fixture = CreateFixtureArtifacts(project.Path);
            _ = Directory.CreateDirectory(context.OutputDir);
            _ = Directory.CreateDirectory(Path.Join(context.OutputDir, "manifest.json"));

            ProductAssemblyResult result = layout.Assemble(context, fixture.Artifacts);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, static diagnostic =>
                diagnostic.Code == BuildDiagnosticCode.ManifestWriteFailed);
        }

        [Fact]
        public void ApplicationLayout_FileHashesUsePortablePathsWithSpaces()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(ApplicationLayout_FileHashesUsePortablePathsWithSpaces));
            QmlSharpConfig config = BuildTestFixtures.CreateDefaultConfig() with
            {
                Name = "My App",
            };
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path) with
            {
                Config = config,
            };
            ProductLayout layout = new();
            FixtureArtifacts fixture = CreateFixtureArtifacts(project.Path, includeSpaces: true, assemblyName: "My App.dll");

            ProductAssemblyResult result = layout.Assemble(context, fixture.Artifacts);

            Assert.True(result.Success);
            Assert.NotNull(result.Manifest);
            string qmlRelativePath = "qml/QmlSharp/MyApp/Counter View.qml";
            string assetRelativePath = "assets/images/app icon.png";
            Assert.Equal("managed/My App.dll", result.Manifest.ManagedAssembly);
            Assert.True(result.Manifest.FileHashes.TryGetValue(qmlRelativePath, out string? qmlHash));
            Assert.True(result.Manifest.FileHashes.ContainsKey(assetRelativePath));
            Assert.Equal(
                HashFile(Path.Join(context.OutputDir, "qml", "QmlSharp", "MyApp", "Counter View.qml")),
                qmlHash);
        }

        [Fact]
        public void ApplicationLayout_ManifestUsesEntryAssemblyArtifactWhenPublishOutputContainsDependencies()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(ApplicationLayout_ManifestUsesEntryAssemblyArtifactWhenPublishOutputContainsDependencies));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ProductLayout layout = new();
            FixtureArtifacts fixture = CreateFixtureArtifacts(
                project.Path,
                assemblyName: "ZzzApp.dll",
                dependencyAssemblyName: "A.Dependency.dll");

            ProductAssemblyResult result = layout.Assemble(context, fixture.Artifacts);

            Assert.True(result.Success);
            Assert.NotNull(result.Manifest);
            Assert.Equal("managed/ZzzApp.dll", result.Manifest.ManagedAssembly);
        }

        [Fact]
        public async Task BP11_OutputAssemblyStage_ManifestUsesProjectAssemblyWhenManagedDirectoryContainsDependencyDlls()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(BP11_OutputAssemblyStage_ManifestUsesProjectAssemblyWhenManagedDirectoryContainsDependencyDlls));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            _ = CreateStageInputs(context, dependencyAssemblyName: "A.Dependency.dll");
            OutputAssemblyBuildStage stage = new();

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.Manifest);
            Assert.Equal("managed/MyApp.dll", result.Manifest.ManagedAssembly);
        }

        [Fact]
        public void LibraryMode_AssemblesModuleManifestWithoutApplicationOutputs()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(LibraryMode_AssemblesModuleManifestWithoutApplicationOutputs));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path) with
            {
                LibraryMode = true,
            };
            ProductLayout layout = new();
            FixtureArtifacts fixture = CreateFixtureArtifacts(project.Path);

            ProductAssemblyResult result = layout.Assemble(context, fixture.Artifacts);
            string libraryRoot = Path.Join(project.Path, "lib");

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Join(libraryRoot, "qml", "QmlSharp", "MyApp", "CounterView.qml")));
            Assert.True(File.Exists(Path.Join(libraryRoot, "schemas", "CounterViewModel.schema.json")));
            Assert.True(File.Exists(Path.Join(libraryRoot, "qmlsharp.module.json")));
            Assert.False(File.Exists(Path.Join(libraryRoot, "manifest.json")));
            Assert.False(File.Exists(Path.Join(libraryRoot, "event-bindings.json")));
            Assert.False(Directory.Exists(Path.Join(libraryRoot, "native")));
            Assert.False(Directory.Exists(Path.Join(libraryRoot, "managed")));
            Assert.Empty(layout.ValidateOutput(libraryRoot));
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Join(libraryRoot, "qmlsharp.module.json")));
            Assert.Equal("QmlSharp.MyApp", document.RootElement.GetProperty("moduleUri").GetString());
            Assert.Contains("qml", ReadStringArray(document.RootElement.GetProperty("qmlImportPaths")));
            Assert.Contains("schemas/CounterViewModel.schema.json", ReadStringArray(document.RootElement.GetProperty("schemaFiles")));
        }

        private static FixtureArtifacts CreateFixtureArtifacts(
            string projectDir,
            bool includeNative = true,
            bool includeManaged = true,
            bool includeSpaces = false,
            string assemblyName = "MyApp.dll",
            string? dependencyAssemblyName = null,
            string eventBindingsJson = "{\"commands\":[],\"effects\":[]}")
        {
            string artifactRoot = Path.Join(projectDir, "artifact inputs");
            string qmlFileName = includeSpaces ? "Counter View.qml" : "CounterView.qml";
            string qmlPath = WriteFile(
                Path.Join(artifactRoot, "qml", "QmlSharp", "MyApp", qmlFileName),
                "import QtQuick\nItem { }\n");
            string schemaPath = WriteFile(
                Path.Join(artifactRoot, "schemas", "CounterViewModel.schema.json"),
                new ViewModelSchemaSerializer().Serialize(BuildTestFixtures.CreateCounterSchema()));
            string qmldirPath = WriteFile(
                Path.Join(artifactRoot, "qml", "QmlSharp", "MyApp", "qmldir"),
                "module QmlSharp.MyApp\n");
            string qmltypesPath = WriteFile(
                Path.Join(artifactRoot, "qml", "QmlSharp", "MyApp", "qmlsharp_myapp.qmltypes"),
                "Module {}\n");
            string eventBindingsPath = WriteFile(Path.Join(artifactRoot, "event-bindings.json"), eventBindingsJson);
            string sourceMapPath = WriteFile(Path.Join(artifactRoot, "source-maps", qmlFileName + ".map"), "{}\n");
            string assetFileName = includeSpaces ? "app icon.png" : "icon.png";
            string assetPath = WriteFile(Path.Join(artifactRoot, "assets", "images", assetFileName), "asset");
            string generatedPath = WriteFile(Path.Join(artifactRoot, "native", "generated", "CounterViewModel.cpp"), "// generated\n");
            string? nativePath = includeNative
                ? WriteFile(Path.Join(artifactRoot, "native", NativeLibraryNames.GetFileName("qmlsharp_native")), "native")
                : null;
            string? assemblyPath = includeManaged
                ? WriteFile(Path.Join(artifactRoot, "publish output", assemblyName), "managed")
                : null;
            if (assemblyPath is not null)
            {
                _ = WriteFile(Path.Join(artifactRoot, "publish output", "MyApp.deps.json"), "{}\n");
                if (!string.IsNullOrWhiteSpace(dependencyAssemblyName))
                {
                    _ = WriteFile(Path.Join(artifactRoot, "publish output", dependencyAssemblyName), "dependency");
                }
            }

            return new FixtureArtifacts(
                new BuildArtifacts
                {
                    QmlFiles = ImmutableArray.Create(qmlPath),
                    SchemaFiles = ImmutableArray.Create(schemaPath),
                    EventBindingsFile = eventBindingsPath,
                    SourceMapFiles = ImmutableArray.Create(sourceMapPath),
                    ModuleMetadataFiles = ImmutableArray.Create(qmldirPath, qmltypesPath),
                    AssetFiles = ImmutableArray.Create(assetPath),
                    NativeLibraryPath = nativePath,
                    AssemblyPath = assemblyPath,
                },
                generatedPath);
        }

        private static FixtureArtifacts CreateStageInputs(BuildContext context, string? dependencyAssemblyName = null)
        {
            FixtureArtifacts fixture = CreateFixtureArtifacts(context.ProjectDir, dependencyAssemblyName: dependencyAssemblyName);
            ProductAssemblyResult result = new ProductLayout().Assemble(context, fixture.Artifacts);
            Assert.True(result.Success);
            File.Delete(Path.Join(context.OutputDir, "manifest.json"));
            return fixture;
        }

        private static BuildResult CreateBuildResult(ProductManifest manifest, string outputDir)
        {
            return new BuildResult
            {
                Success = true,
                PhaseResults = ImmutableArray<PhaseResult>.Empty,
                Diagnostics = ImmutableArray<BuildDiagnostic>.Empty,
                Stats = new BuildStats(TimeSpan.Zero, 0, 0, 0, 0, false),
                Artifacts = new BuildArtifacts
                {
                    NativeLibraryPath = Path.Join(outputDir, manifest.NativeLib.Replace('/', Path.DirectorySeparatorChar)),
                    AssemblyPath = Path.Join(outputDir, manifest.ManagedAssembly.Replace('/', Path.DirectorySeparatorChar)),
                },
                Manifest = manifest,
            };
        }

        private static string WriteFile(string path, string content)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, content);
            return path;
        }

        private static string HashFile(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        private static string NormalizeTimestamp(string json)
        {
            JsonNode root = JsonNode.Parse(json)!;
            root["buildTimestamp"] = "__timestamp__";
            return root.ToJsonString(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            });
        }

        private static ImmutableArray<string> ReadStringArray(JsonElement element)
        {
            return element
                .EnumerateArray()
                .Select(static item => item.GetString()!)
                .ToImmutableArray();
        }

        private sealed record FixtureArtifacts(BuildArtifacts Artifacts, string GeneratedPath);
    }
}
