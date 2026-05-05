using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using QmlSharp.Build;
using QmlSharp.Compiler;
using QmlSharp.Integration.Tests.Fixtures;
using QmlSharp.Qt.Tools;

namespace QmlSharp.Integration.Tests
{
    public sealed class BuildSystemFixtureIntegrationTests
    {
        private static readonly ImmutableArray<BuildPhase> NativeBuildPhases =
            ImmutableArray.Create(
                BuildPhase.ConfigLoading,
                BuildPhase.CSharpCompilation,
                BuildPhase.ModuleMetadata,
                BuildPhase.DependencyResolution,
                BuildPhase.AssetBundling,
                BuildPhase.QmlValidation,
                BuildPhase.CppCodeGenAndBuild);

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public async Task FullApplicationBuild_FromCounterFixture_ProducesExpectedDist_BC01_BP01_PL06_INT_05()
        {
            using BuildFixtureProject fixture = BuildFixtureProject.Copy("counter-application");
            FixtureBuild build = await BuildFixtureAsync(fixture);

            AssertBuildSucceeded(build.Result);
            Assert.Equal(8, build.Result.PhaseResults.Length);
            Assert.All(build.Result.PhaseResults, static phase => Assert.True(phase.Success));
            Assert.True(File.Exists(Path.Join(fixture.OutputDirectory, "manifest.json")));
            Assert.True(File.Exists(Path.Join(fixture.OutputDirectory, "event-bindings.json")));
            Assert.True(File.Exists(Path.Join(fixture.OutputDirectory, "qml", "QmlSharp", "CounterApp", "CounterView.qml")));
            Assert.True(File.Exists(Path.Join(fixture.OutputDirectory, "qml", "QmlSharp", "CounterApp", "qmldir")));
            Assert.True(File.Exists(Path.Join(
                fixture.OutputDirectory,
                "qml",
                "QmlSharp",
                "CounterApp",
                ModuleMetadataPaths.GetQmltypesFileName("QmlSharp.CounterApp"))));
            Assert.True(File.Exists(Path.Join(fixture.OutputDirectory, "schemas", "CounterViewModel.schema.json")));
            Assert.True(File.Exists(Path.Join(fixture.OutputDirectory, "source-maps", "CounterView.qml.map")));
            Assert.True(File.Exists(Path.Join(fixture.OutputDirectory, "native", NativeLibraryNames.GetFileName("qmlsharp_native"))));
            Assert.True(File.Exists(Path.Join(fixture.OutputDirectory, "managed", "CounterApp.dll")));
            Assert.NotNull(build.Result.Manifest);
            Assert.True(build.Result.Stats.FilesCompiled >= 1);
            Assert.True(build.Result.Stats.SchemasGenerated >= 1);
            Assert.True(build.Result.Stats.NativeLibBuilt);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public async Task BuildCommandJson_FromCounterFixture_MatchesResultModel_BC04()
        {
            using BuildFixtureProject fixture = BuildFixtureProject.Copy("counter-application");
            CapturingCommandOutput output = new();
            BuildCommand command = new(
                new ConfigLoader(),
                CreatePipeline(fixture, generateQrc: false, runtimeStage: new RuntimeArtifactStage()),
                output);

            int exitCode = await command.ExecuteAsync(new BuildCommandOptions
            {
                ProjectDir = fixture.ProjectDirectory,
                Force = true,
                Json = true,
            });

            Assert.Equal(CliExitCode.Success, exitCode);
            Assert.Equal(string.Empty, output.Stderr);
            using JsonDocument document = JsonDocument.Parse(output.Stdout);
            JsonElement root = document.RootElement;
            Assert.Equal("build", root.GetProperty("command").GetString());
            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal("success", root.GetProperty("status").GetString());
            Assert.Equal(CliExitCode.Success, root.GetProperty("exitCode").GetInt32());
            JsonElement stats = root.GetProperty("stats");
            Assert.True(stats.GetProperty("filesCompiled").GetInt32() >= 1);
            Assert.True(stats.GetProperty("schemasGenerated").GetInt32() >= 1);
            Assert.True(stats.GetProperty("cppFilesGenerated").GetInt32() >= 1);
            Assert.True(stats.GetProperty("nativeLibBuilt").GetBoolean());
            Assert.Equal(JsonValueKind.Array, root.GetProperty("diagnostics").ValueKind);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public async Task LibraryMode_FromLibraryFixture_ProducesModuleManifest_BC05_BP08()
        {
            using BuildFixtureProject fixture = BuildFixtureProject.Copy("qmlsharp-library");
            FixtureBuild build = await BuildFixtureAsync(fixture, libraryMode: true);

            AssertBuildSucceeded(build.Result);
            string moduleManifestPath = Path.Join(fixture.LibraryDirectory, "qmlsharp.module.json");
            Assert.True(File.Exists(moduleManifestPath));
            Assert.True(File.Exists(Path.Join(fixture.LibraryDirectory, "qml", "QmlSharp", "CounterLibrary", "LibraryCounterView.qml")));
            Assert.True(File.Exists(Path.Join(fixture.LibraryDirectory, "schemas", "LibraryCounterViewModel.schema.json")));
            Assert.False(File.Exists(Path.Join(fixture.LibraryDirectory, "manifest.json")));
            Assert.False(File.Exists(Path.Join(fixture.LibraryDirectory, "event-bindings.json")));
            Assert.False(Directory.Exists(Path.Join(fixture.LibraryDirectory, "native")));
            Assert.False(Directory.Exists(Path.Join(fixture.LibraryDirectory, "managed")));
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(moduleManifestPath));
            Assert.Equal("QmlSharp.CounterLibrary", document.RootElement.GetProperty("moduleUri").GetString());
            Assert.Equal("qml", Assert.Single(document.RootElement.GetProperty("qmlImportPaths").EnumerateArray()).GetString());
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public void PackageResolver_FromConsumingFixture_ConsumesLibraryManifest_PR05_PR06()
        {
            using BuildFixtureProject fixture = BuildFixtureProject.Copy("consuming-application");
            PackageResolver resolver = new();

            PackageResolutionResult result = resolver.ResolveWithDiagnostics(fixture.ProjectDirectory);

            Assert.Empty(result.Diagnostics);
            ResolvedPackage package = Assert.Single(result.Packages);
            Assert.Equal("QmlSharp.CounterLibrary", package.PackageId);
            Assert.NotNull(package.Manifest);
            Assert.Equal("QmlSharp.CounterLibrary", package.Manifest!.ModuleUri);
            string importPath = Assert.Single(resolver.CollectImportPaths(result.Packages));
            Assert.Equal(Path.GetFullPath(Path.Join(package.PackagePath, "qml")), importPath);
            string schemaPath = Assert.Single(resolver.CollectSchemas(result.Packages));
            Assert.EndsWith(Path.Join("schemas", "LibraryCounterViewModel.schema.json"), schemaPath, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public async Task AssetsApplicationBuild_BundlesAssetsAndQrc_RB03_RB05()
        {
            using BuildFixtureProject fixture = BuildFixtureProject.Copy("assets-application");
            FixtureBuild build = await BuildFixtureAsync(fixture, generateQrc: true);

            AssertBuildSucceeded(build.Result);
            Assert.Equal(3, build.Result.Stats.AssetsCollected);
            string qrcPath = Path.Join(fixture.OutputDirectory, "assets", "resources.qrc");
            Assert.True(File.Exists(Path.Join(fixture.OutputDirectory, "assets", "fonts", "app.ttf")));
            Assert.True(File.Exists(Path.Join(fixture.OutputDirectory, "assets", "icons", "app.svg")));
            Assert.True(File.Exists(Path.Join(fixture.OutputDirectory, "assets", "images", "logo.png")));
            Assert.True(File.Exists(qrcPath));
            string qrcText = File.ReadAllText(qrcPath);
            Assert.Contains("<file>fonts/app.ttf</file>", qrcText, StringComparison.Ordinal);
            Assert.Contains("<file>icons/app.svg</file>", qrcText, StringComparison.Ordinal);
            Assert.Contains("<file>images/logo.png</file>", qrcText, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public async Task QmlValidation_UsesDependencyImportPaths_INT_01_PR06()
        {
            using BuildFixtureProject fixture = BuildFixtureProject.Copy("consuming-application");
            RecordingQmlLint linter = new();

            FixtureBuild build = await BuildFixtureAsync(fixture, linter: linter);

            AssertBuildSucceeded(build.Result);
            Assert.NotNull(linter.LastOptions);
            ImmutableArray<string> importPaths = linter.LastOptions!.ImportPaths;
            Assert.Contains(Path.GetFullPath(Path.Join(fixture.OutputDirectory, "qml")), importPaths);
            Assert.Contains(Path.GetFullPath(Path.Join(fixture.QtDir, "qml")), importPaths);
            Assert.Contains(Path.GetFullPath(Path.Join(
                fixture.ProjectDirectory,
                "packages",
                "QmlSharp.CounterLibrary",
                "1.0.0",
                "qml")), importPaths);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public async Task PrebuiltNativeMode_WithExistingNativeLibrary_AssemblesDist_BC01_BP07()
        {
            using BuildFixtureProject fixture = BuildFixtureProject.Copy("native-prebuilt-application");
            PreparePrebuiltRuntimeArtifacts(fixture, "NativePrebuiltApp");
            FixtureBuild build = await BuildFixtureAsync(fixture, runtimeStage: new NativeBuildStage());

            AssertBuildSucceeded(build.Result);
            Assert.True(File.Exists(Path.Join(fixture.OutputDirectory, "native", NativeLibraryNames.GetFileName("qmlsharp_native"))));
            Assert.True(File.Exists(Path.Join(fixture.OutputDirectory, "managed", "NativePrebuiltApp.dll")));
            string generatedNativeDir = Path.Join(fixture.OutputDirectory, "native", "generated");
            Assert.True(!Directory.Exists(generatedNativeDir) ||
                !Directory.EnumerateFiles(generatedNativeDir, "*.cpp", SearchOption.AllDirectories).Any());
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        [Trait("Category", TestCategories.RequiresCMake)]
        [Trait("Category", TestCategories.RequiresNative)]
        public async Task GeneratedCpp_FromCounterFixture_CompilesWithCMake_INT_03()
        {
            using BuildFixtureProject fixture = BuildFixtureProject.Copy("counter-application");
            AssertCMakeAvailable();
            BuildContext context = CreateBuildContext(fixture, libraryMode: false);
            BuildPipeline pipeline = CreatePipeline(
                fixture,
                generateQrc: false,
                runtimeStage: CreateNativeCMakeStage(context));

            BuildResult result = await pipeline.BuildPhasesAsync(context, NativeBuildPhases);

            AssertBuildSucceeded(result);
            Assert.True(File.Exists(Path.Join(fixture.OutputDirectory, "native", NativeLibraryNames.GetFileName("qmlsharp_native"))));
            Assert.True(Directory.EnumerateFiles(
                Path.Join(fixture.OutputDirectory, "native", "generated"),
                "*.cpp",
                SearchOption.AllDirectories).Any());
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public async Task Manifest_FileHashesMatchOutputFiles_PL06_INT_06()
        {
            using BuildFixtureProject fixture = BuildFixtureProject.Copy("counter-application");
            FixtureBuild build = await BuildFixtureAsync(fixture);

            AssertBuildSucceeded(build.Result);
            string manifestPath = Path.Join(fixture.OutputDirectory, "manifest.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement fileHashes = document.RootElement.GetProperty("fileHashes");
            Assert.NotEmpty(fileHashes.EnumerateObject());
            foreach (JsonProperty hashEntry in fileHashes.EnumerateObject())
            {
                string filePath = Path.Join(
                    fixture.OutputDirectory,
                    hashEntry.Name.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(filePath), filePath);
                string actualHash = ComputeSha256(filePath);
                Assert.Equal(hashEntry.Value.GetString(), actualHash);
            }
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public async Task CleanCommand_AfterFixtureBuild_RemovesOnlyGeneratedArtifacts_CN04()
        {
            using BuildFixtureProject fixture = BuildFixtureProject.Copy("counter-application");
            string sourceMarker = Path.Join(fixture.ProjectDirectory, "src", "source.marker");
            File.WriteAllText(sourceMarker, "source");
            FixtureBuild build = await BuildFixtureAsync(fixture);
            AssertBuildSucceeded(build.Result);
            CapturingCommandOutput output = new();
            CleanCommand cleanCommand = new(new CleanService(), output);

            int exitCode = await cleanCommand.ExecuteAsync(new CleanCommandOptions
            {
                ProjectDir = fixture.ProjectDirectory,
                Cache = true,
            });

            Assert.Equal(CliExitCode.Success, exitCode);
            Assert.False(Directory.Exists(fixture.OutputDirectory));
            Assert.True(File.Exists(sourceMarker));
            Assert.True(File.Exists(Path.Join(fixture.ProjectDirectory, "qmlsharp.json")));
            Assert.Contains("Clean completed.", output.Stdout, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public async Task BuildCommand_InvalidConfigFixture_ReturnsConfigError_BC07()
        {
            using BuildFixtureProject fixture = BuildFixtureProject.Copy("invalid-config");
            CapturingCommandOutput output = new();
            BuildCommand command = new(new ConfigLoader(), CreateNoopPipeline(), output);

            int exitCode = await command.ExecuteAsync(new BuildCommandOptions
            {
                ProjectDir = fixture.ProjectDirectory,
            });

            Assert.Equal(CliExitCode.ConfigOrCommandError, exitCode);
            Assert.Contains(BuildDiagnosticCode.ConfigValidationError, output.Stderr, StringComparison.Ordinal);
            Assert.Contains("module.prefix", output.Stderr, StringComparison.Ordinal);
            Assert.Contains("build.mode", output.Stderr, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public async Task BuildCommand_CompilationErrorFixture_ReturnsCompilationDiagnostic_BC08()
        {
            using BuildFixtureProject fixture = BuildFixtureProject.Copy("compilation-error");
            CapturingCommandOutput output = new();
            BuildCommand command = new(
                new ConfigLoader(),
                CreatePipeline(fixture, generateQrc: false, runtimeStage: new RuntimeArtifactStage()),
                output);

            int exitCode = await command.ExecuteAsync(new BuildCommandOptions
            {
                ProjectDir = fixture.ProjectDirectory,
                Force = true,
            });

            Assert.Equal(CliExitCode.BuildError, exitCode);
            Assert.Contains(BuildDiagnosticCode.CompilationFailed, output.Stderr, StringComparison.Ordinal);
            Assert.Contains("MissingType", output.Stderr, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public async Task PlatformDistributor_PackagesFixtureDist_PD05()
        {
            using BuildFixtureProject fixture = BuildFixtureProject.Copy("counter-application");
            FixtureBuild build = await BuildFixtureAsync(fixture);
            AssertBuildSucceeded(build.Result);
            PlatformDistributor distributor = new();

            DistributionResult distribution = distributor.Package(build.Result, build.Context);

            Assert.True(distribution.Success);
            Assert.True(Directory.Exists(distribution.OutputPath));
            Assert.Contains("manifest.json", distribution.IncludedFiles);
            Assert.Contains("managed/CounterApp.dll", distribution.IncludedFiles);
            Assert.Contains("native/" + NativeLibraryNames.GetFileName("qmlsharp_native"), distribution.IncludedFiles);
            Assert.True(distribution.TotalSizeBytes > 0);
        }

        private static async Task<FixtureBuild> BuildFixtureAsync(
            BuildFixtureProject fixture,
            bool libraryMode = false,
            bool generateQrc = false,
            RecordingQmlLint? linter = null,
            IBuildStage? runtimeStage = null)
        {
            BuildPipeline pipeline = CreatePipeline(
                fixture,
                generateQrc,
                runtimeStage ?? new RuntimeArtifactStage(),
                linter ?? new RecordingQmlLint());
            BuildContext context = CreateBuildContext(fixture, libraryMode);
            BuildResult result = await pipeline.BuildAsync(context);
            return new FixtureBuild(result, context);
        }

        private static BuildPipeline CreatePipeline(
            BuildFixtureProject fixture,
            bool generateQrc,
            IBuildStage runtimeStage,
            RecordingQmlLint? linter = null)
        {
            PackageResolver packageResolver = new();
            ResourceBundler resourceBundler = new(
                fixture.ProjectDirectory,
                ImmutableArray.Create("assets"),
                reportMissingRoots: false,
                generateQrcOnBundle: generateQrc);
            return new BuildPipeline(ImmutableArray.Create<IBuildStage>(
                new StaticBuildStage(BuildPhase.ConfigLoading, BuildStageResult.Succeeded()),
                new CSharpCompilationBuildStage(),
                new ModuleMetadataBuildStage(),
                new PackageResolutionBuildStage(packageResolver),
                new ResourceBundlingBuildStage(resourceBundler),
                new QmlValidationBuildStage(new RecordingQmlFormat(), linter ?? new RecordingQmlLint(), packageResolver),
                runtimeStage,
                new OutputAssemblyBuildStage()));
        }

        private static BuildPipeline CreateNoopPipeline()
        {
            return new BuildPipeline(ImmutableArray.Create<IBuildStage>(
                new StaticBuildStage(BuildPhase.ConfigLoading, BuildStageResult.Succeeded()),
                new StaticBuildStage(BuildPhase.CSharpCompilation, BuildStageResult.Succeeded()),
                new StaticBuildStage(BuildPhase.ModuleMetadata, BuildStageResult.Succeeded()),
                new StaticBuildStage(BuildPhase.DependencyResolution, BuildStageResult.Succeeded()),
                new StaticBuildStage(BuildPhase.AssetBundling, BuildStageResult.Succeeded()),
                new StaticBuildStage(BuildPhase.QmlValidation, BuildStageResult.Succeeded()),
                new StaticBuildStage(BuildPhase.CppCodeGenAndBuild, BuildStageResult.Succeeded()),
                new StaticBuildStage(BuildPhase.OutputAssembly, BuildStageResult.Succeeded())));
        }

        private static BuildContext CreateBuildContext(BuildFixtureProject fixture, bool libraryMode)
        {
            QmlSharpConfig config = new ConfigLoader().Load(fixture.ProjectDirectory);
            return new BuildContext
            {
                Config = config,
                ProjectDir = fixture.ProjectDirectory,
                OutputDir = config.OutDir,
                QtDir = config.Qt.Dir ?? fixture.QtDir,
                ForceRebuild = true,
                LibraryMode = libraryMode,
            };
        }

        private static NativeBuildStage CreateNativeCMakeStage(BuildContext context)
        {
            return new NativeBuildStage(
                new CppCodeGenerator(),
                new ViewModelSchemaSerializer(),
                new PackageResolver(),
                CreateCMakeBuilder(context));
        }

        private static CMakeBuilder CreateCMakeBuilder(BuildContext context)
        {
            ImmutableDictionary<string, string>.Builder environment =
                ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
            string? pathValue = Environment.GetEnvironmentVariable("PATH");
            if (OperatingSystem.IsWindows())
            {
                string? windowsSdkToolDirectory = FindWindowsSdkToolDirectory();
                if (windowsSdkToolDirectory is not null)
                {
                    pathValue = string.IsNullOrEmpty(pathValue)
                        ? windowsSdkToolDirectory
                        : windowsSdkToolDirectory + Path.PathSeparator + pathValue;
                }
            }

            if (!string.IsNullOrEmpty(pathValue))
            {
                environment["PATH"] = pathValue;
            }

            if (IsExecutableAvailable("ninja"))
            {
                environment["CMAKE_GENERATOR"] = "Ninja";
                if (OperatingSystem.IsWindows())
                {
                    string compilerPath = ResolveWindowsCxxCompilerPath();
                    environment["CC"] = compilerPath;
                    environment["CXX"] = compilerPath;
                }
                else
                {
                    SetNonWindowsCompilerEnvironment(environment);
                }
            }

            string nativeDirectory = Path.Join(context.OutputDir, "native");
            return new CMakeBuilder(new CMakeBuilderOptions
            {
                SourceDir = Path.Join(nativeDirectory, "generated"),
                NativeOutputDir = nativeDirectory,
                QtDir = context.QtDir,
                BuildPreset = context.Config.Native.CmakePreset,
                EnvironmentVariables = environment.ToImmutable(),
            });
        }

        private static void PreparePrebuiltRuntimeArtifacts(BuildFixtureProject fixture, string projectName)
        {
            string nativeDirectory = Path.Join(fixture.OutputDirectory, "native");
            string managedDirectory = Path.Join(fixture.OutputDirectory, "managed");
            _ = Directory.CreateDirectory(nativeDirectory);
            _ = Directory.CreateDirectory(managedDirectory);
            File.WriteAllText(Path.Join(nativeDirectory, NativeLibraryNames.GetFileName("qmlsharp_native")), "prebuilt-native");
            File.WriteAllText(Path.Join(managedDirectory, projectName + ".dll"), "managed");
        }

        private static void AssertBuildSucceeded(BuildResult result)
        {
            Assert.True(result.Success, FormatDiagnostics(result.Diagnostics));
        }

        private static string FormatDiagnostics(ImmutableArray<BuildDiagnostic> diagnostics)
        {
            return string.Join(
                Environment.NewLine,
                diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message} ({diagnostic.FilePath})"));
        }

        private static void AssertCMakeAvailable()
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmake",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.StartInfo.ArgumentList.Add("--version");
            Assert.True(process.Start(), "cmake --version did not start.");
            Assert.True(process.WaitForExit(10_000), "cmake --version did not exit.");
            Assert.Equal(0, process.ExitCode);
        }

        private static bool IsExecutableAvailable(string fileName)
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.StartInfo.ArgumentList.Add("--version");
            try
            {
                if (!process.Start())
                {
                    return false;
                }

                return process.WaitForExit(10_000) && process.ExitCode == 0;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        private static string ResolveWindowsCxxCompilerPath()
        {
            return FindExecutableOnPath("cl.exe")
                ?? FindExecutableOnPath("clang-cl.exe")
                ?? FindVisualStudioClCompilerPath()
                ?? "cl";
        }

        private static void SetNonWindowsCompilerEnvironment(ImmutableDictionary<string, string>.Builder environment)
        {
            string? cLanguageCompiler = Environment.GetEnvironmentVariable("CC") ?? FindExecutableOnPath("clang");
            string? cxxCompiler = Environment.GetEnvironmentVariable("CXX") ?? FindExecutableOnPath("clang++");
            if (cLanguageCompiler is null || cxxCompiler is null)
            {
                return;
            }

            environment["CC"] = cLanguageCompiler;
            environment["CXX"] = cxxCompiler;
        }

        private static string? FindVisualStudioClCompilerPath()
        {
            string? vcToolsInstallDir = Environment.GetEnvironmentVariable("VCToolsInstallDir");
            if (!string.IsNullOrWhiteSpace(vcToolsInstallDir))
            {
                string candidatePath = Path.Join(vcToolsInstallDir, "bin", "Hostx64", "x64", "cl.exe");
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            string? vsInstallDir = Environment.GetEnvironmentVariable("VSINSTALLDIR");
            string? compilerPath = FindVisualStudioClCompilerPath(vsInstallDir);
            if (compilerPath is not null)
            {
                return compilerPath;
            }

            return FindVisualStudioClCompilerPath(
                Path.Join(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Microsoft Visual Studio",
                    "18",
                    "Community"));
        }

        private static string? FindVisualStudioClCompilerPath(string? visualStudioDirectory)
        {
            if (string.IsNullOrWhiteSpace(visualStudioDirectory))
            {
                return null;
            }

            string msvcDirectory = Path.Join(visualStudioDirectory, "VC", "Tools", "MSVC");
            if (!Directory.Exists(msvcDirectory))
            {
                return null;
            }

            return Directory
                .EnumerateFiles(msvcDirectory, "cl.exe", SearchOption.AllDirectories)
                .Where(static path => path.Contains(
                    Path.Join("bin", "Hostx64", "x64"),
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(static path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static string? FindWindowsSdkToolDirectory()
        {
            string windowsKitsBinDirectory = Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Windows Kits",
                "10",
                "bin");
            if (!Directory.Exists(windowsKitsBinDirectory))
            {
                return null;
            }

            string directToolDirectory = Path.Join(windowsKitsBinDirectory, "x64");
            if (WindowsSdkToolDirectoryExists(directToolDirectory))
            {
                return directToolDirectory;
            }

            return Directory
                .EnumerateDirectories(windowsKitsBinDirectory)
                .Select(static directory => Path.Join(directory, "x64"))
                .Where(WindowsSdkToolDirectoryExists)
                .OrderByDescending(static directory => directory, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static bool WindowsSdkToolDirectoryExists(string directory)
        {
            return File.Exists(Path.Join(directory, "rc.exe"))
                && File.Exists(Path.Join(directory, "mt.exe"));
        }

        private static string? FindExecutableOnPath(string fileName)
        {
            string? pathValue = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathValue))
            {
                return null;
            }

            return pathValue
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(directory => Path.Join(directory, fileName))
                .FirstOrDefault(File.Exists);
        }

        private static string ComputeSha256(string filePath)
        {
            using FileStream stream = File.OpenRead(filePath);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        private sealed record FixtureBuild(BuildResult Result, BuildContext Context);

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

        private sealed class RecordingQmlFormat : IQmlFormat
        {
            public Task<QmlFormatResult> FormatFileAsync(
                string filePath,
                QmlFormatOptions? options = null,
                CancellationToken ct = default)
            {
                return Task.FromResult(CreateResult("qmlformat " + filePath));
            }

            public Task<QmlFormatResult> FormatStringAsync(
                string qmlSource,
                QmlFormatOptions? options = null,
                CancellationToken ct = default)
            {
                return Task.FromResult(CreateResult("qmlformat"));
            }

            public Task<ImmutableArray<QmlFormatResult>> FormatBatchAsync(
                ImmutableArray<string> filePaths,
                QmlFormatOptions? options = null,
                CancellationToken ct = default)
            {
                ImmutableArray<QmlFormatResult> results = filePaths
                    .Select(static filePath => CreateResult("qmlformat " + filePath))
                    .ToImmutableArray();
                return Task.FromResult(results);
            }

            private static QmlFormatResult CreateResult(string command)
            {
                return new QmlFormatResult
                {
                    ToolResult = CreateToolResult(command),
                    HasChanges = false,
                };
            }
        }

        private sealed class RecordingQmlLint : IQmlLint
        {
            public QmlLintOptions? LastOptions { get; private set; }

            public Task<QmlLintResult> LintFileAsync(
                string filePath,
                QmlLintOptions? options = null,
                CancellationToken ct = default)
            {
                LastOptions = options;
                return Task.FromResult(CreateResult("qmllint " + filePath));
            }

            public Task<QmlLintResult> LintStringAsync(
                string qmlSource,
                QmlLintOptions? options = null,
                CancellationToken ct = default)
            {
                LastOptions = options;
                return Task.FromResult(CreateResult("qmllint"));
            }

            public Task<ImmutableArray<QmlLintResult>> LintBatchAsync(
                ImmutableArray<string> filePaths,
                QmlLintOptions? options = null,
                CancellationToken ct = default)
            {
                LastOptions = options;
                ImmutableArray<QmlLintResult> results = filePaths
                    .Select(static filePath => CreateResult("qmllint " + filePath))
                    .ToImmutableArray();
                return Task.FromResult(results);
            }

            public Task<QmlLintResult> LintModuleAsync(
                string modulePath,
                QmlLintOptions? options = null,
                CancellationToken ct = default)
            {
                LastOptions = options;
                return Task.FromResult(CreateResult("qmllint " + modulePath));
            }

            public Task<ImmutableArray<string>> ListPluginsAsync(CancellationToken ct = default)
            {
                return Task.FromResult(ImmutableArray<string>.Empty);
            }

            private static QmlLintResult CreateResult(string command)
            {
                return new QmlLintResult
                {
                    ToolResult = CreateToolResult(command),
                    ErrorCount = 0,
                    WarningCount = 0,
                    InfoCount = 0,
                };
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

        private sealed class RuntimeArtifactStage : IBuildStage
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
                string managedPath = Path.Join(context.OutputDir, "managed", ProjectName(context) + ".dll");
                string generatedDir = Path.Join(context.OutputDir, "native", "generated");
                _ = Directory.CreateDirectory(Path.GetDirectoryName(nativePath) ?? context.OutputDir);
                _ = Directory.CreateDirectory(Path.GetDirectoryName(managedPath) ?? context.OutputDir);
                _ = Directory.CreateDirectory(generatedDir);
                File.WriteAllText(nativePath, "native");
                File.WriteAllText(managedPath, "managed");
                File.WriteAllText(Path.Join(generatedDir, "CMakeLists.txt"), "cmake_minimum_required(VERSION 3.20)\n");
                File.WriteAllText(Path.Join(generatedDir, "type_registration.cpp"), "// generated fixture\n");
                return Task.FromResult(BuildStageResult.Succeeded(
                    new BuildStatsDelta
                    {
                        CppFilesGenerated = 2,
                        NativeLibBuilt = true,
                    },
                    new BuildArtifacts
                    {
                        NativeLibraryPath = nativePath,
                        AssemblyPath = managedPath,
                    }));
            }

            private static string ProjectName(BuildContext context)
            {
                return context.Config.Name ?? context.Config.Module.Prefix;
            }
        }

        private static ToolResult CreateToolResult(string command)
        {
            return new ToolResult
            {
                ExitCode = 0,
                Stdout = string.Empty,
                Stderr = string.Empty,
                DurationMs = 0,
                Command = command,
            };
        }
    }
}
