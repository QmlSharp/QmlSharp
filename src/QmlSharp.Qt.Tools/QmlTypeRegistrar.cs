using System.Globalization;

namespace QmlSharp.Qt.Tools
{
    /// <summary>Default qmltyperegistrar wrapper.</summary>
    public sealed class QmlTypeRegistrar : IQmlTypeRegistrar
    {
        private const string ToolName = "qmltyperegistrar";

        private readonly IQtToolchain _toolchain;
        private readonly IToolRunner _toolRunner;
        private readonly IQtDiagnosticParser _diagnosticParser;

        /// <summary>Create a qmltyperegistrar wrapper backed by default Qt discovery and process execution.</summary>
        public QmlTypeRegistrar()
            : this(new QtToolchain(), new ToolRunner(), new QtDiagnosticParser())
        {
        }

        /// <summary>Create a qmltyperegistrar wrapper using explicit infrastructure services.</summary>
        public QmlTypeRegistrar(IQtToolchain toolchain, IToolRunner toolRunner, IQtDiagnosticParser diagnosticParser)
        {
            _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
            _toolRunner = toolRunner ?? throw new ArgumentNullException(nameof(toolRunner));
            _diagnosticParser = diagnosticParser ?? throw new ArgumentNullException(nameof(diagnosticParser));
        }

        /// <inheritdoc />
        public async Task<TypeRegistrarResult> RegisterAsync(
            string metatypesJsonPath,
            TypeRegistrarOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(metatypesJsonPath);

            string normalizedMetatypesJsonPath = Path.GetFullPath(metatypesJsonPath);
            TypeRegistrarOptions effectiveOptions = options ?? new TypeRegistrarOptions();
            string? outputFile = NormalizeOutputPath(effectiveOptions.OutputFile);
            if (outputFile is not null)
            {
                EnsureParentDirectory(outputFile);
            }

            ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
            ImmutableArray<string> args = BuildArguments(normalizedMetatypesJsonPath, outputFile, effectiveOptions);
            ToolResult toolResult = await _toolRunner
                .RunAsync(tool.Path, args, new ToolRunnerOptions(), ct)
                .ConfigureAwait(false);

            ImmutableArray<QtDiagnostic> diagnostics = _diagnosticParser.ParseStderr(toolResult.Stderr);
            if (!toolResult.Success && diagnostics.IsDefaultOrEmpty)
            {
                diagnostics = [CreateFallbackDiagnostic(toolResult, normalizedMetatypesJsonPath)];
            }

            return new TypeRegistrarResult
            {
                ToolResult = toolResult,
                OutputFile = GetOutputFile(toolResult, outputFile),
                Diagnostics = diagnostics,
            };
        }

        internal static ImmutableArray<string> BuildArguments(
            string metatypesJsonPath,
            string? outputFile,
            TypeRegistrarOptions options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(metatypesJsonPath);
            ArgumentNullException.ThrowIfNull(options);

            ImmutableArray<string>.Builder args = ImmutableArray.CreateBuilder<string>();
            if (!string.IsNullOrWhiteSpace(outputFile))
            {
                args.Add("-o");
                args.Add(outputFile);
            }

            if (!string.IsNullOrWhiteSpace(options.ModuleImportUri))
            {
                args.Add("--import-name");
                args.Add(options.ModuleImportUri);
            }

            if (options.MajorVersion is int majorVersion)
            {
                args.Add("--major-version");
                args.Add(majorVersion.ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(options.Namespace))
            {
                args.Add("--namespace");
                args.Add(options.Namespace);
            }

            if (!string.IsNullOrWhiteSpace(options.ForeignTypesFile))
            {
                args.Add("--foreign-types");
                args.Add(options.ForeignTypesFile);
            }

            args.Add(metatypesJsonPath);
            return args.ToImmutable();
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
                ?? $"qmltyperegistrar failed with exit code {toolResult.ExitCode}.";

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
