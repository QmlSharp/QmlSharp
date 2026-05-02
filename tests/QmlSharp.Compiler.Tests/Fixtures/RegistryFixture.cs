using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Compiler.Tests.Fixtures
{
    internal static class RegistryFixture
    {
        public static IRegistryQuery CreateQtQuickAndControlsRegistry()
        {
            QmlType item = CreateType("QtQuick.Item", "Item", "QtQuick");
            QmlType rectangle = CreateType("QtQuick.Rectangle", "Rectangle", "QtQuick", prototype: "QtQuick.Item");
            QmlType text = CreateType("QtQuick.Text", "Text", "QtQuick", prototype: "QtQuick.Item");
            QmlType button = CreateType("QtQuick.Controls.Button", "Button", "QtQuick.Controls", prototype: "QtQuick.Item");

            QmlModule qtQuick = new(
                "QtQuick",
                new QmlSharp.Registry.QmlVersion(6, 11),
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty,
                ImmutableArray.Create(
                    new QmlModuleType("QtQuick.Item", "Item", new QmlSharp.Registry.QmlVersion(6, 11)),
                    new QmlModuleType("QtQuick.Rectangle", "Rectangle", new QmlSharp.Registry.QmlVersion(6, 11)),
                    new QmlModuleType("QtQuick.Text", "Text", new QmlSharp.Registry.QmlVersion(6, 11))));

            QmlModule controls = new(
                "QtQuick.Controls",
                new QmlSharp.Registry.QmlVersion(6, 11),
                ImmutableArray<string>.Empty,
                ImmutableArray.Create("QtQuick"),
                ImmutableArray.Create(new QmlModuleType("QtQuick.Controls.Button", "Button", new QmlSharp.Registry.QmlVersion(6, 11))));

            ImmutableDictionary<string, QmlType> types = ImmutableDictionary<string, QmlType>.Empty
                .Add(item.QualifiedName, item)
                .Add(rectangle.QualifiedName, rectangle)
                .Add(text.QualifiedName, text)
                .Add(button.QualifiedName, button);

            QmlRegistry registry = new(
                ImmutableArray.Create(qtQuick, controls),
                types,
                ImmutableArray<QmlType>.Empty,
                FormatVersion: 1,
                QtVersion: "6.11.0",
                BuildTimestamp: DateTimeOffset.UnixEpoch);

            return new FixtureRegistryQuery(registry);
        }

        private static QmlType CreateType(string qualifiedName, string qmlName, string moduleUri, string? prototype = null)
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
                Properties: ImmutableArray<QmlProperty>.Empty,
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }
    }
}
