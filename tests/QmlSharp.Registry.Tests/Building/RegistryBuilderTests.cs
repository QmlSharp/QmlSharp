using System.Diagnostics;
using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Normalization;
using QmlSharp.Registry.Parsing;
using QmlSharp.Registry.Scanning;
using QmlSharp.Registry.Snapshots;
using QmlSharp.Registry.Tests.Helpers;

namespace QmlSharp.Registry.Tests.Building
{
    [Collection(QtEnvironmentCollection.Name)]
    public sealed class RegistryBuilderTests
    {
        public static TheoryData<string> ParserExceptionKinds =>
        [
            "io",
            "unauthorized",
            "notsupported",
            "argument",
            "format",
            "invalidoperation",
        ];

        public static TheoryData<string> MetatypesParserExceptionKinds =>
        [
            "io",
            "unauthorized",
            "notsupported",
            "argument",
            "json",
            "format",
            "invalidoperation",
        ];

        public static TheoryData<string> SnapshotSaveExceptionKinds =>
        [
            "io",
            "unauthorized",
            "notsupported",
            "argument",
            "security",
        ];

        [Trait("Category", "Integration")]
        [SkipUnlessEnvironmentVariableFact(RegistryTestEnvironment.QtDirVariableName, RegistryTestEnvironment.QtSdkUnavailableReason)]
        public void BLD_01_Full_build_from_qt_sdk_returns_registry_and_query()
        {
            RegistryBuilder builder = new();
            string qtDir = GetQtDir();

            BuildResult result = builder.Build(new BuildConfig(qtDir, SnapshotPath: null, ForceRebuild: true, ModuleFilter: null, IncludeInternal: false));

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.TypeRegistry);
            Assert.NotNull(result.Query);
            Assert.NotEmpty(result.TypeRegistry!.Modules);
            Assert.NotEmpty(result.TypeRegistry.Types);
            Assert.NotNull(result.Query!.FindModule("QtQuick"));
            Assert.NotNull(result.Query.FindTypeByQmlName("QtQuick", "Item"));
            Assert.Contains(result.TypeRegistry.Types, type => string.Equals(type.QualifiedName, "QQuickItem", StringComparison.Ordinal));
        }

        [Trait("Category", "Integration")]
        [SkipUnlessEnvironmentVariableFact(RegistryTestEnvironment.QtDirVariableName, RegistryTestEnvironment.QtSdkUnavailableReason)]
        public void BLD_02_Build_with_progress_callback_reports_build_phases_in_order()
        {
            RegistryBuilder builder = new();
            string qtDir = GetQtDir();
            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "registry.snapshot.bin");
            List<BuildProgress> progress = [];

