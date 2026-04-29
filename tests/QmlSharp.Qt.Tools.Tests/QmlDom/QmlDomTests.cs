using System.Collections.Immutable;
using System.Text.Json;
using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.QmlDom
{
    [Trait("Category", TestCategories.QmlDom)]
    public sealed class QmlDomTests
    {
        [Fact]
        public async Task QD001_DumpFile_WithDomMode_CapturesValidJson()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, """{"currentItem":{"isValid":true}}""", string.Empty));
            global::QmlSharp.Qt.Tools.QmlDom dom = CreateDom(runner);

            QmlDomResult result = await dom.DumpFileAsync(file.Path);

            Assert.True(result.Success);
            Assert.Equal("""{"currentItem":{"isValid":true}}""", result.JsonOutput);
            Assert.Equal("-d", runner.SingleCall.Args[0]);
            Assert.Equal(file.Path, runner.SingleCall.Args[^1]);
            using JsonDocument document = JsonDocument.Parse(result.JsonOutput!);
            Assert.True(document.RootElement.GetProperty("currentItem").GetProperty("isValid").GetBoolean());
        }

        [Fact]
        public async Task QD002_DumpFile_WithAstMode_PassesDumpAstAndCapturesOutput()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "<UiProgram></UiProgram>", string.Empty));
            global::QmlSharp.Qt.Tools.QmlDom dom = CreateDom(runner);

            QmlDomResult result = await dom.DumpFileAsync(file.Path, new QmlDomOptions { AstMode = true });

            Assert.True(result.Success);
            Assert.Contains("UiProgram", result.JsonOutput, StringComparison.Ordinal);
            Assert.Equal("--dump-ast", runner.SingleCall.Args[0]);
        }

        [Fact]
        public async Task QD003_DumpString_UsesTemporaryInputAndCleansItUp()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, """{"currentItem":{"isValid":true}}""", string.Empty));
            global::QmlSharp.Qt.Tools.QmlDom dom = CreateDom(runner);

            QmlDomResult result = await dom.DumpStringAsync("import QtQuick\nItem {}\n");

            string tempInput = runner.SingleCall.Args[^1];
            Assert.True(result.Success);
            Assert.EndsWith(".qml", tempInput, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(tempInput));
        }

        [Fact]
        public async Task QD004_DumpFile_WithFilterFields_PassesCommaSeparatedFilterFields()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, """{"currentRevision":3}""", string.Empty));
            global::QmlSharp.Qt.Tools.QmlDom dom = CreateDom(runner);

            _ = await dom.DumpFileAsync(
                file.Path,
                new QmlDomOptions { FilterFields = ["currentItem", "-:imports"] });

            AssertOptionValue(runner.SingleCall.Args, "--filter-fields", "currentItem,-:imports");
        }

        [Fact]
        public async Task QD005_DumpFile_WithNoDependencies_MapsToQtDependenciesNone()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, """{"currentItem":{"isValid":true}}""", string.Empty));
            global::QmlSharp.Qt.Tools.QmlDom dom = CreateDom(runner);

            _ = await dom.DumpFileAsync(file.Path, new QmlDomOptions { NoDependencies = true });

            AssertOptionValue(runner.SingleCall.Args, "-D", "none");
        }

        [Fact]
        public async Task DumpFile_WithToolFailure_DoesNotCaptureJsonOutput()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(1, """{"currentItem":{"isValid":false}}""", "qmldom failed"));
            global::QmlSharp.Qt.Tools.QmlDom dom = CreateDom(runner);

            QmlDomResult result = await dom.DumpFileAsync(file.Path);

            Assert.False(result.Success);
            Assert.Null(result.JsonOutput);
        }

        [Fact]
        public async Task DumpFile_WithMalformedJson_ReturnsNullJsonOutputWithoutThrowing()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "{not json", string.Empty));
            global::QmlSharp.Qt.Tools.QmlDom dom = CreateDom(runner);

            QmlDomResult result = await dom.DumpFileAsync(file.Path);

            Assert.True(result.Success);
            Assert.Null(result.JsonOutput);
        }

        [Fact]
        public void TryCaptureJsonOutput_WithPrefixText_ReturnsJsonSlice()
        {
            string? output = global::QmlSharp.Qt.Tools.QmlDom.TryCaptureJsonOutput("log line\n{\"ok\":true}");

            Assert.Equal("""{"ok":true}""", output);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlDom_DumpsRealFixtureDom()
        {
            global::QmlSharp.Qt.Tools.QmlDom dom = new();

            QmlDomResult result = await dom.DumpFileAsync(FixturePath("qml", "valid.qml"));

            Assert.True(result.Success);
            Assert.NotNull(result.JsonOutput);
            using JsonDocument document = JsonDocument.Parse(result.JsonOutput);
            Assert.True(document.RootElement.TryGetProperty("currentItem", out JsonElement _));
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlDom_DumpsRealFixtureAst()
        {
            global::QmlSharp.Qt.Tools.QmlDom dom = new();

            QmlDomResult result = await dom.DumpFileAsync(
                FixturePath("qml", "valid.qml"),
                new QmlDomOptions { AstMode = true });

            Assert.True(result.Success);
            Assert.Contains("UiProgram", result.JsonOutput, StringComparison.Ordinal);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlDom_DumpsString()
        {
            global::QmlSharp.Qt.Tools.QmlDom dom = new();

            QmlDomResult result = await dom.DumpStringAsync("import QtQuick\nItem { width: 100 }\n");

            Assert.True(result.Success);
            Assert.NotNull(result.JsonOutput);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlDom_DumpsWithNoDependencies()
        {
            global::QmlSharp.Qt.Tools.QmlDom dom = new();

            QmlDomResult result = await dom.DumpFileAsync(
                FixturePath("qml", "valid.qml"),
                new QmlDomOptions { NoDependencies = true });

            Assert.True(result.Success);
            Assert.NotNull(result.JsonOutput);
        }

        private static global::QmlSharp.Qt.Tools.QmlDom CreateDom(MockToolRunner runner)
        {
            return new global::QmlSharp.Qt.Tools.QmlDom(
                new MockQtToolchain("qmldom", Path.Join(Path.GetTempPath(), "qmldom-test.exe")),
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
                Command = "qmldom",
            };
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
                    "qmlsharp-qmldom-test-" + Guid.NewGuid().ToString("N") + ".qml");
                File.WriteAllText(path, contents);
                return new TemporaryQmlFile(path);
            }

            public void Dispose()
            {
                TryDeleteFile(Path);
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
