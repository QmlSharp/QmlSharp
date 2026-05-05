using QmlSharp.Build.Tests.Infrastructure;
using CompilerDiagnosticSeverity = QmlSharp.Compiler.DiagnosticSeverity;
using QtDiagnosticSeverity = QmlSharp.Qt.Tools.DiagnosticSeverity;

namespace QmlSharp.Build.Tests
{
    public sealed class BuildStageEndToEndTests
    {
        [Fact]
        public async Task BC02_CSharpCompilationStage_ForceDisablesIncrementalCompilerOptions()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("bc02-force");
            WriteProjectFile(project.Path);
            MockCompiler compiler = CreateSuccessfulCompiler(project.Path);
            CSharpCompilationBuildStage stage = new(compiler);
            BuildContext context = CreateContext(project.Path) with
            {
                ForceRebuild = true,
            };

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(compiler.LastOptions);
            Assert.False(compiler.LastOptions.Incremental);
            Assert.True(compiler.LastOptions.GenerateSourceMaps);
            Assert.False(compiler.LastOptions.FormatQml);
            Assert.False(compiler.LastOptions.LintQml);
            Assert.Equal(Path.Join(context.OutputDir, "source-maps"), compiler.LastOptions.SourceMapDir);
        }

        [Fact]
        public async Task BC06_CSharpCompilationStage_FilesOptionBecomesCompilerIncludePattern()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("bc06-files");
            WriteProjectFile(project.Path);
            MockCompiler compiler = CreateSuccessfulCompiler(project.Path);
            CSharpCompilationBuildStage stage = new(compiler);
            BuildContext context = CreateContext(project.Path) with
            {
                FileFilter = "Counter*.cs",
            };

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(compiler.LastOptions);
            Assert.Equal("Counter*.cs", Assert.Single(compiler.LastOptions.IncludePatterns));
        }

        [Fact]
        public async Task CSharpCompilationStage_MapsCompilerDiagnosticsToBuildDiagnostics()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("compile-diagnostic");
            WriteProjectFile(project.Path);
            CompilerDiagnostic compilerDiagnostic = new(
                DiagnosticCodes.UnsupportedStateType,
                CompilerDiagnosticSeverity.Error,
                "Unsupported state type.",
                SourceLocation.FileOnly(Path.Join(project.Path, "CounterViewModel.cs")),
                "extract");
            MockCompiler compiler = new()
            {
                CompilationResult = CompilationResult.FromUnits(
                    ImmutableArray<CompilationUnit>.Empty,
                    ImmutableArray.Create(compilerDiagnostic)),
            };
            CSharpCompilationBuildStage stage = new(compiler);

            BuildStageResult result = await stage.ExecuteAsync(CreateContext(project.Path), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(0, compiler.WriteOutputCallCount);
            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCode.CompilationFailed, diagnostic.Code);
            Assert.Equal(BuildDiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Contains(DiagnosticCodes.UnsupportedStateType, diagnostic.Message, StringComparison.Ordinal);
            Assert.Equal(BuildPhase.CSharpCompilation, diagnostic.Phase);
        }

        [Fact]
        public async Task CSharpCompilationStage_MapsSchemaOutputDiagnosticsToBuildDiagnostics()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("schema-diagnostic");
            WriteProjectFile(project.Path);
            CompilerDiagnostic outputDiagnostic = new(
                DiagnosticCodes.SchemaSerializationFailed,
                CompilerDiagnosticSeverity.Error,
                "Schema serialization failed.",
                SourceLocation.FileOnly(Path.Join(project.Path, "dist", "schemas", "CounterViewModel.schema.json")),
                "output");
            MockCompiler compiler = CreateSuccessfulCompiler(project.Path);
            compiler.OutputResult = new OutputResult(
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty,
                null,
                ImmutableArray<string>.Empty,
                0)
            {
                Diagnostics = ImmutableArray.Create(outputDiagnostic),
            };
            CSharpCompilationBuildStage stage = new(compiler);

            BuildStageResult result = await stage.ExecuteAsync(CreateContext(project.Path), CancellationToken.None);

            Assert.False(result.Success);
            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCode.SchemaGenerationFailed, diagnostic.Code);
            Assert.Contains(DiagnosticCodes.SchemaSerializationFailed, diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CSharpCompilationStage_NoSchemasProducesB011()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("no-schemas");
            WriteProjectFile(project.Path);
            MockCompiler compiler = CreateSuccessfulCompiler(project.Path);
            compiler.OutputResult = new OutputResult(
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty,
                null,
                ImmutableArray<string>.Empty,
                0);
            CSharpCompilationBuildStage stage = new(compiler);

            BuildStageResult result = await stage.ExecuteAsync(CreateContext(project.Path), CancellationToken.None);

            Assert.False(result.Success);
            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCode.NoViewModelsFound, diagnostic.Code);
        }

        [Fact]
        public async Task QmlValidationStage_MapsQmllintErrorsToB060()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("b060-qmllint");
            string qmlPath = WriteQmlFile(project.Path);
            MockQmlLint linter = new()
            {
                BatchResults = ImmutableArray.Create(new QmlLintResult
                {
                    ToolResult = MockQtToolServices.CreateSuccessfulToolResult("qmllint " + qmlPath),
                    Diagnostics = ImmutableArray.Create(new QtDiagnostic
                    {
                        File = qmlPath,
                        Severity = QtDiagnosticSeverity.Error,
                        Message = "Invalid binding.",
                    }),
                    ErrorCount = 1,
                    WarningCount = 0,
                    InfoCount = 0,
                }),
            };
            QmlValidationBuildStage stage = new(new MockQmlFormat(), linter, new EmptyPackageResolver());

            BuildStageResult result = await stage.ExecuteAsync(CreateContext(project.Path), CancellationToken.None);

            Assert.False(result.Success);
            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCode.QmlLintError, diagnostic.Code);
            Assert.Equal(BuildDiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(qmlPath, diagnostic.FilePath);
        }

        [Fact]
        public async Task QmlValidationStage_MapsQmlformatErrorsToB061()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("b061-qmlformat");
            string qmlPath = WriteQmlFile(project.Path);
            MockQmlFormat formatter = new()
            {
                BatchResults = ImmutableArray.Create(new QmlFormatResult
                {
                    ToolResult = MockQtToolServices.CreateSuccessfulToolResult("qmlformat " + qmlPath) with
                    {
                        ExitCode = 1,
                        Stderr = "format failed",
                    },
                    Diagnostics = ImmutableArray.Create(new QtDiagnostic
                    {
                        File = qmlPath,
                        Severity = QtDiagnosticSeverity.Error,
                        Message = "Expected token.",
                    }),
                    HasChanges = false,
                }),
            };
            QmlValidationBuildStage stage = new(formatter, new MockQmlLint(), new EmptyPackageResolver());

            BuildStageResult result = await stage.ExecuteAsync(CreateContext(project.Path), CancellationToken.None);

            Assert.False(result.Success);
            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCode.QmlFormatError, diagnostic.Code);
            Assert.Equal(BuildDiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(qmlPath, diagnostic.FilePath);
        }

        [Fact]
        public async Task QmlValidationStage_QmllintWarningsDoNotFailBuild()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("qmllint-warning");
            string qmlPath = WriteQmlFile(project.Path);
            MockQmlLint linter = new()
            {
                BatchResults = ImmutableArray.Create(new QmlLintResult
                {
                    ToolResult = MockQtToolServices.CreateSuccessfulToolResult("qmllint " + qmlPath),
                    Diagnostics = ImmutableArray.Create(new QtDiagnostic
                    {
                        File = qmlPath,
                        Severity = QtDiagnosticSeverity.Warning,
                        Message = "Unused import.",
                    }),
                    ErrorCount = 0,
                    WarningCount = 1,
                    InfoCount = 0,
                }),
            };
            QmlValidationBuildStage stage = new(new MockQmlFormat(), linter, new EmptyPackageResolver());

            BuildStageResult result = await stage.ExecuteAsync(CreateContext(project.Path), CancellationToken.None);

            Assert.True(result.Success);
            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCode.QmlLintError, diagnostic.Code);
            Assert.Equal(BuildDiagnosticSeverity.Warning, diagnostic.Severity);
        }

        private static BuildContext CreateContext(string projectDir)
        {
            QmlSharpConfig defaults = BuildTestFixtures.CreateDefaultConfig();
            QmlSharpConfig config = defaults with
            {
                OutDir = Path.Join(projectDir, "dist"),
                Qt = defaults.Qt with
                {
                    Dir = Environment.GetEnvironmentVariable("QT_DIR") ?? defaults.Qt.Dir,
                },
            };

            return BuildTestFixtures.CreateDefaultContext(projectDir) with
            {
                Config = config,
                OutputDir = config.OutDir,
                QtDir = config.Qt.Dir ?? string.Empty,
            };
        }

        private static MockCompiler CreateSuccessfulCompiler(string projectDir)
        {
            string schemaPath = Path.Join(projectDir, "dist", "schemas", "CounterViewModel.schema.json");
            CompilationUnit unit = new()
            {
                SourceFilePath = Path.Join(projectDir, "CounterView.cs"),
                ViewClassName = "CounterView",
                ViewModelClassName = "CounterViewModel",
            };

            return new MockCompiler
            {
                CompilationResult = CompilationResult.FromUnits(ImmutableArray.Create(unit)),
                OutputResult = new OutputResult(
                    ImmutableArray.Create(Path.Join(projectDir, "dist", "qml", "QmlSharp", "MyApp", "CounterView.qml")),
                    ImmutableArray.Create(schemaPath),
                    Path.Join(projectDir, "dist", "event-bindings.json"),
                    ImmutableArray.Create(Path.Join(projectDir, "dist", "source-maps", "CounterView.qml.map")),
                    0),
            };
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

        private static string WriteQmlFile(string projectDir)
        {
            string qmlDir = Path.Join(projectDir, "dist", "qml", "QmlSharp", "MyApp");
            _ = Directory.CreateDirectory(qmlDir);
            string qmlPath = Path.Join(qmlDir, "CounterView.qml");
            File.WriteAllText(qmlPath, "import QtQuick\nItem {}\n");
            return qmlPath;
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
    }
}
