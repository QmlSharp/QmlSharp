using System.Globalization;

namespace QmlSharp.Qt.Tools
{
    /// <summary>Default qmlformat wrapper.</summary>
    public sealed class QmlFormat : IQmlFormat
    {
        private const string ToolName = "qmlformat";
        private const string StringInputFileName = "<string>";

        private readonly IQtToolchain _toolchain;
        private readonly IToolRunner _toolRunner;
        private readonly IQtDiagnosticParser _diagnosticParser;

        /// <summary>Create a qmlformat wrapper backed by default Qt discovery and process execution.</summary>
        public QmlFormat()
            : this(new QtToolchain(), new ToolRunner(), new QtDiagnosticParser())
        {
        }

        /// <summary>Create a qmlformat wrapper using explicit infrastructure services.</summary>
        public QmlFormat(IQtToolchain toolchain, IToolRunner toolRunner, IQtDiagnosticParser diagnosticParser)
        {
            _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
            _toolRunner = toolRunner ?? throw new ArgumentNullException(nameof(toolRunner));
            _diagnosticParser = diagnosticParser ?? throw new ArgumentNullException(nameof(diagnosticParser));
        }

        /// <inheritdoc />
        public async Task<QmlFormatResult> FormatFileAsync(
            string filePath,
            QmlFormatOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string normalizedFilePath = Path.GetFullPath(filePath);
            string beforeText = await File.ReadAllTextAsync(normalizedFilePath, ct).ConfigureAwait(false);
            QmlFormatOptions effectiveOptions = options ?? new QmlFormatOptions();
            ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
            ImmutableArray<string> args = BuildArguments(normalizedFilePath, effectiveOptions);

            ToolResult toolResult = await _toolRunner
                .RunAsync(tool.Path, args, new ToolRunnerOptions(), ct)
                .ConfigureAwait(false);

            return await CreateResultAsync(
                    toolResult,
                    normalizedFilePath,
                    beforeText,
                    effectiveOptions,
                    filenameOverride: null,
                    ct)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<QmlFormatResult> FormatStringAsync(
            string qmlSource,
            QmlFormatOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(qmlSource);

            string tempFile = Path.Join(
                Path.GetTempPath(),
                "qmlsharp-qmlformat-" + Guid.NewGuid().ToString("N") + ".qml");

            try
            {
                await File.WriteAllTextAsync(tempFile, qmlSource, ct).ConfigureAwait(false);

                QmlFormatOptions effectiveOptions = options ?? new QmlFormatOptions();
                ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
                ImmutableArray<string> args = BuildArguments(tempFile, effectiveOptions);

                ToolResult toolResult = await _toolRunner
                    .RunAsync(tool.Path, args, new ToolRunnerOptions(), ct)
                    .ConfigureAwait(false);

                return await CreateResultAsync(
                        toolResult,
                        tempFile,
                        qmlSource,
                        effectiveOptions,
                        StringInputFileName,
                        ct)
                    .ConfigureAwait(false);
            }
            finally
            {
                TryDeleteFile(tempFile);
            }
        }

        /// <inheritdoc />
        public async Task<ImmutableArray<QmlFormatResult>> FormatBatchAsync(
            ImmutableArray<string> filePaths,
            QmlFormatOptions? options = null,
            CancellationToken ct = default)
        {
            ImmutableArray<QmlFormatResult>.Builder results = ImmutableArray.CreateBuilder<QmlFormatResult>(filePaths.Length);
            foreach (string filePath in filePaths)
            {
                results.Add(await FormatFileAsync(filePath, options, ct).ConfigureAwait(false));
            }

            return results.MoveToImmutable();
        }

        internal static ImmutableArray<string> BuildArguments(string filePath, QmlFormatOptions options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(options);

            ImmutableArray<string>.Builder args = ImmutableArray.CreateBuilder<string>();
            AddExecutionArguments(args, options);
            AddFormattingArguments(args, options);
            args.Add(filePath);
            return args.ToImmutable();
        }

        private static void AddExecutionArguments(ImmutableArray<string>.Builder args, QmlFormatOptions options)
        {
            if (options.IgnoreSettings)
            {
                args.Add("--ignore-settings");
            }

            if (options.Force)
            {
                args.Add("--force");
            }

            if (options.InPlace)
            {
                args.Add("--inplace");
            }
        }

        private static void AddFormattingArguments(ImmutableArray<string>.Builder args, QmlFormatOptions options)
        {
            if (options.UseTabs)
            {
                args.Add("--tabs");
            }

            if (options.IndentWidth != 4)
            {
                args.Add("--indent-width");
                args.Add(options.IndentWidth.ToString(CultureInfo.InvariantCulture));
            }

            if (options.ColumnWidth > 0)
            {
                args.Add("--column-width");
                args.Add(options.ColumnWidth.ToString(CultureInfo.InvariantCulture));
            }

            if (options.Normalize)
            {
                args.Add("--normalize");
            }

            if (!string.IsNullOrWhiteSpace(options.Newline))
            {
                args.Add("--newline");
                args.Add(MapNewline(options.Newline));
            }

            if (options.SortImports)
            {
                args.Add("--sort-imports");
            }

            string? semicolonRule = MapSemicolonRule(options.SemicolonRule);
            if (semicolonRule is not null)
            {
                args.Add("--semicolon-rule");
                args.Add(semicolonRule);
            }
        }

        private async Task<QmlFormatResult> CreateResultAsync(
            ToolResult toolResult,
            string filePath,
            string beforeText,
            QmlFormatOptions options,
            string? filenameOverride,
            CancellationToken ct)
        {
            string? formattedSource = null;
            if (toolResult.Success)
            {
                formattedSource = options.InPlace
                    ? await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false)
                    : toolResult.Stdout;
            }

            ImmutableArray<QtDiagnostic> diagnostics = _diagnosticParser.ParseStderr(
                toolResult.Stderr,
                filenameOverride);
            if (!toolResult.Success && diagnostics.IsDefaultOrEmpty)
            {
                diagnostics = [CreateFallbackDiagnostic(toolResult, filenameOverride ?? filePath)];
            }

            return new QmlFormatResult
            {
                ToolResult = toolResult,
                FormattedSource = formattedSource,
                HasChanges = formattedSource is not null && HasTextChanged(beforeText, formattedSource),
                Diagnostics = diagnostics,
            };
        }

