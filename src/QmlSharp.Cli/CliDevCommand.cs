using System.Collections.Immutable;
using QmlSharp.Build;
using QmlSharp.DevTools;

#pragma warning disable MA0048

namespace QmlSharp.Cli
{
    /// <summary>Runs the dotnet qmlsharp dev command through QmlSharp.DevTools.</summary>
    public sealed class CliDevCommand
    {
        private readonly IConfigLoader configLoader;
        private readonly ICliDevServerFactory devServerFactory;
        private readonly ICommandOutput output;
        private readonly TextWriter devConsoleOutput;

        /// <summary>Create a CLI dev command runner.</summary>
        public CliDevCommand(
            IConfigLoader configLoader,
            ICliDevServerFactory devServerFactory,
            ICommandOutput output,
            TextWriter devConsoleOutput)
        {
            ArgumentNullException.ThrowIfNull(configLoader);
            ArgumentNullException.ThrowIfNull(devServerFactory);
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(devConsoleOutput);

            this.configLoader = configLoader;
            this.devServerFactory = devServerFactory;
            this.output = output;
            this.devConsoleOutput = devConsoleOutput;
        }

        /// <summary>Starts the dev server and blocks until cancellation.</summary>
        public async Task<int> ExecuteAsync(
            DevCommandOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            IDevServer? server = null;
            bool serverStarted = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                DevServerCreationContext creationContext = CreateServerCreationContext(options);
                server = devServerFactory.Create(creationContext);

                await StartServerAsync(server, cancellationToken).ConfigureAwait(false);
                serverStarted = true;
                if (server.Status == DevServerStatus.Error)
                {
                    return await StopAndWriteResultAsync(
                        server,
                        CommandResultStatus.BuildError,
                        "Dev server failed to start.",
                        cancellationToken).ConfigureAwait(false);
                }

                output.WriteLine("Dev server ready.");
                return await WaitForStopAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return await WriteCancellationAsync(server, serverStarted).ConfigureAwait(false);
            }
            catch (ConfigParseException ex)
            {
                return WriteConfigError(ex);
            }
            catch (Exception ex) when (!IsCriticalException(ex))
            {
                return WriteServerFailure(ex);
            }
            finally
            {
                if (server is not null)
                {
                    await server.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task StartServerAsync(IDevServer server, CancellationToken cancellationToken)
        {
            output.WriteLine("Starting QmlSharp dev server...");
            await server.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> WaitForStopAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return CommandResultFormatter.GetExitCode(CommandResultStatus.Success);
        }

        private async Task<int> WriteCancellationAsync(IDevServer? server, bool serverStarted)
        {
            if (serverStarted && server is not null)
            {
                output.WriteLine("Stopping QmlSharp dev server...");
                await server.StopAsync().ConfigureAwait(false);
                output.WriteLine("Dev server stopped.");
            }

            CommandServiceResult result = CommandServiceResult.Failed(
                CommandResultStatus.Cancelled,
                "Command was cancelled.",
                ImmutableArray<BuildDiagnostic>.Empty);
            return CommandResultFormatter.WriteResult("dev", result, output, json: false);
        }

        private int WriteConfigError(ConfigParseException ex)
        {
            CommandServiceResult result = CommandServiceResult.Failed(
                CommandResultStatus.ConfigOrCommandError,
                ex.Message,
                ex.Diagnostics);
            return CommandResultFormatter.WriteResult("dev", result, output, json: false);
        }

        private int WriteServerFailure(Exception ex)
        {
            BuildDiagnostic diagnostic = CreateCommandDiagnostic(
                "dev",
                "Dev server failed: " + ex.Message,
                BuildDiagnosticCode.InternalError);
            CommandServiceResult result = CommandServiceResult.Failed(
                CommandResultStatus.BuildError,
                diagnostic.Message,
                ImmutableArray.Create(diagnostic));
            return CommandResultFormatter.WriteResult("dev", result, output, json: false);
        }

        private async Task<int> StopAndWriteResultAsync(
            IDevServer server,
            CommandResultStatus status,
            string message,
            CancellationToken cancellationToken)
        {
            output.WriteLine("Stopping QmlSharp dev server...");
            await server.StopAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            output.WriteLine("Dev server stopped.");

            BuildDiagnostic diagnostic = CreateCommandDiagnostic("dev", message, BuildDiagnosticCode.InternalError);
            CommandServiceResult result = CommandServiceResult.Failed(
                status,
                message,
                ImmutableArray.Create(diagnostic));
            return CommandResultFormatter.WriteResult("dev", result, output, json: false);
        }

        private DevServerCreationContext CreateServerCreationContext(DevCommandOptions options)
        {
            string projectRoot = Path.GetFullPath(options.ProjectDir);
            QmlSharpConfig loadedConfig = configLoader.Load(projectRoot);
            string effectiveEntry = ResolveEntry(projectRoot, loadedConfig, options);
            QmlSharpConfig effectiveConfig = loadedConfig with
            {
                Entry = effectiveEntry,
            };
            DevCommandOptions effectiveCommandOptions = options with
            {
                Entry = effectiveEntry,
                ProjectDir = projectRoot,
            };
            BuildContext buildContext = new()
            {
                Config = effectiveConfig,
                ProjectDir = projectRoot,
                OutputDir = effectiveConfig.OutDir,
                QtDir = effectiveConfig.Qt.Dir ?? string.Empty,
                ForceRebuild = false,
                LibraryMode = false,
                DryRun = false,
                FileFilter = null,
            };
            DevServerOptions serverOptions = new(
                projectRoot,
                new FileWatcherOptions(effectiveConfig.Dev.WatchPaths, effectiveConfig.Dev.DebounceMs),
                new DevConsoleOptions(LogLevel.Info, Color: false, ShowTimestamps: false, devConsoleOutput),
                EnableRepl: true,
                EnableProfiling: true,
                ConfigPath: Path.Join(projectRoot, "qmlsharp.json"),
                Headless: options.Headless);

            return new DevServerCreationContext(
                effectiveCommandOptions,
                loadedConfig,
                effectiveConfig,
                buildContext,
                serverOptions);
        }

        private static string ResolveEntry(
            string projectRoot,
            QmlSharpConfig config,
            DevCommandOptions options)
        {
            string? candidate = string.IsNullOrWhiteSpace(options.Entry) ? config.Entry : options.Entry;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                BuildDiagnostic diagnostic = CreateCommandDiagnostic(
                    "entry",
                    "Dev requires an entry from qmlsharp.json or --entry.");
                throw new ConfigParseException(diagnostic);
            }

            return Path.GetFullPath(Path.IsPathRooted(candidate) ? candidate : Path.Join(projectRoot, candidate));
        }

        private static bool IsCriticalException(Exception exception)
        {
            return exception is OutOfMemoryException
                or StackOverflowException
                or AccessViolationException
                or AppDomainUnloadedException
                or BadImageFormatException
                or CannotUnloadAppDomainException
                or InvalidProgramException
                or ThreadAbortException;
        }

        private static BuildDiagnostic CreateCommandDiagnostic(
            string field,
            string message,
            string code = BuildDiagnosticCode.ConfigValidationError)
        {
            return new BuildDiagnostic(
                code,
                BuildDiagnosticSeverity.Error,
                message,
                BuildPhase.ConfigLoading,
                field);
        }
    }

    /// <summary>Factory used by the CLI to create a dev server.</summary>
    public interface ICliDevServerFactory
    {
        /// <summary>Create a dev server for the resolved command context.</summary>
        IDevServer Create(DevServerCreationContext context);
    }

    /// <summary>Resolved inputs used to construct a dev server.</summary>
    public sealed record DevServerCreationContext(
        DevCommandOptions CommandOptions,
        QmlSharpConfig LoadedConfig,
        QmlSharpConfig EffectiveConfig,
        BuildContext BuildContext,
        DevServerOptions ServerOptions);
}

#pragma warning restore MA0048
