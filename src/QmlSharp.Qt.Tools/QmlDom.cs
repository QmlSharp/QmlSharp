using System.Text.Json;

namespace QmlSharp.Qt.Tools
{
    /// <summary>Default qmldom wrapper.</summary>
    public sealed class QmlDom : IQmlDom
    {
        private const string ToolName = "qmldom";

        private readonly IQtToolchain _toolchain;
        private readonly IToolRunner _toolRunner;

        /// <summary>Create a qmldom wrapper backed by default Qt discovery and process execution.</summary>
        public QmlDom()
            : this(new QtToolchain(), new ToolRunner())
        {
        }

        /// <summary>Create a qmldom wrapper using explicit infrastructure services.</summary>
        public QmlDom(IQtToolchain toolchain, IToolRunner toolRunner)
        {
            _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
            _toolRunner = toolRunner ?? throw new ArgumentNullException(nameof(toolRunner));
        }

        /// <inheritdoc />
        public async Task<QmlDomResult> DumpFileAsync(
            string filePath,
            QmlDomOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string normalizedFilePath = Path.GetFullPath(filePath);
            QmlDomOptions effectiveOptions = options ?? new QmlDomOptions();
            ToolInfo tool = await _toolchain.GetToolInfoAsync(ToolName, ct).ConfigureAwait(false);
            ImmutableArray<string> args = BuildArguments(normalizedFilePath, effectiveOptions);

            ToolResult toolResult = await _toolRunner
                .RunAsync(tool.Path, args, new ToolRunnerOptions(), ct)
                .ConfigureAwait(false);

            return CreateResult(toolResult, effectiveOptions);
        }

        /// <inheritdoc />
        public async Task<QmlDomResult> DumpStringAsync(
            string qmlSource,
            QmlDomOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(qmlSource);

            string tempFile = Path.Join(
                Path.GetTempPath(),
                "qmlsharp-qmldom-" + Guid.NewGuid().ToString("N") + ".qml");

            try
            {
                await File.WriteAllTextAsync(tempFile, qmlSource, ct).ConfigureAwait(false);
                return await DumpFileAsync(tempFile, options, ct).ConfigureAwait(false);
            }
            finally
            {
                TryDeleteFile(tempFile);
            }
        }

        internal static ImmutableArray<string> BuildArguments(string filePath, QmlDomOptions options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(options);

            ImmutableArray<string>.Builder args = ImmutableArray.CreateBuilder<string>();
            args.Add(options.AstMode ? "--dump-ast" : "-d");

            if (!options.FilterFields.IsDefaultOrEmpty)
            {
                args.Add("--filter-fields");
                args.Add(string.Join(",", options.FilterFields.Where(static field => !string.IsNullOrWhiteSpace(field))));
            }

            if (options.NoDependencies)
            {
                args.Add("-D");
                args.Add("none");
            }

            args.Add(filePath);
            return args.ToImmutable();
        }

        internal static string? TryCaptureJsonOutput(string stdout)
        {
            if (string.IsNullOrWhiteSpace(stdout))
            {
                return null;
            }

            string trimmed = stdout.Trim();
            int jsonStart = FindJsonStart(trimmed);
            if (jsonStart < 0)
            {
                return null;
            }

            string candidate = trimmed[jsonStart..];
            try
            {
                using JsonDocument document = JsonDocument.Parse(candidate);
                return candidate;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static QmlDomResult CreateResult(ToolResult toolResult, QmlDomOptions options)
        {
            string? output = null;
            if (toolResult.Success)
            {
                output = options.AstMode ? toolResult.Stdout : TryCaptureJsonOutput(toolResult.Stdout);
            }

            return new QmlDomResult
            {
                ToolResult = toolResult,
                JsonOutput = output,
            };
        }

        private static int FindJsonStart(string text)
        {
            int objectStart = text.IndexOf('{', StringComparison.Ordinal);
            int arrayStart = text.IndexOf('[', StringComparison.Ordinal);
            if (objectStart < 0)
            {
                return arrayStart;
            }

            if (arrayStart < 0)
            {
                return objectStart;
            }

            return Math.Min(objectStart, arrayStart);
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
