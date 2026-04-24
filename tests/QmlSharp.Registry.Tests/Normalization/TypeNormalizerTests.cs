using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Normalization;
using QmlSharp.Registry.Parsing;
using QmlSharp.Registry.Tests.Helpers;

namespace QmlSharp.Registry.Tests.Normalization
{
    public sealed class TypeNormalizerTests
    {
        [Fact]
        public void NRM_01_Normalize_empty_inputs_returns_empty_registry_with_frozen_indexes()
        {
            NormalizeResult result = CreateNormalizer().Normalize([], [], [], CreateMapper());

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Registry);
            Assert.Empty(result.Registry!.Modules);
            Assert.Empty(result.Registry.TypesByQualifiedName);
            Assert.NotEmpty(result.Registry.Builtins);
            Assert.Empty(result.Diagnostics);

            QmlRegistryLookupIndexes lookupIndexes = result.Registry.GetLookupIndexes();
            Assert.Empty(lookupIndexes.ModulesByUri);
            Assert.Empty(lookupIndexes.TypesByModuleAndQmlName);
        }

        [Fact]
        public void NRM_02_Normalize_single_qmltypes_source_only_creates_types_from_qmltypes_data()
        {
            RawQmltypesFile qmltypesFile = CreateQmltypesFile(
                CreateComponent(
                    name: "QQuickItem",
                    prototype: null,
                    exports: ["QtQuick/Item 2.0"],
                    properties: [RawAstFixtures.CreateQmltypesProperty()],
                    signals: [RawAstFixtures.CreateQmltypesSignal()],
                    methods: [RawAstFixtures.CreateQmltypesMethod()],
                    enums: [RawAstFixtures.CreateQmltypesEnum()]));

            NormalizeResult result = CreateNormalizer().Normalize([qmltypesFile], [], [], CreateMapper());

            Assert.True(result.IsSuccess);
            QmlType item = AssertType(result.Registry!, "QQuickItem");
            Assert.Equal("Item", item.QmlName);
            Assert.Equal("QtQuick", item.ModuleUri);
            Assert.Collection(
                item.Properties,
                property =>
                {
                    Assert.Equal("width", property.Name);
                    Assert.Equal("double", property.TypeName);
                });
            _ = Assert.Single(item.Signals);
            _ = Assert.Single(item.Methods);
            _ = Assert.Single(item.Enums);
            Assert.Empty(result.Registry!.Modules);
        }

        [Fact]
        public void NRM_03_Normalize_single_qmldir_source_only_creates_modules()
        {
            RawQmldirFile qmldirFile = RawAstFixtures.CreateQmldirFile();

            NormalizeResult result = CreateNormalizer().Normalize([], [("QtQuick", qmldirFile)], [], CreateMapper());

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Registry);
            Assert.Empty(result.Registry!.TypesByQualifiedName);

