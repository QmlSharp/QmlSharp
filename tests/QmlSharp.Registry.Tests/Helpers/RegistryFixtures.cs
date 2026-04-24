namespace QmlSharp.Registry.Tests.Helpers
{
    internal static class RegistryFixtures
    {
        public static QmlRegistry CreateMinimalInheritanceFixture()
        {
            return CreateRegistry(
                modules:
                [
                    CreateQtQuickModule(),
                ],
                types:
                [
                    CreateQObjectType(),
                    CreateItemType(),
                    CreateRectangleType(),
                ],
                builtins:
                [
                    CreateBuiltinType("double"),
                ]);
        }

        public static QmlRegistry CreateModuleQueryFixture()
        {
            QmlRegistry baseFixture = CreateMinimalInheritanceFixture();

            return CreateRegistry(
                modules:
                [
                    CreateQtQuickModule(),
                    new QmlModule(
                        Uri: "QtQuick.Controls",
                        Version: new QmlVersion(2, 15),
                        Dependencies: ["QtQuick"],
                        Imports: ["QtQuick"],
                        Types:
                        [
                            new QmlModuleType("QQuickButton", "Button", new QmlVersion(2, 15)),
                        ]),
                ],
                types: baseFixture.TypesByQualifiedName.Values.Append(CreateButtonType()).ToImmutableArray(),
                builtins: baseFixture.Builtins);
        }

        public static QmlRegistry CreateCategoryFixture()
        {
            QmlRegistry baseFixture = CreateMinimalInheritanceFixture();

            return CreateRegistry(
                modules: baseFixture.Modules,
                types: baseFixture.TypesByQualifiedName.Values
                    .Append(CreateColorValueType())
                    .Append(CreatePaletteSingletonType())
                    .Append(CreateSequenceType())
                    .ToImmutableArray(),
                builtins: baseFixture.Builtins);
        }

        public static QmlRegistry CreateQueryFixture()
        {
            return CreateRegistry(
                modules:
                [
                    new QmlModule(
                        Uri: "QtQuick",
                        Version: new QmlVersion(2, 15),
                        Dependencies: ["QtQml"],
                        Imports: ["QtQml"],
                        Types:
                        [
                            new QmlModuleType("QQuickItem", "Item", new QmlVersion(2, 15)),
                            new QmlModuleType("QQuickRectangle", "Rectangle", new QmlVersion(2, 15)),
                            new QmlModuleType("QQuickText", "Text", new QmlVersion(2, 15)),
                        ]),
                    new QmlModule(
                        Uri: "QtQuick.Controls",
                        Version: new QmlVersion(2, 15),
                        Dependencies: ["QtQuick"],
                        Imports: ["QtQuick"],
                        Types:
                        [
                            new QmlModuleType("QQuickButton", "Button", new QmlVersion(2, 15)),
                        ]),
                ],
                types:
                [
                    CreateQObjectType(),
                    CreateKeysAttachedType(),
                    CreateItemType(),
                    CreateRectangleType(),
                    CreateTextType(),
                    CreateButtonType(),
                    CreateColorValueType(),
                    CreatePaletteSingletonType(),
                    CreateSequenceType(),
                ],
                builtins:
                [
                    CreateBuiltinType("bool"),
                    CreateBuiltinType("color"),
                    CreateBuiltinType("double"),
                    CreateBuiltinType("point"),
                    CreateBuiltinType("string"),
                    CreateBuiltinType("void", AccessSemantics.None),
                ]);
        }

        public static QmlRegistry CreatePerformanceFixture(int typeCount = 128, int propertiesPerType = 8)
        {
            const string moduleUri = "QtQuick.Performance";
            List<QmlType> types = [CreateQObjectType()];
            ImmutableArray<QmlModuleType>.Builder moduleTypes = ImmutableArray.CreateBuilder<QmlModuleType>();
            string prototype = "QObject";

            for (int index = 0; index < typeCount; index++)
            {
                string qualifiedName = GetPerformanceQualifiedName(index);
                string qmlName = $"PerfType{index:D3}";

                ImmutableArray<QmlProperty> properties = Enumerable.Range(0, propertiesPerType)
                    .Select(propertyIndex => new QmlProperty(
                        Name: $"prop{index:D3}_{propertyIndex:D2}",
                        TypeName: propertyIndex % 2 == 0 ? "double" : "string",
                        IsReadonly: false,
                        IsList: false,
                        IsRequired: false,
                        DefaultValue: null,
                        NotifySignal: $"prop{index:D3}_{propertyIndex:D2}Changed"))
                    .ToImmutableArray();

                ImmutableArray<QmlSignal> signals = Enumerable.Range(0, propertiesPerType)
                    .Select(propertyIndex => new QmlSignal(
                        Name: $"prop{index:D3}_{propertyIndex:D2}Changed",
                        Parameters: ImmutableArray<QmlParameter>.Empty))
                    .ToImmutableArray();

                ImmutableArray<QmlMethod> methods =
                [
                    new QmlMethod($"perform{index:D3}", "void", ImmutableArray<QmlParameter>.Empty),
                    new QmlMethod("measure", "bool", [new QmlParameter("sample", "double")]),
                ];

                types.Add(new QmlType(
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
                    Exports: [new QmlTypeExport(moduleUri, qmlName, new QmlVersion(1, 0))],
                    Properties: properties,
                    Signals: signals,
                    Methods: methods,
                    Enums: ImmutableArray<QmlEnum>.Empty,
                    Interfaces: ImmutableArray<string>.Empty));
                moduleTypes.Add(new QmlModuleType(qualifiedName, qmlName, new QmlVersion(1, 0)));
                prototype = qualifiedName;
            }

            return CreateRegistry(
                modules:
                [
                    new QmlModule(
                        Uri: moduleUri,
                        Version: new QmlVersion(1, 0),
                        Dependencies: ImmutableArray<string>.Empty,
                        Imports: ImmutableArray<string>.Empty,
                        Types: moduleTypes.ToImmutable()),
                ],
                types: types.ToImmutableArray(),
                builtins:
                [
                    CreateBuiltinType("bool"),
                    CreateBuiltinType("double"),
                    CreateBuiltinType("string"),
                    CreateBuiltinType("void", AccessSemantics.None),
                ]);
        }

        public static QmlModule CreateQtQuickModule()
        {
            return new QmlModule(
                Uri: "QtQuick",
                Version: new QmlVersion(2, 15),
                Dependencies: ["QtQml"],
                Imports: ["QtQml"],
                Types:
                [
                    CreateQtQuickModuleType(),
                    new QmlModuleType("QQuickRectangle", "Rectangle", new QmlVersion(2, 15)),
                ]);
        }

        public static QmlModuleType CreateQtQuickModuleType()
        {
            return new QmlModuleType("QQuickItem", "Item", new QmlVersion(2, 15));
        }

        public static QmlType CreateQObjectType()
        {
            return new QmlType(
                QualifiedName: "QObject",
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
                Properties: ImmutableArray<QmlProperty>.Empty,
                Signals: [new QmlSignal("destroyed", [CreateParameter()])],
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        public static QmlType CreateItemType()
        {
            return new QmlType(
                QualifiedName: "QQuickItem",
                QmlName: "Item",
                ModuleUri: "QtQuick",
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QObject",
                DefaultProperty: "data",
                AttachedType: "QQuickKeysAttached",
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: [CreateTypeExport()],
                Properties:
                [
                    CreateProperty("width", "double", defaultValue: "0", notifySignal: "widthChanged"),
                    CreateProperty("height", "double", defaultValue: "0", notifySignal: "heightChanged"),
                    CreateProperty("visible", "bool", defaultValue: "true", notifySignal: "visibleChanged"),
                ],
                Signals:
                [
                    CreateSignal("widthChanged", CreateParameter()),
                    CreateSignal("visibleChanged"),
                ],
                Methods:
                [
                    CreateMethod("forceLayout", "void"),
                    CreateMethod(
                        "contains",
                        "bool",
                        new QmlParameter("x", "double"),
                        new QmlParameter("y", "double")),
                ],
                Enums:
                [
                    CreateEnum(),
                ],
                Interfaces: ["QQmlParserStatus"]);
        }

        public static QmlType CreateRectangleType()
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
                Exports: [new QmlTypeExport("QtQuick", "Rectangle", new QmlVersion(2, 15))],
                Properties:
                [
                    CreateProperty("color", "color", notifySignal: "colorChanged"),
                    CreateProperty("radius", "double", defaultValue: "0", notifySignal: "radiusChanged"),
                    CreateProperty("width", "int", defaultValue: "100", notifySignal: "widthChanged"),
                ],
                Signals:
                [
                    CreateSignal("colorChanged"),
                    CreateSignal("radiusChanged"),
                ],
                Methods:
                [
                    CreateMethod("contains", "bool", new QmlParameter("point", "point")),
                    CreateMethod("startAnimation", "void"),
                ],
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        public static QmlType CreateKeysAttachedType()
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
                Properties: [CreateProperty("enabled", "bool", notifySignal: "enabledChanged")],
                Signals: [CreateSignal("enabledChanged")],
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        public static QmlType CreateTextType()
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
                Exports: [new QmlTypeExport("QtQuick", "Text", new QmlVersion(2, 15))],
                Properties:
                [
                    CreateProperty("text", "string", notifySignal: "textChanged"),
                    CreateProperty("wrapMode", "int", defaultValue: "0", notifySignal: "wrapModeChanged"),
                ],
                Signals:
                [
                    CreateSignal("textChanged"),
                    CreateSignal("wrapModeChanged"),
                ],
                Methods:
                [
                    CreateMethod("append", "void", new QmlParameter("value", "string")),
                ],
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        public static QmlType CreateButtonType()
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
                Exports: [new QmlTypeExport("QtQuick.Controls", "Button", new QmlVersion(2, 15))],
                Properties:
                [
                    CreateProperty("text", "string", notifySignal: "textChanged"),
                    CreateProperty("checked", "bool", defaultValue: "false", notifySignal: "checkedChanged"),
                ],
                Signals:
                [
                    CreateSignal("clicked"),
                    CreateSignal("checkedChanged"),
                ],
                Methods:
                [
                    CreateMethod("click", "void"),
                    CreateMethod("toggle", "void"),
                ],
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        public static QmlType CreateColorValueType()
        {
            return new QmlType(
                QualifiedName: "QColor",
                QmlName: "color",
                ModuleUri: null,
                AccessSemantics: AccessSemantics.Value,
                Prototype: null,
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: false,
                Exports: ImmutableArray<QmlTypeExport>.Empty,
                Properties: ImmutableArray<QmlProperty>.Empty,
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        public static QmlType CreatePaletteSingletonType()
        {
            return new QmlType(
                QualifiedName: "QmlSharpPalette",
                QmlName: "Palette",
                ModuleUri: null,
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QObject",
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: true,
                IsCreatable: false,
                Exports: ImmutableArray<QmlTypeExport>.Empty,
                Properties: [CreateProperty("accentColor", "color", notifySignal: "accentColorChanged")],
                Signals: [CreateSignal("accentColorChanged")],
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        public static QmlType CreateSequenceType()
        {
            return new QmlType(
                QualifiedName: "QVariantList",
                QmlName: "list",
                ModuleUri: null,
                AccessSemantics: AccessSemantics.Sequence,
                Prototype: null,
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: false,
                Exports: ImmutableArray<QmlTypeExport>.Empty,
                Properties: ImmutableArray<QmlProperty>.Empty,
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        public static QmlTypeExport CreateTypeExport()
        {
            return new QmlTypeExport("QtQuick", "Item", new QmlVersion(2, 15));
        }

        public static QmlProperty CreateProperty()
        {
            return CreateProperty("width", "double", defaultValue: "0", notifySignal: "widthChanged");
        }

        public static QmlProperty CreateProperty(
            string name,
            string typeName,
            bool isReadonly = false,
            bool isList = false,
            bool isRequired = false,
            string? defaultValue = null,
            string? notifySignal = null)
        {
            return new QmlProperty(name, typeName, isReadonly, isList, isRequired, defaultValue, notifySignal);
        }

        public static QmlSignal CreateSignal()
        {
            return CreateSignal("widthChanged", CreateParameter());
        }

        public static QmlSignal CreateSignal(string name, params QmlParameter[] parameters)
        {
            return new QmlSignal(name, parameters.ToImmutableArray());
        }

        public static QmlMethod CreateMethod()
        {
            return CreateMethod("forceLayout", "void", CreateParameter());
        }

        public static QmlMethod CreateMethod(string name, string? returnType, params QmlParameter[] parameters)
        {
            return new QmlMethod(name, returnType, parameters.ToImmutableArray());
        }

        public static QmlParameter CreateParameter()
        {
            return new QmlParameter("value", "double");
        }

        public static QmlEnum CreateEnum()
        {
            return new QmlEnum("TransformOrigin", false, [CreateEnumValue(), new QmlEnumValue("Center", 1)]);
        }

        public static QmlEnumValue CreateEnumValue()
        {
            return new QmlEnumValue("TopLeft", 0);
        }

        private static QmlRegistry CreateRegistry(
            IEnumerable<QmlModule> modules,
            IEnumerable<QmlType> types,
            IEnumerable<QmlType> builtins)
        {
            ImmutableArray<QmlModule> orderedModules = modules
                .OrderBy(module => module.Uri, StringComparer.Ordinal)
                .ToImmutableArray();
            ImmutableArray<QmlType> orderedTypes = types
                .OrderBy(type => type.QualifiedName, StringComparer.Ordinal)
                .ToImmutableArray();
            ImmutableArray<QmlType> orderedBuiltins = builtins
                .OrderBy(type => type.QualifiedName, StringComparer.Ordinal)
                .ToImmutableArray();

            return new QmlRegistry(
                Modules: orderedModules,
                TypesByQualifiedName: orderedTypes.ToImmutableDictionary(type => type.QualifiedName, type => type, StringComparer.Ordinal),
                Builtins: orderedBuiltins,
                FormatVersion: 1,
                QtVersion: "6.11.0",
                BuildTimestamp: new DateTimeOffset(2026, 4, 23, 12, 0, 0, TimeSpan.Zero))
                .WithLookupIndexes();
        }

        private static QmlType CreateBuiltinType(string name, AccessSemantics semantics = AccessSemantics.Value)
        {
            return new QmlType(
                QualifiedName: name,
                QmlName: name,
                ModuleUri: null,
                AccessSemantics: semantics,
                Prototype: null,
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: false,
                Exports: ImmutableArray<QmlTypeExport>.Empty,
                Properties: ImmutableArray<QmlProperty>.Empty,
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static string GetPerformanceQualifiedName(int index)
        {
            return $"QmlSharpPerformanceType{index:D3}";
        }
    }
}
