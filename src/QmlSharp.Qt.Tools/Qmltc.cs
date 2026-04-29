namespace QmlSharp.Qt.Tools
{
    /// <summary>Default qmltc wrapper.</summary>
    public sealed class Qmltc : IQmltc
    {
        private const string ToolName = "qmltc";

        private readonly IQtToolchain _toolchain;
        private readonly IToolRunner _toolRunner;
        private readonly IQtDiagnosticParser _diagnosticParser;

        /// <summary>Create a qmltc wrapper backed by default Qt discovery and process execution.</summary>
        public Qmltc()
            : this(new QtToolchain(), new ToolRunner(), new QtDiagnosticParser())
        {
        }

        /// <summary>Create a qmltc wrapper using explicit infrastructure services.</summary>
        public Qmltc(IQtToolchain toolchain, IToolRunner toolRunner, IQtDiagnosticParser diagnosticParser)
        {
            _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
            _toolRunner = toolRunner ?? throw new ArgumentNullException(nameof(toolRunner));
            _diagnosticParser = diagnosticParser ?? throw new ArgumentNullException(nameof(diagnosticParser));
        }

        /// <inheritdoc />
        public async Task<QmltcResult> CompileFileAsync(
            string filePath,
            QmltcOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string normalizedFilePath = Path.GetFullPath(filePath);
            QmltcOptions effectiveOptions = options ?? new QmltcOptions();
            GeneratedPaths generatedPaths = CreateGeneratedPaths(normalizedFilePath, effectiveOptions);
            string resourceFile = CreateResourceFile(normalizedFilePath);

            try
            {
                ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
                ImmutableArray<string> args = BuildArguments(normalizedFilePath, generatedPaths, resourceFile, effectiveOptions);

                ToolResult toolResult = await _toolRunner
                    .RunAsync(tool.Path, args, new ToolRunnerOptions(), ct)
                    .ConfigureAwait(false);

                ImmutableArray<QtDiagnostic> diagnostics = _diagnosticParser.ParseStderr(toolResult.Stderr);
                if (!toolResult.Success && diagnostics.IsDefaultOrEmpty)
                {
                    diagnostics = [CreateFallbackDiagnostic(toolResult, normalizedFilePath)];
                }

                return new QmltcResult
                {
                    ToolResult = toolResult,
                    GeneratedHeader = GetGeneratedPath(toolResult, generatedPaths.HeaderPath),
                    GeneratedSource = GetGeneratedPath(toolResult, generatedPaths.SourcePath),
                    Diagnostics = diagnostics,
                };
            }
            finally
            {
                TryDeleteFile(resourceFile);
            }
        }

        internal static ImmutableArray<string> BuildArguments(
            string filePath,
            GeneratedPaths generatedPaths,
            string resourceFile,
            QmltcOptions options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(generatedPaths);
            ArgumentException.ThrowIfNullOrWhiteSpace(resourceFile);
            ArgumentNullException.ThrowIfNull(options);

            ImmutableArray<string>.Builder args = ImmutableArray.CreateBuilder<string>();
            args.Add("--impl");
            args.Add(generatedPaths.SourcePath);
            args.Add("--header");
            args.Add(generatedPaths.HeaderPath);
            args.Add("--resource");
            args.Add(resourceFile);

            if (!string.IsNullOrWhiteSpace(options.Namespace))
            {
                args.Add("--namespace");
                args.Add(options.Namespace);
            }

            if (!string.IsNullOrWhiteSpace(options.Module))
            {
                args.Add("--module");
                args.Add(options.Module);
            }

            if (!string.IsNullOrWhiteSpace(options.ExportMacro))
            {
                args.Add("--export");
                args.Add(options.ExportMacro);
            }

            args.Add(filePath);
            return args.ToImmutable();
        }

        private static string CreateResourceFile(string filePath)
        {
            string resourceFile = Path.Join(
                Path.GetTempPath(),
                "qmlsharp-qmltc-resource-" + Guid.NewGuid().ToString("N") + ".qrc");
            string escapedAlias = EscapeXml(Path.GetFileName(filePath));
            string escapedFile = EscapeXml(filePath);
            File.WriteAllText(
                resourceFile,
                string.Concat(
                    "<RCC><qresource prefix=\"/\">",
                    "<file alias=\"",
                    escapedAlias,
                    "\">",
                    escapedFile,
                    "</file></qresource></RCC>"));
            return resourceFile;
        }

        private static GeneratedPaths CreateGeneratedPaths(string filePath, QmltcOptions options)
        {
            string? header = NormalizeOutputPath(options.OutputHeader);
            string? source = NormalizeOutputPath(options.OutputSource);
            if (header is not null && source is not null)
            {
                EnsureParentDirectory(header);
                EnsureParentDirectory(source);
                return new GeneratedPaths(header, source);
            }

            string outputDirectory = Path.Join(Path.GetTempPath(), "qmlsharp-qmltc-out-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(outputDirectory);
            string baseName = Path.GetFileNameWithoutExtension(filePath);
            header ??= Path.Join(outputDirectory, baseName + ".h");
            source ??= Path.Join(outputDirectory, baseName + ".cpp");
            EnsureParentDirectory(header);
            EnsureParentDirectory(source);
            return new GeneratedPaths(header, source);
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

        private static string? GetGeneratedPath(ToolResult toolResult, string path)
        {
            return toolResult.Success || File.Exists(path) ? path : null;
        }

        private static QtDiagnostic CreateFallbackDiagnostic(ToolResult toolResult, string filePath)
        {
            string message = FirstNonEmptyLine(toolResult.Stderr)
                ?? FirstNonEmptyLine(toolResult.Stdout)
                ?? $"qmltc failed with exit code {toolResult.ExitCode}.";

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

        private static string EscapeXml(string value)
        {
            return value
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);
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
                // Best-effort cleanup for temporary qmltc resource files.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup for temporary qmltc resource files.
            }
        }

        internal sealed record GeneratedPaths(string HeaderPath, string SourcePath);
    }
}
