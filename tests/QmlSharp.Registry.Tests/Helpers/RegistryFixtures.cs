namespace QmlSharp.Registry.Tests.Helpers
{
    internal static class RegistryFixtures
    {
        public static QmlRegistry CreateMinimalInheritanceFixture()
        {
            QmlType qObject = CreateQObjectType();
            QmlType item = CreateItemType();
            QmlType rectangle = CreateRectangleType();

            return new QmlRegistry(
                Modules:
                [
                    CreateQtQuickModule(),
                ],
                TypesByQualifiedName: ImmutableDictionary<string, QmlType>.Empty
                    .Add(qObject.QualifiedName, qObject)
                    .Add(item.QualifiedName, item)
                    .Add(rectangle.QualifiedName, rectangle),
                Builtins:
                [
                    new QmlType(
                        QualifiedName: "double",
                        QmlName: "double",
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
                        Interfaces: ImmutableArray<string>.Empty),
                ],
                FormatVersion: 1,
                QtVersion: "6.8.0",
                BuildTimestamp: new DateTimeOffset(2026, 4, 23, 12, 0, 0, TimeSpan.Zero));
        }

        public static QmlRegistry CreateModuleQueryFixture()
        {
            QmlRegistry baseFixture = CreateMinimalInheritanceFixture();
            QmlModule controlsModule = new QmlModule(
                Uri: "QtQuick.Controls",
                Version: new QmlVersion(2, 15),
                Dependencies: ["QtQuick"],
                Imports: ["QtQuick"],
                Types:
                [
                    new QmlModuleType("QQuickButton", "Button", new QmlVersion(2, 15)),
                ]);
            QmlType button = new QmlType(
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
                Properties: [new QmlProperty("text", "string", false, false, false, null, "textChanged")],
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);

            return baseFixture with
            {
                Modules = [CreateQtQuickModule(), controlsModule],
                TypesByQualifiedName = baseFixture.TypesByQualifiedName.Add(button.QualifiedName, button),
            };
        }

        public static QmlRegistry CreateCategoryFixture()
        {
            QmlRegistry baseFixture = CreateMinimalInheritanceFixture();
            QmlType valueType = new QmlType(
                QualifiedName: "QColor",
                QmlName: "color",
                ModuleUri: "QtQuick",
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
            QmlType singleton = new QmlType(
                QualifiedName: "QmlSharpPalette",
                QmlName: "Palette",
                ModuleUri: "QtQuick",
                AccessSemantics: AccessSemantics.Reference,
                Prototype: "QObject",
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: true,
                IsCreatable: false,
                Exports: [new QmlTypeExport("QtQuick", "Palette", new QmlVersion(2, 15))],
                Properties: ImmutableArray<QmlProperty>.Empty,
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
            QmlType sequence = new QmlType(
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

            return baseFixture with
            {
                TypesByQualifiedName = baseFixture.TypesByQualifiedName
                    .Add(valueType.QualifiedName, valueType)
                    .Add(singleton.QualifiedName, singleton)
                    .Add(sequence.QualifiedName, sequence),
            };
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
                    CreateProperty(),
                    new QmlProperty("height", "double", false, false, false, "0", "heightChanged"),
                ],
                Signals:
                [
                    CreateSignal(),
                ],
                Methods:
                [
                    CreateMethod(),
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
                    new QmlProperty("color", "color", false, false, false, null, "colorChanged"),
                ],
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
            return new QmlProperty("width", "double", false, false, false, "0", "widthChanged");
        }

        public static QmlSignal CreateSignal()
        {
            return new QmlSignal("widthChanged", [CreateParameter()]);
        }

        public static QmlMethod CreateMethod()
        {
            return new QmlMethod("forceLayout", "void", [CreateParameter()]);
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
    }
}
