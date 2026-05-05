using System.Text.Json;

namespace QmlSharp.Build
{
    /// <summary>Filesystem-backed implementation of the clean command service.</summary>
    public sealed class CleanService : ICleanService
    {
        private const string ConfigFileName = "qmlsharp.json";
        private const string DefaultOutputDirectory = "./dist";
        private const string CacheDirectoryName = ".compiler-cache";

        /// <inheritdoc />
        public Task<CommandServiceResult> CleanAsync(
            CleanCommandOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string projectRoot = NormalizeProjectRoot(options.ProjectDir);
                CleanConfiguration configuration = LoadCleanConfiguration(projectRoot);
                ImmutableArray<BuildDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<BuildDiagnostic>();
                ImmutableArray<string>.Builder removedPaths = ImmutableArray.CreateBuilder<string>();

                CleanPath(configuration.OutputDirectory, projectRoot, removedPaths, diagnostics, cancellationToken);
                if (options.Cache)
                {
                    string cacheDirectory = Path.Join(projectRoot, CacheDirectoryName);
                    CleanPath(cacheDirectory, projectRoot, removedPaths, diagnostics, cancellationToken);
                }

                if (diagnostics.Any(static diagnostic => diagnostic.Severity is BuildDiagnosticSeverity.Error or BuildDiagnosticSeverity.Fatal))
                {
                    return Task.FromResult(CommandServiceResult.Failed(
                        CommandResultStatus.BuildError,
                        "Clean failed.",
                        diagnostics.ToImmutable()));
                }

                string message = removedPaths.Count == 0
                    ? "Clean completed. No build artifacts were present."
                    : $"Clean completed. Removed {removedPaths.Count} path(s).";
                return Task.FromResult(CommandServiceResult.Succeeded(message) with
                {
                    Diagnostics = diagnostics.ToImmutable(),
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ConfigParseException ex)
            {
                return Task.FromResult(CommandServiceResult.Failed(
                    CommandResultStatus.ConfigOrCommandError,
                    ex.Message,
                    ex.Diagnostics));
            }
        }

        private static string NormalizeProjectRoot(string projectDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
            return Path.GetFullPath(projectDirectory.Trim());
        }

        private static CleanConfiguration LoadCleanConfiguration(string projectRoot)
        {
            string configPath = Path.Join(projectRoot, ConfigFileName);
            if (!File.Exists(configPath))
            {
                throw new ConfigParseException(CreateDiagnostic(
                    BuildDiagnosticCode.ConfigNotFound,
                    $"Configuration file '{ConfigFileName}' was not found in '{projectRoot}'.",
                    configPath));
            }

            try
            {
                using FileStream stream = File.OpenRead(configPath);
                using JsonDocument document = JsonDocument.Parse(stream);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new ConfigParseException(CreateDiagnostic(
                        BuildDiagnosticCode.ConfigParseError,
                        "qmlsharp.json must contain a JSON object.",
                        configPath));
                }

                string outDir = ReadOutDir(document.RootElement, configPath);
                string outputDirectory = Path.IsPathRooted(outDir)
                    ? Path.GetFullPath(outDir)
                    : Path.GetFullPath(Path.Join(projectRoot, outDir));
                return new CleanConfiguration(outputDirectory);
            }
            catch (JsonException ex)
            {
                throw new ConfigParseException(CreateDiagnostic(
                    BuildDiagnosticCode.ConfigParseError,
                    $"qmlsharp.json could not be parsed: {ex.Message}",
                    configPath));
            }
            catch (IOException ex)
            {
                throw new ConfigParseException(CreateDiagnostic(
                    BuildDiagnosticCode.ConfigParseError,
                    $"qmlsharp.json could not be read: {ex.Message}",
                    configPath));
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new ConfigParseException(CreateDiagnostic(
                    BuildDiagnosticCode.ConfigParseError,
                    $"qmlsharp.json could not be read: {ex.Message}",
                    configPath));
            }
        }

        private static string ReadOutDir(JsonElement root, string configPath)
        {
            if (!root.TryGetProperty("outDir", out JsonElement outDirElement) ||
                outDirElement.ValueKind == JsonValueKind.Null)
            {
                return DefaultOutputDirectory;
            }

            if (outDirElement.ValueKind != JsonValueKind.String)
            {
                throw new ConfigParseException(CreateDiagnostic(
                    BuildDiagnosticCode.ConfigValidationError,
                    "outDir must be a string.",
                    configPath));
            }

            string? outDir = outDirElement.GetString();
            if (string.IsNullOrWhiteSpace(outDir))
            {
                throw new ConfigParseException(CreateDiagnostic(
                    BuildDiagnosticCode.ConfigValidationError,
                    "outDir must not be empty.",
                    configPath));
            }

            return outDir;
        }

        private static void CleanPath(
            string targetPath,
            string projectRoot,
            ImmutableArray<string>.Builder removedPaths,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalizedTargetPath = Path.GetFullPath(targetPath);
            if (!IsDirectoryBelowProjectRoot(projectRoot, normalizedTargetPath))
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.OutputValidationFailed,
                    $"Refusing to delete '{normalizedTargetPath}' because it is outside the project root '{projectRoot}'.",
                    normalizedTargetPath));
                return;
            }

            if (!Directory.Exists(normalizedTargetPath))
            {
                return;
            }

            try
            {
                Directory.Delete(normalizedTargetPath, recursive: true);
                removedPaths.Add(normalizedTargetPath);
            }
            catch (IOException ex)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.OutputAssemblyFailed,
                    $"Failed to delete '{normalizedTargetPath}': {ex.Message}",
                    normalizedTargetPath));
            }
            catch (UnauthorizedAccessException ex)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.OutputAssemblyFailed,
                    $"Failed to delete '{normalizedTargetPath}': {ex.Message}",
                    normalizedTargetPath));
            }
        }

        private static bool IsDirectoryBelowProjectRoot(string projectRoot, string targetPath)
        {
            string normalizedProjectRoot = EnsureTrailingDirectorySeparator(Path.GetFullPath(projectRoot));
            string normalizedTarget = EnsureTrailingDirectorySeparator(Path.GetFullPath(targetPath));
            if (string.Equals(normalizedProjectRoot, normalizedTarget, GetPathComparison()))
            {
                return false;
            }

            return normalizedTarget.StartsWith(normalizedProjectRoot, GetPathComparison());
        }

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static StringComparison GetPathComparison()
        {
            return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }

        private static BuildDiagnostic CreateDiagnostic(string code, string message, string filePath)
        {
            return new BuildDiagnostic(
                code,
                BuildDiagnosticSeverity.Error,
                message,
                BuildPhase.ConfigLoading,
                filePath);
        }

        private sealed record CleanConfiguration(string OutputDirectory);
    }
}
