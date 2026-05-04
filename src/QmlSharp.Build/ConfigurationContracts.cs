#pragma warning disable MA0048

namespace QmlSharp.Build
{
    /// <summary>
    /// Root configuration loaded from qmlsharp.json.
    /// </summary>
    public sealed record QmlSharpConfig
    {
        /// <summary>Path to the C# entry point file.</summary>
        public string? Entry { get; init; }

        /// <summary>Output directory for the build product.</summary>
        public string OutDir { get; init; } = "./dist";

        /// <summary>Qt-specific configuration.</summary>
        public required QtConfig Qt { get; init; }

        /// <summary>Build behavior options.</summary>
        public BuildConfig Build { get; init; } = new();

        /// <summary>Native C++ build configuration.</summary>
        public NativeConfig Native { get; init; } = new();

        /// <summary>Development mode settings.</summary>
        public DevConfig Dev { get; init; } = new();

        /// <summary>QML module identity configuration.</summary>
        public required ModuleConfig Module { get; init; }

        /// <summary>Project name used in manifest and diagnostics.</summary>
        public string? Name { get; init; }

        /// <summary>Project version string.</summary>
        public string Version { get; init; } = "0.0.0";
    }

    /// <summary>Qt SDK configuration.</summary>
    public sealed record QtConfig
    {
        /// <summary>Path to the Qt installation directory.</summary>
        public string? Dir { get; init; }

        /// <summary>Qt modules required by this project.</summary>
        public ImmutableArray<string> Modules { get; init; } =
            ImmutableArray.Create("QtQuick", "QtQuick.Controls");
    }

    /// <summary>Build behavior configuration.</summary>
    public sealed record BuildConfig
    {
        /// <summary>Enable AOT QML compilation via qmlcachegen.</summary>
        public bool Aot { get; init; }

        /// <summary>Run qmllint on generated QML files.</summary>
        public bool Lint { get; init; } = true;

        /// <summary>Run qmlformat on generated QML files.</summary>
        public bool Format { get; init; } = true;

        /// <summary>Generate source maps.</summary>
        public bool SourceMaps { get; init; } = true;

        /// <summary>Enable incremental compilation.</summary>
        public bool Incremental { get; init; } = true;

        /// <summary>Build mode value from configuration.</summary>
        public string Mode { get; init; } = "development";
    }

    /// <summary>Native C++ build configuration.</summary>
    public sealed record NativeConfig
    {
        /// <summary>Use an existing native library instead of running Build Stage 7.</summary>
        public bool Prebuilt { get; init; }

        /// <summary>CMake preset name for the native build.</summary>
        public string CmakePreset { get; init; } = "default";
    }

    /// <summary>Development mode configuration.</summary>
    public sealed record DevConfig
    {
        /// <summary>Enable hot reload in dev mode.</summary>
        public bool HotReload { get; init; } = true;

        /// <summary>Directories to watch for file changes.</summary>
        public ImmutableArray<string> WatchPaths { get; init; } =
            ImmutableArray.Create("./src");

        /// <summary>Debounce interval for file change events in milliseconds.</summary>
        public int DebounceMs { get; init; } = 200;
    }

    /// <summary>QML module identity configuration.</summary>
    public sealed record ModuleConfig
    {
        /// <summary>QML module URI prefix.</summary>
        public required string Prefix { get; init; }

        /// <summary>Module version.</summary>
        public QmlVersion Version { get; init; } = new(1, 0);
    }

    /// <summary>QML module version.</summary>
    /// <param name="Major">Major version.</param>
    /// <param name="Minor">Minor version.</param>
    public sealed record QmlVersion(int Major, int Minor);

    /// <summary>Build mode enumeration.</summary>
    public enum BuildMode
    {
        /// <summary>Development build mode.</summary>
        Development,

        /// <summary>Production build mode.</summary>
        Production,
    }

    /// <summary>Loads and validates qmlsharp.json configuration.</summary>
    public interface IConfigLoader
    {
        /// <summary>Loads configuration from the given project directory.</summary>
        QmlSharpConfig Load(string projectDir);

        /// <summary>Validates context-free configuration structure.</summary>
        ImmutableArray<ConfigDiagnostic> Validate(QmlSharpConfig config);

        /// <summary>Returns the default configuration with defaults applied.</summary>
        QmlSharpConfig GetDefaults();
    }

    /// <summary>A configuration validation diagnostic.</summary>
    /// <param name="Code">Stable build-system diagnostic code.</param>
    /// <param name="Field">Configuration field path.</param>
    /// <param name="Message">Human-readable validation message.</param>
    /// <param name="Severity">Validation severity.</param>
    public sealed record ConfigDiagnostic(
        string Code,
        string Field,
        string Message,
        ConfigDiagnosticSeverity Severity);

    /// <summary>Configuration diagnostic severity.</summary>
    public enum ConfigDiagnosticSeverity
    {
        /// <summary>Warning diagnostic.</summary>
        Warning,

        /// <summary>Error diagnostic.</summary>
        Error,
    }

    /// <summary>Thrown when qmlsharp.json cannot be loaded into a valid configuration.</summary>
    public sealed class ConfigParseException : Exception
    {
        /// <summary>Create a configuration parse exception from one diagnostic.</summary>
        public ConfigParseException(BuildDiagnostic diagnostic)
            : this(ImmutableArray.Create(diagnostic))
        {
        }

        /// <summary>Create a configuration parse exception from one or more diagnostics.</summary>
        public ConfigParseException(ImmutableArray<BuildDiagnostic> diagnostics)
            : base(CreateMessage(diagnostics))
        {
            if (diagnostics.IsDefaultOrEmpty)
            {
                throw new ArgumentException("At least one diagnostic is required.", nameof(diagnostics));
            }

            Diagnostics = diagnostics;
        }

        /// <summary>Diagnostics that explain why loading failed.</summary>
        public ImmutableArray<BuildDiagnostic> Diagnostics { get; }

        /// <summary>The primary diagnostic.</summary>
        public BuildDiagnostic Diagnostic => Diagnostics[0];

        /// <summary>The primary diagnostic code.</summary>
        public string Code => Diagnostic.Code;

        /// <summary>The primary related configuration field or file path.</summary>
        public string? Field => Diagnostic.FilePath;

        private static string CreateMessage(ImmutableArray<BuildDiagnostic> diagnostics)
        {
            if (diagnostics.IsDefaultOrEmpty)
            {
                return "Configuration could not be loaded.";
            }

            return diagnostics[0].Message;
        }
    }
}

#pragma warning restore MA0048
