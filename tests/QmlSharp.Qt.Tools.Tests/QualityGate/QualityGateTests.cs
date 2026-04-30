using System.Collections.Immutable;
using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.QualityGate
{
    [Trait("Category", TestCategories.QualityGate)]
    public sealed class QualityGateTests
    {
        [Fact]
        public async Task QG001_SyntaxLevel_WithValidQml_PassesWithNoDiagnostics()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            FakeTools tools = FakeTools.Create();
            tools.Format.Enqueue(FormatResult(success: true, durationMs: 3));
            global::QmlSharp.Qt.Tools.QualityGate gate = tools.CreateGate();

            QualityGateResult result = await gate.RunAsync(file.Path, QualityGateLevel.Syntax);

            Assert.True(result.Passed);
            Assert.Equal(QualityGateLevel.Syntax, result.CompletedLevel);
            Assert.Null(result.FailedAtLevel);
            Assert.Empty(result.Diagnostics);
            Assert.Equal(3, result.LevelDurationMs[QualityGateLevel.Syntax]);
            _ = Assert.Single(tools.Format.FileCalls);
            Assert.Empty(tools.Lint.FileCalls);
            Assert.Empty(tools.Cachegen.FileCalls);
            Assert.Empty(tools.Runner.FileCalls);
        }

        [Fact]
        public async Task QG002_SyntaxLevel_WithSyntaxError_FailsAtSyntax()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {\n");
            QtDiagnostic diagnostic = Diagnostic(file.Path, "Expected token");
            FakeTools tools = FakeTools.Create();
            tools.Format.Enqueue(FormatResult(success: false, durationMs: 4, diagnostics: [diagnostic]));
            global::QmlSharp.Qt.Tools.QualityGate gate = tools.CreateGate();

            QualityGateResult result = await gate.RunAsync(file.Path, QualityGateLevel.Syntax);

            Assert.False(result.Passed);
            Assert.Equal(QualityGateLevel.Syntax, result.CompletedLevel);
            Assert.Equal(QualityGateLevel.Syntax, result.FailedAtLevel);
            Assert.Equal([diagnostic], result.Diagnostics.ToArray());
            Assert.Equal([diagnostic], result.LevelDiagnostics[QualityGateLevel.Syntax].ToArray());
        }

        [Fact]
        public async Task QG003_LintLevel_WithCleanQml_RunsSyntaxAndLint()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            FakeTools tools = FakeTools.Create();
            tools.Format.Enqueue(FormatResult(success: true, durationMs: 2));
            tools.Lint.Enqueue(LintResult(durationMs: 5));
            global::QmlSharp.Qt.Tools.QualityGate gate = tools.CreateGate();

            QualityGateResult result = await gate.RunAsync(file.Path, QualityGateLevel.Lint);

            Assert.True(result.Passed);
            Assert.Equal(QualityGateLevel.Lint, result.CompletedLevel);
            Assert.Equal([QualityGateLevel.Syntax, QualityGateLevel.Lint], result.LevelDurationMs.Keys.Order().ToArray());
            _ = Assert.Single(tools.Format.FileCalls);
            _ = Assert.Single(tools.Lint.FileCalls);
            Assert.Empty(tools.Cachegen.FileCalls);
        }

        [Fact]
        public async Task QG004_LintLevel_WithLintWarning_FailsAtLintAndAggregatesDiagnostics()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            QtDiagnostic diagnostic = Diagnostic(file.Path, "Unqualified access", DiagnosticSeverity.Warning);
            FakeTools tools = FakeTools.Create();
            tools.Format.Enqueue(FormatResult(success: true, durationMs: 2));
            tools.Lint.Enqueue(LintResult(durationMs: 7, diagnostics: [diagnostic]));
            global::QmlSharp.Qt.Tools.QualityGate gate = tools.CreateGate();

            QualityGateResult result = await gate.RunAsync(file.Path, QualityGateLevel.Lint);

            Assert.False(result.Passed);
            Assert.Equal(QualityGateLevel.Lint, result.CompletedLevel);
            Assert.Equal(QualityGateLevel.Lint, result.FailedAtLevel);
            Assert.Equal([diagnostic], result.Diagnostics.ToArray());
            Assert.Equal([diagnostic], result.LevelDiagnostics[QualityGateLevel.Lint].ToArray());
            Assert.Empty(tools.Cachegen.FileCalls);
        }

        [Fact]
        public async Task QG005_CompileLevel_RunsSyntaxLintAndCompile()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            FakeTools tools = FakeTools.Create();
            tools.Format.Enqueue(FormatResult(success: true, durationMs: 2));
            tools.Lint.Enqueue(LintResult(durationMs: 3));
            tools.Cachegen.Enqueue(CachegenResult(success: true, durationMs: 4));
            global::QmlSharp.Qt.Tools.QualityGate gate = tools.CreateGate();

            QualityGateResult result = await gate.RunAsync(
                file.Path,
                QualityGateLevel.Compile,
                new QualityGateOptions { ImportPaths = [@"C:\qml-imports"] });

            Assert.True(result.Passed);
            Assert.Equal(QualityGateLevel.Compile, result.CompletedLevel);
            Assert.Equal([QualityGateLevel.Syntax, QualityGateLevel.Lint, QualityGateLevel.Compile], result.LevelDurationMs.Keys.Order().ToArray());
            Assert.Equal(@"C:\qml-imports", Assert.Single(tools.Lint.OptionsCalls).ImportPaths.Single());
            Assert.Equal(@"C:\qml-imports", Assert.Single(tools.Cachegen.OptionsCalls).ImportPaths.Single());
            Assert.False(Directory.Exists(Assert.Single(tools.Cachegen.OptionsCalls).OutputDir));
        }

        [Fact]
        public async Task QG006_FullLevel_RunsAllFourLevels()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            FakeTools tools = FakeTools.Create();
            tools.Format.Enqueue(FormatResult(success: true, durationMs: 2));
            tools.Lint.Enqueue(LintResult(durationMs: 3));
            tools.Cachegen.Enqueue(CachegenResult(success: true, durationMs: 4));
            tools.Runner.Enqueue(RunResult(passed: true, durationMs: 5));
            global::QmlSharp.Qt.Tools.QualityGate gate = tools.CreateGate();

            QualityGateResult result = await gate.RunAsync(file.Path, QualityGateLevel.Full);

            Assert.True(result.Passed);
            Assert.Equal(QualityGateLevel.Full, result.CompletedLevel);
            Assert.Equal(4, result.LevelDurationMs.Count);
            _ = Assert.Single(tools.Runner.FileCalls);
        }

        [Fact]
        public async Task QG007_RunString_UsesOneTemporaryFileAndCleansItUp()
        {
            FakeTools tools = FakeTools.Create();
            tools.Format.Enqueue(FormatResult(success: true, durationMs: 2));
            tools.Lint.Enqueue(LintResult(durationMs: 3));
            tools.Cachegen.Enqueue(CachegenResult(success: true, durationMs: 4));
            global::QmlSharp.Qt.Tools.QualityGate gate = tools.CreateGate();

            QualityGateResult result = await gate.RunStringAsync(
                "import QtQuick\nItem {}\n",
                QualityGateLevel.Compile);

            string tempPath = Assert.Single(tools.Format.FileCalls);
            Assert.True(result.Passed);
            Assert.Equal(tempPath, Assert.Single(tools.Lint.FileCalls));
            Assert.Equal(tempPath, Assert.Single(tools.Cachegen.FileCalls));
            Assert.False(File.Exists(tempPath));
        }

        [Fact]
        public async Task QG008_RunBatch_AggregatesFileResultsAndPreservesInputOrder()
        {
            using TemporaryQmlFile first = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            using TemporaryQmlFile second = TemporaryQmlFile.Create("import QtQuick\nBadType {}\n");
            using TemporaryQmlFile third = TemporaryQmlFile.Create("import QtQuick\nAnotherBadType {}\n");
            QtDiagnostic secondDiagnostic = Diagnostic(second.Path, "bad second", DiagnosticSeverity.Warning);
            QtDiagnostic thirdDiagnostic = Diagnostic(third.Path, "bad third", DiagnosticSeverity.Warning);
            FakeTools tools = FakeTools.Create();
            tools.Format.Enqueue(FormatResult(success: true, durationMs: 1));
            tools.Format.Enqueue(FormatResult(success: true, durationMs: 1));
            tools.Format.Enqueue(FormatResult(success: true, durationMs: 1));
            tools.Lint.Enqueue(LintResult(durationMs: 2));
            tools.Lint.Enqueue(LintResult(durationMs: 2, diagnostics: [secondDiagnostic]));
            tools.Lint.Enqueue(LintResult(durationMs: 2, diagnostics: [thirdDiagnostic]));
            global::QmlSharp.Qt.Tools.QualityGate gate = tools.CreateGate();

            QualityGateResult result = await gate.RunBatchAsync([first.Path, second.Path, third.Path], QualityGateLevel.Lint);

            Assert.False(result.Passed);
            Assert.Equal(QualityGateLevel.Lint, result.FailedAtLevel);
            Assert.Equal([first.Path, second.Path, third.Path], result.FileResults.Select(static item => item.FilePath).ToArray());
            Assert.Equal([true, false, false], result.FileResults.Select(static item => item.Passed).ToArray());
            Assert.Equal([secondDiagnostic, thirdDiagnostic], result.Diagnostics.ToArray());
            Assert.Equal(3, result.LevelDurationMs[QualityGateLevel.Syntax]);
            Assert.Equal(6, result.LevelDurationMs[QualityGateLevel.Lint]);
        }

        [Fact]
        public async Task QG009_OnProgress_FiresAfterEachLevelInOrder()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            FakeTools tools = FakeTools.Create();
            tools.Format.Enqueue(FormatResult(success: true, durationMs: 2));
            tools.Lint.Enqueue(LintResult(durationMs: 3));
            tools.Cachegen.Enqueue(CachegenResult(success: true, durationMs: 4));
            List<QualityGateProgress> progress = [];
            global::QmlSharp.Qt.Tools.QualityGate gate = tools.CreateGate();

            QualityGateResult result = await gate.RunAsync(
                file.Path,
                QualityGateLevel.Compile,
                new QualityGateOptions { OnProgress = progress.Add });

            Assert.True(result.Passed);
            Assert.Equal([QualityGateLevel.Syntax, QualityGateLevel.Lint, QualityGateLevel.Compile], progress.Select(static item => item.Level).ToArray());
            Assert.Equal([true, true, true], progress.Select(static item => item.Passed).ToArray());
            Assert.Equal([2, 3, 4], progress.Select(static item => item.DurationMs).ToArray());
            Assert.All(progress, static item => Assert.Equal(0, item.DiagnosticCount));
        }

        [Fact]
        public async Task QG010_EarlyStopTrue_WhenSyntaxFails_DoesNotCallLaterLevels()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {\n");
            FakeTools tools = FakeTools.Create();
            tools.Format.Enqueue(FormatResult(success: false, durationMs: 2, diagnostics: [Diagnostic(file.Path, "bad syntax")]));
            global::QmlSharp.Qt.Tools.QualityGate gate = tools.CreateGate();

            QualityGateResult result = await gate.RunAsync(file.Path, QualityGateLevel.Full);

            Assert.False(result.Passed);
            Assert.Equal(QualityGateLevel.Syntax, result.CompletedLevel);
            Assert.Equal(QualityGateLevel.Syntax, result.FailedAtLevel);
            _ = Assert.Single(tools.Format.FileCalls);
            Assert.Empty(tools.Lint.FileCalls);
            Assert.Empty(tools.Cachegen.FileCalls);
            Assert.Empty(tools.Runner.FileCalls);
        }

        [Fact]
        public async Task QG011_Durations_ArePositiveAndTotalEqualsLevelSum()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            FakeTools tools = FakeTools.Create();
            tools.Format.Enqueue(FormatResult(success: true, durationMs: 3));
            tools.Lint.Enqueue(LintResult(durationMs: 5));
            tools.Cachegen.Enqueue(CachegenResult(success: true, durationMs: 7));
            global::QmlSharp.Qt.Tools.QualityGate gate = tools.CreateGate();

            QualityGateResult result = await gate.RunAsync(file.Path, QualityGateLevel.Compile);

            Assert.All(result.LevelDurationMs.Values, static duration => Assert.True(duration > 0));
            Assert.Equal(15, result.TotalDurationMs);
            Assert.Equal(result.TotalDurationMs, result.LevelDurationMs.Values.Sum());
        }

        [Fact]
        public async Task EarlyStopTrue_WhenLintFails_DoesNotCallCompileOrFull()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {}\n");
            FakeTools tools = FakeTools.Create();
            tools.Format.Enqueue(FormatResult(success: true, durationMs: 2));
            tools.Lint.Enqueue(LintResult(durationMs: 3, diagnostics: [Diagnostic(file.Path, "lint failed", DiagnosticSeverity.Warning)]));
            global::QmlSharp.Qt.Tools.QualityGate gate = tools.CreateGate();

            QualityGateResult result = await gate.RunAsync(file.Path, QualityGateLevel.Full);

            Assert.False(result.Passed);
            Assert.Equal(QualityGateLevel.Lint, result.CompletedLevel);
            Assert.Equal(QualityGateLevel.Lint, result.FailedAtLevel);
            Assert.Empty(tools.Cachegen.FileCalls);
            Assert.Empty(tools.Runner.FileCalls);
        }

        [Fact]
        public async Task EarlyStopFalse_ContinuesThroughRequestedLevelAndKeepsFirstFailure()
        {
            using TemporaryQmlFile file = TemporaryQmlFile.Create("import QtQuick\nItem {\n");
            FakeTools tools = FakeTools.Create();
            tools.Format.Enqueue(FormatResult(success: false, durationMs: 2, diagnostics: [Diagnostic(file.Path, "syntax failed")]));
            tools.Lint.Enqueue(LintResult(durationMs: 3));
            tools.Cachegen.Enqueue(CachegenResult(success: true, durationMs: 4));
            global::QmlSharp.Qt.Tools.QualityGate gate = tools.CreateGate();

            QualityGateResult result = await gate.RunAsync(
                file.Path,
                QualityGateLevel.Compile,
                new QualityGateOptions { EarlyStop = false });

            Assert.False(result.Passed);
            Assert.Equal(QualityGateLevel.Compile, result.CompletedLevel);
            Assert.Equal(QualityGateLevel.Syntax, result.FailedAtLevel);
            _ = Assert.Single(tools.Lint.FileCalls);
            _ = Assert.Single(tools.Cachegen.FileCalls);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QualityGate_SyntaxLevel_RunsRealTool()
        {
            global::QmlSharp.Qt.Tools.QualityGate gate = new();

            QualityGateResult result = await gate.RunAsync(FixturePath("valid.qml"), QualityGateLevel.Syntax);

            Assert.True(result.Passed);
            Assert.Equal(QualityGateLevel.Syntax, result.CompletedLevel);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QualityGate_LintLevel_RunsRealTools()
        {
            global::QmlSharp.Qt.Tools.QualityGate gate = new();

            QualityGateResult result = await gate.RunAsync(FixturePath("valid.qml"), QualityGateLevel.Lint);

            Assert.True(result.Passed);
            Assert.Equal(QualityGateLevel.Lint, result.CompletedLevel);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QualityGate_CompileLevel_RunsRealTools()
        {
            global::QmlSharp.Qt.Tools.QualityGate gate = new();

            QualityGateResult result = await gate.RunAsync(FixturePath("valid.qml"), QualityGateLevel.Compile);

            Assert.True(result.Passed);
            Assert.Equal(QualityGateLevel.Compile, result.CompletedLevel);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_QualityGate_FullLevel_RunsRealTools()
        {
            global::QmlSharp.Qt.Tools.QualityGate gate = new();

            QualityGateResult result = await gate.RunAsync(FixturePath("runner-window.qml"), QualityGateLevel.Full);

            Assert.True(result.Passed);
            Assert.Equal(QualityGateLevel.Full, result.CompletedLevel);
        }

        private static QmlFormatResult FormatResult(
            bool success,
            long durationMs,
            ImmutableArray<QtDiagnostic> diagnostics = default)
        {
            return new QmlFormatResult
            {
                ToolResult = ToolResult("qmlformat", success, durationMs),
                FormattedSource = success ? "Item {}\n" : null,
                HasChanges = false,
                Diagnostics = NormalizeDiagnostics(diagnostics),
            };
        }

        private static QmlLintResult LintResult(
            long durationMs,
            ImmutableArray<QtDiagnostic> diagnostics = default)
        {
            ImmutableArray<QtDiagnostic> normalizedDiagnostics = NormalizeDiagnostics(diagnostics);
            int errorCount = normalizedDiagnostics.Count(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            int warningCount = normalizedDiagnostics.Count(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);
            return new QmlLintResult
            {
                ToolResult = ToolResult("qmllint", exitCode: errorCount > 0 ? 1 : 0, durationMs),
                Diagnostics = normalizedDiagnostics,
                ErrorCount = errorCount,
                WarningCount = warningCount,
                InfoCount = normalizedDiagnostics.Count(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Info or DiagnosticSeverity.Hint),
            };
        }

        private static QmlCachegenResult CachegenResult(
            bool success,
            long durationMs,
            ImmutableArray<QtDiagnostic> diagnostics = default)
        {
            return new QmlCachegenResult
            {
                ToolResult = ToolResult("qmlcachegen", success, durationMs),
                Diagnostics = NormalizeDiagnostics(diagnostics),
            };
        }

        private static QmlRunResult RunResult(
            bool passed,
            long durationMs,
            ImmutableArray<QtDiagnostic> diagnostics = default)
        {
            return new QmlRunResult
            {
                ToolResult = ToolResult("qml", exitCode: passed ? -1 : 1, durationMs),
                Passed = passed,
                RuntimeErrors = NormalizeDiagnostics(diagnostics),
                AutoKilled = passed,
            };
        }

        private static ToolResult ToolResult(string command, bool success, long durationMs)
        {
            return ToolResult(command, success ? 0 : 1, durationMs);
        }

        private static ToolResult ToolResult(string command, int exitCode, long durationMs)
        {
            return new ToolResult
            {
                ExitCode = exitCode,
                Stdout = string.Empty,
                Stderr = string.Empty,
                DurationMs = durationMs,
                Command = command,
            };
        }

        private static QtDiagnostic Diagnostic(
            string filePath,
            string message,
            DiagnosticSeverity severity = DiagnosticSeverity.Error)
        {
            return new QtDiagnostic
            {
                File = filePath,
                Line = 1,
                Column = 1,
                Severity = severity,
                Message = message,
            };
        }

        private static ImmutableArray<QtDiagnostic> NormalizeDiagnostics(ImmutableArray<QtDiagnostic> diagnostics)
        {
            return diagnostics.IsDefault ? [] : diagnostics;
        }

        private static string FixturePath(string fileName)
        {
            return Path.Join(AppContext.BaseDirectory, "Fixtures", "qml", fileName);
        }

        private sealed class FakeTools
        {
            private FakeTools()
            {
            }

            public FakeQmlFormat Format { get; } = new();

            public FakeQmlLint Lint { get; } = new();

            public FakeQmlCachegen Cachegen { get; } = new();

            public FakeQmlRunner Runner { get; } = new();

            public static FakeTools Create()
            {
                return new FakeTools();
            }

            public global::QmlSharp.Qt.Tools.QualityGate CreateGate()
            {
                return new global::QmlSharp.Qt.Tools.QualityGate(Format, Lint, Cachegen, Runner);
            }
        }

        private sealed class FakeQmlFormat : IQmlFormat
        {
            private readonly Queue<QmlFormatResult> _results = [];

            public List<string> FileCalls { get; } = [];

            public void Enqueue(QmlFormatResult result)
            {
                _results.Enqueue(result);
            }

            public Task<QmlFormatResult> FormatFileAsync(
                string filePath,
                QmlFormatOptions? options = null,
                CancellationToken ct = default)
            {
                FileCalls.Add(filePath);
                return Task.FromResult(_results.Dequeue());
            }

            public Task<QmlFormatResult> FormatStringAsync(
                string qmlSource,
                QmlFormatOptions? options = null,
                CancellationToken ct = default)
            {
                throw new NotSupportedException("QualityGate should use a single temp file and call file wrappers.");
            }

            public Task<ImmutableArray<QmlFormatResult>> FormatBatchAsync(
                ImmutableArray<string> filePaths,
                QmlFormatOptions? options = null,
                CancellationToken ct = default)
            {
                throw new NotSupportedException("QualityGate should preserve batch ordering itself.");
            }
        }

        private sealed class FakeQmlLint : IQmlLint
        {
            private readonly Queue<QmlLintResult> _results = [];

            public List<string> FileCalls { get; } = [];

            public List<QmlLintOptions> OptionsCalls { get; } = [];

            public void Enqueue(QmlLintResult result)
            {
                _results.Enqueue(result);
            }

            public Task<QmlLintResult> LintFileAsync(
                string filePath,
                QmlLintOptions? options = null,
                CancellationToken ct = default)
            {
                FileCalls.Add(filePath);
                OptionsCalls.Add(options ?? new QmlLintOptions());
                return Task.FromResult(_results.Dequeue());
            }

            public Task<QmlLintResult> LintStringAsync(
                string qmlSource,
                QmlLintOptions? options = null,
                CancellationToken ct = default)
            {
                throw new NotSupportedException("QualityGate should use a single temp file and call file wrappers.");
            }

            public Task<ImmutableArray<QmlLintResult>> LintBatchAsync(
                ImmutableArray<string> filePaths,
                QmlLintOptions? options = null,
                CancellationToken ct = default)
            {
                throw new NotSupportedException("QualityGate should preserve batch ordering itself.");
            }

            public Task<QmlLintResult> LintModuleAsync(
                string modulePath,
                QmlLintOptions? options = null,
                CancellationToken ct = default)
            {
                throw new NotSupportedException("QualityGate should lint files, not modules.");
            }

            public Task<ImmutableArray<string>> ListPluginsAsync(CancellationToken ct = default)
            {
                throw new NotSupportedException("QualityGate should not list lint plugins.");
            }
        }

        private sealed class FakeQmlCachegen : IQmlCachegen
        {
            private readonly Queue<QmlCachegenResult> _results = [];

            public List<string> FileCalls { get; } = [];

            public List<QmlCachegenOptions> OptionsCalls { get; } = [];

            public void Enqueue(QmlCachegenResult result)
            {
                _results.Enqueue(result);
            }

            public Task<QmlCachegenResult> CompileFileAsync(
                string filePath,
                QmlCachegenOptions? options = null,
                CancellationToken ct = default)
            {
                FileCalls.Add(filePath);
                OptionsCalls.Add(options ?? new QmlCachegenOptions());
                if (!string.IsNullOrWhiteSpace(options?.OutputDir))
                {
                    File.WriteAllText(Path.Join(options.OutputDir, "generated.cpp"), "// generated");
                }

                return Task.FromResult(_results.Dequeue());
            }

            public Task<QmlCachegenResult> CompileStringAsync(
                string qmlSource,
                QmlCachegenOptions? options = null,
                CancellationToken ct = default)
            {
                throw new NotSupportedException("QualityGate should use a single temp file and call file wrappers.");
            }

            public Task<QmlCachegenBatchResult> CompileBatchAsync(
                ImmutableArray<string> filePaths,
                QmlCachegenOptions? options = null,
                CancellationToken ct = default)
            {
                throw new NotSupportedException("QualityGate should preserve batch ordering itself.");
            }
        }

        private sealed class FakeQmlRunner : IQmlRunner
        {
            private readonly Queue<QmlRunResult> _results = [];

            public List<string> FileCalls { get; } = [];

            public void Enqueue(QmlRunResult result)
            {
                _results.Enqueue(result);
            }

            public Task<QmlRunResult> RunFileAsync(
                string filePath,
                QmlRunOptions? options = null,
                CancellationToken ct = default)
            {
                FileCalls.Add(filePath);
                return Task.FromResult(_results.Dequeue());
            }

            public Task<QmlRunResult> RunStringAsync(
                string qmlSource,
                QmlRunOptions? options = null,
                CancellationToken ct = default)
            {
                throw new NotSupportedException("QualityGate should use a single temp file and call file wrappers.");
            }

            public Task<ImmutableArray<string>> ListConfigurationsAsync(CancellationToken ct = default)
            {
                throw new NotSupportedException("QualityGate should not list runner configurations.");
            }
        }

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
                    "qmlsharp-qualitygate-test-" + Guid.NewGuid().ToString("N") + ".qml");
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
