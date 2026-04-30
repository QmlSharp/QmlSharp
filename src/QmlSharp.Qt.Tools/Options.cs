#pragma warning disable MA0048

namespace QmlSharp.Qt.Tools
{
    /// <summary>Configuration for Qt SDK discovery.</summary>
    public sealed record QtToolchainConfig
    {
        /// <summary>Explicit Qt installation directory.</summary>
        public string? QtDir { get; init; }

        /// <summary>Additional QML import paths.</summary>
        public ImmutableArray<string> ImportPaths { get; init; } = [];

        /// <summary>Default timeout for tool invocations.</summary>
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>Working directory for tool invocations.</summary>
        public string? Cwd { get; init; }

        /// <summary>Additional environment variables for tool processes.</summary>
        public ImmutableDictionary<string, string>? Env { get; init; }
    }

    /// <summary>Options for qmlformat invocation.</summary>
    public sealed record QmlFormatOptions
    {
        /// <summary>Indentation width in spaces.</summary>
        public int IndentWidth { get; init; } = 4;

        /// <summary>Use tabs instead of spaces.</summary>
        public bool UseTabs { get; init; } = false;

        /// <summary>Normalize object member order.</summary>
        public bool Normalize { get; init; } = false;

        /// <summary>Sort import statements alphabetically.</summary>
        public bool SortImports { get; init; } = false;

        /// <summary>Semicolon handling: add, remove, or preserve.</summary>
        public string? SemicolonRule { get; init; }

        /// <summary>Line ending style.</summary>
        public string? Newline { get; init; }

        /// <summary>Maximum column width for line wrapping; 0 means no wrapping.</summary>
        public int ColumnWidth { get; init; } = 0;

        /// <summary>Ignore .qmlformat.ini settings files.</summary>
        public bool IgnoreSettings { get; init; } = false;

        /// <summary>Force formatting even if recoverable syntax issues exist.</summary>
        public bool Force { get; init; } = false;

        /// <summary>Write formatted output back to the original file.</summary>
        public bool InPlace { get; init; } = false;
    }

    /// <summary>Options for qmllint invocation.</summary>
    public sealed record QmlLintOptions
    {
        /// <summary>Enable JSON diagnostic output.</summary>
        public bool JsonOutput { get; init; } = true;

        /// <summary>Per-category warning level overrides.</summary>
        public ImmutableDictionary<QmlLintCategory, DiagnosticSeverity>? WarningLevels { get; init; }

        /// <summary>Maximum number of warnings before stopping; 0 means unlimited.</summary>
        public int MaxWarnings { get; init; } = 0;

        /// <summary>Suppress output and rely on the exit code.</summary>
        public bool Silent { get; init; } = false;

        /// <summary>Additional QML import paths.</summary>
        public ImmutableArray<string> ImportPaths { get; init; } = [];

        /// <summary>Lint without implicit imports.</summary>
        public bool Bare { get; init; } = false;

        /// <summary>Auto-fix warnings where supported.</summary>
        public bool Fix { get; init; } = false;
    }

    /// <summary>Options for qmlcachegen invocation.</summary>
    public sealed record QmlCachegenOptions
    {
        /// <summary>Generate bytecode only.</summary>
        public bool BytecodeOnly { get; init; } = false;

        /// <summary>Treat warnings as errors.</summary>
        public bool WarningsAsErrors { get; init; } = false;

        /// <summary>Enable verbose output.</summary>
        public bool Verbose { get; init; } = false;

        /// <summary>Additional QML import paths.</summary>
        public ImmutableArray<string> ImportPaths { get; init; } = [];

        /// <summary>Output directory for generated files.</summary>
        public string? OutputDir { get; init; }
    }

    /// <summary>Options for qmltc invocation.</summary>
    public sealed record QmltcOptions
    {
        /// <summary>C++ namespace for generated types.</summary>
        public string? Namespace { get; init; }

        /// <summary>QML module URI.</summary>
        public string? Module { get; init; }

