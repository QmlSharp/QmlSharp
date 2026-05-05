using System.Reflection;
using System.Xml.Linq;
using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class BuildSystemClosureTests
    {
        [Fact]
        public void Step0816_TestSpecIds_AllNamedRowsHaveTestCoverage()
        {
            string corpus = ReadBuildSystemTestCorpus();
            ImmutableArray<string> expectedIds = CreateExpectedTestSpecIds();

            ImmutableArray<string> missing = expectedIds
                .Where(id => !corpus.Contains(NormalizeTestId(id), StringComparison.Ordinal))
                .ToImmutableArray();

            Assert.True(
                missing.IsDefaultOrEmpty,
                "Missing 08-build-system test-spec coverage IDs: " + string.Join(", ", missing));
            Assert.Equal(103, expectedIds.Count(id => !id.StartsWith("CG-G", StringComparison.Ordinal) && !id.StartsWith("PF-", StringComparison.Ordinal)));
            Assert.Equal(4, expectedIds.Count(static id => id.StartsWith("CG-G", StringComparison.Ordinal)));
            Assert.Equal(8, expectedIds.Count(static id => id.StartsWith("PF-", StringComparison.Ordinal)));
        }

        [Fact]
        public void Step0816_GoldenAndPerformanceTargets_ArePresentAndGated()
        {
            string repoRoot = BuildTestFixtures.FindRepositoryRoot();
            string corpus = ReadBuildSystemTestCorpus();
            string goldenRoot = Path.Join(repoRoot, "tests", "QmlSharp.Build.Tests", "testdata", "golden");
            ImmutableArray<string> goldenFiles = ImmutableArray.Create(
                "CounterViewModel.h",
                "CounterViewModel.cpp",
                "CMakeLists.txt",
                "type_registration.cpp");

            foreach (string goldenFile in goldenFiles)
            {
                Assert.True(File.Exists(Path.Join(goldenRoot, goldenFile)), $"Missing golden file {goldenFile}.");
            }

            Assert.Equal(4, CountOccurrences(corpus, "[Trait(\"Category\", \"Golden\")]"));
            foreach (string performanceId in CreateRange("PF", 1, 8))
            {
                Assert.Contains(NormalizeTestId(performanceId), corpus, StringComparison.Ordinal);
            }

            Assert.True(
                CountOccurrences(corpus, "BuildTestCategories.Performance") >= 8,
                "Performance tests must remain explicitly gated by category.");
        }

        [Fact]
        public void Step0816_PipelineCliAndDoctorCoverage_IsPinned()
        {
            string corpus = ReadBuildSystemTestCorpus();
            ImmutableDictionary<string, ImmutableArray<string>> stageCoverage = ImmutableDictionary.CreateRange(
                StringComparer.Ordinal,
                new[]
                {
                    Pair("Build Stage 1", "CL01", "CV02"),
                    Pair("Build Stage 2", "CSharpCompilationStage_MapsCompilerDiagnosticsToBuildDiagnostics", "CSharpCompilationStage_NoSchemasProducesB011"),
                    Pair("Build Stage 3", "MM04_ModuleMetadataStage_WritesQmldirAndQmltypes", "MM05_ModuleMetadataStage_ReportsB030"),
                    Pair("Build Stage 4", "PR01", "Stage04_DependencyResolution_EmitsPackageArtifacts"),
                    Pair("Build Stage 5", "RB03", "Stage05_AssetBundling_ReturnsArtifactsAndStatsForLaterStages"),
                    Pair("Build Stage 6", "QmlValidationStage_MapsQmllintErrorsToB060", "QmlValidationStage_MapsQmlformatErrorsToB061"),
                    Pair("Build Stage 7", "BP05_SchemaChangeDetected_WritesGeneratedCppAndRunsCMake", "PrebuiltMode_WithMissingNativeLibrary_FailsBeforeStageEight"),
                    Pair("Build Stage 8", "BP09_OutputAssemblyStage_WritesManifestWithCorrectMetadata", "BP10_OutputAssemblyStage_ValidatesCompleteOutput"),
                });
            ImmutableDictionary<string, ImmutableArray<string>> commandCoverage = ImmutableDictionary.CreateRange(
                StringComparer.Ordinal,
                new[]
                {
                    Pair("build", "BC01", "BC07", "BC08"),
                    Pair("dev", "DC01", "DC05", "DevCommand_MissingEntryReturnsCommandError"),
                    Pair("doctor", "DR01", "DR02", "Doctor_AllChecksHaveNegativeCoverage"),
                    Pair("init", "IN01", "IN03"),
                    Pair("clean", "CN01", "CleanRefusesToDeleteOutDirOutsideProjectRoot"),
                });

            AssertCoverageTokens(corpus, stageCoverage);
            AssertCoverageTokens(corpus, commandCoverage);
            foreach (FieldInfo checkIdField in typeof(DoctorCheckId)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(static field => field.FieldType == typeof(string)))
            {
                Assert.Contains("DoctorCheckId." + checkIdField.Name, corpus, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Step0816_PublicInterfaceMethods_HaveCoverageMapEntries()
        {
            string corpus = ReadBuildSystemTestCorpus();
            ImmutableDictionary<string, string> coverage = CreatePublicInterfaceCoverageMap();
            ImmutableArray<string> publicMethods = GetBuildPublicInterfaceMethods();
            ImmutableArray<string> missingMap = publicMethods
                .Where(method => !coverage.ContainsKey(method))
                .ToImmutableArray();
            ImmutableArray<string> missingTests = coverage
                .Where(entry => !corpus.Contains(entry.Value, StringComparison.Ordinal))
                .Select(static entry => entry.Key + " -> " + entry.Value)
                .ToImmutableArray();

            Assert.True(
                missingMap.IsDefaultOrEmpty,
                "Missing public interface coverage map entries: " + string.Join(", ", missingMap));
            Assert.True(
                missingTests.IsDefaultOrEmpty,
                "Coverage map points at missing tests: " + string.Join(", ", missingTests));
        }

        [Fact]
        public void Step0816_DiagnosticsCppTypesAndNativeContract_AreClosed()
        {
            string corpus = ReadBuildSystemTestCorpus();
            ImmutableArray<string> diagnosticCodes = typeof(BuildDiagnosticCode)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(static field => field.FieldType == typeof(string))
                .Select(static field => (string)field.GetValue(null)!)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
            ImmutableArray<string> qmlTypes = ImmutableArray.Create(
                "int",
                "double",
                "real",
                "number",
                "bool",
                "string",
                "url",
                "color",
                "date",
                "var",
                "variant",
                "list",
                "list<string>",
                "point",
                "rect",
                "size");

            foreach (string code in diagnosticCodes)
            {
                Assert.Contains(code, corpus, StringComparison.Ordinal);
            }

            foreach (string qmlType in qmlTypes)
            {
                Assert.False(string.IsNullOrWhiteSpace(CppTypeMap.ToCppType(qmlType)));
            }

            Assert.True(CppTypeMap.ToCppType("unsupported") == "QVariant");
            Assert.Contains("TypeMap_DesignCppTypes_AreCovered", corpus, StringComparison.Ordinal);
            Assert.Contains("Generate_PatternMatchesMergedNativeHostContract", corpus, StringComparison.Ordinal);
        }

        [Fact]
        public void Step0816_ProductLayoutModesAndArtifactBoundaries_ArePinned()
        {
            string repoRoot = BuildTestFixtures.FindRepositoryRoot();
            string corpus = ReadBuildSystemTestCorpus();
            string productCorpus = ReadSourceCorpus(
                Path.Join(repoRoot, "src", "QmlSharp.Build"),
                Path.Join(repoRoot, "src", "QmlSharp.Cli"),
                Path.Join(repoRoot, "templates"));

            Assert.Contains("ApplicationLayout_CopiesEventBindingsWithoutModification", corpus, StringComparison.Ordinal);
            Assert.Contains("LibraryMode_AssemblesModuleManifestWithoutApplicationOutputs", corpus, StringComparison.Ordinal);
            Assert.Contains("PrebuiltMode_WithExistingNativeLibrary_SucceedsWithoutCMake", corpus, StringComparison.Ordinal);
            Assert.Contains("PrebuiltMode_WithMissingNativeLibrary_FailsBeforeStageEight", corpus, StringComparison.Ordinal);
            Assert.Contains("\"native\", \"generated\"", productCorpus, StringComparison.Ordinal);
            Assert.DoesNotContain("runtimeVersion", productCorpus, StringComparison.Ordinal);
            Assert.DoesNotContain("v1Compat", productCorpus, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Join(repoRoot, "docs")));
        }

        [Fact]
        public void Step0816_DependencyDagAndQmlTsDivergences_AreEnforced()
        {
            string repoRoot = BuildTestFixtures.FindRepositoryRoot();
            ImmutableArray<string> buildReferences = ReadProjectReferences(Path.Join(repoRoot, "src", "QmlSharp.Build", "QmlSharp.Build.csproj"));
            ImmutableArray<string> cliReferences = ReadProjectReferences(Path.Join(repoRoot, "src", "QmlSharp.Cli", "QmlSharp.Cli.csproj"));
            string productCorpus = ReadSourceCorpus(
                Path.Join(repoRoot, "src", "QmlSharp.Build"),
                Path.Join(repoRoot, "src", "QmlSharp.Cli"),
                Path.Join(repoRoot, "templates"));
            ImmutableArray<string> lowerLayerReferences = Directory
                .EnumerateFiles(Path.Join(repoRoot, "src"), "*.csproj", SearchOption.AllDirectories)
                .Where(static path =>
                    !path.EndsWith(Path.Join("QmlSharp.Build", "QmlSharp.Build.csproj"), StringComparison.OrdinalIgnoreCase) &&
                    !path.EndsWith(Path.Join("QmlSharp.Cli", "QmlSharp.Cli.csproj"), StringComparison.OrdinalIgnoreCase))
                .SelectMany(static path => ReadProjectReferences(path).AsEnumerable())
                .ToImmutableArray();
            ImmutableArray<string> forbiddenParityTokens = ImmutableArray.Create(
                "qmlts.config.ts",
                "defineConfig",
                "node_modules",
                "@qmlts",
                "__qmlts",
                "napi-rs",
                "cxx-qt",
                "cargo");

            Assert.Contains(@"..\QmlSharp.Build\QmlSharp.Build.csproj", cliReferences);
            Assert.DoesNotContain(buildReferences, static reference =>
                reference.Contains("QmlSharp.Cli", StringComparison.OrdinalIgnoreCase) ||
                reference.Contains("QmlSharp.DevTools", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(lowerLayerReferences, static reference =>
                reference.Contains("QmlSharp.Build", StringComparison.OrdinalIgnoreCase) ||
                reference.Contains("QmlSharp.Cli", StringComparison.OrdinalIgnoreCase) ||
                reference.Contains("QmlSharp.DevTools", StringComparison.OrdinalIgnoreCase));
            foreach (string forbiddenToken in forbiddenParityTokens)
            {
                Assert.DoesNotContain(forbiddenToken, productCorpus, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static ImmutableArray<string> CreateExpectedTestSpecIds()
        {
            return ImmutableArray.CreateBuilder<string>()
                .AddRangeFluent(CreateRange("CL", 1, 7))
                .AddRangeFluent(CreateRange("CV", 1, 8))
                .AddRangeFluent(CreateRange("BC", 1, 8))
                .AddRangeFluent(CreateRange("DC", 1, 6))
                .AddRangeFluent(CreateRange("IN", 1, 5))
                .AddRangeFluent(CreateRange("DR", 1, 8))
                .AddRangeFluent(CreateRange("CN", 1, 4))
                .AddRangeFluent(CreateRange("BP", 1, 10))
                .AddRangeFluent(CreateRange("CG", 1, 16))
                .AddRangeFluent(ImmutableArray.Create("CG-G1", "CG-G2", "CG-G3", "CG-G4"))
                .AddRangeFluent(CreateRange("PR", 1, 7))
                .AddRangeFluent(CreateRange("RB", 1, 6))
                .AddRangeFluent(CreateRange("PL", 1, 7))
                .AddRangeFluent(CreateRange("PD", 1, 5))
                .AddRangeFluent(CreateRange("EH", 1, 6))
                .AddRangeFluent(CreateRange("PF", 1, 8))
                .ToImmutable();
        }

        private static ImmutableDictionary<string, string> CreatePublicInterfaceCoverageMap()
        {
            return ImmutableDictionary.CreateRange(
                StringComparer.Ordinal,
                new[]
                {
                    InterfaceCoverage<IInitService>(nameof(IInitService.InitAsync), "IN01"),
                    InterfaceCoverage<ICleanService>(nameof(ICleanService.CleanAsync), "CN01"),
                    InterfaceCoverage<ICommandOutput>(nameof(ICommandOutput.WriteLine), "BC04"),
                    InterfaceCoverage<ICommandOutput>(nameof(ICommandOutput.WriteErrorLine), "BC07"),
                    InterfaceCoverage<IConfigLoader>(nameof(IConfigLoader.Load), "CL01"),
                    InterfaceCoverage<IConfigLoader>(nameof(IConfigLoader.Validate), "CV01"),
                    InterfaceCoverage<IConfigLoader>(nameof(IConfigLoader.GetDefaults), "CV07"),
                    InterfaceCoverage<IPlatformDistributor>(nameof(IPlatformDistributor.Package), "PD05"),
                    InterfaceCoverage<IPlatformDistributor>(nameof(IPlatformDistributor.GetNativeLibExtension), "PD01"),
                    InterfaceCoverage<IPlatformDistributor>(nameof(IPlatformDistributor.GetQtRuntimeDependencies), "PD04"),
                    InterfaceCoverage<IDevSession>(nameof(IDevSession.StartAsync), "DC01"),
                    InterfaceCoverage<IDevSession>(nameof(IDevSession.RebuildAsync), "DC02"),
                    InterfaceCoverage<IDevSession>(nameof(IDevSession.OnBuildComplete), "DC01"),
                    InterfaceCoverage<IDevSession>(nameof(IDevSession.OnStateChanged), "DC01"),
                    InterfaceCoverage<IDevHostHook>(nameof(IDevHostHook.StartAsync), "DC01"),
                    InterfaceCoverage<IDevHostHook>(nameof(IDevHostHook.ReloadAsync), "DC02"),
                    InterfaceCoverage<IDevHostHook>(nameof(IDevHostHook.StopAsync), "DC06"),
                    InterfaceCoverage<IDoctor>(nameof(IDoctor.RunAllChecksAsync), "DR01"),
                    InterfaceCoverage<IDoctor>(nameof(IDoctor.RunCheckAsync), "Doctor_RunCheckById_ReturnsOnlyRequestedCheck"),
                    InterfaceCoverage<IDoctor>(nameof(IDoctor.AutoFixAsync), "DR07"),
                    InterfaceCoverage<ICppCodeGenerator>(nameof(ICppCodeGenerator.Generate), "CG16"),
                    InterfaceCoverage<ICppCodeGenerator>(nameof(ICppCodeGenerator.GenerateHeader), "CG01"),
                    InterfaceCoverage<ICppCodeGenerator>(nameof(ICppCodeGenerator.GenerateImplementation), "CG09"),
                    InterfaceCoverage<ICppCodeGenerator>(nameof(ICppCodeGenerator.GenerateCMakeLists), "CG13"),
                    InterfaceCoverage<ICppCodeGenerator>(nameof(ICppCodeGenerator.GenerateTypeRegistration), "CG15"),
                    InterfaceCoverage<ICMakeBuilder>(nameof(ICMakeBuilder.ConfigureAsync), "ConfigureAsync_WithoutPresetFile_UsesSourceBuildDirsAndQtEnvironment"),
                    InterfaceCoverage<ICMakeBuilder>(nameof(ICMakeBuilder.BuildAsync), "BuildAsync_WithPresetFile_UsesRequestedBuildDir"),
                    InterfaceCoverage<ICMakeBuilder>(nameof(ICMakeBuilder.GetOutputLibraryPath), "GetOutputLibraryPath_UsesConfiguredNativeOutputDirectory"),
                    InterfaceCoverage<IQmldirGenerator>(nameof(IQmldirGenerator.Generate), "QD01"),
                    InterfaceCoverage<IQmltypesGenerator>(nameof(IQmltypesGenerator.Generate), "QT01"),
                    InterfaceCoverage<IPackageResolver>(nameof(IPackageResolver.Resolve), "PR01"),
                    InterfaceCoverage<IPackageResolver>(nameof(IPackageResolver.CollectImportPaths), "PR05"),
                    InterfaceCoverage<IPackageResolver>(nameof(IPackageResolver.CollectSchemas), "PR06"),
                    InterfaceCoverage<IResourceBundler>(nameof(IResourceBundler.Collect), "RB01"),
                    InterfaceCoverage<IResourceBundler>(nameof(IResourceBundler.Bundle), "RB03"),
                    InterfaceCoverage<IResourceBundler>(nameof(IResourceBundler.GenerateQrc), "RB05"),
                    InterfaceCoverage<IBuildPipeline>(nameof(IBuildPipeline.BuildAsync), "BP01"),
                    InterfaceCoverage<IBuildPipeline>(nameof(IBuildPipeline.BuildPhasesAsync), "BP07"),
                    InterfaceCoverage<IBuildPipeline>(nameof(IBuildPipeline.OnProgress), "BP06"),
                    InterfaceCoverage<IProductLayout>(nameof(IProductLayout.CreateDirectoryStructure), "PL01"),
                    InterfaceCoverage<IProductLayout>(nameof(IProductLayout.Assemble), "PL02"),
                    InterfaceCoverage<IProductLayout>(nameof(IProductLayout.GenerateManifest), "PL05"),
                    InterfaceCoverage<IProductLayout>(nameof(IProductLayout.ValidateOutput), "PL06"),
                });
        }

        private static ImmutableArray<string> GetBuildPublicInterfaceMethods()
        {
            ImmutableArray<Type> interfaceTypes = ImmutableArray.Create(
                typeof(IInitService),
                typeof(ICleanService),
                typeof(ICommandOutput),
                typeof(IConfigLoader),
                typeof(IPlatformDistributor),
                typeof(IDevSession),
                typeof(IDevHostHook),
                typeof(IDoctor),
                typeof(ICppCodeGenerator),
                typeof(ICMakeBuilder),
                typeof(IQmldirGenerator),
                typeof(IQmltypesGenerator),
                typeof(IPackageResolver),
                typeof(IResourceBundler),
                typeof(IBuildPipeline),
                typeof(IProductLayout));

            return interfaceTypes
                .SelectMany(static type => type
                    .GetMethods()
                    .Where(static method => !method.IsSpecialName)
                    .Select(method => type.Name + "." + method.Name))
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static KeyValuePair<string, string> InterfaceCoverage<TInterface>(string methodName, string testToken)
        {
            return new KeyValuePair<string, string>(typeof(TInterface).Name + "." + methodName, testToken);
        }

        private static void AssertCoverageTokens(
            string corpus,
            ImmutableDictionary<string, ImmutableArray<string>> coverage)
        {
            foreach (KeyValuePair<string, ImmutableArray<string>> entry in coverage)
            {
                ImmutableArray<string> missing = entry.Value
                    .Where(token => !corpus.Contains(token, StringComparison.Ordinal))
                    .ToImmutableArray();
                Assert.True(
                    missing.IsDefaultOrEmpty,
                    $"{entry.Key} missing coverage tokens: {string.Join(", ", missing)}");
            }
        }

        private static KeyValuePair<string, ImmutableArray<string>> Pair(
            string key,
            params string[] values)
        {
            return new KeyValuePair<string, ImmutableArray<string>>(key, values.ToImmutableArray());
        }

        private static ImmutableArray<string> CreateRange(string prefix, int start, int end)
        {
            return Enumerable
                .Range(start, end - start + 1)
                .Select(index => $"{prefix}-{index:00}")
                .ToImmutableArray();
        }

        private static string NormalizeTestId(string testId)
        {
            return testId.Replace("-", string.Empty, StringComparison.Ordinal);
        }

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        private static string ReadBuildSystemTestCorpus()
        {
            string repoRoot = BuildTestFixtures.FindRepositoryRoot();
            return ReadSourceCorpus(
                Path.Join(repoRoot, "tests", "QmlSharp.Build.Tests"),
                Path.Join(repoRoot, "tests", "QmlSharp.Integration.Tests"));
        }

        private static string ReadSourceCorpus(params string[] roots)
        {
            return string.Join(
                "\n",
                roots
                    .Where(Directory.Exists)
                    .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                    .Where(static path => !path.EndsWith("BuildSystemClosureTests.cs", StringComparison.Ordinal))
                    .OrderBy(static path => path, StringComparer.Ordinal)
                    .Select(File.ReadAllText));
        }

        private static ImmutableArray<string> ReadProjectReferences(string projectPath)
        {
            XDocument document = XDocument.Load(projectPath);
            return document
                .Descendants("ProjectReference")
                .Select(static element => (string?)element.Attribute("Include"))
                .Where(static include => include is not null)
                .Select(static include => include!)
                .ToImmutableArray();
        }
    }

    internal static class ImmutableArrayBuilderExtensions
    {
        public static ImmutableArray<T>.Builder AddRangeFluent<T>(
            this ImmutableArray<T>.Builder builder,
            ImmutableArray<T> values)
        {
            builder.AddRange(values);
            return builder;
        }
    }
}
