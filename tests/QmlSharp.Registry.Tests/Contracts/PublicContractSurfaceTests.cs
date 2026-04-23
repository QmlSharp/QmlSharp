using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Normalization;
using QmlSharp.Registry.Parsing;
using QmlSharp.Registry.Querying;
using QmlSharp.Registry.Scanning;
using QmlSharp.Registry.Snapshots;
using QmlSharp.Registry.Tests.Helpers;

namespace QmlSharp.Registry.Tests.Contracts
{
    public sealed class PublicContractSurfaceTests
    {
        [Fact]
        public void Public_interfaces_can_be_implemented_by_downstream_code()
        {
            object[] implementations =
            [
                new StubQtTypeScanner(),
                 new QmltypesParser(),
                 new QmldirParser(),
                 new StubQmltypesParser(),
                 new StubQmldirParser(),
                 new StubMetatypesParser(),
                 new StubTypeNameMapper(),
                 new StubTypeNormalizer(),
                new StubTypeRegistry(RegistryFixtures.CreateMinimalInheritanceFixture()),
                new StubRegistryQuery(RegistryFixtures.CreateMinimalInheritanceFixture()),
                new StubRegistrySnapshot(),
                new StubRegistryBuilder(),
            ];

            Assert.Equal(12, implementations.Length);
            Assert.All(implementations, Assert.NotNull);
        }

        [Fact]
        public void Public_records_can_be_instantiated_by_downstream_code()
        {
            QmlRegistry registry = RegistryFixtures.CreateMinimalInheritanceFixture();
            ImmutableArray<RegistryDiagnostic> diagnostics = ImmutableArray.Create(
                new RegistryDiagnostic(
                    DiagnosticSeverity.Warning,
                    DiagnosticCodes.UnresolvedPrototype,
                    "Prototype could not be resolved.",
                    @"fixtures\qmltypes\minimal.qmltypes",
                    4,
                    9));

            object[] records =
            [
                RawAstFixtures.CreateQmltypesFile(),
                RawAstFixtures.CreateQmltypesComponent(),
                RawAstFixtures.CreateQmltypesProperty(),
                RawAstFixtures.CreateQmltypesSignal(),
                RawAstFixtures.CreateQmltypesMethod(),
                RawAstFixtures.CreateQmltypesParameter(),
                RawAstFixtures.CreateQmltypesEnum(),
                RawAstFixtures.CreateQmldirFile(),
                RawAstFixtures.CreateQmldirPlugin(),
                RawAstFixtures.CreateQmldirImport(),
                RawAstFixtures.CreateQmldirTypeEntry(),
                RawAstFixtures.CreateMetatypesFile(),
                RawAstFixtures.CreateMetatypesEntry(),
                RawAstFixtures.CreateMetatypesClass(),
                RawAstFixtures.CreateMetatypesSuperClass(),
                RawAstFixtures.CreateMetatypesClassInfo(),
                RawAstFixtures.CreateMetatypesProperty(),
                RawAstFixtures.CreateMetatypesSignal(),
                RawAstFixtures.CreateMetatypesMethod(),
                RawAstFixtures.CreateMetatypesParameter(),
                RawAstFixtures.CreateMetatypesEnum(),
                registry,
                RegistryFixtures.CreateQtQuickModule(),
                RegistryFixtures.CreateQtQuickModuleType(),
                RegistryFixtures.CreateQObjectType(),
                RegistryFixtures.CreateItemType(),
                RegistryFixtures.CreateTypeExport(),
                RegistryFixtures.CreateProperty(),
                RegistryFixtures.CreateSignal(),
                RegistryFixtures.CreateMethod(),
                RegistryFixtures.CreateParameter(),
                RegistryFixtures.CreateEnum(),
                RegistryFixtures.CreateEnumValue(),
                new QmlVersion(2, 15),
                new ResolvedProperty(RegistryFixtures.CreateProperty(), RegistryFixtures.CreateItemType(), true),
                new ResolvedSignal(RegistryFixtures.CreateSignal(), RegistryFixtures.CreateItemType(), false),
                new ResolvedMethod(RegistryFixtures.CreateMethod(), RegistryFixtures.CreateItemType(), true),
                diagnostics[0],
                new ScannerConfig(
                    QtDir: @"C:\Qt\6.11.0\msvc2022_64",
                    ModuleFilter: ["QtQuick"],
                    IncludeInternal: false),
                new BuildConfig(
                    QtDir: @"C:\Qt\6.11.0\msvc2022_64",
                    SnapshotPath: @"C:\repo\data\qt-registry-snapshots\qt-6.11.0.bin",
                    ForceRebuild: false,
                    ModuleFilter: ["QtQuick"],
                    IncludeInternal: false),
                new BuildProgress(BuildPhase.ParsingQmltypes, 2, 6, "QtQuick"),
                new ScanResult(
                    QmltypesPaths: [@"C:\Qt\qml\QtQuick\plugins.qmltypes"],
                    QmldirPaths: [@"C:\Qt\qml\QtQuick\qmldir"],
                    MetatypesPaths: [@"C:\Qt\lib\metatypes\qtquick_metatypes.json"],
                    Diagnostics: diagnostics),
                new ScanValidation(IsValid: true, QtVersion: "6.11.0", ErrorMessage: null),
                new ParseResult<RawQmltypesFile>(RawAstFixtures.CreateQmltypesFile(), diagnostics),
                new NormalizeResult(registry, diagnostics),
                new BuildResult(new StubTypeRegistry(registry), new StubRegistryQuery(registry), diagnostics),
                new SnapshotValidity(
                    IsValid: true,
                    FormatVersion: registry.FormatVersion,
                    QtVersion: registry.QtVersion,
                    BuildTimestamp: registry.BuildTimestamp,
                    ErrorMessage: null),
            ];

            Assert.Equal(47, records.Length);
            Assert.All(records, Assert.NotNull);
        }