            QmlModule module = Assert.Single(result.Registry.Modules);
            Assert.Equal("QtQuick", module.Uri);
            Assert.Equal(new QmlVersion(2, 15), module.Version);
            Assert.Empty(module.Types);
            Assert.Equal("QtQuick", result.Registry.GetLookupIndexes().ModulesByUri[module.Uri].Uri);
        }

        [Fact]
        public void NRM_04_Normalize_single_metatypes_source_only_creates_fallback_types()
        {
            RawMetatypesFile metatypesFile = CreateMetatypesFile(
                CreateMetatypesClass(
                    className: "QQuickItem",
                    qmlElement: "Item",
                    prototype: "QObject",
                    attachedType: "QQuickKeysAttached",
                    properties: [RawAstFixtures.CreateMetatypesProperty()]));

            NormalizeResult result = CreateNormalizer().Normalize([], [], [metatypesFile], CreateMapper());

            Assert.True(result.IsSuccess);
            QmlType item = AssertType(result.Registry!, "QQuickItem");
            Assert.Equal("Item", item.QmlName);
            Assert.Equal(AccessSemantics.Reference, item.AccessSemantics);
            Assert.Equal("QQuickKeysAttached", item.AttachedType);
            Assert.Contains(item.Properties, property => property.Name == "width" && property.TypeName == "double");
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnresolvedPrototype);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnresolvedAttachedType);
        }

        [Fact]
        public void NRM_05_Three_source_merge_combines_qmltypes_qmldir_and_metatypes()
        {
            RawQmltypesFile qmltypesFile = CreateQmltypesFile(
                CreateComponent(
                    name: "QQuickItem",
                    prototype: null,
                    exports: ["QtQuick/Item 2.15"],
                    properties: [RawAstFixtures.CreateQmltypesProperty()]));

            NormalizeResult result = CreateNormalizer().Normalize(
                [qmltypesFile],
                [("QtQuick", RawAstFixtures.CreateQmldirFile())],
                [CreateMetatypesFile(CreateMetatypesClass(
                    className: "QQuickItem",
                    qmlElement: "Item",
                    properties: [RawAstFixtures.CreateMetatypesProperty() with { Name = "height", Type = "double", Notify = "heightChanged" }]))],
                CreateMapper());

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Registry);
            QmlType item = AssertType(result.Registry!, "QQuickItem");
            Assert.Equal("QtQuick", item.ModuleUri);
            Assert.Contains(item.Properties, property => property.Name == "width");
            Assert.Contains(item.Properties, property => property.Name == "height");

            QmlModule module = Assert.Single(result.Registry.Modules);
            Assert.Equal("QtQuick", module.Uri);
            Assert.Contains(module.Types, moduleType => moduleType.QualifiedName == "QQuickItem" && moduleType.QmlName == "Item");
        }

        [Fact]
        public void NRM_06_Qmltypes_primary_merge_keeps_qmltypes_values_when_metatypes_conflict()
        {
            RawQmltypesFile qmltypesFile = CreateQmltypesFile(
                CreateComponent(
                    name: "QQuickItem",
                    prototype: null,
                    exports: ["QtQuick/Item 2.0"],
                    properties: [RawAstFixtures.CreateQmltypesProperty()]));
            RawMetatypesFile metatypesFile = CreateMetatypesFile(
                CreateMetatypesClass(
                    className: "QQuickItem",
                    qmlElement: "Item",
                    properties: [RawAstFixtures.CreateMetatypesProperty() with { Type = "int" }]));

            NormalizeResult result = CreateNormalizer().Normalize([qmltypesFile], [], [metatypesFile], CreateMapper());

            QmlType item = AssertType(result.Registry!, "QQuickItem");
            QmlProperty property = Assert.Single(item.Properties);
            Assert.Equal("double", property.TypeName);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.TypeConflict);
        }

        [Fact]
        public void NRM_07_Metatypes_supplements_missing_members_without_overwriting_qmltypes_authority()
        {
            RawQmltypesFile qmltypesFile = CreateQmltypesFile(
                CreateComponent(
                    name: "QQuickItem",
                    prototype: null,
                    exports: ["QtQuick/Item 2.0"],
                    properties: []));

            NormalizeResult result = CreateNormalizer().Normalize(
                [qmltypesFile],
                [],
                [CreateMetatypesFile(CreateMetatypesClass(
                    className: "QQuickItem",
                    qmlElement: "Item",
                    properties: [RawAstFixtures.CreateMetatypesProperty()]))],
                CreateMapper());

            QmlType item = AssertType(result.Registry!, "QQuickItem");
            QmlProperty property = Assert.Single(item.Properties);
            Assert.Equal("width", property.Name);
            Assert.Equal("double", property.TypeName);
        }

        [Fact]
        public void NRM_08_Qmldir_can_attribute_types_to_modules_when_exports_are_missing()
        {
            RawQmltypesFile qmltypesFile = CreateQmltypesFile(CreateComponent(name: "QQuickItem", prototype: null, exports: []));
            RawMetatypesFile metatypesFile = CreateMetatypesFile(CreateMetatypesClass(className: "QQuickItem", qmlElement: "Item"));

            NormalizeResult result = CreateNormalizer().Normalize(
                [qmltypesFile],
                [("QtQuick", RawAstFixtures.CreateQmldirFile())],
                [metatypesFile],
                CreateMapper());

            QmlType item = AssertType(result.Registry!, "QQuickItem");
            Assert.Equal("QtQuick", item.ModuleUri);
            Assert.Contains(item.Exports, export => export.Module == "QtQuick" && export.Name == "Item" && export.Version == new QmlVersion(2, 15));
            Assert.Contains(result.Registry!.Modules.Single().Types, moduleType => moduleType.QualifiedName == "QQuickItem");
        }

        [Fact]
        public void NRM_09_Export_string_parsing_preserves_module_name_and_version_information()
        {
            RawQmltypesFile qmltypesFile = CreateQmltypesFile(
                CreateComponent(
                    name: "QQuickItem",
                    prototype: null,
                    exports: ["QtQuick/Item 2.0", "QtQuick/Item 6.0"]));

            NormalizeResult result = CreateNormalizer().Normalize([qmltypesFile], [], [], CreateMapper());

            QmlType item = AssertType(result.Registry!, "QQuickItem");
            Assert.Equal("Item", item.QmlName);
            Assert.Equal("QtQuick", item.ModuleUri);
            Assert.Contains(item.Exports, export => export.Version == new QmlVersion(2, 0));
            Assert.Contains(item.Exports, export => export.Version == new QmlVersion(6, 0));
        }

        [Fact]
        public void NRM_10_Inheritance_chain_resolution_populates_lookup_index_for_queries()
        {
            RawQmltypesFile qmltypesFile = CreateQmltypesFile(
                CreateComponent(name: "QObject", prototype: null, accessSemantics: "reference", exports: []),
                CreateComponent(name: "QQuickItem", prototype: "QObject", exports: ["QtQuick/Item 2.0"]),
                CreateComponent(name: "QQuickRectangle", prototype: "QQuickItem", exports: ["QtQuick/Rectangle 2.0"]));

            NormalizeResult result = CreateNormalizer().Normalize([qmltypesFile], [], [], CreateMapper());

            ImmutableArray<string> chain = result.Registry!.GetLookupIndexes().InheritanceChainsByQualifiedName["QQuickRectangle"];
            Assert.Equal(["QQuickRectangle", "QQuickItem", "QObject"], chain.ToArray());
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnresolvedPrototype);
        }

        [Fact]
        public void NRM_11_Circular_inheritance_is_detected()
        {
            RawQmltypesFile qmltypesFile = CreateQmltypesFile(
                CreateComponent(name: "QQuickA", prototype: "QQuickB", exports: ["QtQuick/A 2.0"]),
                CreateComponent(name: "QQuickB", prototype: "QQuickA", exports: ["QtQuick/B 2.0"]));

            NormalizeResult result = CreateNormalizer().Normalize([qmltypesFile], [], [], CreateMapper());

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.CircularInheritance);
            Assert.Equal(["QQuickA", "QQuickB"], result.Registry!.GetLookupIndexes().InheritanceChainsByQualifiedName["QQuickA"].ToArray());
        }

        [Fact]
        public void NRM_12_Attached_type_resolution_succeeds_when_the_attached_type_exists()
        {
            RawQmltypesFile qmltypesFile = CreateQmltypesFile(
                CreateComponent(name: "QQuickItem", prototype: null, attachedType: "QQuickKeysAttached", exports: ["QtQuick/Item 2.0"]),
                CreateComponent(name: "QQuickKeysAttached", prototype: null, exports: ["QtQuick/KeysAttached 2.0"]));

            NormalizeResult result = CreateNormalizer().Normalize([qmltypesFile], [], [], CreateMapper());

            QmlType item = AssertType(result.Registry!, "QQuickItem");
            Assert.Equal("QQuickKeysAttached", item.AttachedType);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnresolvedAttachedType);
        }

        [Fact]
        public void NRM_13_Unresolved_prototype_produces_REG041_warning()
        {
            RawQmltypesFile qmltypesFile = CreateQmltypesFile(
                CreateComponent(name: "QQuickItem", prototype: "QQuickMissingBase", exports: ["QtQuick/Item 2.0"]));

            NormalizeResult result = CreateNormalizer().Normalize([qmltypesFile], [], [], CreateMapper());

            Assert.True(result.Registry is not null);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnresolvedPrototype);
        }

        [Fact]
        public void NRM_14_Duplicate_exports_produce_REG044_warning()
        {
            RawQmltypesFile qmltypesFile = CreateQmltypesFile(
                CreateComponent(name: "QQuickItem", prototype: null, exports: ["QtQuick/Item 2.0"]),
                CreateComponent(name: "QQuickShadowItem", prototype: null, exports: ["QtQuick/Item 2.0"]));

            NormalizeResult result = CreateNormalizer().Normalize([qmltypesFile], [], [], CreateMapper());

            Assert.True(result.Registry is not null);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.DuplicateExport);
        }

        [Fact]
        public void NRM_15_Large_fixture_merge_produces_a_stable_registry_with_lookup_indexes()
        {
            RawQmltypesFile qmltypesFile = ParseQmltypesFixture("qtquick-excerpt.qmltypes");
            RawQmldirFile qmldirFile = ParseQmldirFixture("minimal-qmldir");
            RawMetatypesFile qtquickMetatypes = ParseMetatypesFixture("qtquick-excerpt.json");
            RawMetatypesFile supplementalMetatypes = ParseMetatypesFixture("multiple-classes.json");

            NormalizeResult result = CreateNormalizer().Normalize(
                [qmltypesFile],
                [("QtQuick", qmldirFile)],
                [qtquickMetatypes, supplementalMetatypes],
                CreateMapper());

            Assert.True(result.Registry is not null);
            Assert.True(result.Registry!.TypesByQualifiedName.Count >= 6);
            Assert.NotEmpty(result.Registry.Builtins);

            QmlType item = AssertType(result.Registry, "QQuickItem");
            Assert.Contains(item.Properties, property => property.Name == "width" && property.TypeName == "double");
            Assert.Contains(item.Properties, property => property.Name == "parent" && property.TypeName == "Item");

            QmlModule module = Assert.Single(result.Registry.Modules);
            Assert.Equal("QtQuick", module.Uri);
            Assert.Contains(module.Types, moduleType => moduleType.QmlName == "Item");
            Assert.Contains(module.Types, moduleType => moduleType.QmlName == "Rectangle");
            Assert.Equal("QQuickItem", result.Registry.GetLookupIndexes().TypesByModuleAndQmlName[("QtQuick", "Item")].QualifiedName);
        }

        [Fact]
        public void Duplicate_qmldir_modules_are_merged_before_lookup_indexes_are_built()
        {
            RawQmltypesFile qmltypesFile = CreateQmltypesFile(
                CreateComponent(name: "QQuickItem", prototype: null, exports: ["QtQuick/Item 2.0"]),
                CreateComponent(name: "QQuickRectangle", prototype: null, exports: ["QtQuick/Rectangle 6.0"]));
            RawQmldirFile firstQmldirFile = CreateQmldirFile(
                sourcePath: @"fixtures\qmldir\qtquick-a",
                module: "QtQuick",
                imports: [new RawQmldirImport("QtQml", "2.15")],
                depends: [new RawQmldirImport("QtCore", "6.0")],
                typeEntries: [new RawQmldirTypeEntry("Item", "2.0", "Item.qml", IsSingleton: false, IsInternal: false, StyleSelector: null)]);
            RawQmldirFile secondQmldirFile = CreateQmldirFile(
                sourcePath: @"fixtures\qmldir\qtquick-b",
                module: "QtQuick",
                imports: [new RawQmldirImport("QtQuick.Window", "6.0")],
                depends: [new RawQmldirImport("QtGui", "6.0")],
                typeEntries: [new RawQmldirTypeEntry("Rectangle", "6.0", "Rectangle.qml", IsSingleton: false, IsInternal: false, StyleSelector: null)]);

            NormalizeResult result = CreateNormalizer().Normalize(
                [qmltypesFile],
                [("QtQuick", firstQmldirFile), ("QtQuick", secondQmldirFile)],
                [],
                CreateMapper());

            Assert.True(result.IsSuccess);

            QmlModule module = Assert.Single(result.Registry!.Modules);
            Assert.Equal("QtQuick", module.Uri);
            Assert.Equal(new QmlVersion(6, 0), module.Version);
            Assert.Equal(["QtCore", "QtGui"], module.Dependencies.ToArray());
            Assert.Equal(["QtQml", "QtQuick.Window"], module.Imports.ToArray());
            Assert.Contains(module.Types, moduleType => moduleType.QualifiedName == "QQuickItem");
            Assert.Contains(module.Types, moduleType => moduleType.QualifiedName == "QQuickRectangle");
            Assert.Equal(module, result.Registry.GetLookupIndexes().ModulesByUri["QtQuick"]);
        }

        [Fact]
        public void Qmldir_export_matching_compares_versions_semantically()
        {
            RawQmltypesFile qmltypesFile = CreateQmltypesFile(
                CreateComponent(name: "QQuickItem", prototype: null, exports: ["QtQuick/Item 2.0"]),
                CreateComponent(name: "QQuickLegacyItem", prototype: null, exports: ["QtQuick/Item 1.0"]));
            RawQmldirFile qmldirFile = CreateQmldirFile(
                sourcePath: @"fixtures\qmldir\qtquick",
                module: "QtQuick",
                imports: [],
                depends: [],
                typeEntries: [new RawQmldirTypeEntry("Item", "2.00", "Item.qml", IsSingleton: true, IsInternal: false, StyleSelector: null)]);

            NormalizeResult result = CreateNormalizer().Normalize([qmltypesFile], [("QtQuick", qmldirFile)], [], CreateMapper());

            Assert.True(result.IsSuccess);
            Assert.True(AssertType(result.Registry!, "QQuickItem").IsSingleton);
            Assert.False(AssertType(result.Registry!, "QQuickLegacyItem").IsSingleton);
        }

        [Fact]
        public void Conflict_diagnostics_use_source_neutral_existing_and_incoming_language()
        {
            RawQmltypesFile qmltypesFile = CreateQmltypesFile(
                CreateComponent(
                    name: "QQuickItem",
                    prototype: null,
                    exports: ["QtQuick/Item 2.0"],
                    properties: [RawAstFixtures.CreateQmltypesProperty()]),
                CreateComponent(
                    name: "QQuickItem",
                    prototype: null,
                    exports: ["QtQuick/Item 2.0"],
                    properties: [RawAstFixtures.CreateQmltypesProperty() with { Type = "int" }]));

            NormalizeResult result = CreateNormalizer().Normalize([qmltypesFile], [], [], CreateMapper());

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics.Where(diagnostic => diagnostic.Code == DiagnosticCodes.TypeConflict));
            Assert.Contains("existing value 'double' wins over incoming value 'int'", diagnostic.Message);
            Assert.DoesNotContain("qmltypes value", diagnostic.Message);
            Assert.DoesNotContain("metatypes value", diagnostic.Message);
        }

        [Fact]
        public void Enum_conflict_diagnostics_describe_conflicting_definitions()
        {
            RawQmltypesFile qmltypesFile = CreateQmltypesFile(
                CreateComponent(
                    name: "QQuickItem",
                    prototype: null,
                    exports: ["QtQuick/Item 2.0"],
                    enums: [new RawQmltypesEnum("Mode", Alias: null, IsFlag: false, Values: ["First"])]),
                CreateComponent(
                    name: "QQuickItem",
                    prototype: null,
                    exports: ["QtQuick/Item 2.0"],
                    enums: [new RawQmltypesEnum("Mode", Alias: null, IsFlag: true, Values: ["Second"])]));

            NormalizeResult result = CreateNormalizer().Normalize([qmltypesFile], [], [], CreateMapper());

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics.Where(diagnostic => diagnostic.Code == DiagnosticCodes.TypeConflict));
            Assert.Contains("enum 'Mode'", diagnostic.Message);
            Assert.Contains("existing value 'enum [First]'", diagnostic.Message);
            Assert.Contains("incoming value 'flag [Second]'", diagnostic.Message);
        }

        private static QmlType AssertType(QmlRegistry registry, string qualifiedName)
        {
            KeyValuePair<string, QmlType> entry = Assert.Single(registry.TypesByQualifiedName, pair => pair.Key == qualifiedName);
            return entry.Value;
        }

        private static RawQmltypesComponent CreateComponent(
            string name,
            string? prototype,
            IEnumerable<string> exports,
            string? accessSemantics = "reference",
            string? attachedType = null,
            IEnumerable<RawQmltypesProperty>? properties = null,
            IEnumerable<RawQmltypesSignal>? signals = null,
            IEnumerable<RawQmltypesMethod>? methods = null,
            IEnumerable<RawQmltypesEnum>? enums = null)
        {
            return new RawQmltypesComponent(
                Name: name,
                AccessSemantics: accessSemantics,
                Prototype: prototype,
                DefaultProperty: null,
                AttachedType: attachedType,
                Extension: null,
                IsSingleton: false,
                IsCreatable: false,
                Exports: [.. exports],
                ExportMetaObjectRevisions: ImmutableArray<int>.Empty,
                Interfaces: ImmutableArray<string>.Empty,
                Properties: properties is null ? ImmutableArray<RawQmltypesProperty>.Empty : [.. properties],
                Signals: signals is null ? ImmutableArray<RawQmltypesSignal>.Empty : [.. signals],
                Methods: methods is null ? ImmutableArray<RawQmltypesMethod>.Empty : [.. methods],
                Enums: enums is null ? ImmutableArray<RawQmltypesEnum>.Empty : [.. enums]);
        }

        private static RawMetatypesClass CreateMetatypesClass(
            string className,
            string? qmlElement = null,
            string? prototype = null,
            string? attachedType = null,
            bool isObject = true,
            bool isGadget = false,
            bool isNamespace = false,
            IEnumerable<RawMetatypesProperty>? properties = null,
            IEnumerable<RawMetatypesSignal>? signals = null,
            IEnumerable<RawMetatypesMethod>? methods = null,
            IEnumerable<RawMetatypesEnum>? enums = null)
        {
            List<RawMetatypesClassInfo> classInfos = [];
            if (!string.IsNullOrWhiteSpace(qmlElement))
            {
                classInfos.Add(new RawMetatypesClassInfo("QML.Element", qmlElement));
            }

            if (!string.IsNullOrWhiteSpace(attachedType))
            {
                classInfos.Add(new RawMetatypesClassInfo("QML.Attached", attachedType));
            }

            return new RawMetatypesClass(
                ClassName: className,
                QualifiedClassName: className,
                IsObject: isObject,
                IsGadget: isGadget,
                IsNamespace: isNamespace,
                SuperClasses: prototype is null ? ImmutableArray<RawMetatypesSuperClass>.Empty : [new RawMetatypesSuperClass(prototype, "public")],
                ClassInfos: [.. classInfos],
                Properties: properties is null ? ImmutableArray<RawMetatypesProperty>.Empty : [.. properties],
                Signals: signals is null ? ImmutableArray<RawMetatypesSignal>.Empty : [.. signals],
                Methods: methods is null ? ImmutableArray<RawMetatypesMethod>.Empty : [.. methods],
                Enums: enums is null ? ImmutableArray<RawMetatypesEnum>.Empty : [.. enums]);
        }

        private static RawMetatypesFile CreateMetatypesFile(params RawMetatypesClass[] classes)
        {
            return new RawMetatypesFile(
                SourcePath: @"fixtures\metatypes\inline.json",
                Entries: [new RawMetatypesEntry(InputFile: "inline.h", Classes: [.. classes])],
                Diagnostics: ImmutableArray<RegistryDiagnostic>.Empty);
        }

        private static RawQmldirFile CreateQmldirFile(
            string sourcePath,
            string module,
            IEnumerable<RawQmldirImport> imports,
            IEnumerable<RawQmldirImport> depends,
            IEnumerable<RawQmldirTypeEntry> typeEntries)
        {
            return new RawQmldirFile(
                SourcePath: sourcePath,
                Module: module,
                Plugins: ImmutableArray<RawQmldirPlugin>.Empty,
                Classname: null,
                Imports: [.. imports],
                Depends: [.. depends],
                TypeEntries: [.. typeEntries],
                Designersupported: ImmutableArray<string>.Empty,
                Typeinfo: null,
                Diagnostics: ImmutableArray<RegistryDiagnostic>.Empty);
        }

        private static RawQmltypesFile CreateQmltypesFile(params RawQmltypesComponent[] components)
        {
            return new RawQmltypesFile(
                SourcePath: @"fixtures\qmltypes\inline.qmltypes",
                Components: [.. components],
                Diagnostics: ImmutableArray<RegistryDiagnostic>.Empty);
        }

        private static ITypeNameMapper CreateMapper()
        {
            return new TypeNameMapper();
        }

        private static ITypeNormalizer CreateNormalizer()
        {
            return new TypeNormalizer();
        }

        private static string GetFixturePath(string folderName, string fixtureName)
        {
            if (Path.IsPathRooted(fixtureName))
            {
                throw new ArgumentException("Fixture name must be a relative path.", nameof(fixtureName));
            }

            return Path.Join(AppContext.BaseDirectory, "fixtures", folderName, fixtureName);
        }

        private static RawMetatypesFile ParseMetatypesFixture(string fixtureName)
        {
            ParseResult<RawMetatypesFile> result = new MetatypesParser().Parse(GetFixturePath("metatypes", fixtureName));
            Assert.True(result.IsSuccess);
            return result.Value!;
        }

        private static RawQmldirFile ParseQmldirFixture(string fixtureName)
        {
            ParseResult<RawQmldirFile> result = new QmldirParser().Parse(GetFixturePath("qmldir", fixtureName));
            Assert.True(result.IsSuccess);
            return result.Value!;
        }

        private static RawQmltypesFile ParseQmltypesFixture(string fixtureName)
        {
            ParseResult<RawQmltypesFile> result = new QmltypesParser().Parse(GetFixturePath("qmltypes", fixtureName));
            Assert.True(result.IsSuccess);
            return result.Value!;
        }
    }
}
