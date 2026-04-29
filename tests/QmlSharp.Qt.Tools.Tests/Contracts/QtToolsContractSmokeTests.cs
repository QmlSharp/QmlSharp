using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.Contracts
{
    [Trait("Category", TestCategories.Smoke)]
    public sealed class QtToolsContractSmokeTests
    {
        [Fact]
        public void Options_Defaults_MatchApiDesign()
        {
            QtToolchainConfig toolchain = new();
            QmlFormatOptions format = new();
            QmlLintOptions lint = new();
            QmlCachegenOptions cachegen = new();
            QmltcOptions qmltc = new();
            QmlImportScanOptions importScan = new();
            QmlDomOptions dom = new();
            QmlRunOptions run = new();
            RccOptions rcc = new();
            TypeRegistrarOptions registrar = new();
            ToolRunnerOptions runner = new();
            QualityGateOptions gate = new();

            Assert.Null(toolchain.QtDir);
            Assert.Empty(toolchain.ImportPaths);
            Assert.Equal(TimeSpan.FromSeconds(30), toolchain.Timeout);
            Assert.Null(toolchain.Cwd);
            Assert.Null(toolchain.Env);

            Assert.Equal(4, format.IndentWidth);
            Assert.False(format.UseTabs);
            Assert.False(format.Normalize);
            Assert.False(format.SortImports);
            Assert.Null(format.SemicolonRule);
            Assert.Null(format.Newline);
            Assert.Equal(0, format.ColumnWidth);
            Assert.False(format.IgnoreSettings);
            Assert.False(format.Force);
            Assert.False(format.InPlace);

            Assert.True(lint.JsonOutput);
            Assert.Null(lint.WarningLevels);
            Assert.Equal(0, lint.MaxWarnings);
            Assert.False(lint.Silent);
            Assert.Empty(lint.ImportPaths);
            Assert.False(lint.Bare);
            Assert.False(lint.Fix);

            Assert.False(cachegen.BytecodeOnly);
            Assert.False(cachegen.WarningsAsErrors);
            Assert.False(cachegen.Verbose);
            Assert.Empty(cachegen.ImportPaths);
            Assert.Null(cachegen.OutputDir);

            Assert.Null(qmltc.Namespace);
            Assert.Null(qmltc.Module);
            Assert.Null(qmltc.ExportMacro);
            Assert.Null(qmltc.OutputHeader);
            Assert.Null(qmltc.OutputSource);

            Assert.Null(importScan.RootPath);
            Assert.Empty(importScan.ImportPaths);
            Assert.Empty(importScan.ExcludeDirs);

            Assert.False(dom.AstMode);
            Assert.Empty(dom.FilterFields);
            Assert.False(dom.NoDependencies);

            Assert.Equal("offscreen", run.Platform);
            Assert.Equal(QmlAppType.Auto, run.AppType);
            Assert.Equal(TimeSpan.FromSeconds(5), run.Timeout);
            Assert.Equal(TimeSpan.FromSeconds(2), run.StableRunPeriod);

            Assert.False(rcc.BinaryMode);
            Assert.False(rcc.NoCompress);
            Assert.False(rcc.PythonOutput);
            Assert.Null(rcc.OutputFile);

            Assert.Null(registrar.ForeignTypesFile);
            Assert.Null(registrar.ModuleImportUri);
            Assert.Null(registrar.MajorVersion);
            Assert.Null(registrar.Namespace);
            Assert.Null(registrar.OutputFile);

            Assert.Equal(TimeSpan.FromSeconds(30), runner.Timeout);
            Assert.Null(runner.Cwd);
            Assert.Null(runner.Stdin);
            Assert.Null(runner.Env);

            Assert.Null(gate.OnProgress);
            Assert.Empty(gate.ImportPaths);
            Assert.True(gate.EarlyStop);
        }

        [Fact]
        public void Results_SuccessProperties_DelegateToToolResult()
        {
            ToolResult success = CreateToolResult(0);
            ToolResult failure = CreateToolResult(1);

            Assert.True(success.Success);
            Assert.False(failure.Success);
            Assert.True(new QmlFormatResult { ToolResult = success, HasChanges = false }.Success);
            Assert.False(new QmlFormatResult { ToolResult = failure, HasChanges = false }.Success);
            Assert.True(new QmlLintResult { ToolResult = success, ErrorCount = 0, WarningCount = 0, InfoCount = 0 }.Success);
            Assert.True(new QmlCachegenResult { ToolResult = success }.Success);
            Assert.True(new QmltcResult { ToolResult = success }.Success);
            Assert.True(new QmlImportScanResult { ToolResult = success }.Success);
            Assert.True(new QmlDomResult { ToolResult = success }.Success);
            Assert.True(new RccResult { ToolResult = success }.Success);
            Assert.True(new TypeRegistrarResult { ToolResult = success }.Success);
            Assert.True(new QmlAotStats { TotalFunctions = 4, CompiledFunctions = 3, FailedFunctions = 1 }.SuccessRate > 0);
        }

        [Fact]
        public void Interfaces_PublicSurface_ContainsAllStep0401Contracts()
        {
            Type[] interfaces =
            [
                typeof(IQtToolchain),
                typeof(IToolRunner),
                typeof(IQmlFormat),
                typeof(IQmlLint),
                typeof(IQmlCachegen),
                typeof(IQmltc),
                typeof(IQmlImportScanner),
                typeof(IQmlDom),
                typeof(IQmlRunner),
                typeof(IRcc),
                typeof(IQmlTypeRegistrar),
                typeof(IQualityGate),
                typeof(IQtDiagnosticParser),
            ];

            Assert.All(interfaces, static contract =>
            {
                Assert.True(contract.IsInterface);
                Assert.Equal("QmlSharp.Qt.Tools", contract.Namespace);
                Assert.True(contract.IsPublic);
            });
        }

        [Fact]
        public void Errors_PreserveApiDesignPayloads()
        {
            QtInstallationNotFoundError installationError = new(
                "Qt was not found.",
                ["explicit: missing", "QT_DIR: missing"]);
            QtToolNotFoundError toolError = new("qmllint", @"C:\Qt\bin\qmllint.exe");
            QtToolTimeoutError timeoutError = new(
                "qmlformat",
                TimeSpan.FromSeconds(30),
                "partial stdout",
                "partial stderr");

            Assert.Equal(["explicit: missing", "QT_DIR: missing"], installationError.AttemptedSteps.ToArray());
            Assert.Equal("qmllint", toolError.ToolName);
            Assert.Equal(@"C:\Qt\bin\qmllint.exe", toolError.ExpectedPath);
            Assert.Contains("qmllint", toolError.Message, StringComparison.Ordinal);
            Assert.Equal("qmlformat", timeoutError.ToolName);
            Assert.Equal(TimeSpan.FromSeconds(30), timeoutError.Timeout);
            Assert.Equal("partial stdout", timeoutError.PartialStdout);
            Assert.Equal("partial stderr", timeoutError.PartialStderr);
        }

        private static ToolResult CreateToolResult(int exitCode)
        {
            return new ToolResult
            {
                ExitCode = exitCode,
                Stdout = string.Empty,
                Stderr = string.Empty,
                DurationMs = 1,
                Command = "tool",
            };
        }
    }
}
