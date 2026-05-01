using QmlSharp.Dsl.Generator.Tests.Fixtures;
using QmlSharp.Registry;

namespace QmlSharp.Dsl.Generator.Tests.Packager
{
    public sealed class ModulePackagerTests
    {
        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PackageModule_QtQuick_ProducesNuGetShapedFileStructure()
        {
            ModulePackager packager = new();
            QmlModule module = DslTestFixtures.CreateMinimalFixture().FindModule("QtQuick")!;
            Dictionary<string, GeneratedTypeCode> generatedTypes = new(StringComparer.Ordinal)
            {
                ["QQuickRectangle"] = DslTestFixtures.CreateGeneratedRectangleMetadata(),
                ["QQuickText"] = DslTestFixtures.CreateGeneratedTextMetadata(),
            };

            GeneratedPackage package = packager.PackageModule(module, generatedTypes, DslTestFixtures.DefaultOptions.Packager);

            Assert.Equal("QmlSharp.QtQuick", package.PackageName);
            Assert.Equal("QtQuick", package.ModuleUri);
            Assert.Equal("0.1.0", package.PackageVersion);
            Assert.Equal(2, package.Types);
            Assert.Contains(package.Files, file => file.RelativePath == "Rectangle.cs" && file.Kind == GeneratedFileKind.TypeFile);
            Assert.Contains(package.Files, file => file.RelativePath == "Text.cs" && file.Kind == GeneratedFileKind.TypeFile);
            Assert.Contains(package.Files, file => file.RelativePath == "Index.cs" && file.Kind == GeneratedFileKind.IndexFile);
            Assert.Contains(package.Files, file => file.RelativePath == "QmlSharp.QtQuick.csproj" && file.Kind == GeneratedFileKind.ProjectFile);
            Assert.Contains(package.Files, file => file.RelativePath == "README.md" && file.Kind == GeneratedFileKind.ReadmeFile);
            Assert.Equal(package.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray(), package.Files.ToArray());
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PackageModule_Dependencies_IncludesCoreDslAndMappedModuleDependencies()
        {
            ModulePackager packager = new();
            QmlModule module = new(
                Uri: "QtQuick.Controls",
                Version: new QmlVersion(2, 15),
                Dependencies: ["QtQuick"],
                Imports: ["QtQml"],
                Types:
                [
                    new QmlModuleType("QQuickButton", "Button", new QmlVersion(2, 15)),
                ]);
            Dictionary<string, GeneratedTypeCode> generatedTypes = new(StringComparer.Ordinal)
            {
                ["QQuickButton"] = DslTestFixtures.CreateGeneratedButtonMetadata(),
            };

            GeneratedPackage package = packager.PackageModule(module, generatedTypes, DslTestFixtures.DefaultOptions.Packager);

            Assert.Equal(
                ["QmlSharp.Core", "QmlSharp.Dsl", "QmlSharp.QtQml", "QmlSharp.QtQuick"],
                package.Dependencies.ToArray());
            GeneratedFile projectFile = Assert.Single(package.Files, file => file.Kind == GeneratedFileKind.ProjectFile);
            Assert.Contains("PackageReference Include=\"QmlSharp.Core\"", projectFile.Content, StringComparison.Ordinal);
            Assert.Contains("PackageReference Include=\"QmlSharp.Dsl\"", projectFile.Content, StringComparison.Ordinal);
            Assert.Contains("PackageReference Include=\"QmlSharp.QtQuick\"", projectFile.Content, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PackageAll_P0Fixture_GeneratesAllModulesWithGeneratedTypes()
        {
            ModulePackager packager = new();
            Dictionary<string, GeneratedTypeCode> generatedTypes = new(StringComparer.Ordinal)
            {
                ["QObject"] = CreateGeneratedMetadata("QtObject", "QtQml", "QtObject"),
                ["QQuickRectangle"] = DslTestFixtures.CreateGeneratedRectangleMetadata(),
                ["QQuickText"] = DslTestFixtures.CreateGeneratedTextMetadata(),
                ["QQuickButton"] = DslTestFixtures.CreateGeneratedButtonMetadata(),
                ["QQuickLayout"] = CreateGeneratedMetadata("Layout", "QtQuick.Layouts", "Layout"),
            };

            ImmutableArray<GeneratedPackage> packages = packager.PackageAll(
                DslTestFixtures.CreateP0Fixture(),
                generatedTypes,
                DslTestFixtures.DefaultOptions.Packager);

            Assert.Equal(
                ["QmlSharp.QtQml", "QmlSharp.QtQuick", "QmlSharp.QtQuick.Controls", "QmlSharp.QtQuick.Layouts"],
                packages.Select(package => package.PackageName).ToArray());
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public async Task WritePackage_OutputPathWithDotsAndSpaces_WritesExpectedFilesAndCountsBytes()
        {
            using GeneratedOutputTempDirectory tempDirectory = DslTestFixtures.CreateGeneratedOutputTempDirectory();
            string outputRoot = Path.Join(tempDirectory.Path, "generated output.with dots");
            ModulePackager packager = new();
            QmlModule module = DslTestFixtures.CreateMinimalFixture().FindModule("QtQuick")!;
            Dictionary<string, GeneratedTypeCode> generatedTypes = new(StringComparer.Ordinal)
            {
                ["QQuickRectangle"] = DslTestFixtures.CreateGeneratedRectangleMetadata(),
            };
            GeneratedPackage package = packager.PackageModule(module, generatedTypes, DslTestFixtures.DefaultOptions.Packager);

            WrittenPackageInfo written = await packager.WritePackage(package, outputRoot);

            Assert.Equal("QmlSharp.QtQuick", written.PackageName);
            Assert.True(Directory.Exists(written.OutputPath));
            Assert.EndsWith(Path.Join("generated output.with dots", "QmlSharp.QtQuick"), written.OutputPath, StringComparison.Ordinal);
            Assert.Equal(package.Files.Length, written.FileCount);
            Assert.Equal(package.Files.Sum(file => System.Text.Encoding.UTF8.GetByteCount(file.Content)), written.TotalBytes);
            Assert.True(File.Exists(Path.Join(written.OutputPath, "QmlSharp.QtQuick.csproj")));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PackageModule_GeneratedFileNames_DoNotContainInvalidPathSeparators()
        {
            ModulePackager packager = new();
            QmlModule module = new(
                Uri: "QtQuick",
                Version: new QmlVersion(2, 15),
                Dependencies: ImmutableArray<string>.Empty,
                Imports: ImmutableArray<string>.Empty,
                Types:
                [
                    new QmlModuleType("QQuickBadName", "Bad/Name", new QmlVersion(2, 15)),
                ]);
            Dictionary<string, GeneratedTypeCode> generatedTypes = new(StringComparer.Ordinal)
            {
                ["QQuickBadName"] = DslTestFixtures.CreateGeneratedRectangleMetadata() with
                {
                    QmlName = "Bad/Name",
                    FactoryName = "Bad/Name",
                },
            };

            GeneratedPackage package = packager.PackageModule(module, generatedTypes, DslTestFixtures.DefaultOptions.Packager);

            Assert.DoesNotContain(package.Files, file => file.RelativePath.Contains('/') || file.RelativePath.Contains('\\'));
            Assert.Contains(package.Files, file => file.RelativePath == "Bad_Name.cs");
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PackageModule_EmptyModule_ThrowsDsl090()
        {
            ModulePackager packager = new();
            QmlModule module = new(
                Uri: "QtQuick.Empty",
                Version: new QmlVersion(2, 15),
                Dependencies: ImmutableArray<string>.Empty,
                Imports: ImmutableArray<string>.Empty,
                Types: ImmutableArray<QmlModuleType>.Empty);

            DslGenerationException exception = Assert.Throws<DslGenerationException>(
                () => packager.PackageModule(module, new Dictionary<string, GeneratedTypeCode>(StringComparer.Ordinal), DslTestFixtures.DefaultOptions.Packager));

            Assert.Equal(DslDiagnosticCodes.EmptyModule, exception.DiagnosticCode);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public async Task WritePackage_MissingRequiredDependency_ThrowsDsl091()
        {
            using GeneratedOutputTempDirectory tempDirectory = DslTestFixtures.CreateGeneratedOutputTempDirectory();
            GeneratedPackage package = new(
                PackageName: "QmlSharp.QtQuick",
                ModuleUri: "QtQuick",
                PackageVersion: "0.1.0",
                Files:
                [
                    new GeneratedFile("Index.cs", "// <auto-generated />\n", GeneratedFileKind.IndexFile),
                ],
                Types: 0,
                Dependencies: ["QmlSharp.Core"],
                Stats: new PackageStats(
                    TotalTypes: 0,
                    CreatableTypes: 0,
                    NonCreatableTypes: 0,
                    EnumCount: 0,
                    AttachedTypeCount: 0,
                    TotalLinesOfCode: 1,
                    TotalFileSize: 22));
            ModulePackager packager = new();

            DslGenerationException exception = await Assert.ThrowsAsync<DslGenerationException>(
                () => packager.WritePackage(package, tempDirectory.Path));

            Assert.Equal(DslDiagnosticCodes.MissingDependency, exception.DiagnosticCode);
        }

        private static GeneratedTypeCode CreateGeneratedMetadata(string qmlName, string moduleUri, string factoryName)
        {
            return DslTestFixtures.CreateGeneratedRectangleMetadata() with
            {
                QmlName = qmlName,
                ModuleUri = moduleUri,
                FactoryName = factoryName,
                PropsInterfaceName = $"I{factoryName}Props",
                BuilderInterfaceName = $"I{factoryName}Builder",
                FactoryMethodCode = $"public static I{factoryName}Builder {factoryName}() => ObjectFactory.Create<I{factoryName}Builder>(\"{qmlName}\");",
                Properties = ImmutableArray<GeneratedProperty>.Empty,
                Signals = ImmutableArray<GeneratedSignal>.Empty,
                Methods = ImmutableArray<GeneratedMethod>.Empty,
                Enums = ImmutableArray<GeneratedEnum>.Empty,
                AttachedTypes = ImmutableArray<GeneratedAttachedType>.Empty,
                DefaultProperty = null,
                IsCreatable = true,
                IsDeprecated = false,
            };
        }
    }
}
