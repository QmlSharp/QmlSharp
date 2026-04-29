#pragma warning disable MA0048

namespace QmlSharp.Qt.Tools
{
    /// <summary>Discovered Qt installation metadata.</summary>
    public sealed record QtInstallation
    {
        /// <summary>Qt installation root directory.</summary>
        public required string RootDir { get; init; }

        /// <summary>Qt bin directory.</summary>
        public required string BinDir { get; init; }

        /// <summary>Qt QML import directory.</summary>
        public required string QmlDir { get; init; }

        /// <summary>Qt library directory.</summary>
        public required string LibDir { get; init; }

        /// <summary>Qt version.</summary>
        public required QtVersion Version { get; init; }

        /// <summary>Detected platform name.</summary>
        public required string Platform { get; init; }

        /// <summary>QML import paths discovered for this installation.</summary>
        public ImmutableArray<string> ImportPaths { get; init; } = [];
    }

    /// <summary>Qt semantic version.</summary>
    public sealed record QtVersion
    {
        /// <summary>Major version.</summary>
        public required int Major { get; init; }

        /// <summary>Minor version.</summary>
        public required int Minor { get; init; }

        /// <summary>Patch version.</summary>
        public required int Patch { get; init; }

        /// <summary>String form of the version.</summary>
        public string String => $"{Major}.{Minor}.{Patch}";

        /// <inheritdoc />
        public override string ToString() => String;
    }

    /// <summary>Qt tool path and availability metadata.</summary>
    public sealed record ToolInfo
    {
        /// <summary>Tool name.</summary>
        public required string Name { get; init; }

        /// <summary>Tool path.</summary>
        public required string Path { get; init; }

        /// <summary>Whether the tool is available.</summary>
        public required bool Available { get; init; }

        /// <summary>Tool version, when known.</summary>
        public string? Version { get; init; }
    }

    /// <summary>Availability for all wrapped and supporting Qt tools.</summary>
    public sealed record ToolAvailability
    {
        /// <summary>qmlformat availability.</summary>
        public required ToolInfo QmlFormat { get; init; }

        /// <summary>qmllint availability.</summary>
        public required ToolInfo QmlLint { get; init; }

        /// <summary>qmlcachegen availability.</summary>
        public required ToolInfo QmlCachegen { get; init; }

        /// <summary>qmltc availability.</summary>
        public required ToolInfo Qmltc { get; init; }

        /// <summary>qmlimportscanner availability.</summary>
        public required ToolInfo QmlImportScanner { get; init; }

        /// <summary>qmldom availability.</summary>
        public required ToolInfo QmlDom { get; init; }

        /// <summary>qml runner availability.</summary>
        public required ToolInfo Qml { get; init; }

        /// <summary>rcc availability.</summary>
        public required ToolInfo Rcc { get; init; }

        /// <summary>qmltyperegistrar availability.</summary>
        public required ToolInfo QmlTypeRegistrar { get; init; }

        /// <summary>moc availability.</summary>
        public required ToolInfo Moc { get; init; }

        /// <summary>qmlaotstats availability.</summary>
        public required ToolInfo QmlAotStats { get; init; }
    }

    /// <summary>Captured result of a process invocation.</summary>
    public sealed record ToolResult
    {
        /// <summary>Process exit code.</summary>
        public required int ExitCode { get; init; }

        /// <summary>Captured stdout.</summary>
        public required string Stdout { get; init; }

        /// <summary>Captured stderr.</summary>
        public required string Stderr { get; init; }

        /// <summary>Process duration in milliseconds.</summary>
        public required long DurationMs { get; init; }

        /// <summary>Diagnostic command string.</summary>
        public required string Command { get; init; }

        /// <summary>True when the process exited with code 0.</summary>
        public bool Success => ExitCode == 0;
    }

    /// <summary>Result of qmlformat.</summary>
    public sealed record QmlFormatResult
    {
        /// <summary>Raw tool result.</summary>
        public required ToolResult ToolResult { get; init; }

        /// <summary>Formatted source, when returned by the tool.</summary>
        public string? FormattedSource { get; init; }

        /// <summary>Whether formatting changed the input.</summary>
        public required bool HasChanges { get; init; }

