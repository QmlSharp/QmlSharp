#pragma warning disable MA0048

namespace QmlSharp.Build
{
    /// <summary>The dotnet qmlsharp build command shell.</summary>
    public sealed class BuildCommand
    {
        private readonly IConfigLoader _configLoader;
        private readonly IBuildPipeline _buildPipeline;
        private readonly ICommandOutput _output;

        /// <summary>Create a build command shell.</summary>
        public BuildCommand(IConfigLoader configLoader, IBuildPipeline buildPipeline, ICommandOutput output)
        {
            ArgumentNullException.ThrowIfNull(configLoader);
            ArgumentNullException.ThrowIfNull(buildPipeline);
            ArgumentNullException.ThrowIfNull(output);

            _configLoader = configLoader;
            _buildPipeline = buildPipeline;
            _output = output;
        }

        /// <summary>Executes the build command shell.</summary>
        public async Task<int> ExecuteAsync(
            BuildCommandOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                QmlSharpConfig config = _configLoader.Load(options.ProjectDir);
                CommandServiceResult? validation = ValidateBuildContext(config, options);
                if (validation is not null)
                {
                    return CommandResultFormatter.WriteResult("build", validation, _output, options.Json, options.DryRun);
                }

                BuildContext context = CreateBuildContext(config, options);
                if (options.DryRun)
                {
                    CommandServiceResult dryRunResult = CommandServiceResult.Succeeded(
                        "Build dry run completed. No output was written.") with
                    {
                        Stats = new BuildStats(TimeSpan.Zero, 0, 0, 0, 0, false),
                    };
                    return CommandResultFormatter.WriteResult("build", dryRunResult, _output, options.Json, dryRun: true);
                }

                BuildResult result = await _buildPipeline.BuildAsync(context, cancellationToken).ConfigureAwait(false);
                CommandServiceResult commandResult = FromBuildResult(result);
                return CommandResultFormatter.WriteResult("build", commandResult, _output, options.Json);
            }
            catch (OperationCanceledException)
            {
                return WriteCancellation("build", options.Json);
            }
            catch (ConfigParseException ex)
            {
                CommandServiceResult result = CommandServiceResult.Failed(
                    CommandResultStatus.ConfigOrCommandError,
                    ex.Message,
                    ex.Diagnostics);
                return CommandResultFormatter.WriteResult("build", result, _output, options.Json, options.DryRun);
            }
        }

        private static BuildContext CreateBuildContext(QmlSharpConfig config, BuildCommandOptions options)
        {
            string qtDir = config.Qt.Dir ?? string.Empty;
            return new BuildContext
            {
                Config = config,
                ProjectDir = Path.GetFullPath(options.ProjectDir),
                OutputDir = config.OutDir,
                QtDir = qtDir,
                ForceRebuild = options.Force,
                LibraryMode = options.Library,
                DryRun = options.DryRun,
                FileFilter = options.Files,
            };
        }

        private static CommandServiceResult? ValidateBuildContext(QmlSharpConfig config, BuildCommandOptions options)
        {
            if (string.IsNullOrWhiteSpace(config.Qt.Dir))
            {
                BuildDiagnostic diagnostic = CommandDiagnostics.CreateCommandDiagnostic(
                    "qt.dir",
                    "Qt directory is required for build command execution.");
                return CommandServiceResult.Failed(
                    CommandResultStatus.ConfigOrCommandError,
                    diagnostic.Message,
                    ImmutableArray.Create(diagnostic));
            }

            if (!options.Library && string.IsNullOrWhiteSpace(config.Entry))
            {
                BuildDiagnostic diagnostic = CommandDiagnostics.CreateCommandDiagnostic(
                    "entry",
                    "Entry is required for application builds. Set entry in qmlsharp.json or pass --library.");
                return CommandServiceResult.Failed(
                    CommandResultStatus.ConfigOrCommandError,
                    diagnostic.Message,
                    ImmutableArray.Create(diagnostic));
            }

            return null;
        }

