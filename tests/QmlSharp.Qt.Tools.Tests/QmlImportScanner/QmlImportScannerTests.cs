using System.Collections.Immutable;
using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.QmlImportScanner
{
    [Trait("Category", TestCategories.QmlImportScanner)]
    public sealed class QmlImportScannerTests
    {
        [Fact]
        public async Task IS001_ScanDirectory_UsesRootPathAndParsesImports()
        {
            using TemporaryDirectory directory = TemporaryDirectory.Create();
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, MockImportJson(), string.Empty));
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = CreateScanner(runner);

            QmlImportScanResult result = await scanner.ScanDirectoryAsync(directory.Path);

            Assert.True(result.Success);
            Assert.Contains(result.Imports, static entry => entry.Name == "QtQuick");
            AssertOptionValue(runner.SingleCall.Args, "-rootPath", directory.Path);
        }

        [Fact]
        public async Task IS001B_ScanDirectory_WithRootPathOption_UsesExplicitRoot()
        {
            using TemporaryDirectory directory = TemporaryDirectory.Create();
            using TemporaryDirectory root = TemporaryDirectory.Create();
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "[]", string.Empty));
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = CreateScanner(runner);

            _ = await scanner.ScanDirectoryAsync(
                directory.Path,
                new QmlImportScanOptions { RootPath = root.Path });

            AssertOptionValue(runner.SingleCall.Args, "-rootPath", root.Path);
        }

        [Fact]
        public async Task IS002_ScanFiles_UsesQmlFilesModeAndParsesOnlyToolOutput()
        {
            using TemporaryQmlFile first = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            using TemporaryQmlFile second = TemporaryQmlFile.Create("import QtQuick.Controls\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, MockImportJson(), string.Empty));
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = CreateScanner(runner);

            QmlImportScanResult result = await scanner.ScanFilesAsync([first.Path, second.Path]);

            Assert.True(result.Success);
            Assert.Equal("-qmlFiles", runner.SingleCall.Args[0]);
            Assert.Contains(first.Path, runner.SingleCall.Args);
            Assert.Contains(second.Path, runner.SingleCall.Args);
            Assert.Contains(result.Imports, static entry => entry.Name == "helpers.js");
        }

        [Fact]
        public async Task IS002B_ScanFiles_WithRootPathOption_PassesRootPath()
        {
            using TemporaryDirectory root = TemporaryDirectory.Create();
            string filePath = Path.Join(root.Path, "Main.qml");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "[]", string.Empty));
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = CreateScanner(runner);

            _ = await scanner.ScanFilesAsync(
                [filePath],
                new QmlImportScanOptions { RootPath = root.Path });

            AssertOptionValue(runner.SingleCall.Args, "-rootPath", root.Path);
            Assert.Contains("-qmlFiles", runner.SingleCall.Args);
            Assert.Contains(filePath, runner.SingleCall.Args);
        }

        [Fact]
        public async Task IS003_ScanString_UsesTemporaryInputAndCleansItUp()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, MockImportJson(), string.Empty));
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = CreateScanner(runner);

            QmlImportScanResult result = await scanner.ScanStringAsync("import QtQuick\nItem {}\n");

            string tempInput = runner.SingleCall.Args[1];
            Assert.True(result.Success);
            Assert.Equal("-qmlFiles", runner.SingleCall.Args[0]);
            Assert.EndsWith(".qml", tempInput, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(tempInput));
        }

        [Fact]
        public async Task IS003B_ScanString_WithRootPathOption_PassesRootPathToFileScan()
        {
            using TemporaryDirectory root = TemporaryDirectory.Create();
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "[]", string.Empty));
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = CreateScanner(runner);

            _ = await scanner.ScanStringAsync(
                "import QtQuick\nItem {}\n",
                new QmlImportScanOptions { RootPath = root.Path });

            AssertOptionValue(runner.SingleCall.Args, "-rootPath", root.Path);
            Assert.Contains("-qmlFiles", runner.SingleCall.Args);
        }

        [Fact]
        public async Task IS004_ParseImports_PreservesModuleDirectoryAndJavaScriptTypes()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, MockImportJson(), string.Empty));
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = CreateScanner(runner);

            QmlImportScanResult result = await scanner.ScanFilesAsync([Path.Join(Path.GetTempPath(), "Main.qml")]);

            Assert.Contains(result.Imports, static entry => entry.Type == "module");
            Assert.Contains(result.Imports, static entry => entry.Type == "directory");
            Assert.Contains(result.Imports, static entry => entry.Type == "javascript");
        }

        [Fact]
        public async Task IS005_ParseImports_PreservesResolvedPathAndVersion()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, MockImportJson(), string.Empty));
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = CreateScanner(runner);

            QmlImportScanResult result = await scanner.ScanFilesAsync([Path.Join(Path.GetTempPath(), "Main.qml")]);

            QmlImportEntry qtQuick = Assert.Single(result.Imports, static entry => entry.Name == "QtQuick");
            Assert.Equal("C:/Qt/6.11.0/msvc2022_64/qml/QtQuick", qtQuick.Path);
            Assert.Equal("6.11", qtQuick.Version);
        }

        [Fact]
        public async Task IS006_ScanDirectory_WithExcludeDirs_PassesExcludeArguments()
        {
            using TemporaryDirectory directory = TemporaryDirectory.Create();
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "[]", string.Empty));
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = CreateScanner(runner);

            _ = await scanner.ScanDirectoryAsync(
                directory.Path,
                new QmlImportScanOptions { ExcludeDirs = ["ignored", "generated"] });

            AssertOptionValue(runner.SingleCall.Args, "-exclude", "ignored");
            Assert.Contains("generated", runner.SingleCall.Args);
        }

        [Fact]
        public async Task ScanDirectory_WithImportPaths_PassesToolchainThenOptionImports()
        {
            using TemporaryDirectory directory = TemporaryDirectory.Create();
            string toolchainImport = Path.Join(Path.GetTempPath(), "toolchain-import");
            string optionImport = Path.Join(Path.GetTempPath(), "option-import");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "[]", string.Empty));
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = CreateScanner(runner, [toolchainImport]);

            _ = await scanner.ScanDirectoryAsync(
                directory.Path,
                new QmlImportScanOptions { ImportPaths = [optionImport] });

            AssertOptionValue(runner.SingleCall.Args, "-importPath", toolchainImport);
            Assert.Contains(optionImport, runner.SingleCall.Args);
        }

        [Fact]
        public async Task ScanFiles_WithMalformedJson_ReturnsEmptyImportsWithoutThrowing()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "{not json", string.Empty));
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = CreateScanner(runner);

            QmlImportScanResult result = await scanner.ScanFilesAsync([Path.Join(Path.GetTempPath(), "Main.qml")]);

            Assert.True(result.Success);
            Assert.Empty(result.Imports);
        }

        [Fact]
        public async Task ScanFiles_WithNonZeroExit_DoesNotParseStdoutImports()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(1, MockImportJson(), "scanner failed"));
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = CreateScanner(runner);

            QmlImportScanResult result = await scanner.ScanFilesAsync([Path.Join(Path.GetTempPath(), "Main.qml")]);

            Assert.False(result.Success);
            Assert.Empty(result.Imports);
        }

        [Fact]
        public async Task ScanFiles_WithEmptyInput_ReturnsSuccessWithoutToolLookup()
        {
            MockToolRunner runner = new();
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = new(new ThrowingQtToolchain(), runner);

            QmlImportScanResult result = await scanner.ScanFilesAsync([]);

            Assert.True(result.Success);
            Assert.Empty(result.Imports);
            Assert.Empty(runner.RecordedCalls);
        }

        [RequiresQtFact("qmlimportscanner")]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlImportScanner_ScansValidRealFixture()
        {
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = new();

            QmlImportScanResult result = await scanner.ScanFilesAsync([FixturePath("qml", "valid.qml")]);

            Assert.True(result.Success);
            Assert.Contains(result.Imports, static entry => entry.Name == "QtQuick");
        }

        [RequiresQtFact("qmlimportscanner")]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlImportScanner_ScansDirectory()
        {
            using TemporaryDirectory directory = TemporaryDirectory.Create();
            string mainFile = Path.Join(directory.Path, "Main.qml");
            File.WriteAllText(mainFile, "import QtQuick\nItem {}\n");
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = new();

            QmlImportScanResult result = await scanner.ScanDirectoryAsync(directory.Path);

            Assert.True(result.Success);
            Assert.Contains(result.Imports, static entry => entry.Name == "QtQuick");
        }

        [RequiresQtFact("qmlimportscanner")]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlImportScanner_ScansString()
        {
            global::QmlSharp.Qt.Tools.QmlImportScanner scanner = new();

            QmlImportScanResult result = await scanner.ScanStringAsync(
                "import QtQuick\nimport QtQuick.Controls\nItem {}\n");

            Assert.True(result.Success);
            Assert.Contains(result.Imports, static entry => entry.Name == "QtQuick.Controls");
        }

        private static global::QmlSharp.Qt.Tools.QmlImportScanner CreateScanner(
            MockToolRunner runner,
            ImmutableArray<string> importPaths = default)
        {
            return new global::QmlSharp.Qt.Tools.QmlImportScanner(
                new MockQtToolchain("qmlimportscanner", Path.Join(Path.GetTempPath(), "qmlimportscanner-test.exe"), importPaths),
                runner);
        }

        private static ToolResult CreateToolResult(int exitCode, string stdout, string stderr)
        {
            return new ToolResult
            {
                ExitCode = exitCode,
                Stdout = stdout,
                Stderr = stderr,
                DurationMs = 1,
                Command = "qmlimportscanner",
            };
        }

        private static string MockImportJson()
        {
            return """
                [
                  {
                    "name": "QtQuick",
                    "type": "module",
                    "version": "6.11",
                    "path": "C:/Qt/6.11.0/msvc2022_64/qml/QtQuick"
                  },
                  {
                    "name": "./module",
                    "type": "directory",
                    "path": "tests/fixtures/qml/module"
                  },
                  {
                    "name": "helpers.js",
                    "type": "javascript",
                    "path": "tests/fixtures/qml/helpers.js"
                  }
                ]
                """;
        }

        private static void AssertOptionValue(ImmutableArray<string> args, string option, string expectedValue)
        {
            int optionIndex = args.IndexOf(option);
            Assert.True(optionIndex >= 0, $"Expected argument '{option}' was not found.");
            Assert.True(optionIndex + 1 < args.Length, $"Expected argument '{option}' to have a value.");
            Assert.Equal(expectedValue, args[optionIndex + 1]);
        }

        private static string FixturePath(string group, string fileName)
        {
            return Path.Join(AppContext.BaseDirectory, "Fixtures", group, fileName);
        }

        private sealed class MockQtToolchain : IQtToolchain
        {
            private readonly ToolInfo _toolInfo;

            public MockQtToolchain(string toolName, string toolPath, ImmutableArray<string> importPaths = default)
            {
                _toolInfo = new ToolInfo
                {
                    Name = toolName,
                    Path = toolPath,
                    Available = true,
                    Version = "6.11.0",
                };
                Installation = new QtInstallation
                {
                    RootDir = Path.GetTempPath(),
                    BinDir = Path.GetTempPath(),
                    QmlDir = Path.GetTempPath(),
                    LibDir = Path.GetTempPath(),
                    Platform = OperatingSystem.IsWindows() ? "windows" : "linux",
                    Version = new QtVersion { Major = 6, Minor = 11, Patch = 0 },
                    ImportPaths = importPaths.IsDefault ? [] : importPaths,
                };
            }

            public QtInstallation? Installation { get; }

            public Task<QtInstallation> DiscoverAsync(QtToolchainConfig? config = null, CancellationToken ct = default)
            {
                return Task.FromResult(Installation!);
            }

            public Task<ToolAvailability> CheckToolsAsync(CancellationToken ct = default)
            {
                return Task.FromResult(new ToolAvailability
                {
                    QmlFormat = _toolInfo,
                    QmlLint = _toolInfo,
                    QmlCachegen = _toolInfo,
                    Qmltc = _toolInfo,
                    QmlImportScanner = _toolInfo,
                    QmlDom = _toolInfo,
                    Qml = _toolInfo,
                    Rcc = _toolInfo,
                    QmlTypeRegistrar = _toolInfo,
                    Moc = _toolInfo,
                    QmlAotStats = _toolInfo,
                });
            }

            public Task<ToolInfo> GetToolInfoAsync(string toolName, CancellationToken ct = default)
            {
                Assert.Equal(_toolInfo.Name, toolName);
                return Task.FromResult(_toolInfo);
            }
        }

        private sealed class ThrowingQtToolchain : IQtToolchain
        {
            public QtInstallation? Installation => null;

            public Task<QtInstallation> DiscoverAsync(QtToolchainConfig? config = null, CancellationToken ct = default)
            {
                throw new InvalidOperationException("Toolchain discovery should not be called.");
            }

            public Task<ToolAvailability> CheckToolsAsync(CancellationToken ct = default)
            {
                throw new InvalidOperationException("Toolchain availability should not be checked.");
            }

            public Task<ToolInfo> GetToolInfoAsync(string toolName, CancellationToken ct = default)
            {
                throw new InvalidOperationException("Tool lookup should not be called.");
            }
        }

        private sealed class MockToolRunner : IToolRunner
        {
            private readonly Queue<ToolResult> _results = [];

            public List<RecordedCall> RecordedCalls { get; } = [];

            public RecordedCall SingleCall => Assert.Single(RecordedCalls);

            public void Enqueue(ToolResult result)
            {
                _results.Enqueue(result);
            }

            public Task<ToolResult> RunAsync(
                string toolPath,
                ImmutableArray<string> args,
                ToolRunnerOptions? options = null,
                CancellationToken ct = default)
            {
                RecordedCall call = new(toolPath, args, options);
                RecordedCalls.Add(call);
                return Task.FromResult(_results.Dequeue());
            }
        }

        private sealed record RecordedCall(
            string ToolPath,
            ImmutableArray<string> Args,
            ToolRunnerOptions? Options);

        private sealed class TemporaryQmlFile : IDisposable
        {
            private TemporaryQmlFile(string path)
            {
                Path = path;
            }

            public string Path { get; }

            public static TemporaryQmlFile Create(string contents)
            {
                string path = System.IO.Path.Join(
                    System.IO.Path.GetTempPath(),
                    "qmlsharp-qmlimportscanner-test-" + Guid.NewGuid().ToString("N") + ".qml");
                File.WriteAllText(path, contents);
                return new TemporaryQmlFile(path);
            }

            public void Dispose()
            {
                TryDeleteFile(Path);
            }
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            private TemporaryDirectory(string path)
            {
                Path = path;
            }

            public string Path { get; }

            public static TemporaryDirectory Create()
            {
                string path = System.IO.Path.Join(
                    System.IO.Path.GetTempPath(),
                    "qmlsharp-qmlimportscanner-dir-test-" + Guid.NewGuid().ToString("N"));
                _ = Directory.CreateDirectory(path);
                return new TemporaryDirectory(path);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Path))
                    {
                        Directory.Delete(Path, recursive: true);
                    }
                }
                catch (IOException ex)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"Failed to delete temporary directory '{Path}' due to I/O error: {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"Failed to delete temporary directory '{Path}' due to access error: {ex.Message}");
                }
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"Failed to delete temporary QML file '{path}' due to I/O error: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"Failed to delete temporary QML file '{path}' due to access error: {ex.Message}");
            }
        }
    }
}
