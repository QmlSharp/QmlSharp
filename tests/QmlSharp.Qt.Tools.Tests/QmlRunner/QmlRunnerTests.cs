using System.Collections.Immutable;
using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.QmlRunner
{
    [Trait("Category", TestCategories.QmlRunner)]
    public sealed class QmlRunnerTests
    {
        [Fact]
        public async Task QR001_RunFile_WithStablePeriodAutoKill_PassesAndUsesOffscreenPlatform()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(new QtToolTimeoutError("qml", TimeSpan.FromMilliseconds(25), string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.QmlRunner qmlRunner = CreateRunner(runner);

            QmlRunResult result = await qmlRunner.RunFileAsync(
                file.Path,
                new QmlRunOptions
                {
                    StableRunPeriod = TimeSpan.FromMilliseconds(25),
                    Timeout = TimeSpan.FromSeconds(1),
                });

            Assert.True(result.Passed);
            Assert.True(result.AutoKilled);
            Assert.Empty(result.RuntimeErrors);
            Assert.Equal(TimeSpan.FromMilliseconds(25), runner.SingleCall.Options?.Timeout);
            AssertOptionValue(runner.SingleCall.Args, "--platform", "offscreen");
            Assert.Equal("--", runner.SingleCall.Args[^2]);
            Assert.Equal(file.Path, runner.SingleCall.Args[^1]);
        }

        [Fact]
        public async Task QR002_RunFile_WithStartupCrash_FailsWithoutAutoKill()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(
                1,
                string.Empty,
                file.Path + ":2:1: error: Component is not ready"));
            global::QmlSharp.Qt.Tools.QmlRunner qmlRunner = CreateRunner(runner);

            QmlRunResult result = await qmlRunner.RunFileAsync(
                file.Path,
                new QmlRunOptions
                {
                    StableRunPeriod = TimeSpan.FromSeconds(2),
                    Timeout = TimeSpan.FromSeconds(2),
                });

            Assert.False(result.Passed);
            Assert.False(result.AutoKilled);
            QtDiagnostic diagnostic = Assert.Single(result.RuntimeErrors);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Contains("Component is not ready", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task QR003_RunFile_WhenTimeoutPreventsStablePeriod_ThrowsTimeout()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(new QtToolTimeoutError("qml", TimeSpan.FromMilliseconds(10), "partial out", "partial err"));
            global::QmlSharp.Qt.Tools.QmlRunner qmlRunner = CreateRunner(runner);

            QtToolTimeoutError error = await Assert.ThrowsAsync<QtToolTimeoutError>(() =>
                qmlRunner.RunFileAsync(
                    file.Path,
                    new QmlRunOptions
                    {
                        StableRunPeriod = TimeSpan.FromSeconds(1),
                        Timeout = TimeSpan.FromMilliseconds(10),
                    }));

            Assert.Equal("qml", error.ToolName);
            Assert.Equal("partial err", error.PartialStderr);
        }

        [Fact]
        public async Task QR004_RunString_UsesTemporaryInputAndCleansItUp()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.QmlRunner qmlRunner = CreateRunner(runner);

            QmlRunResult result = await qmlRunner.RunStringAsync(
                "import QtQuick\nItem { Component.onCompleted: Qt.quit() }\n",
                new QmlRunOptions
                {
                    StableRunPeriod = TimeSpan.FromSeconds(2),
                    Timeout = TimeSpan.FromSeconds(2),
                });

            string tempInput = runner.SingleCall.Args[^1];
            Assert.True(result.Passed);
            Assert.False(result.AutoKilled);
            Assert.EndsWith(".qml", tempInput, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(tempInput));
        }

        [Fact]
        public async Task QR005_RunFile_WithWindowAppType_MapsToGuiAppTypeArgument()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nWindow {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.QmlRunner qmlRunner = CreateRunner(runner);

            _ = await qmlRunner.RunFileAsync(
                file.Path,
                new QmlRunOptions
                {
                    AppType = QmlAppType.Window,
                    StableRunPeriod = TimeSpan.FromSeconds(2),
                    Timeout = TimeSpan.FromSeconds(2),
                });

            AssertOptionValue(runner.SingleCall.Args, "--apptype", "gui");
        }

        [Fact]
        public async Task RunFile_WithItemAppType_MapsToGuiAppTypeArgument()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.QmlRunner qmlRunner = CreateRunner(runner);

            _ = await qmlRunner.RunFileAsync(
                file.Path,
                new QmlRunOptions
                {
                    AppType = QmlAppType.Item,
                    StableRunPeriod = TimeSpan.FromSeconds(2),
                    Timeout = TimeSpan.FromSeconds(2),
                });

            AssertOptionValue(runner.SingleCall.Args, "--apptype", "gui");
        }

        [Fact]
        public async Task QR006_RunFile_ParsesRuntimeReferenceErrorsFromStderr()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(
                0,
                string.Empty,
                file.Path + ":4: ReferenceError: missingFunction is not defined"));
            global::QmlSharp.Qt.Tools.QmlRunner qmlRunner = CreateRunner(runner);

            QmlRunResult result = await qmlRunner.RunFileAsync(
                file.Path,
                new QmlRunOptions
                {
                    StableRunPeriod = TimeSpan.FromSeconds(2),
                    Timeout = TimeSpan.FromSeconds(2),
                });

            Assert.False(result.Passed);
            QtDiagnostic diagnostic = Assert.Single(result.RuntimeErrors);
            Assert.Equal(file.Path, diagnostic.File);
            Assert.Equal(4, diagnostic.Line);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("ReferenceError: missingFunction is not defined", diagnostic.Message);
        }

        [Fact]
        public async Task QR007_ListConfigurations_ParsesConfigurationNamesFromToolOutput()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "Configurations:\n  default\n  software\n\n", string.Empty));
            global::QmlSharp.Qt.Tools.QmlRunner qmlRunner = CreateRunner(runner);

            ImmutableArray<string> configurations = await qmlRunner.ListConfigurationsAsync();

            Assert.Equal(["default", "software"], configurations.ToArray());
            Assert.Equal("--list-conf", runner.SingleCall.Args.Single());
        }

        [Fact]
        public async Task RunFile_WithToolchainImportPaths_PassesImportDirectoryArguments()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            string importPath = Path.Join(Path.GetTempPath(), "qmlsharp-imports");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.QmlRunner qmlRunner = new(
                new MockQtToolchain("qml", Path.Join(Path.GetTempPath(), "qml-test.exe"), [importPath]),
                runner,
                new QtDiagnosticParser());

            _ = await qmlRunner.RunFileAsync(
                file.Path,
                new QmlRunOptions
                {
                    StableRunPeriod = TimeSpan.FromSeconds(2),
                    Timeout = TimeSpan.FromSeconds(2),
                });

            AssertOptionValue(runner.SingleCall.Args, "-I", importPath);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlRunner_SmokeRunsWindowOffscreen()
        {
            global::QmlSharp.Qt.Tools.QmlRunner runner = new();

            QmlRunResult result = await runner.RunFileAsync(
                FixturePath("qml", "runner-window.qml"),
                new QmlRunOptions
                {
                    AppType = QmlAppType.Window,
                    StableRunPeriod = TimeSpan.FromMilliseconds(500),
                    Timeout = TimeSpan.FromSeconds(5),
                });

            Assert.True(result.Passed);
            Assert.True(result.AutoKilled);
            Assert.Empty(result.RuntimeErrors);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlRunner_RunsStringThatQuitsCleanly()
        {
            global::QmlSharp.Qt.Tools.QmlRunner runner = new();

            QmlRunResult result = await runner.RunStringAsync(
                "import QtQuick\nItem { Component.onCompleted: Qt.quit() }\n",
                new QmlRunOptions
                {
                    StableRunPeriod = TimeSpan.FromSeconds(2),
                    Timeout = TimeSpan.FromSeconds(5),
                });

            Assert.True(result.Passed);
            Assert.False(result.AutoKilled);
            Assert.Empty(result.RuntimeErrors);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlRunner_ListsConfigurations()
        {
            global::QmlSharp.Qt.Tools.QmlRunner runner = new();

            ImmutableArray<string> configurations = await runner.ListConfigurationsAsync();

            Assert.All(configurations, static configuration => Assert.False(string.IsNullOrWhiteSpace(configuration)));
        }

        private static global::QmlSharp.Qt.Tools.QmlRunner CreateRunner(MockToolRunner runner)
        {
            return new global::QmlSharp.Qt.Tools.QmlRunner(
                new MockQtToolchain("qml", Path.Join(Path.GetTempPath(), "qml-test.exe"), []),
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
                Command = "qml",
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

            public MockQtToolchain(string toolName, string toolPath, ImmutableArray<string> importPaths)
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
                    ImportPaths = importPaths,
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

            public List<RecordedCall> RecordedCalls { get; } = [];

            public RecordedCall SingleCall => Assert.Single(RecordedCalls);

            public void Enqueue(ToolResult result)
            {
                _results.Enqueue(result);
            }

            public void Enqueue(QtToolTimeoutError error)
            {
                _results.Enqueue(error);
            }

            public Task<ToolResult> RunAsync(
                string toolPath,
                ImmutableArray<string> args,
                ToolRunnerOptions? options = null,
                CancellationToken ct = default)
            {
                RecordedCall call = new(toolPath, args, options);
                RecordedCalls.Add(call);
                object result = _results.Dequeue();
                if (result is QtToolTimeoutError timeoutError)
                {
                    throw timeoutError;
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
                    "qmlsharp-qmlrunner-test-" + Guid.NewGuid().ToString("N") + ".qml");
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
