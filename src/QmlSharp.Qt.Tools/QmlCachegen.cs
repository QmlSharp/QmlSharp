using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QmlSharp.Qt.Tools
{
    /// <summary>Default qmlcachegen wrapper.</summary>
    public sealed class QmlCachegen : IQmlCachegen
    {
        private const string ToolName = "qmlcachegen";
        private const string StringInputFileName = "<string>";

        private readonly IQtToolchain _toolchain;
        private readonly IToolRunner _toolRunner;
        private readonly IQtDiagnosticParser _diagnosticParser;

        /// <summary>Create a qmlcachegen wrapper backed by default Qt discovery and process execution.</summary>
        public QmlCachegen()
            : this(new QtToolchain(), new ToolRunner(), new QtDiagnosticParser())
        {
        }

        /// <summary>Create a qmlcachegen wrapper using explicit infrastructure services.</summary>
        public QmlCachegen(IQtToolchain toolchain, IToolRunner toolRunner, IQtDiagnosticParser diagnosticParser)
        {
            _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
            _toolRunner = toolRunner ?? throw new ArgumentNullException(nameof(toolRunner));
            _diagnosticParser = diagnosticParser ?? throw new ArgumentNullException(nameof(diagnosticParser));
        }

        /// <inheritdoc />
        public async Task<QmlCachegenResult> CompileFileAsync(
            string filePath,
            QmlCachegenOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string normalizedFilePath = Path.GetFullPath(filePath);
            QmlCachegenOptions effectiveOptions = options ?? new QmlCachegenOptions();
            string outputDirectory = GetOrCreateOutputDirectory(effectiveOptions.OutputDir);
            string resourcePath = CreateResourcePath(filePath);
            string outputFile = CreateOutputFilePath(resourcePath, outputDirectory, effectiveOptions);
            ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
            ImmutableArray<string> args = BuildArguments(
                normalizedFilePath,
                outputFile,
                resourcePath,
                effectiveOptions,
                _toolchain.Installation?.ImportPaths ?? []);

            ToolResult toolResult = await _toolRunner
                .RunAsync(tool.Path, args, new ToolRunnerOptions(), ct)
                .ConfigureAwait(false);

            return CreateResult(toolResult, normalizedFilePath, outputFile, outputDirectory, filenameOverride: null);
        }

        /// <inheritdoc />
        public async Task<QmlCachegenResult> CompileStringAsync(
            string qmlSource,
            QmlCachegenOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(qmlSource);

            string tempFile = Path.Join(
                Path.GetTempPath(),
                "qmlsharp-qmlcachegen-" + Guid.NewGuid().ToString("N") + ".qml");

            try
            {
                await File.WriteAllTextAsync(tempFile, qmlSource, ct).ConfigureAwait(false);
                return await CompileFileCoreAsync(tempFile, options, StringInputFileName, ct).ConfigureAwait(false);
            }
            finally
            {
                TryDeleteFile(tempFile);
            }
        }

        /// <inheritdoc />
        public async Task<QmlCachegenBatchResult> CompileBatchAsync(
            ImmutableArray<string> filePaths,
            QmlCachegenOptions? options = null,
            CancellationToken ct = default)
        {
            if (filePaths.IsDefaultOrEmpty)
            {
                return new QmlCachegenBatchResult
                {
                    Results = [],
                    SuccessCount = 0,
                    FailureCount = 0,
                    TotalDurationMs = 0,
                };
            }

            ImmutableArray<QmlCachegenResult>.Builder results = ImmutableArray.CreateBuilder<QmlCachegenResult>(filePaths.Length);
            long totalDurationMs = 0;
            foreach (string filePath in filePaths)
            {
                QmlCachegenResult result = await CompileFileAsync(filePath, options, ct).ConfigureAwait(false);
                results.Add(result);
                totalDurationMs += result.ToolResult.DurationMs;
            }

            ImmutableArray<QmlCachegenResult> immutableResults = results.MoveToImmutable();
            return new QmlCachegenBatchResult
            {
                Results = immutableResults,
                SuccessCount = immutableResults.Count(static result => result.Success),
                FailureCount = immutableResults.Count(static result => !result.Success),
                TotalDurationMs = totalDurationMs,
                AggregateStats = AggregateStats(immutableResults),
            };
        }

        internal static ImmutableArray<string> BuildArguments(
            string filePath,
            string outputFile,
            string resourcePath,
            QmlCachegenOptions options,
            ImmutableArray<string> toolchainImportPaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputFile);
            ArgumentException.ThrowIfNullOrWhiteSpace(resourcePath);
            ArgumentNullException.ThrowIfNull(options);

            ImmutableArray<string>.Builder args = ImmutableArray.CreateBuilder<string>();
            args.Add("-o");
            args.Add(outputFile);
            AddImportPaths(args, toolchainImportPaths);
            AddImportPaths(args, options.ImportPaths);

            if (options.BytecodeOnly)
            {
                args.Add("--only-bytecode");
            }

            if (options.WarningsAsErrors)
            {
                args.Add("--warnings-are-errors");
            }

            if (options.Verbose)
            {
                args.Add("--verbose");
            }

            args.Add("--resource-path");
            args.Add(resourcePath);
            args.Add("--dump-aot-stats");
            args.Add("--module-id");
            args.Add("QmlSharp");
            args.Add(filePath);
            return args.ToImmutable();
        }

        private async Task<QmlCachegenResult> CompileFileCoreAsync(
            string filePath,
            QmlCachegenOptions? options,
            string? filenameOverride,
            CancellationToken ct)
        {
            QmlCachegenOptions effectiveOptions = options ?? new QmlCachegenOptions();
            string outputDirectory = GetOrCreateOutputDirectory(effectiveOptions.OutputDir);
            string resourcePath = CreateResourcePath(filePath);
            string outputFile = CreateOutputFilePath(resourcePath, outputDirectory, effectiveOptions);
            ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
            ImmutableArray<string> args = BuildArguments(
                filePath,
                outputFile,
                resourcePath,
                effectiveOptions,
                _toolchain.Installation?.ImportPaths ?? []);

            ToolResult toolResult = await _toolRunner
                .RunAsync(tool.Path, args, new ToolRunnerOptions(), ct)
                .ConfigureAwait(false);

            return CreateResult(toolResult, filePath, outputFile, outputDirectory, filenameOverride);
        }

        private QmlCachegenResult CreateResult(
            ToolResult toolResult,
            string inputFile,
            string outputFile,
            string outputDirectory,
            string? filenameOverride)
        {
            ImmutableArray<QtDiagnostic> diagnostics = _diagnosticParser.ParseStderr(toolResult.Stderr, filenameOverride);
            diagnostics = RewriteStringInputDiagnostics(diagnostics, inputFile, filenameOverride);
            if (!toolResult.Success && diagnostics.IsDefaultOrEmpty)
            {
                diagnostics = [CreateFallbackDiagnostic(toolResult, filenameOverride ?? inputFile)];
            }

            return new QmlCachegenResult
            {
                ToolResult = toolResult,
                OutputFiles = DiscoverOutputFiles(toolResult, outputFile, outputDirectory),
                Diagnostics = diagnostics,
                AotStats = ParseAotStats(outputFile),
            };
        }

        private static ImmutableArray<string> DiscoverOutputFiles(
            ToolResult toolResult,
            string outputFile,
            string outputDirectory)
        {
            ImmutableArray<string>.Builder files = ImmutableArray.CreateBuilder<string>();
            if (!toolResult.Success)
            {
                return [];
            }

            files.Add(outputFile);
            if (Directory.Exists(outputDirectory))
            {
                foreach (string file in Directory
                    .EnumerateFiles(outputDirectory)
                    .Where(file => IsExpectedOutputFile(file, outputFile))
                    .Where(file => !files.Contains(file, GetPathComparer()))
                    .OrderBy(static file => file, GetPathComparer()))
                {
                    files.Add(file);
                }
            }

            return files.ToImmutable();
        }

        private static bool IsExpectedOutputFile(string filePath, string outputFile)
        {
            if (GetPathComparer().Equals(filePath, outputFile))
            {
                return true;
            }

            if (GetPathComparer().Equals(filePath, outputFile + ".aotstats"))
            {
                return true;
            }

            return false;
        }

        private static QmlAotStats? ParseAotStats(string outputFile)
        {
            string statsPath = outputFile + ".aotstats";
            if (!File.Exists(statsPath))
            {
                return null;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(statsPath));
                return ParseAotStatsDocument(document.RootElement);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static QmlAotStats ParseAotStatsDocument(JsonElement root)
        {
            AotAccumulator accumulator = new();
            VisitAotStats(root, accumulator);
            return new QmlAotStats
            {
                TotalFunctions = accumulator.TotalFunctions,
                CompiledFunctions = accumulator.CompiledFunctions,
                FailedFunctions = Math.Max(0, accumulator.TotalFunctions - accumulator.CompiledFunctions),
            };
        }

        private static void VisitAotStats(JsonElement element, AotAccumulator accumulator)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    AddAotEntryIfPresent(element, accumulator);
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        VisitAotStats(property.Value, accumulator);
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        VisitAotStats(item, accumulator);
                    }

                    break;
            }
        }

        private static void AddAotEntryIfPresent(JsonElement element, AotAccumulator accumulator)
        {
            if (element.TryGetProperty("codegenResult", out JsonElement codegenResult)
                && codegenResult.ValueKind == JsonValueKind.Number
                && codegenResult.TryGetInt32(out int result))
            {
                accumulator.TotalFunctions++;
                if (result == 0)
                {
                    accumulator.CompiledFunctions++;
                }

                return;
            }

            int? total = ReadInt32(element, "totalFunctions") ?? ReadInt32(element, "total");
            int? compiled = ReadInt32(element, "compiledFunctions") ?? ReadInt32(element, "compiled");
            int? failed = ReadInt32(element, "failedFunctions") ?? ReadInt32(element, "failed");
            if (total is not null || compiled is not null || failed is not null)
            {
                int totalFunctions = total ?? ((compiled ?? 0) + (failed ?? 0));
                accumulator.TotalFunctions += totalFunctions;
                accumulator.CompiledFunctions += compiled ?? Math.Max(0, totalFunctions - (failed ?? 0));
            }
        }

        private static int? ReadInt32(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property))
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int number))
            {
                return number;
            }

            if (property.ValueKind == JsonValueKind.String
                && int.TryParse(property.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            return null;
        }

        private static QmlAotStats? AggregateStats(ImmutableArray<QmlCachegenResult> results)
        {
            ImmutableArray<QmlAotStats> stats = results
                .Select(static result => result.AotStats)
                .Where(static stats => stats is not null)
                .Select(static stats => stats!)
                .ToImmutableArray();
            if (stats.IsEmpty)
            {
                return null;
            }

            return new QmlAotStats
            {
                TotalFunctions = stats.Sum(static item => item.TotalFunctions),
                CompiledFunctions = stats.Sum(static item => item.CompiledFunctions),
                FailedFunctions = stats.Sum(static item => item.FailedFunctions),
            };
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

        private static QtDiagnostic CreateFallbackDiagnostic(ToolResult toolResult, string filePath)
        {
            string message = FirstNonEmptyLine(toolResult.Stderr)
                ?? FirstNonEmptyLine(toolResult.Stdout)
                ?? $"qmlcachegen failed with exit code {toolResult.ExitCode}.";

            return new QtDiagnostic
            {
                File = filePath,
                Severity = DiagnosticSeverity.Error,
                Message = message,
            };
        }

        private static string? FirstNonEmptyLine(string text)
        {
            return text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(static line => line.Trim())
                .FirstOrDefault(static line => line.Length > 0);
        }

        private static string GetOrCreateOutputDirectory(string? outputDir)
        {
            string directory = string.IsNullOrWhiteSpace(outputDir)
                ? Path.Join(Path.GetTempPath(), "qmlsharp-qmlcachegen-out-" + Guid.NewGuid().ToString("N"))
                : Path.GetFullPath(outputDir);
            _ = Directory.CreateDirectory(directory);
            return directory;
        }

        internal static string CreateOutputFilePath(
            string resourcePath,
            string outputDirectory,
            QmlCachegenOptions options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(resourcePath);

            string extension = options.BytecodeOnly ? ".qmlc" : ".cpp";
            string normalizedResourcePath = resourcePath.Trim().Replace('\\', '/').TrimStart('/');
            string stem = CreateOutputStem(normalizedResourcePath);
            string hash = CreateStableHash(normalizedResourcePath);
            return Path.Join(outputDirectory, $"{stem}.{hash}{extension}");
        }

        internal static string CreateResourcePath(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string normalizedFilePath = Path.GetFullPath(filePath);
            string candidate = filePath.Trim();
            if (Path.IsPathFullyQualified(candidate))
            {
                candidate = TryCreateCurrentDirectoryRelativePath(normalizedFilePath)
                    ?? CreateOpaqueResourcePath(normalizedFilePath);
            }

            candidate = candidate
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/')
                .TrimStart('/');

            while (candidate.StartsWith("./", StringComparison.Ordinal))
            {
                candidate = candidate[2..];
            }

            if (candidate.Length == 0 || IsEscapedRelativePath(candidate))
            {
                candidate = CreateOpaqueResourcePath(normalizedFilePath);
            }

            return "/" + candidate;
        }

        private static string? TryCreateCurrentDirectoryRelativePath(string normalizedFilePath)
        {
            string relativePath = Path.GetRelativePath(Environment.CurrentDirectory, normalizedFilePath);
            string normalizedRelativePath = relativePath
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');

            return Path.IsPathFullyQualified(relativePath) || IsEscapedRelativePath(normalizedRelativePath)
                ? null
                : normalizedRelativePath;
        }

        private static string CreateOpaqueResourcePath(string normalizedFilePath)
        {
            return "absolute/" + CreateStableHash(NormalizePathKey(normalizedFilePath)) + "/" + Path.GetFileName(normalizedFilePath);
        }

        private static bool IsEscapedRelativePath(string path)
        {
            return string.Equals(path, "..", StringComparison.Ordinal)
                || path.StartsWith("../", StringComparison.Ordinal)
                || path.StartsWith("..\\", StringComparison.Ordinal);
        }

        private static string CreateOutputStem(string normalizedResourcePath)
        {
            string stem = string.Concat(normalizedResourcePath.Select(static character =>
                char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'
                    ? character
                    : '_'));
            stem = stem.Trim('_');
            if (stem.Length == 0)
            {
                return "qml";
            }

            return stem.Length > 96 ? stem[..96].TrimEnd('_') : stem;
        }

        private static string CreateStableHash(string value)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12].ToLowerInvariant();
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

        private sealed class AotAccumulator
        {
            public int TotalFunctions { get; set; }

            public int CompiledFunctions { get; set; }
        }
    }
}
