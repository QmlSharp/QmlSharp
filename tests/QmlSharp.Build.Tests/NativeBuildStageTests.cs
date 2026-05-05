using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class NativeBuildStageTests
    {
        [Fact]
        public async Task BP05_SchemaChangeDetected_WritesGeneratedCppAndRunsCMake()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(BP05_SchemaChangeDetected_WritesGeneratedCppAndRunsCMake));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            await WriteSchemaAsync(context, BuildTestFixtures.CreateCounterSchema());
            MockCMakeBuilder cmakeBuilder = new();
            NativeBuildStage stage = CreateStage(new CppCodeGenerator(), cmakeBuilder);

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(4, result.Stats.CppFilesGenerated);
            Assert.True(result.Stats.NativeLibBuilt);
            Assert.Equal(1, cmakeBuilder.ConfigureCallCount);
            Assert.Equal(1, cmakeBuilder.BuildCallCount);
            Assert.Equal(context.Config.Native.CmakePreset, cmakeBuilder.LastConfigurePreset);
            Assert.True(File.Exists(Path.Join(context.OutputDir, "native", "generated", "CounterViewModel.h")));
            Assert.True(File.Exists(Path.Join(context.OutputDir, "native", "generated", "CounterViewModel.cpp")));
            Assert.True(File.Exists(Path.Join(context.OutputDir, "native", "generated", "CMakeLists.txt")));
            Assert.True(File.Exists(Path.Join(context.OutputDir, "native", "generated", "type_registration.cpp")));
            string nativeLibraryPath = ExpectedNativeLibraryPath(context);
            Assert.True(File.Exists(nativeLibraryPath));
            Assert.Equal(nativeLibraryPath, result.Artifacts.NativeLibraryPath);
        }

        [Fact]
        public async Task BP04_IncrementalNoSchemaOrNativeContractChange_SkipsGenerationAndCMake()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(BP04_IncrementalNoSchemaOrNativeContractChange_SkipsGenerationAndCMake));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            await WriteSchemaAsync(context, BuildTestFixtures.CreateCounterSchema());
            NativeBuildStage firstStage = CreateStage(new CppCodeGenerator(), new MockCMakeBuilder());
            BuildStageResult firstResult = await firstStage.ExecuteAsync(context, CancellationToken.None);
            MockCMakeBuilder secondCMakeBuilder = new();
            ThrowingCppCodeGenerator throwingGenerator = new();
            NativeBuildStage secondStage = CreateStage(throwingGenerator, secondCMakeBuilder);

            BuildStageResult secondResult = await secondStage.ExecuteAsync(context, CancellationToken.None);

            Assert.True(firstResult.Success);
            Assert.True(secondResult.Success);
            Assert.Equal(0, secondResult.Stats.CppFilesGenerated);
            Assert.False(secondResult.Stats.NativeLibBuilt);
            Assert.Equal(0, throwingGenerator.GenerateCallCount);
            Assert.Equal(0, secondCMakeBuilder.ConfigureCallCount);
            Assert.Equal(0, secondCMakeBuilder.BuildCallCount);
            Assert.Equal(ExpectedNativeLibraryPath(context), secondResult.Artifacts.NativeLibraryPath);
        }

        [Fact]
        public async Task IncrementalForceRebuild_BypassesNativeFingerprintAndRunsCMake()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(IncrementalForceRebuild_BypassesNativeFingerprintAndRunsCMake));
            BuildContext initialContext = BuildTestFixtures.CreateDefaultContext(project.Path);
            await WriteSchemaAsync(initialContext, BuildTestFixtures.CreateCounterSchema());
            NativeBuildStage firstStage = CreateStage(new CppCodeGenerator(), new MockCMakeBuilder());
            BuildStageResult firstResult = await firstStage.ExecuteAsync(initialContext, CancellationToken.None);
            MockCMakeBuilder secondCMakeBuilder = new();
            NativeBuildStage secondStage = CreateStage(new CppCodeGenerator(), secondCMakeBuilder);
            BuildContext forceContext = initialContext with
            {
                ForceRebuild = true,
            };

            BuildStageResult secondResult = await secondStage.ExecuteAsync(forceContext, CancellationToken.None);

            Assert.True(firstResult.Success);
            Assert.True(secondResult.Success);
            Assert.Equal(1, secondCMakeBuilder.ConfigureCallCount);
            Assert.Equal(1, secondCMakeBuilder.BuildCallCount);
            Assert.True(secondResult.Stats.NativeLibBuilt);
        }

        [Fact]
        public async Task IncrementalMissingGeneratedNativeFile_InvalidatesCacheAndRegenerates()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(IncrementalMissingGeneratedNativeFile_InvalidatesCacheAndRegenerates));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            await WriteSchemaAsync(context, BuildTestFixtures.CreateCounterSchema());
            NativeBuildStage firstStage = CreateStage(new CppCodeGenerator(), new MockCMakeBuilder());
            BuildStageResult firstResult = await firstStage.ExecuteAsync(context, CancellationToken.None);
            string generatedHeader = Path.Join(context.OutputDir, "native", "generated", "CounterViewModel.h");
            File.Delete(generatedHeader);
            MockCMakeBuilder secondCMakeBuilder = new();
            NativeBuildStage secondStage = CreateStage(new CppCodeGenerator(), secondCMakeBuilder);

            BuildStageResult secondResult = await secondStage.ExecuteAsync(context, CancellationToken.None);

            Assert.True(firstResult.Success);
            Assert.True(secondResult.Success);
            Assert.True(File.Exists(generatedHeader));
            Assert.Equal(1, secondCMakeBuilder.ConfigureCallCount);
            Assert.Equal(1, secondCMakeBuilder.BuildCallCount);
        }

        [Fact]
        public async Task IncrementalMissingNativeLibrary_InvalidatesCacheAndRebuilds()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(IncrementalMissingNativeLibrary_InvalidatesCacheAndRebuilds));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            await WriteSchemaAsync(context, BuildTestFixtures.CreateCounterSchema());
            NativeBuildStage firstStage = CreateStage(new CppCodeGenerator(), new MockCMakeBuilder());
            BuildStageResult firstResult = await firstStage.ExecuteAsync(context, CancellationToken.None);
            string nativeLibraryPath = ExpectedNativeLibraryPath(context);
            File.Delete(nativeLibraryPath);
            MockCMakeBuilder secondCMakeBuilder = new();
            NativeBuildStage secondStage = CreateStage(new CppCodeGenerator(), secondCMakeBuilder);

            BuildStageResult secondResult = await secondStage.ExecuteAsync(context, CancellationToken.None);

            Assert.True(firstResult.Success);
            Assert.True(secondResult.Success);
            Assert.True(File.Exists(nativeLibraryPath));
            Assert.Equal(1, secondCMakeBuilder.ConfigureCallCount);
            Assert.Equal(1, secondCMakeBuilder.BuildCallCount);
        }

        [Fact]
        public async Task BP08_LibraryMode_SkipsNativeGenerationAndCMake()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(BP08_LibraryMode_SkipsNativeGenerationAndCMake));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path) with
            {
                LibraryMode = true,
            };
            await WriteSchemaAsync(context, BuildTestFixtures.CreateCounterSchema());
            MockCMakeBuilder cmakeBuilder = new();
            ThrowingCppCodeGenerator generator = new();
            NativeBuildStage stage = CreateStage(generator, cmakeBuilder);

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(0, generator.GenerateCallCount);
            Assert.Equal(0, cmakeBuilder.ConfigureCallCount);
            Assert.Equal(0, cmakeBuilder.BuildCallCount);
            Assert.Null(result.Artifacts.NativeLibraryPath);
        }

        [Fact]
        public async Task PrebuiltMode_WithExistingNativeLibrary_SucceedsWithoutCMake()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(PrebuiltMode_WithExistingNativeLibrary_SucceedsWithoutCMake));
            BuildContext context = CreatePrebuiltContext(project.Path);
            string nativeLibraryPath = ExpectedNativeLibraryPath(context);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(nativeLibraryPath)!);
            await File.WriteAllTextAsync(nativeLibraryPath, "prebuilt-native");
            MockCMakeBuilder cmakeBuilder = new();
            NativeBuildStage stage = CreateStage(new ThrowingCppCodeGenerator(), cmakeBuilder);

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(nativeLibraryPath, result.Artifacts.NativeLibraryPath);
            Assert.Equal(0, cmakeBuilder.ConfigureCallCount);
            Assert.Equal(0, cmakeBuilder.BuildCallCount);
        }

        [Fact]
        public async Task PrebuiltMode_WithMissingNativeLibrary_FailsBeforeStageEight()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(PrebuiltMode_WithMissingNativeLibrary_FailsBeforeStageEight));
            BuildContext context = CreatePrebuiltContext(project.Path);
            NativeBuildStage stage = CreateStage(new ThrowingCppCodeGenerator(), new MockCMakeBuilder());

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.False(result.Success);
            Assert.Equal(BuildDiagnosticCode.CMakeBuildFailed, diagnostic.Code);
            Assert.Contains("Prebuilt native mode", diagnostic.Message, StringComparison.Ordinal);
            Assert.Equal(ExpectedNativeLibraryPath(context), diagnostic.FilePath);
        }

        [Fact]
        public async Task EH02_UnsupportedCppTypeWarning_IncludesTypeNameAndViewModel()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(EH02_UnsupportedCppTypeWarning_IncludesTypeNameAndViewModel));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            ViewModelSchema schema = BuildTestFixtures.CreateCounterSchema() with
            {
                Properties = ImmutableArray.Create(new StateEntry("payload", "UnsupportedThing", null, false, 1)),
            };
            await WriteSchemaAsync(context, schema);
            NativeBuildStage stage = CreateStage(new CppCodeGenerator(), new MockCMakeBuilder());

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.True(result.Success);
            Assert.Equal(BuildDiagnosticCode.UnsupportedCppType, diagnostic.Code);
            Assert.Equal(BuildDiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("UnsupportedThing", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("CounterViewModel", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task EH03_CMakeConfigureFailure_CapturesStdoutAndStderr()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(EH03_CMakeConfigureFailure_CapturesStdoutAndStderr));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            await WriteSchemaAsync(context, BuildTestFixtures.CreateCounterSchema());
            MockCMakeBuilder cmakeBuilder = new();
            cmakeBuilder.EnqueueConfigureResult(new CMakeStepResult(
                false,
                "configure stdout",
                "Qt6Config.cmake not found",
                TimeSpan.FromMilliseconds(10),
                1));
            NativeBuildStage stage = CreateStage(new CppCodeGenerator(), cmakeBuilder);

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.False(result.Success);
            Assert.Equal(BuildDiagnosticCode.CMakeConfigureFailed, diagnostic.Code);
            Assert.Contains("configure stdout", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("Qt6Config.cmake not found", diagnostic.Message, StringComparison.Ordinal);
            Assert.Equal(0, cmakeBuilder.BuildCallCount);
        }

        [Fact]
        public async Task EH04_CMakeBuildFailure_CapturesCompilerErrors()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(EH04_CMakeBuildFailure_CapturesCompilerErrors));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            await WriteSchemaAsync(context, BuildTestFixtures.CreateCounterSchema());
            MockCMakeBuilder cmakeBuilder = new(createLibraryOnBuild: false);
            cmakeBuilder.EnqueueBuildResult(new CMakeStepResult(
                false,
                "build stdout",
                "CounterViewModel.cpp(42): error C2065: missing_symbol",
                TimeSpan.FromMilliseconds(10),
                2));
            NativeBuildStage stage = CreateStage(new CppCodeGenerator(), cmakeBuilder);

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.False(result.Success);
            Assert.Equal(BuildDiagnosticCode.CMakeBuildFailed, diagnostic.Code);
            Assert.Contains("build stdout", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("missing_symbol", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GenerationFailure_ReportsB020()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(GenerationFailure_ReportsB020));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            await WriteSchemaAsync(context, BuildTestFixtures.CreateCounterSchema());
            ThrowingCppCodeGenerator generator = new(new InvalidOperationException("schema cannot be generated"));
            NativeBuildStage stage = CreateStage(generator, new MockCMakeBuilder());

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.False(result.Success);
            Assert.Equal(BuildDiagnosticCode.CppGenerationFailed, diagnostic.Code);
            Assert.Contains("schema cannot be generated", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task DependencyPackageSchemas_AreIncludedInStageSevenGeneration()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(DependencyPackageSchemas_AreIncludedInStageSevenGeneration));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            string packageSchemaPath = Path.Join(project.Path, "package-schema", "DependencyViewModel.schema.json");
            await WriteSchemaFileAsync(packageSchemaPath, BuildTestFixtures.CreateCounterSchema() with
            {
                ClassName = "DependencyViewModel",
                CompilerSlotKey = "DependencyView::__qmlsharp_vm0",
            });
            MockCMakeBuilder cmakeBuilder = new();
            NativeBuildStage stage = CreateStage(
                new CppCodeGenerator(),
                cmakeBuilder,
                new StaticPackageResolver(packageSchemaPath));

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Join(context.OutputDir, "native", "generated", "DependencyViewModel.h")));
            Assert.Equal(1, cmakeBuilder.ConfigureCallCount);
        }

        private static NativeBuildStage CreateStage(
            ICppCodeGenerator generator,
            MockCMakeBuilder cmakeBuilder,
            IPackageResolver? packageResolver = null)
        {
            return new NativeBuildStage(
                generator,
                new ViewModelSchemaSerializer(),
                packageResolver ?? new EmptyPackageResolver(),
                cmakeBuilder);
        }

        private static BuildContext CreatePrebuiltContext(string projectPath)
        {
            BuildContext context = BuildTestFixtures.CreateDefaultContext(projectPath);
            return context with
            {
                Config = context.Config with
                {
                    Native = context.Config.Native with
                    {
                        Prebuilt = true,
                    },
                },
            };
        }

        private static async Task WriteSchemaAsync(BuildContext context, ViewModelSchema schema)
        {
            string schemaPath = Path.Join(context.OutputDir, "schemas", schema.ClassName + ".schema.json");
            await WriteSchemaFileAsync(schemaPath, schema);
        }

        private static async Task WriteSchemaFileAsync(string schemaPath, ViewModelSchema schema)
        {
            string? schemaDirectory = Path.GetDirectoryName(schemaPath);
            if (!string.IsNullOrWhiteSpace(schemaDirectory))
            {
                _ = Directory.CreateDirectory(schemaDirectory);
            }

            ViewModelSchemaSerializer serializer = new();
            await File.WriteAllTextAsync(schemaPath, serializer.Serialize(schema));
        }

        private static string ExpectedNativeLibraryPath(BuildContext context)
        {
            return Path.Join(context.OutputDir, "native", NativeLibraryNames.GetFileName("qmlsharp_native"));
        }

        private class EmptyPackageResolver : IPackageResolver
        {
            public virtual ImmutableArray<ResolvedPackage> Resolve(string projectDir)
            {
                return ImmutableArray<ResolvedPackage>.Empty;
            }

            public virtual ImmutableArray<string> CollectImportPaths(ImmutableArray<ResolvedPackage> packages)
            {
                return ImmutableArray<string>.Empty;
            }

            public virtual ImmutableArray<string> CollectSchemas(ImmutableArray<ResolvedPackage> packages)
            {
                return ImmutableArray<string>.Empty;
            }
        }

        private sealed class StaticPackageResolver : EmptyPackageResolver
        {
            private readonly string schemaPath;

            public StaticPackageResolver(string schemaPath)
            {
                this.schemaPath = schemaPath;
            }

            public override ImmutableArray<string> CollectSchemas(ImmutableArray<ResolvedPackage> packages)
            {
                return ImmutableArray.Create(schemaPath);
            }
        }

        private sealed class ThrowingCppCodeGenerator : ICppCodeGenerator
        {
            private readonly Exception? exception;

            public ThrowingCppCodeGenerator(Exception? exception = null)
            {
                this.exception = exception;
            }

            public int GenerateCallCount { get; private set; }

            public CppGenerationResult Generate(
                ImmutableArray<ViewModelSchema> schemas,
                CppGenerationOptions options)
            {
                GenerateCallCount++;
                throw exception ?? new InvalidOperationException("Generator should not be called.");
            }

            public string GenerateHeader(ViewModelSchema schema, CppGenerationOptions options)
            {
                throw new NotSupportedException();
            }

            public string GenerateImplementation(ViewModelSchema schema, CppGenerationOptions options)
            {
                throw new NotSupportedException();
            }

            public string GenerateCMakeLists(
                ImmutableArray<ViewModelSchema> schemas,
                CppGenerationOptions options)
            {
                throw new NotSupportedException();
            }

            public string GenerateTypeRegistration(
                ImmutableArray<ViewModelSchema> schemas,
                CppGenerationOptions options)
            {
                throw new NotSupportedException();
            }
        }
    }
}
