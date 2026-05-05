using System.Text.Json;
using System.Text.Json.Nodes;
using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class DeterminismHardeningTests
    {
        [Fact]
        public void GeneratedOutputs_RepeatedRunsWithShuffledInputs_AreByteStable()
        {
            ImmutableArray<ViewModelSchema> orderedSchemas = BuildTestFixtures.CreateNSchemas(3);
            ImmutableArray<ViewModelSchema> shuffledSchemas = ImmutableArray.Create(
                orderedSchemas[2],
                orderedSchemas[0],
                orderedSchemas[1]);
            CppGenerationOptions options = BuildTestFixtures.CreateDefaultCppOptions("dist");
            CppCodeGenerator cppGenerator = new();
            QmldirGenerator qmldirGenerator = new();
            QmltypesGenerator qmltypesGenerator = new();
            ImmutableArray<string> qmlFiles = orderedSchemas
                .Select(static schema => schema.CompilerSlotKey[..schema.CompilerSlotKey.IndexOf("::", StringComparison.Ordinal)] + ".qml")
                .Reverse()
                .ToImmutableArray();

            CppGenerationResult firstCpp = cppGenerator.Generate(orderedSchemas, options);
            CppGenerationResult secondCpp = cppGenerator.Generate(shuffledSchemas, options);
            string firstQmldir = qmldirGenerator.Generate("QmlSharp.MyApp", new QmlVersion(1, 0), orderedSchemas, qmlFiles);
            string secondQmldir = qmldirGenerator.Generate("QmlSharp.MyApp", new QmlVersion(1, 0), shuffledSchemas, qmlFiles.Reverse().ToImmutableArray());
            string firstQmltypes = qmltypesGenerator.Generate("QmlSharp.MyApp", new QmlVersion(1, 0), orderedSchemas);
            string secondQmltypes = qmltypesGenerator.Generate("QmlSharp.MyApp", new QmlVersion(1, 0), shuffledSchemas);

            Assert.Equal(
                firstCpp.Files.OrderBy(static file => file.Key, StringComparer.Ordinal),
                secondCpp.Files.OrderBy(static file => file.Key, StringComparer.Ordinal));
            Assert.Equal(firstQmldir, secondQmldir);
            Assert.Equal(firstQmltypes, secondQmltypes);
        }

        [Fact]
        public void ManifestJson_FileHashesAreSortedAndUsePortablePaths()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(ManifestJson_FileHashesAreSortedAndUsePortablePaths));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ProductLayout layout = new();
            BuildArtifacts artifacts = CreateArtifacts(project.Path);
            ProductAssemblyResult result = layout.Assemble(context, artifacts);
            BuildResult buildResult = new()
            {
                Success = true,
                PhaseResults = ImmutableArray<PhaseResult>.Empty,
                Diagnostics = ImmutableArray<BuildDiagnostic>.Empty,
                Stats = new BuildStats(TimeSpan.Zero, 0, 0, 0, 0, false),
                Artifacts = result.Manifest is null
                    ? artifacts
                    : new BuildArtifacts
                    {
                        NativeLibraryPath = Path.Join(context.OutputDir, result.Manifest.NativeLib.Replace('/', Path.DirectorySeparatorChar)),
                        AssemblyPath = Path.Join(context.OutputDir, result.Manifest.ManagedAssembly.Replace('/', Path.DirectorySeparatorChar)),
                    },
                Manifest = result.Manifest,
            };

            string json = NormalizeTimestamp(layout.GenerateManifest(buildResult, context));
            JsonNode root = JsonNode.Parse(json)!;
            JsonObject hashes = root["fileHashes"]!.AsObject();
            ImmutableArray<string> keys = hashes
                .Select(static pair => pair.Key)
                .ToImmutableArray();

            Assert.Equal(keys.Order(StringComparer.Ordinal), keys);
            Assert.DoesNotContain(keys, static key => key.Contains('\\', StringComparison.Ordinal));
            Assert.Contains("assets/images/icon.png", keys);
            Assert.Contains("qml/QmlSharp/MyApp/CounterView.qml", keys);
        }

        private static BuildArtifacts CreateArtifacts(string projectDir)
        {
            string root = Path.Join(projectDir, "artifact-inputs");
            string qmlPath = WriteFile(Path.Join(root, "qml", "QmlSharp", "MyApp", "CounterView.qml"), "Item {}\n");
            string schemaPath = WriteFile(
                Path.Join(root, "schemas", "CounterViewModel.schema.json"),
                new ViewModelSchemaSerializer().Serialize(BuildTestFixtures.CreateCounterSchema()));
            string qmldirPath = WriteFile(Path.Join(root, "qml", "QmlSharp", "MyApp", "qmldir"), "module QmlSharp.MyApp\n");
            string qmltypesPath = WriteFile(Path.Join(root, "qml", "QmlSharp", "MyApp", "qmlsharp_myapp.qmltypes"), "Module {}\n");
            string eventBindingsPath = WriteFile(Path.Join(root, "event-bindings.json"), "{\"commands\":[],\"effects\":[]}\n");
            string sourceMapPath = WriteFile(Path.Join(root, "source-maps", "CounterView.qml.map"), "{}\n");
            string assetPath = WriteFile(Path.Join(root, "assets", "images", "icon.png"), "asset");
            string nativePath = WriteFile(Path.Join(root, "native", NativeLibraryNames.GetFileName("qmlsharp_native")), "native");
            string assemblyPath = WriteFile(Path.Join(root, "managed", "MyApp.dll"), "managed");

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
    }
}
