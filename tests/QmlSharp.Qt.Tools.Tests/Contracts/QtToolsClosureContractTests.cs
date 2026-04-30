using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.Contracts
{
    [Trait("Category", TestCategories.Smoke)]
    public sealed class QtToolsClosureContractTests
    {
        [Fact]
        public void Step0414_TestSpecCoverage_MapsAll112QtToolsIds()
        {
            string repositoryRoot = FindRepositoryRoot();
            string testRoot = Path.Join(repositoryRoot, "tests", "QmlSharp.Qt.Tools.Tests");
            HashSet<string> actualIds = Directory
                .EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories)
                .Where(static path => !path.EndsWith(nameof(QtToolsClosureContractTests) + ".cs", StringComparison.Ordinal))
                .SelectMany(ReadSpecIds)
                .ToHashSet(StringComparer.Ordinal);

            string[] expectedIds = CreateExpectedSpecIds();

            Assert.Equal(112, expectedIds.Length);
            Assert.Empty(expectedIds.Except(actualIds, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void Step0414_DirectWrappers_RemainPublicAndConstructible()
        {
            (Type Contract, Type Implementation)[] wrappers =
            [
                (typeof(IQmlFormat), typeof(global::QmlSharp.Qt.Tools.QmlFormat)),
                (typeof(IQmlLint), typeof(global::QmlSharp.Qt.Tools.QmlLint)),
                (typeof(IQmlCachegen), typeof(global::QmlSharp.Qt.Tools.QmlCachegen)),
                (typeof(IQmltc), typeof(global::QmlSharp.Qt.Tools.Qmltc)),
                (typeof(IQmlImportScanner), typeof(global::QmlSharp.Qt.Tools.QmlImportScanner)),
                (typeof(IQmlDom), typeof(global::QmlSharp.Qt.Tools.QmlDom)),
                (typeof(IQmlRunner), typeof(global::QmlSharp.Qt.Tools.QmlRunner)),
                (typeof(IRcc), typeof(global::QmlSharp.Qt.Tools.Rcc)),
                (typeof(IQmlTypeRegistrar), typeof(global::QmlSharp.Qt.Tools.QmlTypeRegistrar)),
            ];

            Assert.Equal(9, wrappers.Length);
            foreach ((Type contract, Type implementation) in wrappers)
            {
                Assert.True(contract.IsAssignableFrom(implementation), $"{implementation.Name} must implement {contract.Name}.");
                Assert.NotNull(implementation.GetConstructor(Type.EmptyTypes));
                Assert.NotNull(Activator.CreateInstance(implementation));
            }
        }

        [Fact]
        public void Step0414_DirectWrappers_ValidateInjectedInfrastructure()
        {
            QtToolchain toolchain = new();
            global::QmlSharp.Qt.Tools.ToolRunner toolRunner = new();
            QtDiagnosticParser parser = new();

            AssertParam("toolchain", () => new global::QmlSharp.Qt.Tools.QmlFormat(null!, toolRunner, parser));
            AssertParam("toolRunner", () => new global::QmlSharp.Qt.Tools.QmlFormat(toolchain, null!, parser));
            AssertParam("diagnosticParser", () => new global::QmlSharp.Qt.Tools.QmlFormat(toolchain, toolRunner, null!));

            AssertParam("toolchain", () => new global::QmlSharp.Qt.Tools.QmlLint(null!, toolRunner, parser));
            AssertParam("toolRunner", () => new global::QmlSharp.Qt.Tools.QmlLint(toolchain, null!, parser));
            AssertParam("diagnosticParser", () => new global::QmlSharp.Qt.Tools.QmlLint(toolchain, toolRunner, null!));

            AssertParam("toolchain", () => new global::QmlSharp.Qt.Tools.QmlCachegen(null!, toolRunner, parser));
            AssertParam("toolRunner", () => new global::QmlSharp.Qt.Tools.QmlCachegen(toolchain, null!, parser));
            AssertParam("diagnosticParser", () => new global::QmlSharp.Qt.Tools.QmlCachegen(toolchain, toolRunner, null!));

            AssertParam("toolchain", () => new global::QmlSharp.Qt.Tools.Qmltc(null!, toolRunner, parser));
            AssertParam("toolRunner", () => new global::QmlSharp.Qt.Tools.Qmltc(toolchain, null!, parser));
            AssertParam("diagnosticParser", () => new global::QmlSharp.Qt.Tools.Qmltc(toolchain, toolRunner, null!));

            AssertParam("toolchain", () => new global::QmlSharp.Qt.Tools.QmlImportScanner(null!, toolRunner));
            AssertParam("toolRunner", () => new global::QmlSharp.Qt.Tools.QmlImportScanner(toolchain, null!));

            AssertParam("toolchain", () => new global::QmlSharp.Qt.Tools.QmlDom(null!, toolRunner));
            AssertParam("toolRunner", () => new global::QmlSharp.Qt.Tools.QmlDom(toolchain, null!));

            AssertParam("toolchain", () => new global::QmlSharp.Qt.Tools.QmlRunner(null!, toolRunner, parser));
            AssertParam("toolRunner", () => new global::QmlSharp.Qt.Tools.QmlRunner(toolchain, null!, parser));
            AssertParam("diagnosticParser", () => new global::QmlSharp.Qt.Tools.QmlRunner(toolchain, toolRunner, null!));

            AssertParam("toolchain", () => new global::QmlSharp.Qt.Tools.Rcc(null!, toolRunner, parser));
            AssertParam("toolRunner", () => new global::QmlSharp.Qt.Tools.Rcc(toolchain, null!, parser));
            AssertParam("diagnosticParser", () => new global::QmlSharp.Qt.Tools.Rcc(toolchain, toolRunner, null!));

            AssertParam("toolchain", () => new global::QmlSharp.Qt.Tools.QmlTypeRegistrar(null!, toolRunner, parser));
            AssertParam("toolRunner", () => new global::QmlSharp.Qt.Tools.QmlTypeRegistrar(toolchain, null!, parser));
            AssertParam("diagnosticParser", () => new global::QmlSharp.Qt.Tools.QmlTypeRegistrar(toolchain, toolRunner, null!));
        }

        [Fact]
        public void Step0414_ToolAvailability_ReportsNineWrappersAndSupportTools()
        {
            string[] expectedProperties =
            [
                nameof(ToolAvailability.QmlFormat),
                nameof(ToolAvailability.QmlLint),
                nameof(ToolAvailability.QmlCachegen),
                nameof(ToolAvailability.Qmltc),
                nameof(ToolAvailability.QmlImportScanner),
                nameof(ToolAvailability.QmlDom),
                nameof(ToolAvailability.Qml),
                nameof(ToolAvailability.Rcc),
                nameof(ToolAvailability.QmlTypeRegistrar),
                nameof(ToolAvailability.Moc),
                nameof(ToolAvailability.QmlAotStats),
            ];

            string[] actualProperties = typeof(ToolAvailability)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(static property => property.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expectedProperties.Order(StringComparer.Ordinal), actualProperties);
        }

        [Fact]
        public void Step0414_StaticWrapperContracts_CoverDefensiveParsingAndArgumentBranches()
        {
            ImmutableArray<string> scanArgs = global::QmlSharp.Qt.Tools.QmlImportScanner.BuildFilesArguments(
                ["Main.qml"],
                new QmlImportScanOptions
                {
                    RootPath = ".",
                    ImportPaths = ["imports", "imports", " "],
                    ExcludeDirs = ["build", "build", ""],
                },
                toolchainImportPaths: ["toolchain-imports"]);

            Assert.Contains("-rootPath", scanArgs);
            Assert.Contains("-importPath", scanArgs);
            Assert.Contains("-exclude", scanArgs);
            _ = Assert.Throws<ArgumentException>("filePaths", () =>
                global::QmlSharp.Qt.Tools.QmlImportScanner.BuildFilesArguments([" "], new QmlImportScanOptions(), toolchainImportPaths: []));

            ImmutableArray<QmlImportEntry> imports = global::QmlSharp.Qt.Tools.QmlImportScanner.ParseImports("""
                {
                  "imports": [
                    42,
                    { "name": "QtQuick", "type": "module", "path": "/qt", "version": "6.11" },
                    { "name": 5, "majorVersion": 2 },
                    { "name": "Versioned", "versionMajor": 1, "versionMinor": 3 }
                  ]
                }
                """);

            Assert.Equal(3, imports.Length);
            Assert.Equal("6.11", imports[0].Version);
            Assert.Equal(string.Empty, imports[1].Name);
            Assert.Equal("module", imports[1].Type);
            Assert.Equal("2", imports[1].Version);
            Assert.Equal("1.3", imports[2].Version);
            Assert.Empty(global::QmlSharp.Qt.Tools.QmlImportScanner.ParseImports("not json"));
            Assert.Empty(global::QmlSharp.Qt.Tools.QmlImportScanner.ParseImports("""{"imports":{}}"""));

            ImmutableArray<string> runnerArgs = global::QmlSharp.Qt.Tools.QmlRunner.BuildArguments(
                "Main.qml",
                new QmlRunOptions
                {
                    Platform = " ",
                    AppType = QmlAppType.Item,
                    ImportPaths = ["app-imports", "app-imports"],
                },
                toolchainImportPaths: ["toolchain-imports"]);
            Assert.Contains("--apptype", runnerArgs);
            Assert.DoesNotContain("--platform", runnerArgs);
            Assert.Equal(["windows", "linux"], global::QmlSharp.Qt.Tools.QmlRunner.ParseConfigurations("Available configurations:\nwindows\r\n\nlinux\n").ToArray());

            ImmutableArray<string> rccArgs = global::QmlSharp.Qt.Tools.Rcc.BuildCompileArguments(
                "resources.qrc",
                "resources.rcc",
                new RccOptions { BinaryMode = true, NoCompress = true });
            Assert.Contains("--binary", rccArgs);
            Assert.Contains("--no-compress", rccArgs);
            _ = Assert.Throws<ArgumentException>("options", () =>
                global::QmlSharp.Qt.Tools.Rcc.BuildCompileArguments("resources.qrc", null, new RccOptions { BinaryMode = true, PythonOutput = true }));
            ImmutableArray<RccMapping> mappings = global::QmlSharp.Qt.Tools.Rcc.ParseMappings("\n:/main.qml\tC:/app/Main.qml\ninvalid\n/\troot.txt\n");
            Assert.Equal(2, mappings.Length);
            Assert.Equal("/main.qml", mappings[0].ResourcePath);
            Assert.Equal("/", mappings[1].ResourcePath);

            ImmutableArray<string> cachegenArgs = global::QmlSharp.Qt.Tools.QmlCachegen.BuildArguments(
                "Main.qml",
                "Main.cpp",
                "/Main.qml",
                new QmlCachegenOptions
                {
                    BytecodeOnly = true,
                    WarningsAsErrors = true,
                    Verbose = true,
                    ImportPaths = ["imports", "imports"],
                },
                toolchainImportPaths: ["toolchain-imports"]);
            Assert.Contains("--only-bytecode", cachegenArgs);
            Assert.Contains("--warnings-are-errors", cachegenArgs);
            Assert.Contains("--verbose", cachegenArgs);
            Assert.EndsWith(".qmlc", global::QmlSharp.Qt.Tools.QmlCachegen.CreateOutputFilePath("/Main.qml", Path.GetTempPath(), new QmlCachegenOptions { BytecodeOnly = true }), StringComparison.Ordinal);
            Assert.StartsWith("/absolute/", global::QmlSharp.Qt.Tools.QmlCachegen.CreateResourcePath(Path.GetFullPath(Path.Join("..", "outside.qml"))), StringComparison.Ordinal);
        }

        [Fact]
        public void Step0414_PrivateDefensiveHelpers_CoverRuntimeAndAotBranches()
        {
            Type runnerType = typeof(global::QmlSharp.Qt.Tools.QmlRunner);
            global::QmlSharp.Qt.Tools.QmlRunner runner = new(new QtToolchain(), new global::QmlSharp.Qt.Tools.ToolRunner(), new QtDiagnosticParser());
            string inputFile = Path.GetFullPath("Main.qml");
            ImmutableArray<QtDiagnostic> runtimeDiagnostics = InvokeInstance<ImmutableArray<QtDiagnostic>>(
                runner,
                "ParseRuntimeDiagnostics",
                $"{inputFile}:12:7: ReferenceError: missingThing is not defined\n"
                + "module \"QtQml.WorkerScript\" is not installed\n"
                + "plain informational line",
                inputFile,
                "<string>");

            QtDiagnostic runtimeDiagnostic = Assert.Single(runtimeDiagnostics);
            Assert.Equal("<string>", runtimeDiagnostic.File);
            Assert.Equal(12, runtimeDiagnostic.Line);
            Assert.Equal(7, runtimeDiagnostic.Column);
            Assert.Contains("ReferenceError", runtimeDiagnostic.Message, StringComparison.Ordinal);

            Assert.True(InvokeStatic<bool>(runnerType, "IsRuntimeErrorLine", "TypeError: bad value"));
            Assert.False(InvokeStatic<bool>(runnerType, "IsRuntimeErrorLine", "module \"QtQml.WorkerScript\" is not installed"));
            Assert.Equal("\"\"", InvokeStatic<string>(runnerType, "QuoteCommandPart", string.Empty));
            Assert.Equal("plain", InvokeStatic<string>(runnerType, "QuoteCommandPart", "plain"));
            Assert.Equal("\"two words\"", InvokeStatic<string>(runnerType, "QuoteCommandPart", "two words"));
            Assert.Equal("\"has\\\"quote\"", InvokeStatic<string>(runnerType, "QuoteCommandPart", "has\"quote"));
            Assert.Equal("tool \"two words\"", InvokeStatic<string>(runnerType, "FormatCommand", "tool", ImmutableArray.Create("two words")));

            Type cachegenType = typeof(global::QmlSharp.Qt.Tools.QmlCachegen);
            using System.Text.Json.JsonDocument aotDocument = System.Text.Json.JsonDocument.Parse("""
                {
                  "functions": [
                    { "codegenResult": 0 },
                    { "codegenResult": 1 },
                    { "totalFunctions": "3", "compiledFunctions": "2" },
                    { "total": 2, "failed": 1 },
                    { "total": false }
                  ]
                }
                """);
            QmlAotStats stats = InvokeStatic<QmlAotStats>(cachegenType, "ParseAotStatsDocument", aotDocument.RootElement);
            Assert.Equal(7, stats.TotalFunctions);
            Assert.Equal(4, stats.CompiledFunctions);
            Assert.Equal(3, stats.FailedFunctions);
            Assert.Equal(0.0, new QmlAotStats { TotalFunctions = 0, CompiledFunctions = 0, FailedFunctions = 0 }.SuccessRate);
            Assert.Equal("qml", InvokeStatic<string>(cachegenType, "CreateOutputStem", "///"));
            Assert.Equal(96, InvokeStatic<string>(cachegenType, "CreateOutputStem", new string('a', 120)).Length);

            Type diagnosticParserType = typeof(QtDiagnosticParser);
            Assert.Equal(DiagnosticSeverity.Warning, InvokeStatic<DiagnosticSeverity>(diagnosticParserType, "MapSeverity", new object?[] { null }));
            Assert.Equal(DiagnosticSeverity.Warning, InvokeStatic<DiagnosticSeverity>(diagnosticParserType, "MapSeverity", " "));
            Assert.Equal(DiagnosticSeverity.Error, InvokeStatic<DiagnosticSeverity>(diagnosticParserType, "MapSeverity", "fatal"));
            Assert.Equal(DiagnosticSeverity.Error, InvokeStatic<DiagnosticSeverity>(diagnosticParserType, "MapSeverity", "critical"));
            Assert.Equal(DiagnosticSeverity.Warning, InvokeStatic<DiagnosticSeverity>(diagnosticParserType, "MapSeverity", "warn"));
            Assert.Equal(DiagnosticSeverity.Info, InvokeStatic<DiagnosticSeverity>(diagnosticParserType, "MapSeverity", "information"));
            Assert.Equal(DiagnosticSeverity.Hint, InvokeStatic<DiagnosticSeverity>(diagnosticParserType, "MapSeverity", "note"));
            Assert.Equal(DiagnosticSeverity.Disabled, InvokeStatic<DiagnosticSeverity>(diagnosticParserType, "MapSeverity", "disable"));
            Assert.Equal(DiagnosticSeverity.Disabled, InvokeStatic<DiagnosticSeverity>(diagnosticParserType, "MapSeverity", "off"));
            Assert.Equal(DiagnosticSeverity.Warning, InvokeStatic<DiagnosticSeverity>(diagnosticParserType, "MapSeverity", "future"));
            Assert.False(InvokeStatic<bool>(diagnosticParserType, "StartsWithSeverity", "warn", 0, "warning"));
            Assert.False(InvokeStatic<bool>(diagnosticParserType, "IsDecimal", string.Empty));

            Type formatType = typeof(global::QmlSharp.Qt.Tools.QmlFormat);
            Assert.Null(InvokeStatic<string?>(formatType, "MapSemicolonRule", new object?[] { null }));
            Assert.Null(InvokeStatic<string?>(formatType, "MapSemicolonRule", "preserve"));
            Assert.Equal("always", InvokeStatic<string?>(formatType, "MapSemicolonRule", "add"));
            Assert.Equal("always", InvokeStatic<string?>(formatType, "MapSemicolonRule", "always"));
            Assert.Equal("essential", InvokeStatic<string?>(formatType, "MapSemicolonRule", "remove"));
            Assert.Equal("essential", InvokeStatic<string?>(formatType, "MapSemicolonRule", "essential"));
            Assert.Equal("custom", InvokeStatic<string?>(formatType, "MapSemicolonRule", "custom"));
            Assert.Equal("native", InvokeStatic<string>(formatType, "MapNewline", "native"));
            Assert.Equal("windows", InvokeStatic<string>(formatType, "MapNewline", "windows"));
            Assert.Equal("unix", InvokeStatic<string>(formatType, "MapNewline", "unix"));
            Assert.Equal("macos", InvokeStatic<string>(formatType, "MapNewline", "macos"));
            Assert.Equal("future", InvokeStatic<string>(formatType, "MapNewline", "future"));

            Type toolchainType = typeof(QtToolchain);
            Assert.Null(InvokeStatic<string?>(toolchainType, "NormalizePath", "   "));
            Assert.Equal(
                Path.GetFullPath("quoted"),
                InvokeStatic<string?>(toolchainType, "NormalizePath", "\"quoted\""));
        }

        [Fact]
        public void Step0414_EmitterRealToolBridge_StatusIsExplicit()
        {
            string repositoryRoot = FindRepositoryRoot();
            string pipelineTests = Path.Join(
                repositoryRoot,
                "tests",
                "QmlSharp.Qml.Emitter.Tests",
                "Pipeline",
                "EmitPipelineTests.cs");
            string source = File.ReadAllText(pipelineTests);

            Assert.Contains("PL_09_ProcessAsync_WithRealQmlFormat_FormatsOutput", source, StringComparison.Ordinal);
            Assert.Contains("PL_10_ProcessAsync_WithRealQmlLint_ReturnsValidForValidQml", source, StringComparison.Ordinal);
            Assert.Contains("[RequiresQtFact(\"qmlformat\")]", source, StringComparison.Ordinal);
            Assert.Contains("[RequiresQtFact(\"qmllint\")]", source, StringComparison.Ordinal);
            Assert.Contains("QmlSharp.Qt.Tools.QmlFormat", source, StringComparison.Ordinal);
            Assert.Contains("QmlSharp.Qt.Tools.QmlLint", source, StringComparison.Ordinal);
        }

        private static IEnumerable<string> ReadSpecIds(string path)
        {
            string source = File.ReadAllText(path);
            MatchCollection matches = Regex.Matches(
                source,
                @"(?<![A-Z0-9])(TC|TR|QF|QL|QC|QT|IS|QD|QR|RC|REG|QG|DP)[_-]?(\d{3})(?!\d)",
                RegexOptions.CultureInvariant);

            foreach (Match match in matches)
            {
                yield return match.Groups[1].Value + match.Groups[2].Value;
            }
        }

        private static string[] CreateExpectedSpecIds()
        {
            return
            [
                .. CreateRange("TC", 1, 9),
                .. CreateRange("TR", 1, 6),
                .. CreateRange("QF", 1, 16),
                .. CreateRange("QL", 1, 18),
                .. CreateRange("QC", 1, 10),
                .. CreateRange("QT", 1, 5),
                .. CreateRange("IS", 1, 6),
                .. CreateRange("QD", 1, 5),
                .. CreateRange("QR", 1, 7),
                .. CreateRange("RC", 1, 7),
                .. CreateRange("REG", 1, 6),
                .. CreateRange("QG", 1, 11),
                .. CreateRange("DP", 1, 6),
            ];
        }

        private static string[] CreateRange(string prefix, int first, int last)
        {
            return Enumerable
                .Range(first, last - first + 1)
                .Select(value => FormattableString.Invariant($"{prefix}{value:000}"))
                .ToArray();
        }

        private static void AssertParam(string paramName, Action action)
        {
            _ = Assert.Throws<ArgumentNullException>(paramName, action);
        }

        private static T InvokeStatic<T>(Type type, string methodName, params object?[] args)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(type.FullName, methodName);
            return (T)method.Invoke(null, args)!;
        }

        private static T InvokeInstance<T>(object target, string methodName, params object?[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(target.GetType().FullName, methodName);
            return (T)method.Invoke(target, args)!;
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);

            while (directory is not null)
            {
                string solutionPath = Path.Join(directory.FullName, "QmlSharp.slnx");
                if (File.Exists(solutionPath))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not locate QmlSharp repository root.");
        }
    }
}
