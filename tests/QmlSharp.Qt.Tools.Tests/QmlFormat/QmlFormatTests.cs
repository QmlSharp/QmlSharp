using System.Collections.Immutable;
using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.QmlFormat
{
    [Trait("Category", TestCategories.QmlFormat)]
    public sealed class QmlFormatTests
    {
        [Fact]
        public async Task QF001_FormatFile_WithDefaultOptions_ReturnsFormattedSourceAndChangeStatus()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem{width:100}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "import QtQuick\n\nItem {\n    width: 100\n}\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            QmlFormatResult result = await formatter.FormatFileAsync(file.Path);

            Assert.True(result.Success);
            Assert.Equal("import QtQuick\n\nItem {\n    width: 100\n}\n", result.FormattedSource);
            Assert.True(result.HasChanges);
            Assert.Equal([file.Path], runner.SingleCall.Args.Where(static arg => arg.EndsWith(".qml", StringComparison.OrdinalIgnoreCase)).ToArray());
        }

        [Fact]
        public async Task QF002_FormatFile_WithSyntaxError_ReturnsErrorDiagnostics()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(1, string.Empty, $"{file.Path}:2:1: error: Expected token \"}}\""));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            QmlFormatResult result = await formatter.FormatFileAsync(file.Path);

            Assert.False(result.Success);
            QtDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(file.Path, diagnostic.File);
            Assert.Equal(2, diagnostic.Line);
            Assert.Null(result.FormattedSource);
        }

        [Fact]
        public async Task QF003_FormatString_UsesTemporaryFileAndCleansItUp()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "import QtQuick\n\nItem {}\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            QmlFormatResult result = await formatter.FormatStringAsync("import QtQuick\nItem {}\n");

            string tempPath = runner.SingleCall.Args[^1];
            Assert.True(result.Success);
            Assert.Equal("import QtQuick\n\nItem {}\n", result.FormattedSource);
            Assert.False(File.Exists(tempPath));
        }

        [Fact]
        public async Task FormatString_WhenToolFails_CleansTemporaryFile()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(2, string.Empty, "qmlformat failed"));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            QmlFormatResult result = await formatter.FormatStringAsync("Item {");

            string tempPath = runner.SingleCall.Args[^1];
            Assert.False(result.Success);
            Assert.False(File.Exists(tempPath));
        }

        [Fact]
        public async Task FormatString_WhenToolTimesOut_CleansTemporaryFile()
        {
            MockToolRunner runner = new();
            runner.Enqueue(new QtToolTimeoutError("qmlformat", TimeSpan.FromSeconds(1), string.Empty, "timeout"));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            _ = await Assert.ThrowsAsync<QtToolTimeoutError>(() => formatter.FormatStringAsync("Item {}"));

            string tempPath = runner.SingleCall.Args[^1];
            Assert.False(File.Exists(tempPath));
        }

        [Fact]
        public async Task FormatString_WhenCanceled_CleansTemporaryFile()
        {
            MockToolRunner runner = new();
            using CancellationTokenSource cts = new();
            runner.Enqueue(new OperationCanceledException(cts.Token));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                formatter.FormatStringAsync("Item {}", ct: cts.Token));

            string tempPath = runner.SingleCall.Args[^1];
            Assert.False(File.Exists(tempPath));
        }

        [Fact]
        public async Task QF004_FormatString_WithIndentWidthTwo_PassesIndentWidthArgument()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "Item {\n  width: 100\n}\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            QmlFormatResult result = await formatter.FormatStringAsync(
                "Item{width:100}",
                new QmlFormatOptions { IndentWidth = 2 });

            Assert.Contains("--indent-width", runner.SingleCall.Args);
            Assert.Contains("2", runner.SingleCall.Args);
            Assert.Contains("  width", result.FormattedSource, StringComparison.Ordinal);
        }

        [Fact]
        public async Task QF005_FormatString_WithTabs_PassesTabsArgument()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "Item {\n\twidth: 100\n}\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            QmlFormatResult result = await formatter.FormatStringAsync(
                "Item{width:100}",
                new QmlFormatOptions { UseTabs = true });

            Assert.Contains("--tabs", runner.SingleCall.Args);
            Assert.Contains("\twidth", result.FormattedSource, StringComparison.Ordinal);
        }

        [Fact]
        public async Task QF006_FormatString_WithNormalize_PassesNormalizeArgument()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "Rectangle {\n    id: root\n    width: 100\n}\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            QmlFormatResult result = await formatter.FormatStringAsync(
                "Rectangle { width: 100; id: root }",
                new QmlFormatOptions { Normalize = true });

            Assert.Contains("--normalize", runner.SingleCall.Args);
            Assert.True(result.FormattedSource!.IndexOf("id:", StringComparison.Ordinal) < result.FormattedSource.IndexOf("width:", StringComparison.Ordinal));
        }

        [Fact]
        public async Task QF007_FormatString_WithSortImports_PassesSortImportsArgument()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "import QtQuick\nimport QtQuick.Controls\n\nItem {}\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            QmlFormatResult result = await formatter.FormatStringAsync(
                "import QtQuick.Controls\nimport QtQuick\nItem {}\n",
                new QmlFormatOptions { SortImports = true });

            Assert.Contains("--sort-imports", runner.SingleCall.Args);
            Assert.True(result.FormattedSource!.IndexOf("import QtQuick\n", StringComparison.Ordinal) < result.FormattedSource.IndexOf("import QtQuick.Controls", StringComparison.Ordinal));
        }

        [Fact]
        public async Task QF008_FormatString_WithSemicolonRuleAdd_MapsToQtAlwaysRule()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "Item {\n    width: 100;\n}\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            QmlFormatResult result = await formatter.FormatStringAsync(
                "Item{width:100}",
                new QmlFormatOptions { SemicolonRule = "add" });

            AssertOptionValue(runner.SingleCall.Args, "--semicolon-rule", "always");
            Assert.Contains("width: 100", result.FormattedSource, StringComparison.Ordinal);
        }

        [Fact]
        public async Task QF009_FormatString_WithSemicolonRuleRemove_MapsToQtEssentialRule()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "Item {\n    width: 100\n}\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            QmlFormatResult result = await formatter.FormatStringAsync(
                "Item{width:100;}",
                new QmlFormatOptions { SemicolonRule = "remove" });

            AssertOptionValue(runner.SingleCall.Args, "--semicolon-rule", "essential");
            Assert.DoesNotContain("100;", result.FormattedSource, StringComparison.Ordinal);
        }

        [Fact]
        public async Task QF010_FormatString_WithUnixNewline_PassesUnixNewlineArgument()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "Item {}\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            QmlFormatResult result = await formatter.FormatStringAsync(
                "Item {}\r\n",
                new QmlFormatOptions { Newline = "unix" });

            AssertOptionValue(runner.SingleCall.Args, "--newline", "unix");
            Assert.DoesNotContain("\r\n", result.FormattedSource, StringComparison.Ordinal);
        }

        [Fact]
        public async Task QF011_FormatString_WithColumnWidth_PassesColumnWidthArgument()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "Item {\n    property string value: \"long\"\n}\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            _ = await formatter.FormatStringAsync(
                "Item{property string value:\"long\"}",
                new QmlFormatOptions { ColumnWidth = 80 });

            AssertOptionValue(runner.SingleCall.Args, "--column-width", "80");
        }

        [Fact]
        public async Task QF012_FormatFile_WithIgnoreSettings_PassesIgnoreSettingsArgument()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item{width:100}");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "Item {\n    width: 100\n}\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            _ = await formatter.FormatFileAsync(file.Path, new QmlFormatOptions { IgnoreSettings = true });

            Assert.Contains("--ignore-settings", runner.SingleCall.Args);
        }

        [Fact]
        public async Task FormatFile_WhenPathContainsSpaces_PassesPathAsSingleArgument()
        {
            string directory = Path.Join(
                Path.GetTempPath(),
                "qmlsharp qmlformat path " + Guid.NewGuid().ToString("N"));
            string filePath = Path.Join(directory, "file with spaces.qml");
            _ = Directory.CreateDirectory(directory);
            File.WriteAllText(filePath, "Item{width:100}");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "Item {\n    width: 100\n}\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            try
            {
                QmlFormatResult result = await formatter.FormatFileAsync(filePath);

                Assert.True(result.Success);
                Assert.Equal(Path.GetFullPath(filePath), runner.SingleCall.Args[^1]);
                Assert.Contains(" ", runner.SingleCall.Args[^1], StringComparison.Ordinal);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }

        [Fact]
        public async Task QF013_FormatBatch_ReturnsResultsInInputOrder()
        {
            using TemporaryQmlFile first = TemporaryQmlFile.Create("Item{width:100}");
            using TemporaryQmlFile second = TemporaryQmlFile.Create("Item{height:100}");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "Item {\n    width: 100\n}\n", string.Empty));
            runner.Enqueue(CreateToolResult(1, string.Empty, "second.qml:1:1: error: bad"));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            ImmutableArray<QmlFormatResult> results = await formatter.FormatBatchAsync([first.Path, second.Path]);

            Assert.Equal(2, results.Length);
            Assert.True(results[0].Success);
            Assert.False(results[1].Success);
            Assert.Equal(first.Path, runner.RecordedCalls[0].Args[^1]);
            Assert.Equal(second.Path, runner.RecordedCalls[1].Args[^1]);
        }

        [Fact]
        public async Task QF014_FormatFile_WhenAlreadyFormatted_ReportsNoChanges()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {\n    width: 100\n}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "Item {\r\n    width: 100\r\n}\r\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            QmlFormatResult result = await formatter.FormatFileAsync(file.Path);

            Assert.True(result.Success);
            Assert.False(result.HasChanges);
        }

        [Fact]
        public async Task QF015_FormatFile_WithNoOptions_UsesDefaultOptionsWithoutOptionalCliFlags()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "Item {}\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            _ = await formatter.FormatFileAsync(file.Path);

            Assert.Equal([file.Path], runner.SingleCall.Args.ToArray());
        }

        [Fact]
        public async Task QF016_FormatFile_WithForce_PassesForceAndCanReturnSuccess()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "Item {}\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            QmlFormatResult result = await formatter.FormatFileAsync(file.Path, new QmlFormatOptions { Force = true });

            Assert.Contains("--force", runner.SingleCall.Args);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task FormatFile_WithInPlace_ReadsPostFormatFileContent()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item{width:100}");
            MockToolRunner runner = new(static call =>
            {
                File.WriteAllText(call.Args[^1], "Item {\n    width: 100\n}\n");
                return CreateToolResult(0, string.Empty, string.Empty);
            });
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            QmlFormatResult result = await formatter.FormatFileAsync(file.Path, new QmlFormatOptions { InPlace = true });

            Assert.Contains("--inplace", runner.SingleCall.Args);
            Assert.Equal(File.ReadAllText(file.Path), result.FormattedSource);
            Assert.True(result.HasChanges);
        }

        [Fact]
        public async Task FormatString_NonZeroExitWithoutParseableStderr_ReturnsFallbackDiagnosticWithStringPath()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(2, string.Empty, "qmlformat parse failed"));
            global::QmlSharp.Qt.Tools.QmlFormat formatter = CreateFormatter(runner);

            QmlFormatResult result = await formatter.FormatStringAsync("Item {");

            QtDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("<string>", diagnostic.File);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("qmlformat parse failed", diagnostic.Message);
        }

        [RequiresQtFact("qmlformat")]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlFormat_FormatsRealFixture()
        {
            global::QmlSharp.Qt.Tools.QmlFormat formatter = new();
            string fixture = FixturePath("needs-formatting.qml");

            QmlFormatResult result = await formatter.FormatFileAsync(fixture);

            Assert.True(result.Success);
            Assert.NotNull(result.FormattedSource);
            Assert.Contains("width: 100", result.FormattedSource, StringComparison.Ordinal);
            Assert.True(result.HasChanges);
        }

        [RequiresQtFact("qmlformat")]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlFormat_ReturnsDiagnosticsForSyntaxError()
        {
            global::QmlSharp.Qt.Tools.QmlFormat formatter = new();
            string fixture = FixturePath("syntax-error.qml");

            QmlFormatResult result = await formatter.FormatFileAsync(fixture);

            Assert.False(result.Success);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, static diagnostic => Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity));
        }

        [RequiresQtFact("qmlformat")]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlFormat_FormatsStringAndCleansTempInput()
        {
            global::QmlSharp.Qt.Tools.QmlFormat formatter = new();

            QmlFormatResult result = await formatter.FormatStringAsync("import QtQuick\nItem{width:100}\n");

            Assert.True(result.Success);
            Assert.NotNull(result.FormattedSource);
            Assert.Contains("Item {", result.FormattedSource, StringComparison.Ordinal);
            Assert.True(result.HasChanges);
        }

        private static global::QmlSharp.Qt.Tools.QmlFormat CreateFormatter(MockToolRunner runner)
        {
            return new global::QmlSharp.Qt.Tools.QmlFormat(
                new MockQtToolchain("qmlformat", Path.Join(Path.GetTempPath(), "qmlformat-test.exe")),
                runner,
                new QtDiagnosticParser());
        }

        private static ToolResult CreateToolResult(int exitCode, string stdout, string stderr)
        {
            return new ToolResult
            {
                ExitCode = exitCode,
                Stdout = stdout,
                Stderr = stderr,
                DurationMs = 1,
                Command = "qmlformat",
            };
        }

        private static void AssertOptionValue(ImmutableArray<string> args, string option, string expectedValue)
        {
            int optionIndex = args.IndexOf(option);
            Assert.True(optionIndex >= 0, $"Expected argument '{option}' was not found.");
            Assert.True(optionIndex + 1 < args.Length, $"Expected argument '{option}' to have a value.");
            Assert.Equal(expectedValue, args[optionIndex + 1]);
        }

        private static string FixturePath(string fileName)
        {
            return Path.Join(AppContext.BaseDirectory, "Fixtures", "qml", fileName);
        }

        private sealed class MockQtToolchain : IQtToolchain
        {
            private readonly ToolInfo _toolInfo;

            public MockQtToolchain(string toolName, string toolPath)
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

        private sealed class MockToolRunner : IToolRunner
        {
            private readonly Queue<object> _results = [];
            private readonly Func<RecordedCall, ToolResult>? _callback;

            public MockToolRunner()
            {
            }

            public MockToolRunner(Func<RecordedCall, ToolResult> callback)
            {
                _callback = callback;
            }

            public List<RecordedCall> RecordedCalls { get; } = [];

            public RecordedCall SingleCall => Assert.Single(RecordedCalls);

            public void Enqueue(ToolResult result)
            {
                _results.Enqueue(result);
            }

            public void Enqueue(Exception exception)
            {
                _results.Enqueue(exception);
            }

            public Task<ToolResult> RunAsync(
                string toolPath,
                ImmutableArray<string> args,
                ToolRunnerOptions? options = null,
                CancellationToken ct = default)
            {
                RecordedCall call = new(toolPath, args, options);
                RecordedCalls.Add(call);

                if (_callback is not null)
                {
                    return Task.FromResult(_callback(call));
                }

                object result = _results.Dequeue();
                if (result is Exception exception)
                {
                    return Task.FromException<ToolResult>(exception);
                }

                return Task.FromResult((ToolResult)result);
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
                    "qmlsharp-qmlformat-test-" + Guid.NewGuid().ToString("N") + ".qml");
                File.WriteAllText(path, contents);
                return new TemporaryQmlFile(path);
            }

            public void Dispose()
            {
                try
                {
                    if (File.Exists(Path))
                    {
                        File.Delete(Path);
                    }
                }
                catch (IOException ex)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"Failed to delete temporary QML file '{Path}' due to I/O error: {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"Failed to delete temporary QML file '{Path}' due to access error: {ex.Message}");
                }
            }
        }
    }
}
