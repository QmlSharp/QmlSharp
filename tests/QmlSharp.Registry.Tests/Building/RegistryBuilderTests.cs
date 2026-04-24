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
            string snapshotPath = Path.Combine(temporaryDirectory.Path, "registry.snapshot.bin");
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
            string snapshotPath = Path.Combine(temporaryDirectory.Path, "registry.snapshot.bin");

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
            string snapshotPath = Path.Combine(temporaryDirectory.Path, "fixture.snapshot.bin");

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
            string snapshotPath = Path.Combine(temporaryDirectory.Path, "fixture.snapshot.bin");
            List<BuildProgress> progress = [];

            snapshot.SaveToFile(registry, snapshotPath);

            BuildResult result = builder.BuildOrLoad(
                new BuildConfig(
                    QtDir: Path.Combine(temporaryDirectory.Path, "missing-qt"),
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
            Assert.NotNull(result.Query!.FindTypeByQmlName("QtQuick.Controls", "Button"));
        }

        [Trait("Category", "Integration")]
        [SkipUnlessEnvironmentVariableFact(RegistryTestEnvironment.QtDirVariableName, RegistryTestEnvironment.QtSdkUnavailableReason)]
        public void BLD_06_BuildOrLoad_with_missing_snapshot_builds_from_qt_and_saves_snapshot()
        {
            RegistryBuilder builder = new();
            string qtDir = GetQtDir();
            using TemporaryDirectory temporaryDirectory = new();
            string snapshotPath = Path.Combine(temporaryDirectory.Path, "missing.snapshot.bin");

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
            string snapshotPath = Path.Combine(temporaryDirectory.Path, "invalid.snapshot.bin");
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
            string snapshotPath = Path.Combine(temporaryDirectory.Path, "valid.snapshot.bin");
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
            string invalidQtDir = Path.Combine(temporaryDirectory.Path, "missing-qt");

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
        public void BLD_09_Build_performance_full_build_is_below_3_seconds()
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
            Assert.True(
                p99 < TimeSpan.FromSeconds(3),
                $"Warm full registry build P99 was {p99.TotalSeconds:F3} s; budget is 3.000 s.");
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
                new BuildConfig(@"C:\Qt\6.11.0\msvc2022_64", Path.Combine(temporaryDirectory.Path, "registry.snapshot.bin"), ForceRebuild: true, ModuleFilter: null, IncludeInternal: false));

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
            string snapshotPath = Path.Combine(temporaryDirectory.Path, "invalid.snapshot.bin");
            File.WriteAllBytes(snapshotPath, [0x00, 0x10, 0x20, 0x30]);

            BuildResult result = builder.LoadFromSnapshot(snapshotPath);

            Assert.False(result.IsSuccess);
            Assert.Null(result.TypeRegistry);
            Assert.Null(result.Query);
            Assert.Contains(result.Diagnostics, diagnostic =>
                string.Equals(diagnostic.Code, DiagnosticCodes.SnapshotCorrupt, StringComparison.Ordinal));
        }

        private static string GetQtDir()
        {
            return Environment.GetEnvironmentVariable(RegistryTestEnvironment.QtDirVariableName)
                ?? throw new InvalidOperationException("QT_DIR must be set for this test.");
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"qmlsharp-registry-builder-{Guid.NewGuid():N}");
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
    }
}
