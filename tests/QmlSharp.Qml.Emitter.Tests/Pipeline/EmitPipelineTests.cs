using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Pipeline
{
    public sealed class EmitPipelineTests
    {
        [Fact]
        [Trait("Category", TestCategories.Pipeline)]
        public async Task PL_01_ProcessAsync_EmitOnly_ReturnsEmitterOutputAndSkipsToolStages()
        {
            QmlDocument document = AstFixtureFactory.MinimalDocument();
            EmitPipelineConfig config = new()
            {
                EnableFormat = false,
                EnableLint = false,
            };
            EmitPipeline pipeline = new(new QmlEmitter(), config);

            PipelineResult result = await pipeline.ProcessAsync(document);

            Assert.True(result.Succeeded);
            Assert.True(result.Valid);
            Assert.Equal("Item {}\n", result.Text);
            Assert.Equal(result.Text, result.EmitResult.Text);
            Assert.Null(result.FormatResult);
            Assert.Null(result.LintResult);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        [Trait("Category", TestCategories.Pipeline)]
        public async Task PL_02_ProcessAsync_FormatEnabled_UsesInjectedFormatterOutput()
        {
            QmlDocument document = AstFixtureFactory.MinimalDocument();
            EmitPipelineConfig config = new()
            {
                EnableFormat = true,
                EnableLint = false,
            };
            FakeQmlFormatter formatter = new(static text => $"formatted:{text}");
            EmitPipeline pipeline = new(new QmlEmitter(), config, formatter: formatter);

            PipelineResult result = await pipeline.ProcessAsync(document);

            Assert.True(result.Succeeded);
            Assert.True(result.Valid);
            Assert.Equal("formatted:Item {}\n", result.Text);
            Assert.NotNull(result.FormatResult);
            Assert.Equal(result.Text, result.FormatResult.Text);
            Assert.Null(result.LintResult);
        }

        [Fact]
        [Trait("Category", TestCategories.Pipeline)]
        public async Task PL_03_ProcessAsync_LintEnabledWithValidResult_PreservesValidLintResult()
        {
            QmlDocument document = AstFixtureFactory.MinimalDocument();
            EmitPipelineConfig config = new()
            {
                EnableFormat = false,
                EnableLint = true,
            };
            FakeQmlLinter linter = new(valid: true);
            EmitPipeline pipeline = new(new QmlEmitter(), config, linter: linter);

            PipelineResult result = await pipeline.ProcessAsync(document);

            Assert.True(result.Succeeded);
            Assert.True(result.Valid);
            Assert.Null(result.FormatResult);
            Assert.NotNull(result.LintResult);
            Assert.True(result.LintResult.Valid);
            Assert.Empty(result.LintResult.Diagnostics);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        [Trait("Category", TestCategories.Pipeline)]
        public async Task PL_04_ProcessAsync_LintEnabledWithInvalidResult_AggregatesDiagnostics()
        {
            QmlDocument document = AstFixtureFactory.MinimalDocument();
            PipelineDiagnostic diagnostic = new()
            {
                Code = "QMLLINT-FAKE",
                Severity = PipelineDiagnosticSeverity.Error,
                Message = "invalid qml",
                File = "Main.qml",
                Line = 1,
                Column = 1,
            };
            EmitPipelineConfig config = new()
            {
                EnableFormat = false,
                EnableLint = true,
            };
            FakeQmlLinter linter = new(valid: false, [diagnostic]);
            EmitPipeline pipeline = new(new QmlEmitter(), config, linter: linter);

            PipelineResult result = await pipeline.ProcessAsync(document);

            Assert.False(result.Succeeded);
            Assert.False(result.Valid);
            Assert.NotNull(result.LintResult);
            Assert.False(result.LintResult.Valid);
            PipelineDiagnostic lintDiagnostic = Assert.Single(result.LintResult.Diagnostics);
            Assert.Same(diagnostic, lintDiagnostic);
            PipelineDiagnostic aggregatedDiagnostic = Assert.Single(result.Diagnostics);
            Assert.Same(diagnostic, aggregatedDiagnostic);
        }

        [Fact]
        [Trait("Category", TestCategories.Pipeline)]
        public async Task PL_05_ProcessAsync_FullPipeline_PopulatesAllStagesInOrder()
        {
            QmlDocument document = AstFixtureFactory.MinimalDocument();
            PipelineDiagnostic formatWarning = new()
            {
                Code = "QMLFORMAT-FAKE",
                Severity = PipelineDiagnosticSeverity.Warning,
                Message = "format warning",
            };
            PipelineDiagnostic lintWarning = new()
            {
                Code = "QMLLINT-FAKE",
                Severity = PipelineDiagnosticSeverity.Warning,
                Message = "lint warning",
            };
            EmitPipelineConfig config = new()
            {
                EnableFormat = true,
                EnableLint = true,
                EnableSourceMap = true,
            };
            FakeQmlFormatter formatter = new(static text => text.Replace("Item", "Rectangle", StringComparison.Ordinal), [formatWarning]);
            FakeQmlLinter linter = new(valid: true, [lintWarning]);
            EmitPipeline pipeline = new(new QmlEmitter(), config, formatter, linter);

            PipelineResult result = await pipeline.ProcessAsync(document);

            Assert.True(result.Succeeded);
            Assert.True(result.Valid);
            Assert.Equal("Rectangle {}\n", result.Text);
            Assert.NotNull(result.EmitResult.SourceMap);
            Assert.NotNull(result.FormatResult);
            Assert.NotNull(result.LintResult);
            Assert.Collection(
                result.Diagnostics,
                diagnostic => Assert.Same(formatWarning, diagnostic),
                diagnostic => Assert.Same(lintWarning, diagnostic));
        }

        [Fact]
        [Trait("Category", TestCategories.Pipeline)]
        public async Task PL_06_ProcessAsync_RecordsPositiveDurationForEveryExecutedStage()
        {
            QmlDocument document = AstFixtureFactory.MinimalDocument();
            EmitPipelineConfig config = new()
            {
                EnableFormat = true,
                EnableLint = true,
            };
            EmitPipeline pipeline = new(new QmlEmitter(), config, new FakeQmlFormatter(), new FakeQmlLinter());

            PipelineResult result = await pipeline.ProcessAsync(document);

            Assert.True(result.TotalDurationMs > 0);
            Assert.True(result.EmitResult.DurationMs > 0);
            Assert.NotNull(result.FormatResult);
            Assert.True(result.FormatResult.DurationMs > 0);
            Assert.NotNull(result.LintResult);
            Assert.True(result.LintResult.DurationMs > 0);
        }

        [Fact]
        [Trait("Category", TestCategories.Pipeline)]
        public async Task ProcessAsync_FormatEnabledWithoutFormatter_ReturnsConfigurationFailure()
        {
            QmlDocument document = AstFixtureFactory.MinimalDocument();
            EmitPipelineConfig config = new()
            {
                EnableFormat = true,
                EnableLint = false,
            };
            EmitPipeline pipeline = new(new QmlEmitter(), config);

            PipelineResult result = await pipeline.ProcessAsync(document);

            Assert.False(result.Succeeded);
            Assert.False(result.Valid);
            Assert.Equal("Item {}\n", result.Text);
            Assert.Null(result.FormatResult);
            Assert.Null(result.LintResult);
            PipelineDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("PIPELINE-CONFIG", diagnostic.Code);
            Assert.Equal(PipelineDiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("Pipeline format stage is enabled but no format service was injected.", diagnostic.Message);
        }

        [Fact]
        [Trait("Category", TestCategories.Pipeline)]
        public async Task ProcessAsync_LintEnabledWithoutLinter_ReturnsConfigurationFailure()
        {
            QmlDocument document = AstFixtureFactory.MinimalDocument();
            EmitPipelineConfig config = new()
            {
                EnableFormat = false,
                EnableLint = true,
            };
            EmitPipeline pipeline = new(new QmlEmitter(), config);

            PipelineResult result = await pipeline.ProcessAsync(document);

            Assert.False(result.Succeeded);
            Assert.False(result.Valid);
            Assert.Equal("Item {}\n", result.Text);
            Assert.Null(result.FormatResult);
            Assert.Null(result.LintResult);
            PipelineDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("PIPELINE-CONFIG", diagnostic.Code);
            Assert.Equal(PipelineDiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("Pipeline lint stage is enabled but no lint service was injected.", diagnostic.Message);
        }

        [Fact]
        [Trait("Category", TestCategories.Pipeline)]
        public async Task PL_07_ProcessBatchAsync_MultiDocumentInput_PreservesInputOrdering()
        {
            ImmutableArray<NamedDocument> documents =
            [
                new NamedDocument
                {
                    Name = "B.qml",
                    Document = AstFixtureFactory.MinimalDocument("Button"),
                },
                new NamedDocument
                {
                    Name = "A.qml",
                    Document = AstFixtureFactory.MinimalDocument("Item"),
                },
                new NamedDocument
                {
                    Name = "C.qml",
                    Document = AstFixtureFactory.MinimalDocument("Rectangle"),
                },
            ];
            EmitPipelineConfig config = new()
            {
                EnableFormat = false,
                EnableLint = false,
            };
            EmitPipeline pipeline = new(new QmlEmitter(), config);

            ImmutableArray<NamedPipelineResult> results = await pipeline.ProcessBatchAsync(documents);

            Assert.Collection(
                results,
                result => Assert.Equal("B.qml", result.Name),
                result => Assert.Equal("A.qml", result.Name),
                result => Assert.Equal("C.qml", result.Name));
            Assert.Equal("Button {}\n", results[0].Result.Text);
            Assert.Equal("Item {}\n", results[1].Result.Text);
            Assert.Equal("Rectangle {}\n", results[2].Result.Text);
        }

        [Fact]
        [Trait("Category", TestCategories.Pipeline)]
        public async Task PL_08_ProcessAsync_CancellationTokenCancelledBeforeStart_ThrowsOperationCanceledException()
        {
            QmlDocument document = AstFixtureFactory.MinimalDocument();
            EmitPipeline pipeline = new(new QmlEmitter());
            using CancellationTokenSource cancellation = new();

            await cancellation.CancelAsync();

            OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(() => pipeline.ProcessAsync(document, cancellation.Token));
            Assert.NotNull(exception);
        }

        [Fact]
        [Trait("Category", TestCategories.Pipeline)]
        public async Task ProcessBatchAsync_WhenOneDocumentFails_ReturnsPerDocumentFailureAndContinues()
        {
            ImmutableArray<NamedDocument> documents =
            [
                new NamedDocument
                {
                    Name = "Valid.qml",
                    Document = AstFixtureFactory.MinimalDocument("Item"),
                },
                new NamedDocument
                {
                    Name = "Invalid.qml",
                    Document = new QmlDocument
                    {
                        RootObject = null!,
                    },
                },
                new NamedDocument
                {
                    Name = "StillRuns.qml",
                    Document = AstFixtureFactory.MinimalDocument("Rectangle"),
                },
            ];
            EmitPipelineConfig config = new()
            {
                EnableFormat = false,
                EnableLint = false,
            };
            EmitPipeline pipeline = new(new QmlEmitter(), config);

            ImmutableArray<NamedPipelineResult> results = await pipeline.ProcessBatchAsync(documents);

            Assert.True(results[0].Result.Succeeded);
            Assert.False(results[1].Result.Succeeded);
            Assert.Equal("PIPELINE-EMIT", Assert.Single(results[1].Result.Diagnostics).Code);
            Assert.True(results[2].Result.Succeeded);
            Assert.Equal("Rectangle {}\n", results[2].Result.Text);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        [Trait("Category", TestCategories.Pipeline)]
        public async Task PL_09_ProcessAsync_WithRealQmlFormat_FormatsAndLintsOutput()
        {
            QmlDocument document = AstFixtureFactory.MinimalDocument();
            EmitPipelineConfig config = new()
            {
                EnableFormat = true,
                EnableLint = false,
            };
            QtQmlFormatPipelineAdapter formatter = new(new QmlSharp.Qt.Tools.QmlFormat());
            EmitPipeline pipeline = new(new QmlEmitter(), config, formatter);

            PipelineResult result = await pipeline.ProcessAsync(document);

            Assert.True(result.Succeeded);
            Assert.True(result.Valid);
            Assert.NotNull(result.FormatResult);
            Assert.Equal(result.FormatResult.Text, result.Text);
            Assert.Empty(result.FormatResult.Diagnostics);
        }

        [Fact(Skip = "Requires 04-qt-tools real qmllint integration in implementation wave 04.")]
        [Trait("Category", TestCategories.RequiresQt)]
        [Trait("Category", TestCategories.Pipeline)]
        public Task PL_10_ProcessAsync_WithRealQmlLint_ReturnsValidForValidQml()
        {
            return Task.CompletedTask;
        }

        private sealed class QtQmlFormatPipelineAdapter : IEmitPipelineFormatter
        {
            private readonly QmlSharp.Qt.Tools.IQmlFormat _formatter;

            public QtQmlFormatPipelineAdapter(QmlSharp.Qt.Tools.IQmlFormat formatter)
            {
                _formatter = formatter;
            }

            public async Task<FormatStageResult> FormatAsync(string documentName, string text, CancellationToken ct = default)
            {
                QmlSharp.Qt.Tools.QmlFormatResult result = await _formatter.FormatStringAsync(text, ct: ct);
                return new FormatStageResult
                {
                    Text = result.FormattedSource ?? text,
                    DurationMs = result.ToolResult.DurationMs,
                    Diagnostics = ConvertDiagnostics(result.Diagnostics),
                };
            }

            private static ImmutableArray<PipelineDiagnostic> ConvertDiagnostics(ImmutableArray<QmlSharp.Qt.Tools.QtDiagnostic> diagnostics)
            {
                ImmutableArray<PipelineDiagnostic>.Builder builder = ImmutableArray.CreateBuilder<PipelineDiagnostic>(diagnostics.Length);
                for (int index = 0; index < diagnostics.Length; index++)
                {
                    QmlSharp.Qt.Tools.QtDiagnostic diagnostic = diagnostics[index];
                    builder.Add(new PipelineDiagnostic
                    {
                        Code = diagnostic.Category,
                        Severity = ConvertSeverity(diagnostic.Severity),
                        Message = diagnostic.Message,
                        File = diagnostic.File,
                        Line = diagnostic.Line,
                        Column = diagnostic.Column,
                    });
                }

                return builder.MoveToImmutable();
            }

            private static PipelineDiagnosticSeverity ConvertSeverity(QmlSharp.Qt.Tools.DiagnosticSeverity severity)
            {
                return severity switch
                {
                    QmlSharp.Qt.Tools.DiagnosticSeverity.Error => PipelineDiagnosticSeverity.Error,
                    QmlSharp.Qt.Tools.DiagnosticSeverity.Warning => PipelineDiagnosticSeverity.Warning,
                    _ => PipelineDiagnosticSeverity.Info,
                };
            }
        }
    }
}
