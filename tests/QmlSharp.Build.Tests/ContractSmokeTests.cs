using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class ContractSmokeTests
    {
        [Fact]
        public void QmlSharpConfig_Defaults_MatchApiDesignBaseline()
        {
            QmlSharpConfig config = BuildTestFixtures.CreateDefaultConfig();

            Assert.Equal("./dist", config.OutDir);
            Assert.Equal("development", config.Build.Mode);
            Assert.True(config.Build.Lint);
            Assert.True(config.Build.Format);
            Assert.True(config.Build.SourceMaps);
            Assert.True(config.Build.Incremental);
            Assert.False(config.Native.Prebuilt);
            Assert.Equal("default", config.Native.CmakePreset);
            Assert.True(config.Dev.HotReload);
            Assert.Equal(200, config.Dev.DebounceMs);
            Assert.True(config.Dev.WatchPaths.SequenceEqual(ImmutableArray.Create("./src")));
            Assert.Equal("QmlSharp.MyApp", config.Module.Prefix);
            Assert.Equal(new QmlSharp.Build.QmlVersion(1, 0), config.Module.Version);
        }

        [Fact]
        public void BuildContracts_DefaultRecords_AreConstructible()
        {
            BuildContext context = BuildTestFixtures.CreateDefaultContext();
            BuildResult result = BuildTestFixtures.CreateSuccessfulBuildResult();
            PhaseResult phase = new(
                BuildPhase.ConfigLoading,
                true,
                TimeSpan.Zero,
                ImmutableArray<BuildDiagnostic>.Empty);
            BuildArtifacts artifacts = new();
            ProductManifest manifest = new(
                "MyApp",
                "1.0.0",
                "development",
                DateTimeOffset.UnixEpoch,
                "6.11.0",
                Environment.Version.ToString(),
                ImmutableArray.Create("QmlSharp.MyApp"),
                ImmutableArray.Create("CounterViewModel"),
                ImmutableDictionary<string, string>.Empty,
                "native/qmlsharp_native.dll",
                "managed/MyApp.dll");

            Assert.Equal("C:/Qt/6.11.0/msvc2022_64", context.QtDir);
            Assert.True(result.Success);
            Assert.True(phase.Success);
            Assert.Empty(artifacts.QmlFiles);
            Assert.Equal("MyApp", manifest.ProjectName);
        }

        [Fact]
        public async Task TestDoubles_ReturnDeterministicSuccessfulResults()
        {
            MockCompiler compiler = new();
            MockQmlFormat formatter = new();
            MockQmlLint linter = new();
            MockCMakeBuilder cmake = new();

            CompilationResult compilation = compiler.Compile(new CompilerOptions
            {
                ProjectPath = "Mock.csproj",
                OutputDir = "dist",
                ModuleUriPrefix = "QmlSharp.MyApp",
            });
            QmlFormatResult formatResult = await formatter.FormatStringAsync("Item {}\n");
            QmlLintResult lintResult = await linter.LintStringAsync("Item {}\n");
            CMakeStepResult configure = await cmake.ConfigureAsync("build", "windows-ci");
            CMakeStepResult build = await cmake.BuildAsync("build");

            Assert.True(compilation.Success);
            Assert.True(formatResult.Success);
            Assert.True(lintResult.Success);
            Assert.True(configure.Success);
            Assert.True(build.Success);
            Assert.Equal("Use test-isolated real filesystem roots created under the OS temp directory.", BuildTestFixtures.FileSystemDecision);
        }

        [Fact]
        public void BuildPhase_ContainsExactlyEightCanonicalStages()
        {
            BuildPhase[] phases = Enum.GetValues<BuildPhase>();

            Assert.Equal(8, phases.Length);
            Assert.Equal(1, (int)BuildPhase.ConfigLoading);
            Assert.Equal(8, (int)BuildPhase.OutputAssembly);
        }
    }
}
