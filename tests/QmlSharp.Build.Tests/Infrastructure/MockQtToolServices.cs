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
        public ImmutableArray<QmlFormatResult>? BatchResults { get; set; }

        public Exception? BatchException { get; set; }

        public int FormatBatchCallCount { get; private set; }

        public ImmutableArray<string> LastFilePaths { get; private set; } = ImmutableArray<string>.Empty;

        public QmlFormatOptions? LastOptions { get; private set; }

        public Task<QmlFormatResult> FormatFileAsync(
            string filePath,
            QmlFormatOptions? options = null,
            CancellationToken ct = default)
        {
            return Task.FromResult(CreateResult(filePath: filePath));
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
            FormatBatchCallCount++;
            LastFilePaths = filePaths.IsDefault ? ImmutableArray<string>.Empty : filePaths;
            LastOptions = options;

            if (BatchException is not null)
            {
                return Task.FromException<ImmutableArray<QmlFormatResult>>(BatchException);
            }

            if (BatchResults is not null)
            {
                return Task.FromResult(BatchResults.Value);
            }

            ImmutableArray<QmlFormatResult> results = LastFilePaths
                .Select(static path => CreateResult(filePath: path))
                .ToImmutableArray();
            return Task.FromResult(results);
        }

        private static QmlFormatResult CreateResult(string? source = null, string? filePath = null)
        {
            return new QmlFormatResult
            {
                ToolResult = MockQtToolServices.CreateSuccessfulToolResult(CreateCommand("qmlformat", filePath)),
                FormattedSource = source,
                HasChanges = false,
            };
        }

        private static string CreateCommand(string toolName, string? filePath)
        {
            return string.IsNullOrWhiteSpace(filePath) ? toolName : toolName + " " + filePath;
        }
    }

    public sealed class MockQmlLint : IQmlLint
    {
        public ImmutableArray<QmlLintResult>? BatchResults { get; set; }

        public Exception? BatchException { get; set; }

        public int LintBatchCallCount { get; private set; }

        public ImmutableArray<string> LastFilePaths { get; private set; } = ImmutableArray<string>.Empty;

        public QmlLintOptions? LastOptions { get; private set; }

        public Task<QmlLintResult> LintFileAsync(
            string filePath,
            QmlLintOptions? options = null,
            CancellationToken ct = default)
        {
            return Task.FromResult(CreateResult(filePath));
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
            LintBatchCallCount++;
            LastFilePaths = filePaths.IsDefault ? ImmutableArray<string>.Empty : filePaths;
            LastOptions = options;

            if (BatchException is not null)
            {
                return Task.FromException<ImmutableArray<QmlLintResult>>(BatchException);
            }

            if (BatchResults is not null)
            {
                return Task.FromResult(BatchResults.Value);
            }

            ImmutableArray<QmlLintResult> results = LastFilePaths
                .Select(static path => CreateResult(path))
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

        private static QmlLintResult CreateResult(string? filePath = null)
        {
            return new QmlLintResult
            {
                ToolResult = MockQtToolServices.CreateSuccessfulToolResult(CreateCommand("qmllint", filePath)),
                ErrorCount = 0,
                WarningCount = 0,
                InfoCount = 0,
            };
        }

        private static string CreateCommand(string toolName, string? filePath)
        {
            return string.IsNullOrWhiteSpace(filePath) ? toolName : toolName + " " + filePath;
        }
    }
}
