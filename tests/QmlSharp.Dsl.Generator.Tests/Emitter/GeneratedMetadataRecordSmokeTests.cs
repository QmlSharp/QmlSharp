using QmlSharp.Dsl.Generator.Tests.Fixtures;

namespace QmlSharp.Dsl.Generator.Tests.Emitter
{
    public sealed class GeneratedMetadataRecordSmokeTests
    {
        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void GeneratedTypeCode_ConstructsExpectedMetadataGraph()
        {
            GeneratedTypeCode metadata = DslTestFixtures.CreateGeneratedRectangleMetadata();

            Assert.Equal("Rectangle", metadata.QmlName);
            Assert.Equal("QtQuick", metadata.ModuleUri);
            Assert.Equal("IRectangleBuilder", metadata.BuilderInterfaceName);
            Assert.True(metadata.IsCreatable);
            Assert.False(metadata.IsDeprecated);
            Assert.Contains(metadata.Properties, property => property.Name == "Color" && property.CSharpType == "QmlColor");
            Assert.Contains(metadata.Signals, signal => signal.HandlerName == "OnColorChanged");
            Assert.NotNull(metadata.DefaultProperty);
        }

        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void GenerationResult_ConstructsWarningsStatsAndSkippedTypes()
        {
            GenerationResult result = new(
                Packages:
                [
                    new GeneratedPackage(
                        PackageName: "QmlSharp.QtQuick",
                        ModuleUri: "QtQuick",
                        Files:
                        [
                            new GeneratedFile("Rectangle.cs", "content", GeneratedFileKind.TypeFile),
                        ],
                        Types: 1,
                        Dependencies: ["QmlSharp.Core", "QmlSharp.Dsl"],
                        Stats: new PackageStats(
                            TotalTypes: 1,
                            CreatableTypes: 1,
                            NonCreatableTypes: 0,
                            EnumCount: 0,
                            AttachedTypeCount: 0,
                            TotalLinesOfCode: 1,
                            TotalFileSize: 7)),
                ],
                Stats: new GenerationStats(
                    TotalPackages: 1,
                    TotalTypes: 1,
                    TotalFiles: 1,
                    TotalBytes: 7,
                    ElapsedTime: TimeSpan.FromMilliseconds(1)),
                Warnings:
                [
                    new GenerationWarning(
                        GenerationWarningCode.UnresolvedTypeReference,
                        "warning",
                        "Rectangle",
                        "QtQuick"),
                ],
                SkippedTypes:
                [
                    new SkippedType("InternalType", "QtQuick", "Excluded by fixture"),
                ]);

            _ = Assert.Single(result.Packages);
            _ = Assert.Single(result.Warnings);
            _ = Assert.Single(result.SkippedTypes);
            Assert.Equal(1, result.Stats.TotalTypes);
        }
    }
}