        [Fact]
        public void Public_contract_surface_matches_the_step_01_01_inventory()
        {
            HashSet<string> expected = new HashSet<string>(StringComparer.Ordinal)
            {
                typeof(IQtTypeScanner).FullName!,
                typeof(QtTypeScanner).FullName!,
                 typeof(IQmltypesParser).FullName!,
                 typeof(QmltypesParser).FullName!,
                 typeof(IQmldirParser).FullName!,
                 typeof(QmldirParser).FullName!,
                 typeof(IMetatypesParser).FullName!,
                typeof(ITypeNameMapper).FullName!,
                typeof(ITypeNormalizer).FullName!,
                typeof(ITypeRegistry).FullName!,
                typeof(IRegistryQuery).FullName!,
                typeof(IRegistrySnapshot).FullName!,
                typeof(IRegistryBuilder).FullName!,
                typeof(RawQmltypesFile).FullName!,
                typeof(RawQmltypesComponent).FullName!,
                typeof(RawQmltypesProperty).FullName!,
                typeof(RawQmltypesSignal).FullName!,
                typeof(RawQmltypesMethod).FullName!,
                typeof(RawQmltypesParameter).FullName!,
                typeof(RawQmltypesEnum).FullName!,
                typeof(RawQmldirFile).FullName!,
                typeof(RawQmldirPlugin).FullName!,
                typeof(RawQmldirImport).FullName!,
                typeof(RawQmldirTypeEntry).FullName!,
                typeof(RawMetatypesFile).FullName!,
                typeof(RawMetatypesEntry).FullName!,
                typeof(RawMetatypesClass).FullName!,
                typeof(RawMetatypesSuperClass).FullName!,
                typeof(RawMetatypesClassInfo).FullName!,
                typeof(RawMetatypesProperty).FullName!,
                typeof(RawMetatypesSignal).FullName!,
                typeof(RawMetatypesMethod).FullName!,
                typeof(RawMetatypesParameter).FullName!,
                typeof(RawMetatypesEnum).FullName!,
                typeof(QmlRegistry).FullName!,
                typeof(QmlModule).FullName!,
                typeof(QmlModuleType).FullName!,
                typeof(QmlType).FullName!,
                typeof(AccessSemantics).FullName!,
                typeof(QmlTypeExport).FullName!,
                typeof(QmlProperty).FullName!,
                typeof(QmlSignal).FullName!,
                typeof(QmlMethod).FullName!,
                typeof(QmlEnum).FullName!,
                typeof(QmlEnumValue).FullName!,
                typeof(QmlParameter).FullName!,
                typeof(QmlVersion).FullName!,
                typeof(ResolvedProperty).FullName!,
                typeof(ResolvedSignal).FullName!,
                typeof(ResolvedMethod).FullName!,
                typeof(RegistryDiagnostic).FullName!,
                typeof(DiagnosticSeverity).FullName!,
                typeof(DiagnosticCodes).FullName!,
                typeof(ScannerConfig).FullName!,
                typeof(BuildConfig).FullName!,
                typeof(BuildProgress).FullName!,
                typeof(BuildPhase).FullName!,
                typeof(ScanResult).FullName!,
                typeof(ScanValidation).FullName!,
                typeof(ParseResult<>).FullName!,
                typeof(NormalizeResult).FullName!,
                typeof(BuildResult).FullName!,
                typeof(SnapshotValidity).FullName!,
            };

            HashSet<string> actual = typeof(QmlRegistry).Assembly
                .GetExportedTypes()
                .Select(type => type.FullName!)
                .ToHashSet(StringComparer.Ordinal);

            Assert.Equal(expected.OrderBy(name => name), actual.OrderBy(name => name));
        }
    }
}
