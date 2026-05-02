using Microsoft.CodeAnalysis;
using QmlSharp.Compiler.Tests.Fixtures;
using QmlSharp.Qml.Ast;
using QmlSharp.Qml.Emitter;
using QmlSharp.Qt.Tools;
using QmlSharp.Registry.Querying;
using CompilerSeverity = QmlSharp.Compiler.DiagnosticSeverity;
using QtSeverity = QmlSharp.Qt.Tools.DiagnosticSeverity;

namespace QmlSharp.Compiler.Tests.Pipeline
{
    public sealed class QtValidationTests
    {
        private static readonly IRegistryQuery Registry = RegistryFixture.CreateQtQuickAndControlsRegistry();

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void QtValidation_WhenFlagsAreFalse_SkipsQtTools()
        {
            using ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            FakeQmlFormat format = new();
            FakeQmlLint lint = new();
            ICompiler compiler = CreateCompiler(context, qmlFormat: format, qmlLint: lint);

            CompilationResult result = compiler.Compile(CompilerTestFixtures.DefaultOptions);

            Assert.True(result.Success);
            Assert.Equal(0, format.FormatStringCalls);
            Assert.Equal(0, lint.LintStringCalls);
            Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Code == DiagnosticCodes.QtValidationFailed);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void QtValidation_FormatQml_InvokesQmlFormatAndUsesFormattedText()
        {
            using ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            FakeQmlFormat format = new()
            {
                FormatStringResult = CreateFormatResult(
                    success: true,
                    formattedSource: "import QtQuick\n\nItem {}\n"),
            };
            FakeQmlLint lint = new();
            ICompiler compiler = CreateCompiler(context, qmlFormat: format, qmlLint: lint);
            CompilerOptions options = CompilerTestFixtures.DefaultOptions with { FormatQml = true };

            CompilationUnit unit = Assert.Single(compiler.Compile(options).Units);

            Assert.True(unit.Success);
            Assert.Equal(1, format.FormatStringCalls);
            Assert.Equal(0, lint.LintStringCalls);
            Assert.Equal("import QtQuick\n\nItem {}\n", unit.QmlText);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void QtValidation_LintQml_MergesQtDiagnosticsWithGeneratedQmlContext()
        {
            using ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            FakeQmlLint lint = new()
            {
                LintStringResult = CreateLintResult(
                    success: false,
                    new QtDiagnostic
                    {
                        File = "<string>",
                        Line = 3,
                        Column = 9,
                        Severity = QtSeverity.Error,
                        Message = "Unknown property countt.",
                        Category = "missing-property",
                    }),
            };
            ICompiler compiler = CreateCompiler(context, qmlLint: lint);
            CompilerOptions options = CompilerTestFixtures.DefaultOptions with { LintQml = true };

            CompilationResult result = compiler.Compile(options);

            Assert.False(result.Success);
            CompilerDiagnostic diagnostic = Assert.Single(result.Diagnostics, static candidate => candidate.Code == DiagnosticCodes.QtValidationFailed);
            Assert.Equal(CompilerSeverity.Error, diagnostic.Severity);
            Assert.Equal("QtValidation", diagnostic.Phase);
            Assert.EndsWith(Path.Join("qml", "QmlSharp", "TestApp", "CounterView.qml"), diagnostic.Location?.FilePath, StringComparison.Ordinal);
            Assert.Equal(3, diagnostic.Location?.Line);
            Assert.Equal(9, diagnostic.Location?.Column);
            Assert.Contains("qmllint [missing-property]: Unknown property countt.", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void QtValidation_InvalidQtDiscovery_BecomesCompilerDiagnostic()
        {
            using ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            FakeQmlFormat format = new()
            {
                FormatStringException = new QtInstallationNotFoundError(
                    "Qt was not found.",
                    ImmutableArray.Create("QT_DIR: C:/missing (root directory not found)")),
            };
            ICompiler compiler = CreateCompiler(context, qmlFormat: format);
            CompilerOptions options = CompilerTestFixtures.DefaultOptions with { FormatQml = true };

            CompilationResult result = compiler.Compile(options);

            Assert.False(result.Success);
            CompilerDiagnostic diagnostic = Assert.Single(result.Diagnostics, static candidate => candidate.Code == DiagnosticCodes.QtValidationFailed);
            Assert.Equal(CompilerSeverity.Error, diagnostic.Severity);
            Assert.Contains("qmlformat: QtInstallationNotFoundError: Qt was not found.", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void QtValidation_FormatThenLint_LintsFormattedQml()
        {
            using ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            FakeQmlFormat format = new()
            {
                FormatStringResult = CreateFormatResult(success: true, formattedSource: "import QtQuick\nItem {}\n"),
            };
            FakeQmlLint lint = new();
            ICompiler compiler = CreateCompiler(context, qmlFormat: format, qmlLint: lint);
            CompilerOptions options = CompilerTestFixtures.DefaultOptions with { FormatQml = true, LintQml = true };

            CompilationResult result = compiler.Compile(options);

            Assert.True(result.Success);
            Assert.Equal("import QtQuick\nItem {}\n", lint.LastLintSource);
        }

        [RequiresQtFact("qmlformat")]
        [Trait("Category", TestCategories.RequiresQt)]
        public void QtValidation_RequiresQt_FormatQml_FormatsWithRealQmlformat()
        {
            using ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            ICompiler compiler = CreateCompiler(context, qmlEmitter: new ValidQtOnlyEmitter());
            CompilerOptions options = CompilerTestFixtures.DefaultOptions with { FormatQml = true };

            CompilationResult result = compiler.Compile(options);

            Assert.True(result.Success);
            Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Code == DiagnosticCodes.QtValidationFailed && diagnostic.Severity == CompilerSeverity.Error);
            _ = Assert.Single(result.Units);
        }

        [RequiresQtFact("qmllint")]
        [Trait("Category", TestCategories.RequiresQt)]
        public void QtValidation_RequiresQt_LintQml_LintsWithRealQmllint()
        {
            using ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            ICompiler compiler = CreateCompiler(context, qmlEmitter: new ValidQtOnlyEmitter());
            CompilerOptions options = CompilerTestFixtures.DefaultOptions with { LintQml = true };

            CompilationResult result = compiler.Compile(options);

            Assert.True(result.Success);
            Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Code == DiagnosticCodes.QtValidationFailed && diagnostic.Severity == CompilerSeverity.Error);
            _ = Assert.Single(result.Units);
        }

        private static ICompiler CreateCompiler(
            ProjectContext context,
            IQmlEmitter? qmlEmitter = null,
            IQmlFormat? qmlFormat = null,
            IQmlLint? qmlLint = null)
        {
            return new QmlSharp.Compiler.QmlCompiler(
                new StaticAnalyzer(context),
                new ViewModelExtractor(),
                static () => new IdAllocator(),
                new DslTransformer(),
                new ImportResolver(),
                new PostProcessor(),
                qmlEmitter ?? new QmlEmitter(),
                Registry,
                new SourceMapManager(),
                new EventBindingsBuilder(),
                new CompilerOutputWriter(),
                qmlFormat,
                qmlLint);
        }

        private static QmlFormatResult CreateFormatResult(bool success, string? formattedSource = null, params QtDiagnostic[] diagnostics)
        {
            return new QmlFormatResult
            {
                ToolResult = CreateToolResult(success),
                FormattedSource = formattedSource,
                HasChanges = formattedSource is not null,
                Diagnostics = diagnostics.ToImmutableArray(),
            };
        }

        private static QmlLintResult CreateLintResult(bool success, params QtDiagnostic[] diagnostics)
        {
            ImmutableArray<QtDiagnostic> immutableDiagnostics = diagnostics.ToImmutableArray();
            return new QmlLintResult
            {
                ToolResult = CreateToolResult(success),
                Diagnostics = immutableDiagnostics,
                ErrorCount = immutableDiagnostics.Count(static diagnostic => diagnostic.Severity == QtSeverity.Error),
                WarningCount = immutableDiagnostics.Count(static diagnostic => diagnostic.Severity == QtSeverity.Warning),
                InfoCount = immutableDiagnostics.Count(static diagnostic => diagnostic.Severity is QtSeverity.Info or QtSeverity.Hint),
            };
        }

        private static ToolResult CreateToolResult(bool success)
        {
            return new ToolResult
            {
                ExitCode = success ? 0 : 1,
                Stdout = string.Empty,
                Stderr = string.Empty,
                DurationMs = 1,
                Command = "qt-tool",
            };
        }

        private sealed class StaticAnalyzer : ICSharpAnalyzer
        {
            private readonly ProjectContext projectContext;
            private readonly CSharpAnalyzer inner = new();

            public StaticAnalyzer(ProjectContext context)
            {
                projectContext = context;
            }

            public ProjectContext CreateProjectContext(CompilerOptions options)
            {
                return projectContext;
            }

            public ImmutableArray<DiscoveredView> DiscoverViews(ProjectContext context)
            {
                return inner.DiscoverViews(context);
            }

            public ImmutableArray<DiscoveredViewModel> DiscoverViewModels(ProjectContext context)
            {
                return inner.DiscoverViewModels(context);
            }

            public ImmutableArray<DiscoveredImport> DiscoverImports(ProjectContext context, string filePath)
            {
                return inner.DiscoverImports(context, filePath);
            }

            public SemanticModel GetSemanticModel(ProjectContext context, string filePath)
            {
                return inner.GetSemanticModel(context, filePath);
            }
        }

        private sealed class FakeQmlFormat : IQmlFormat
        {
            public int FormatStringCalls { get; private set; }

            public QmlFormatResult FormatStringResult { get; init; } = CreateFormatResult(success: true, formattedSource: null);

            public Exception? FormatStringException { get; init; }

            public Task<QmlFormatResult> FormatFileAsync(string filePath, QmlFormatOptions? options = null, CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }

            public Task<QmlFormatResult> FormatStringAsync(string qmlSource, QmlFormatOptions? options = null, CancellationToken ct = default)
            {
                FormatStringCalls++;
                if (FormatStringException is not null)
                {
                    throw FormatStringException;
                }

                return Task.FromResult(FormatStringResult);
            }

            public Task<ImmutableArray<QmlFormatResult>> FormatBatchAsync(ImmutableArray<string> filePaths, QmlFormatOptions? options = null, CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class FakeQmlLint : IQmlLint
        {
            public int LintStringCalls { get; private set; }

            public string? LastLintSource { get; private set; }

            public QmlLintResult LintStringResult { get; init; } = CreateLintResult(success: true);

            public Task<QmlLintResult> LintFileAsync(string filePath, QmlLintOptions? options = null, CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }

            public Task<QmlLintResult> LintStringAsync(string qmlSource, QmlLintOptions? options = null, CancellationToken ct = default)
            {
                LintStringCalls++;
                LastLintSource = qmlSource;
                return Task.FromResult(LintStringResult);
            }

            public Task<ImmutableArray<QmlLintResult>> LintBatchAsync(ImmutableArray<string> filePaths, QmlLintOptions? options = null, CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }

            public Task<QmlLintResult> LintModuleAsync(string modulePath, QmlLintOptions? options = null, CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }

            public Task<ImmutableArray<string>> ListPluginsAsync(CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class ValidQtOnlyEmitter : IQmlEmitter
        {
            public string Emit(QmlDocument document, EmitOptions? options = null)
            {
                return """
                    import QtQuick

                    Item {
                        width: 10
                        height: 10
                    }

                    """;
            }

            public string EmitFragment(AstNode node, FragmentEmitOptions? options = null)
            {
                throw new NotSupportedException();
            }

            public string EmitFragment(BindingValue value, FragmentEmitOptions? options = null)
            {
                throw new NotSupportedException();
            }

            public EmitResult EmitWithSourceMap(QmlDocument document, EmitOptions? options = null)
            {
                throw new NotSupportedException();
            }
        }
    }
}
