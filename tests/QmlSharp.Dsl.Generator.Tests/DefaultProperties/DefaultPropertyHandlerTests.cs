using QmlSharp.Dsl.Generator.Tests.Fixtures;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.DefaultProperties
{
    public sealed class DefaultPropertyHandlerTests
    {
        [Fact]
        public void Analyze_DP01_ListDefaultProperty_GeneratesChildAndChildrenMetadata()
        {
            QmlType item = CreateType(
                "QQuickItem",
                "Item",
                defaultProperty: "data",
                properties: [CreateProperty("data", "Item", isList: true)]);
            ResolvedType resolved = Resolve(item);
            DefaultPropertyHandler handler = new();

            DefaultPropertyInfo info = handler.Analyze(resolved)!;

            Assert.Equal("data", info.PropertyName);
            Assert.True(info.IsList);
            Assert.True(info.GenerateChildMethod);
            Assert.True(info.GenerateChildrenMethod);
        }

        [Fact]
        public void Analyze_DP02_SingleDefaultProperty_GeneratesOnlyChildMetadata()
        {
            QmlType loader = CreateType(
                "QQuickLoader",
                "Loader",
                defaultProperty: "sourceComponent",
                properties: [CreateProperty("sourceComponent", "Component")]);
            ResolvedType resolved = Resolve(loader);
            DefaultPropertyHandler handler = new();

            DefaultPropertyInfo info = handler.Analyze(resolved)!;

            Assert.False(info.IsList);
            Assert.True(info.GenerateChildMethod);
            Assert.False(info.GenerateChildrenMethod);
        }

        [Fact]
        public void Analyze_DP03_NoDefaultProperty_ReturnsNull()
        {
            QmlType text = CreateType("QQuickText", "Text");
            ResolvedType resolved = Resolve(text);
            DefaultPropertyHandler handler = new();

            DefaultPropertyInfo? info = handler.Analyze(resolved);

            Assert.Null(info);
        }

        [Fact]
        public void Analyze_DP04_ListDefaultProperty_IdentifiesElementType()
        {
            QmlType item = CreateType(
                "QQuickItem",
                "Item",
                defaultProperty: "data",
                properties: [CreateProperty("data", "Item", isList: true)]);
            ResolvedType resolved = Resolve(item);
            DefaultPropertyHandler handler = new();

            DefaultPropertyInfo info = handler.Analyze(resolved)!;

            Assert.Equal("Item", info.ElementType);
        }

        [Fact]
        public void GenerateMethods_DP05_ListDefaultProperty_ReturnsChildAndChildrenSignatures()
        {
            DefaultPropertyInfo info = new(
                PropertyName: "data",
                ElementType: "Item",
                IsList: true,
                GenerateChildMethod: true,
                GenerateChildrenMethod: true);
            DefaultPropertyHandler handler = new();

            ImmutableArray<string> methods = handler.GenerateMethods(info, CreateContext());

            Assert.Equal(["Child(IObjectBuilder obj)", "Children(params IObjectBuilder[] objs)"], methods.ToArray());
        }

        [Fact]
        public void Analyze_InheritedDefaultProperty_FlowsThroughGeneration()
        {
            QmlType item = CreateType(
                "QQuickItem",
                "Item",
                defaultProperty: "data",
                properties: [CreateProperty("data", "Item", isList: true)]);
            QmlType rectangle = CreateType("QQuickRectangle", "Rectangle", prototype: "QQuickItem");
            IRegistryQuery registry = CreateRegistry(item, rectangle);
            ResolvedType resolved = new InheritanceResolver().Resolve(rectangle, registry);
            DefaultPropertyHandler handler = new();

            DefaultPropertyInfo info = handler.Analyze(resolved)!;

            Assert.Equal("data", info.PropertyName);
            Assert.True(info.IsList);
            Assert.Equal("Item", info.ElementType);
        }

        private static GenerationContext CreateContext()
        {
            return new GenerationContext(
                TypeMapper: new QmlSharp.Dsl.Generator.TypeMapper(),
                NameRegistry: new NameRegistry(),
                Registry: DslTestFixtures.CreateMinimalFixture(),
                Options: DslTestFixtures.DefaultOptions,
                CurrentModuleUri: "QtQuick");
        }

        private static ResolvedType Resolve(QmlType type)
        {
            return new InheritanceResolver().Resolve(type, CreateRegistry(type));
        }

        private static TestRegistryQuery CreateRegistry(params QmlType[] types)
        {
            ImmutableArray<QmlModuleType> moduleTypes = types
                .Where(type => type.QmlName is not null)
                .OrderBy(type => type.QmlName, StringComparer.Ordinal)
                .Select(type => new QmlModuleType(type.QualifiedName, type.QmlName!, new QmlVersion(2, 15)))
                .ToImmutableArray();
            QmlModule module = new(
                Uri: "QtQuick",
                Version: new QmlVersion(2, 15),
                Dependencies: ImmutableArray<string>.Empty,
                Imports: ImmutableArray<string>.Empty,
                Types: moduleTypes);

            return new TestRegistryQuery([module], types.OrderBy(type => type.QualifiedName, StringComparer.Ordinal).ToArray(), "6.11.0");
        }

        private static QmlProperty CreateProperty(string name, string typeName, bool isList = false)
        {
            return new QmlProperty(
                Name: name,
                TypeName: typeName,
                IsReadonly: false,
                IsList: isList,
                IsRequired: false,
                DefaultValue: null,
                NotifySignal: null);
        }

        private static QmlType CreateType(
            string qualifiedName,
            string? qmlName,
            string? prototype = null,
            string? defaultProperty = null,
            ImmutableArray<QmlProperty> properties = default)
        {
            return new QmlType(
                QualifiedName: qualifiedName,
                QmlName: qmlName,
                ModuleUri: "QtQuick",
                AccessSemantics: AccessSemantics.Reference,
                Prototype: prototype,
                DefaultProperty: defaultProperty,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: qmlName is not null,
                Exports: qmlName is null ? ImmutableArray<QmlTypeExport>.Empty : [new QmlTypeExport("QtQuick", qmlName, new QmlVersion(2, 15))],
                Properties: properties.IsDefault ? ImmutableArray<QmlProperty>.Empty : properties,
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }
    }
}
