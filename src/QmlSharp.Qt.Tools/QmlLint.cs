using System.Globalization;
using System.Text.Json;

namespace QmlSharp.Qt.Tools
{
    /// <summary>Default qmllint wrapper.</summary>
    public sealed class QmlLint : IQmlLint
    {
        private const string ToolName = "qmllint";
        private const string StringInputFileName = "<string>";

        private readonly IQtToolchain _toolchain;
        private readonly IToolRunner _toolRunner;
        private readonly IQtDiagnosticParser _diagnosticParser;

        /// <summary>Create a qmllint wrapper backed by default Qt discovery and process execution.</summary>
        public QmlLint()
            : this(new QtToolchain(), new ToolRunner(), new QtDiagnosticParser())
        {
        }

        /// <summary>Create a qmllint wrapper using explicit infrastructure services.</summary>
        public QmlLint(IQtToolchain toolchain, IToolRunner toolRunner, IQtDiagnosticParser diagnosticParser)
        {
            _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
            _toolRunner = toolRunner ?? throw new ArgumentNullException(nameof(toolRunner));
            _diagnosticParser = diagnosticParser ?? throw new ArgumentNullException(nameof(diagnosticParser));
        }

        /// <inheritdoc />
        public async Task<QmlLintResult> LintFileAsync(
            string filePath,
            QmlLintOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string normalizedFilePath = Path.GetFullPath(filePath);
            QmlLintOptions effectiveOptions = options ?? new QmlLintOptions();
            ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
            ImmutableArray<string> args = BuildArguments([normalizedFilePath], effectiveOptions, moduleMode: false, _toolchain.Installation?.ImportPaths ?? []);

            ToolResult toolResult = await _toolRunner
                .RunAsync(tool.Path, args, new ToolRunnerOptions(), ct)
                .ConfigureAwait(false);

            return CreateResult(toolResult, normalizedFilePath, effectiveOptions, filenameOverride: null);
        }

        /// <inheritdoc />
        public async Task<QmlLintResult> LintStringAsync(
            string qmlSource,
            QmlLintOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(qmlSource);

            string tempFile = Path.Join(
                Path.GetTempPath(),
                "qmlsharp-qmllint-" + Guid.NewGuid().ToString("N") + ".qml");

            try
            {
                await File.WriteAllTextAsync(tempFile, qmlSource, ct).ConfigureAwait(false);

                QmlLintOptions effectiveOptions = options ?? new QmlLintOptions();
                ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
                ImmutableArray<string> args = BuildArguments([tempFile], effectiveOptions, moduleMode: false, _toolchain.Installation?.ImportPaths ?? []);

                ToolResult toolResult = await _toolRunner
                    .RunAsync(tool.Path, args, new ToolRunnerOptions(), ct)
                    .ConfigureAwait(false);

                return CreateResult(toolResult, tempFile, effectiveOptions, StringInputFileName);
            }
            finally
            {
                TryDeleteFile(tempFile);
            }
        }

        /// <inheritdoc />
        public async Task<ImmutableArray<QmlLintResult>> LintBatchAsync(
            ImmutableArray<string> filePaths,
            QmlLintOptions? options = null,
            CancellationToken ct = default)
        {
            QmlLintOptions effectiveOptions = options ?? new QmlLintOptions();
            ImmutableArray<string> normalizedFilePaths = filePaths
                .Select(Path.GetFullPath)
                .ToImmutableArray();

            ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
            ImmutableArray<string> args = BuildArguments(normalizedFilePaths, effectiveOptions, moduleMode: false, _toolchain.Installation?.ImportPaths ?? []);

            ToolResult toolResult = await _toolRunner
                .RunAsync(tool.Path, args, new ToolRunnerOptions(), ct)
                .ConfigureAwait(false);

            ImmutableArray<QmlLintResult>? jsonResults = TryCreateBatchJsonResults(toolResult, normalizedFilePaths, effectiveOptions);
            if (jsonResults is not null)
            {
                return jsonResults.Value;
            }

            ImmutableArray<QmlLintResult>.Builder fallbackResults = ImmutableArray.CreateBuilder<QmlLintResult>(normalizedFilePaths.Length);
            foreach (string normalizedFilePath in normalizedFilePaths)
            {
                fallbackResults.Add(await LintFileAsync(normalizedFilePath, effectiveOptions, ct).ConfigureAwait(false));
            }

            return fallbackResults.MoveToImmutable();
        }

