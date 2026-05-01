using System.Xml.Linq;
using QmlSharp.Dsl.Generator.Tests.Fixtures;

namespace QmlSharp.Dsl.Generator.Tests.Emitter
{
    public sealed class CodeEmitterTests
    {
        private static readonly CodeEmitOptions DefaultOptions = DslTestFixtures.DefaultEmitOptions;

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void EmitTypeFile_Rectangle_EmitsNamespaceInterfacesBuilderAndFactory()
        {
            CodeEmitter emitter = new();

            string output = emitter.EmitTypeFile(DslTestFixtures.CreateGeneratedRectangleMetadata(), DefaultOptions);

            Assert.Contains("namespace QmlSharp.QtQuick;", output, StringComparison.Ordinal);
            Assert.Contains("public interface IRectangleProps", output, StringComparison.Ordinal);
            Assert.Contains("public interface IRectangleBuilder : IObjectBuilder", output, StringComparison.Ordinal);
            Assert.Contains("IRectangleBuilder Width(double value);", output, StringComparison.Ordinal);
            Assert.Contains("IRectangleBuilder ColorBind(string expr);", output, StringComparison.Ordinal);
            Assert.Contains("public static IRectangleBuilder Rectangle()", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void EmitTypeFile_GenerateXmlDocTrue_EmitsXmlDocs()
        {
            CodeEmitter emitter = new();

            string output = emitter.EmitTypeFile(DslTestFixtures.CreateGeneratedRectangleMetadata(), DefaultOptions with { GenerateXmlDoc = true });

            Assert.Contains("/// <summary>", output, StringComparison.Ordinal);
            Assert.Contains("/// Builder for Rectangle.", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void EmitTypeFile_GenerateXmlDocFalse_OmitsXmlDocs()
        {
            CodeEmitter emitter = new();

            string output = emitter.EmitTypeFile(DslTestFixtures.CreateGeneratedRectangleMetadata(), DefaultOptions with { GenerateXmlDoc = false });

            Assert.DoesNotContain("///", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void EmitTypeFile_DeprecatedTypeAndMarkDeprecatedTrue_EmitsObsoleteAttribute()
        {
            CodeEmitter emitter = new();
            GeneratedTypeCode metadata = DslTestFixtures.CreateGeneratedRectangleMetadata() with { IsDeprecated = true };

            string output = emitter.EmitTypeFile(metadata, DefaultOptions with { MarkDeprecated = true });

            Assert.Contains("[Obsolete]", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void EmitTypeFile_HeaderCommentSet_EmitsHeaderFirst()
        {
            CodeEmitter emitter = new();

            string output = emitter.EmitTypeFile(
                DslTestFixtures.CreateGeneratedRectangleMetadata(),
                DefaultOptions with { HeaderComment = "// GENERATED - DO NOT EDIT" });

            Assert.StartsWith("// GENERATED - DO NOT EDIT\n", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void EmitIndexFile_CreatableTypes_EmitsSortedFactoryAggregation()
        {
            CodeEmitter emitter = new();

            string output = emitter.EmitIndexFile(
                [DslTestFixtures.CreateGeneratedTextMetadata(), DslTestFixtures.CreateGeneratedRectangleMetadata()],
                ImmutableArray<GeneratedEnum>.Empty);

            Assert.Contains("public static class QtQuickFactories", output, StringComparison.Ordinal);
            Assert.Contains("RectangleFactory.Rectangle()", output, StringComparison.Ordinal);
            Assert.Contains("TextFactory.Text()", output, StringComparison.Ordinal);
            Assert.True(
                output.IndexOf("RectangleFactory", StringComparison.Ordinal) < output.IndexOf("TextFactory", StringComparison.Ordinal),
                "Factory aggregation must be emitted in deterministic name order.");
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void EmitProjectFile_PackageMetadata_EmitsValidProjectXml()
        {
            CodeEmitter emitter = new();
            GeneratedPackage package = CreatePackage();

            string output = emitter.EmitProjectFile(package);
            XDocument document = XDocument.Parse(output);

            Assert.Equal("Project", document.Root?.Name.LocalName);
            Assert.Contains("PackageId>QmlSharp.QtQuick<", output, StringComparison.Ordinal);
            Assert.True(
                output.IndexOf("QmlSharp.Core", StringComparison.Ordinal) < output.IndexOf("QmlSharp.Dsl", StringComparison.Ordinal),
                "Package dependencies must be emitted in deterministic order.");
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void EmitCommonTypes_ReturnsForwardingSourceWithCoreValueTypeNames()
        {
            CodeEmitter emitter = new();

            string output = emitter.EmitCommonTypes();

            Assert.Contains("QmlSharp.Core", output, StringComparison.Ordinal);
            Assert.Contains("QmlColor", output, StringComparison.Ordinal);
            Assert.Contains("QmlPoint", output, StringComparison.Ordinal);
            Assert.Contains("QmlSize", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void EmitViewModelHelpers_ViewModelBindingInfo_EmitsProxyBaseClass()
        {
            IViewModelIntegration integration = new ViewModelIntegration();
            ViewModelBindingInfo info = integration.AnalyzeSchema(DslTestFixtures.CreateCounterViewModelSchema());
            CodeEmitter emitter = new();

            string output = emitter.EmitViewModelHelpers(info);

            Assert.Contains("CounterViewModelProxy", output, StringComparison.Ordinal);
            Assert.Contains("public abstract int Count { get; }", output, StringComparison.Ordinal);
            Assert.Contains("public abstract void Increment();", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void EmitTypeFile_NonCreatableType_OmitsFactoryMethod()
        {
            CodeEmitter emitter = new();
            GeneratedTypeCode metadata = DslTestFixtures.CreateGeneratedRectangleMetadata() with
            {
                IsCreatable = false,
                FactoryMethodCode = null,
            };

            string output = emitter.EmitTypeFile(metadata, DefaultOptions);

            Assert.Contains("public interface IRectangleBuilder", output, StringComparison.Ordinal);
            Assert.DoesNotContain("public static IRectangleBuilder Rectangle()", output, StringComparison.Ordinal);
            Assert.DoesNotContain("RectangleFactory", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void EmitTypeFile_GroupedAndAttachedMetadata_EmitsBuilderSurfaces()
        {
            CodeEmitter emitter = new();

            string output = emitter.EmitTypeFile(DslTestFixtures.CreateGeneratedRectangleMetadata(), DefaultOptions);

            Assert.Contains("public interface IBorderBuilder : IPropertyCollector", output, StringComparison.Ordinal);
            Assert.Contains("IRectangleBuilder Border(Action<IBorderBuilder> setup);", output, StringComparison.Ordinal);
            Assert.Contains("public interface ILayoutBuilder : IPropertyCollector", output, StringComparison.Ordinal);
            Assert.Contains("IRectangleBuilder Layout(Action<ILayoutBuilder> setup);", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void EmitTypeFile_RepeatedEmission_ReturnsIdenticalOutput()
        {
            CodeEmitter emitter = new();
            GeneratedTypeCode metadata = DslTestFixtures.CreateGeneratedButtonMetadata();

            string first = emitter.EmitTypeFile(metadata, DefaultOptions);
            string second = emitter.EmitTypeFile(metadata, DefaultOptions);

            Assert.Equal(first, second);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void EmitTypeFile_Output_UsesLfLineEndings()
        {
            CodeEmitter emitter = new();

            string output = emitter.EmitTypeFile(DslTestFixtures.CreateGeneratedRectangleMetadata(), DefaultOptions);

            Assert.DoesNotContain("\r", output, StringComparison.Ordinal);
            Assert.Contains('\n', output);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void EmitTypeFile_Output_DoesNotContainTypeScriptOrQmlTsTerms()
        {
            CodeEmitter emitter = new();

            string output = emitter.EmitTypeFile(DslTestFixtures.CreateGeneratedRectangleMetadata(), DefaultOptions);

            Assert.DoesNotContain("export ", output, StringComparison.Ordinal);
            Assert.DoesNotContain("from './", output, StringComparison.Ordinal);
            Assert.DoesNotContain("npm", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("QmlTS", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("@qmlts", output, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void EmitTypeFile_InvalidMetadata_ThrowsDsl080()
        {
            CodeEmitter emitter = new();
            GeneratedTypeCode metadata = DslTestFixtures.CreateGeneratedRectangleMetadata() with { QmlName = string.Empty };

            DslGenerationException exception = Assert.Throws<DslGenerationException>(() => emitter.EmitTypeFile(metadata, DefaultOptions));

            Assert.Equal(DslDiagnosticCodes.EmitFailure, exception.DiagnosticCode);
        }

        private static GeneratedPackage CreatePackage()
        {
            return new GeneratedPackage(
                PackageName: "QmlSharp.QtQuick",
                ModuleUri: "QtQuick",
                Files:
                [
                    new GeneratedFile("Rectangle.cs", "content", GeneratedFileKind.TypeFile),
                ],
                Types: 1,
                Dependencies: ["QmlSharp.Dsl", "QmlSharp.Core"],
                Stats: new PackageStats(
                    TotalTypes: 1,
                    CreatableTypes: 1,
                    NonCreatableTypes: 0,
                    EnumCount: 0,
                    AttachedTypeCount: 0,
                    TotalLinesOfCode: 1,
                    TotalFileSize: 7));
        }
    }
}
