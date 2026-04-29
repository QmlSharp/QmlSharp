using System.Globalization;
using System.Text.Json;

namespace QmlSharp.Qt.Tools
{
    /// <summary>Default qmlimportscanner wrapper.</summary>
    public sealed class QmlImportScanner : IQmlImportScanner
    {
        private const string ToolName = "qmlimportscanner";

        private readonly IQtToolchain _toolchain;
        private readonly IToolRunner _toolRunner;

        /// <summary>Create a qmlimportscanner wrapper backed by default Qt discovery and process execution.</summary>
        public QmlImportScanner()
            : this(new QtToolchain(), new ToolRunner())
        {
        }

        /// <summary>Create a qmlimportscanner wrapper using explicit infrastructure services.</summary>
        public QmlImportScanner(IQtToolchain toolchain, IToolRunner toolRunner)
        {
            _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
            _toolRunner = toolRunner ?? throw new ArgumentNullException(nameof(toolRunner));
        }

        /// <inheritdoc />
        public async Task<QmlImportScanResult> ScanDirectoryAsync(
            string directoryPath,
            QmlImportScanOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

            QmlImportScanOptions effectiveOptions = options ?? new QmlImportScanOptions();
            string rootPath = Path.GetFullPath(effectiveOptions.RootPath ?? directoryPath);
            ToolResult toolResult = await RunAsync(
                    BuildDirectoryArguments(rootPath, effectiveOptions, _toolchain.Installation?.ImportPaths ?? []),
                    ct)
                .ConfigureAwait(false);

            return CreateResult(toolResult);
        }

        /// <inheritdoc />
        public async Task<QmlImportScanResult> ScanFilesAsync(
            ImmutableArray<string> filePaths,
            QmlImportScanOptions? options = null,
            CancellationToken ct = default)
        {
            if (filePaths.IsDefaultOrEmpty)
            {
                return new QmlImportScanResult
                {
                    ToolResult = CreateSyntheticSuccessResult(),
                    Imports = [],
                };
            }

            QmlImportScanOptions effectiveOptions = options ?? new QmlImportScanOptions();
            ImmutableArray<string> normalizedFilePaths = filePaths
                .Select(static filePath =>
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
                    return Path.GetFullPath(filePath);
                })
                .ToImmutableArray();

            ToolResult toolResult = await RunAsync(
                    BuildFilesArguments(normalizedFilePaths, effectiveOptions, _toolchain.Installation?.ImportPaths ?? []),
                    ct)
                .ConfigureAwait(false);

            return CreateResult(toolResult);
        }

        /// <inheritdoc />
        public async Task<QmlImportScanResult> ScanStringAsync(
            string qmlSource,
            QmlImportScanOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(qmlSource);

            string tempFile = Path.Join(
                Path.GetTempPath(),
                "qmlsharp-qmlimportscanner-" + Guid.NewGuid().ToString("N") + ".qml");

            try
            {
                await File.WriteAllTextAsync(tempFile, qmlSource, ct).ConfigureAwait(false);
                return await ScanFilesAsync([tempFile], options, ct).ConfigureAwait(false);
            }
            finally
            {
                TryDeleteFile(tempFile);
            }
        }

        internal static ImmutableArray<string> BuildDirectoryArguments(
            string rootPath,
            QmlImportScanOptions options,
            ImmutableArray<string> toolchainImportPaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
            ArgumentNullException.ThrowIfNull(options);

            ImmutableArray<string>.Builder args = ImmutableArray.CreateBuilder<string>();
            args.Add("-rootPath");
            args.Add(rootPath);
            AddImportPaths(args, toolchainImportPaths);
            AddImportPaths(args, options.ImportPaths);
            AddExcludeDirs(args, options.ExcludeDirs);
            return args.ToImmutable();
        }

        internal static ImmutableArray<string> BuildFilesArguments(
            ImmutableArray<string> filePaths,
            QmlImportScanOptions options,
            ImmutableArray<string> toolchainImportPaths)
        {
            ArgumentNullException.ThrowIfNull(options);

            ImmutableArray<string>.Builder args = ImmutableArray.CreateBuilder<string>();
            args.Add("-qmlFiles");
            foreach (string filePath in filePaths)
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    throw new ArgumentException("File paths cannot contain null, empty, or whitespace entries.", nameof(filePaths));
                }

                args.Add(filePath);
            }

            AddImportPaths(args, toolchainImportPaths);
            AddImportPaths(args, options.ImportPaths);
            AddExcludeDirs(args, options.ExcludeDirs);
            return args.ToImmutable();
        }

        internal static ImmutableArray<QmlImportEntry> ParseImports(string stdout)
        {
            if (string.IsNullOrWhiteSpace(stdout))
            {
                return [];
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(stdout);
                JsonElement entries = document.RootElement;
                if (entries.ValueKind == JsonValueKind.Object
                    && entries.TryGetProperty("imports", out JsonElement importsElement))
                {
                    entries = importsElement;
                }

                if (entries.ValueKind != JsonValueKind.Array)
                {
                    return [];
                }

                ImmutableArray<QmlImportEntry>.Builder imports = ImmutableArray.CreateBuilder<QmlImportEntry>();
                foreach (JsonElement entry in entries.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    imports.Add(new QmlImportEntry
                    {
                        Name = ReadString(entry, "name") ?? string.Empty,
                        Type = ReadString(entry, "type") ?? "module",
                        Path = ReadString(entry, "path"),
                        Version = ReadVersion(entry),
                    });
                }

                return imports.ToImmutable();
            }
            catch (JsonException)
            {
                return [];
            }
        }

        private async Task<ToolResult> RunAsync(ImmutableArray<string> args, CancellationToken ct)
        {
            ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
            return await _toolRunner
                .RunAsync(tool.Path, args, new ToolRunnerOptions(), ct)
                .ConfigureAwait(false);
        }

        private static QmlImportScanResult CreateResult(ToolResult toolResult)
        {
            return new QmlImportScanResult
            {
                ToolResult = toolResult,
                Imports = toolResult.Success ? ParseImports(toolResult.Stdout) : [],
            };
        }

        private static ToolResult CreateSyntheticSuccessResult()
        {
            return new ToolResult
            {
                ExitCode = 0,
                Stdout = string.Empty,
                Stderr = string.Empty,
                DurationMs = 0,
                Command = ToolName,
            };
        }

        private static void AddImportPaths(ImmutableArray<string>.Builder args, ImmutableArray<string> importPaths)
        {
            foreach (string importPath in importPaths
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(GetPathComparer()))
            {
                args.Add("-importPath");
                args.Add(importPath);
            }
        }

        private static void AddExcludeDirs(ImmutableArray<string>.Builder args, ImmutableArray<string> excludeDirs)
        {
            foreach (string excludeDir in excludeDirs
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(GetPathComparer()))
            {
                args.Add("-exclude");
                args.Add(excludeDir);
            }
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement property)
                && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        private static string? ReadVersion(JsonElement element)
        {
            string? version = ReadString(element, "version");
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }

            int? major = ReadInt32(element, "majorVersion") ?? ReadInt32(element, "versionMajor");
            int? minor = ReadInt32(element, "minorVersion") ?? ReadInt32(element, "versionMinor");
            if (major is null)
            {
                return null;
            }

            return minor is null
                ? major.Value.ToString(CultureInfo.InvariantCulture)
                : string.Create(CultureInfo.InvariantCulture, $"{major.Value}.{minor.Value}");
        }

        private static int? ReadInt32(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property))
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int value))
            {
                return value;
            }

            return null;
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
