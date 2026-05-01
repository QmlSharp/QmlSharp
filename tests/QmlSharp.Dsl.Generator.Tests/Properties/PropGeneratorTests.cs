using QmlSharp.Dsl.Generator.Tests.Fixtures;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Properties
{
    public sealed class PropGeneratorTests
    {
        [Fact]
        public void Generate_PG01_IntProperty_ReturnsBuilderSetterSignature()
        {
            ResolvedProperty property = CreateResolvedProperty(CreateProperty("width", "int"), CreateType("QQuickRectangle", "Rectangle"));
            PropGenerator generator = new();

            GeneratedProperty generated = Generate(generator, property, CreateContext());

            Assert.Equal("Width", generated.Name);
            Assert.Equal("IRectangleBuilder Width(int value)", generated.SetterSignature);
            Assert.Equal("int", generated.CSharpType);
        }

        [Fact]
        public void Generate_PG02_StringProperty_UsesStringParameter()
        {
            ResolvedProperty property = CreateResolvedProperty(CreateProperty("text", "string"), CreateType("QQuickText", "Text"));
            PropGenerator generator = new();

            GeneratedProperty generated = Generate(generator, property, CreateContext());

            Assert.Equal("ITextBuilder Text(string value)", generated.SetterSignature);
        }

        [Fact]
        public void Generate_PG03_ColorProperty_UsesQmlColorParameter()
        {
            ResolvedProperty property = CreateResolvedProperty(CreateProperty("color", "color"), CreateType("QQuickRectangle", "Rectangle"));
            PropGenerator generator = new();

            GeneratedProperty generated = Generate(generator, property, CreateContext());

            Assert.Equal("IRectangleBuilder Color(QmlColor value)", generated.SetterSignature);
            Assert.Equal("QmlColor", generated.CSharpType);
        }

        [Fact]
        public void Generate_PG04_BindableProperty_ReturnsBindSignature()
        {
            ResolvedProperty property = CreateResolvedProperty(CreateProperty("width", "double"), CreateType("QQuickRectangle", "Rectangle"));
            PropGenerator generator = new();

            GeneratedProperty generated = Generate(generator, property, CreateContext());

            Assert.Equal("IRectangleBuilder WidthBind(string expr)", generated.BindSignature);
        }

        [Fact]
        public void Generate_PG05_ReadOnlyProperty_DoesNotEmitSetterOrBindMethod()
        {
            ResolvedProperty property = CreateResolvedProperty(
                CreateProperty("activeFocus", "bool", isReadonly: true),
                CreateType("QQuickItem", "Item"));
            PropGenerator generator = new();

            GeneratedProperty generated = Generate(generator, property, CreateContext());

            Assert.True(generated.IsReadOnly);
            Assert.Equal(string.Empty, generated.SetterSignature);
            Assert.Null(generated.BindSignature);
        }

        [Fact]
        public void Generate_PG06_RequiredProperty_PreservesMetadata()
        {
            ResolvedProperty property = CreateResolvedProperty(
                CreateProperty("source", "url", isRequired: true),
                CreateType("QQuickImage", "Image"));
            PropGenerator generator = new();

            GeneratedProperty generated = Generate(generator, property, CreateContext());

            Assert.True(generated.IsRequired);
        }

        [Fact]
        public void Generate_PG07_PascalCaseName_ConvertsFromQmlName()
        {
            ResolvedProperty property = CreateResolvedProperty(CreateProperty("implicitWidth", "double"), CreateType("QQuickItem", "Item"));
            PropGenerator generator = new();

            GeneratedProperty generated = Generate(generator, property, CreateContext());

            Assert.Equal("ImplicitWidth", generated.Name);
        }

        [Fact]
        public void DetectGroupedProperties_PG08_BorderSubProperties_ReturnsBorderGroup()
        {
            QmlType rectangle = CreateType("QQuickRectangle", "Rectangle");
            ImmutableArray<ResolvedProperty> properties =
            [
                CreateResolvedProperty(CreateProperty("border.width", "double"), rectangle),
                CreateResolvedProperty(CreateProperty("border.color", "color"), rectangle),
                CreateResolvedProperty(CreateProperty("radius", "double"), rectangle),
            ];
            PropGenerator generator = new();

            GroupedPropertyInfo group = Assert.Single(generator.DetectGroupedProperties(CreateResolvedType(rectangle, properties)));

            Assert.Equal("Border", group.GroupName);
            Assert.Equal(["border.color", "border.width"], group.SubProperties.Select(property => property.Property.Name));
        }

        [Fact]
        public void DetectGroupedProperties_PG09_BorderGroup_ReturnsCallbackBuilderSignature()
        {
            QmlType rectangle = CreateType("QQuickRectangle", "Rectangle");
            ImmutableArray<ResolvedProperty> properties =
            [
                CreateResolvedProperty(CreateProperty("border.width", "double"), rectangle),
                CreateResolvedProperty(CreateProperty("border.color", "color"), rectangle),
            ];
            PropGenerator generator = new();

            GroupedPropertyInfo group = Assert.Single(generator.DetectGroupedProperties(CreateResolvedType(rectangle, properties)));

            Assert.Equal("IRectangleBuilder Border(Action<IBorderBuilder> setup)", group.BuilderSignature);
        }

        [Fact]
        public void Generate_PG10_Property_ReturnsXmlDocSummary()
        {
            ResolvedProperty property = CreateResolvedProperty(CreateProperty("width", "double"), CreateType("QQuickRectangle", "Rectangle"));
            PropGenerator generator = new();

            GeneratedProperty generated = Generate(generator, property, CreateContext());

            Assert.Contains("<summary>", generated.XmlDoc, StringComparison.Ordinal);
            Assert.Contains("width", generated.XmlDoc, StringComparison.Ordinal);
        }

        [Fact]
        public void GenerateAll_PG11_ResolvedRectangle_ReturnsAllResolvedProperties()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();
            QmlType rectangle = registry.FindTypeByQualifiedName("QQuickRectangle")!;
            ResolvedType resolved = new InheritanceResolver().Resolve(rectangle, registry);
            PropGenerator generator = new();

            ImmutableArray<GeneratedProperty> generated = generator.GenerateAll(resolved, CreateContext(registry));

            Assert.Equal(resolved.AllProperties.Length, generated.Length);
            Assert.Contains(generated, property => property.Name == "Width");
            Assert.Contains(generated, property => property.Name == "Color");
        }

        [Fact]
        public void Generate_PG12_BindMethodsDisabled_RemovesBindOnly()
        {
            ResolvedProperty property = CreateResolvedProperty(CreateProperty("width", "double"), CreateType("QQuickRectangle", "Rectangle"));
            PropGenerator generator = new();

            GeneratedProperty generated = Generate(generator, property, CreateContext(options: DslTestFixtures.DefaultOptions with
            {
                Properties = DslTestFixtures.DefaultOptions.Properties with { GenerateBindMethods = false },
            }));

            Assert.Equal("IRectangleBuilder Width(double value)", generated.SetterSignature);
            Assert.Null(generated.BindSignature);
        }

        [Fact]
        public void Generate_UnsupportedPropertyType_ThrowsDsl020Diagnostic()
        {
            ResolvedProperty property = CreateResolvedProperty(CreateProperty("broken", "void"), CreateType("QQuickBroken", "Broken"));
            PropGenerator generator = new();

            UnsupportedPropertyTypeException exception = Assert.Throws<UnsupportedPropertyTypeException>(() =>
                Generate(generator, property, CreateContext()));

            Assert.Equal(DslDiagnosticCodes.UnsupportedPropertyType, exception.DiagnosticCode);
            Assert.Equal("broken", exception.PropertyName);
        }

        [Fact]
        public void DetectGroupedProperties_DirectAndGroupedNameConflict_ThrowsDsl021Diagnostic()
        {
            QmlType rectangle = CreateType("QQuickRectangle", "Rectangle");
            ImmutableArray<ResolvedProperty> properties =
            [
                CreateResolvedProperty(CreateProperty("border", "QQuickPen"), rectangle),
                CreateResolvedProperty(CreateProperty("border.width", "double"), rectangle),
            ];
            PropGenerator generator = new();

            GroupedPropertyConflictException exception = Assert.Throws<GroupedPropertyConflictException>(() =>
                generator.DetectGroupedProperties(CreateResolvedType(rectangle, properties)));

            Assert.Equal(DslDiagnosticCodes.GroupedPropertyConflict, exception.DiagnosticCode);
            Assert.Equal("border", exception.GroupName);
        }

        [Fact]
        public void GenerateAll_InheritedProperties_ReturnResolvedOwnerBuilderSignatures()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();
            QmlType rectangle = registry.FindTypeByQualifiedName("QQuickRectangle")!;
            ResolvedType resolved = new InheritanceResolver().Resolve(rectangle, registry);
            PropGenerator generator = new();

            ImmutableArray<GeneratedProperty> generated = generator.GenerateAll(resolved, CreateContext(registry));
            GeneratedProperty inheritedWidth = Assert.Single(generated.Where(property => property.Name == "Width"));

            Assert.Equal("IRectangleBuilder Width(double value)", inheritedWidth.SetterSignature);
            Assert.Equal("QQuickItem", inheritedWidth.DeclaredBy.QualifiedName);
        }

        [Fact]
        public void GenerateAll_OverrideProperty_UsesChildOverrideThroughGeneration()
        {
            QmlType item = CreateType("QQuickItem", "Item", properties: [CreateProperty("width", "double")]);
            QmlType rectangle = CreateType("QQuickRectangle", "Rectangle", prototype: "QQuickItem", properties: [CreateProperty("width", "int")]);
            IRegistryQuery registry = CreateRegistry(item, rectangle);
            ResolvedType resolved = new InheritanceResolver().Resolve(rectangle, registry);
            PropGenerator generator = new();

            GeneratedProperty generated = Assert.Single(generator.GenerateAll(resolved, CreateContext(registry)));

            Assert.Equal("IRectangleBuilder Width(int value)", generated.SetterSignature);
            Assert.Equal("int", generated.CSharpType);
            Assert.Equal("QQuickRectangle", generated.DeclaredBy.QualifiedName);
        }

        [Fact]
        public void GenerateAll_InheritOnlyType_UsesTargetBuilderForInheritedSetter()
        {
            QmlType item = CreateType("QQuickItem", "Item", properties: [CreateProperty("width", "double")]);
            QmlType customItem = CreateType("QQuickCustomItem", "CustomItem", prototype: "QQuickItem");
            IRegistryQuery registry = CreateRegistry(item, customItem);
            ResolvedType resolved = new InheritanceResolver().Resolve(customItem, registry);
            PropGenerator generator = new();

            GeneratedProperty generated = Assert.Single(generator.GenerateAll(resolved, CreateContext(registry)));

            Assert.Equal("ICustomItemBuilder Width(double value)", generated.SetterSignature);
            Assert.Equal("QQuickItem", generated.DeclaredBy.QualifiedName);
        }

        [Fact]
        public void Generate_InheritedProperty_UsesExplicitTargetBuilder()
        {
            QmlType item = CreateType("QQuickItem", "Item");
            QmlType customItem = CreateType("QQuickCustomItem", "CustomItem", prototype: "QQuickItem");
            ResolvedProperty property = CreateResolvedProperty(CreateProperty("width", "double"), item);
            PropGenerator generator = new();

            GeneratedProperty generated = generator.Generate(property, customItem, CreateContext());

            Assert.Equal("ICustomItemBuilder Width(double value)", generated.SetterSignature);
        }

        [Fact]
        public void DetectGroupedProperties_InheritOnlyType_UsesTargetBuilderForGroupSignature()
        {
            QmlType rectangle = CreateType(
                "QQuickRectangle",
                "Rectangle",
                properties:
                [
                    CreateProperty("border.width", "double"),
                    CreateProperty("border.color", "color"),
                ]);
            QmlType customRectangle = CreateType("QQuickCustomRectangle", "CustomRectangle", prototype: "QQuickRectangle");
            IRegistryQuery registry = CreateRegistry(rectangle, customRectangle);
            ResolvedType resolved = new InheritanceResolver().Resolve(customRectangle, registry);
            PropGenerator generator = new();

            GroupedPropertyInfo group = Assert.Single(generator.DetectGroupedProperties(resolved));

            Assert.Equal("ICustomRectangleBuilder Border(Action<IBorderBuilder> setup)", group.BuilderSignature);
        }

        private static GenerationContext CreateContext(
            IRegistryQuery? registry = null,
            GenerationOptions? options = null)
        {
            return new GenerationContext(
                TypeMapper: new QmlSharp.Dsl.Generator.TypeMapper(),
                NameRegistry: new NameRegistry(),
                Registry: registry ?? DslTestFixtures.CreateMinimalFixture(),
                Options: options ?? DslTestFixtures.DefaultOptions,
                CurrentModuleUri: "QtQuick");
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

            return new TestRegistryQuery([module], types, "6.11.0");
        }

        private static ResolvedProperty CreateResolvedProperty(QmlProperty property, QmlType declaredBy, bool isOverridden = false)
        {
            return new ResolvedProperty(property, declaredBy, isOverridden);
        }

        private static GeneratedProperty Generate(
            PropGenerator generator,
            ResolvedProperty property,
            GenerationContext context)
        {
            return generator.Generate(property, property.DeclaredBy, context);
        }

        private static ResolvedType CreateResolvedType(QmlType type, ImmutableArray<ResolvedProperty> properties)
        {
            return new ResolvedType(
                Type: type,
                InheritanceChain: [type],
                AllProperties: properties,
                AllSignals: ImmutableArray<ResolvedSignal>.Empty,
                AllMethods: ImmutableArray<ResolvedMethod>.Empty,
                AllEnums: ImmutableArray<QmlEnum>.Empty,
                AttachedType: null,
                ExtensionType: null);
        }

        private static QmlProperty CreateProperty(
            string name,
            string typeName,
            bool isReadonly = false,
            bool isRequired = false,
            bool isList = false)
        {
            return new QmlProperty(
                Name: name,
                TypeName: typeName,
                IsReadonly: isReadonly,
                IsList: isList,
                IsRequired: isRequired,
                DefaultValue: null,
                NotifySignal: null);
        }

        private static QmlType CreateType(
            string qualifiedName,
            string? qmlName,
            string? prototype = null,
            ImmutableArray<QmlProperty> properties = default,
            string? defaultProperty = null)
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
