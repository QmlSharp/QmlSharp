using System.Text;
using QmlSharp.Dsl.Generator.Tests.Fixtures;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Pipeline
{
    public sealed class DeterminismHardeningTests
    {
        [Fact]
        public async Task Generate_UnorderedRegistryInput_ProducesByteIdenticalFileSet()
        {
            GenerationPipeline pipeline = new();

            GenerationResult first = await pipeline.Generate(CreateRegistry(reverseOrder: false), DslTestFixtures.DefaultOptions);
            GenerationResult second = await pipeline.Generate(CreateRegistry(reverseOrder: true), DslTestFixtures.DefaultOptions);

            Assert.Equal(SerializeFileSet(first.Packages), SerializeFileSet(second.Packages));
            Assert.Equal(first.Warnings, second.Warnings);
            Assert.Equal(first.SkippedTypes, second.SkippedTypes);
        }

        [Fact]
        public async Task Generate_P0Modules_OrdersModulesTypesFilesWarningsAndSkippedTypesOrdinally()
        {
            GenerationPipeline pipeline = new();
            GenerationOptions options = DslTestFixtures.DefaultOptions with
            {
                Filter = DslTestFixtures.DefaultOptions.Filter with { ExcludeTypes = ["Rectangle", "Button"] },
            };

            GenerationResult result = await pipeline.Generate(DslTestFixtures.CreateP0ScaleFixture(), options);

            Assert.Equal(
                result.Packages.OrderBy(static package => package.ModuleUri, StringComparer.Ordinal).ToArray(),
                result.Packages.ToArray());
            foreach (GeneratedPackage package in result.Packages)
            {
                Assert.Equal(package.Files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal).ToArray(), package.Files.ToArray());
                Assert.Equal(package.Dependencies.Order(StringComparer.Ordinal).ToArray(), package.Dependencies.ToArray());
            }

            Assert.Equal(
                result.Warnings
                    .OrderBy(static warning => warning.ModuleUri, StringComparer.Ordinal)
                    .ThenBy(static warning => warning.TypeName, StringComparer.Ordinal)
                    .ThenBy(static warning => warning.Code.ToString(), StringComparer.Ordinal)
                    .ThenBy(static warning => warning.Message, StringComparer.Ordinal)
                    .ToArray(),
                result.Warnings.ToArray());
            Assert.Equal(
                result.SkippedTypes
                    .OrderBy(static skipped => skipped.ModuleUri, StringComparer.Ordinal)
                    .ThenBy(static skipped => skipped.TypeName, StringComparer.Ordinal)
                    .ThenBy(static skipped => skipped.Reason, StringComparer.Ordinal)
                    .ToArray(),
                result.SkippedTypes.ToArray());
        }

        private static IRegistryQuery CreateRegistry(bool reverseOrder)
        {
            QmlType qtObject = CreateType("QObject", "QtObject", "QtQml", null, []);
            QmlType item = CreateType("QQuickItem", "Item", "QtQuick", "QObject", [CreateProperty("z", "double"), CreateProperty("width", "double")]);
            QmlType rectangle = CreateType("QQuickRectangle", "Rectangle", "QtQuick", "QQuickItem", [CreateProperty("radius", "double"), CreateProperty("color", "color")]);
            QmlType text = CreateType("QQuickText", "Text", "QtQuick", "QQuickItem", [CreateProperty("text", "string")]);
            QmlType button = CreateType("QQuickButton", "Button", "QtQuick.Controls", "QQuickItem", [CreateProperty("checked", "bool"), CreateProperty("text", "string")]);

            QmlModule qtQml = CreateModule("QtQml", qtObject);
            QmlModule quick = CreateModule("QtQuick", text, rectangle, item);
            QmlModule controls = CreateModule("QtQuick.Controls", button);
            QmlModule[] modules = [controls, quick, qtQml];
            QmlType[] types = [button, text, rectangle, item, qtObject];

            if (!reverseOrder)
            {
                Array.Reverse(modules);
                Array.Reverse(types);
            }

            return new TestRegistryQuery(modules, types, "6.11.0");
        }

        private static string SerializeFileSet(ImmutableArray<GeneratedPackage> packages)
        {
            StringBuilder builder = new();
            foreach (GeneratedPackage package in packages)
            {
                _ = builder.AppendLine(package.PackageName);
                foreach (GeneratedFile file in package.Files)
                {
                    _ = builder.AppendLine(file.RelativePath);
                    _ = builder.AppendLine(file.Content);
                }
            }

            return builder.ToString();
        }

        private static QmlModule CreateModule(string uri, params QmlType[] types)
        {
            return new QmlModule(
                uri,
                new QmlVersion(2, 15),
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty,
                types.Select(static type => new QmlModuleType(type.QualifiedName, type.QmlName ?? type.QualifiedName, new QmlVersion(2, 15))).ToImmutableArray());
        }

        private static QmlType CreateType(string qualifiedName, string? qmlName, string? moduleUri, string? prototype, ImmutableArray<QmlProperty> properties)
        {
            return new QmlType(
                QualifiedName: qualifiedName,
                QmlName: qmlName,
                ModuleUri: moduleUri,
                AccessSemantics: AccessSemantics.Reference,
                Prototype: prototype,
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: moduleUri is null || qmlName is null ? ImmutableArray<QmlTypeExport>.Empty : [new QmlTypeExport(moduleUri, qmlName, new QmlVersion(2, 15))],
                Properties: properties,
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlProperty CreateProperty(string name, string typeName)
        {
            return new QmlProperty(
                Name: name,
                TypeName: typeName,
                IsReadonly: false,
                IsList: false,
                IsRequired: false,
                DefaultValue: null,
                NotifySignal: null);
        }
    }
}