        private static QtDiagnostic CreateFallbackDiagnostic(ToolResult toolResult, string filePath)
        {
            string message = FirstNonEmptyLine(toolResult.Stderr)
                ?? FirstNonEmptyLine(toolResult.Stdout)
                ?? $"qmlformat failed with exit code {toolResult.ExitCode}.";

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

        private static bool HasTextChanged(string beforeText, string formattedSource)
        {
            return !string.Equals(NormalizeForChangeComparison(beforeText), NormalizeForChangeComparison(formattedSource), StringComparison.Ordinal);
        }

        private static string NormalizeForChangeComparison(string text)
        {
            return text.Replace("\r", string.Empty, StringComparison.Ordinal).TrimEnd();
        }

        private static string? MapSemicolonRule(string? semicolonRule)
        {
            if (string.IsNullOrWhiteSpace(semicolonRule))
            {
                return null;
            }

            return semicolonRule.Trim().ToLowerInvariant() switch
            {
                "add" or "always" => "always",
                "remove" or "essential" => "essential",
                "preserve" => null,
                string value => value,
            };
        }

        private static string MapNewline(string newline)
        {
            return newline.Trim().ToLowerInvariant() switch
            {
                "native" => "native",
                "windows" => "windows",
                "unix" => "unix",
                "macos" => "macos",
                string value => value,
            };
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
