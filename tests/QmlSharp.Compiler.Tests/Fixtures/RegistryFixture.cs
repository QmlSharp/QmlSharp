using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Compiler.Tests.Fixtures
{
    internal static class RegistryFixture
    {
        public static IRegistryQuery CreateQtQuickAndControlsRegistry()
        {
            QmlType item = CreateType(
                "QtQuick.Item",
                "Item",
                "QtQuick",
                properties: ImmutableArray.Create(
                    Property("width", "double"),
                    Property("height", "double"),
                    Property("visible", "bool"),
                    Property("implicitWidth", "double", isReadonly: true)));
            QmlType rectangle = CreateType(
                "QtQuick.Rectangle",
                "Rectangle",
                "QtQuick",
                prototype: "QtQuick.Item",
                properties: ImmutableArray.Create(
                    Property("color", "string"),
                    Property("border", "var"),
                    Property("gradient", "var")));
            QmlType text = CreateType(
                "QtQuick.Text",
                "Text",
                "QtQuick",
                prototype: "QtQuick.Item",
                properties: ImmutableArray.Create(
                    Property("text", "string"),
                    Property("horizontalAlignment", "int")),
                enums: ImmutableArray.Create(new QmlEnum(
                    "HAlignment",
                    IsFlag: false,
                    Values: ImmutableArray.Create(new QmlEnumValue("AlignLeft", 1)))));
            QmlType mouseArea = CreateType(
                "QtQuick.MouseArea",
                "MouseArea",
                "QtQuick",
                prototype: "QtQuick.Item",
                signals: ImmutableArray.Create(Signal("clicked")));
            QmlType layout = CreateType(
                "QtQuick.Layouts.Layout",
                "Layout",
                "QtQuick.Layouts",
                properties: ImmutableArray.Create(Property("fillWidth", "bool")));
            QmlType linearGradient = CreateType(
                "QtQuick.LinearGradient",
                "LinearGradient",
                "QtQuick",
                prototype: "QtQuick.Item");
            QmlType button = CreateType(
                "QtQuick.Controls.Button",
                "Button",
                "QtQuick.Controls",
                prototype: "QtQuick.Item",
                properties: ImmutableArray.Create(Property("text", "string")),
                signals: ImmutableArray.Create(Signal("clicked")));

            QmlModule qtQuick = new(
                "QtQuick",
                new QmlSharp.Registry.QmlVersion(6, 11),
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty,
                ImmutableArray.Create(
                    new QmlModuleType("QtQuick.Item", "Item", new QmlSharp.Registry.QmlVersion(6, 11)),
                    new QmlModuleType("QtQuick.Rectangle", "Rectangle", new QmlSharp.Registry.QmlVersion(6, 11)),
                    new QmlModuleType("QtQuick.Text", "Text", new QmlSharp.Registry.QmlVersion(6, 11)),
                    new QmlModuleType("QtQuick.MouseArea", "MouseArea", new QmlSharp.Registry.QmlVersion(6, 11)),
                    new QmlModuleType("QtQuick.LinearGradient", "LinearGradient", new QmlSharp.Registry.QmlVersion(6, 11))));

            QmlModule controls = new(
                "QtQuick.Controls",
                new QmlSharp.Registry.QmlVersion(6, 11),
                ImmutableArray<string>.Empty,
                ImmutableArray.Create("QtQuick"),
                ImmutableArray.Create(new QmlModuleType("QtQuick.Controls.Button", "Button", new QmlSharp.Registry.QmlVersion(6, 11))));

            QmlModule layouts = new(
                "QtQuick.Layouts",
                new QmlSharp.Registry.QmlVersion(6, 11),
                ImmutableArray<string>.Empty,
                ImmutableArray.Create("QtQuick"),
                ImmutableArray.Create(new QmlModuleType("QtQuick.Layouts.Layout", "Layout", new QmlSharp.Registry.QmlVersion(6, 11))));

            ImmutableDictionary<string, QmlType> types = ImmutableDictionary<string, QmlType>.Empty
                .Add(item.QualifiedName, item)
                .Add(rectangle.QualifiedName, rectangle)
                .Add(text.QualifiedName, text)
                .Add(mouseArea.QualifiedName, mouseArea)
                .Add(layout.QualifiedName, layout)
                .Add(linearGradient.QualifiedName, linearGradient)
                .Add(button.QualifiedName, button);

            QmlRegistry registry = new(
                ImmutableArray.Create(qtQuick, controls, layouts),
                types,
                ImmutableArray<QmlType>.Empty,
                FormatVersion: 1,
                QtVersion: "6.11.0",
                BuildTimestamp: DateTimeOffset.UnixEpoch);

            return new FixtureRegistryQuery(registry);
        }

        private static QmlType CreateType(
            string qualifiedName,
            string qmlName,
            string moduleUri,
            string? prototype = null,
            ImmutableArray<QmlProperty> properties = default,
            ImmutableArray<QmlSignal> signals = default,
            ImmutableArray<QmlEnum> enums = default)
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
                Exports: ImmutableArray.Create(new QmlTypeExport(moduleUri, qmlName, new QmlSharp.Registry.QmlVersion(6, 11))),
                Properties: properties.IsDefault ? ImmutableArray<QmlProperty>.Empty : properties,
                Signals: signals.IsDefault ? ImmutableArray<QmlSignal>.Empty : signals,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: enums.IsDefault ? ImmutableArray<QmlEnum>.Empty : enums,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlProperty Property(string name, string typeName, bool isReadonly = false)
        {
            return new QmlProperty(
                name,
                typeName,
                isReadonly,
                IsList: false,
                IsRequired: false,
                DefaultValue: null,
                NotifySignal: null);
        }

        private static QmlSignal Signal(string name)
        {
            return new QmlSignal(name, ImmutableArray<QmlParameter>.Empty);
        }
    }
}
