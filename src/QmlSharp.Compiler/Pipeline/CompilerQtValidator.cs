using QmlSharp.Qt.Tools;
using CompilerSeverity = QmlSharp.Compiler.DiagnosticSeverity;
using QtSeverity = QmlSharp.Qt.Tools.DiagnosticSeverity;

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Runs optional Qt-owned validation for generated QML.
    /// </summary>
    internal sealed class CompilerQtValidator
    {
        private const string PhaseName = "QtValidation";
        private const string StringInputFileName = "<string>";

        private readonly IQmlFormat qmlFormat;
        private readonly IQmlLint qmlLint;

        public CompilerQtValidator(IQmlFormat qmlFormat, IQmlLint qmlLint)
        {
            this.qmlFormat = qmlFormat ?? throw new ArgumentNullException(nameof(qmlFormat));
            this.qmlLint = qmlLint ?? throw new ArgumentNullException(nameof(qmlLint));
        }

        public static CompilerQtValidator CreateDefault()
        {
            QtToolchain toolchain = new();
            ToolRunner runner = new();
            QtDiagnosticParser parser = new();
            return new CompilerQtValidator(
                new QmlFormat(toolchain, runner, parser),
                new QmlLint(toolchain, runner, parser));
        }

        public CompilationUnit Validate(CompilationUnit unit, CompilerOptions options)
        {
            ArgumentNullException.ThrowIfNull(unit);
            ArgumentNullException.ThrowIfNull(options);

            if ((!options.FormatQml && !options.LintQml) || !unit.Success || string.IsNullOrEmpty(unit.QmlText))
            {
                return unit;
            }

            string qmlText = unit.QmlText;
            string qmlFilePath = CreateGeneratedQmlPath(unit, options);
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<CompilerDiagnostic>();
            diagnostics.AddRange(unit.Diagnostics.IsDefault ? ImmutableArray<CompilerDiagnostic>.Empty : unit.Diagnostics);
            bool qmlTextChanged = false;

            if (options.FormatQml)
            {
                string originalQmlText = qmlText;
                qmlText = RunFormat(qmlText, qmlFilePath, diagnostics);
                qmlTextChanged = !StringComparer.Ordinal.Equals(originalQmlText, qmlText);
            }

            if (options.LintQml)
            {
                RunLint(qmlText, qmlFilePath, diagnostics);
            }

            return unit with
            {
                QmlText = qmlText,
                SourceMap = qmlTextChanged ? null : unit.SourceMap,
                Diagnostics = SortDiagnostics(diagnostics.ToImmutable()),
                Stats = unit.Stats with
                {
                    QmlBytes = System.Text.Encoding.UTF8.GetByteCount(qmlText),
                },
            };
        }

        private string RunFormat(
            string qmlText,
            string qmlFilePath,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            try
            {
                QmlFormatResult result = qmlFormat
                    .FormatStringAsync(
                        qmlText,
                        new QmlFormatOptions
                        {
                            Newline = "unix",
                        })
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                diagnostics.AddRange(ConvertDiagnostics(result.Diagnostics, qmlFilePath, "qmlformat"));
                if (!result.Success && result.Diagnostics.IsDefaultOrEmpty)
                {
                    diagnostics.Add(CreateToolFailureDiagnostic("qmlformat", qmlFilePath, result.ToolResult));
                }

                return result.Success && result.FormattedSource is not null
                    ? NormalizeText(result.FormattedSource)
                    : qmlText;
            }
            catch (Exception exception) when (IsQtValidationException(exception))
            {
                diagnostics.Add(CreateExceptionDiagnostic("qmlformat", qmlFilePath, exception));
                return qmlText;
            }
        }

        private void RunLint(
            string qmlText,
            string qmlFilePath,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            try
            {
                QmlLintResult result = qmlLint
                    .LintStringAsync(qmlText)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                diagnostics.AddRange(ConvertDiagnostics(result.Diagnostics, qmlFilePath, "qmllint"));
                if (!result.Success && result.Diagnostics.IsDefaultOrEmpty)
                {
                    diagnostics.Add(CreateToolFailureDiagnostic("qmllint", qmlFilePath, result.ToolResult));
                }
            }
            catch (Exception exception) when (IsQtValidationException(exception))
            {
                diagnostics.Add(CreateExceptionDiagnostic("qmllint", qmlFilePath, exception));
            }
        }

        private static ImmutableArray<CompilerDiagnostic> ConvertDiagnostics(
            ImmutableArray<QtDiagnostic> diagnostics,
            string qmlFilePath,
            string toolName)
        {
            if (diagnostics.IsDefaultOrEmpty)
            {
                return [];
            }

            return diagnostics
                .Select(diagnostic => new CompilerDiagnostic(
                    DiagnosticCodes.QtValidationFailed,
                    MapSeverity(diagnostic.Severity),
                    DiagnosticMessageCatalog.FormatMessage(
                        DiagnosticCodes.QtValidationFailed,
                        CreateQtDiagnosticDetails(toolName, diagnostic)),
                    CreateLocation(diagnostic, qmlFilePath),
                    PhaseName))
                .ToImmutableArray();
        }

        private static string CreateQtDiagnosticDetails(string toolName, QtDiagnostic diagnostic)
        {
            string category = string.IsNullOrWhiteSpace(diagnostic.Category)
                ? string.Empty
                : $" [{diagnostic.Category}]";
            return $"{toolName}{category}: {diagnostic.Message}";
        }

        private static SourceLocation CreateLocation(QtDiagnostic diagnostic, string qmlFilePath)
        {
            string filePath = string.IsNullOrWhiteSpace(diagnostic.File) || string.Equals(diagnostic.File, StringInputFileName, StringComparison.Ordinal)
                ? qmlFilePath
                : diagnostic.File;

            if (diagnostic.Line is not null && diagnostic.Column is not null)
            {
                return new SourceLocation(filePath, diagnostic.Line.Value, diagnostic.Column.Value);
            }

            if (diagnostic.Line is not null)
            {
                return SourceLocation.Partial(filePath, diagnostic.Line, column: null);
            }

            return SourceLocation.FileOnly(filePath);
        }

        private static CompilerDiagnostic CreateToolFailureDiagnostic(string toolName, string qmlFilePath, ToolResult toolResult)
        {
            string message = FirstNonEmptyLine(toolResult.Stderr)
                ?? FirstNonEmptyLine(toolResult.Stdout)
                ?? $"{toolName} failed with exit code {toolResult.ExitCode}.";
            return new CompilerDiagnostic(
                DiagnosticCodes.QtValidationFailed,
                CompilerSeverity.Error,
                DiagnosticMessageCatalog.FormatMessage(DiagnosticCodes.QtValidationFailed, $"{toolName}: {message}"),
                SourceLocation.FileOnly(qmlFilePath),
                PhaseName);
        }

        private static CompilerDiagnostic CreateExceptionDiagnostic(string toolName, string qmlFilePath, Exception exception)
        {
            return new CompilerDiagnostic(
                DiagnosticCodes.QtValidationFailed,
                CompilerSeverity.Error,
                DiagnosticMessageCatalog.FormatMessage(
                    DiagnosticCodes.QtValidationFailed,
                    $"{toolName}: {exception.GetType().Name}: {exception.Message}"),
                SourceLocation.FileOnly(qmlFilePath),
                PhaseName);
        }

        private static CompilerSeverity MapSeverity(QtSeverity severity)
        {
            return severity switch
            {
                QtSeverity.Warning => CompilerSeverity.Warning,
                QtSeverity.Error => CompilerSeverity.Error,
                QtSeverity.Info or QtSeverity.Hint or QtSeverity.Disabled => CompilerSeverity.Info,
                _ => CompilerSeverity.Error,
            };
        }

        private static bool IsQtValidationException(Exception exception)
        {
            return exception is QtInstallationNotFoundError
                or QtToolNotFoundError
                or QtToolTimeoutError
                or IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or ArgumentException
                or NotSupportedException;
        }

        private static ImmutableArray<CompilerDiagnostic> SortDiagnostics(ImmutableArray<CompilerDiagnostic> diagnostics)
        {
            return diagnostics
                .OrderBy(static diagnostic => diagnostic.Location?.FilePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Location?.Line ?? 0)
                .ThenBy(static diagnostic => diagnostic.Location?.Column ?? 0)
                .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static string CreateGeneratedQmlPath(CompilationUnit unit, CompilerOptions options)
        {
            string moduleDir = Path.Join(options.OutputDir, "qml");
            foreach (string segment in options.ModuleUriPrefix.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                moduleDir = Path.Join(moduleDir, segment);
            }

            return Path.Join(moduleDir, $"{unit.ViewClassName}.qml");
        }

        private static string? FirstNonEmptyLine(string text)
        {
            return text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(static line => line.Trim())
                .FirstOrDefault(static line => line.Length > 0);
        }

        private static string NormalizeText(string text)
        {
            string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            return normalized.EndsWith("\n", StringComparison.Ordinal) ? normalized : normalized + "\n";
        }
    }
}
