using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Errors
{
    public sealed class EmitErrorTests
    {
        [Fact]
        [Trait("Category", TestCategories.Errors)]
        public void EH_01_EmitNullDocument_ThrowsArgumentNullException()
        {
            IQmlEmitter emitter = new QmlEmitter();

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => emitter.Emit(null!));

            Assert.Equal("document", exception.ParamName);
        }

        [Fact]
        [Trait("Category", TestCategories.Errors)]
        public void EH_02_EmitFragmentNullNode_ThrowsArgumentNullException()
        {
            IQmlEmitter emitter = new QmlEmitter();

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => emitter.EmitFragment((AstNode)null!));

            Assert.Equal("node", exception.ParamName);
        }

        [Fact]
        [Trait("Category", TestCategories.Errors)]
        public void EmitFragment_NullBindingValue_ThrowsArgumentNullException()
        {
            IQmlEmitter emitter = new QmlEmitter();

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => emitter.EmitFragment((BindingValue)null!));

            Assert.Equal("value", exception.ParamName);
        }

        [Fact]
        [Trait("Category", TestCategories.Errors)]
        public void Emit_InvalidOptions_FailsBeforeWritingOutput()
        {
            IQmlEmitter emitter = new QmlEmitter();
            QmlDocument document = AstFixtureFactory.MinimalDocument();
            EmitOptions options = new()
            {
                IndentSize = 0,
            };

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => emitter.Emit(document, options));

            Assert.Equal("options", exception.ParamName);
        }

        [Fact]
        [Trait("Category", TestCategories.Errors)]
        public void EmitFragment_UnsupportedAstNodeKind_ThrowsNotSupportedExceptionWithKind()
        {
            IQmlEmitter emitter = new QmlEmitter();
            AstNode unsupported = new UnsupportedAstNode();

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => emitter.EmitFragment(unsupported));

            Assert.Contains("Unsupported AST node kind", exception.Message, StringComparison.Ordinal);
            Assert.Contains(NodeKind.Comment.ToString(), exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Errors)]
        public void Emit_InconsistentAstStateWithoutRootObject_ThrowsInvalidOperationException()
        {
            IQmlEmitter emitter = new QmlEmitter();
            QmlDocument document = new()
            {
                RootObject = null!,
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => emitter.Emit(document));

            Assert.Equal("QML documents require a root object.", exception.Message);
        }

        [Fact]
        [Trait("Category", TestCategories.Errors)]
        public void EmitWithSourceMap_InvalidSourceSpan_ThrowsInvalidOperationException()
        {
            IQmlEmitter emitter = new QmlEmitter();
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Span = new SourceSpan(
                        new SourcePosition(2, 1, 10),
                        new SourcePosition(1, 1, 9)),
                },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => emitter.EmitWithSourceMap(document));

            Assert.Contains("Source map source spans", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Errors)]
        public void SourceMap_InvalidLineOrColumnQueries_ThrowArgumentOutOfRangeException()
        {
            IQmlEmitter emitter = new QmlEmitter();
            ISourceMap sourceMap = emitter.EmitWithSourceMap(AstFixtureFactory.MinimalDocument()).SourceMap;

            _ = Assert.Throws<ArgumentOutOfRangeException>(() => sourceMap.GetNodesAtLine(0));
            _ = Assert.Throws<ArgumentOutOfRangeException>(() => sourceMap.GetInnermostNodeAt(0, 1));
            _ = Assert.Throws<ArgumentOutOfRangeException>(() => sourceMap.GetInnermostNodeAt(1, 0));
        }

        [Fact]
        [Trait("Category", TestCategories.Errors)]
        public void Writer_IndentationUnderflow_ThrowsInvalidOperationException()
        {
            QmlWriter writer = new(ResolvedEmitOptions.From(null));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(writer.Dedent);

            Assert.Equal("Cannot decrease indentation below zero.", exception.Message);
        }

        [Fact]
        [Trait("Category", TestCategories.Errors)]
        public async Task Pipeline_EmitStageFailure_ComposesDiagnosticAndSkipsLaterStages()
        {
            QmlDocument document = new()
            {
                RootObject = null!,
            };
            EmitPipelineConfig config = new()
            {
                EnableFormat = true,
                EnableLint = true,
            };
            EmitPipeline pipeline = new(new QmlEmitter(), config, new ThrowingFormatter(), new ThrowingLinter());

            PipelineResult result = await pipeline.ProcessAsync(document);

            Assert.False(result.Succeeded);
            Assert.False(result.Valid);
            Assert.Empty(result.Text);
            Assert.Null(result.FormatResult);
            Assert.Null(result.LintResult);
            PipelineDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("PIPELINE-EMIT", diagnostic.Code);
            Assert.Contains("QML documents require a root object.", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Errors)]
        public async Task Pipeline_FormatStageFailure_ComposesDiagnosticAndSkipsLint()
        {
            QmlDocument document = AstFixtureFactory.MinimalDocument();
            EmitPipelineConfig config = new()
            {
                EnableFormat = true,
                EnableLint = true,
            };
            EmitPipeline pipeline = new(new QmlEmitter(), config, new ThrowingFormatter(), new ThrowingLinter());

            PipelineResult result = await pipeline.ProcessAsync(document);

            Assert.False(result.Succeeded);
            Assert.False(result.Valid);
            Assert.Equal("Item {}\n", result.Text);
            Assert.NotNull(result.FormatResult);
            Assert.Null(result.LintResult);
            PipelineDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("PIPELINE-FORMAT", diagnostic.Code);
            Assert.Contains("formatter failed", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Errors)]
        public async Task Pipeline_LintStageFailure_ComposesInvalidLintResult()
        {
            QmlDocument document = AstFixtureFactory.MinimalDocument();
            EmitPipelineConfig config = new()
            {
                EnableFormat = false,
                EnableLint = true,
            };
            EmitPipeline pipeline = new(new QmlEmitter(), config, linter: new ThrowingLinter());

            PipelineResult result = await pipeline.ProcessAsync(document);

            Assert.False(result.Succeeded);
            Assert.False(result.Valid);
            Assert.Equal("Item {}\n", result.Text);
            Assert.NotNull(result.LintResult);
            Assert.False(result.LintResult.Valid);
            PipelineDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("PIPELINE-LINT", diagnostic.Code);
            Assert.Contains("linter failed", diagnostic.Message, StringComparison.Ordinal);
        }

        private sealed record UnsupportedAstNode : AstNode
        {
            public override NodeKind Kind => NodeKind.Comment;
        }

        private sealed class ThrowingFormatter : IEmitPipelineFormatter
        {
            public Task<FormatStageResult> FormatAsync(string documentName, string text, CancellationToken ct = default)
            {
                throw new InvalidOperationException("formatter failed");
            }
        }

        private sealed class ThrowingLinter : IEmitPipelineLinter
        {
            public Task<LintStageResult> LintAsync(string documentName, string text, CancellationToken ct = default)
            {
                throw new InvalidOperationException("linter failed");
            }
        }
    }
}
