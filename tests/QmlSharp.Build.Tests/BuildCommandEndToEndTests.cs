using System.Text.Json;
using QmlSharp.Build.Tests.Infrastructure;
using CompilerDiagnosticSeverity = QmlSharp.Compiler.DiagnosticSeverity;

namespace QmlSharp.Build.Tests
{
    public sealed class BuildCommandEndToEndTests
    {
        [Fact]
        public async Task BC01_BuildValidCounterProject_ProducesHumanOutputAndArtifacts()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("bc01-build-counter");
            WriteProjectFile(project.Path);
            QmlSharpConfig config = CreateCommandConfig(project.Path);
            MockCompiler compiler = CreateCounterCompiler();
            MockQmlFormat formatter = new();
            MockQmlLint linter = new();
            BuildPipeline pipeline = CreateCommandPipeline(compiler, formatter, linter);
            CapturingCommandOutput output = new();
            BuildCommand command = new(new FakeConfigLoader(config), pipeline, output);

            int exitCode = await command.ExecuteAsync(new BuildCommandOptions
            {
                ProjectDir = project.Path,
            });

            string outputDir = config.OutDir;
            Assert.Equal(CliExitCode.Success, exitCode);
            Assert.Contains("Build completed.", output.Stdout, StringComparison.Ordinal);
            Assert.Equal(string.Empty, output.Stderr);
            Assert.Equal(1, compiler.CompileCallCount);
            Assert.Equal(1, compiler.WriteOutputCallCount);
            Assert.Equal(1, formatter.FormatBatchCallCount);
            Assert.Equal(1, linter.LintBatchCallCount);
            Assert.True(File.Exists(Path.Join(outputDir, "manifest.json")));
            Assert.True(File.Exists(Path.Join(outputDir, "qml", "QmlSharp", "MyApp", "CounterView.qml")));
            Assert.True(File.Exists(Path.Join(outputDir, "schemas", "CounterViewModel.schema.json")));
            Assert.True(File.Exists(Path.Join(outputDir, "native", NativeLibraryNames.GetFileName("qmlsharp_native"))));
            Assert.True(File.Exists(Path.Join(outputDir, "managed", "MyApp.dll")));
        }

        [Fact]
        public async Task BC04_BuildJson_WritesMachineReadableEnvelopeFromRealFormatter()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("bc04-json-e2e");
            WriteProjectFile(project.Path);
            QmlSharpConfig config = CreateCommandConfig(project.Path);
            MockCompiler compiler = CreateCounterCompiler();
            BuildPipeline pipeline = CreateCommandPipeline(compiler, new MockQmlFormat(), new MockQmlLint());
            CapturingCommandOutput output = new();
            BuildCommand command = new(new FakeConfigLoader(config), pipeline, output);

            int exitCode = await command.ExecuteAsync(new BuildCommandOptions
            {
                ProjectDir = project.Path,
                Json = true,
            });