        /// <summary>Formatting diagnostics.</summary>
        public ImmutableArray<QtDiagnostic> Diagnostics { get; init; } = [];

        /// <summary>True when the underlying tool succeeded.</summary>
        public bool Success => ToolResult.Success;
    }

    /// <summary>Result of qmllint.</summary>
    public sealed record QmlLintResult
    {
        /// <summary>Raw tool result.</summary>
        public required ToolResult ToolResult { get; init; }

        /// <summary>Lint diagnostics.</summary>
        public ImmutableArray<QtDiagnostic> Diagnostics { get; init; } = [];

        /// <summary>Error diagnostic count.</summary>
        public required int ErrorCount { get; init; }

        /// <summary>Warning diagnostic count.</summary>
        public required int WarningCount { get; init; }

        /// <summary>Info diagnostic count.</summary>
        public required int InfoCount { get; init; }

        /// <summary>Human-readable summary.</summary>
        public string? Summary { get; init; }

        /// <summary>True when qmllint completed and reported no errors or warnings.</summary>
        public bool Success => ToolResult.Success && ErrorCount == 0 && WarningCount == 0;
    }

    /// <summary>Result of qmlcachegen.</summary>
    public sealed record QmlCachegenResult
    {
        /// <summary>Raw tool result.</summary>
        public required ToolResult ToolResult { get; init; }

        /// <summary>Generated output files.</summary>
        public ImmutableArray<string> OutputFiles { get; init; } = [];

        /// <summary>Compilation diagnostics.</summary>
        public ImmutableArray<QtDiagnostic> Diagnostics { get; init; } = [];

        /// <summary>AOT compilation statistics.</summary>
        public QmlAotStats? AotStats { get; init; }

        /// <summary>True when the underlying tool succeeded.</summary>
        public bool Success => ToolResult.Success;
    }

    /// <summary>Batch qmlcachegen result.</summary>
    public sealed record QmlCachegenBatchResult
    {
        /// <summary>Per-file results.</summary>
        public required ImmutableArray<QmlCachegenResult> Results { get; init; }

        /// <summary>Successful result count.</summary>
        public required int SuccessCount { get; init; }

        /// <summary>Failed result count.</summary>
        public required int FailureCount { get; init; }

        /// <summary>Total duration in milliseconds.</summary>
        public required long TotalDurationMs { get; init; }

        /// <summary>Aggregate AOT statistics.</summary>
        public QmlAotStats? AggregateStats { get; init; }
    }

    /// <summary>AOT compilation statistics.</summary>
    public sealed record QmlAotStats
    {
        /// <summary>Total function count.</summary>
        public required int TotalFunctions { get; init; }

        /// <summary>Successfully compiled function count.</summary>
        public required int CompiledFunctions { get; init; }

        /// <summary>Failed function count.</summary>
        public required int FailedFunctions { get; init; }

        /// <summary>Compilation success rate as a percentage.</summary>
        public double SuccessRate => TotalFunctions > 0
            ? (double)CompiledFunctions / TotalFunctions * 100.0
            : 0.0;
    }

    /// <summary>Result of qmltc.</summary>
    public sealed record QmltcResult
    {
        /// <summary>Raw tool result.</summary>
        public required ToolResult ToolResult { get; init; }

        /// <summary>Generated header path.</summary>
        public string? GeneratedHeader { get; init; }

        /// <summary>Generated source path.</summary>
        public string? GeneratedSource { get; init; }

        /// <summary>Compilation diagnostics.</summary>
        public ImmutableArray<QtDiagnostic> Diagnostics { get; init; } = [];

        /// <summary>True when the underlying tool succeeded.</summary>
        public bool Success => ToolResult.Success;
    }

    /// <summary>Result of qmlimportscanner.</summary>
    public sealed record QmlImportScanResult
    {
        /// <summary>Raw tool result.</summary>
        public required ToolResult ToolResult { get; init; }

        /// <summary>Discovered imports.</summary>
        public ImmutableArray<QmlImportEntry> Imports { get; init; } = [];

        /// <summary>True when the underlying tool succeeded.</summary>
        public bool Success => ToolResult.Success;
    }

    /// <summary>Single QML import entry.</summary>
    public sealed record QmlImportEntry
    {
        /// <summary>Import name.</summary>
        public required string Name { get; init; }

