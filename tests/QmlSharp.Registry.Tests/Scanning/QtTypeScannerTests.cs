using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Scanning;
using QmlSharp.Registry.Tests.Helpers;

namespace QmlSharp.Registry.Tests.Scanning
{
    public sealed class QtTypeScannerTests
    {
        [Fact]
        public void SCN_02_Scan_non_existent_directory_returns_REG001()
        {
            QtTypeScanner scanner = new QtTypeScanner();
            string qtDir = Path.Join(
                Path.GetTempPath(),
                "qmlsharp-registry-tests",
                "missing-sdk",
                Guid.NewGuid().ToString("N"));

            ScanResult result = scanner.Scan(new ScannerConfig(qtDir, ModuleFilter: null, IncludeInternal: true));

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticCodes.InvalidQtDir, diagnostic.Code);
            Assert.Empty(result.QmltypesPaths);
            Assert.Empty(result.QmldirPaths);
            Assert.Empty(result.MetatypesPaths);
        }

        [Fact]
        public void SCN_03_Scan_directory_without_qml_subdirectory_returns_REG001()
        {
            using ScannerTestWorkspace workspace = new ScannerTestWorkspace();
            _ = workspace.CreateDirectory("lib/metatypes");

            QtTypeScanner scanner = new QtTypeScanner();
            ScanResult result = scanner.Scan(new ScannerConfig(workspace.RootDirectory, ModuleFilter: null, IncludeInternal: true));

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticCodes.InvalidQtDir, diagnostic.Code);
        }

        [Fact]
        public void SCN_05_Validate_invalid_directory_returns_error_message()
        {
            using ScannerTestWorkspace workspace = new ScannerTestWorkspace();

            QtTypeScanner scanner = new QtTypeScanner();
            ScanValidation validation = scanner.ValidateQtDir(workspace.RootDirectory);

            Assert.False(validation.IsValid);
            Assert.Null(validation.QtVersion);
            Assert.Contains("qml", validation.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SCN_06_Scan_discovers_qmltypes_recursively_and_normalizes_paths()
        {
            using ScannerTestWorkspace workspace = CreateSampleQtSdkWorkspace();
            string qtDir = workspace.RootDirectory.Replace(Path.DirectorySeparatorChar, '/');
            QtTypeScanner scanner = new QtTypeScanner();

            ScanResult result = scanner.Scan(new ScannerConfig(qtDir, ModuleFilter: null, IncludeInternal: true));

            AssertPathsEqual(
                [
                    workspace.GetPath("qml/Qt/labs/platform/plugins.qmltypes"),
                    workspace.GetPath("qml/QtQuick/Controls/plugins.qmltypes"),
                    workspace.GetPath("qml/QtQuick/plugins.qmltypes"),
                    workspace.GetPath("qml/QtQuick/private/Hidden/plugins.qmltypes"),
                ],
                result.QmltypesPaths);
        }

        [Fact]
        public void SCN_06A_Scan_accepts_backslash_delimited_qt_dir_input()
        {
            using ScannerTestWorkspace workspace = CreateSampleQtSdkWorkspace();
            string qtDir = workspace.RootDirectory
                .Replace(Path.DirectorySeparatorChar, '\\')
                .Replace(Path.AltDirectorySeparatorChar, '\\');
            QtTypeScanner scanner = new QtTypeScanner();

            ScanResult result = scanner.Scan(new ScannerConfig(qtDir, ModuleFilter: null, IncludeInternal: true));

            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.InvalidQtDir);
            Assert.NotEmpty(result.QmltypesPaths);
            Assert.NotEmpty(result.QmldirPaths);
            Assert.NotEmpty(result.MetatypesPaths);
        }

        [Fact]
        public void SCN_07_Scan_discovers_qmldir_files_recursively()
        {
            using ScannerTestWorkspace workspace = CreateSampleQtSdkWorkspace();
            QtTypeScanner scanner = new QtTypeScanner();

            ScanResult result = scanner.Scan(new ScannerConfig(workspace.RootDirectory, ModuleFilter: null, IncludeInternal: true));

            AssertPathsEqual(
                [
                    workspace.GetPath("qml/Qt/labs/platform/qmldir"),
                    workspace.GetPath("qml/QtQuick/Controls/qmldir"),
                    workspace.GetPath("qml/QtQuick/qmldir"),
                    workspace.GetPath("qml/QtQuick/private/Hidden/qmldir"),
                ],
                result.QmldirPaths);
        }

        [Fact]
        public void SCN_08_Scan_discovers_metatypes_files()
        {
            using ScannerTestWorkspace workspace = CreateSampleQtSdkWorkspace();
            QtTypeScanner scanner = new QtTypeScanner();

            ScanResult result = scanner.Scan(new ScannerConfig(workspace.RootDirectory, ModuleFilter: null, IncludeInternal: true));

            AssertPathsEqual(
                [
                    workspace.GetPath("lib/metatypes/qtlabsplatform_metatypes.json"),
                    workspace.GetPath("lib/metatypes/qtquick_metatypes.json"),
                    workspace.GetPath("lib/metatypes/qtquickprivate_metatypes.json"),
                ],
                result.MetatypesPaths);
        }

        [Fact]
        public void SCN_08A_Scan_discovers_metatypes_files_from_qt_root_metatypes_directory()
        {
            using ScannerTestWorkspace workspace = new ScannerTestWorkspace();
            _ = workspace.CreateDirectory("qml");
            _ = workspace.CreateDirectory("metatypes");
            _ = workspace.CreateFile("qml/QtQuick/plugins.qmltypes");
            _ = workspace.CreateFile("qml/QtQuick/qmldir", "module QtQuick");
            _ = workspace.CreateFile("metatypes/qtquick_metatypes.json", "{}");

            QtTypeScanner scanner = new QtTypeScanner();
            ScanResult result = scanner.Scan(new ScannerConfig(workspace.RootDirectory, ModuleFilter: null, IncludeInternal: true));

            AssertPathsEqual(
                [workspace.GetPath("metatypes/qtquick_metatypes.json")],
                result.MetatypesPaths);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.InvalidQtDir);
        }

        [Fact]
        public void SCN_09_Scan_filters_by_module_uri()
        {
            using ScannerTestWorkspace workspace = CreateSampleQtSdkWorkspace();
            QtTypeScanner scanner = new QtTypeScanner();

            ScanResult result = scanner.Scan(
                new ScannerConfig(workspace.RootDirectory, ModuleFilter: ["QtQuick"], IncludeInternal: true));

            AssertPathsEqual(
                [
                    workspace.GetPath("qml/QtQuick/Controls/plugins.qmltypes"),
                    workspace.GetPath("qml/QtQuick/plugins.qmltypes"),
                    workspace.GetPath("qml/QtQuick/private/Hidden/plugins.qmltypes"),
                ],
                result.QmltypesPaths);
            AssertPathsEqual(
                [
                    workspace.GetPath("qml/QtQuick/Controls/qmldir"),
                    workspace.GetPath("qml/QtQuick/qmldir"),
                    workspace.GetPath("qml/QtQuick/private/Hidden/qmldir"),
                ],
                result.QmldirPaths);
            AssertPathsEqual(
                [
                    workspace.GetPath("lib/metatypes/qtquick_metatypes.json"),
                    workspace.GetPath("lib/metatypes/qtquickprivate_metatypes.json"),
                ],
                result.MetatypesPaths);
        }

        [Fact]
        public void SCN_09A_Scan_filters_qt6_style_metatypes_for_qtquick_modules()
        {
            using ScannerTestWorkspace workspace = new ScannerTestWorkspace();
            CreateSdkStructure(workspace);
            _ = workspace.CreateFile("qml/QtQuick/plugins.qmltypes");
            _ = workspace.CreateFile("qml/QtQuick/qmldir", "module QtQuick");
            _ = workspace.CreateFile("qml/QtQuick/Controls/plugins.qmltypes");
            _ = workspace.CreateFile("qml/QtQuick/Controls/qmldir", "module QtQuick.Controls");
            _ = workspace.CreateFile("lib/metatypes/qt6quick_metatypes.json", "{}");
            _ = workspace.CreateFile("lib/metatypes/qt6quickcontrols2_metatypes.json", "{}");
            _ = workspace.CreateFile("lib/metatypes/qt6quickcontrolstestutilsprivate_metatypes.json", "{}");

            QtTypeScanner scanner = new QtTypeScanner();

            ScanResult result = scanner.Scan(
                new ScannerConfig(workspace.RootDirectory, ModuleFilter: ["QtQuick"], IncludeInternal: false));

            AssertPathsEqual(
                [
                    workspace.GetPath("lib/metatypes/qt6quick_metatypes.json"),
                    workspace.GetPath("lib/metatypes/qt6quickcontrols2_metatypes.json"),
                ],
                result.MetatypesPaths);
        }

        [Fact]
        public void SCN_09C_Scan_trims_and_matches_module_filter_case_insensitively()
        {
            using ScannerTestWorkspace workspace = CreateSampleQtSdkWorkspace();
            QtTypeScanner scanner = new QtTypeScanner();

            ScanResult result = scanner.Scan(
                new ScannerConfig(workspace.RootDirectory, ModuleFilter: [" qtquick.controls "], IncludeInternal: true));

            AssertPathsEqual(
                [workspace.GetPath("qml/QtQuick/Controls/plugins.qmltypes")],
                result.QmltypesPaths);
            AssertPathsEqual(
                [workspace.GetPath("qml/QtQuick/Controls/qmldir")],
                result.QmldirPaths);
            AssertPathsEqual(
                [],
                result.MetatypesPaths);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.NoMetatypesFound);
        }

        [Fact]
        public void SCN_10_Scan_excludes_internal_modules_when_requested()
        {
            using ScannerTestWorkspace workspace = CreateSampleQtSdkWorkspace();
            QtTypeScanner scanner = new QtTypeScanner();

            ScanResult result = scanner.Scan(new ScannerConfig(workspace.RootDirectory, ModuleFilter: null, IncludeInternal: false));

            AssertPathsEqual(
                [
                    workspace.GetPath("qml/Qt/labs/platform/plugins.qmltypes"),
                    workspace.GetPath("qml/QtQuick/Controls/plugins.qmltypes"),
                    workspace.GetPath("qml/QtQuick/plugins.qmltypes"),
                ],
                result.QmltypesPaths);
            AssertPathsEqual(
                [
                    workspace.GetPath("qml/Qt/labs/platform/qmldir"),
                    workspace.GetPath("qml/QtQuick/Controls/qmldir"),
                    workspace.GetPath("qml/QtQuick/qmldir"),
                ],
                result.QmldirPaths);
            AssertPathsEqual(
                [
                    workspace.GetPath("lib/metatypes/qtlabsplatform_metatypes.json"),
                    workspace.GetPath("lib/metatypes/qtquick_metatypes.json"),
                ],
                result.MetatypesPaths);
        }

        [Fact]
        public void SCN_10A_Scan_excludes_private_qml_uri_segments_but_not_qml_segments_that_only_contain_private()
        {
            using ScannerTestWorkspace workspace = new ScannerTestWorkspace();
            CreateSdkStructure(workspace);
            _ = workspace.CreateFile("qml/Qt/private/Widgets/plugins.qmltypes");
            _ = workspace.CreateFile("qml/Qt/private/Widgets/qmldir", "module Qt.private.Widgets");
            _ = workspace.CreateFile("qml/QtPrivate/Widgets/plugins.qmltypes");
            _ = workspace.CreateFile("qml/QtPrivate/Widgets/qmldir", "module QtPrivate.Widgets");
            _ = workspace.CreateFile("lib/metatypes/qtprivatewidgets_metatypes.json", "{}");
            _ = workspace.CreateFile("lib/metatypes/qtwidgets_metatypes.json", "{}");

            QtTypeScanner scanner = new QtTypeScanner();

            ScanResult result = scanner.Scan(new ScannerConfig(workspace.RootDirectory, ModuleFilter: null, IncludeInternal: false));

            AssertPathsEqual(
                [workspace.GetPath("qml/QtPrivate/Widgets/plugins.qmltypes")],
                result.QmltypesPaths);
            AssertPathsEqual(
                [workspace.GetPath("qml/QtPrivate/Widgets/qmldir")],
                result.QmldirPaths);
            AssertPathsEqual(
                [workspace.GetPath("lib/metatypes/qtwidgets_metatypes.json")],
                result.MetatypesPaths);
        }

        [Fact]
        public void SCN_11_Infer_module_uri_handles_windows_separators()
        {
            QtTypeScanner scanner = new QtTypeScanner();

            string? moduleUri = scanner.InferModuleUri(@"qml\QtQuick\Controls\qmldir", "qml");

            Assert.Equal("QtQuick.Controls", moduleUri);
        }

        [Fact]
        public void SCN_12_Infer_module_uri_handles_slash_separated_nested_paths()
        {
            QtTypeScanner scanner = new QtTypeScanner();

            string? moduleUri = scanner.InferModuleUri("qml/Qt/labs/platform/qmldir", "qml");

            Assert.Equal("Qt.labs.platform", moduleUri);
        }

        [Fact]
        public void InferModuleUri_handles_absolute_paths_under_qml_root()
        {
            using ScannerTestWorkspace workspace = CreateSampleQtSdkWorkspace();
            QtTypeScanner scanner = new QtTypeScanner();

            string? moduleUri = scanner.InferModuleUri(
                workspace.GetPath("qml/QtQuick/Controls/qmldir"),
                workspace.GetPath("qml"));

            Assert.Equal("QtQuick.Controls", moduleUri);
        }

        [Fact]
        public void InferModuleUri_returns_null_for_non_qmldir_file()
        {
            QtTypeScanner scanner = new QtTypeScanner();

            string? moduleUri = scanner.InferModuleUri("qml/QtQuick/plugins.qmltypes", "qml");

            Assert.Null(moduleUri);
        }

        [Fact]
        public void InferModuleUri_returns_null_for_qmldir_outside_qml_root()
        {
            using ScannerTestWorkspace workspace = CreateSampleQtSdkWorkspace();
            using ScannerTestWorkspace otherWorkspace = new ScannerTestWorkspace();
            _ = otherWorkspace.CreateFile("qml/QtQuick/qmldir");
            QtTypeScanner scanner = new QtTypeScanner();

            string? moduleUri = scanner.InferModuleUri(
                otherWorkspace.GetPath("qml/QtQuick/qmldir"),
                workspace.GetPath("qml"));

            Assert.Null(moduleUri);
        }

        [Fact]
        public void InferModuleUri_returns_null_for_qmldir_at_qml_root()
        {
            QtTypeScanner scanner = new QtTypeScanner();

            string? moduleUri = scanner.InferModuleUri("qml/qmldir", "qml");

            Assert.Null(moduleUri);
        }

        [Fact]
        public void Scan_returns_REG002_when_no_qmltypes_are_found()
        {
            using ScannerTestWorkspace workspace = new ScannerTestWorkspace();
            CreateSdkStructure(workspace);
            _ = workspace.CreateFile("qml/QtQuick/qmldir");
            _ = workspace.CreateFile("lib/metatypes/qtquick_metatypes.json", "{}");

            QtTypeScanner scanner = new QtTypeScanner();
            ScanResult result = scanner.Scan(new ScannerConfig(workspace.RootDirectory, ModuleFilter: null, IncludeInternal: true));

            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.NoQmltypesFound);
        }

        [Fact]
        public void Scan_returns_REG003_when_no_qmldir_files_are_found()
        {
            using ScannerTestWorkspace workspace = new ScannerTestWorkspace();
            CreateSdkStructure(workspace);
            _ = workspace.CreateFile("qml/QtQuick/plugins.qmltypes");
            _ = workspace.CreateFile("lib/metatypes/qtquick_metatypes.json", "{}");

            QtTypeScanner scanner = new QtTypeScanner();
            ScanResult result = scanner.Scan(new ScannerConfig(workspace.RootDirectory, ModuleFilter: null, IncludeInternal: true));

            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.NoQmldirFound);
        }

        [Fact]
        public void Scan_returns_REG004_when_no_metatypes_are_found()
        {
            using ScannerTestWorkspace workspace = new ScannerTestWorkspace();
            CreateSdkStructure(workspace);
            _ = workspace.CreateFile("qml/QtQuick/plugins.qmltypes");
            _ = workspace.CreateFile("qml/QtQuick/qmldir");

            QtTypeScanner scanner = new QtTypeScanner();
            ScanResult result = scanner.Scan(new ScannerConfig(workspace.RootDirectory, ModuleFilter: null, IncludeInternal: true));

            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.NoMetatypesFound);
        }

        [Fact]
        public void Scan_returns_all_missing_file_diagnostics_when_filters_match_no_files()
        {
            using ScannerTestWorkspace workspace = CreateSampleQtSdkWorkspace();
            QtTypeScanner scanner = new QtTypeScanner();

            ScanResult result = scanner.Scan(
                new ScannerConfig(workspace.RootDirectory, ModuleFilter: ["Does.Not.Exist"], IncludeInternal: true));

            Assert.Empty(result.QmltypesPaths);
            Assert.Empty(result.QmldirPaths);
            Assert.Empty(result.MetatypesPaths);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code == DiagnosticCodes.NoQmltypesFound
                && diagnostic.Message.Contains("matched the current scan filters", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code == DiagnosticCodes.NoQmldirFound
                && diagnostic.Message.Contains("matched the current scan filters", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code == DiagnosticCodes.NoMetatypesFound
                && diagnostic.Message.Contains("matched the current scan filters", StringComparison.Ordinal));
        }

        [Fact]
        public void Scan_null_config_returns_REG001_diagnostic_instead_of_throwing()
        {
            QtTypeScanner scanner = new QtTypeScanner();

            ScanResult result = scanner.Scan(null!);

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticCodes.InvalidQtDir, diagnostic.Code);
            Assert.Equal("Scanner configuration is required.", diagnostic.Message);
            Assert.Null(diagnostic.FilePath);
            Assert.Empty(result.QmltypesPaths);
            Assert.Empty(result.QmldirPaths);
            Assert.Empty(result.MetatypesPaths);
        }

        [Fact]
        public void Scan_null_qt_dir_returns_REG001_diagnostic_instead_of_throwing()
        {
            QtTypeScanner scanner = new QtTypeScanner();

            ScanResult result = scanner.Scan(new ScannerConfig(null!, ModuleFilter: null, IncludeInternal: true));

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticCodes.InvalidQtDir, diagnostic.Code);
            Assert.Equal("Qt SDK directory path is required.", diagnostic.Message);
            Assert.Null(diagnostic.FilePath);
            Assert.Empty(result.QmltypesPaths);
            Assert.Empty(result.QmldirPaths);
            Assert.Empty(result.MetatypesPaths);
        }

        private static ScannerTestWorkspace CreateSampleQtSdkWorkspace()
        {
            ScannerTestWorkspace workspace = new ScannerTestWorkspace();
            CreateSdkStructure(workspace);

            _ = workspace.CreateFile("qml/QtQuick/plugins.qmltypes");
            _ = workspace.CreateFile("qml/QtQuick/qmldir", "module QtQuick");
            _ = workspace.CreateFile("qml/QtQuick/Controls/plugins.qmltypes");
            _ = workspace.CreateFile("qml/QtQuick/Controls/qmldir", "module QtQuick.Controls");
            _ = workspace.CreateFile("qml/Qt/labs/platform/plugins.qmltypes");
            _ = workspace.CreateFile("qml/Qt/labs/platform/qmldir", "module Qt.labs.platform");
            _ = workspace.CreateFile("qml/QtQuick/private/Hidden/plugins.qmltypes");
            _ = workspace.CreateFile("qml/QtQuick/private/Hidden/qmldir", "module QtQuick.private.Hidden");

            _ = workspace.CreateFile("lib/metatypes/qtquick_metatypes.json", "{}");
            _ = workspace.CreateFile("lib/metatypes/qtlabsplatform_metatypes.json", "{}");
            _ = workspace.CreateFile("lib/metatypes/qtquickprivate_metatypes.json", "{}");

            return workspace;
        }

        private static void CreateSdkStructure(ScannerTestWorkspace workspace)
        {
            _ = workspace.CreateDirectory("qml");
            _ = workspace.CreateDirectory("lib/metatypes");
        }

        private static void AssertPathsEqual(IEnumerable<string> expectedPaths, ImmutableArray<string> actualPaths)
        {
            string[] expected = expectedPaths
                .Select(Path.GetFullPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected, actualPaths);
        }
    }
}
