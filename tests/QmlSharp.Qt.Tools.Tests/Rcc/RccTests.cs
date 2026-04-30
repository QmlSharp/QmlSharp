using System.Collections.Immutable;
using System.Xml.Linq;
using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.Rcc
{
    [Trait("Category", TestCategories.Rcc)]
    public sealed class RccTests
    {
        [Fact]
        public async Task RC001_CompileAsync_WithOutputFile_ReportsGeneratedCpp()
        {
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            string outputFile = Path.Join(outputDirectory.Path, "resources.cpp");
            MockToolRunner runner = new(static call =>
            {
                File.WriteAllText(ValueAfter(call.Args, "--output"), "int qInitResources();\n");
                return CreateToolResult(0, string.Empty, string.Empty);
            });
            global::QmlSharp.Qt.Tools.Rcc rcc = CreateRcc(runner);

            RccResult result = await rcc.CompileAsync(
                FixturePath("resources", "test.qrc"),
                new RccOptions { OutputFile = outputFile });

            Assert.True(result.Success);
            Assert.Equal(outputFile, result.OutputFile);
            Assert.Equal(1, result.ToolResult.DurationMs);
            Assert.Contains("rcc", result.ToolResult.Command, StringComparison.Ordinal);
            AssertOptionValue(runner.SingleCall.Args, "--output", outputFile);
            Assert.Equal(FixturePath("resources", "test.qrc"), runner.SingleCall.Args[^1]);
        }

        [Fact]
        public async Task RC002_ListEntriesAsync_ParsesResourceEntries()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, "/icons/logo.png\n/data/data.json\n\n", string.Empty));
            global::QmlSharp.Qt.Tools.Rcc rcc = CreateRcc(runner);

            ImmutableArray<string> entries = await rcc.ListEntriesAsync(FixturePath("resources", "test.qrc"));

            Assert.Equal(["/icons/logo.png", "/data/data.json"], entries.ToArray());
            Assert.Contains("--list", runner.SingleCall.Args);
        }

        [Fact]
        public async Task RC003_ListMappingsAsync_ParsesResourceMappings()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, ":/icon.png\tC:/fixtures/icon.png\n:/data.json\tC:/fixtures/data.json\n", string.Empty));
            global::QmlSharp.Qt.Tools.Rcc rcc = CreateRcc(runner);

            ImmutableArray<RccMapping> mappings = await rcc.ListMappingsAsync(FixturePath("resources", "test.qrc"));

            Assert.Equal(2, mappings.Length);
            Assert.Equal("/icon.png", mappings[0].ResourcePath);
            Assert.Equal("C:/fixtures/icon.png", mappings[0].FilePath);
            Assert.Equal("/data.json", mappings[1].ResourcePath);
            Assert.Contains("--list-mapping", runner.SingleCall.Args);
        }

        [Fact]
        public async Task RC004_CreateQrcXmlAsync_GeneratesDeterministicEscapedXml()
        {
            global::QmlSharp.Qt.Tools.Rcc rcc = new(
                new MockQtToolchain("rcc", Path.Join(Path.GetTempPath(), "rcc-test.exe")),
                new MockToolRunner(),
                new QtDiagnosticParser());
            ImmutableArray<RccFileEntry> files =
            [
                new RccFileEntry { FilePath = "qml/a&b<test>.qml", Alias = "alias\"&.qml" },
                new RccFileEntry { FilePath = "qml/second.qml" },
            ];

            string first = await rcc.CreateQrcXmlAsync(files, "/qml\"&");
            string second = await rcc.CreateQrcXmlAsync(files, "/qml\"&");

            Assert.Equal(first, second);
            Assert.Contains("&amp;", first, StringComparison.Ordinal);
            Assert.Contains("&lt;test&gt;", first, StringComparison.Ordinal);
            Assert.Contains("prefix=\"/qml&quot;&amp;\"", first, StringComparison.Ordinal);
            Assert.Contains("alias=\"alias&quot;&amp;.qml\"", first, StringComparison.Ordinal);

            XDocument document = XDocument.Parse(first);
            Assert.Equal("RCC", document.DocumentType?.Name);
            Assert.Equal("RCC", document.Root?.Name.LocalName);
            Assert.Equal(
                ["qml/a&b<test>.qml", "qml/second.qml"],
                document.Descendants("file").Select(static element => element.Value).ToArray());
        }

        [Fact]
        public async Task RC005_CompileAsync_WithBinaryMode_PassesBinaryOption()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.Rcc rcc = CreateRcc(runner);

            _ = await rcc.CompileAsync(FixturePath("resources", "test.qrc"), new RccOptions { BinaryMode = true });

            Assert.Contains("--binary", runner.SingleCall.Args);
        }

        [Fact]
        public async Task RC006_CompileAsync_WithNoCompress_PassesNoCompressOption()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.Rcc rcc = CreateRcc(runner);

            _ = await rcc.CompileAsync(FixturePath("resources", "test.qrc"), new RccOptions { NoCompress = true });

            Assert.Contains("--no-compress", runner.SingleCall.Args);
        }

        [Fact]
        public async Task RC007_CompileAsync_WithPythonOutput_PassesPythonGenerator()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.Rcc rcc = CreateRcc(runner);

            _ = await rcc.CompileAsync(FixturePath("resources", "test.qrc"), new RccOptions { PythonOutput = true });

            AssertOptionValue(runner.SingleCall.Args, "--generator", "python");
        }

        [Fact]
        public async Task CompileAsync_WithBinaryAndPythonOutput_ThrowsWithoutRunningRcc()
        {
            MockToolRunner runner = new();
            global::QmlSharp.Qt.Tools.Rcc rcc = CreateRcc(runner);

            ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(() =>
                rcc.CompileAsync(
                    FixturePath("resources", "test.qrc"),
                    new RccOptions { BinaryMode = true, PythonOutput = true }));

            Assert.Equal("options", error.ParamName);
            Assert.Empty(runner.RecordedCalls);
        }

        [Fact]
        public async Task CompileAsync_NonZeroExitWithoutParseableStderr_ReturnsFallbackDiagnostic()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(1, string.Empty, "rcc failed"));
            global::QmlSharp.Qt.Tools.Rcc rcc = CreateRcc(runner);

            RccResult result = await rcc.CompileAsync(FixturePath("resources", "test.qrc"));

            Assert.False(result.Success);
            QtDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("rcc failed", diagnostic.Message);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_Rcc_CompilesAndListsRealResourceFixture()
        {
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            string outputFile = Path.Join(outputDirectory.Path, "resources.cpp");
            global::QmlSharp.Qt.Tools.Rcc rcc = new();

            RccResult result = await rcc.CompileAsync(
                FixturePath("resources", "test.qrc"),
                new RccOptions { OutputFile = outputFile });
            ImmutableArray<string> entries = await rcc.ListEntriesAsync(FixturePath("resources", "test.qrc"));
            ImmutableArray<RccMapping> mappings = await rcc.ListMappingsAsync(FixturePath("resources", "test.qrc"));

            Assert.True(result.Success);
            Assert.Equal(outputFile, result.OutputFile);
            Assert.True(File.Exists(outputFile));
            Assert.Contains(entries, static entry => entry.EndsWith("icon.png", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(mappings, static mapping => mapping.ResourcePath == "/icon.png");
        }

        private static global::QmlSharp.Qt.Tools.Rcc CreateRcc(MockToolRunner runner)
        {
            return new global::QmlSharp.Qt.Tools.Rcc(
                new MockQtToolchain("rcc", Path.Join(Path.GetTempPath(), "rcc-test.exe")),
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
                Command = "rcc",
            };
        }

        private static string FixturePath(string group, string fileName)
        {
            return Path.Join(AppContext.BaseDirectory, "Fixtures", group, fileName);
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
                    "qmlsharp-rcc-test-" + Guid.NewGuid().ToString("N"));
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
    }
}