            BuildResult result = builder.Build(
                new BuildConfig(qtDir, snapshotPath, ForceRebuild: true, ModuleFilter: null, IncludeInternal: false),
                progress.Add);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                [
                    BuildPhase.Scanning,
                    BuildPhase.ParsingQmltypes,
                    BuildPhase.ParsingQmldir,
                    BuildPhase.ParsingMetatypes,
                    BuildPhase.Normalizing,
                    BuildPhase.SavingSnapshot,
                    BuildPhase.Complete,
                ],
                progress.Select(item => item.Phase).ToArray());
            Assert.Equal(Enumerable.Range(1, progress.Count), progress.Select(item => item.CurrentStep));
            Assert.All(progress, item => Assert.Equal(progress.Count, item.TotalSteps));
        }

        [Trait("Category", "Integration")]
        [SkipUnlessEnvironmentVariableFact(RegistryTestEnvironment.QtDirVariableName, RegistryTestEnvironment.QtSdkUnavailableReason)]
        public void BLD_03_Build_and_save_snapshot_creates_a_loadable_snapshot_file()
        {
            RegistryBuilder builder = new();
            string qtDir = GetQtDir();
            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "registry.snapshot.bin");

            BuildResult buildResult = builder.Build(
                new BuildConfig(qtDir, snapshotPath, ForceRebuild: true, ModuleFilter: null, IncludeInternal: false));
            BuildResult loadResult = builder.LoadFromSnapshot(snapshotPath);

            Assert.True(buildResult.IsSuccess);
            Assert.True(File.Exists(snapshotPath));
            Assert.True(loadResult.IsSuccess);
            Assert.NotNull(loadResult.Query);
            Assert.NotNull(loadResult.TypeRegistry);
            Assert.NotNull(loadResult.Query!.FindModule("QtQuick"));
        }

        [Fact]
        public void BLD_04_Load_from_snapshot_returns_a_registry_without_qt_sdk()
        {
            RegistryBuilder builder = new();
            RegistrySnapshot snapshot = new();
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();
            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "fixture.snapshot.bin");

            snapshot.SaveToFile(registry, snapshotPath);

            BuildResult result = builder.LoadFromSnapshot(snapshotPath);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.TypeRegistry);
            Assert.NotNull(result.Query);
            Assert.Equal(registry.QtVersion, result.TypeRegistry!.QtVersion);
            Assert.NotNull(result.Query!.FindTypeByQmlName("QtQuick.Controls", "Button"));
        }

        [Fact]
        public void BLD_05_BuildOrLoad_with_existing_valid_snapshot_loads_without_scanning_qt()
        {
            RegistryBuilder builder = new();
            RegistrySnapshot snapshot = new();
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();
            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "fixture.snapshot.bin");
            List<BuildProgress> progress = [];

            snapshot.SaveToFile(registry, snapshotPath);

            BuildResult result = builder.BuildOrLoad(
                new BuildConfig(
                    QtDir: Path.Join(temporaryDirectory.Path, "missing-qt"),
                    SnapshotPath: snapshotPath,
                    ForceRebuild: false,
                    ModuleFilter: null,
                    IncludeInternal: false),
                progress.Add);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.TypeRegistry);
            Assert.NotNull(result.Query);
            Assert.Equal(
                [BuildPhase.LoadingSnapshot, BuildPhase.Complete],
                progress.Select(item => item.Phase).ToArray());
            Assert.Equal(progress[^1].TotalSteps, progress[^1].CurrentStep);
            Assert.NotNull(result.Query!.FindTypeByQmlName("QtQuick.Controls", "Button"));
        }

        [Fact]
        public void BuildOrLoad_with_existing_valid_snapshot_loads_once_and_reports_complete_progress()
        {
            CountingSnapshot snapshot = new(RegistryFixtures.CreateQueryFixture());
            RegistryBuilder builder = new(
                new ThrowingQtTypeScanner(),
                new InlineQmltypesParser(new RegistryDiagnostic(DiagnosticSeverity.Warning, "TEST-QTP", "unused", null, null, null)),
                new InlineQmldirParser(new RegistryDiagnostic(DiagnosticSeverity.Warning, "TEST-QDP", "unused", null, null, null)),
                new InlineMetatypesParser(new RegistryDiagnostic(DiagnosticSeverity.Warning, "TEST-MTP", "unused", null, null, null)),
                new TypeNameMapper(),
                new InlineTypeNormalizer(new RegistryDiagnostic(DiagnosticSeverity.Warning, "TEST-NRM", "unused", null, null, null)),
                snapshot);
            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "fixture.snapshot.bin");
            File.WriteAllBytes(snapshotPath, [0x01]);
            List<BuildProgress> progress = [];

            BuildResult result = builder.BuildOrLoad(
                new BuildConfig(
                    QtDir: Path.Join(temporaryDirectory.Path, "missing-qt"),
                    SnapshotPath: snapshotPath,
                    ForceRebuild: false,
                    ModuleFilter: null,
                    IncludeInternal: false),
                progress.Add);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, snapshot.LoadFromFileCallCount);
            Assert.Equal(0, snapshot.CheckValidityCallCount);
            Assert.Equal(BuildPhase.Complete, progress[^1].Phase);
            Assert.Equal(progress[^1].TotalSteps, progress[^1].CurrentStep);
        }

        [Trait("Category", "Integration")]
        [SkipUnlessEnvironmentVariableFact(RegistryTestEnvironment.QtDirVariableName, RegistryTestEnvironment.QtSdkUnavailableReason)]
        public void BLD_06_BuildOrLoad_with_missing_snapshot_builds_from_qt_and_saves_snapshot()
        {
            RegistryBuilder builder = new();
            string qtDir = GetQtDir();
            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "missing.snapshot.bin");

            BuildResult result = builder.BuildOrLoad(
                new BuildConfig(qtDir, snapshotPath, ForceRebuild: false, ModuleFilter: null, IncludeInternal: false));

            Assert.True(result.IsSuccess);
            Assert.True(File.Exists(snapshotPath));
            Assert.NotNull(result.TypeRegistry);
            Assert.NotNull(result.Query);
            Assert.NotNull(result.Query!.FindModule("QtQuick"));
        }

        [Trait("Category", "Integration")]
        [SkipUnlessEnvironmentVariableFact(RegistryTestEnvironment.QtDirVariableName, RegistryTestEnvironment.QtSdkUnavailableReason)]
        public void BLD_07_BuildOrLoad_with_invalid_snapshot_rebuilds_and_replaces_the_snapshot()
        {
            RegistryBuilder builder = new();
            string qtDir = GetQtDir();
            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "invalid.snapshot.bin");
            List<BuildProgress> progress = [];

            File.WriteAllBytes(snapshotPath, [0x01, 0x02, 0x03, 0x04]);

            BuildResult result = builder.BuildOrLoad(
                new BuildConfig(qtDir, snapshotPath, ForceRebuild: false, ModuleFilter: null, IncludeInternal: false),
                progress.Add);

            Assert.True(result.IsSuccess);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Warning
                && string.Equals(diagnostic.Code, DiagnosticCodes.SnapshotCorrupt, StringComparison.Ordinal));

            RegistrySnapshot snapshot = new();
            SnapshotValidity validity = snapshot.CheckValidity(snapshotPath);

            Assert.True(validity.IsValid);
            Assert.Equal(BuildPhase.LoadingSnapshot, progress[0].Phase);
            Assert.Contains(progress.Select(item => item.Phase), phase => phase == BuildPhase.Scanning);
            Assert.Equal(BuildPhase.Complete, progress[^1].Phase);
        }

        [Trait("Category", "Integration")]
        [SkipUnlessEnvironmentVariableFact(RegistryTestEnvironment.QtDirVariableName, RegistryTestEnvironment.QtSdkUnavailableReason)]
        public void BuildOrLoad_force_rebuild_ignores_a_valid_snapshot_and_executes_the_build_path()
        {
            RegistryBuilder builder = new();
            RegistrySnapshot snapshot = new();
            string qtDir = GetQtDir();
            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "valid.snapshot.bin");
            byte[] originalBytes = snapshot.Serialize(RegistryFixtures.CreateQueryFixture());
            List<BuildProgress> progress = [];

            File.WriteAllBytes(snapshotPath, originalBytes);

            BuildResult result = builder.BuildOrLoad(
                new BuildConfig(qtDir, snapshotPath, ForceRebuild: true, ModuleFilter: null, IncludeInternal: false),
                progress.Add);

            Assert.True(result.IsSuccess);
            Assert.Equal(BuildPhase.Scanning, progress[0].Phase);
            Assert.DoesNotContain(progress.Select(item => item.Phase), phase => phase == BuildPhase.LoadingSnapshot);
            Assert.NotEqual(originalBytes, File.ReadAllBytes(snapshotPath));
        }

        [Fact]
        public void BLD_08_Build_with_invalid_qt_dir_returns_REG001()
        {
            RegistryBuilder builder = new();
            using TemporaryDirectory temporaryDirectory = new();
            string invalidQtDir = Path.Join(temporaryDirectory.Path, "missing-qt");

            BuildResult result = builder.Build(
                new BuildConfig(invalidQtDir, SnapshotPath: null, ForceRebuild: true, ModuleFilter: null, IncludeInternal: false));

            Assert.False(result.IsSuccess);
            Assert.Null(result.TypeRegistry);
            Assert.Null(result.Query);
            Assert.Contains(result.Diagnostics, diagnostic => string.Equals(diagnostic.Code, DiagnosticCodes.InvalidQtDir, StringComparison.Ordinal));
        }

        [Trait("Category", "Integration")]
        [Trait("Category", "Performance")]
        [SkipUnlessEnvironmentVariableFact(RegistryTestEnvironment.QtDirVariableName, RegistryTestEnvironment.QtSdkUnavailableReason)]
        public void BLD_09_Build_performance_full_build_is_within_budget()
        {
            RegistryBuilder builder = new();
            string qtDir = GetQtDir();
            BuildConfig config = new(qtDir, SnapshotPath: null, ForceRebuild: true, ModuleFilter: null, IncludeInternal: false);

            BuildResult warmupResult = builder.Build(config);
            Assert.True(warmupResult.IsSuccess);

            TimeSpan[] samples = new TimeSpan[3];
            for (int iteration = 0; iteration < samples.Length; iteration++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                BuildResult result = builder.Build(config);
                stopwatch.Stop();

                Assert.True(result.IsSuccess);
                samples[iteration] = stopwatch.Elapsed;
            }

            TimeSpan p99 = samples.Max();
            TimeSpan budget = GetFullBuildPerformanceBudget();
            Assert.True(
                p99 < budget,
                $"Warm full registry build P99 was {p99.TotalSeconds:F3} s; budget is {budget.TotalSeconds:F3} s.");
        }

        [Fact]
        public void Build_aggregates_diagnostics_from_scan_parse_normalize_and_snapshot_save_stages()
        {
            RegistryDiagnostic scanDiagnostic = new(DiagnosticSeverity.Warning, "TEST-SCN", "scan warning", @"C:\Qt", null, null);
            RegistryDiagnostic qmltypesDiagnostic = new(DiagnosticSeverity.Warning, "TEST-QTP", "qmltypes warning", @"C:\Qt\qml\QtQuick\plugins.qmltypes", 1, 1);
            RegistryDiagnostic qmldirDiagnostic = new(DiagnosticSeverity.Warning, "TEST-QDP", "qmldir warning", @"C:\Qt\qml\QtQuick\qmldir", 2, 1);
            RegistryDiagnostic metatypesDiagnostic = new(DiagnosticSeverity.Warning, "TEST-MTP", "metatypes warning", @"C:\Qt\metatypes\qt6quick_metatypes.json", 3, 1);
            RegistryDiagnostic normalizeDiagnostic = new(DiagnosticSeverity.Warning, "TEST-NRM", "normalizer warning", null, null, null);

            RegistryBuilder builder = new(
                new InlineQtTypeScanner(scanDiagnostic),
                new InlineQmltypesParser(qmltypesDiagnostic),
                new InlineQmldirParser(qmldirDiagnostic),
                new InlineMetatypesParser(metatypesDiagnostic),
                new TypeNameMapper(),
                new InlineTypeNormalizer(normalizeDiagnostic),
                new ThrowingSnapshot());
            using TemporaryDirectory temporaryDirectory = new();

            BuildResult result = builder.Build(
                new BuildConfig(@"C:\Qt\6.11.0\msvc2022_64", Path.Join(temporaryDirectory.Path, "registry.snapshot.bin"), ForceRebuild: true, ModuleFilter: null, IncludeInternal: false));

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.TypeRegistry);
            Assert.NotNull(result.Query);
            Assert.Contains(result.Diagnostics, diagnostic => string.Equals(diagnostic.Code, scanDiagnostic.Code, StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => string.Equals(diagnostic.Code, qmltypesDiagnostic.Code, StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => string.Equals(diagnostic.Code, qmldirDiagnostic.Code, StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => string.Equals(diagnostic.Code, metatypesDiagnostic.Code, StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => string.Equals(diagnostic.Code, normalizeDiagnostic.Code, StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => string.Equals(diagnostic.Code, DiagnosticCodes.SnapshotCorrupt, StringComparison.Ordinal));
        }

        [Fact]
        public void LoadFromSnapshot_with_invalid_snapshot_returns_a_snapshot_diagnostic()
        {
            RegistryBuilder builder = new();
            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "invalid.snapshot.bin");
            File.WriteAllBytes(snapshotPath, [0x00, 0x10, 0x20, 0x30]);

            BuildResult result = builder.LoadFromSnapshot(snapshotPath);

            Assert.False(result.IsSuccess);
            Assert.Null(result.TypeRegistry);
            Assert.Null(result.Query);
            Assert.Contains(result.Diagnostics, diagnostic =>
                string.Equals(diagnostic.Code, DiagnosticCodes.SnapshotCorrupt, StringComparison.Ordinal));
        }

        [Theory]
        [MemberData(nameof(ParserExceptionKinds))]
        public void Build_qmltypes_parser_exceptions_are_reported_as_REG010_errors(string exceptionKind)
        {
            RegistryBuilder builder = new(
                new EmptyQtTypeScanner(),
                new ThrowingQmltypesParser(CreateException(exceptionKind)),
                new EmptyQmldirParser(),
                new EmptyMetatypesParser(),
                new TypeNameMapper(),
                new PropagatingTypeNormalizer(),
                new NoopSnapshot());

            BuildResult result = builder.Build(
                new BuildConfig(@"C:\Qt\6.11.0\msvc2022_64", SnapshotPath: null, ForceRebuild: true, ModuleFilter: null, IncludeInternal: false));

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.TypeRegistry);
            Assert.NotNull(result.Query);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error
                && string.Equals(diagnostic.Code, DiagnosticCodes.QmltypesSyntaxError, StringComparison.Ordinal)
                && diagnostic.Message.Contains("Failed to parse qmltypes file", StringComparison.Ordinal));
        }

        [Theory]
        [MemberData(nameof(ParserExceptionKinds))]
        public void Build_qmldir_parser_exceptions_are_reported_as_REG020_errors(string exceptionKind)
        {
            RegistryBuilder builder = new(
                new EmptyQtTypeScanner(),
                new EmptyQmltypesParser(),
                new ThrowingQmldirParser(CreateException(exceptionKind)),
                new EmptyMetatypesParser(),
                new TypeNameMapper(),
                new PropagatingTypeNormalizer(),
                new NoopSnapshot());

            BuildResult result = builder.Build(
                new BuildConfig(@"C:\Qt\6.11.0\msvc2022_64", SnapshotPath: null, ForceRebuild: true, ModuleFilter: null, IncludeInternal: false));

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.TypeRegistry);
            Assert.NotNull(result.Query);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error
                && string.Equals(diagnostic.Code, DiagnosticCodes.QmldirSyntaxError, StringComparison.Ordinal)
                && diagnostic.Message.Contains("Failed to parse qmldir file", StringComparison.Ordinal));
        }

        [Theory]
        [MemberData(nameof(MetatypesParserExceptionKinds))]
        public void Build_metatypes_parser_exceptions_are_reported_as_REG030_errors(string exceptionKind)
        {
            RegistryBuilder builder = new(
                new EmptyQtTypeScanner(),
                new EmptyQmltypesParser(),
                new EmptyQmldirParser(),
                new ThrowingMetatypesParser(CreateException(exceptionKind, includeVersionMismatchCode: false)),
                new TypeNameMapper(),
                new PropagatingTypeNormalizer(),
                new NoopSnapshot());

            BuildResult result = builder.Build(
                new BuildConfig(@"C:\Qt\6.11.0\msvc2022_64", SnapshotPath: null, ForceRebuild: true, ModuleFilter: null, IncludeInternal: false));

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.TypeRegistry);
            Assert.NotNull(result.Query);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error
                && string.Equals(diagnostic.Code, DiagnosticCodes.MetatypesJsonError, StringComparison.Ordinal)
                && diagnostic.Message.Contains("Failed to parse metatypes file", StringComparison.Ordinal));
        }

        [Theory]
        [MemberData(nameof(SnapshotSaveExceptionKinds))]
        public void Build_snapshot_save_exceptions_are_reported_as_snapshot_errors(string exceptionKind)
        {
            RegistryBuilder builder = new(
                new EmptyQtTypeScanner(),
                new EmptyQmltypesParser(),
                new EmptyQmldirParser(),
                new EmptyMetatypesParser(),
                new TypeNameMapper(),
                new PropagatingTypeNormalizer(),
                new ThrowingSaveSnapshot(CreateException(exceptionKind)));
            using TemporaryDirectory temporaryDirectory = new();

            BuildResult result = builder.Build(
                new BuildConfig(
                    @"C:\Qt\6.11.0\msvc2022_64",
                    Path.Join(temporaryDirectory.Path, "registry.snapshot.bin"),
                    ForceRebuild: true,
                    ModuleFilter: null,
                    IncludeInternal: false));

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.TypeRegistry);
            Assert.NotNull(result.Query);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error
                && string.Equals(diagnostic.Code, DiagnosticCodes.SnapshotCorrupt, StringComparison.Ordinal)
                && diagnostic.Message.Contains("Failed to save registry snapshot", StringComparison.Ordinal));
        }

        [Fact]
        public void BuildOrLoad_when_snapshot_load_fails_but_validity_is_still_true_rebuilds_with_warning()
        {
            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Join(temporaryDirectory.Path, "fixture.snapshot.bin");
            File.WriteAllBytes(snapshotPath, [0x01, 0x02, 0x03]);
            List<BuildProgress> progress = [];

            RegistryBuilder builder = new(
                new EmptyQtTypeScanner(),
                new EmptyQmltypesParser(),
                new EmptyQmldirParser(),
                new EmptyMetatypesParser(),
                new TypeNameMapper(),
                new EmptyTypeNormalizer(),
                new FallbackSnapshot(new IOException("snapshot file is busy")));

            BuildResult result = builder.BuildOrLoad(
                new BuildConfig(@"C:\Qt\6.11.0\msvc2022_64", snapshotPath, ForceRebuild: false, ModuleFilter: null, IncludeInternal: false),
                progress.Add);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.TypeRegistry);
            Assert.NotNull(result.Query);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Warning
                && string.Equals(diagnostic.Code, DiagnosticCodes.SnapshotCorrupt, StringComparison.Ordinal)
                && diagnostic.Message.Contains("Failed to load registry snapshot", StringComparison.Ordinal));
            Assert.Equal(BuildPhase.LoadingSnapshot, progress[0].Phase);
            Assert.Contains(progress.Select(item => item.Phase), phase => phase == BuildPhase.Scanning);
            Assert.Equal(BuildPhase.Complete, progress[^1].Phase);
        }

        [Fact]
        public void Build_returns_failure_when_normalizer_does_not_produce_a_registry()
        {
            RegistryBuilder builder = new(
                new EmptyQtTypeScanner(),
                new EmptyQmltypesParser(),
                new EmptyQmldirParser(),
                new EmptyMetatypesParser(),
                new TypeNameMapper(),
                new NullTypeNormalizer(),
                new NoopSnapshot());

            BuildResult result = builder.Build(
                new BuildConfig(@"C:\Qt\6.11.0\msvc2022_64", SnapshotPath: null, ForceRebuild: true, ModuleFilter: null, IncludeInternal: false));

            Assert.False(result.IsSuccess);
            Assert.Null(result.TypeRegistry);
            Assert.Null(result.Query);
        }

        [Theory]
        [InlineData("unauthorized", DiagnosticCodes.SnapshotCorrupt)]
        [InlineData("notsupported", DiagnosticCodes.SnapshotVersionMismatch)]
        [InlineData("argument", DiagnosticCodes.SnapshotCorrupt)]
        public void LoadFromSnapshot_wraps_expected_snapshot_load_exceptions(string exceptionKind, string expectedCode)
        {
            RegistryBuilder builder = new(
                new EmptyQtTypeScanner(),
                new EmptyQmltypesParser(),
                new EmptyQmldirParser(),
                new EmptyMetatypesParser(),
                new TypeNameMapper(),
                new EmptyTypeNormalizer(),
                new ThrowingLoadSnapshot(CreateException(exceptionKind)));

            BuildResult result = builder.LoadFromSnapshot(@"C:\snapshots\fixture.snapshot.bin");

            Assert.False(result.IsSuccess);
            Assert.Null(result.TypeRegistry);
            Assert.Null(result.Query);
            Assert.Contains(result.Diagnostics, diagnostic => string.Equals(diagnostic.Code, expectedCode, StringComparison.Ordinal));
        }

        [Fact]
        public void Build_qmltypes_parser_null_results_are_converted_into_stage_diagnostics()
        {
            RegistryBuilder builder = new(
                new EmptyQtTypeScanner(),
                new NullQmltypesParser(),
                new EmptyQmldirParser(),
                new EmptyMetatypesParser(),
                new TypeNameMapper(),
                new PropagatingTypeNormalizer(),
                new NoopSnapshot());

            BuildResult result = builder.Build(
                new BuildConfig(@"C:\Qt\6.11.0\msvc2022_64", SnapshotPath: null, ForceRebuild: true, ModuleFilter: null, IncludeInternal: false));

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code == DiagnosticCodes.QmltypesSyntaxError
                && diagnostic.Message.Contains("returned no file content", StringComparison.Ordinal));
        }

        [Fact]
        public void Build_qmldir_parser_null_results_are_converted_into_stage_diagnostics()
        {
            RegistryBuilder builder = new(
                new EmptyQtTypeScanner(),
                new EmptyQmltypesParser(),
                new NullQmldirParser(),
                new EmptyMetatypesParser(),
                new TypeNameMapper(),
                new PropagatingTypeNormalizer(),
                new NoopSnapshot());

            BuildResult result = builder.Build(
                new BuildConfig(@"C:\Qt\6.11.0\msvc2022_64", SnapshotPath: null, ForceRebuild: true, ModuleFilter: null, IncludeInternal: false));

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code == DiagnosticCodes.QmldirSyntaxError
                && diagnostic.Message.Contains("returned no file content", StringComparison.Ordinal));
        }

        [Fact]
        public void Build_metatypes_parser_null_results_are_converted_into_stage_diagnostics()
        {
            RegistryBuilder builder = new(
                new EmptyQtTypeScanner(),
                new EmptyQmltypesParser(),
                new EmptyQmldirParser(),
                new NullMetatypesParser(),
                new TypeNameMapper(),
                new PropagatingTypeNormalizer(),
                new NoopSnapshot());

            BuildResult result = builder.Build(
                new BuildConfig(@"C:\Qt\6.11.0\msvc2022_64", SnapshotPath: null, ForceRebuild: true, ModuleFilter: null, IncludeInternal: false));

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code == DiagnosticCodes.MetatypesJsonError
                && diagnostic.Message.Contains("returned no file content", StringComparison.Ordinal));
        }

        private static Exception CreateException(string exceptionKind, bool includeVersionMismatchCode = true)
        {
            return exceptionKind switch
            {
                "io" => new IOException("disk failure"),
                "unauthorized" => new UnauthorizedAccessException("access denied"),
                "notsupported" when includeVersionMismatchCode => new NotSupportedException($"{DiagnosticCodes.SnapshotVersionMismatch}: unsupported snapshot format"),
                "notsupported" => new NotSupportedException("operation is not supported"),
                "argument" => new ArgumentException("bad argument"),
                "format" => new FormatException("bad format"),
                "invalidoperation" => new InvalidOperationException("invalid operation"),
                "json" => new System.Text.Json.JsonException("invalid json"),
                "security" => new System.Security.SecurityException("security failure"),
                _ => throw new ArgumentOutOfRangeException(nameof(exceptionKind), exceptionKind, "Unknown exception kind."),
            };
        }

        private static string GetQtDir()
        {
            return Environment.GetEnvironmentVariable(RegistryTestEnvironment.QtDirVariableName)
                ?? throw new InvalidOperationException("QT_DIR must be set for this test.");
        }

        private static TimeSpan GetFullBuildPerformanceBudget()
        {
            return OperatingSystem.IsMacOS()
                && string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
                    ? TimeSpan.FromSeconds(4)
                    : TimeSpan.FromSeconds(3);
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Join(System.IO.Path.GetTempPath(), $"qmlsharp-registry-builder-{Guid.NewGuid():N}");
                _ = Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
        }

        private sealed class CountingSnapshot : IRegistrySnapshot
        {
            private readonly QmlRegistry registry;

            public CountingSnapshot(QmlRegistry registry)
            {
                this.registry = registry;
            }

            public int CheckValidityCallCount { get; private set; }

            public int LoadFromFileCallCount { get; private set; }

            public SnapshotValidity CheckValidity(string filePath)
            {
                CheckValidityCallCount++;
                return new SnapshotValidity(true, registry.FormatVersion, registry.QtVersion, registry.BuildTimestamp, null);
            }

            public QmlRegistry Deserialize(byte[] data)
            {
                return registry;
            }

            public QmlRegistry LoadFromFile(string filePath)
            {
                LoadFromFileCallCount++;
                return registry;
            }

            public void SaveToFile(QmlRegistry snapshotRegistry, string filePath)
            {
            }

            public byte[] Serialize(QmlRegistry snapshotRegistry)
            {
                return [0x01];
            }
        }

        private sealed class ThrowingQtTypeScanner : IQtTypeScanner
        {
            public ScanResult Scan(ScannerConfig config)
            {
                throw new InvalidOperationException("Snapshot load path must not scan Qt metadata.");
            }

            public ScanValidation ValidateQtDir(string qtDir)
            {
                throw new InvalidOperationException("Snapshot load path must not validate Qt metadata.");
            }

            public string? InferModuleUri(string qmldirPath, string qmlRootDir)
            {
                throw new InvalidOperationException("Snapshot load path must not infer Qt module URIs.");
            }
        }

        private sealed class InlineQtTypeScanner : IQtTypeScanner
        {
            private readonly RegistryDiagnostic diagnostic;

            public InlineQtTypeScanner(RegistryDiagnostic diagnostic)
            {
                this.diagnostic = diagnostic;
            }

            public ScanResult Scan(ScannerConfig config)
            {
                return new ScanResult(
                    [@"C:\Qt\qml\QtQuick\plugins.qmltypes"],
                    [@"C:\Qt\qml\QtQuick\qmldir"],
                    [@"C:\Qt\metatypes\qt6quick_metatypes.json"],
                    [diagnostic]);
            }

            public ScanValidation ValidateQtDir(string qtDir)
            {
                return new ScanValidation(IsValid: true, QtVersion: "6.11.0", ErrorMessage: null);
            }

            public string? InferModuleUri(string qmldirPath, string qmlRootDir)
            {
                return "QtQuick";
            }
        }

        private sealed class EmptyQtTypeScanner : IQtTypeScanner
        {
            public ScanResult Scan(ScannerConfig config)
            {
                return new ScanResult(
                    [@"C:\Qt\qml\QtQuick\plugins.qmltypes"],
                    [@"C:\Qt\qml\QtQuick\qmldir"],
                    [@"C:\Qt\metatypes\qt6quick_metatypes.json"],
                    ImmutableArray<RegistryDiagnostic>.Empty);
            }

            public ScanValidation ValidateQtDir(string qtDir)
            {
                return new ScanValidation(IsValid: true, QtVersion: "6.11.0", ErrorMessage: null);
            }

            public string? InferModuleUri(string qmldirPath, string qmlRootDir)
            {
                return "QtQuick";
            }
        }

        private sealed class InlineQmltypesParser : IQmltypesParser
        {
            private readonly RegistryDiagnostic diagnostic;

            public InlineQmltypesParser(RegistryDiagnostic diagnostic)
            {
                this.diagnostic = diagnostic;
            }

            public ParseResult<RawQmltypesFile> Parse(string filePath)
            {
                return new ParseResult<RawQmltypesFile>(
                    new RawQmltypesFile(filePath, ImmutableArray<RawQmltypesComponent>.Empty, [diagnostic]),
                    [diagnostic]);
            }

            public ParseResult<RawQmltypesFile> ParseContent(string content, string sourcePath)
            {
                return Parse(sourcePath);
            }
        }

        private sealed class EmptyQmltypesParser : IQmltypesParser
        {
            public ParseResult<RawQmltypesFile> Parse(string filePath)
            {
                return new ParseResult<RawQmltypesFile>(
                    new RawQmltypesFile(filePath, ImmutableArray<RawQmltypesComponent>.Empty, ImmutableArray<RegistryDiagnostic>.Empty),
                    ImmutableArray<RegistryDiagnostic>.Empty);
            }

            public ParseResult<RawQmltypesFile> ParseContent(string content, string sourcePath)
            {
                return Parse(sourcePath);
            }
        }

        private sealed class ThrowingQmltypesParser : IQmltypesParser
        {
            private readonly Exception exception;

            public ThrowingQmltypesParser(Exception exception)
            {
                this.exception = exception;
            }

            public ParseResult<RawQmltypesFile> Parse(string filePath)
            {
                throw exception;
            }

            public ParseResult<RawQmltypesFile> ParseContent(string content, string sourcePath)
            {
                return Parse(sourcePath);
            }
        }

        private sealed class NullQmltypesParser : IQmltypesParser
        {
            public ParseResult<RawQmltypesFile> Parse(string filePath)
            {
                return new ParseResult<RawQmltypesFile>(null, ImmutableArray<RegistryDiagnostic>.Empty);
            }

            public ParseResult<RawQmltypesFile> ParseContent(string content, string sourcePath)
            {
                return Parse(sourcePath);
            }
        }

        private sealed class InlineQmldirParser : IQmldirParser
        {
            private readonly RegistryDiagnostic diagnostic;

            public InlineQmldirParser(RegistryDiagnostic diagnostic)
            {
                this.diagnostic = diagnostic;
            }

            public ParseResult<RawQmldirFile> Parse(string filePath)
            {
                return new ParseResult<RawQmldirFile>(
                    new RawQmldirFile(
                        filePath,
                        "QtQuick",
                        ImmutableArray<RawQmldirPlugin>.Empty,
                        null,
                        ImmutableArray<RawQmldirImport>.Empty,
                        ImmutableArray<RawQmldirImport>.Empty,
                        ImmutableArray<RawQmldirTypeEntry>.Empty,
                        ImmutableArray<string>.Empty,
                        null,
                        [diagnostic]),
                    [diagnostic]);
            }

            public ParseResult<RawQmldirFile> ParseContent(string content, string sourcePath)
            {
                return Parse(sourcePath);
            }
        }

        private sealed class EmptyQmldirParser : IQmldirParser
        {
            public ParseResult<RawQmldirFile> Parse(string filePath)
            {
                return new ParseResult<RawQmldirFile>(
                    new RawQmldirFile(
                        filePath,
                        "QtQuick",
                        ImmutableArray<RawQmldirPlugin>.Empty,
                        null,
                        ImmutableArray<RawQmldirImport>.Empty,
                        ImmutableArray<RawQmldirImport>.Empty,
                        ImmutableArray<RawQmldirTypeEntry>.Empty,
                        ImmutableArray<string>.Empty,
                        null,
                        ImmutableArray<RegistryDiagnostic>.Empty),
                    ImmutableArray<RegistryDiagnostic>.Empty);
            }

            public ParseResult<RawQmldirFile> ParseContent(string content, string sourcePath)
            {
                return Parse(sourcePath);
            }
        }

        private sealed class ThrowingQmldirParser : IQmldirParser
        {
            private readonly Exception exception;

            public ThrowingQmldirParser(Exception exception)
            {
                this.exception = exception;
            }

            public ParseResult<RawQmldirFile> Parse(string filePath)
            {
                throw exception;
            }

            public ParseResult<RawQmldirFile> ParseContent(string content, string sourcePath)
            {
                return Parse(sourcePath);
            }
        }

        private sealed class NullQmldirParser : IQmldirParser
        {
            public ParseResult<RawQmldirFile> Parse(string filePath)
            {
                return new ParseResult<RawQmldirFile>(null, ImmutableArray<RegistryDiagnostic>.Empty);
            }

            public ParseResult<RawQmldirFile> ParseContent(string content, string sourcePath)
            {
                return Parse(sourcePath);
            }
        }

        private sealed class InlineMetatypesParser : IMetatypesParser
        {
            private readonly RegistryDiagnostic diagnostic;

            public InlineMetatypesParser(RegistryDiagnostic diagnostic)
            {
                this.diagnostic = diagnostic;
            }

            public ParseResult<RawMetatypesFile> Parse(string filePath)
            {
                return new ParseResult<RawMetatypesFile>(
                    new RawMetatypesFile(filePath, ImmutableArray<RawMetatypesEntry>.Empty, [diagnostic]),
                    [diagnostic]);
            }

            public ParseResult<RawMetatypesFile> ParseContent(string content, string sourcePath)
            {
                return Parse(sourcePath);
            }
        }

        private sealed class EmptyMetatypesParser : IMetatypesParser
        {
            public ParseResult<RawMetatypesFile> Parse(string filePath)
            {
                return new ParseResult<RawMetatypesFile>(
                    new RawMetatypesFile(filePath, ImmutableArray<RawMetatypesEntry>.Empty, ImmutableArray<RegistryDiagnostic>.Empty),
                    ImmutableArray<RegistryDiagnostic>.Empty);
            }

            public ParseResult<RawMetatypesFile> ParseContent(string content, string sourcePath)
            {
                return Parse(sourcePath);
            }
        }

        private sealed class ThrowingMetatypesParser : IMetatypesParser
        {
            private readonly Exception exception;

            public ThrowingMetatypesParser(Exception exception)
            {
                this.exception = exception;
            }

            public ParseResult<RawMetatypesFile> Parse(string filePath)
            {
                throw exception;
            }

            public ParseResult<RawMetatypesFile> ParseContent(string content, string sourcePath)
            {
                return Parse(sourcePath);
            }
        }

        private sealed class NullMetatypesParser : IMetatypesParser
        {
            public ParseResult<RawMetatypesFile> Parse(string filePath)
            {
                return new ParseResult<RawMetatypesFile>(null, ImmutableArray<RegistryDiagnostic>.Empty);
            }

            public ParseResult<RawMetatypesFile> ParseContent(string content, string sourcePath)
            {
                return Parse(sourcePath);
            }
        }

        private sealed class InlineTypeNormalizer : ITypeNormalizer
        {
            private readonly RegistryDiagnostic diagnostic;

            public InlineTypeNormalizer(RegistryDiagnostic diagnostic)
            {
                this.diagnostic = diagnostic;
            }

            public NormalizeResult Normalize(
                IReadOnlyList<RawQmltypesFile> qmltypesFiles,
                IReadOnlyList<(string ModuleUri, RawQmldirFile File)> qmldirFiles,
                IReadOnlyList<RawMetatypesFile> metatypesFiles,
                ITypeNameMapper typeNameMapper)
            {
                ImmutableArray<RegistryDiagnostic> diagnostics = qmltypesFiles
                    .SelectMany(file => file.Diagnostics)
                    .Concat(qmldirFiles.SelectMany(tuple => tuple.File.Diagnostics))
                    .Concat(metatypesFiles.SelectMany(file => file.Diagnostics))
                    .Append(diagnostic)
                    .ToImmutableArray();

                return new NormalizeResult(RegistryFixtures.CreateMinimalInheritanceFixture(), diagnostics);
            }
        }

        private sealed class PropagatingTypeNormalizer : ITypeNormalizer
        {
            public NormalizeResult Normalize(
                IReadOnlyList<RawQmltypesFile> qmltypesFiles,
                IReadOnlyList<(string ModuleUri, RawQmldirFile File)> qmldirFiles,
                IReadOnlyList<RawMetatypesFile> metatypesFiles,
                ITypeNameMapper typeNameMapper)
            {
                ImmutableArray<RegistryDiagnostic> diagnostics = qmltypesFiles
                    .SelectMany(file => file.Diagnostics)
                    .Concat(qmldirFiles.SelectMany(tuple => tuple.File.Diagnostics))
                    .Concat(metatypesFiles.SelectMany(file => file.Diagnostics))
                    .ToImmutableArray();

                return new NormalizeResult(RegistryFixtures.CreateMinimalInheritanceFixture(), diagnostics);
            }
        }

        private sealed class EmptyTypeNormalizer : ITypeNormalizer
        {
            public NormalizeResult Normalize(
                IReadOnlyList<RawQmltypesFile> qmltypesFiles,
                IReadOnlyList<(string ModuleUri, RawQmldirFile File)> qmldirFiles,
                IReadOnlyList<RawMetatypesFile> metatypesFiles,
                ITypeNameMapper typeNameMapper)
            {
                return new NormalizeResult(RegistryFixtures.CreateMinimalInheritanceFixture(), ImmutableArray<RegistryDiagnostic>.Empty);
            }
        }

        private sealed class NullTypeNormalizer : ITypeNormalizer
        {
            public NormalizeResult Normalize(
                IReadOnlyList<RawQmltypesFile> qmltypesFiles,
                IReadOnlyList<(string ModuleUri, RawQmldirFile File)> qmldirFiles,
                IReadOnlyList<RawMetatypesFile> metatypesFiles,
                ITypeNameMapper typeNameMapper)
            {
                return new NormalizeResult(null, ImmutableArray<RegistryDiagnostic>.Empty);
            }
        }

        private sealed class ThrowingSnapshot : IRegistrySnapshot
        {
            public SnapshotValidity CheckValidity(string filePath)
            {
                return new SnapshotValidity(true, 1, "6.11.0", DateTimeOffset.UtcNow, null);
            }

            public QmlRegistry Deserialize(byte[] data)
            {
                return RegistryFixtures.CreateMinimalInheritanceFixture();
            }

            public QmlRegistry LoadFromFile(string filePath)
            {
                return RegistryFixtures.CreateMinimalInheritanceFixture();
            }

            public void SaveToFile(QmlRegistry registry, string filePath)
            {
                throw new IOException("disk full");
            }

            public byte[] Serialize(QmlRegistry registry)
            {
                return [0x01];
            }
        }

        private sealed class NoopSnapshot : IRegistrySnapshot
        {
            public SnapshotValidity CheckValidity(string filePath)
            {
                return new SnapshotValidity(true, 1, "6.11.0", DateTimeOffset.UtcNow, null);
            }

            public QmlRegistry Deserialize(byte[] data)
            {
                return RegistryFixtures.CreateMinimalInheritanceFixture();
            }

            public QmlRegistry LoadFromFile(string filePath)
            {
                return RegistryFixtures.CreateMinimalInheritanceFixture();
            }

            public void SaveToFile(QmlRegistry registry, string filePath)
            {
            }

            public byte[] Serialize(QmlRegistry registry)
            {
                return [0x01];
            }
        }

        private sealed class ThrowingSaveSnapshot : IRegistrySnapshot
        {
            private readonly Exception exception;

            public ThrowingSaveSnapshot(Exception exception)
            {
                this.exception = exception;
            }

            public SnapshotValidity CheckValidity(string filePath)
            {
                return new SnapshotValidity(true, 1, "6.11.0", DateTimeOffset.UtcNow, null);
            }

            public QmlRegistry Deserialize(byte[] data)
            {
                return RegistryFixtures.CreateMinimalInheritanceFixture();
            }

            public QmlRegistry LoadFromFile(string filePath)
            {
                return RegistryFixtures.CreateMinimalInheritanceFixture();
            }

            public void SaveToFile(QmlRegistry registry, string filePath)
            {
                throw exception;
            }

            public byte[] Serialize(QmlRegistry registry)
            {
                return [0x01];
            }
        }

        private sealed class FallbackSnapshot : IRegistrySnapshot
        {
            private readonly Exception exception;

            public FallbackSnapshot(Exception exception)
            {
                this.exception = exception;
            }

            public SnapshotValidity CheckValidity(string filePath)
            {
                return new SnapshotValidity(true, 1, "6.11.0", DateTimeOffset.UtcNow, null);
            }

            public QmlRegistry Deserialize(byte[] data)
            {
                return RegistryFixtures.CreateMinimalInheritanceFixture();
            }

            public QmlRegistry LoadFromFile(string filePath)
            {
                throw exception;
            }

            public void SaveToFile(QmlRegistry registry, string filePath)
            {
            }

            public byte[] Serialize(QmlRegistry registry)
            {
                return [0x01];
            }
        }

        private sealed class ThrowingLoadSnapshot : IRegistrySnapshot
        {
            private readonly Exception exception;

            public ThrowingLoadSnapshot(Exception exception)
            {
                this.exception = exception;
            }

            public SnapshotValidity CheckValidity(string filePath)
            {
                return new SnapshotValidity(true, 1, "6.11.0", DateTimeOffset.UtcNow, null);
            }

            public QmlRegistry Deserialize(byte[] data)
            {
                return RegistryFixtures.CreateMinimalInheritanceFixture();
            }

            public QmlRegistry LoadFromFile(string filePath)
            {
                throw exception;
            }

            public void SaveToFile(QmlRegistry registry, string filePath)
            {
            }

            public byte[] Serialize(QmlRegistry registry)
            {
                return [0x01];
            }
        }
    }
}
