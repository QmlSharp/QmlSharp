using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Parsing;

namespace QmlSharp.Registry.Tests.Helpers
{
    internal static class RawAstFixtures
    {
        public static RawQmltypesFile CreateQmltypesFile()
        {
            return new RawQmltypesFile(
                SourcePath: @"fixtures\qmltypes\minimal.qmltypes",
                Components: [CreateQmltypesComponent()],
                Diagnostics: ImmutableArray<RegistryDiagnostic>.Empty);
        }

        public static RawQmltypesComponent CreateQmltypesComponent()
        {
            return new RawQmltypesComponent(
                Name: "QQuickItem",
                AccessSemantics: "reference",
                Prototype: "QObject",
                DefaultProperty: "data",
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: ["QtQuick/Item 2.0"],
                ExportMetaObjectRevisions: [512],
                Interfaces: ["QQmlParserStatus"],
                Properties: [CreateQmltypesProperty()],
                Signals: [CreateQmltypesSignal()],
                Methods: [CreateQmltypesMethod()],
                Enums: [CreateQmltypesEnum()]);
        }

        public static RawQmltypesProperty CreateQmltypesProperty()
        {
            return new RawQmltypesProperty(
                Name: "width",
                Type: "double",
                IsReadonly: false,
                IsList: false,
                IsPointer: false,
                IsRequired: false,
                Read: "width",
                Write: "setWidth",
                Notify: "widthChanged",
                BindableProperty: "bindableWidth",
                Revision: 0);
        }

        public static RawQmltypesSignal CreateQmltypesSignal()
        {
            return new RawQmltypesSignal(
                Name: "widthChanged",
                Parameters: [CreateQmltypesParameter()],
                Revision: 0);
        }

        public static RawQmltypesMethod CreateQmltypesMethod()
        {
            return new RawQmltypesMethod(
                Name: "forceLayout",
                ReturnType: "void",
                Parameters: [CreateQmltypesParameter()],
                Revision: 0);
        }

        public static RawQmltypesParameter CreateQmltypesParameter()
        {
            return new RawQmltypesParameter(Name: "value", Type: "double");
        }

        public static RawQmltypesEnum CreateQmltypesEnum()
        {
            return new RawQmltypesEnum(Name: "TransformOrigin", Alias: null, IsFlag: false, Values: ["TopLeft", "Center"]);
        }

        public static RawQmldirFile CreateQmldirFile()
        {
            return new RawQmldirFile(
                SourcePath: @"fixtures\qmldir\minimal-qmldir",
                Module: "QtQuick",
                Plugins: [CreateQmldirPlugin()],
                Classname: "QtQuickPlugin",
                Imports: [CreateQmldirImport()],
                Depends: [new RawQmldirImport("QtQml", "2.15")],
                TypeEntries: [CreateQmldirTypeEntry()],
                Designersupported: ["true"],
                Typeinfo: "plugins.qmltypes",
                Diagnostics: ImmutableArray<RegistryDiagnostic>.Empty);
        }

        public static RawQmldirPlugin CreateQmldirPlugin()
        {
            return new RawQmldirPlugin(Name: "qtquickplugin", Path: "plugins/qtquickplugin");
        }

        public static RawQmldirImport CreateQmldirImport()
        {
            return new RawQmldirImport(Module: "QtQuick", Version: "2.15");
        }

        public static RawQmldirTypeEntry CreateQmldirTypeEntry()
        {
            return new RawQmldirTypeEntry(
                Name: "Item",
                Version: "2.15",
                FilePath: "Item.qml",
                IsSingleton: false,
                IsInternal: false,
                StyleSelector: null);
        }

        public static RawMetatypesFile CreateMetatypesFile()
        {
            return new RawMetatypesFile(
                SourcePath: @"fixtures\metatypes\minimal.json",
                Entries: [CreateMetatypesEntry()],
                Diagnostics: ImmutableArray<RegistryDiagnostic>.Empty);
        }

        public static RawMetatypesEntry CreateMetatypesEntry()
        {
            return new RawMetatypesEntry(
                InputFile: "qquickitem.h",
                Classes: [CreateMetatypesClass()]);
        }

        public static RawMetatypesClass CreateMetatypesClass()
        {
            return new RawMetatypesClass(
                ClassName: "QQuickItem",
                QualifiedClassName: "QQuickItem",
                IsObject: true,
                IsGadget: false,
                IsNamespace: false,
                SuperClasses: [CreateMetatypesSuperClass()],
                ClassInfos: [CreateMetatypesClassInfo()],
                Properties: [CreateMetatypesProperty()],
                Signals: [CreateMetatypesSignal()],
                Methods: [CreateMetatypesMethod()],
                Enums: [CreateMetatypesEnum()]);
        }

        public static RawMetatypesSuperClass CreateMetatypesSuperClass()
        {
            return new RawMetatypesSuperClass(Name: "QObject", Access: "public");
        }

        public static RawMetatypesClassInfo CreateMetatypesClassInfo()
        {
            return new RawMetatypesClassInfo(Name: "QML.Element", Value: "Item");
        }

        public static RawMetatypesProperty CreateMetatypesProperty()
        {
            return new RawMetatypesProperty(
                Name: "width",
                Type: "double",
                Read: "width",
                Write: "setWidth",
                Notify: "widthChanged",
                BindableProperty: "bindableWidth",
                Revision: 0,
                Index: 1,
                IsReadonly: false,
                IsConstant: false,
                IsFinal: false,
                IsRequired: false);
        }

        public static RawMetatypesSignal CreateMetatypesSignal()
        {
            return new RawMetatypesSignal(Name: "widthChanged", Arguments: [CreateMetatypesParameter()], Revision: 0);
        }

        public static RawMetatypesMethod CreateMetatypesMethod()
        {
            return new RawMetatypesMethod(
                Name: "forceLayout",
                ReturnType: "void",
                Arguments: [CreateMetatypesParameter()],
                Revision: 0,
                IsCloned: false);
        }

        public static RawMetatypesParameter CreateMetatypesParameter()
        {
            return new RawMetatypesParameter(Name: "value", Type: "double");
        }

        public static RawMetatypesEnum CreateMetatypesEnum()
        {
            return new RawMetatypesEnum(Name: "TransformOrigin", Alias: null, IsFlag: false, IsClass: false, Values: ["TopLeft", "Center"]);
        }
    }
}
