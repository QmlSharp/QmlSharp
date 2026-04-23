using QmlSharp.Registry.Scanning;
using QmlSharp.Registry.Tests.Helpers;

namespace QmlSharp.Registry.Tests.Scanning
{
    [Collection(QtEnvironmentCollection.Name)]
    public sealed class QtTypeScannerIntegrationTests
    {
        [SkipUnlessEnvironmentVariableFact(RegistryTestEnvironment.QtDirVariableName, RegistryTestEnvironment.QtSdkUnavailableReason)]
        public void SCN_01_Scan_valid_qt_directory_returns_non_empty_lists_for_all_file_types()
        {
            QtTypeScanner scanner = new QtTypeScanner();
            string qtDir = Environment.GetEnvironmentVariable(RegistryTestEnvironment.QtDirVariableName)!;

            ScanResult result = scanner.Scan(new ScannerConfig(qtDir, ModuleFilter: null, IncludeInternal: true));

            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == QmlSharp.Registry.Diagnostics.DiagnosticSeverity.Error);
            Assert.NotEmpty(result.QmltypesPaths);
            Assert.NotEmpty(result.QmldirPaths);
            Assert.NotEmpty(result.MetatypesPaths);
        }

        [SkipUnlessEnvironmentVariableFact(RegistryTestEnvironment.QtDirVariableName, RegistryTestEnvironment.QtSdkUnavailableReason)]
        public void SCN_04_Validate_valid_qt_directory_returns_detected_qt_version()
        {
            QtTypeScanner scanner = new QtTypeScanner();
            string qtDir = Environment.GetEnvironmentVariable(RegistryTestEnvironment.QtDirVariableName)!;
            string expectedVersion = GetExpectedQtVersionFromPath(qtDir);

            ScanValidation validation = scanner.ValidateQtDir(qtDir);

            Assert.True(validation.IsValid);
            Assert.Equal(expectedVersion, validation.QtVersion);
            Assert.NotEqual("unknown", validation.QtVersion);
            Assert.Null(validation.ErrorMessage);
        }

        [SkipUnlessEnvironmentVariableFact(RegistryTestEnvironment.QtDirVariableName, RegistryTestEnvironment.QtSdkUnavailableReason)]
        public void SCN_09B_Scan_qtquick_filter_includes_real_qt6_metatypes()
        {
            QtTypeScanner scanner = new QtTypeScanner();
            string qtDir = Environment.GetEnvironmentVariable(RegistryTestEnvironment.QtDirVariableName)!;

            ScanResult result = scanner.Scan(new ScannerConfig(qtDir, ModuleFilter: ["QtQuick"], IncludeInternal: false));

            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == QmlSharp.Registry.Diagnostics.DiagnosticSeverity.Error);
            Assert.Contains(result.MetatypesPaths, path => string.Equals(Path.GetFileName(path), "qt6quick_metatypes.json", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.MetatypesPaths, path => string.Equals(Path.GetFileName(path), "qt6quickcontrols2_metatypes.json", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(result.MetatypesPaths, path => string.Equals(Path.GetFileName(path), "qt6widgets_metatypes.json", StringComparison.OrdinalIgnoreCase));
        }

        private static string GetExpectedQtVersionFromPath(string qtDir)
        {
            string[] segments = qtDir.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

            for (int index = segments.Length - 1; index >= 0; index--)
            {
                if (Version.TryParse(segments[index], out _))
                {
                    return segments[index];
                }
            }

            throw new InvalidOperationException($"QT_DIR '{qtDir}' does not contain a parseable version segment.");
        }
    }
}
