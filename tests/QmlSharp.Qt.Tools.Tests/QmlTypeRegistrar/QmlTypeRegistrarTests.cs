using System.Collections.Immutable;
using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.QmlTypeRegistrar
{
    [Trait("Category", TestCategories.QmlTypeRegistrar)]
    public sealed class QmlTypeRegistrarTests
    {
        [Fact]
        public async Task REG001_Register_WithMetatypesJsonInput_ReportsOutputFile()
        {
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            string outputFile = Path.Join(outputDirectory.Path, "registration.cpp");
            MockToolRunner runner = new(static call =>
            {
                File.WriteAllText(ValueAfter(call.Args, "-o"), "void qml_register_types_TestModule();\n");
                return CreateToolResult(0, string.Empty, string.Empty);
            });
            global::QmlSharp.Qt.Tools.QmlTypeRegistrar registrar = CreateRegistrar(runner);

            TypeRegistrarResult result = await registrar.RegisterAsync(
                FixturePath("metatypes", "test_metatypes.json"),
                new TypeRegistrarOptions
                {
                    OutputFile = outputFile,
                    ModuleImportUri = "TestModule",
                    MajorVersion = 1,
                });

            Assert.True(result.Success);
            Assert.Equal(outputFile, result.OutputFile);
            AssertOptionValue(runner.SingleCall.Args, "-o", outputFile);
            AssertOptionValue(runner.SingleCall.Args, "--import-name", "TestModule");
            AssertOptionValue(runner.SingleCall.Args, "--major-version", "1");
            Assert.Equal(FixturePath("metatypes", "test_metatypes.json"), runner.SingleCall.Args[^1]);
        }

        [Fact]
        public void REG002_JsRootMode_IsNotExposedByCurrentQmlSharpApi()
        {
            Assert.DoesNotContain(
                typeof(TypeRegistrarOptions).GetProperties(),
                static property => string.Equals(property.Name, "JsRoot", StringComparison.Ordinal));
        }

        [Fact]
        public async Task REG003_QmlTypesConvenienceOutput_IsFoldedIntoRegisterOutputReporting()
        {
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            string outputFile = Path.Join(outputDirectory.Path, "out.qmltypes");
            MockToolRunner runner = new(static call =>
            {
                File.WriteAllText(ValueAfter(call.Args, "-o"), "Module { }\n");
                return CreateToolResult(0, string.Empty, string.Empty);
            });
            global::QmlSharp.Qt.Tools.QmlTypeRegistrar registrar = CreateRegistrar(runner);

            TypeRegistrarResult result = await registrar.RegisterAsync(
                FixturePath("metatypes", "test_metatypes.json"),
                new TypeRegistrarOptions { OutputFile = outputFile });

            Assert.True(result.Success);
            Assert.Equal(outputFile, result.OutputFile);
            Assert.DoesNotContain(
                typeof(IQmlTypeRegistrar).GetMethods(),
                static method => string.Equals(method.Name, "GenerateQmltypesAsync", StringComparison.Ordinal));
        }

        [Fact]
        public async Task REG004_Register_ResultIncludesCommandAndTimingMetadata()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.QmlTypeRegistrar registrar = CreateRegistrar(runner);

            TypeRegistrarResult result = await registrar.RegisterAsync(FixturePath("metatypes", "test_metatypes.json"));

            Assert.Contains("qmltyperegistrar", result.ToolResult.Command, StringComparison.Ordinal);
            Assert.True(result.ToolResult.DurationMs > 0);
        }

        [Fact]
        public async Task REG005_Register_WithNamespaceAndForeignTypes_PassesOptions()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(0, string.Empty, string.Empty));
            global::QmlSharp.Qt.Tools.QmlTypeRegistrar registrar = CreateRegistrar(runner);
            string foreignTypes = FixturePath("metatypes", "test_metatypes.json");

            _ = await registrar.RegisterAsync(
                FixturePath("metatypes", "test_metatypes.json"),
                new TypeRegistrarOptions
                {
                    Namespace = "TestNS",
                    ForeignTypesFile = foreignTypes,
                });

            AssertOptionValue(runner.SingleCall.Args, "--namespace", "TestNS");
            AssertOptionValue(runner.SingleCall.Args, "--foreign-types", foreignTypes);
        }

        [Fact]
        public async Task REG006_Register_InvalidMetatypesJson_ReturnsDiagnostics()
        {
            MockToolRunner runner = new();
            runner.Enqueue(CreateToolResult(1, string.Empty, "Error: invalid metatypes JSON"));
            global::QmlSharp.Qt.Tools.QmlTypeRegistrar registrar = CreateRegistrar(runner);

            TypeRegistrarResult result = await registrar.RegisterAsync(FixturePath("qml", "valid.qml"));

            Assert.False(result.Success);
            Assert.Null(result.OutputFile);
            QtDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("Error: invalid metatypes JSON", diagnostic.Message);
        }

        [RequiresQtFact("qmltyperegistrar")]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlTypeRegistrar_RegistersRealMetatypesFixture()
        {
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            string outputFile = Path.Join(outputDirectory.Path, "registration.cpp");
            global::QmlSharp.Qt.Tools.QmlTypeRegistrar registrar = new();

            TypeRegistrarResult result = await registrar.RegisterAsync(
                FixturePath("metatypes", "test_metatypes.json"),
                new TypeRegistrarOptions
                {
                    OutputFile = outputFile,
                    ModuleImportUri = "TestModule",
                    MajorVersion = 1,
                    Namespace = "QmlSharpTest",
                });

            Assert.True(result.Success);
            Assert.Equal(outputFile, result.OutputFile);
            Assert.True(File.Exists(outputFile));
        }

        [RequiresQtFact("qmltyperegistrar")]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QmlTypeRegistrar_InvalidMetatypesJsonFailsGracefully()
        {
            using TemporaryDirectory outputDirectory = TemporaryDirectory.Create();
            global::QmlSharp.Qt.Tools.QmlTypeRegistrar registrar = new();

            TypeRegistrarResult result = await registrar.RegisterAsync(
                FixturePath("qml", "valid.qml"),
                new TypeRegistrarOptions
                {
                    OutputFile = Path.Join(outputDirectory.Path, "bad.cpp"),
                    ModuleImportUri = "Bad",
                    MajorVersion = 1,
                });

            Assert.False(result.Success);
            Assert.NotEmpty(result.Diagnostics);
        }

        private static global::QmlSharp.Qt.Tools.QmlTypeRegistrar CreateRegistrar(MockToolRunner runner)
        {
            return new global::QmlSharp.Qt.Tools.QmlTypeRegistrar(
                new MockQtToolchain("qmltyperegistrar", Path.Join(Path.GetTempPath(), "qmltyperegistrar-test.exe")),
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
                Command = "qmltyperegistrar",
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
                    "qmlsharp-qmltyperegistrar-test-" + Guid.NewGuid().ToString("N"));
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
