namespace QmlSharp.Build.Tests.Infrastructure
{
    public static class MockQtToolServices
    {
        public static ToolResult CreateSuccessfulToolResult(string command = "mock")
        {
            return new ToolResult
            {
                ExitCode = 0,
                Stdout = string.Empty,
                Stderr = string.Empty,
                DurationMs = 0,
                Command = command,
            };
        }
    }

    public sealed class MockQmlFormat : IQmlFormat
    {
        public Task<QmlFormatResult> FormatFileAsync(
            string filePath,
            QmlFormatOptions? options = null,
            CancellationToken ct = default)
        {
            return Task.FromResult(CreateResult());
        }

        public Task<QmlFormatResult> FormatStringAsync(
            string qmlSource,
            QmlFormatOptions? options = null,
            CancellationToken ct = default)
        {
            return Task.FromResult(CreateResult(qmlSource));
        }

        public Task<ImmutableArray<QmlFormatResult>> FormatBatchAsync(
            ImmutableArray<string> filePaths,
            QmlFormatOptions? options = null,
            CancellationToken ct = default)
        {
            ImmutableArray<QmlFormatResult> results = (filePaths.IsDefault ? ImmutableArray<string>.Empty : filePaths)
                .Select(static _ => CreateResult())
                .ToImmutableArray();
            return Task.FromResult(results);
        }

        private static QmlFormatResult CreateResult(string? source = null)
        {
            return new QmlFormatResult
            {
                ToolResult = MockQtToolServices.CreateSuccessfulToolResult("qmlformat"),
                FormattedSource = source,
                HasChanges = false,
            };
        }
    }

    public sealed class MockQmlLint : IQmlLint
    {
        public Task<QmlLintResult> LintFileAsync(
            string filePath,
            QmlLintOptions? options = null,
            CancellationToken ct = default)
        {
            return Task.FromResult(CreateResult());
        }

        public Task<QmlLintResult> LintStringAsync(
            string qmlSource,
            QmlLintOptions? options = null,
            CancellationToken ct = default)
        {
            return Task.FromResult(CreateResult());
        }

        public Task<ImmutableArray<QmlLintResult>> LintBatchAsync(
            ImmutableArray<string> filePaths,
            QmlLintOptions? options = null,
            CancellationToken ct = default)
        {
            ImmutableArray<QmlLintResult> results = (filePaths.IsDefault ? ImmutableArray<string>.Empty : filePaths)
                .Select(static _ => CreateResult())
                .ToImmutableArray();
            return Task.FromResult(results);
        }

        public Task<QmlLintResult> LintModuleAsync(
            string modulePath,
            QmlLintOptions? options = null,
            CancellationToken ct = default)
        {
            return Task.FromResult(CreateResult());
        }

        public Task<ImmutableArray<string>> ListPluginsAsync(CancellationToken ct = default)
        {
            return Task.FromResult(ImmutableArray<string>.Empty);
        }

        private static QmlLintResult CreateResult()
        {
            return new QmlLintResult
            {
                ToolResult = MockQtToolServices.CreateSuccessfulToolResult("qmllint"),
                ErrorCount = 0,
                WarningCount = 0,
                InfoCount = 0,
            };
        }
    }
}
