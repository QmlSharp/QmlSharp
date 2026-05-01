using QmlSharp.Dsl.Generator.Tests.Fixtures;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Attached
{
    public sealed class AttachedPropGeneratorTests
    {
        [Fact]
        public void Generate_AP01_LayoutAttachedType_ReturnsLayoutBuilderMetadata()
        {
            IRegistryQuery registry = DslTestFixtures.CreateAttachedTypesFixture();
            QmlType layoutAttached = registry.FindTypeByQualifiedName("QQuickLayoutAttached")!;
            AttachedPropGenerator generator = new();

            GeneratedAttachedType generated = generator.Generate(layoutAttached, CreateContext(registry, "QtQuick.Layouts"));

            Assert.Equal("Layout", generated.MethodName);
            Assert.Equal("ILayoutAttachedBuilder", generated.BuilderInterfaceName);
            Assert.Equal("QQuickLayoutAttached", generated.TypeName);
        }

        [Fact]
        public void Generate_AP02_KeysAttachedType_ReturnsKeysMethodName()
        {
            IRegistryQuery registry = DslTestFixtures.CreateAttachedTypesFixture();
            QmlType keysAttached = registry.FindTypeByQualifiedName("QQuickKeysAttached")!;
            AttachedPropGenerator generator = new();

            GeneratedAttachedType generated = generator.Generate(keysAttached, CreateContext(registry));

            Assert.Equal("Keys", generated.MethodName);
            Assert.Equal("IKeysAttachedBuilder", generated.BuilderInterfaceName);
        }

        [Fact]
        public void Generate_AP03_LayoutAttachedProperties_ReturnsFluentProperties()
        {
            IRegistryQuery registry = DslTestFixtures.CreateAttachedTypesFixture();
            QmlType layoutAttached = registry.FindTypeByQualifiedName("QQuickLayoutAttached")!;
            AttachedPropGenerator generator = new();

            GeneratedAttachedType generated = generator.Generate(layoutAttached, CreateContext(registry, "QtQuick.Layouts"));

            Assert.Contains(generated.Properties, property => property.Name == "FillWidth"
                && property.SetterSignature == "ILayoutAttachedBuilder FillWidth(bool value)");
            Assert.Contains(generated.Properties, property => property.Name == "FillHeight"
                && property.BindSignature == "ILayoutAttachedBuilder FillHeightBind(string expr)");
        }

        [Fact]
        public void Generate_AP04_KeysAttachedSignals_ReturnsSignalHandlers()
        {
            IRegistryQuery registry = DslTestFixtures.CreateAttachedTypesFixture();
            QmlType keysAttached = registry.FindTypeByQualifiedName("QQuickKeysAttached")!;
            AttachedPropGenerator generator = new();

            GeneratedAttachedType generated = generator.Generate(keysAttached, CreateContext(registry));

            GeneratedSignal pressed = Assert.Single(generated.Signals);
            Assert.Equal("OnPressed", pressed.HandlerName);
            Assert.Equal("IKeysAttachedBuilder OnPressed(Action<object> handler)", pressed.HandlerSignature);
            GeneratedParameter parameter = Assert.Single(pressed.Parameters);
            Assert.Equal("@event", parameter.Name);
            Assert.Equal("object", parameter.CSharpType);
        }

        [Fact]
        public void GetAllAttachedTypes_AP05_EnumeratesAttachedSurfacesFromRegistryDeclarations()
        {
            IRegistryQuery registry = DslTestFixtures.CreateAttachedTypesFixture();
            AttachedPropGenerator generator = new();

            IReadOnlyList<QmlType> attachedTypes = generator.GetAllAttachedTypes(registry);

            Assert.Equal(["QQuickKeysAttached", "QQuickLayoutAttached"], attachedTypes.Select(type => type.QualifiedName));
        }

        [Fact]
        public void Generate_AP06_DerivesMethodNameFromQualifiedAttachedTypeName()
        {
            QmlType attachedType = CreateAttachedType("QQuickLayoutAttached");
            IRegistryQuery registry = CreateRegistry(attachedType);
            AttachedPropGenerator generator = new();

            GeneratedAttachedType generated = generator.Generate(attachedType, CreateContext(registry, "QtQuick.Layouts"));

            Assert.Equal("Layout", generated.MethodName);
            Assert.Equal("ILayoutAttachedBuilder", generated.BuilderInterfaceName);
        }

        [Fact]
        public void GetAllAttachedTypes_UnresolvedAttachedType_ThrowsDsl060Diagnostic()
        {
            QmlType owner = CreateOwnerType("QQuickBrokenLayout", "QQuickMissingAttached");
            IRegistryQuery registry = CreateRegistry(owner);
            AttachedPropGenerator generator = new();

            UnresolvedAttachedTypeException exception = Assert.Throws<UnresolvedAttachedTypeException>(() =>
                generator.GetAllAttachedTypes(registry));

            Assert.Equal(DslDiagnosticCodes.UnresolvedAttachedType, exception.DiagnosticCode);
            Assert.Equal("QQuickMissingAttached", exception.AttachedTypeName);
        }

        [Fact]
        public void Generate_LayoutAndKeysFixtureCases_ResolvePropertiesAndSignals()
        {
            IRegistryQuery registry = DslTestFixtures.CreateAttachedTypesFixture();
            AttachedPropGenerator generator = new();

            IReadOnlyList<GeneratedAttachedType> generated = generator.GetAllAttachedTypes(registry)
                .Select(type => generator.Generate(type, CreateContext(registry, type.ModuleUri ?? "QtQuick")))
                .ToArray();

            Assert.Contains(generated, type => type.MethodName == "Layout" && type.Properties.Any(property => property.Name == "FillWidth"));
            Assert.Contains(generated, type => type.MethodName == "Keys" && type.Signals.Any(signal => signal.HandlerName == "OnPressed"));
        }

        private static GenerationContext CreateContext(IRegistryQuery registry, string currentModuleUri = "QtQuick")
        {
            return new GenerationContext(
                TypeMapper: new QmlSharp.Dsl.Generator.TypeMapper(),
                NameRegistry: new NameRegistry(),
                Registry: registry,
                Options: DslTestFixtures.DefaultOptions,
                CurrentModuleUri: currentModuleUri);
        }

        private static TestRegistryQuery CreateRegistry(params QmlType[] types)
        {
            QmlModule module = new(
                Uri: "QtQuick.Layouts",
                Version: new QmlVersion(2, 15),
                Dependencies: ImmutableArray<string>.Empty,
                Imports: ImmutableArray<string>.Empty,
                Types: types
                    .Where(type => type.QmlName is not null)
                    .Select(type => new QmlModuleType(type.QualifiedName, type.QmlName!, new QmlVersion(2, 15)))
                    .ToImmutableArray());

            return new TestRegistryQuery([module], types, "6.11.0");
        }

        private static QmlType CreateOwnerType(string qualifiedName, string attachedType)
        {
            return new QmlType(
                QualifiedName: qualifiedName,
                QmlName: "BrokenLayout",
                ModuleUri: "QtQuick.Layouts",
                AccessSemantics: AccessSemantics.Reference,
                Prototype: null,
                DefaultProperty: null,
                AttachedType: attachedType,
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: [new QmlTypeExport("QtQuick.Layouts", "BrokenLayout", new QmlVersion(2, 15))],
                Properties: ImmutableArray<QmlProperty>.Empty,
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlType CreateAttachedType(string qualifiedName)
        {
            return new QmlType(
                QualifiedName: qualifiedName,
                QmlName: null,
                ModuleUri: null,
                AccessSemantics: AccessSemantics.Reference,
                Prototype: null,
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: false,
                Exports: ImmutableArray<QmlTypeExport>.Empty,
                Properties: [new QmlProperty("fillWidth", "bool", false, false, false, null, null)],
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }
    }
}
