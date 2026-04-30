using System.Globalization;

namespace QmlSharp.Qt.Tools
{
    /// <summary>Default qml runtime smoke-test wrapper.</summary>
    public sealed class QmlRunner : IQmlRunner
    {
        private const string ToolName = "qml";
        private const string StringInputFileName = "<string>";

        private static readonly ImmutableArray<string> RuntimeErrorMarkers =
        [
            "QQmlApplicationEngine failed",
            "ReferenceError:",
            "TypeError:",
            "Component is not ready",
            "Cannot assign to non-existent property",
            "is not a type",
            "is not installed",
            "file not found",
        ];

        private readonly IQtToolchain _toolchain;
        private readonly IToolRunner _toolRunner;
        private readonly IQtDiagnosticParser _diagnosticParser;

        /// <summary>Create a qml runner backed by default Qt discovery and process execution.</summary>
        public QmlRunner()
            : this(new QtToolchain(), new ToolRunner(), new QtDiagnosticParser())
        {
        }

        /// <summary>Create a qml runner using explicit infrastructure services.</summary>
        public QmlRunner(IQtToolchain toolchain, IToolRunner toolRunner, IQtDiagnosticParser diagnosticParser)
        {
            _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
            _toolRunner = toolRunner ?? throw new ArgumentNullException(nameof(toolRunner));
            _diagnosticParser = diagnosticParser ?? throw new ArgumentNullException(nameof(diagnosticParser));
        }

        /// <inheritdoc />
        public async Task<QmlRunResult> RunFileAsync(
            string filePath,
            QmlRunOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string normalizedFilePath = Path.GetFullPath(filePath);
            QmlRunOptions effectiveOptions = options ?? new QmlRunOptions();
            ValidateOptions(effectiveOptions);

            ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
            ImmutableArray<string> args = BuildArguments(
                normalizedFilePath,
                effectiveOptions,
                _toolchain.Installation?.ImportPaths ?? []);

            bool autoKillArmed = IsAutoKillArmed(effectiveOptions);
            TimeSpan processTimeout = autoKillArmed
                ? effectiveOptions.StableRunPeriod
                : effectiveOptions.Timeout;

            try
            {
                ToolResult toolResult = await _toolRunner
                    .RunAsync(tool.Path, args, new ToolRunnerOptions { Timeout = processTimeout }, ct)
                    .ConfigureAwait(false);

                return CreateCompletedResult(toolResult, normalizedFilePath, filenameOverride: null);
            }
            catch (QtToolTimeoutError ex) when (autoKillArmed)
            {
                return CreateAutoKilledResult(ex, tool.Path, args, normalizedFilePath, filenameOverride: null);
            }
        }

        /// <inheritdoc />
        public async Task<QmlRunResult> RunStringAsync(
            string qmlSource,
            QmlRunOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(qmlSource);

            string tempFile = Path.Join(
                Path.GetTempPath(),
                "qmlsharp-qmlrunner-" + Guid.NewGuid().ToString("N") + ".qml");

            try
            {
                await File.WriteAllTextAsync(tempFile, qmlSource, ct).ConfigureAwait(false);
                return await RunFileCoreAsync(tempFile, options, StringInputFileName, ct).ConfigureAwait(false);
            }
            finally
            {
                TryDeleteFile(tempFile);
            }
        }

        /// <inheritdoc />
        public async Task<ImmutableArray<string>> ListConfigurationsAsync(CancellationToken ct = default)
        {
            ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
            ToolResult toolResult = await _toolRunner
                .RunAsync(tool.Path, ["--list-conf"], new ToolRunnerOptions(), ct)
                .ConfigureAwait(false);

            return ParseConfigurations(toolResult.Stdout.Length > 0 ? toolResult.Stdout : toolResult.Stderr);
        }

