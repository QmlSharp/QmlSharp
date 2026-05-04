#pragma warning disable MA0048

using System.Text.Json;
using System.Text.Json.Serialization;

namespace QmlSharp.Build
{
    /// <summary>Stable command outcome categories used by CLI result envelopes.</summary>
    public enum CommandResultStatus
    {
        /// <summary>The command completed successfully.</summary>
        Success,

        /// <summary>The delegated build or runtime work failed.</summary>
        BuildError,

        /// <summary>The command or configuration was invalid.</summary>
        ConfigOrCommandError,

        /// <summary>The command was cancelled.</summary>
        Cancelled,
    }

    /// <summary>Result returned by thin command-shell backing services.</summary>
    public sealed record CommandServiceResult
    {
        /// <summary>True when the service completed successfully.</summary>
        public required bool Success { get; init; }

        /// <summary>Stable command status.</summary>
        public required CommandResultStatus Status { get; init; }

        /// <summary>Human-readable result message.</summary>
        public string? Message { get; init; }

        /// <summary>Diagnostics emitted by the service.</summary>
        public ImmutableArray<BuildDiagnostic> Diagnostics { get; init; } =
            ImmutableArray<BuildDiagnostic>.Empty;

        /// <summary>Build statistics when a command produces build work.</summary>
        public BuildStats? Stats { get; init; }

        /// <summary>Create a successful service result.</summary>
        public static CommandServiceResult Succeeded(string? message = null)
        {
            return new CommandServiceResult
            {
                Success = true,
                Status = CommandResultStatus.Success,
                Message = message,
            };
        }

        /// <summary>Create a failed service result.</summary>
        public static CommandServiceResult Failed(
            CommandResultStatus status,
            string message,
            ImmutableArray<BuildDiagnostic> diagnostics)
        {
            return new CommandServiceResult
            {
                Success = false,
                Status = status,
                Message = message,
                Diagnostics = diagnostics,
            };
        }
    }

    /// <summary>Writes command output for command shells.</summary>
    public interface ICommandOutput
    {
        /// <summary>Writes regular command output.</summary>
        void WriteLine(string value);

        /// <summary>Writes command error output.</summary>
        void WriteErrorLine(string value);
    }

    /// <summary>TextWriter-backed command output.</summary>
    public sealed class TextWriterCommandOutput : ICommandOutput
    {
        private readonly TextWriter _output;
        private readonly TextWriter _error;

        /// <summary>Create output backed by two text writers.</summary>
        public TextWriterCommandOutput(TextWriter output, TextWriter error)
        {
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(error);

            _output = output;
            _error = error;
        }

        /// <inheritdoc />
        public void WriteLine(string value)
        {
            _output.WriteLine(value);
        }

        /// <inheritdoc />
        public void WriteErrorLine(string value)
        {
            _error.WriteLine(value);
        }
    }

    /// <summary>Formats command results for human-readable and JSON output.</summary>
    public static class CommandResultFormatter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            },
        };

        /// <summary>Maps a command status to the Step 08.00 exit-code contract.</summary>
        public static int GetExitCode(CommandResultStatus status)
        {
            return status switch
            {
                CommandResultStatus.Success => CliExitCode.Success,
                CommandResultStatus.BuildError => CliExitCode.BuildError,
                CommandResultStatus.ConfigOrCommandError => CliExitCode.ConfigOrCommandError,
                CommandResultStatus.Cancelled => CliExitCode.Cancelled,
                _ => CliExitCode.ConfigOrCommandError,
            };
        }

        /// <summary>Writes a command result and returns the mapped process exit code.</summary>
        public static int WriteResult(
            string command,
            CommandServiceResult result,
            ICommandOutput output,
            bool json,
            bool dryRun = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(command);
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(output);

            int exitCode = GetExitCode(result.Status);
            if (json)
            {
                output.WriteLine(CreateJsonEnvelope(command, result, exitCode, dryRun));
                return exitCode;
            }

            WriteHumanReadable(result, output);
            return exitCode;
        }

        /// <summary>Creates a machine-readable command result envelope.</summary>
        public static string CreateJsonEnvelope(
            string command,
            CommandServiceResult result,
            int exitCode,
            bool dryRun = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(command);
            ArgumentNullException.ThrowIfNull(result);

            CommandResultEnvelope envelope = new(
                command,
                result.Success,
                result.Status,
                exitCode,
                dryRun,
                result.Message,
                CreateDiagnostics(result.Diagnostics),
                CreateStats(result.Stats));
            return JsonSerializer.Serialize(envelope, JsonOptions);
        }

        private static void WriteHumanReadable(CommandServiceResult result, ICommandOutput output)
        {
            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                if (result.Success)
                {
                    output.WriteLine(result.Message);
                }
                else
                {
                    output.WriteErrorLine(result.Message);
                }
            }

            foreach (BuildDiagnostic diagnostic in result.Diagnostics)
            {
                string line = $"{diagnostic.Code}: {diagnostic.Message}";
                if (result.Success)
                {
                    output.WriteLine(line);
                }
                else
                {
                    output.WriteErrorLine(line);
                }
            }
        }

        private static ImmutableArray<CommandDiagnosticEnvelope> CreateDiagnostics(
            ImmutableArray<BuildDiagnostic> diagnostics)
        {
            ImmutableArray<CommandDiagnosticEnvelope>.Builder builder =
                ImmutableArray.CreateBuilder<CommandDiagnosticEnvelope>(diagnostics.Length);
            foreach (BuildDiagnostic diagnostic in diagnostics)
            {
                builder.Add(new CommandDiagnosticEnvelope(
                    diagnostic.Code,
                    diagnostic.Severity,
                    diagnostic.Message,
                    diagnostic.Phase,
                    diagnostic.FilePath));
            }

            return builder.ToImmutable();
        }

        private static CommandStatsEnvelope? CreateStats(BuildStats? stats)
        {
            if (stats is null)
            {
                return null;
            }

            return new CommandStatsEnvelope(
                stats.TotalDuration.TotalMilliseconds,
                stats.FilesCompiled,
                stats.SchemasGenerated,
                stats.CppFilesGenerated,
                stats.AssetsCollected,
                stats.NativeLibBuilt);
        }

        private sealed record CommandResultEnvelope(
            string Command,
            bool Success,
            CommandResultStatus Status,
            int ExitCode,
            bool DryRun,
            string? Message,
            ImmutableArray<CommandDiagnosticEnvelope> Diagnostics,
            CommandStatsEnvelope? Stats);

        private sealed record CommandDiagnosticEnvelope(
            string Code,
            BuildDiagnosticSeverity Severity,
            string Message,
            BuildPhase? Phase,
            string? FilePath);

        private sealed record CommandStatsEnvelope(
            double TotalDurationMs,
            int FilesCompiled,
            int SchemasGenerated,
            int CppFilesGenerated,
            int AssetsCollected,
            bool NativeLibBuilt);
    }
}

#pragma warning restore MA0048