        /// <summary>Export macro for generated class declarations.</summary>
        public string? ExportMacro { get; init; }

        /// <summary>Output header file path.</summary>
        public string? OutputHeader { get; init; }

        /// <summary>Output source file path.</summary>
        public string? OutputSource { get; init; }
    }

    /// <summary>Options for qmlimportscanner invocation.</summary>
    public sealed record QmlImportScanOptions
    {
        /// <summary>Root directory path for import resolution.</summary>
        public string? RootPath { get; init; }

        /// <summary>Additional QML import paths.</summary>
        public ImmutableArray<string> ImportPaths { get; init; } = [];

        /// <summary>Directories to exclude from scanning.</summary>
        public ImmutableArray<string> ExcludeDirs { get; init; } = [];
    }

    /// <summary>Options for qmldom invocation.</summary>
    public sealed record QmlDomOptions
    {
        /// <summary>Dump AST when true; dump DOM by default.</summary>
        public bool AstMode { get; init; } = false;

        /// <summary>Filter output to specific fields.</summary>
        public ImmutableArray<string> FilterFields { get; init; } = [];

        /// <summary>Exclude dependency information from output.</summary>
        public bool NoDependencies { get; init; } = false;
    }

    /// <summary>Options for qml runner invocation.</summary>
    public sealed record QmlRunOptions
    {
        /// <summary>Platform override.</summary>
        public string Platform { get; init; } = "offscreen";

        /// <summary>QML application type.</summary>
        public QmlAppType AppType { get; init; } = QmlAppType.Auto;

        /// <summary>Additional QML import paths.</summary>
        public ImmutableArray<string> ImportPaths { get; init; } = [];

        /// <summary>Timeout for the running process.</summary>
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

        /// <summary>Stable running period before auto-kill.</summary>
        public TimeSpan StableRunPeriod { get; init; } = TimeSpan.FromSeconds(2);
    }

    /// <summary>Options for rcc invocation.</summary>
    public sealed record RccOptions
    {
        /// <summary>Output binary resource data instead of C++ source.</summary>
        public bool BinaryMode { get; init; } = false;

        /// <summary>Disable resource compression.</summary>
        public bool NoCompress { get; init; } = false;

        /// <summary>Generate Python output instead of C++.</summary>
        public bool PythonOutput { get; init; } = false;

        /// <summary>Output file path.</summary>
        public string? OutputFile { get; init; }
    }

    /// <summary>Options for qmltyperegistrar invocation.</summary>
    public sealed record TypeRegistrarOptions
    {
        /// <summary>Path to foreign types JSON.</summary>
        public string? ForeignTypesFile { get; init; }

        /// <summary>Module import URI.</summary>
        public string? ModuleImportUri { get; init; }

        /// <summary>Module major version.</summary>
        public int? MajorVersion { get; init; }

        /// <summary>C++ namespace for generated registration source.</summary>
        public string? Namespace { get; init; }

        /// <summary>Output file path.</summary>
        public string? OutputFile { get; init; }
    }

    /// <summary>Options for tool runner process execution.</summary>
    public sealed record ToolRunnerOptions
    {
        /// <summary>Timeout for the process.</summary>
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>Working directory for the process.</summary>
        public string? Cwd { get; init; }

        /// <summary>String to write to the process stdin.</summary>
        public string? Stdin { get; init; }

        /// <summary>Additional environment variables.</summary>
        public ImmutableDictionary<string, string>? Env { get; init; }
    }

    /// <summary>Options for quality gate execution.</summary>
    public sealed record QualityGateOptions
    {
        /// <summary>Progress callback invoked as each level completes.</summary>
        public Action<QualityGateProgress>? OnProgress { get; init; }

        /// <summary>Additional QML import paths for all tools.</summary>
        public ImmutableArray<string> ImportPaths { get; init; } = [];

        /// <summary>Stop on first failure.</summary>
        public bool EarlyStop { get; init; } = true;
    }
}

#pragma warning restore MA0048
