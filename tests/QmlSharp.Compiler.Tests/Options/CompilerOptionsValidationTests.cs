using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.Options
{
    public sealed class CompilerOptionsValidationTests
    {
        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CompilerOptions_CO01_ValidOptionsWithRequiredFields_HaveNoValidationErrors()
        {
            CompilerOptions options = CreateValidOptions();

            CompilerOptions normalized = options.ValidateAndNormalize();

            Assert.Equal(options.ProjectPath, normalized.ProjectPath);
            Assert.Equal(options.OutputDir, normalized.OutputDir);
            Assert.Equal(options.ModuleUriPrefix, normalized.ModuleUriPrefix);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CompilerOptions_CO02_MissingProjectPath_ThrowsArgumentException()
        {
            CompilerOptions options = CreateValidOptions() with { ProjectPath = null! };

            ArgumentException exception = Assert.Throws<ArgumentException>(options.ValidateAndNormalize);
            Assert.Equal("ProjectPath", exception.ParamName);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CompilerOptions_CO03_MissingOutputDir_ThrowsArgumentException()
        {
            CompilerOptions options = CreateValidOptions() with { OutputDir = " " };

            ArgumentException exception = Assert.Throws<ArgumentException>(options.ValidateAndNormalize);
            Assert.Equal("OutputDir", exception.ParamName);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CompilerOptions_CO04_MissingModuleUriPrefix_ThrowsArgumentException()
        {
            CompilerOptions options = CreateValidOptions() with { ModuleUriPrefix = string.Empty };

            ArgumentException exception = Assert.Throws<ArgumentException>(options.ValidateAndNormalize);
            Assert.Equal("ModuleUriPrefix", exception.ParamName);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CompilerOptions_CO05_DefaultValuesPopulated_AfterNormalization()
        {
            CompilerOptions options = CreateValidOptions() with
            {
                SourceMapDir = null,
                CacheDir = null,
                IncludePatterns = ImmutableArray<string>.Empty,
                AdditionalAnalyzers = default,
            };

            CompilerOptions normalized = options.ValidateAndNormalize();

            Assert.True(normalized.Incremental);
            Assert.True(normalized.GenerateSourceMaps);
            Assert.Equal(["**/*.cs"], normalized.IncludePatterns.ToArray());
            Assert.Equal(Path.Join("dist", "source-maps"), normalized.SourceMapDir);
            Assert.Equal(Path.Join("dist", ".compiler-cache"), normalized.CacheDir);
            Assert.Empty(normalized.AdditionalAnalyzers);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CompilerOptions_CO06_CustomExcludePatterns_AreMergedWithDefaults()
        {
            CompilerOptions options = CreateValidOptions() with
            {
                ExcludePatterns = ImmutableArray.Create("**/generated/**", "**/bin/**", " "),
            };

            CompilerOptions normalized = options.ValidateAndNormalize();

            Assert.Contains("**/obj/**", normalized.ExcludePatterns);
            Assert.Contains("**/bin/**", normalized.ExcludePatterns);
            Assert.Contains("**/*Tests*/**", normalized.ExcludePatterns);
            Assert.Contains("**/generated/**", normalized.ExcludePatterns);
            Assert.Equal(4, normalized.ExcludePatterns.Length);
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(1, -1)]
        [Trait("Category", TestCategories.Unit)]
        public void CompilerOptions_InvalidModuleVersion_ThrowsArgumentException(int major, int minor)
        {
            CompilerOptions options = CreateValidOptions() with
            {
                ModuleVersion = new QmlVersion(major, minor),
            };

            ArgumentException exception = Assert.Throws<ArgumentException>(options.ValidateAndNormalize);
            Assert.Equal("moduleVersion", exception.ParamName);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CompilerOptions_MaxAllowedSeverity_DecidesBlockingDiagnostics()
        {
            CompilerOptions options = CreateValidOptions() with
            {
                MaxAllowedSeverity = DiagnosticSeverity.Warning,
            };

            Assert.True(options.Allows(DiagnosticSeverity.Info));
            Assert.True(options.Allows(DiagnosticSeverity.Warning));
            Assert.True(options.ShouldStopOn(DiagnosticSeverity.Error));
            Assert.True(options.ShouldStopOn(DiagnosticSeverity.Fatal));
        }

        private static CompilerOptions CreateValidOptions()
        {
            return new CompilerOptions
            {
                ProjectPath = "TestApp.csproj",
                OutputDir = "dist",
                ModuleUriPrefix = "QmlSharp.TestApp",
            };
        }
    }
}
