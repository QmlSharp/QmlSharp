#pragma warning disable MA0048

namespace QmlSharp.Qt.Tools
{
    /// <summary>Discovers Qt installations and resolves Qt SDK tool metadata.</summary>
    public interface IQtToolchain
    {
        /// <summary>Discover Qt installation metadata.</summary>
        Task<QtInstallation> DiscoverAsync(QtToolchainConfig? config = null, CancellationToken ct = default);

        /// <summary>Check availability of all wrapped Qt tools plus supporting tools.</summary>
        Task<ToolAvailability> CheckToolsAsync(CancellationToken ct = default);

        /// <summary>Resolve metadata for a specific Qt tool name.</summary>
        Task<ToolInfo> GetToolInfoAsync(string toolName, CancellationToken ct = default);

        /// <summary>Gets the discovered installation, if discovery has succeeded.</summary>
        QtInstallation? Installation { get; }
    }

    /// <summary>Runs external tools with captured output and process metadata.</summary>
    public interface IToolRunner
    {
        /// <summary>Run an external tool and capture stdout, stderr, exit code, command, and duration.</summary>
        Task<ToolResult> RunAsync(
            string toolPath,
            ImmutableArray<string> args,
            ToolRunnerOptions? options = null,
            CancellationToken ct = default);
    }

    /// <summary>Wraps qmlformat.</summary>
    public interface IQmlFormat
    {
        /// <summary>Format a QML file.</summary>
        Task<QmlFormatResult> FormatFileAsync(
            string filePath,
            QmlFormatOptions? options = null,
            CancellationToken ct = default);

        /// <summary>Format QML source supplied as a string.</summary>
        Task<QmlFormatResult> FormatStringAsync(
            string qmlSource,
            QmlFormatOptions? options = null,
            CancellationToken ct = default);

        /// <summary>Format multiple QML files.</summary>
        Task<ImmutableArray<QmlFormatResult>> FormatBatchAsync(
            ImmutableArray<string> filePaths,
            QmlFormatOptions? options = null,
            CancellationToken ct = default);
    }

    /// <summary>Wraps qmllint.</summary>
    public interface IQmlLint
    {
        /// <summary>Lint a QML file.</summary>
        Task<QmlLintResult> LintFileAsync(
            string filePath,
            QmlLintOptions? options = null,
            CancellationToken ct = default);

        /// <summary>Lint QML source supplied as a string.</summary>
        Task<QmlLintResult> LintStringAsync(
            string qmlSource,
            QmlLintOptions? options = null,
            CancellationToken ct = default);

        /// <summary>Lint multiple QML files.</summary>
        Task<ImmutableArray<QmlLintResult>> LintBatchAsync(
            ImmutableArray<string> filePaths,
            QmlLintOptions? options = null,
            CancellationToken ct = default);

        /// <summary>Lint a QML module directory.</summary>
        Task<QmlLintResult> LintModuleAsync(
            string modulePath,
            QmlLintOptions? options = null,
            CancellationToken ct = default);

        /// <summary>List available qmllint plugins.</summary>
        Task<ImmutableArray<string>> ListPluginsAsync(CancellationToken ct = default);
    }

    /// <summary>Wraps qmlcachegen.</summary>
    public interface IQmlCachegen
    {
        /// <summary>Compile a QML file to bytecode or generated code.</summary>
        Task<QmlCachegenResult> CompileFileAsync(
            string filePath,
            QmlCachegenOptions? options = null,
            CancellationToken ct = default);

        /// <summary>Compile QML source supplied as a string.</summary>
        Task<QmlCachegenResult> CompileStringAsync(
            string qmlSource,
            QmlCachegenOptions? options = null,
            CancellationToken ct = default);

        /// <summary>Compile multiple QML files.</summary>
        Task<QmlCachegenBatchResult> CompileBatchAsync(
            ImmutableArray<string> filePaths,
            QmlCachegenOptions? options = null,
            CancellationToken ct = default);
    }

    /// <summary>Wraps qmltc.</summary>
    public interface IQmltc
    {
        /// <summary>Compile a QML file to C++ source.</summary>
        Task<QmltcResult> CompileFileAsync(
            string filePath,
            QmltcOptions? options = null,
            CancellationToken ct = default);
    }

    /// <summary>Wraps qmlimportscanner.</summary>
    public interface IQmlImportScanner
    {
        /// <summary>Scan a directory for QML import dependencies.</summary>
        Task<QmlImportScanResult> ScanDirectoryAsync(
            string directoryPath,
            QmlImportScanOptions? options = null,
            CancellationToken ct = default);

