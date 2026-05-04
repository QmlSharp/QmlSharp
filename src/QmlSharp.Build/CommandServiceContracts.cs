#pragma warning disable MA0048

namespace QmlSharp.Build
{
    /// <summary>Service used by the init command shell before templates are implemented.</summary>
    public interface IInitService
    {
        /// <summary>Runs the init command service.</summary>
        Task<CommandServiceResult> InitAsync(
            InitCommandOptions options,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Service used by the clean command shell before real clean behavior is implemented.</summary>
    public interface ICleanService
    {
        /// <summary>Runs the clean command service.</summary>
        Task<CommandServiceResult> CleanAsync(
            CleanCommandOptions options,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Mock shell build pipeline used until Step 08.04 implements real orchestration.</summary>
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

    /// <summary>Mock shell doctor used until Step 08.12 implements real checks.</summary>
    public sealed class CommandShellDoctor : IDoctor
    {
        /// <inheritdoc />
        public Task<ImmutableArray<DoctorCheckResult>> RunAllChecksAsync(QmlSharpConfig? config = null)
        {
            ImmutableArray<DoctorCheckResult> checks = ImmutableArray.Create(
                new DoctorCheckResult(
                    DoctorCheckId.QtInstalled,
                    "Qt SDK",
                    DoctorCheckStatus.Pass,
                    config?.Qt.Dir,
                    null,
                    false),
                new DoctorCheckResult(
                    DoctorCheckId.DotNetVersion,
                    ".NET SDK",
                    DoctorCheckStatus.Pass,
                    Environment.Version.ToString(),
                    null,
                    false));
            return Task.FromResult(checks);
        }

        /// <inheritdoc />
        public Task<DoctorCheckResult> RunCheckAsync(string checkId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(checkId);

            DoctorCheckResult result = new(
                checkId,
                checkId,
                DoctorCheckStatus.Pass,
                null,
                null,
                false);
            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public Task<ImmutableArray<DoctorFixResult>> AutoFixAsync(ImmutableArray<DoctorCheckResult> failedChecks)
        {
            ImmutableArray<DoctorFixResult> fixes = failedChecks
                .Select(static check => new DoctorFixResult(check.CheckId, false, "No command-shell fix is available."))
                .ToImmutableArray();
            return Task.FromResult(fixes);
        }
    }

    /// <summary>Mock shell init service used until Step 08.11 implements templates.</summary>
    public sealed class CommandShellInitService : IInitService
    {
        /// <inheritdoc />
        public Task<CommandServiceResult> InitAsync(
            InitCommandOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            cancellationToken.ThrowIfCancellationRequested();

            if (Directory.Exists(options.TargetDir) &&
                Directory.EnumerateFileSystemEntries(options.TargetDir).Any())
            {
                BuildDiagnostic diagnostic = CommandDiagnostics.CreateCommandDiagnostic(
                    "targetDir",
                    $"Target directory '{options.TargetDir}' is not empty.");
                return Task.FromResult(CommandServiceResult.Failed(
                    CommandResultStatus.ConfigOrCommandError,
                    diagnostic.Message,
                    ImmutableArray.Create(diagnostic)));
            }

            return Task.FromResult(CommandServiceResult.Succeeded("Init command shell completed."));
        }
    }

    /// <summary>Mock shell clean service used until Step 08.11 implements artifact deletion.</summary>
    public sealed class CommandShellCleanService : ICleanService
    {
        /// <inheritdoc />
        public Task<CommandServiceResult> CleanAsync(
            CleanCommandOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(CommandServiceResult.Succeeded("Clean command shell completed."));
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
