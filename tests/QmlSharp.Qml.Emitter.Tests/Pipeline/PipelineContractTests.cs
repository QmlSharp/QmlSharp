using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Pipeline
{
    public sealed class PipelineContractTests
    {
        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PipelineConfig_Defaults_MatchApiDesignContract()
        {
            EmitPipelineConfig config = new();

            Assert.NotNull(config.EmitOptions);
            Assert.True(config.EnableFormat);
            Assert.True(config.EnableLint);
            Assert.False(config.EnableSourceMap);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PipelineDtos_CarryNamedDocumentAndStageResults()
        {
            QmlDocument document = AstFixtureFactory.MinimalDocument();
            NamedDocument namedDocument = new()
            {
                Name = "Main.qml",
                Document = document,
            };
            FormatStageResult formatResult = new FakeQmlFormatter().Format("Item {}");
            LintStageResult lintResult = new FakeQmlLinter().LintInvalid("bad qml");
            PipelineResult pipelineResult = new()
            {
                Text = formatResult.Text,
                Valid = false,
                EmitResult = new EmitStageResult
                {
                    Text = "Item {}",
                    DurationMs = 0.1,
                },
                FormatResult = formatResult,
                LintResult = lintResult,
                TotalDurationMs = 0.3,
            };
            NamedPipelineResult namedResult = new()
            {
                Name = namedDocument.Name,
                Result = pipelineResult,
            };

            Assert.Equal("Main.qml", namedDocument.Name);
            Assert.Same(document, namedDocument.Document);
            Assert.Equal("Item {}", namedResult.Result.Text);
            Assert.False(namedResult.Result.Valid);
            Assert.NotNull(namedResult.Result.FormatResult);
            Assert.NotNull(namedResult.Result.LintResult);
            Assert.Same(formatResult, namedResult.Result.FormatResult);
            Assert.Same(lintResult, namedResult.Result.LintResult);
            PipelineDiagnostic diagnostic = Assert.Single(namedResult.Result.LintResult.Diagnostics);
            Assert.Equal(PipelineDiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("bad qml", diagnostic.Message);
        }
    }
}
