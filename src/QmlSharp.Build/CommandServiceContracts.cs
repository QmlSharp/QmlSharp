#pragma warning disable MA0048

namespace QmlSharp.Build
{
    /// <summary>Service used by the init command shell.</summary>
    public interface IInitService
    {
        /// <summary>Runs the init command service.</summary>
        Task<CommandServiceResult> InitAsync(
            InitCommandOptions options,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Service used by the clean command shell.</summary>
    public interface ICleanService
    {
        /// <summary>Runs the clean command service.</summary>
        Task<CommandServiceResult> CleanAsync(
            CleanCommandOptions options,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Simple command-shell build pipeline retained for command-layer tests.</summary>
    public sealed class CommandShellBuildPipeline : IBuildPipeline
    {
        private Action<BuildProgress>? _progress;

        /// <summary>Number of full build calls.</summary>
        public int BuildCallCount { get; private set; }

        /// <summary>Last build context received by the shell pipeline.</summary>
        public BuildContext? LastContext { get; private set; }

        /// <inheritdoc />
        public Task<BuildResult> BuildAsync(BuildContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            BuildCallCount++;
            LastContext = context;
            _progress?.Invoke(new BuildProgress(BuildPhase.ConfigLoading, "Command shell build pipeline.", 1, 1));
            return Task.FromResult(CreateSuccessfulBuildResult());
        }

        /// <inheritdoc />
        public Task<BuildResult> BuildPhasesAsync(
            BuildContext context,
            ImmutableArray<BuildPhase> phases,
            CancellationToken cancellationToken = default)
        {
            return BuildAsync(context, cancellationToken);
        }

        /// <inheritdoc />
        public void OnProgress(Action<BuildProgress> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _progress += callback;
        }

        private static BuildResult CreateSuccessfulBuildResult()
        {
            return new BuildResult
            {
                Success = true,
                PhaseResults = ImmutableArray<PhaseResult>.Empty,
                Diagnostics = ImmutableArray<BuildDiagnostic>.Empty,
                Stats = new BuildStats(TimeSpan.Zero, 0, 0, 0, 0, false),
            };
        }
    }

    /// <summary>Compatibility wrapper for the default filesystem-backed doctor service.</summary>
    public sealed class CommandShellDoctor : IDoctor
    {
        private readonly Doctor _inner;

        /// <summary>Create a doctor for the current directory.</summary>
        public CommandShellDoctor()
            : this(null)
        {
        }

        /// <summary>Create a doctor for a project directory.</summary>
        public CommandShellDoctor(string? projectDir)
        {
            _inner = new Doctor(projectDir);
        }

        /// <inheritdoc />
        public Task<ImmutableArray<DoctorCheckResult>> RunAllChecksAsync(QmlSharpConfig? config = null)
        {
            return _inner.RunAllChecksAsync(config);
        }

        /// <inheritdoc />
        public Task<DoctorCheckResult> RunCheckAsync(string checkId)
        {
            return _inner.RunCheckAsync(checkId);
        }

        /// <inheritdoc />
        public Task<ImmutableArray<DoctorFixResult>> AutoFixAsync(ImmutableArray<DoctorCheckResult> failedChecks)
        {
            return _inner.AutoFixAsync(failedChecks);
        }
    }

    /// <summary>Compatibility wrapper for the default filesystem-backed init service.</summary>
    public sealed class CommandShellInitService : IInitService
    {
        private readonly InitService _inner = new();

        /// <inheritdoc />
        public Task<CommandServiceResult> InitAsync(
            InitCommandOptions options,
            CancellationToken cancellationToken = default)
        {
            return _inner.InitAsync(options, cancellationToken);
        }
    }

    /// <summary>Compatibility wrapper for the default filesystem-backed clean service.</summary>
    public sealed class CommandShellCleanService : ICleanService
    {
        private readonly CleanService _inner = new();

        /// <inheritdoc />
        public Task<CommandServiceResult> CleanAsync(
            CleanCommandOptions options,
            CancellationToken cancellationToken = default)
        {
            return _inner.CleanAsync(options, cancellationToken);
        }
    }

    internal static class CommandDiagnostics
    {
        public static BuildDiagnostic CreateCommandDiagnostic(string field, string message)
        {
            return new BuildDiagnostic(
                BuildDiagnosticCode.ConfigValidationError,
                BuildDiagnosticSeverity.Error,
                message,
                BuildPhase.ConfigLoading,
                field);
        }
    }
}

#pragma warning restore MA0048
