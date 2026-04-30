using System.Xml.Linq;

namespace QmlSharp.Qt.Tools
{
    /// <summary>Default rcc wrapper.</summary>
    public sealed class Rcc : IRcc
    {
        private const string ToolName = "rcc";

        private readonly IQtToolchain _toolchain;
        private readonly IToolRunner _toolRunner;
        private readonly IQtDiagnosticParser _diagnosticParser;

        /// <summary>Create an rcc wrapper backed by default Qt discovery and process execution.</summary>
        public Rcc()
            : this(new QtToolchain(), new ToolRunner(), new QtDiagnosticParser())
        {
        }

        /// <summary>Create an rcc wrapper using explicit infrastructure services.</summary>
        public Rcc(IQtToolchain toolchain, IToolRunner toolRunner, IQtDiagnosticParser diagnosticParser)
        {
            _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
            _toolRunner = toolRunner ?? throw new ArgumentNullException(nameof(toolRunner));
            _diagnosticParser = diagnosticParser ?? throw new ArgumentNullException(nameof(diagnosticParser));
        }

        /// <inheritdoc />
        public async Task<RccResult> CompileAsync(
            string qrcFilePath,
            RccOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(qrcFilePath);

            string normalizedQrcFilePath = Path.GetFullPath(qrcFilePath);
            RccOptions effectiveOptions = options ?? new RccOptions();
            string? outputFile = NormalizeOutputPath(effectiveOptions.OutputFile);
            if (outputFile is not null)
            {
                EnsureParentDirectory(outputFile);
            }

            ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
            ImmutableArray<string> args = BuildCompileArguments(normalizedQrcFilePath, outputFile, effectiveOptions);
            ToolResult toolResult = await _toolRunner
                .RunAsync(tool.Path, args, new ToolRunnerOptions(), ct)
                .ConfigureAwait(false);

            ImmutableArray<QtDiagnostic> diagnostics = _diagnosticParser.ParseStderr(toolResult.Stderr);
            if (!toolResult.Success && diagnostics.IsDefaultOrEmpty)
            {
                diagnostics = [CreateFallbackDiagnostic(toolResult, normalizedQrcFilePath)];
            }

            return new RccResult
            {
                ToolResult = toolResult,
                OutputFile = GetOutputFile(toolResult, outputFile),
                Diagnostics = diagnostics,
            };
        }

        /// <inheritdoc />
        public async Task<ImmutableArray<string>> ListEntriesAsync(
            string qrcFilePath,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(qrcFilePath);

            string normalizedQrcFilePath = Path.GetFullPath(qrcFilePath);
            ToolResult toolResult = await RunListAsync("--list", normalizedQrcFilePath, ct).ConfigureAwait(false);
            return toolResult.Success ? ParseNonEmptyLines(toolResult.Stdout) : [];
        }

        /// <inheritdoc />
        public async Task<ImmutableArray<RccMapping>> ListMappingsAsync(
            string qrcFilePath,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(qrcFilePath);

            string normalizedQrcFilePath = Path.GetFullPath(qrcFilePath);
            ToolResult toolResult = await RunListAsync("--list-mapping", normalizedQrcFilePath, ct).ConfigureAwait(false);
            return toolResult.Success ? ParseMappings(toolResult.Stdout) : [];
        }

        /// <inheritdoc />
        public Task<string> CreateQrcXmlAsync(
            ImmutableArray<RccFileEntry> files,
            string? prefix = null,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (files.IsDefault)
            {
                files = [];
            }

            string effectivePrefix = string.IsNullOrWhiteSpace(prefix) ? "/" : prefix.Trim();
            XDocument document = new(
                new XDocumentType("RCC", null, null, null),
                new XElement(
                    "RCC",
                    new XElement(
                        "qresource",
                        new XAttribute("prefix", effectivePrefix),
                        files.Select(CreateFileElement))));

            return Task.FromResult(document.ToString(SaveOptions.DisableFormatting) + "\n");
        }

        internal static ImmutableArray<string> BuildCompileArguments(
            string qrcFilePath,
            string? outputFile,
            RccOptions options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(qrcFilePath);
            ArgumentNullException.ThrowIfNull(options);

            ImmutableArray<string>.Builder args = ImmutableArray.CreateBuilder<string>();

            if (!string.IsNullOrWhiteSpace(outputFile))
            {
                args.Add("--output");
                args.Add(outputFile);
            }

            if (options.BinaryMode)
            {
                args.Add("--binary");
            }

            if (options.NoCompress)
            {
                args.Add("--no-compress");
            }

            if (options.PythonOutput)
            {
                args.Add("--generator");
                args.Add("python");
            }

            args.Add(qrcFilePath);
            return args.ToImmutable();
        }

        internal static ImmutableArray<RccMapping> ParseMappings(string stdout)
        {
            if (string.IsNullOrWhiteSpace(stdout))
            {
                return [];
            }

            ImmutableArray<RccMapping>.Builder mappings = ImmutableArray.CreateBuilder<RccMapping>();
            foreach (string line in SplitLines(stdout))
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.Length == 0)
                {
                    continue;
                }

                string[] parts = trimmedLine.Split('\t', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                mappings.Add(new RccMapping
                {
                    ResourcePath = NormalizeResourcePath(parts[0]),
                    FilePath = parts[1],
                });
            }

            return mappings.ToImmutable();
        }

        private async Task<ToolResult> RunListAsync(string listOption, string qrcFilePath, CancellationToken ct)
        {
            ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
            return await _toolRunner
                .RunAsync(tool.Path, [listOption, qrcFilePath], new ToolRunnerOptions(), ct)
                .ConfigureAwait(false);
        }

        private static ImmutableArray<string> ParseNonEmptyLines(string stdout)
        {
            return SplitLines(stdout)
                .Select(static line => line.Trim())
                .Where(static line => line.Length > 0)
                .ToImmutableArray();
        }

        private static IEnumerable<string> SplitLines(string value)
        {
            return value
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');
        }

        private static XElement CreateFileElement(RccFileEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (string.IsNullOrWhiteSpace(entry.FilePath))
            {
                throw new ArgumentException("Resource file paths cannot be null, empty, or whitespace.", nameof(entry));
            }

            XElement file = new("file", entry.FilePath);
            if (!string.IsNullOrWhiteSpace(entry.Alias))
            {
                file.SetAttributeValue("alias", entry.Alias);
            }

            return file;
        }

        private static string NormalizeResourcePath(string resourcePath)
        {
            string normalized = resourcePath.Trim();
            if (normalized.StartsWith(":", StringComparison.Ordinal))
            {
                normalized = normalized[1..];
            }

            return normalized.Length == 0 ? "/" : normalized;
        }

        private static string? NormalizeOutputPath(string? path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        }

        private static void EnsureParentDirectory(string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }
        }

        private static string? GetOutputFile(ToolResult toolResult, string? outputFile)
        {
            return outputFile is not null && (toolResult.Success || File.Exists(outputFile))
                ? outputFile
                : null;
        }

        private static QtDiagnostic CreateFallbackDiagnostic(ToolResult toolResult, string filePath)
        {
            string message = FirstNonEmptyLine(toolResult.Stderr)
                ?? FirstNonEmptyLine(toolResult.Stdout)
                ?? $"rcc failed with exit code {toolResult.ExitCode}.";

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
    }
}