            Assert.Equal(CliExitCode.Success, exitCode);
            using JsonDocument document = JsonDocument.Parse(output.Stdout);
            JsonElement root = document.RootElement;
            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal("success", root.GetProperty("status").GetString());
            Assert.Equal(CliExitCode.Success, root.GetProperty("exitCode").GetInt32());
            Assert.Equal("build", root.GetProperty("command").GetString());
            Assert.Equal(1, root.GetProperty("stats").GetProperty("filesCompiled").GetInt32());
            Assert.Equal(1, root.GetProperty("stats").GetProperty("schemasGenerated").GetInt32());
            Assert.Equal(string.Empty, output.Stderr);
        }

        [Fact]
        public async Task BC05_LibraryBuild_WritesModuleManifestWithoutApplicationArtifacts()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("bc05-library-e2e");
            WriteProjectFile(project.Path);
            QmlSharpConfig config = CreateCommandConfig(project.Path) with
            {
                Entry = null,
            };
            MockCompiler compiler = CreateCounterCompiler();
            BuildPipeline pipeline = CreateCommandPipeline(compiler, new MockQmlFormat(), new MockQmlLint());
            CapturingCommandOutput output = new();
            BuildCommand command = new(new FakeConfigLoader(config), pipeline, output);

            int exitCode = await command.ExecuteAsync(new BuildCommandOptions
            {
                ProjectDir = project.Path,
                Library = true,
            });

            string libraryDir = Path.Join(project.Path, "lib");
            Assert.Equal(CliExitCode.Success, exitCode);
            Assert.True(File.Exists(Path.Join(libraryDir, "qmlsharp.module.json")));
            Assert.False(File.Exists(Path.Join(libraryDir, "manifest.json")));
            Assert.False(File.Exists(Path.Join(libraryDir, "event-bindings.json")));
            Assert.False(Directory.Exists(Path.Join(libraryDir, "native")));
            Assert.False(Directory.Exists(Path.Join(libraryDir, "managed")));
        }

        [Fact]
        public async Task BC07_InvalidConfig_WritesConfigErrorToStderrAndReturnsTwo()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("bc07-invalid-config");
            BuildDiagnostic diagnostic = new(
                BuildDiagnosticCode.ConfigValidationError,
                BuildDiagnosticSeverity.Error,
                "module.prefix: Module prefix must not be empty.",
                BuildPhase.ConfigLoading,
                "module.prefix");
            CapturingCommandOutput output = new();
            BuildCommand command = new(
                new ThrowingConfigLoader(new ConfigParseException(diagnostic)),
                new BuildPipeline(CreateSuccessfulRecordingStages()),
                output);

            int exitCode = await command.ExecuteAsync(new BuildCommandOptions
            {
                ProjectDir = project.Path,
            });

            Assert.Equal(CliExitCode.ConfigOrCommandError, exitCode);
            Assert.Equal(string.Empty, output.Stdout);
            Assert.Contains("module.prefix", output.Stderr, StringComparison.Ordinal);
            Assert.Contains(BuildDiagnosticCode.ConfigValidationError, output.Stderr, StringComparison.Ordinal);
        }

        [Fact]
        public async Task BC08_CompilationErrors_WriteHumanReadableDiagnosticAndReturnOne()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("bc08-compile-error");
            WriteProjectFile(project.Path);
            QmlSharpConfig config = CreateCommandConfig(project.Path);
            CompilerDiagnostic compilerDiagnostic = new(
                DiagnosticCodes.InvalidStateAttribute,
                CompilerDiagnosticSeverity.Error,
                "State attribute usage is invalid.",
                SourceLocation.FileOnly(Path.Join(project.Path, "CounterViewModel.cs")),
                "extract");
            MockCompiler compiler = new()
            {
                CompilationResult = CompilationResult.FromUnits(
                    ImmutableArray<CompilationUnit>.Empty,
                    ImmutableArray.Create(compilerDiagnostic)),
            };
            BuildPipeline pipeline = CreateCommandPipeline(compiler, new MockQmlFormat(), new MockQmlLint());
            CapturingCommandOutput output = new();
            BuildCommand command = new(new FakeConfigLoader(config), pipeline, output);

            int exitCode = await command.ExecuteAsync(new BuildCommandOptions
            {
                ProjectDir = project.Path,
            });

            Assert.Equal(CliExitCode.BuildError, exitCode);
            Assert.Equal(string.Empty, output.Stdout);
            Assert.Contains("Build failed.", output.Stderr, StringComparison.Ordinal);
            Assert.Contains(BuildDiagnosticCode.CompilationFailed, output.Stderr, StringComparison.Ordinal);
            Assert.Contains(DiagnosticCodes.InvalidStateAttribute, output.Stderr, StringComparison.Ordinal);
            Assert.Equal(0, compiler.WriteOutputCallCount);
        }

        private static BuildPipeline CreateCommandPipeline(
            MockCompiler compiler,
            MockQmlFormat formatter,
            MockQmlLint linter)
        {
            return new BuildPipeline(ImmutableArray.Create<IBuildStage>(
                new StaticBuildStage(BuildPhase.ConfigLoading, BuildStageResult.Succeeded()),
                new CSharpCompilationBuildStage(compiler),
                new ModuleMetadataBuildStage(),
                new PackageResolutionBuildStage(new EmptyPackageResolver()),
                new ResourceBundlingBuildStage(),
                new QmlValidationBuildStage(formatter, linter, new EmptyPackageResolver()),
                new ApplicationRuntimeArtifactStage(),
                new OutputAssemblyBuildStage()));
        }

        private static ImmutableArray<IBuildStage> CreateSuccessfulRecordingStages()
        {
            return ImmutableArray.Create<IBuildStage>(
                new StaticBuildStage(BuildPhase.ConfigLoading, BuildStageResult.Succeeded()),
                new StaticBuildStage(BuildPhase.CSharpCompilation, BuildStageResult.Succeeded()),
                new StaticBuildStage(BuildPhase.ModuleMetadata, BuildStageResult.Succeeded()),
                new StaticBuildStage(BuildPhase.DependencyResolution, BuildStageResult.Succeeded()),
                new StaticBuildStage(BuildPhase.AssetBundling, BuildStageResult.Succeeded()),
                new StaticBuildStage(BuildPhase.QmlValidation, BuildStageResult.Succeeded()),
                new StaticBuildStage(BuildPhase.CppCodeGenAndBuild, BuildStageResult.Succeeded()),
                new StaticBuildStage(BuildPhase.OutputAssembly, BuildStageResult.Succeeded()));
        }

        private static QmlSharpConfig CreateCommandConfig(string projectDir)
        {
            QmlSharpConfig defaults = BuildTestFixtures.CreateDefaultConfig();
            return defaults with
            {
                OutDir = Path.Join(projectDir, "dist"),
                Qt = defaults.Qt with
                {
                    Dir = GetQtDir(),
                },
            };
        }

        private static string GetQtDir()
        {
            return Environment.GetEnvironmentVariable("QT_DIR") ??
                BuildTestFixtures.CreateDefaultConfig().Qt.Dir ??
                "C:/Qt/6.11.0/msvc2022_64";
        }

        private static MockCompiler CreateCounterCompiler()
        {
            CompilationUnit unit = new()
            {
                SourceFilePath = "CounterView.cs",
                ViewClassName = "CounterView",
                ViewModelClassName = "CounterViewModel",
                QmlText = "import QtQuick\nItem {}\n",
                Schema = BuildTestFixtures.CreateCounterSchema(),
            };
            MockCompiler compiler = new()
            {
                CompilationResult = CompilationResult.FromUnits(ImmutableArray.Create(unit)),
            };
            compiler.WriteOutputHandler = static (result, options) => WriteCounterCompilerArtifacts(options);
            return compiler;
        }

        private static OutputResult WriteCounterCompilerArtifacts(CompilerOptions options)
        {
            string qmlDir = ModuleMetadataPaths.GetModuleDirectory(options.OutputDir, options.ModuleUriPrefix);
            string schemaDir = Path.Join(options.OutputDir, "schemas");
            string sourceMapDir = options.SourceMapDir ?? Path.Join(options.OutputDir, "source-maps");
            _ = Directory.CreateDirectory(qmlDir);
            _ = Directory.CreateDirectory(schemaDir);
            _ = Directory.CreateDirectory(sourceMapDir);

            string qmlPath = Path.Join(qmlDir, "CounterView.qml");
            string schemaPath = Path.Join(schemaDir, "CounterViewModel.schema.json");
            string eventBindingsPath = Path.Join(options.OutputDir, "event-bindings.json");
            string sourceMapPath = Path.Join(sourceMapDir, "CounterView.qml.map");
            File.WriteAllText(qmlPath, "import QtQuick\nItem {}\n");
            File.WriteAllText(schemaPath, new ViewModelSchemaSerializer().Serialize(BuildTestFixtures.CreateCounterSchema()));
            File.WriteAllText(eventBindingsPath, "{}\n");
            File.WriteAllText(sourceMapPath, "{}\n");

            return new OutputResult(
                ImmutableArray.Create(qmlPath),
                ImmutableArray.Create(schemaPath),
                eventBindingsPath,
                ImmutableArray.Create(sourceMapPath),
                TotalBytes: 4);
        }

        private static void WriteProjectFile(string projectDir)
        {
            File.WriteAllText(
                Path.Join(projectDir, "Counter.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
        }

        private sealed class CapturingCommandOutput : ICommandOutput
        {
            private readonly StringWriter stdout = new();
            private readonly StringWriter stderr = new();

            public string Stdout => stdout.ToString();

            public string Stderr => stderr.ToString();

            public void WriteLine(string value)
            {
                stdout.WriteLine(value);
            }

            public void WriteErrorLine(string value)
            {
                stderr.WriteLine(value);
            }
        }

        private sealed class FakeConfigLoader : IConfigLoader
        {
            private readonly QmlSharpConfig config;

            public FakeConfigLoader(QmlSharpConfig config)
            {
                this.config = config;
            }

            public QmlSharpConfig Load(string projectDir)
            {
                return config;
            }

            public ImmutableArray<ConfigDiagnostic> Validate(QmlSharpConfig loadedConfig)
            {
                return ImmutableArray<ConfigDiagnostic>.Empty;
            }

            public QmlSharpConfig GetDefaults()
            {
                return config;
            }
        }

        private sealed class ThrowingConfigLoader : IConfigLoader
        {
            private readonly ConfigParseException exception;

            public ThrowingConfigLoader(ConfigParseException exception)
            {
                this.exception = exception;
            }

            public QmlSharpConfig Load(string projectDir)
            {
                throw exception;
            }

            public ImmutableArray<ConfigDiagnostic> Validate(QmlSharpConfig config)
            {
                return ImmutableArray<ConfigDiagnostic>.Empty;
            }

            public QmlSharpConfig GetDefaults()
            {
                return BuildTestFixtures.CreateDefaultConfig();
            }
        }

        private sealed class EmptyPackageResolver : IPackageResolver
        {
            public ImmutableArray<ResolvedPackage> Resolve(string projectDir)
            {
                return ImmutableArray<ResolvedPackage>.Empty;
            }

            public ImmutableArray<string> CollectImportPaths(ImmutableArray<ResolvedPackage> packages)
            {
                return ImmutableArray<string>.Empty;
            }

            public ImmutableArray<string> CollectSchemas(ImmutableArray<ResolvedPackage> packages)
            {
                return ImmutableArray<string>.Empty;
            }
        }

        private sealed class StaticBuildStage : IBuildStage
        {
            private readonly BuildStageResult result;

            public StaticBuildStage(BuildPhase phase, BuildStageResult result)
            {
                Phase = phase;
                this.result = result;
            }

            public BuildPhase Phase { get; }

            public Task<BuildStageResult> ExecuteAsync(BuildContext context, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(result);
            }
        }

        private sealed class ApplicationRuntimeArtifactStage : IBuildStage
        {
            public BuildPhase Phase => BuildPhase.CppCodeGenAndBuild;

            public Task<BuildStageResult> ExecuteAsync(BuildContext context, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (context.LibraryMode || context.DryRun)
                {
                    return Task.FromResult(BuildStageResult.Succeeded());
                }

                string nativePath = Path.Join(context.OutputDir, "native", NativeLibraryNames.GetFileName("qmlsharp_native"));
                string managedPath = Path.Join(context.OutputDir, "managed", "MyApp.dll");
                string nativeDirectory = Path.GetDirectoryName(nativePath) ??
                    throw new InvalidOperationException("Native artifact path must include a directory.");
                string managedDirectory = Path.GetDirectoryName(managedPath) ??
                    throw new InvalidOperationException("Managed artifact path must include a directory.");
                _ = Directory.CreateDirectory(nativeDirectory);
                _ = Directory.CreateDirectory(managedDirectory);
                File.WriteAllText(nativePath, "native");
                File.WriteAllText(managedPath, "managed");
                return Task.FromResult(BuildStageResult.Succeeded(
                    new BuildStatsDelta
                    {
                        CppFilesGenerated = 1,
                        NativeLibBuilt = true,
                    },
                    new BuildArtifacts
                    {
                        NativeLibraryPath = nativePath,
                        AssemblyPath = managedPath,
                    }));
            }
        }
    }
}
