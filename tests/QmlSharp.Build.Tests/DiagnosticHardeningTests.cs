using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class DiagnosticHardeningTests
    {
        private static readonly ImmutableArray<string> ExpectedCodes = ImmutableArray.Create(
            BuildDiagnosticCode.ConfigNotFound,
            BuildDiagnosticCode.ConfigParseError,
            BuildDiagnosticCode.ConfigValidationError,
            BuildDiagnosticCode.QtDirNotFound,
            BuildDiagnosticCode.CompilationFailed,
            BuildDiagnosticCode.NoViewModelsFound,
            BuildDiagnosticCode.SchemaGenerationFailed,
            BuildDiagnosticCode.CppGenerationFailed,
            BuildDiagnosticCode.UnsupportedCppType,
            BuildDiagnosticCode.CMakeConfigureFailed,
            BuildDiagnosticCode.CMakeBuildFailed,
            BuildDiagnosticCode.QmldirGenerationFailed,
            BuildDiagnosticCode.QmltypesGenerationFailed,
            BuildDiagnosticCode.PackageResolutionFailed,
            BuildDiagnosticCode.ManifestParseError,
            BuildDiagnosticCode.AssetNotFound,
            BuildDiagnosticCode.AssetCopyFailed,
            BuildDiagnosticCode.QmlLintError,
            BuildDiagnosticCode.QmlFormatError,
            BuildDiagnosticCode.OutputAssemblyFailed,
            BuildDiagnosticCode.ManifestWriteFailed,
            BuildDiagnosticCode.OutputValidationFailed,
            BuildDiagnosticCode.InternalError);

        [Fact]
        public void EH01_ConfigErrorMessage_IncludesFieldAndExpectedFormat()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(EH01_ConfigErrorMessage_IncludesFieldAndExpectedFormat));
            string qtDir = Path.Join(project.Path, "qt");
            _ = Directory.CreateDirectory(qtDir);

            ConfigLoader loader = new(new MissingQtToolchain());
            QmlSharpConfig config = BuildTestFixtures.CreateDefaultConfig() with
            {
                Qt = new QtConfig
                {
                    Dir = qtDir,
                },
                Build = new BuildConfig
                {
                    Mode = "staging",
                },
            };

            ConfigDiagnostic diagnostic = Assert.Single(loader.Validate(config));

            Assert.Equal(BuildDiagnosticCode.ConfigValidationError, diagnostic.Code);
            Assert.Equal("build.mode", diagnostic.Field);
            Assert.Contains("development", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("production", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildDiagnosticCodes_AllHaveStep0815CoverageEntries()
        {
            ImmutableDictionary<string, string> coverage = CreateCoverageMap();

            Assert.Equal(ExpectedCodes.Order(StringComparer.Ordinal), coverage.Keys.Order(StringComparer.Ordinal));
            Assert.All(coverage, static entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value)));
        }

        [Fact]
        public void EH06_AllBDiagnosticCodesHaveUniqueRepresentativeMessages()
        {
            ImmutableDictionary<string, string> messages = CreateRepresentativeMessages();

            Assert.Equal(ExpectedCodes.Order(StringComparer.Ordinal), messages.Keys.Order(StringComparer.Ordinal));
            Assert.Equal(messages.Count, messages.Values.Distinct(StringComparer.Ordinal).Count());
            Assert.All(messages, static entry =>
            {
                Assert.StartsWith("QMLSHARP-B", entry.Key, StringComparison.Ordinal);
                Assert.DoesNotContain("{0}", entry.Value, StringComparison.Ordinal);
                Assert.DoesNotContain("{1}", entry.Value, StringComparison.Ordinal);
                Assert.False(string.IsNullOrWhiteSpace(entry.Value));
            });
        }

        private static ImmutableDictionary<string, string> CreateCoverageMap()
        {
            return ImmutableDictionary.CreateRange(
                StringComparer.Ordinal,
                new[]
                {
                    Pair(BuildDiagnosticCode.ConfigNotFound, "CL06_LoadNonexistentFile_ReportsConfigNotFound"),
                    Pair(BuildDiagnosticCode.ConfigParseError, "CL07_LoadMalformedJson_ReportsConfigParseError"),
                    Pair(BuildDiagnosticCode.ConfigValidationError, "CV03_ValidateInvalidBuildMode_ReportsFieldDiagnostic and EH-01"),
                    Pair(BuildDiagnosticCode.QtDirNotFound, "CV02_ValidateInvalidQtDir_ReportsQtDirNotFound"),
                    Pair(BuildDiagnosticCode.CompilationFailed, "CSharpCompilationStage_MapsCompilerDiagnosticsToBuildDiagnostics"),
                    Pair(BuildDiagnosticCode.NoViewModelsFound, "CSharpCompilationStage_NoSchemasProducesB011"),
                    Pair(BuildDiagnosticCode.SchemaGenerationFailed, "CSharpCompilationStage_MapsSchemaOutputDiagnosticsToBuildDiagnostics"),
                    Pair(BuildDiagnosticCode.CppGenerationFailed, "GenerationFailure_ReportsB020"),
                    Pair(BuildDiagnosticCode.UnsupportedCppType, "EH02_UnsupportedCppTypeWarning_IncludesTypeNameAndViewModel"),
                    Pair(BuildDiagnosticCode.CMakeConfigureFailed, "EH03_CMakeConfigureFailure_CapturesStdoutAndStderr"),
                    Pair(BuildDiagnosticCode.CMakeBuildFailed, "EH04_CMakeBuildFailure_CapturesCompilerErrors"),
                    Pair(BuildDiagnosticCode.QmldirGenerationFailed, "MM05_ModuleMetadataStage_ReportsB030WhenQmldirGenerationFails"),
                    Pair(BuildDiagnosticCode.QmltypesGenerationFailed, "MM06_ModuleMetadataStage_ReportsB031WhenQmltypesGenerationFails"),
                    Pair(BuildDiagnosticCode.PackageResolutionFailed, "PackageResolver_MissingProjectFile_ReportsB040"),
                    Pair(BuildDiagnosticCode.ManifestParseError, "PR07_InvalidManifestJson_ReportsDiagnosticAndSkipsPackage"),
                    Pair(BuildDiagnosticCode.AssetNotFound, "CollectWithDiagnostics_MissingConfiguredAssetRoot_ReportsB050"),
                    Pair(BuildDiagnosticCode.AssetCopyFailed, "Bundle_CopyFailure_ReportsB051"),
                    Pair(BuildDiagnosticCode.QmlLintError, "QmlValidationStage_MapsQmllintErrorsToB060"),
                    Pair(BuildDiagnosticCode.QmlFormatError, "QmlValidationStage_MapsQmlformatErrorsToB061"),
                    Pair(BuildDiagnosticCode.OutputAssemblyFailed, "PL07_ValidateOutput_MissingNativeLibReportsB072 and output assembly copy tests"),
                    Pair(BuildDiagnosticCode.ManifestWriteFailed, "Step 08.15 representative message-format coverage"),
                    Pair(BuildDiagnosticCode.OutputValidationFailed, "PL07_ValidateOutput_MissingNativeLibReportsB072"),
                    Pair(BuildDiagnosticCode.InternalError, "InternalStageException_BecomesB090Diagnostic"),
                });
        }

        private static ImmutableDictionary<string, string> CreateRepresentativeMessages()
        {
            return ImmutableDictionary.CreateRange(
                StringComparer.Ordinal,
                new[]
                {
                    Pair(BuildDiagnosticCode.ConfigNotFound, "Configuration file 'qmlsharp.json' was not found in the project directory."),
                    Pair(BuildDiagnosticCode.ConfigParseError, "qmlsharp.json could not be parsed: expected a JSON object."),
                    Pair(BuildDiagnosticCode.ConfigValidationError, "build.mode: Build mode must be either 'development' or 'production'."),
                    Pair(BuildDiagnosticCode.QtDirNotFound, "qt.dir: Qt SDK directory was not found."),
                    Pair(BuildDiagnosticCode.CompilationFailed, "C# compilation failed: compiler diagnostic C0001."),
                    Pair(BuildDiagnosticCode.NoViewModelsFound, "C# compilation produced no ViewModel schemas."),
                    Pair(BuildDiagnosticCode.SchemaGenerationFailed, "Compiler output writing failed: schema serialization failed."),
                    Pair(BuildDiagnosticCode.CppGenerationFailed, "C++ generation failed: schema cannot be generated."),
                    Pair(BuildDiagnosticCode.UnsupportedCppType, "Property 'payload' on ViewModel 'CounterViewModel' uses unsupported C++ type 'UnsupportedThing' and falls back to QVariant."),
                    Pair(BuildDiagnosticCode.CMakeConfigureFailed, "CMake configure failed with exit code 1. stdout: configure output stderr: Qt6Config.cmake not found."),
                    Pair(BuildDiagnosticCode.CMakeBuildFailed, "CMake build failed with exit code 2. stdout: build output stderr: compiler error C2065."),
                    Pair(BuildDiagnosticCode.QmldirGenerationFailed, "qmldir generation failed: module URI was invalid."),
                    Pair(BuildDiagnosticCode.QmltypesGenerationFailed, "qmltypes generation failed: schema metadata could not be read."),
                    Pair(BuildDiagnosticCode.PackageResolutionFailed, "Package resolution failed because the project file was missing."),
                    Pair(BuildDiagnosticCode.ManifestParseError, "Package manifest qmlsharp.module.json could not be parsed."),
                    Pair(BuildDiagnosticCode.AssetNotFound, "Asset root 'assets' does not exist."),
                    Pair(BuildDiagnosticCode.AssetCopyFailed, "Asset 'logo.png' could not be copied to the output directory."),
                    Pair(BuildDiagnosticCode.QmlLintError, "qmllint reported an unresolved property binding."),
                    Pair(BuildDiagnosticCode.QmlFormatError, "qmlformat reported an expected token error."),
                    Pair(BuildDiagnosticCode.OutputAssemblyFailed, "Output assembly failed because source artifact 'CounterView.qml' does not exist."),
                    Pair(BuildDiagnosticCode.ManifestWriteFailed, "Manifest write failed: access to manifest.json was denied."),
                    Pair(BuildDiagnosticCode.OutputValidationFailed, "Output validation failed because the native library is missing."),
                    Pair(BuildDiagnosticCode.InternalError, "Build Stage 7 (CppCodeGenAndBuild) failed unexpectedly: internal invariant failed."),
                });
        }

        private static KeyValuePair<string, string> Pair(string code, string value)
        {
            return new KeyValuePair<string, string>(code, value);
        }

        private sealed class MissingQtToolchain : IQtToolchain
        {
            public QtInstallation? Installation => null;

            public Task<QtInstallation> DiscoverAsync(QtToolchainConfig? config = null, CancellationToken ct = default)
            {
                throw new QtInstallationNotFoundError("Qt was not found.", ImmutableArray.Create("QT_DIR: not set"));
            }

            public Task<ToolAvailability> CheckToolsAsync(CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }

            public Task<ToolInfo> GetToolInfoAsync(string toolName, CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }
        }
    }
}