        /// <summary>Scan specific QML files for import dependencies.</summary>
        Task<QmlImportScanResult> ScanFilesAsync(
            ImmutableArray<string> filePaths,
            QmlImportScanOptions? options = null,
            CancellationToken ct = default);

        /// <summary>Scan QML source supplied as a string.</summary>
        Task<QmlImportScanResult> ScanStringAsync(
            string qmlSource,
            QmlImportScanOptions? options = null,
            CancellationToken ct = default);
    }

    /// <summary>Wraps qmldom.</summary>
    public interface IQmlDom
    {
        /// <summary>Dump DOM or AST JSON for a QML file.</summary>
        Task<QmlDomResult> DumpFileAsync(
            string filePath,
            QmlDomOptions? options = null,
            CancellationToken ct = default);

        /// <summary>Dump DOM or AST JSON for QML source supplied as a string.</summary>
        Task<QmlDomResult> DumpStringAsync(
            string qmlSource,
            QmlDomOptions? options = null,
            CancellationToken ct = default);
    }

    /// <summary>Wraps qml runtime smoke execution.</summary>
    public interface IQmlRunner
    {
        /// <summary>Run a QML file for a smoke test.</summary>
        Task<QmlRunResult> RunFileAsync(
            string filePath,
            QmlRunOptions? options = null,
            CancellationToken ct = default);

        /// <summary>Run QML source supplied as a string for a smoke test.</summary>
        Task<QmlRunResult> RunStringAsync(
            string qmlSource,
            QmlRunOptions? options = null,
            CancellationToken ct = default);

        /// <summary>List available QML runtime configurations.</summary>
        Task<ImmutableArray<string>> ListConfigurationsAsync(CancellationToken ct = default);
    }

    /// <summary>Wraps rcc resource compilation.</summary>
    public interface IRcc
    {
        /// <summary>Compile a QRC file.</summary>
        Task<RccResult> CompileAsync(
            string qrcFilePath,
            RccOptions? options = null,
            CancellationToken ct = default);

        /// <summary>List entries in a QRC file.</summary>
        Task<ImmutableArray<string>> ListEntriesAsync(
            string qrcFilePath,
            CancellationToken ct = default);

        /// <summary>List mappings in a QRC file.</summary>
        Task<ImmutableArray<RccMapping>> ListMappingsAsync(
            string qrcFilePath,
            CancellationToken ct = default);

        /// <summary>Create QRC XML from resource entries.</summary>
        Task<string> CreateQrcXmlAsync(
            ImmutableArray<RccFileEntry> files,
            string? prefix = null,
            CancellationToken ct = default);
    }

    /// <summary>Wraps qmltyperegistrar.</summary>
    public interface IQmlTypeRegistrar
    {
        /// <summary>Generate type registration source from metatypes JSON.</summary>
        Task<TypeRegistrarResult> RegisterAsync(
            string metatypesJsonPath,
            TypeRegistrarOptions? options = null,
            CancellationToken ct = default);
    }

    /// <summary>Runs cumulative Qt quality gates over QML inputs.</summary>
    public interface IQualityGate
    {
        /// <summary>Run the requested quality gate over one file.</summary>
        Task<QualityGateResult> RunAsync(
            string filePath,
            QualityGateLevel level,
            QualityGateOptions? options = null,
            CancellationToken ct = default);

        /// <summary>Run the requested quality gate over QML source supplied as a string.</summary>
        Task<QualityGateResult> RunStringAsync(
            string qmlSource,
            QualityGateLevel level,
            QualityGateOptions? options = null,
            CancellationToken ct = default);

        /// <summary>Run the requested quality gate over multiple files.</summary>
        Task<QualityGateResult> RunBatchAsync(
            ImmutableArray<string> filePaths,
            QualityGateLevel level,
            QualityGateOptions? options = null,
            CancellationToken ct = default);
    }

    /// <summary>Parses diagnostics emitted by Qt tools.</summary>
    public interface IQtDiagnosticParser
    {
        /// <summary>Parse qmllint JSON output.</summary>
        ImmutableArray<QtDiagnostic> ParseJson(string jsonOutput);

        /// <summary>Parse stderr diagnostics.</summary>
        ImmutableArray<QtDiagnostic> ParseStderr(string stderrText);

        /// <summary>Parse stderr diagnostics with an optional filename override.</summary>
        ImmutableArray<QtDiagnostic> ParseStderr(string stderrText, string? filenameOverride);
    }
}

#pragma warning restore MA0048
