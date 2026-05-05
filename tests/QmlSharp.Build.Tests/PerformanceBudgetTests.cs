using System.Diagnostics;
using System.Globalization;
using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class PerformanceBudgetTests
    {
        [Fact]
        [Trait("Category", BuildTestCategories.Performance)]
        public void PF01_ConfigLoadingAndValidation_CompletesUnder100ms()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(PF01_ConfigLoadingAndValidation_CompletesUnder100ms));
            string qtDir = Path.Join(project.Path, "qt");
            _ = Directory.CreateDirectory(qtDir);
            WriteConfig(project.Path, qtDir);
            ConfigLoader loader = new(new StaticQtToolchain(qtDir));
            _ = loader.Load(project.Path);

            Stopwatch stopwatch = Stopwatch.StartNew();
            QmlSharpConfig config = loader.Load(project.Path);
            ImmutableArray<ConfigDiagnostic> diagnostics = loader.Validate(config);
            stopwatch.Stop();

            Assert.Empty(diagnostics);
            AssertWithinBudget("PF-01 config loading + validation", stopwatch.Elapsed, TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        [Trait("Category", BuildTestCategories.Performance)]
        public void PF02_CppCodeGenerationForTenSchemas_CompletesUnder200ms()
        {
            CppCodeGenerator generator = new();
            ImmutableArray<ViewModelSchema> schemas = BuildTestFixtures.CreateNSchemas(10);
            CppGenerationOptions options = BuildTestFixtures.CreateDefaultCppOptions("dist");
            _ = generator.Generate(schemas, options);

            Stopwatch stopwatch = Stopwatch.StartNew();
            CppGenerationResult result = generator.Generate(schemas, options);
            stopwatch.Stop();

            Assert.DoesNotContain(result.Diagnostics, static diagnostic =>
                diagnostic.Severity is BuildDiagnosticSeverity.Error or BuildDiagnosticSeverity.Fatal);
            AssertWithinBudget("PF-02 C++ code generation for 10 schemas", stopwatch.Elapsed, TimeSpan.FromMilliseconds(200));
        }

        [Fact]
        [Trait("Category", BuildTestCategories.Performance)]
        public void PF03_CMakeListsGeneration_CompletesUnder50ms()
        {
            CppCodeGenerator generator = new();
            ImmutableArray<ViewModelSchema> schemas = BuildTestFixtures.CreateNSchemas(10);
            CppGenerationOptions options = BuildTestFixtures.CreateDefaultCppOptions("dist");
            _ = generator.GenerateCMakeLists(schemas, options);

            Stopwatch stopwatch = Stopwatch.StartNew();
            string cmake = generator.GenerateCMakeLists(schemas, options);
            stopwatch.Stop();

            Assert.Contains("add_library", cmake, StringComparison.Ordinal);
            AssertWithinBudget("PF-03 CMakeLists generation", stopwatch.Elapsed, TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        [Trait("Category", BuildTestCategories.Performance)]
        public void PF04_QmldirGenerationForSingleModule_CompletesUnder10ms()
        {
            QmldirGenerator generator = new();
            ImmutableArray<ViewModelSchema> schemas = BuildTestFixtures.CreateNSchemas(10);
            ImmutableArray<string> qmlFiles = schemas
                .Select(static schema => schema.CompilerSlotKey[..schema.CompilerSlotKey.IndexOf("::", StringComparison.Ordinal)] + ".qml")
                .ToImmutableArray();
            _ = generator.Generate("QmlSharp.MyApp", new QmlVersion(1, 0), schemas, qmlFiles);

            Stopwatch stopwatch = Stopwatch.StartNew();
            string qmldir = generator.Generate("QmlSharp.MyApp", new QmlVersion(1, 0), schemas, qmlFiles);
            stopwatch.Stop();

            Assert.Contains("module QmlSharp.MyApp", qmldir, StringComparison.Ordinal);
            AssertWithinBudget("PF-04 qmldir generation", stopwatch.Elapsed, TimeSpan.FromMilliseconds(10));
        }

        [Fact]
        [Trait("Category", BuildTestCategories.Performance)]
        public void PF05_QmltypesGenerationForSingleModule_CompletesUnder50ms()
        {
            QmltypesGenerator generator = new();
            ImmutableArray<ViewModelSchema> schemas = BuildTestFixtures.CreateNSchemas(10);
            _ = generator.Generate("QmlSharp.MyApp", new QmlVersion(1, 0), schemas);

            Stopwatch stopwatch = Stopwatch.StartNew();
            string qmltypes = generator.Generate("QmlSharp.MyApp", new QmlVersion(1, 0), schemas);
            stopwatch.Stop();

            Assert.Contains("Module", qmltypes, StringComparison.Ordinal);
            AssertWithinBudget("PF-05 qmltypes generation", stopwatch.Elapsed, TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        [Trait("Category", BuildTestCategories.Performance)]
        public void PF06_AssetBundlingForOneHundredFiles_CompletesUnderOneSecond()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(PF06_AssetBundlingForOneHundredFiles_CompletesUnderOneSecond));
            string assetsRoot = Path.Join(project.Path, "assets");
            for (int index = 0; index < 100; index++)
            {
                _ = WriteFile(Path.Join(assetsRoot, "images", "asset-" + index.ToString("D3", CultureInfo.InvariantCulture) + ".png"), "asset");
            }

            ResourceBundler bundler = new(
                project.Path,
                ImmutableArray.Create("assets"),
                reportMissingRoots: true,
                generateQrcOnBundle: true);
            QmlSharpConfig config = BuildTestFixtures.CreateDefaultConfig();
            _ = bundler.CollectWithDiagnostics(config);

            Stopwatch stopwatch = Stopwatch.StartNew();
            ResourceCollectionResult collection = bundler.CollectWithDiagnostics(config);
            ResourceBundleResult bundle = bundler.Bundle(collection.Resources, Path.Join(project.Path, "dist"));
            stopwatch.Stop();

            Assert.Empty(collection.Diagnostics);
            Assert.Empty(bundle.Diagnostics);
            Assert.Equal(100, bundle.FilesCopied);
            AssertWithinBudget("PF-06 asset bundling for 100 files", stopwatch.Elapsed, TimeSpan.FromSeconds(1));
        }

        [Fact]
        [Trait("Category", BuildTestCategories.Performance)]
        public void PF07_ProductLayoutAssembly_CompletesUnder500ms()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(PF07_ProductLayoutAssembly_CompletesUnder500ms));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ProductLayout layout = new();
            BuildArtifacts artifacts = CreateProductLayoutArtifacts(project.Path);
            _ = layout.Assemble(context, artifacts);
            Directory.Delete(context.OutputDir, recursive: true);

            Stopwatch stopwatch = Stopwatch.StartNew();
            ProductAssemblyResult result = layout.Assemble(context, artifacts);
            stopwatch.Stop();

            Assert.True(result.Success);
            AssertWithinBudget("PF-07 product layout assembly", stopwatch.Elapsed, TimeSpan.FromMilliseconds(500));
        }

        [Fact]
        [Trait("Category", BuildTestCategories.Performance)]
        public async Task CleanArtifacts_CompletesUnder500ms()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(CleanArtifacts_CompletesUnder500ms));
            WriteCleanConfig(project.Path);
            string distDirectory = Path.Join(project.Path, "dist");
            string cacheDirectory = Path.Join(project.Path, ".compiler-cache");
            for (int index = 0; index < 100; index++)
            {
                _ = WriteFile(Path.Join(distDirectory, "file-" + index.ToString("D3", CultureInfo.InvariantCulture) + ".txt"), "dist");
                _ = WriteFile(Path.Join(cacheDirectory, "cache-" + index.ToString("D3", CultureInfo.InvariantCulture) + ".json"), "{}");
            }

            CleanService service = new();

            Stopwatch stopwatch = Stopwatch.StartNew();
            CommandServiceResult result = await service.CleanAsync(new CleanCommandOptions
            {
                Cache = true,
                ProjectDir = project.Path,
            });
            stopwatch.Stop();

            Assert.True(result.Success);
            Assert.False(Directory.Exists(distDirectory));
            Assert.False(Directory.Exists(cacheDirectory));
            AssertWithinBudget("clean artifacts", stopwatch.Elapsed, TimeSpan.FromMilliseconds(500));
        }

        private static BuildArtifacts CreateProductLayoutArtifacts(string projectDir)
        {
            string artifactRoot = Path.Join(projectDir, "inputs");
            string qmlPath = WriteFile(
                Path.Join(artifactRoot, "qml", "QmlSharp", "MyApp", "CounterView.qml"),
                "import QtQuick\nItem {}\n");
            string schemaPath = WriteFile(
                Path.Join(artifactRoot, "schemas", "CounterViewModel.schema.json"),
                new ViewModelSchemaSerializer().Serialize(BuildTestFixtures.CreateCounterSchema()));
            string qmldirPath = WriteFile(
                Path.Join(artifactRoot, "qml", "QmlSharp", "MyApp", "qmldir"),
                "module QmlSharp.MyApp\n");
            string qmltypesPath = WriteFile(
                Path.Join(artifactRoot, "qml", "QmlSharp", "MyApp", "qmlsharp_myapp.qmltypes"),
                "Module {}\n");
            string eventBindingsPath = WriteFile(Path.Join(artifactRoot, "event-bindings.json"), "{\"commands\":[],\"effects\":[]}\n");
            string sourceMapPath = WriteFile(Path.Join(artifactRoot, "source-maps", "CounterView.qml.map"), "{}\n");
            string assetPath = WriteFile(Path.Join(artifactRoot, "assets", "images", "icon.png"), "asset");
            _ = WriteFile(Path.Join(artifactRoot, "native", "generated", "CounterViewModel.cpp"), "// generated\n");
            string nativePath = WriteFile(Path.Join(artifactRoot, "native", NativeLibraryNames.GetFileName("qmlsharp_native")), "native");
            string assemblyPath = WriteFile(Path.Join(artifactRoot, "managed", "MyApp.dll"), "managed");
            _ = WriteFile(Path.Join(artifactRoot, "managed", "MyApp.deps.json"), "{}\n");

            return new BuildArtifacts
            {
                QmlFiles = ImmutableArray.Create(qmlPath),
                SchemaFiles = ImmutableArray.Create(schemaPath),
                EventBindingsFile = eventBindingsPath,
                SourceMapFiles = ImmutableArray.Create(sourceMapPath),
                ModuleMetadataFiles = ImmutableArray.Create(qmldirPath, qmltypesPath),
                AssetFiles = ImmutableArray.Create(assetPath),
                NativeLibraryPath = nativePath,
                AssemblyPath = assemblyPath,
            };
        }

        private static void AssertWithinBudget(string operation, TimeSpan elapsed, TimeSpan budget)
        {
            Assert.True(elapsed < budget, $"{operation} took {elapsed.TotalMilliseconds:F2} ms; budget is {budget.TotalMilliseconds:F2} ms.");
        }

        private static void WriteConfig(string projectDir, string qtDir)
        {
            _ = WriteFile(
                Path.Join(projectDir, "qmlsharp.json"),
                $$"""
                {
                  "entry": "./src/Program.cs",
                  "qt": { "dir": "{{qtDir.Replace('\\', '/')}}" },
                  "module": { "prefix": "QmlSharp.Perf" }
                }
                """);
        }

        private static void WriteCleanConfig(string projectDir)
        {
            _ = WriteFile(
                Path.Join(projectDir, "qmlsharp.json"),
                """
                {
                  "outDir": "./dist",
                  "qt": { "dir": null },
                  "module": { "prefix": "QmlSharp.CleanPerf" }
                }
                """);
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

        private sealed class StaticQtToolchain : IQtToolchain
        {
            private readonly string qtDir;

            public StaticQtToolchain(string qtDir)
            {
                this.qtDir = qtDir;
            }

            public QtInstallation? Installation { get; private set; }

            public Task<QtInstallation> DiscoverAsync(QtToolchainConfig? config = null, CancellationToken ct = default)
            {
                Installation = CreateInstallation();
                return Task.FromResult(Installation);
            }

            public Task<ToolAvailability> CheckToolsAsync(CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }

            public Task<ToolInfo> GetToolInfoAsync(string toolName, CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }

            private QtInstallation CreateInstallation()
            {
                return new QtInstallation
                {
                    RootDir = qtDir,
                    BinDir = Path.Join(qtDir, "bin"),
                    QmlDir = Path.Join(qtDir, "qml"),
                    LibDir = Path.Join(qtDir, "lib"),
                    Version = new QtVersion
                    {
                        Major = 6,
                        Minor = 11,
                        Patch = 0,
                    },
                    Platform = "mock",
                };
            }
        }
    }
}