        internal static ImmutableArray<string> BuildArguments(
            string filePath,
            QmlRunOptions options,
            ImmutableArray<string> toolchainImportPaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(options);

            ImmutableArray<string>.Builder args = ImmutableArray.CreateBuilder<string>();
            AddImportPaths(args, toolchainImportPaths);
            AddImportPaths(args, options.ImportPaths);
            AddAppType(args, options.AppType);

            if (!string.IsNullOrWhiteSpace(options.Platform))
            {
                args.Add("--platform");
                args.Add(options.Platform.Trim());
            }

            args.Add("--");
            args.Add(filePath);
            return args.ToImmutable();
        }

        internal static ImmutableArray<string> ParseConfigurations(string output)
        {
            ArgumentNullException.ThrowIfNull(output);

            return output
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(static line => line.Trim())
                .Where(static line => line.Length > 0)
                .Where(static line => !line.EndsWith("configurations:", StringComparison.OrdinalIgnoreCase))
                .ToImmutableArray();
        }

        private async Task<QmlRunResult> RunFileCoreAsync(
            string filePath,
            QmlRunOptions? options,
            string? filenameOverride,
            CancellationToken ct)
        {
            QmlRunOptions effectiveOptions = options ?? new QmlRunOptions();
            ValidateOptions(effectiveOptions);

            ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
            ImmutableArray<string> args = BuildArguments(
                filePath,
                effectiveOptions,
                _toolchain.Installation?.ImportPaths ?? []);

            bool autoKillArmed = IsAutoKillArmed(effectiveOptions);
            TimeSpan processTimeout = autoKillArmed
                ? effectiveOptions.StableRunPeriod
                : effectiveOptions.Timeout;

            try
            {
                ToolResult toolResult = await _toolRunner
                    .RunAsync(tool.Path, args, new ToolRunnerOptions { Timeout = processTimeout }, ct)
                    .ConfigureAwait(false);

                return CreateCompletedResult(toolResult, filePath, filenameOverride);
            }
            catch (QtToolTimeoutError ex) when (autoKillArmed)
            {
                return CreateAutoKilledResult(ex, tool.Path, args, filePath, filenameOverride);
            }
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

        private static void AddAppType(ImmutableArray<string>.Builder args, QmlAppType appType)
        {
            string? qtAppType = appType switch
            {
                QmlAppType.Auto => null,
                QmlAppType.Window => "gui",
                QmlAppType.Item => "gui",
                _ => throw new ArgumentOutOfRangeException(nameof(appType), appType, "Unknown QML app type."),
            };

            if (qtAppType is not null)
            {
                args.Add("--apptype");
                args.Add(qtAppType);
            }
        }

        private QmlRunResult CreateCompletedResult(
            ToolResult toolResult,
            string inputFile,
            string? filenameOverride)
        {
            ImmutableArray<QtDiagnostic> runtimeErrors = ParseRuntimeDiagnostics(
                toolResult.Stderr,
                inputFile,
                filenameOverride);

            return new QmlRunResult
            {
                ToolResult = toolResult,
                Passed = toolResult.Success && !HasError(runtimeErrors),
                RuntimeErrors = runtimeErrors,
                AutoKilled = false,
            };
        }

        private QmlRunResult CreateAutoKilledResult(
            QtToolTimeoutError timeoutError,
            string toolPath,
            ImmutableArray<string> args,
            string inputFile,
            string? filenameOverride)
        {
            ToolResult toolResult = new()
            {
                ExitCode = -1,
                Stdout = timeoutError.PartialStdout,
                Stderr = timeoutError.PartialStderr,
                DurationMs = Math.Max(1, (long)Math.Ceiling(timeoutError.Timeout.TotalMilliseconds)),
                Command = FormatCommand(toolPath, args),
            };

            ImmutableArray<QtDiagnostic> runtimeErrors = ParseRuntimeDiagnostics(
                timeoutError.PartialStderr,
                inputFile,
                filenameOverride);

            return new QmlRunResult
            {
                ToolResult = toolResult,
                Passed = !HasError(runtimeErrors),
                RuntimeErrors = runtimeErrors,
                AutoKilled = true,
            };
        }

        private ImmutableArray<QtDiagnostic> ParseRuntimeDiagnostics(
            string stderr,
            string inputFile,
            string? filenameOverride)
        {
            ImmutableArray<QtDiagnostic> parsed = _diagnosticParser.ParseStderr(stderr, filenameOverride);
            parsed = RewriteStringInputDiagnostics(parsed, inputFile, filenameOverride);

            ImmutableArray<QtDiagnostic> runtimePatternDiagnostics = ParseRuntimePatternDiagnostics(
                stderr,
                inputFile,
                filenameOverride);

            if (runtimePatternDiagnostics.IsDefaultOrEmpty)
            {
                return parsed;
            }

            ImmutableArray<QtDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<QtDiagnostic>(
                parsed.Length + runtimePatternDiagnostics.Length);
            diagnostics.AddRange(parsed);
            diagnostics.AddRange(runtimePatternDiagnostics.Where(
                diagnostic => !ContainsEquivalentDiagnostic(parsed, diagnostic)));

            return diagnostics.ToImmutable();
        }

        private static ImmutableArray<QtDiagnostic> ParseRuntimePatternDiagnostics(
            string stderr,
            string inputFile,
            string? filenameOverride)
        {
            if (string.IsNullOrWhiteSpace(stderr))
            {
                return [];
            }

            ImmutableArray<QtDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<QtDiagnostic>();
            string[] lines = stderr.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            foreach (string line in lines.Select(static rawLine => rawLine.Trim()))
            {
                if (line.Length == 0 || !IsRuntimeErrorLine(line))
                {
                    continue;
                }

                diagnostics.Add(CreateRuntimeDiagnostic(line, inputFile, filenameOverride));
            }

            return diagnostics.ToImmutable();
        }

        private static QtDiagnostic CreateRuntimeDiagnostic(
            string line,
            string inputFile,
            string? filenameOverride)
        {
            RuntimeLocation location = ParseRuntimeLocation(line, inputFile, filenameOverride);
            return new QtDiagnostic
            {
                File = location.File,
                Line = location.Line,
                Column = location.Column,
                Severity = DiagnosticSeverity.Error,
                Message = location.Message,
            };
        }

        private static RuntimeLocation ParseRuntimeLocation(
            string line,
            string inputFile,
            string? filenameOverride)
        {
            int markerIndex = RuntimeErrorMarkers
                .Select(marker => line.IndexOf(marker, StringComparison.OrdinalIgnoreCase))
                .Where(static index => index >= 0)
                .DefaultIfEmpty(-1)
                .Min();

            if (markerIndex <= 0)
            {
                return new RuntimeLocation(filenameOverride, null, null, line);
            }

            string prefix = line[..markerIndex].Trim().TrimEnd(':');
            string message = line[markerIndex..].Trim();
            ParsedLocation? parsedLocation = TryParseLocation(prefix);
            if (parsedLocation is null)
            {
                return new RuntimeLocation(filenameOverride, null, null, message);
            }

            string? file = parsedLocation.File;
            if (filenameOverride is not null && IsSamePath(file, inputFile))
            {
                file = filenameOverride;
            }

            return new RuntimeLocation(file, parsedLocation.Line, parsedLocation.Column, message);
        }

        private static ParsedLocation? TryParseLocation(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return null;
            }

            string[] segments = prefix.Split(':');
            if (segments.Length == 0)
            {
                return null;
            }

            string last = segments[^1].Trim();
            string? secondLast = segments.Length >= 2 ? segments[^2].Trim() : null;

            if (secondLast is not null && IsDecimal(secondLast) && IsDecimal(last))
            {
                string file = string.Join(':', segments[..^2]).Trim();
                return new ParsedLocation(file.Length == 0 ? null : file, int.Parse(secondLast, CultureInfo.InvariantCulture), int.Parse(last, CultureInfo.InvariantCulture));
            }

            if (IsDecimal(last))
            {
                string file = string.Join(':', segments[..^1]).Trim();
                return new ParsedLocation(file.Length == 0 ? null : file, int.Parse(last, CultureInfo.InvariantCulture), null);
            }

            return null;
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

            return diagnostics
                .Select(diagnostic =>
                    diagnostic.File is null || IsSamePath(diagnostic.File, inputPath)
                        ? diagnostic with { File = filenameOverride }
                        : diagnostic)
                .ToImmutableArray();
        }

