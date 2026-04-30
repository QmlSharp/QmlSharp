using System.Collections.Immutable;
using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.Qmltc
{
    [Trait("Category", TestCategories.Qmltc)]
    public sealed class QmltcTests
    {
        [Fact]
        public async Task QT001_CompileFile_WithOutputPaths_ReportsGeneratedPaths()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            string header = Path.Join(outputDirectory.Path, "Main.h");
            string source = Path.Join(outputDirectory.Path, "Main.cpp");
            MockToolRunner runner = new(static call =>
            {
                File.WriteAllText(ValueAfter(call.Args, "--header"), "class Main {};\n");
                File.WriteAllText(ValueAfter(call.Args, "--impl"), "#include \"Main.h\"\n");
                return CreateToolResult(0, string.Empty, string.Empty);
            });
            global::QmlSharp.Qt.Tools.Qmltc qmltc = CreateQmltc(runner);

            QmltcResult result = await qmltc.CompileFileAsync(
                file.Path,
                new QmltcOptions
                {
                    OutputHeader = header,
                    OutputSource = source,
                });

            Assert.True(result.Success);
            Assert.Equal(header, result.GeneratedHeader);
            Assert.Equal(source, result.GeneratedSource);
            AssertOptionValue(runner.SingleCall.Args, "--header", header);
            AssertOptionValue(runner.SingleCall.Args, "--impl", source);
            string resourceFile = ValueAfter(runner.SingleCall.Args, "--resource");
            Assert.EndsWith(".qrc", resourceFile, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(resourceFile));
            Assert.Equal(file.Path, runner.SingleCall.Args[^1]);
        }

        [Fact]
        public async Task QT002_CompileFile_WithNamespace_PassesNamespaceOption()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.Qmltc qmltc = CreateQmltc(runner);

            _ = await qmltc.CompileFileAsync(file.Path, new QmltcOptions { Namespace = "MyApp" });

            AssertOptionValue(runner.SingleCall.Args, "--namespace", "MyApp");
        }

        [Fact]
        public async Task QT003_CompileFile_WithModule_PassesModuleOption()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.Qmltc qmltc = CreateQmltc(runner);

            _ = await qmltc.CompileFileAsync(file.Path, new QmltcOptions { Module = "QmlSharp.MyApp" });

            AssertOptionValue(runner.SingleCall.Args, "--module", "QmlSharp.MyApp");
        }

        [Fact]
        public async Task QT004_CompileFile_WithExportMacro_PassesExportOption()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.Qmltc qmltc = CreateQmltc(runner);

            _ = await qmltc.CompileFileAsync(file.Path, new QmltcOptions { ExportMacro = "MYAPP_EXPORT" });

            AssertOptionValue(runner.SingleCall.Args, "--export", "MYAPP_EXPORT");
        }

        [Fact]
        public async Task QT005_CompileFile_WithSyntaxError_ReturnsDiagnostics()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(1, string.Empty, $"{file.Path}:2:1: error: Expected token \"}}\""));
            global::QmlSharp.Qt.Tools.Qmltc qmltc = CreateQmltc(runner);

            QmltcResult result = await qmltc.CompileFileAsync(file.Path);

            Assert.False(result.Success);
            Assert.Null(result.GeneratedHeader);
            Assert.Null(result.GeneratedSource);
            QtDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(file.Path, diagnostic.File);
            Assert.Equal(2, diagnostic.Line);
        }

        [Fact]
        public async Task CompileFile_WithoutOutputPaths_CreatesStableDefaultPathsForInvocation()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.Qmltc qmltc = CreateQmltc(runner);

            QmltcResult result = await qmltc.CompileFileAsync(file.Path);

            string header = ValueAfter(runner.SingleCall.Args, "--header");
            string source = ValueAfter(runner.SingleCall.Args, "--impl");
            Assert.True(result.Success);
            Assert.Equal(header, result.GeneratedHeader);
            Assert.Equal(source, result.GeneratedSource);
            Assert.EndsWith(".h", header, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(".cpp", source, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void QmltcStringInputCompatibility_IsIntentionallyFileOnly()
        {
            Assert.DoesNotContain(
                typeof(IQmltc).GetMethods(),
                static method => string.Equals(method.Name, "CompileStringAsync", StringComparison.Ordinal));
        }

        [Fact]
        public async Task CompileFile_NonZeroExitWithoutParseableStderr_ReturnsFallbackDiagnostic()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("Item {");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(2, string.Empty, "qmltc failed"));
            global::QmlSharp.Qt.Tools.Qmltc qmltc = CreateQmltc(runner);

            QmltcResult result = await qmltc.CompileFileAsync(file.Path);

            QtDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(file.Path, diagnostic.File);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("qmltc failed", diagnostic.Message);
        }

        [RequiresQtFact("qmltc")]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_Qmltc_CompilesValidRealFixture()
        {
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            string header = Path.Join(outputDirectory.Path, "Valid.h");
            string source = Path.Join(outputDirectory.Path, "Valid.cpp");
            global::QmlSharp.Qt.Tools.Qmltc qmltc = new();

            QmltcResult result = await qmltc.CompileFileAsync(
                FixturePath("qml", "valid.qml"),
                new QmltcOptions
                {
                    OutputHeader = header,
                    OutputSource = source,
                });

            Assert.True(result.Success);
            Assert.Equal(header, result.GeneratedHeader);
            Assert.Equal(source, result.GeneratedSource);
            Assert.True(File.Exists(header));
            Assert.True(File.Exists(source));
        }

        [RequiresQtFact("qmltc")]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_Qmltc_ReturnsDiagnosticsForSyntaxError()
        {
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            global::QmlSharp.Qt.Tools.Qmltc qmltc = new();

            QmltcResult result = await qmltc.CompileFileAsync(
                FixturePath("qml", "syntax-error.qml"),
                new QmltcOptions
                {
                    OutputHeader = Path.Join(outputDirectory.Path, "Error.h"),
                    OutputSource = Path.Join(outputDirectory.Path, "Error.cpp"),
                });

            Assert.False(result.Success);
            Assert.NotEmpty(result.Diagnostics);
        }

        private static global::QmlSharp.Qt.Tools.Qmltc CreateQmltc(MockToolRunner runner)
        {
            return new global::QmlSharp.Qt.Tools.Qmltc(
                new MockQtToolchain("qmltc", Path.Join(Path.GetTempPath(), "qmltc-test.exe")),
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
                Command = "qmltc",
            };
        }

        private static void AssertOptionValue(ImmutableArray<string> args, string option, string expectedValue)
        {
            int optionIndex = args.IndexOf(option);
            Assert.True(optionIndex >= 0, $"Expected argument '{option}' was not found.");
            Assert.True(optionIndex + 1 < args.Length, $"Expected argument '{option}' to have a value.");
            Assert.Equal(expectedValue, args[optionIndex + 1]);
        }

        private static string ValueAfter(ImmutableArray<string> args, string option)
        {
            int optionIndex = args.IndexOf(option);
            Assert.True(optionIndex >= 0, $"Expected argument '{option}' was not found.");
            Assert.True(optionIndex + 1 < args.Length, $"Expected argument '{option}' to have a value.");
            return args[optionIndex + 1];
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
                    "qmlsharp-qmltc-test-" + Guid.NewGuid().ToString("N") + ".qml");
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
                    "qmlsharp-qmltc-out-test-" + Guid.NewGuid().ToString("N"));
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
