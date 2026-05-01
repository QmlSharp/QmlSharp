using QmlSharp.Dsl.Generator.Tests.Fixtures;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Inheritance
{
    public sealed class InheritanceResolverTests
    {
        [Fact]
        public void Resolve_IR01_TypeWithNoParent_ReturnsSelfOnlyChain()
        {
            IRegistryQuery registry = CreateQuery(CreateType("Solo", "Solo", "QtQuick.Test"));
            QmlType solo = registry.FindTypeByQualifiedName("Solo")!;
            InheritanceResolver resolver = new();

            ResolvedType resolved = resolver.Resolve(solo, registry);

            Assert.Equal(["Solo"], resolved.InheritanceChain.Select(type => type.QualifiedName));
            Assert.Empty(resolved.AllProperties);
            Assert.Empty(resolved.AllSignals);
            Assert.Empty(resolved.AllMethods);
            Assert.Empty(resolved.AllEnums);
        }

        [Fact]
        public void Resolve_IR02_SimpleChain_ReturnsTypeToRootOrder()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();
            QmlType rectangle = registry.FindTypeByQualifiedName("QQuickRectangle")!;
            InheritanceResolver resolver = new();

            IReadOnlyList<QmlType> chain = resolver.GetInheritanceChain(rectangle, registry);

            Assert.Equal(["QQuickRectangle", "QQuickItem", "QObject"], chain.Select(type => type.QualifiedName));
        }

        [Fact]
        public void Resolve_IR03_DeepChain_ReturnsAllAncestors()
        {
            IRegistryQuery registry = CreateDeepInheritanceFixture();
            QmlType leaf = registry.FindTypeByQualifiedName("Level5")!;
            InheritanceResolver resolver = new();

            ResolvedType resolved = resolver.Resolve(leaf, registry);

            Assert.Equal(["Level5", "Level4", "Level3", "Level2", "Level1"], resolved.InheritanceChain.Select(type => type.QualifiedName));
        }

        [Fact]
        public void Resolve_IR04_AllProperties_IncludesInheritedProperties()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();
            QmlType rectangle = registry.FindTypeByQualifiedName("QQuickRectangle")!;
            InheritanceResolver resolver = new();

            ResolvedType resolved = resolver.Resolve(rectangle, registry);

            Assert.Contains(resolved.AllProperties, property => property.Property.Name == "color" && property.DeclaredBy.QualifiedName == "QQuickRectangle");
            Assert.Contains(resolved.AllProperties, property => property.Property.Name == "width" && property.DeclaredBy.QualifiedName == "QQuickItem");
            Assert.Contains(resolved.AllProperties, property => property.Property.Name == "height" && property.DeclaredBy.QualifiedName == "QQuickItem");
            Assert.Contains(resolved.AllProperties, property => property.Property.Name == "visible" && property.DeclaredBy.QualifiedName == "QQuickItem");
        }

        [Fact]
        public void Resolve_IR05_PropertyOverride_UsesChildPropertyAndMarksOverride()
        {
            IRegistryQuery registry = CreateOverrideFixture();
            QmlType rectangle = registry.FindTypeByQualifiedName("Rectangle")!;
            InheritanceResolver resolver = new();

            ResolvedType resolved = resolver.Resolve(rectangle, registry);
            ResolvedProperty width = Assert.Single(resolved.AllProperties.Where(property => property.Property.Name == "width"));

            Assert.Equal("Rectangle", width.DeclaredBy.QualifiedName);
            Assert.Equal("int", width.Property.TypeName);
            Assert.True(width.IsOverridden);
        }

        [Fact]
        public void Resolve_IR06_AllSignals_IncludesInheritedSignals()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();
            QmlType rectangle = registry.FindTypeByQualifiedName("QQuickRectangle")!;
            InheritanceResolver resolver = new();

            ResolvedType resolved = resolver.Resolve(rectangle, registry);

            Assert.Contains(resolved.AllSignals, signal => signal.Signal.Name == "colorChanged" && signal.DeclaredBy.QualifiedName == "QQuickRectangle");
            Assert.Contains(resolved.AllSignals, signal => signal.Signal.Name == "widthChanged" && signal.DeclaredBy.QualifiedName == "QQuickItem");
            Assert.Contains(resolved.AllSignals, signal => signal.Signal.Name == "visibleChanged" && signal.DeclaredBy.QualifiedName == "QQuickItem");
        }

        [Fact]
        public void Resolve_IR07_AllMethods_IncludesInheritedMethods()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();
            QmlType rectangle = registry.FindTypeByQualifiedName("QQuickRectangle")!;
            InheritanceResolver resolver = new();

            ResolvedType resolved = resolver.Resolve(rectangle, registry);

            Assert.Contains(resolved.AllMethods, method => method.Method.Name == "forceActiveFocus" && method.DeclaredBy.QualifiedName == "QQuickItem");
        }

        [Fact]
        public void Resolve_IR08_AllEnums_IncludesInheritedEnums()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();
            QmlType rectangle = registry.FindTypeByQualifiedName("QQuickRectangle")!;
            InheritanceResolver resolver = new();

            ResolvedType resolved = resolver.Resolve(rectangle, registry);

            QmlEnum enumDefinition = Assert.Single(resolved.AllEnums.Where(enumDefinition => enumDefinition.Name == "TransformOrigin"));
            Assert.Equal(["TopLeft", "Center"], enumDefinition.Values.Select(value => value.Name));
        }

        [Fact]
        public void Resolve_IR09_CircularInheritance_ThrowsCircularInheritanceException()
        {
            IRegistryQuery registry = DslTestFixtures.CreateCircularInheritanceFixture();
            QmlType type = registry.FindTypeByQualifiedName("A")!;
            InheritanceResolver resolver = new();

            CircularInheritanceException exception = Assert.Throws<CircularInheritanceException>(() => resolver.Resolve(type, registry));

            Assert.Equal(DslDiagnosticCodes.CircularInheritance, exception.DiagnosticCode);
            Assert.Equal(["A", "C", "B", "A"], exception.Chain.ToArray());
        }

        [Fact]
        public void Resolve_IR10_MaxDepthExceeded_ThrowsDsl003Diagnostic()
        {
            IRegistryQuery registry = CreateDeepInheritanceFixture();
            QmlType leaf = registry.FindTypeByQualifiedName("Level5")!;
            InheritanceResolver resolver = new(new InheritanceOptions(MaxDepth: 3, IncludeQtObjectProperties: true));

            MaxDepthExceededException exception = Assert.Throws<MaxDepthExceededException>(() => resolver.Resolve(leaf, registry));

            Assert.Equal(DslDiagnosticCodes.MaxDepthExceeded, exception.DiagnosticCode);
            Assert.Equal("Level5", exception.TypeName);
            Assert.Equal(3, exception.MaxDepth);
        }

        [Fact]
        public void Resolve_IR11_AttachedType_PopulatesAttachedType()
        {
            IRegistryQuery registry = DslTestFixtures.CreateAttachedTypesFixture();
            QmlType item = registry.FindTypeByQualifiedName("QQuickItem")!;
            InheritanceResolver resolver = new();

            ResolvedType resolved = resolver.Resolve(item, registry);

            Assert.NotNull(resolved.AttachedType);
            Assert.Equal("QQuickKeysAttached", resolved.AttachedType.QualifiedName);
        }

        [Fact]
        public void Resolve_ExtensionTypeMetadata_PopulatesExtensionType()
        {
            QmlType baseType = CreateType("Base", "Base", "QtQuick.Test");
            QmlType extensionType = CreateType("BaseExtension", null, null);
            QmlType owner = CreateType("Owner", "Owner", "QtQuick.Test", prototype: "Base", extension: "BaseExtension");
            IRegistryQuery registry = CreateQuery(baseType, extensionType, owner);
            InheritanceResolver resolver = new();

            ResolvedType resolved = resolver.Resolve(owner, registry);

            Assert.NotNull(resolved.ExtensionType);
            Assert.Equal("BaseExtension", resolved.ExtensionType.QualifiedName);
        }

        [Fact]
        public void ResolveModule_IR12_ReturnsAllResolvableModuleTypesKeyedByQmlName()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();
            QmlModule module = registry.FindModule("QtQuick")!;
            InheritanceResolver resolver = new();

            IReadOnlyDictionary<string, ResolvedType> resolvedTypes = resolver.ResolveModule(module, registry);

            Assert.Equal(["Item", "Rectangle", "Text"], resolvedTypes.Keys.Order(StringComparer.Ordinal));
            Assert.Equal("QQuickRectangle", resolvedTypes["Rectangle"].Type.QualifiedName);
        }

        [Fact]
        public void IsSubtypeOf_IR13_TransitiveBase_ReturnsTrue()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();
            QmlType rectangle = registry.FindTypeByQualifiedName("QQuickRectangle")!;
            InheritanceResolver resolver = new();

            bool isSubtype = resolver.IsSubtypeOf(rectangle, "QObject", registry);

            Assert.True(isSubtype);
        }

        [Fact]
        public void GetDirectSubtypes_DirectChildren_ReturnsOnlyImmediateSubtypes()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();
            InheritanceResolver resolver = new();

            IReadOnlyList<QmlType> directSubtypes = resolver.GetDirectSubtypes("QQuickItem", registry);

            Assert.Equal(["Rectangle", "Text"], directSubtypes.Select(type => type.QmlName));
        }

        [Fact]
        public void Resolve_UnresolvedBase_ThrowsDsl001Diagnostic()
        {
            IRegistryQuery registry = CreateQuery(CreateType("Broken", "Broken", "QtQuick.Test", prototype: "MissingBase"));
            QmlType broken = registry.FindTypeByQualifiedName("Broken")!;
            InheritanceResolver resolver = new();

            TypeResolutionException exception = Assert.Throws<TypeResolutionException>(() => resolver.Resolve(broken, registry));

            Assert.Equal(DslDiagnosticCodes.UnresolvedBaseType, exception.DiagnosticCode);
            Assert.Equal("Broken", exception.TypeName);
            Assert.Equal("MissingBase", exception.UnresolvedTypeName);
        }

        [Fact]
        public void ResolveModule_UnresolvedBase_SkipsBrokenTypeAndKeepsValidTypes()
        {
            QmlType root = CreateType("Root", "Root", "QtQuick.Test");
            QmlType child = CreateType("Child", "Child", "QtQuick.Test", prototype: "Root");
            QmlType broken = CreateType("Broken", "Broken", "QtQuick.Test", prototype: "MissingBase");
            IRegistryQuery registry = CreateQuery(root, child, broken);
            QmlModule module = registry.FindModule("QtQuick.Test")!;
            InheritanceResolver resolver = new();

            IReadOnlyDictionary<string, ResolvedType> resolvedTypes = resolver.ResolveModule(module, registry);

            Assert.Equal(["Child", "Root"], resolvedTypes.Keys.Order(StringComparer.Ordinal));
            Assert.DoesNotContain("Broken", resolvedTypes.Keys);
        }

        [Fact]
        public void ResolveModule_CircularInheritance_SkipsBrokenTypeAndKeepsValidTypes()
        {
            QmlType root = CreateType("Root", "Root", "QtQuick.Test");
            QmlType child = CreateType("Child", "Child", "QtQuick.Test", prototype: "Root");
            QmlType cycleA = CreateType("CycleA", "CycleA", "QtQuick.Test", prototype: "CycleB");
            QmlType cycleB = CreateType("CycleB", "CycleB", "QtQuick.Test", prototype: "CycleA");
            IRegistryQuery registry = CreateQuery(root, child, cycleA, cycleB);
            QmlModule module = registry.FindModule("QtQuick.Test")!;
            InheritanceResolver resolver = new();

            IReadOnlyDictionary<string, ResolvedType> resolvedTypes = resolver.ResolveModule(module, registry);

            Assert.Equal(["Child", "Root"], resolvedTypes.Keys.Order(StringComparer.Ordinal));
            Assert.DoesNotContain("CycleA", resolvedTypes.Keys);
            Assert.DoesNotContain("CycleB", resolvedTypes.Keys);
        }

        [Fact]
        public void ResolveModule_MaxDepthExceeded_SkipsBrokenTypeAndKeepsValidTypes()
        {
            QmlType root = CreateType("Root", "Root", "QtQuick.Test");
            QmlType child = CreateType("Child", "Child", "QtQuick.Test", prototype: "Root");
            QmlType grandchild = CreateType("Grandchild", "Grandchild", "QtQuick.Test", prototype: "Child");
            IRegistryQuery registry = CreateQuery(root, child, grandchild);
            QmlModule module = registry.FindModule("QtQuick.Test")!;
            InheritanceResolver resolver = new(new InheritanceOptions(MaxDepth: 2, IncludeQtObjectProperties: true));

            IReadOnlyDictionary<string, ResolvedType> resolvedTypes = resolver.ResolveModule(module, registry);

            Assert.Equal(["Child", "Root"], resolvedTypes.Keys.Order(StringComparer.Ordinal));
            Assert.DoesNotContain("Grandchild", resolvedTypes.Keys);
        }

        [Fact]
        public void GetDirectSubtypes_UnresolvedPrototype_DoesNotReturnRawStringMatch()
        {
            QmlType broken = CreateType("Broken", "Broken", "QtQuick.Test", prototype: "MissingBase");
            IRegistryQuery registry = CreateQuery(broken);
            InheritanceResolver resolver = new();

            IReadOnlyList<QmlType> directSubtypes = resolver.GetDirectSubtypes("MissingBase", registry);

            Assert.Empty(directSubtypes);
        }

        private static IRegistryQuery CreateDeepInheritanceFixture()
        {
            return CreateQuery(
                CreateType("Level1", "Level1", "QtQuick.Deep", properties: [CreateProperty("level1", "int")]),
                CreateType("Level2", "Level2", "QtQuick.Deep", prototype: "Level1", properties: [CreateProperty("level2", "int")]),
                CreateType("Level3", "Level3", "QtQuick.Deep", prototype: "Level2", properties: [CreateProperty("level3", "int")]),
                CreateType("Level4", "Level4", "QtQuick.Deep", prototype: "Level3", properties: [CreateProperty("level4", "int")]),
                CreateType("Level5", "Level5", "QtQuick.Deep", prototype: "Level4", properties: [CreateProperty("level5", "int")]));
        }

        private static IRegistryQuery CreateOverrideFixture()
        {
            return CreateQuery(
                CreateType("Item", "Item", "QtQuick.Override", properties: [CreateProperty("width", "double"), CreateProperty("height", "double")]),
                CreateType("Rectangle", "Rectangle", "QtQuick.Override", prototype: "Item", properties: [CreateProperty("width", "int"), CreateProperty("color", "color")]));
        }

        private static IRegistryQuery CreateQuery(params QmlType[] types)
        {
            string moduleUri = types.First(type => type.ModuleUri is not null).ModuleUri!;
            ImmutableArray<QmlModuleType> moduleTypes = types
                .Where(type => string.Equals(type.ModuleUri, moduleUri, StringComparison.Ordinal) && type.QmlName is not null)
                .OrderBy(type => type.QmlName, StringComparer.Ordinal)
                .Select(type => new QmlModuleType(type.QualifiedName, type.QmlName!, new QmlVersion(2, 15)))
                .ToImmutableArray();
            QmlModule module = new(
                Uri: moduleUri,
                Version: new QmlVersion(2, 15),
                Dependencies: ImmutableArray<string>.Empty,
                Imports: ImmutableArray<string>.Empty,
                Types: moduleTypes);

            return new TestRegistryQuery([module], types.OrderBy(type => type.QualifiedName, StringComparer.Ordinal).ToArray(), "6.11.0");
        }

        private static QmlType CreateType(
            string qualifiedName,
            string? qmlName,
            string? moduleUri,
            string? prototype = null,
            string? attachedType = null,
            string? extension = null,
            ImmutableArray<QmlProperty> properties = default,
            ImmutableArray<QmlSignal> signals = default,
            ImmutableArray<QmlMethod> methods = default,
            ImmutableArray<QmlEnum> enums = default)
        {
            return new QmlType(
                QualifiedName: qualifiedName,
                QmlName: qmlName,
                ModuleUri: moduleUri,
                AccessSemantics: AccessSemantics.Reference,
                Prototype: prototype,
                DefaultProperty: null,
                AttachedType: attachedType,
                Extension: extension,
                IsSingleton: false,
                IsCreatable: qmlName is not null,
                Exports: moduleUri is null || qmlName is null
                    ? ImmutableArray<QmlTypeExport>.Empty
                    : [new QmlTypeExport(moduleUri, qmlName, new QmlVersion(2, 15))],
                Properties: properties.IsDefault ? ImmutableArray<QmlProperty>.Empty : properties,
                Signals: signals.IsDefault ? ImmutableArray<QmlSignal>.Empty : signals,
                Methods: methods.IsDefault ? ImmutableArray<QmlMethod>.Empty : methods,
                Enums: enums.IsDefault ? ImmutableArray<QmlEnum>.Empty : enums,
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