        private static bool ContainsEquivalentDiagnostic(
            ImmutableArray<QtDiagnostic> diagnostics,
            QtDiagnostic candidate)
        {
            return diagnostics.Any(diagnostic =>
                string.Equals(diagnostic.Message, candidate.Message, StringComparison.Ordinal)
                && (HasSameLocation(diagnostic, candidate)
                    || candidate.File is null
                    || diagnostic.File is null));
        }

        private static bool HasSameLocation(QtDiagnostic left, QtDiagnostic right)
        {
            return string.Equals(left.File, right.File, StringComparison.Ordinal)
                && left.Line == right.Line
                && left.Column == right.Column;
        }

        private static bool IsRuntimeErrorLine(string line)
        {
            return RuntimeErrorMarkers.Any(marker =>
                line.Contains(marker, StringComparison.OrdinalIgnoreCase))
                && !IsKnownNonErrorLine(line);
        }

        private static bool IsKnownNonErrorLine(string line)
        {
            return line.Contains("module \"QtQml.WorkerScript\" is not installed", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasError(ImmutableArray<QtDiagnostic> diagnostics)
        {
            return diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        }

        private static bool IsAutoKillArmed(QmlRunOptions options)
        {
            return options.Timeout == Timeout.InfiniteTimeSpan || options.StableRunPeriod < options.Timeout;
        }

        private static void ValidateOptions(QmlRunOptions options)
        {
            if (options.Timeout <= TimeSpan.Zero && options.Timeout != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    options.Timeout,
                    "QmlRunOptions.Timeout must be positive or Timeout.InfiniteTimeSpan.");
            }

            if (options.StableRunPeriod <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    options.StableRunPeriod,
                    "QmlRunOptions.StableRunPeriod must be positive.");
            }
        }