        /// <summary>Import type.</summary>
        public required string Type { get; init; }

        /// <summary>Resolved path, when known.</summary>
        public string? Path { get; init; }

        /// <summary>Import version, when known.</summary>
        public string? Version { get; init; }
    }

    /// <summary>Result of qmldom.</summary>
    public sealed record QmlDomResult
    {
        /// <summary>Raw tool result.</summary>
        public required ToolResult ToolResult { get; init; }

        /// <summary>DOM or AST JSON output.</summary>
        public string? JsonOutput { get; init; }

        /// <summary>True when the underlying tool succeeded.</summary>
        public bool Success => ToolResult.Success;
    }

    /// <summary>Result of qml runtime smoke execution.</summary>
    public sealed record QmlRunResult
    {
        /// <summary>Raw tool result.</summary>
        public required ToolResult ToolResult { get; init; }

        /// <summary>Whether the smoke run passed.</summary>
        public required bool Passed { get; init; }

        /// <summary>Runtime errors parsed from stderr.</summary>
        public ImmutableArray<QtDiagnostic> RuntimeErrors { get; init; } = [];

        /// <summary>Whether the runner auto-killed the process after the stable period.</summary>
        public required bool AutoKilled { get; init; }
    }

    /// <summary>Result of rcc.</summary>
    public sealed record RccResult
    {
        /// <summary>Raw tool result.</summary>
        public required ToolResult ToolResult { get; init; }

        /// <summary>Generated output file.</summary>
        public string? OutputFile { get; init; }

        /// <summary>Resource diagnostics.</summary>
        public ImmutableArray<QtDiagnostic> Diagnostics { get; init; } = [];

        /// <summary>True when the underlying tool succeeded.</summary>
        public bool Success => ToolResult.Success;
    }

    /// <summary>Resource path mapping entry.</summary>
    public sealed record RccMapping
    {
        /// <summary>Resource alias path.</summary>
        public required string ResourcePath { get; init; }

        /// <summary>Filesystem path.</summary>
        public required string FilePath { get; init; }
    }

    /// <summary>Entry for QRC file generation.</summary>
    public sealed record RccFileEntry
    {
        /// <summary>Filesystem path to the resource file.</summary>
        public required string FilePath { get; init; }

        /// <summary>Optional resource alias.</summary>
        public string? Alias { get; init; }
    }

    /// <summary>Result of qmltyperegistrar.</summary>
    public sealed record TypeRegistrarResult
    {
        /// <summary>Raw tool result.</summary>
        public required ToolResult ToolResult { get; init; }

        /// <summary>Generated output file.</summary>
        public string? OutputFile { get; init; }

        /// <summary>True when the underlying tool succeeded.</summary>
        public bool Success => ToolResult.Success;
    }

    /// <summary>Result of a quality gate run.</summary>
    public sealed record QualityGateResult
    {
        /// <summary>Overall pass or fail status.</summary>
        public required bool Passed { get; init; }

        /// <summary>Highest level that completed successfully.</summary>
        public required QualityGateLevel CompletedLevel { get; init; }

        /// <summary>Level that caused failure.</summary>
        public QualityGateLevel? FailedAtLevel { get; init; }

        /// <summary>All diagnostics from all levels.</summary>
        public ImmutableArray<QtDiagnostic> Diagnostics { get; init; } = [];

        /// <summary>Per-level timing information.</summary>
        public ImmutableDictionary<QualityGateLevel, long> LevelDurationMs { get; init; }
            = ImmutableDictionary<QualityGateLevel, long>.Empty;

        /// <summary>Total duration in milliseconds.</summary>
        public required long TotalDurationMs { get; init; }
    }

    /// <summary>Progress report during quality gate execution.</summary>
    public sealed record QualityGateProgress
    {
        /// <summary>Level that just completed.</summary>
        public required QualityGateLevel Level { get; init; }

        /// <summary>Whether the level passed.</summary>
        public required bool Passed { get; init; }

        /// <summary>Duration of the level in milliseconds.</summary>
        public required long DurationMs { get; init; }

        /// <summary>Diagnostic count for this level.</summary>
        public required int DiagnosticCount { get; init; }
    }
}

#pragma warning restore MA0048
