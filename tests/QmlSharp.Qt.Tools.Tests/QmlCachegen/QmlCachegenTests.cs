using System.Collections.Immutable;
using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.QmlCachegen
{
    [Trait("Category", TestCategories.QmlCachegen)]
    public sealed class QmlCachegenTests
    {
        [Fact]
        public async Task QC001_CompileFile_WithDefaultOptions_ReportsOutputFile()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.QmlCachegen cachegen = CreateCachegen(runner);

            QmlCachegenResult result = await cachegen.CompileFileAsync(
                file.Path,
                new QmlCachegenOptions { OutputDir = outputDirectory.Path });

            Assert.True(result.Success);
            Assert.Empty(result.Diagnostics);
            string outputFile = Assert.Single(result.OutputFiles);
            Assert.Equal(Path.Join(outputDirectory.Path, Path.GetFileName(file.Path) + ".cpp"), outputFile);
            AssertOptionValue(runner.SingleCall.Args, "-o", outputFile);
            Assert.Equal(file.Path, runner.SingleCall.Args[^1]);
        }

        [Fact]
        public async Task QC002_CompileFile_WithSyntaxError_ReturnsDiagnostics()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {\n");
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(1, string.Empty, $"{file.Path}:2:1: error: Expected token \"}}\""));
            global::QmlSharp.Qt.Tools.QmlCachegen cachegen = CreateCachegen(runner);

            QmlCachegenResult result = await cachegen.CompileFileAsync(
                file.Path,
                new QmlCachegenOptions { OutputDir = outputDirectory.Path });

            Assert.False(result.Success);
            QtDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(file.Path, diagnostic.File);
            Assert.Equal(2, diagnostic.Line);
        }

        [Fact]
        public async Task QC003_CompileString_UsesTemporaryInputAndCleansItUp()
        {
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.QmlCachegen cachegen = CreateCachegen(runner);

            QmlCachegenResult result = await cachegen.CompileStringAsync(
                "import QtQuick\nItem {}\n",
                new QmlCachegenOptions { OutputDir = outputDirectory.Path });

            string tempInput = runner.SingleCall.Args[^1];
            Assert.True(result.Success);
            Assert.False(File.Exists(tempInput));
            Assert.EndsWith(".qml", tempInput, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task QC004_CompileFile_WithBytecodeOnly_PassesOnlyBytecodeAndReportsQmlc()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.QmlCachegen cachegen = CreateCachegen(runner);

            QmlCachegenResult result = await cachegen.CompileFileAsync(
                file.Path,
                new QmlCachegenOptions
                {
                    BytecodeOnly = true,
                    OutputDir = outputDirectory.Path,
                });

            Assert.Contains("--only-bytecode", runner.SingleCall.Args);
            Assert.EndsWith(".qmlc", Assert.Single(result.OutputFiles), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task QC005_CompileFile_ParsesAotStatsFile()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            MockToolRunner runner = new(call =>
            {
                string outputFile = ValueAfter(call.Args, "-o");
                File.WriteAllText(
                    outputFile + ".aotstats",
                    """{"modules":[{"moduleFiles":[{"entries":[{"functionName":"program","codegenResult":0},{"functionName":"helper","codegenResult":1}]}]}]}""");
                return CreateToolResult(0, string.Empty, string.Empty);
            });
            global::QmlSharp.Qt.Tools.QmlCachegen cachegen = CreateCachegen(runner);

            QmlCachegenResult result = await cachegen.CompileFileAsync(
                file.Path,
                new QmlCachegenOptions { OutputDir = outputDirectory.Path });

            Assert.NotNull(result.AotStats);
            Assert.Equal(2, result.AotStats.TotalFunctions);
            Assert.Equal(1, result.AotStats.CompiledFunctions);
            Assert.Equal(1, result.AotStats.FailedFunctions);
            Assert.Equal(50.0, result.AotStats.SuccessRate);
            Assert.Contains("--dump-aot-stats", runner.SingleCall.Args);
        }

        [Fact]
        public async Task QC006_CompileFile_WithWarningsAsErrors_PassesWarningsAreErrors()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(1, string.Empty, $"{file.Path}:3:1: error: promoted warning"));
            global::QmlSharp.Qt.Tools.QmlCachegen cachegen = CreateCachegen(runner);

            QmlCachegenResult result = await cachegen.CompileFileAsync(
                file.Path,
                new QmlCachegenOptions { WarningsAsErrors = true });

            Assert.Contains("--warnings-are-errors", runner.SingleCall.Args);
            Assert.False(result.Success);
            Assert.Equal(DiagnosticSeverity.Error, Assert.Single(result.Diagnostics).Severity);
        }

        [Fact]
        public async Task QC007_CompileFile_WithVerbose_PassesVerboseAndPreservesStdout()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "verbose compilation output", string.Empty));
            global::QmlSharp.Qt.Tools.QmlCachegen cachegen = CreateCachegen(runner);

            QmlCachegenResult result = await cachegen.CompileFileAsync(
                file.Path,
                new QmlCachegenOptions { Verbose = true });

            Assert.Contains("--verbose", runner.SingleCall.Args);
            Assert.Contains("verbose", result.ToolResult.Stdout, StringComparison.Ordinal);
        }

        [Fact]
        public async Task QC008_CompileFile_WithImportPaths_PassesToolchainThenOptionImports()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.QmlCachegen cachegen = CreateCachegen(
                runner,
                [Path.Join(Path.GetTempPath(), "toolchain-import")]);
            string optionImport = Path.Join(Path.GetTempPath(), "option-import");

            _ = await cachegen.CompileFileAsync(
                file.Path,
                new QmlCachegenOptions { ImportPaths = [optionImport] });

            Assert.Contains("-I", runner.SingleCall.Args);
            Assert.Contains(optionImport, runner.SingleCall.Args);
            Assert.Contains(Path.Join(Path.GetTempPath(), "toolchain-import"), runner.SingleCall.Args);
        }

        [Fact]
        public async Task QC009_CompileBatch_ReturnsAggregateCountsAndDuration()
        {
            using TemporaryQmlFile first = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            using TemporaryQmlFile second = TemporaryQmlFile.Create("import QtQuick\nItem {\n");
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty, durationMs: 7));
            runner.Enqueue(CreateToolResult(1, string.Empty, $"{second.Path}:2:1: error: bad", durationMs: 11));
            global::QmlSharp.Qt.Tools.QmlCachegen cachegen = CreateCachegen(runner);

            QmlCachegenBatchResult result = await cachegen.CompileBatchAsync([first.Path, second.Path]);

            Assert.Equal(2, result.Results.Length);
            Assert.Equal(1, result.SuccessCount);
            Assert.Equal(1, result.FailureCount);
            Assert.Equal(18, result.TotalDurationMs);
            Assert.Equal(first.Path, runner.RecordedCalls[0].Args[^1]);
            Assert.Equal(second.Path, runner.RecordedCalls[1].Args[^1]);
        }

        [Fact]
        public async Task QC010_CompileBatch_AggregatesAotStats()
        {
            using TemporaryQmlFile first = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            using TemporaryQmlFile second = TemporaryQmlFile.Create("import QtQuick\nRectangle {}\n");
            int callIndex = 0;
            MockToolRunner runner = new(call =>
            {
                string outputFile = ValueAfter(call.Args, "-o");
                int result = callIndex++ == 0 ? 0 : 1;
                File.WriteAllText(
                    outputFile + ".aotstats",
                    $$"""{"entries":[{"codegenResult":0},{"codegenResult":{{result}}}]}""");
                return CreateToolResult(0, string.Empty, string.Empty);
            });
            global::QmlSharp.Qt.Tools.QmlCachegen cachegen = CreateCachegen(runner);

            QmlCachegenBatchResult result = await cachegen.CompileBatchAsync([first.Path, second.Path]);

            Assert.NotNull(result.AggregateStats);
            Assert.Equal(4, result.AggregateStats.TotalFunctions);
            Assert.Equal(3, result.AggregateStats.CompiledFunctions);
            Assert.Equal(1, result.AggregateStats.FailedFunctions);
        }

        [Fact]
        public async Task CompileBatch_WithEmptyInput_ReturnsEmptyWithoutToolLookup()
        {
            MockToolRunner runner = new();
            global::QmlSharp.Qt.Tools.QmlCachegen cachegen = new(
                new ThrowingQtToolchain(),
                runner,
                new QtDiagnosticParser());

            QmlCachegenBatchResult result = await cachegen.CompileBatchAsync([]);

            Assert.Empty(result.Results);
            Assert.Empty(runner.RecordedCalls);
        }

        [Fact]
        public async Task CompileString_NonZeroExitWithoutParseableStderr_ReturnsFallbackDiagnosticWithStringPath()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(2, string.Empty, "cachegen failed"));
            global::QmlSharp.Qt.Tools.QmlCachegen cachegen = CreateCachegen(runner);

            QmlCachegenResult result = await cachegen.CompileStringAsync("Item {");

            QtDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("<string>", diagnostic.File);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("cachegen failed", diagnostic.Message);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlCachegen_CompilesValidRealFixture()
        {
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            global::QmlSharp.Qt.Tools.QmlCachegen cachegen = new();

            QmlCachegenResult result = await cachegen.CompileFileAsync(
                FixturePath("qml", "valid.qml"),
                new QmlCachegenOptions { OutputDir = outputDirectory.Path });

            Assert.True(result.Success);
            Assert.NotEmpty(result.OutputFiles);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlCachegen_ReturnsDiagnosticsForSyntaxError()
        {
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            global::QmlSharp.Qt.Tools.QmlCachegen cachegen = new();

            QmlCachegenResult result = await cachegen.CompileFileAsync(
                FixturePath("qml", "syntax-error.qml"),
                new QmlCachegenOptions { OutputDir = outputDirectory.Path });

            Assert.False(result.Success);
            Assert.NotEmpty(result.Diagnostics);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlCachegen_CompilesString()
        {
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            global::QmlSharp.Qt.Tools.QmlCachegen cachegen = new();

            QmlCachegenResult result = await cachegen.CompileStringAsync(
                "import QtQuick\nItem {}\n",
                new QmlCachegenOptions { OutputDir = outputDirectory.Path });

            Assert.True(result.Success);
            Assert.NotEmpty(result.OutputFiles);
        }

        private static global::QmlSharp.Qt.Tools.QmlCachegen CreateCachegen(
            MockToolRunner runner,
            ImmutableArray<string> importPaths = default)
        {
            return new global::QmlSharp.Qt.Tools.QmlCachegen(
                new MockQtToolchain("qmlcachegen", Path.Join(Path.GetTempPath(), "qmlcachegen-test.exe"), importPaths),
                runner,
                new QtDiagnosticParser());
        }

        private static ToolResult CreateToolResult(int exitCode, string stdout, string stderr, long durationMs = 1)
        {
            return new ToolResult
            {
                ExitCode = exitCode,
                Stdout = stdout,
                Stderr = stderr,
                DurationMs = durationMs,
                Command = "qmlcachegen",
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
                    "qmlsharp-qmlcachegen-test-" + Guid.NewGuid().ToString("N") + ".qml");
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
                    "qmlsharp-qmlcachegen-out-test-" + Guid.NewGuid().ToString("N"));
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
