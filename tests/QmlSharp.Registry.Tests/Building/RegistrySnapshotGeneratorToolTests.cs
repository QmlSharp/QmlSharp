using QmlSharp.Tools.GenerateRegistrySnapshot;

namespace QmlSharp.Registry.Tests.Building
{
    public sealed class RegistrySnapshotGeneratorToolTests
    {
        [Fact]
        public void Run_with_fixture_qt_dir_writes_snapshot_and_prints_summary_metadata()
        {
            using TemporaryDirectory temporaryDirectory = new();
            string outputPath = Path.Join(temporaryDirectory.Path, "qt-6.11.0-registry.snapshot.bin");
            StringWriter standardOutput = new();
            StringWriter standardError = new();

            int exitCode = RegistrySnapshotGeneratorCommand.Run(
                ["--qt-dir", GetParityQtDir(), "--output", outputPath],
                standardOutput,
                standardError);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));
            Assert.Equal(string.Empty, NormalizeLineEndings(standardError.ToString()));

            string summary = NormalizeLineEndings(standardOutput.ToString());
            Assert.Contains("Qt version: 6.11.0", summary, StringComparison.Ordinal);
            Assert.Contains("Registry format version: 1", summary, StringComparison.Ordinal);
            Assert.Contains("Module count: 2", summary, StringComparison.Ordinal);
            Assert.Contains("Type count: 11", summary, StringComparison.Ordinal);
            Assert.Contains("Warning count: 0", summary, StringComparison.Ordinal);
            Assert.Contains("Error count: 0", summary, StringComparison.Ordinal);

            BuildResult loadResult = new RegistryBuilder().LoadFromSnapshot(outputPath);
            Assert.True(loadResult.IsSuccess, FormatDiagnostics(loadResult.Diagnostics));
            Assert.NotNull(loadResult.TypeRegistry);
            Assert.Equal(2, loadResult.TypeRegistry!.Modules.Count);
            Assert.Equal(11, loadResult.TypeRegistry.Types.Count);
        }

        [Fact]
        public void Run_with_module_filter_writes_a_filtered_snapshot()
        {
            using TemporaryDirectory temporaryDirectory = new();
            string outputPath = Path.Join(temporaryDirectory.Path, "qtquick-controls-only.snapshot.bin");
            StringWriter standardOutput = new();
            StringWriter standardError = new();

            int exitCode = RegistrySnapshotGeneratorCommand.Run(
                ["--qt-dir", GetParityQtDir(), "--output", outputPath, "--module-filter", "QtQuick.Controls"],
                standardOutput,
                standardError);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));
            Assert.Equal(string.Empty, NormalizeLineEndings(standardError.ToString()));

            string summary = NormalizeLineEndings(standardOutput.ToString());
            Assert.Contains("Module count: 1", summary, StringComparison.Ordinal);
            Assert.Contains("Type count: 3", summary, StringComparison.Ordinal);

            BuildResult loadResult = new RegistryBuilder().LoadFromSnapshot(outputPath);
            Assert.True(loadResult.IsSuccess, FormatDiagnostics(loadResult.Diagnostics));
            Assert.NotNull(loadResult.TypeRegistry);
            Assert.NotNull(loadResult.Query);
            Assert.Equal(["QtQuick.Controls"], loadResult.TypeRegistry!.Modules.Select(module => module.Uri).ToArray());
            Assert.Equal(3, loadResult.TypeRegistry.Types.Count);
            Assert.Null(loadResult.Query!.FindTypeByQmlName("QtQuick", "Item"));
            Assert.NotNull(loadResult.Query.FindTypeByQmlName("QtQuick.Controls", "Button"));
        }

        private static string FormatDiagnostics(IEnumerable<QmlSharp.Registry.Diagnostics.RegistryDiagnostic> diagnostics)
        {
            return string.Join(
                Environment.NewLine,
                diagnostics.Select(diagnostic => $"{diagnostic.Severity}: {diagnostic.Code}: {diagnostic.Message}"));
        }

        private static string GetParityQtDir()
        {
            return Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "fixtures", "parity", "qt-sdk"));
        }

        private static string NormalizeLineEndings(string value)
        {
            return value.Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Join(System.IO.Path.GetTempPath(), $"qmlsharp-registry-tool-{Guid.NewGuid():N}");
                _ = Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Path))
                    {
                        Directory.Delete(Path, recursive: true);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