        private static bool IsDecimal(string value)
        {
            return value.Length > 0 && value.All(char.IsAsciiDigit);
        }

        private static bool IsSamePath(string? left, string right)
        {
            if (left is null)
            {
                return false;
            }

            string normalizedLeft = NormalizePathKey(left);
            string normalizedRight = NormalizePathKey(right);
            return GetPathComparer().Equals(normalizedLeft, normalizedRight);
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
                // Best-effort normalization: keep the original candidate when the path is invalid.
            }
            catch (NotSupportedException)
            {
                // Best-effort normalization: keep the original candidate when the path format is unsupported.
            }
            catch (PathTooLongException)
            {
                // Best-effort normalization: keep the original candidate when full-path resolution exceeds limits.
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

        private static string FormatCommand(string toolPath, ImmutableArray<string> args)
        {
            IEnumerable<string> parts = [toolPath, .. args];
            return string.Join(" ", parts.Select(QuoteCommandPart));
        }

        private static string QuoteCommandPart(string value)
        {
            if (value.Length == 0)
            {
                return "\"\"";
            }

            bool needsQuoting = value.Any(static ch => char.IsWhiteSpace(ch) || ch is '"' or '\'');
            if (!needsQuoting)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
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

        private sealed record ParsedLocation(string? File, int? Line, int? Column);

        private sealed record RuntimeLocation(string? File, int? Line, int? Column, string Message);
    }
}