        private static CommandServiceResult FromBuildResult(BuildResult result)
        {
            return new CommandServiceResult
            {
                Success = result.Success,
                Status = result.Success ? CommandResultStatus.Success : CommandResultStatus.BuildError,
                Message = result.Success ? "Build completed." : "Build failed.",
                Diagnostics = result.Diagnostics,
                Stats = result.Stats,
            };
        }

        private int WriteCancellation(string command, bool json)
        {
            CommandServiceResult result = CommandServiceResult.Failed(
                CommandResultStatus.Cancelled,
                "Command was cancelled.",
                ImmutableArray<BuildDiagnostic>.Empty);
            return CommandResultFormatter.WriteResult(command, result, _output, json);
        }
    }

    /// <summary>The dotnet qmlsharp dev command shell.</summary>
    public sealed class DevCommand
    {
        private readonly IConfigLoader _configLoader;
        private readonly IDevSession _devSession;
        private readonly ICommandOutput _output;

        /// <summary>Create a dev command shell.</summary>
        public DevCommand(IConfigLoader configLoader, IDevSession devSession, ICommandOutput output)
        {
            ArgumentNullException.ThrowIfNull(configLoader);
            ArgumentNullException.ThrowIfNull(devSession);
            ArgumentNullException.ThrowIfNull(output);

            _configLoader = configLoader;
            _devSession = devSession;
            _output = output;
        }

        /// <summary>Executes the dev command shell.</summary>
        public async Task<int> ExecuteAsync(DevCommandOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                QmlSharpConfig config = _configLoader.Load(options.ProjectDir);
                string? entry = string.IsNullOrWhiteSpace(options.Entry) ? config.Entry : options.Entry;
                if (string.IsNullOrWhiteSpace(entry))
                {
                    BuildDiagnostic diagnostic = CommandDiagnostics.CreateCommandDiagnostic(
                        "entry",
                        "Dev requires an entry from qmlsharp.json or --entry.");
                    CommandServiceResult invalid = CommandServiceResult.Failed(
                        CommandResultStatus.ConfigOrCommandError,
                        diagnostic.Message,
                        ImmutableArray.Create(diagnostic));
                    return CommandResultFormatter.WriteResult("dev", invalid, _output, json: false);
                }

                await _devSession.StartAsync(options with { Entry = entry }, cancellationToken).ConfigureAwait(false);
                CommandServiceResult result = CommandServiceResult.Succeeded("Dev command shell completed.");
                return CommandResultFormatter.WriteResult("dev", result, _output, json: false);
            }
            catch (OperationCanceledException)
            {
                return WriteCancellation("dev");
            }
            catch (ConfigParseException ex)
            {
                CommandServiceResult result = CommandServiceResult.Failed(
                    CommandResultStatus.ConfigOrCommandError,
                    ex.Message,
                    ex.Diagnostics);
                return CommandResultFormatter.WriteResult("dev", result, _output, json: false);
            }
        }

        private int WriteCancellation(string command)
        {
            CommandServiceResult result = CommandServiceResult.Failed(
                CommandResultStatus.Cancelled,
                "Command was cancelled.",
                ImmutableArray<BuildDiagnostic>.Empty);
            return CommandResultFormatter.WriteResult(command, result, _output, json: false);
        }
    }

    /// <summary>The dotnet qmlsharp doctor command shell.</summary>
    public sealed class DoctorCommand
    {
        private readonly IDoctor _doctor;
        private readonly ICommandOutput _output;

        /// <summary>Create a doctor command shell.</summary>
        public DoctorCommand(IDoctor doctor, ICommandOutput output)
        {
            ArgumentNullException.ThrowIfNull(doctor);
            ArgumentNullException.ThrowIfNull(output);

            _doctor = doctor;
            _output = output;
        }

        /// <summary>Executes the doctor command shell.</summary>
        public async Task<int> ExecuteAsync(
            DoctorCommandOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                ImmutableArray<DoctorCheckResult> checks = await _doctor.RunAllChecksAsync().ConfigureAwait(false);
                ImmutableArray<DoctorCheckResult> failedChecks = checks
                    .Where(static check => check.Status == DoctorCheckStatus.Fail)
                    .ToImmutableArray();
                if (!failedChecks.IsDefaultOrEmpty && options.Fix)
                {
                    ImmutableArray<DoctorFixResult> fixes = await _doctor.AutoFixAsync(failedChecks).ConfigureAwait(false);
                    if (fixes.All(static fix => fix.Fixed))
                    {
                        failedChecks = ImmutableArray<DoctorCheckResult>.Empty;
                    }
                }

                CommandServiceResult result = failedChecks.IsDefaultOrEmpty
                    ? CommandServiceResult.Succeeded("Doctor checks passed.")
                    : CommandServiceResult.Failed(
                        CommandResultStatus.BuildError,
                        "Doctor checks failed.",
                        CreateDoctorDiagnostics(failedChecks));
                return CommandResultFormatter.WriteResult("doctor", result, _output, json: false);
            }
            catch (OperationCanceledException)
            {
                CommandServiceResult result = CommandServiceResult.Failed(
                    CommandResultStatus.Cancelled,
                    "Command was cancelled.",
                    ImmutableArray<BuildDiagnostic>.Empty);
                return CommandResultFormatter.WriteResult("doctor", result, _output, json: false);
            }
        }

        private static ImmutableArray<BuildDiagnostic> CreateDoctorDiagnostics(
            ImmutableArray<DoctorCheckResult> failedChecks)
        {
            ImmutableArray<BuildDiagnostic>.Builder diagnostics =
                ImmutableArray.CreateBuilder<BuildDiagnostic>(failedChecks.Length);
            foreach (DoctorCheckResult check in failedChecks)
            {
                diagnostics.Add(new BuildDiagnostic(
                    BuildDiagnosticCode.InternalError,
                    BuildDiagnosticSeverity.Error,
                    $"{check.CheckId}: {check.Detail ?? check.Description}",
                    null,
                    check.CheckId));
            }

            return diagnostics.ToImmutable();
        }
    }

    /// <summary>The dotnet qmlsharp init command shell.</summary>
    public sealed class InitCommand
    {
        private readonly IInitService _initService;
        private readonly ICommandOutput _output;

        /// <summary>Create an init command shell.</summary>
        public InitCommand(IInitService initService, ICommandOutput output)
        {
            ArgumentNullException.ThrowIfNull(initService);
            ArgumentNullException.ThrowIfNull(output);

            _initService = initService;
            _output = output;
        }

        /// <summary>Executes the init command shell.</summary>
        public async Task<int> ExecuteAsync(InitCommandOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            try
            {
                CommandServiceResult result = await _initService.InitAsync(options, cancellationToken).ConfigureAwait(false);
                return CommandResultFormatter.WriteResult("init", result, _output, json: false);
            }
            catch (OperationCanceledException)
            {
                CommandServiceResult result = CommandServiceResult.Failed(
                    CommandResultStatus.Cancelled,
                    "Command was cancelled.",
                    ImmutableArray<BuildDiagnostic>.Empty);
                return CommandResultFormatter.WriteResult("init", result, _output, json: false);
            }
        }
    }

    /// <summary>The dotnet qmlsharp clean command shell.</summary>
    public sealed class CleanCommand
    {
        private readonly ICleanService _cleanService;
        private readonly ICommandOutput _output;

        /// <summary>Create a clean command shell.</summary>
        public CleanCommand(ICleanService cleanService, ICommandOutput output)
        {
            ArgumentNullException.ThrowIfNull(cleanService);
            ArgumentNullException.ThrowIfNull(output);

            _cleanService = cleanService;
            _output = output;
        }

        /// <summary>Executes the clean command shell.</summary>
        public async Task<int> ExecuteAsync(CleanCommandOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            try
            {
                CommandServiceResult result = await _cleanService.CleanAsync(options, cancellationToken).ConfigureAwait(false);
                return CommandResultFormatter.WriteResult("clean", result, _output, json: false);
            }
            catch (OperationCanceledException)
            {
                CommandServiceResult result = CommandServiceResult.Failed(
                    CommandResultStatus.Cancelled,
                    "Command was cancelled.",
                    ImmutableArray<BuildDiagnostic>.Empty);
                return CommandResultFormatter.WriteResult("clean", result, _output, json: false);
            }
        }
    }
}

#pragma warning restore MA0048
