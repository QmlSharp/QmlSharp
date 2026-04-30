using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Fixtures
{
    internal static class DslTestFixtures
    {
        public static IRegistryQuery CreateMinimalFixture()
        {
            return CreateQuery(
                modules:
                [
                    CreateModule(
                        "QtQuick",
                        "QQuickItem",
                        "Item",
                        "QQuickRectangle",
                        "Rectangle",
                        "QQuickText",
                        "Text"),
                ],
                types:
                [
                    CreateQObjectType(),
                    CreateItemType(),
                    CreateRectangleType(),
                    CreateTextType(),
                ]);
        }

        public static IRegistryQuery CreateQtQuickFixture()
        {
            return CreateMinimalFixture();
        }

        public static IRegistryQuery CreateQtQuickControlsFixture()
        {
            return CreateQuery(
                modules:
                [
                    CreateModule(
                        "QtQuick",
                        "QQuickItem",
                        "Item",
                        "QQuickRectangle",
                        "Rectangle",
                        "QQuickText",
                        "Text"),
                    CreateModule("QtQuick.Controls", "QQuickButton", "Button"),
                ],
                types:
                [
                    CreateQObjectType(),
                    CreateItemType(),
                    CreateRectangleType(),
                    CreateTextType(),
                    CreateButtonType(),
                ]);
        }

        public static IRegistryQuery CreateP0Fixture()
        {
            return CreateQuery(
                modules:
                [
                    CreateModule("QtQml", "QtObject", "QtObject"),
                    CreateModule(
                        "QtQuick",
                        "QQuickItem",
                        "Item",
                        "QQuickRectangle",
                        "Rectangle",
                        "QQuickText",
                        "Text"),
                    CreateModule("QtQuick.Controls", "QQuickButton", "Button"),
                    CreateModule("QtQuick.Layouts", "QQuickLayout", "Layout"),
                ],
                types:
                [
                    CreateQObjectType("QtObject", "QtObject", "QtQml", null, isCreatable: true),
                    CreateItemType(),
                    CreateRectangleType(),
                    CreateTextType(),
                    CreateButtonType(),
                    CreateLayoutType(),
                ]);
        }

        public static IRegistryQuery CreateCircularInheritanceFixture()
        {
            return CreateQuery(
                modules:
                [
                    CreateModule("QtQuick.Circular", "A", "A", "B", "B", "C", "C"),
                ],
                types:
                [
                    CreateQObjectType("A", "A", "QtQuick.Circular", "C", isCreatable: true),
                    CreateQObjectType("B", "B", "QtQuick.Circular", "A", isCreatable: true),
                    CreateQObjectType("C", "C", "QtQuick.Circular", "B", isCreatable: true),
                ]);
        }

        public static IRegistryQuery CreateAttachedTypesFixture()
        {
            return CreateQuery(
                modules:
                [
                    CreateModule("QtQuick", "QQuickItem", "Item"),
                    CreateModule("QtQuick.Layouts", "QQuickLayout", "Layout"),
                ],
                types:
                [
                    CreateQObjectType(),
                    CreateItemType(attachedType: "QQuickKeysAttached"),
                    CreateKeysAttachedType(),
                    CreateLayoutType(attachedType: "QQuickLayoutAttached"),
                    CreateLayoutAttachedType(),
                ]);
        }

        public static string CreateCounterViewModelSchema()
        {
            return """
                {
                  "schemaVersion": "1.0",
                  "className": "CounterViewModel",
                  "moduleUri": "QmlSharp.TestApp",
                  "compilerSlotKey": "CounterView::__qmlsharp_vm0",
                  "properties": [
                    { "name": "count", "type": "int", "readOnly": false }
                  ],
                  "commands": [
                    { "name": "increment", "parameters": [] }
                  ],
                  "effects": [
                    { "name": "showToast", "payloadType": "string" }
                  ]
                }
                """;
        }

        public static CodeEmitOptions DefaultEmitOptions { get; } = new(
            GenerateXmlDoc: true,
            MarkDeprecated: true,
            HeaderComment: "// <auto-generated />");

        public static GenerationOptions DefaultOptions { get; } = new(
            TypeMapper: null,
            Inheritance: new InheritanceOptions(MaxDepth: 32, IncludeQtObjectProperties: true),
            Properties: new PropertyOptions(
                GenerateBindMethods: true,
                GenerateReadonlyGetters: true,
                GenerateGroupedBuilders: true),
            Signals: new SignalOptions(HandlerPrefix: "On", SimplifyNoArgHandlers: true),
            Enums: new EnumOptions(GenerateFlagHelpers: true),
            Filter: new FilterOptions(
                CreatableOnly: false,
                ExcludeTypes: ImmutableArray<string>.Empty,
                ExcludeInternal: true,
                ExcludeDeprecated: true,
                VersionRange: null),
            ViewModel: new ViewModelOptions(Enabled: true, ProxyPrefix: "Qml"),
            Emit: DefaultEmitOptions,
            Packager: new PackagerOptions(
                OutputDir: "generated",
                PackageVersion: "0.1.0",
                PackagePrefix: "QmlSharp",
                GenerateReadme: true,
                GenerateProjectFile: true));

        public static GeneratedTypeCode CreateGeneratedRectangleMetadata()
        {
            QmlType rectangle = CreateRectangleType();
            QmlType item = CreateItemType();

            return new GeneratedTypeCode(
                QmlName: "Rectangle",
                ModuleUri: "QtQuick",
                FactoryName: "Rectangle",
                PropsInterfaceName: "IRectangleProps",
                BuilderInterfaceName: "IRectangleBuilder",
                FactoryMethodCode: "public static IRectangleBuilder Rectangle() => ObjectFactory.Create<IRectangleBuilder>(\"Rectangle\");",
                Properties:
                [
                    new GeneratedProperty(
                        Name: "Width",
                        SetterSignature: "IRectangleBuilder Width(double value)",
                        BindSignature: "IRectangleBuilder WidthBind(string expr)",
                        XmlDoc: "<summary>Sets width.</summary>",
                        DeclaredBy: item,
                        IsReadOnly: false,
                        IsRequired: false,
                        CSharpType: "double"),
                    new GeneratedProperty(
                        Name: "Color",
                        SetterSignature: "IRectangleBuilder Color(QmlColor value)",
                        BindSignature: "IRectangleBuilder ColorBind(string expr)",
                        XmlDoc: "<summary>Sets color.</summary>",
                        DeclaredBy: rectangle,
                        IsReadOnly: false,
                        IsRequired: false,
                        CSharpType: "QmlColor"),
                ],
                Signals:
                [
                    new GeneratedSignal(
                        SignalName: "colorChanged",
                        HandlerName: "OnColorChanged",
                        HandlerSignature: "IRectangleBuilder OnColorChanged(Action handler)",
                        XmlDoc: "<summary>Handles colorChanged.</summary>",
                        DeclaredBy: rectangle,
                        Parameters: ImmutableArray<GeneratedParameter>.Empty),
                ],
                Methods: ImmutableArray<GeneratedMethod>.Empty,
                Enums: ImmutableArray<GeneratedEnum>.Empty,
                AttachedTypes: ImmutableArray<GeneratedAttachedType>.Empty,
                DefaultProperty: new DefaultPropertyInfo("data", "Item", IsList: true, GenerateChildMethod: true, GenerateChildrenMethod: true),
                IsCreatable: true,
                IsDeprecated: false);
        }

        public static GeneratedOutputTempDirectory CreateGeneratedOutputTempDirectory()
        {
            return GeneratedOutputTempDirectory.Create();
        }

        private static TestRegistryQuery CreateQuery(IReadOnlyList<QmlModule> modules, IReadOnlyList<QmlType> types)
        {
            return new TestRegistryQuery(modules, types.OrderBy(type => type.QualifiedName, StringComparer.Ordinal).ToArray(), "6.11.0");
        }

        private static QmlModule CreateModule(string uri, params string[] qualifiedNameAndQmlNamePairs)
        {
            if (qualifiedNameAndQmlNamePairs.Length % 2 != 0)
            {
                throw new ArgumentException("Module type pairs must contain qualified-name and QML-name entries.", nameof(qualifiedNameAndQmlNamePairs));
            }

            ImmutableArray<QmlModuleType>.Builder types = ImmutableArray.CreateBuilder<QmlModuleType>();
            for (int index = 0; index < qualifiedNameAndQmlNamePairs.Length; index += 2)
            {
                types.Add(new QmlModuleType(
                    QualifiedName: qualifiedNameAndQmlNamePairs[index],
                    QmlName: qualifiedNameAndQmlNamePairs[index + 1],
                    ExportVersion: new QmlVersion(2, 15)));
            }

            return new QmlModule(
                Uri: uri,
                Version: new QmlVersion(2, 15),
                Dependencies: ImmutableArray<string>.Empty,
                Imports: ImmutableArray<string>.Empty,
                Types: types.ToImmutable());
        }

        private static QmlType CreateQObjectType(
            string qualifiedName = "QObject",
            string? qmlName = null,
            string? moduleUri = null,
            string? prototype = null,
            bool isCreatable = false)
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
                IsCreatable: isCreatable,
                Exports: CreateExports(moduleUri, qmlName),
                Properties: ImmutableArray<QmlProperty>.Empty,
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlType CreateItemType(string? attachedType = null)
        {
            return new QmlType(
                QualifiedName: "QQuickItem",
                QmlName: "Item",
                ModuleUri: "QtQuick",
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QObject",
                DefaultProperty: "data",
                AttachedType: attachedType,
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: CreateExports("QtQuick", "Item"),
                Properties:
                [
                    CreateProperty("width", "double"),
                    CreateProperty("height", "double"),
                    CreateProperty("visible", "bool"),
                ],
                Signals:
                [
                    CreateSignal("widthChanged"),
                    CreateSignal("visibleChanged"),
                ],
                Methods:
                [
                    CreateMethod("forceActiveFocus", "void"),
                ],
                Enums:
                [
                    new QmlEnum("TransformOrigin", false, [new QmlEnumValue("TopLeft", 0), new QmlEnumValue("Center", 1)]),
                ],
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlType CreateRectangleType()
        {
            return new QmlType(
                QualifiedName: "QQuickRectangle",
                QmlName: "Rectangle",
                ModuleUri: "QtQuick",
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QQuickItem",
                DefaultProperty: "data",
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: CreateExports("QtQuick", "Rectangle"),
                Properties:
                [
                    CreateProperty("color", "color"),
                    CreateProperty("radius", "double"),
                    CreateProperty("border.width", "double"),
                    CreateProperty("border.color", "color"),
                ],
                Signals:
                [
                    CreateSignal("colorChanged"),
                ],
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlType CreateTextType()
        {
            return new QmlType(
                QualifiedName: "QQuickText",
                QmlName: "Text",
                ModuleUri: "QtQuick",
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QQuickItem",
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: CreateExports("QtQuick", "Text"),
                Properties:
                [
                    CreateProperty("text", "string"),
                    CreateProperty("wrapMode", "int"),
                ],
                Signals: [CreateSignal("textChanged")],
                Methods: [CreateMethod("append", "void", new QmlParameter("value", "string"))],
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlType CreateButtonType()
        {
            return new QmlType(
                QualifiedName: "QQuickButton",
                QmlName: "Button",
                ModuleUri: "QtQuick.Controls",
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QQuickItem",
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: CreateExports("QtQuick.Controls", "Button"),
                Properties: [CreateProperty("text", "string"), CreateProperty("checked", "bool")],
                Signals: [CreateSignal("clicked")],
                Methods: [CreateMethod("click", "void")],
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlType CreateLayoutType(string? attachedType = null)
        {
            return new QmlType(
                QualifiedName: "QQuickLayout",
                QmlName: "Layout",
                ModuleUri: "QtQuick.Layouts",
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QQuickItem",
                DefaultProperty: "data",
                AttachedType: attachedType,
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: CreateExports("QtQuick.Layouts", "Layout"),
                Properties: [CreateProperty("spacing", "double")],
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlType CreateKeysAttachedType()
        {
            return new QmlType(
                QualifiedName: "QQuickKeysAttached",
                QmlName: null,
                ModuleUri: null,
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QObject",
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: false,
                Exports: ImmutableArray<QmlTypeExport>.Empty,
                Properties: [CreateProperty("enabled", "bool")],
                Signals: [CreateSignal("pressed", new QmlParameter("event", "var"))],
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlType CreateLayoutAttachedType()
        {
            return new QmlType(
                QualifiedName: "QQuickLayoutAttached",
                QmlName: null,
                ModuleUri: null,
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QObject",
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: false,
                Exports: ImmutableArray<QmlTypeExport>.Empty,
                Properties: [CreateProperty("fillWidth", "bool"), CreateProperty("fillHeight", "bool")],
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
                NotifySignal: $"{name.Replace(".", string.Empty, StringComparison.Ordinal)}Changed");
        }

        private static QmlSignal CreateSignal(string name, params QmlParameter[] parameters)
        {
            return new QmlSignal(name, parameters.ToImmutableArray());
        }

        private static QmlMethod CreateMethod(string name, string? returnType, params QmlParameter[] parameters)
        {
            return new QmlMethod(name, returnType, parameters.ToImmutableArray());
        }

        private static ImmutableArray<QmlTypeExport> CreateExports(string? moduleUri, string? qmlName)
        {
            if (moduleUri is null || qmlName is null)
            {
                return ImmutableArray<QmlTypeExport>.Empty;
            }

            return [new QmlTypeExport(moduleUri, qmlName, new QmlVersion(2, 15))];
        }
    }
}
