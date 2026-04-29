using System.Collections.Immutable;
using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.QmlLint
{
    [Trait("Category", TestCategories.QmlLint)]
    public sealed class QmlLintTests
    {
        [Fact]
        public async Task QL001_LintFile_WithValidQml_ReturnsNoDiagnostics()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, CreateJson(file.Path, success: true, []), string.Empty));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            QmlLintResult result = await linter.LintFileAsync(file.Path);

            Assert.True(result.Success);
            Assert.Equal(0, result.ErrorCount);
            Assert.Equal(0, result.WarningCount);
            Assert.Empty(result.Diagnostics);
            AssertOptionValue(runner.SingleCall.Args, "--json", "-");
        }

        [Fact]
        public async Task QL002_LintFile_WithSyntaxError_ReturnsSyntaxDiagnostic()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(
                1,
                CreateJson(file.Path, success: false, [DiagnosticJson(6, 1, "warning", "Expected token `}'", "syntax")]),
                string.Empty));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            QmlLintResult result = await linter.LintFileAsync(file.Path);

            Assert.False(result.Success);
            QtDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("syntax", diagnostic.Category);
            Assert.Equal(6, diagnostic.Line);
            Assert.Equal(1, diagnostic.Column);
        }

        [Fact]
        public async Task QL003_LintFile_WithTypeError_ReturnsUnresolvedTypeDiagnostic()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nNotARealQtType {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(
                0,
                CreateJson(file.Path, success: false, [DiagnosticJson(3, 1, "warning", "NotARealQtType was not found.", "unresolved-type")]),
                string.Empty));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            QmlLintResult result = await linter.LintFileAsync(file.Path);

            Assert.False(result.Success);
            Assert.Equal(1, result.WarningCount);
            Assert.Equal("unresolved-type", Assert.Single(result.Diagnostics).Category);
        }

        [Fact]
        public async Task QL004_LintString_UsesTemporaryFileAndRewritesDiagnosticsToStringPath()
        {
            MockToolRunner runner = new(static call =>
            {
                string tempPath = call.Args[^1];
                return CreateToolResult(
                    1,
                    CreateJson(tempPath, success: false, [DiagnosticJson(2, 1, "warning", "Expected token `}'", "syntax")]),
                    string.Empty);
            });
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            QmlLintResult result = await linter.LintStringAsync("import QtQuick\nItem {\n");

            string tempPath = runner.SingleCall.Args[^1];
            Assert.False(File.Exists(tempPath));
            Assert.Equal("<string>", Assert.Single(result.Diagnostics).File);
        }

        [Fact]
        public async Task QL005_LintFile_WithMockedJson_ParsesStructuredDiagnostics()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {}\n");
            string json = File.ReadAllText(FixturePath("qt-tools", "mock-qmllint-json.json"));
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(1, json, string.Empty));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            QmlLintResult result = await linter.LintFileAsync(file.Path);

            Assert.Equal(3, result.Diagnostics.Length);
            Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Category == "syntax");
            Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Suggestion == "Qualify the access through an id");
            Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Severity == DiagnosticSeverity.Info && diagnostic.Category == "compiler");
        }

        [Fact]
        public async Task QL006_LintFile_WithWarningLevels_MapsCategoriesToCliArguments()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, CreateJson(file.Path, success: true, []), string.Empty));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            _ = await linter.LintFileAsync(
                file.Path,
                new QmlLintOptions
                {
                    WarningLevels = ImmutableDictionary<QmlLintCategory, DiagnosticSeverity>.Empty
                        .Add(QmlLintCategory.Deprecated, DiagnosticSeverity.Disabled)
                        .Add(QmlLintCategory.UnqualifiedAccess, DiagnosticSeverity.Error),
                });

            AssertOptionValue(runner.SingleCall.Args, "--deprecated", "disable");
            AssertOptionValue(runner.SingleCall.Args, "--unqualified", "error");
        }

        [Fact]
        public async Task QL007_LintFile_WithMaxWarnings_PassesLimitAndCountsWarnings()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(
                0,
                CreateJson(
                    file.Path,
                    success: false,
                    [
                        DiagnosticJson(2, 1, "warning", "first", "unqualified"),
                        DiagnosticJson(3, 1, "warning", "second", "unused-imports"),
                        DiagnosticJson(4, 1, "warning", "third", "deprecated"),
                    ]),
                string.Empty));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            QmlLintResult result = await linter.LintFileAsync(file.Path, new QmlLintOptions { MaxWarnings = 3 });

            AssertOptionValue(runner.SingleCall.Args, "--max-warnings", "3");
            Assert.Equal(3, result.WarningCount);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task QL008_LintFile_WithSilent_PassesSilentAndPreservesEmptyOutput()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            QmlLintResult result = await linter.LintFileAsync(file.Path, new QmlLintOptions { Silent = true });

            Assert.Contains("--silent", runner.SingleCall.Args);
            Assert.Empty(result.ToolResult.Stdout);
            Assert.Empty(result.ToolResult.Stderr);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task QL009_LintFile_WithImportPaths_PassesImportDirectories()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, CreateJson(file.Path, success: true, []), string.Empty));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            _ = await linter.LintFileAsync(
                file.Path,
                new QmlLintOptions { ImportPaths = [@"C:\imports one", @"C:\imports two"] });

            AssertOptionValue(runner.SingleCall.Args, "-I", @"C:\imports one");
            Assert.Contains(@"C:\imports two", runner.SingleCall.Args);
        }

        [Fact]
        public async Task QL010_LintFile_WithBareMode_PassesBare()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(1, CreateJson(file.Path, success: false, [DiagnosticJson(1, 1, "warning", "QtQuick type unavailable", "unresolved-type")]), string.Empty));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            QmlLintResult result = await linter.LintFileAsync(file.Path, new QmlLintOptions { Bare = true });

            Assert.Contains("--bare", runner.SingleCall.Args);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task QL011_LintBatch_ReturnsResultsInInputOrder()
        {
            using TemporaryQmlFile first = TemporaryQmlFile.Create("Item {}\n");
            using TemporaryQmlFile second = TemporaryQmlFile.Create("NotARealQtType {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(
                1,
                $$"""{"files":[{"filename":"{{second.Path.Replace("\\", "\\\\", StringComparison.Ordinal)}}","success":false,"warnings":[{{DiagnosticJson(1, 1, "warning", "bad type", "unresolved-type")}}]},{"filename":"{{first.Path.Replace("\\", "\\\\", StringComparison.Ordinal)}}","success":true,"warnings":[]}],"revision":4}""",
                string.Empty));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            ImmutableArray<QmlLintResult> results = await linter.LintBatchAsync([first.Path, second.Path]);

            Assert.Equal(2, results.Length);
            Assert.True(results[0].Success);
            Assert.False(results[1].Success);
            Assert.Equal([first.Path, second.Path], runner.SingleCall.Args.TakeLast(2).ToArray());
        }

        [Fact]
        public async Task QL012_LintFile_DiagnosticsUseOneBasedLineNumbers()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(1, CreateJson(file.Path, success: false, [DiagnosticJson(12, 5, "warning", "bad", "unqualified")]), string.Empty));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            QmlLintResult result = await linter.LintFileAsync(file.Path);

            QtDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(12, diagnostic.Line);
            Assert.Equal(5, diagnostic.Column);
        }

        [Fact]
        public async Task QL013_LintFile_DiagnosticsIncludeCategories()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(
                1,
                CreateJson(file.Path, success: false, [DiagnosticJson(1, 1, "warning", "bad", "required-property")]),
                string.Empty));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            QmlLintResult result = await linter.LintFileAsync(file.Path);

            Assert.Equal("required-property", Assert.Single(result.Diagnostics).Category);
        }

        [Fact]
        public async Task QL014_LintFile_SummaryContainsDiagnosticCounts()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(
                1,
                CreateJson(
                    file.Path,
                    success: false,
                    [
                        DiagnosticJson(1, 1, "error", "bad error", "syntax"),
                        DiagnosticJson(2, 1, "warning", "bad warning", "unqualified"),
                        DiagnosticJson(3, 1, "info", "bad info", "compiler"),
                    ]),
                string.Empty));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            QmlLintResult result = await linter.LintFileAsync(file.Path);

            Assert.Equal(1, result.ErrorCount);
            Assert.Equal(1, result.WarningCount);
            Assert.Equal(1, result.InfoCount);
            Assert.Equal("1 error, 1 warning, 1 info", result.Summary);
        }

        [Fact]
        public async Task QL015_ListPlugins_ReturnsAvailablePluginNames()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "Available plugins:\nQtDesignStudio    enabled\nQtQuick enabled\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            ImmutableArray<string> plugins = await linter.ListPluginsAsync();

            Assert.Equal(["QtDesignStudio", "QtQuick"], plugins.ToArray());
            Assert.Equal(["--list-plugins"], runner.SingleCall.Args.ToArray());
        }

        [Fact]
        public async Task QL016_LintFile_WithFixMode_PassesFix()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, CreateJson(file.Path, success: true, []), string.Empty));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            QmlLintResult result = await linter.LintFileAsync(file.Path, new QmlLintOptions { Fix = true });

            Assert.Contains("--fix", runner.SingleCall.Args);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task QL017_LintModule_PassesModuleModeAndAggregatesDiagnostics()
        {
            string modulePath = Path.Join(Path.GetTempPath(), "qmlsharp-module-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(modulePath);
            try
            {
                MockToolRunner runner = new();
                runner.Enqueue(CreateToolResult(1, CreateJson(modulePath, success: false, [DiagnosticJson(4, 1, "warning", "bad module", "import-failure")]), string.Empty));
                global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

                QmlLintResult result = await linter.LintModuleAsync(modulePath);

                Assert.Contains("--module", runner.SingleCall.Args);
                Assert.Equal(Path.GetFullPath(modulePath), runner.SingleCall.Args[^1]);
                Assert.False(result.Success);
                Assert.Equal("import-failure", Assert.Single(result.Diagnostics).Category);
            }
            finally
            {
                Directory.Delete(modulePath, recursive: true);
            }
        }

        [Fact]
        public async Task QL018_LintFile_WithJsonDisabled_ParsesStderrFallback()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(1, string.Empty, $"{file.Path}:10:5: error: Unknown property \"foo\""));
            global::QmlSharp.Qt.Tools.QmlLint linter = CreateLinter(runner);

            QmlLintResult result = await linter.LintFileAsync(file.Path, new QmlLintOptions { JsonOutput = false });

            Assert.DoesNotContain("--json", runner.SingleCall.Args);
            QtDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(file.Path, diagnostic.File);
            Assert.Equal(10, diagnostic.Line);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlLint_LintsValidRealFixture()
        {
            global::QmlSharp.Qt.Tools.QmlLint linter = new();

            QmlLintResult result = await linter.LintFileAsync(FixturePath("qml", "valid.qml"));

            Assert.True(result.Success);
            Assert.Empty(result.Diagnostics);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlLint_ReturnsSyntaxDiagnosticsForRealFixture()
        {
            global::QmlSharp.Qt.Tools.QmlLint linter = new();

            QmlLintResult result = await linter.LintFileAsync(FixturePath("qml", "syntax-error.qml"));

            Assert.False(result.Success);
            Assert.NotEmpty(result.Diagnostics);
            Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Category == "syntax");
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlLint_LintsStringAndRewritesDiagnostics()
        {
            global::QmlSharp.Qt.Tools.QmlLint linter = new();

            QmlLintResult result = await linter.LintStringAsync("import QtQuick\nItem {\n");

            Assert.False(result.Success);
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, static diagnostic => Assert.Equal("<string>", diagnostic.File));
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlLint_ListPluginsDoesNotThrow()
        {
            global::QmlSharp.Qt.Tools.QmlLint linter = new();

            ImmutableArray<string> plugins = await linter.ListPluginsAsync();

            Assert.False(plugins.IsDefault);
        }

        private static global::QmlSharp.Qt.Tools.QmlLint CreateLinter(MockToolRunner runner)
        {
            return new global::QmlSharp.Qt.Tools.QmlLint(
                new MockQtToolchain("qmllint", Path.Join(Path.GetTempPath(), "qmllint-test.exe")),
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
                Command = "qmllint",
            };
        }

        private static string CreateJson(string filePath, bool success, ImmutableArray<string> diagnostics)
        {
            return $$"""{"files":[{"filename":"{{EscapeJson(filePath)}}","success":{{success.ToString().ToLowerInvariant()}},"warnings":[{{string.Join(",", diagnostics)}}]}],"revision":4}""";
        }

        private static string DiagnosticJson(int line, int column, string type, string message, string category)
        {
            return $$"""{"line":{{line}},"column":{{column}},"type":"{{type}}","message":"{{EscapeJson(message)}}","category":"{{category}}","suggestions":[]}""";
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
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
            private readonly Queue<ToolResult> _results = [];
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
                    "qmlsharp-qmllint-test-" + Guid.NewGuid().ToString("N") + ".qml");
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
