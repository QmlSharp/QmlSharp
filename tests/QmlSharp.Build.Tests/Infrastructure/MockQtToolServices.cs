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

    public sealed class MockQtToolchain : IQtToolchain
    {
        private readonly QtInstallation? _installation;
        private readonly Exception? _exception;

        public MockQtToolchain(QtInstallation? installation)
        {
            _installation = installation;
        }

        public MockQtToolchain(Exception exception)
        {
            _exception = exception;
        }

        public int DiscoverCallCount { get; private set; }

        public QtToolchainConfig? LastConfig { get; private set; }

        public QtInstallation? Installation { get; private set; }

        public Task<QtInstallation> DiscoverAsync(QtToolchainConfig? config = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            DiscoverCallCount++;
            LastConfig = config;

            if (_exception is not null)
            {
                throw _exception;
            }

            if (_installation is null)
            {
                throw new InvalidOperationException("Mock Qt installation was not configured.");
            }

            Installation = _installation;
            return Task.FromResult(_installation);
        }

        public Task<ToolAvailability> CheckToolsAsync(CancellationToken ct = default)
        {
            throw new NotSupportedException("Config loader tests do not call CheckToolsAsync.");
        }

        public Task<ToolInfo> GetToolInfoAsync(string toolName, CancellationToken ct = default)
        {
            throw new NotSupportedException("Config loader tests do not call GetToolInfoAsync.");
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