        /// <inheritdoc />
        public async Task<QmlLintResult> LintModuleAsync(
            string modulePath,
            QmlLintOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);

            string normalizedModulePath = Path.GetFullPath(modulePath);
            QmlLintOptions effectiveOptions = options ?? new QmlLintOptions();
            ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
            ImmutableArray<string> args = BuildArguments([normalizedModulePath], effectiveOptions, moduleMode: true, _toolchain.Installation?.ImportPaths ?? []);

            ToolResult toolResult = await _toolRunner
                .RunAsync(tool.Path, args, new ToolRunnerOptions(), ct)
                .ConfigureAwait(false);

            return CreateResult(toolResult, normalizedModulePath, effectiveOptions, filenameOverride: null);
        }

        /// <inheritdoc />
        public async Task<ImmutableArray<string>> ListPluginsAsync(CancellationToken ct = default)
        {
            ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
            ToolResult toolResult = await _toolRunner
                .RunAsync(tool.Path, ["--list-plugins"], new ToolRunnerOptions(), ct)
                .ConfigureAwait(false);

            string output = string.IsNullOrWhiteSpace(toolResult.Stderr)
                ? toolResult.Stdout
                : toolResult.Stderr;

            return output
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParsePluginName)
                .Where(static plugin => plugin is not null)
                .Select(static plugin => plugin!)
                .Distinct(StringComparer.Ordinal)
                .ToImmutableArray();
        }

        internal static ImmutableArray<string> BuildArguments(
            ImmutableArray<string> filePaths,
            QmlLintOptions options,
            bool moduleMode,
            ImmutableArray<string> toolchainImportPaths)
        {
            ArgumentNullException.ThrowIfNull(options);

            ImmutableArray<string>.Builder args = ImmutableArray.CreateBuilder<string>();
            if (options.JsonOutput)
            {
                args.Add("--json");
                args.Add("-");
            }

            if (options.Silent)
            {
                args.Add("--silent");
            }

            if (moduleMode)
            {
                args.Add("--module");
            }

            if (options.Bare)
            {
                args.Add("--bare");
            }

            if (options.Fix)
            {
                args.Add("--fix");
            }

            if (options.MaxWarnings > 0)
            {
                args.Add("--max-warnings");
                args.Add(options.MaxWarnings.ToString(CultureInfo.InvariantCulture));
            }

            AddImportPaths(args, toolchainImportPaths);
            AddImportPaths(args, options.ImportPaths);
            AddWarningLevels(args, options);

            foreach (string filePath in filePaths)
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePaths));
                }

                args.Add(filePath);
            }

            return args.ToImmutable();
        }

        private QmlLintResult CreateResult(
            ToolResult toolResult,
            string inputPath,
            QmlLintOptions options,
            string? filenameOverride)
        {
            ImmutableArray<QtDiagnostic> diagnostics = ParseDiagnostics(toolResult, inputPath, options, filenameOverride);
            if (!toolResult.Success && diagnostics.IsDefaultOrEmpty && !options.Silent)
            {
                diagnostics = [CreateFallbackDiagnostic(toolResult, filenameOverride ?? inputPath)];
            }

            return CreateResultFromDiagnostics(toolResult, diagnostics);
        }

        private ImmutableArray<QmlLintResult>? TryCreateBatchJsonResults(
            ToolResult toolResult,
            ImmutableArray<string> normalizedFilePaths,
            QmlLintOptions options)
        {
            if (!options.JsonOutput || string.IsNullOrWhiteSpace(toolResult.Stdout))
            {
                return null;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(toolResult.Stdout);
                if (!document.RootElement.TryGetProperty("files", out JsonElement files)
                    || files.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                Dictionary<string, ImmutableArray<QtDiagnostic>> diagnosticsByFile = CreateDiagnosticsByFile(files);
                ImmutableArray<QmlLintResult>.Builder results = ImmutableArray.CreateBuilder<QmlLintResult>(normalizedFilePaths.Length);
                foreach (string normalizedFilePath in normalizedFilePaths)
                {
                    if (!TryGetDiagnosticsForFile(diagnosticsByFile, normalizedFilePath, out ImmutableArray<QtDiagnostic> diagnostics))
                    {
                        diagnostics = [CreateMissingBatchDiagnostic(normalizedFilePath)];
                    }

                    int exitCode = diagnostics.Any(static diagnostic =>
                        diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
                        ? 1
                        : 0;
                    results.Add(CreateResultFromDiagnostics(toolResult with { ExitCode = exitCode }, diagnostics));
                }

                return results.MoveToImmutable();
            }
            catch (JsonException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private Dictionary<string, ImmutableArray<QtDiagnostic>> CreateDiagnosticsByFile(JsonElement files)
        {
            Dictionary<string, ImmutableArray<QtDiagnostic>> diagnosticsByFile = new(GetPathComparer());
            foreach (JsonElement fileEntry in files.EnumerateArray())
            {
                string? filename = ReadString(fileEntry, "filename")
                    ?? ReadString(fileEntry, "file")
                    ?? ReadString(fileEntry, "path");
                if (string.IsNullOrWhiteSpace(filename))
                {
                    continue;
                }

                string wrappedJson = $$"""{"files":[{{fileEntry.GetRawText()}}]}""";
                ImmutableArray<QtDiagnostic> diagnostics = _diagnosticParser.ParseJson(wrappedJson);
                diagnosticsByFile[NormalizePathKey(filename)] = diagnostics;
            }

            return diagnosticsByFile;
        }

        private ImmutableArray<QtDiagnostic> ParseDiagnostics(
            ToolResult toolResult,
            string inputPath,
            QmlLintOptions options,
            string? filenameOverride)
        {
            ImmutableArray<QtDiagnostic> diagnostics = [];
            if (options.JsonOutput && !string.IsNullOrWhiteSpace(toolResult.Stdout))
            {
                diagnostics = _diagnosticParser.ParseJson(toolResult.Stdout);
                diagnostics = RewriteStringInputDiagnostics(diagnostics, inputPath, filenameOverride);
            }

            if (diagnostics.IsDefaultOrEmpty)
            {
                diagnostics = _diagnosticParser.ParseStderr(toolResult.Stderr, filenameOverride);
            }

            return diagnostics;
        }

        private static QmlLintResult CreateResultFromDiagnostics(
            ToolResult toolResult,
            ImmutableArray<QtDiagnostic> diagnostics)
        {
            int errorCount = diagnostics.Count(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            int warningCount = diagnostics.Count(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);
            int infoCount = diagnostics.Count(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Info or DiagnosticSeverity.Hint);

            return new QmlLintResult
            {
                ToolResult = toolResult,
                Diagnostics = diagnostics,
                ErrorCount = errorCount,
                WarningCount = warningCount,
                InfoCount = infoCount,
                Summary = CreateSummary(errorCount, warningCount, infoCount),
            };
        }

        private static ImmutableArray<QtDiagnostic> RewriteStringInputDiagnostics(
            ImmutableArray<QtDiagnostic> diagnostics,
            string inputPath,
            string? filenameOverride)
        {
            if (filenameOverride is null || diagnostics.IsDefaultOrEmpty)
            {
                return diagnostics;
            }

            string inputKey = NormalizePathKey(inputPath);
            return diagnostics
                .Select(diagnostic =>
                    diagnostic.File is null || GetPathComparer().Equals(NormalizePathKey(diagnostic.File), inputKey)
                        ? diagnostic with { File = filenameOverride }
                        : diagnostic)
                .ToImmutableArray();
        }

        private static void AddImportPaths(ImmutableArray<string>.Builder args, ImmutableArray<string> importPaths)
        {
            foreach (string importPath in importPaths
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(GetPathComparer()))
            {
                args.Add("-I");
                args.Add(importPath);
            }
        }

        private static void AddWarningLevels(ImmutableArray<string>.Builder args, QmlLintOptions options)
        {
            if (options.WarningLevels is null)
            {
                return;
            }

            foreach (KeyValuePair<QmlLintCategory, DiagnosticSeverity> pair in options.WarningLevels.OrderBy(static pair => pair.Key))
            {
                args.Add("--" + pair.Key.ToCliName());
                args.Add(MapWarningLevel(pair.Value));
            }
        }

        private static string MapWarningLevel(DiagnosticSeverity severity)
        {
            return severity switch
            {
                DiagnosticSeverity.Disabled => "disable",
                DiagnosticSeverity.Info or DiagnosticSeverity.Hint => "info",
                DiagnosticSeverity.Warning => "warning",
                DiagnosticSeverity.Error => "error",
                _ => "warning",
            };
        }

        private static string? ParsePluginName(string line)
        {
            if (line.Length == 0
                || line.StartsWith("Available", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Plugin", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("-", StringComparison.Ordinal))
            {
                return null;
            }

            string name = line.Split(' ', '\t').FirstOrDefault(static part => part.Length > 0) ?? string.Empty;
            return name.Length == 0 ? null : name;
        }

        private static bool TryGetDiagnosticsForFile(
            Dictionary<string, ImmutableArray<QtDiagnostic>> diagnosticsByFile,
            string filePath,
            out ImmutableArray<QtDiagnostic> diagnostics)
        {
            string key = NormalizePathKey(filePath);
            if (diagnosticsByFile.TryGetValue(key, out diagnostics))
            {
                return true;
            }

            string fileName = Path.GetFileName(filePath);
            foreach (KeyValuePair<string, ImmutableArray<QtDiagnostic>> pair in diagnosticsByFile)
            {
                if (string.Equals(Path.GetFileName(pair.Key), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics = pair.Value;
                    return true;
                }
            }

            diagnostics = [];
            return false;
        }

        private static QtDiagnostic CreateFallbackDiagnostic(ToolResult toolResult, string filePath)
        {
            string message = FirstNonEmptyLine(toolResult.Stderr)
                ?? FirstNonEmptyLine(toolResult.Stdout)
                ?? $"qmllint failed with exit code {toolResult.ExitCode}.";

            return new QtDiagnostic
            {
                File = filePath,
                Severity = DiagnosticSeverity.Error,
                Message = message,
            };
        }

        private static QtDiagnostic CreateMissingBatchDiagnostic(string filePath)
        {
            return new QtDiagnostic
            {
                File = filePath,
                Severity = DiagnosticSeverity.Error,
                Message = $"qmllint batch output did not include a result for '{filePath}'.",
            };
        }

        private static string CreateSummary(int errorCount, int warningCount, int infoCount)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{errorCount} {Pluralize(errorCount, "error")}, {warningCount} {Pluralize(warningCount, "warning")}, {infoCount} {Pluralize(infoCount, "info")}");
        }

        private static string Pluralize(int count, string word)
        {
            return count == 1 ? word : word + "s";
        }

        private static string? FirstNonEmptyLine(string text)
        {
            return text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(static line => line.Trim())
                .FirstOrDefault(static line => line.Length > 0);
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property))
            {
                return null;
            }

            return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
        }

        private static string NormalizePathKey(string path)
        {
            string candidate = path.Replace('/', Path.DirectorySeparatorChar);
            try
            {
                candidate = Path.GetFullPath(candidate);
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (PathTooLongException)
            {
            }

            return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? candidate.ToUpperInvariant()
                : candidate;
        }

        private static StringComparer GetPathComparer()
        {
            return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup for temporary string-input files.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup for temporary string-input files.
            }
        }
    }
}
