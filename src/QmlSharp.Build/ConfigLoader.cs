using System.Globalization;
using System.Text.Json;
using QmlSharp.Qt.Tools;

namespace QmlSharp.Build
{
    /// <summary>Filesystem-backed implementation of <see cref="IConfigLoader"/>.</summary>
    public sealed class ConfigLoader : IConfigLoader
    {
        private const string ConfigFileName = "qmlsharp.json";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private readonly IQtToolchain _qtToolchain;
        private readonly Func<string, bool> _fileExists;
        private readonly Func<string, bool> _directoryExists;
        private readonly Func<string, string> _readAllText;

        /// <summary>Create a config loader using the process filesystem and Qt discovery service.</summary>
        public ConfigLoader()
            : this(new QtToolchain())
        {
        }

        /// <summary>Create a config loader with an explicit Qt discovery service.</summary>
        public ConfigLoader(IQtToolchain qtToolchain)
            : this(qtToolchain, File.Exists, Directory.Exists, File.ReadAllText)
        {
        }

        internal ConfigLoader(
            IQtToolchain qtToolchain,
            Func<string, bool> fileExists,
            Func<string, bool> directoryExists,
            Func<string, string> readAllText)
        {
            ArgumentNullException.ThrowIfNull(qtToolchain);
            ArgumentNullException.ThrowIfNull(fileExists);
            ArgumentNullException.ThrowIfNull(directoryExists);
            ArgumentNullException.ThrowIfNull(readAllText);

            _qtToolchain = qtToolchain;
            _fileExists = fileExists;
            _directoryExists = directoryExists;
            _readAllText = readAllText;
        }

        /// <inheritdoc />
        public QmlSharpConfig Load(string projectDir)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectDir);

            string normalizedProjectDir = NormalizeRequiredPath(projectDir);
            string configPath = Path.Join(normalizedProjectDir, ConfigFileName);
            if (!_fileExists(configPath))
            {
                throw new ConfigParseException(CreateBuildDiagnostic(
                    BuildDiagnosticCode.ConfigNotFound,
                    $"Configuration file '{ConfigFileName}' was not found in '{normalizedProjectDir}'.",
                    configPath));
            }

            string json = ReadConfig(configPath);
            ConfigFileModel model = ParseConfig(json, configPath);
            QmlSharpConfig config = ApplyDefaults(model, normalizedProjectDir);
            config = ResolveQtDirectory(config, normalizedProjectDir);

            ImmutableArray<ConfigDiagnostic> diagnostics = Validate(config);
            ThrowIfErrors(diagnostics);

            return config;
        }

        /// <inheritdoc />
        public ImmutableArray<ConfigDiagnostic> Validate(QmlSharpConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            ImmutableArray<ConfigDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<ConfigDiagnostic>();
            ValidatePathValues(config, diagnostics);
            ValidateQtConfig(config.Qt, diagnostics);
            ValidateBuildConfig(config.Build, diagnostics);
            ValidateDevConfig(config.Dev, diagnostics);
            ValidateModuleConfig(config.Module, diagnostics);

            return diagnostics.ToImmutable();
        }

        /// <inheritdoc />
        public QmlSharpConfig GetDefaults()
        {
            return CreateDefaultConfig();
        }

        private static void ValidatePathValues(
            QmlSharpConfig config,
            ImmutableArray<ConfigDiagnostic>.Builder diagnostics)
        {
            if (config.Entry is not null && string.IsNullOrWhiteSpace(config.Entry))
            {
                diagnostics.Add(CreateValidationDiagnostic("entry", "entry must not be empty."));
            }

            if (string.IsNullOrWhiteSpace(config.OutDir))
            {
                diagnostics.Add(CreateValidationDiagnostic("outDir", "outDir must not be empty."));
            }
        }

        private void ValidateQtConfig(QtConfig? qt, ImmutableArray<ConfigDiagnostic>.Builder diagnostics)
        {
            if (qt is null)
            {
                diagnostics.Add(CreateValidationDiagnostic("qt", "The qt section is required."));
            }
            else if (qt.Dir is not null && string.IsNullOrWhiteSpace(qt.Dir))
            {
                diagnostics.Add(CreateValidationDiagnostic("qt.dir", "qt.dir must not be empty."));
            }
            else if (!string.IsNullOrWhiteSpace(qt.Dir) && !_directoryExists(NormalizeRequiredPath(qt.Dir)))
            {
                diagnostics.Add(new ConfigDiagnostic(
                    BuildDiagnosticCode.QtDirNotFound,
                    "qt.dir",
                    $"Qt directory '{qt.Dir}' does not exist.",
                    ConfigDiagnosticSeverity.Error));
            }
        }

        private static void ValidateBuildConfig(BuildConfig build, ImmutableArray<ConfigDiagnostic>.Builder diagnostics)
        {
            if (!string.Equals(build.Mode, "development", StringComparison.Ordinal) &&
                !string.Equals(build.Mode, "production", StringComparison.Ordinal))
            {
                diagnostics.Add(CreateValidationDiagnostic(
                    "build.mode",
                    "Build mode must be either 'development' or 'production'."));
            }
        }

        private static void ValidateDevConfig(DevConfig dev, ImmutableArray<ConfigDiagnostic>.Builder diagnostics)
        {
            if (dev.DebounceMs < 0)
            {
                diagnostics.Add(CreateValidationDiagnostic(
                    "dev.debounceMs",
                    "Debounce interval must be zero or greater."));
            }

            int watchPathIndex = 0;
            foreach (string watchPath in dev.WatchPaths)
            {
                if (string.IsNullOrWhiteSpace(watchPath))
                {
                    diagnostics.Add(CreateValidationDiagnostic(
                        string.Create(CultureInfo.InvariantCulture, $"dev.watchPaths[{watchPathIndex}]"),
                        "dev.watchPaths entries must not be empty."));
                }

                watchPathIndex++;
            }
        }

        private static void ValidateModuleConfig(ModuleConfig? module, ImmutableArray<ConfigDiagnostic>.Builder diagnostics)
        {
            if (module is null)
            {
                diagnostics.Add(CreateValidationDiagnostic("module", "The module section is required."));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(module.Prefix))
                {
                    diagnostics.Add(CreateValidationDiagnostic(
                        "module.prefix",
                        "Module prefix must not be empty."));
                }

                if (module.Version.Major < 0 || module.Version.Minor < 0)
                {
                    diagnostics.Add(CreateValidationDiagnostic(
                        "module.version",
                        "Module version major and minor values must be zero or greater."));
                }
            }
        }

        private static ConfigFileModel ParseConfig(string json, string configPath)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new ConfigParseException(CreateBuildDiagnostic(
                        BuildDiagnosticCode.ConfigParseError,
                        "qmlsharp.json must contain a JSON object.",
                        configPath));
                }

                ImmutableArray<ConfigDiagnostic> shapeDiagnostics = ValidateJsonShape(document.RootElement);
                ThrowIfErrors(shapeDiagnostics);

                ConfigFileModel? model = JsonSerializer.Deserialize<ConfigFileModel>(
                    document.RootElement.GetRawText(),
                    JsonOptions);
                if (model is null)
                {
                    throw new ConfigParseException(CreateBuildDiagnostic(
                        BuildDiagnosticCode.ConfigParseError,
                        "qmlsharp.json did not contain a configuration object.",
                        configPath));
                }

                return model;
            }
            catch (JsonException ex)
            {
                throw new ConfigParseException(CreateBuildDiagnostic(
                    BuildDiagnosticCode.ConfigParseError,
                    $"qmlsharp.json could not be parsed: {ex.Message}",
                    configPath));
            }
        }

        private static ImmutableArray<ConfigDiagnostic> ValidateJsonShape(JsonElement root)
        {
            ImmutableArray<ConfigDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<ConfigDiagnostic>();

            ValidateObjectMembers(
                root,
                string.Empty,
                ImmutableArray.Create("entry", "outDir", "qt", "build", "native", "dev", "module", "name", "version"),
                diagnostics);

            ValidateOptionalString(root, "entry", "entry", diagnostics, rejectWhitespace: true);
            ValidateOptionalString(root, "outDir", "outDir", diagnostics, rejectWhitespace: true);
            ValidateOptionalString(root, "name", "name", diagnostics);
            ValidateOptionalString(root, "version", "version", diagnostics);

            if (!root.TryGetProperty("qt", out JsonElement qtElement))
            {
                diagnostics.Add(CreateValidationDiagnostic("qt", "The qt section is required."));
            }
            else if (ValidateRequiredObject(qtElement, "qt", diagnostics))
            {
                ValidateObjectMembers(qtElement, "qt", ImmutableArray.Create("dir", "modules"), diagnostics);
                ValidateOptionalString(qtElement, "dir", "qt.dir", diagnostics, rejectWhitespace: true);
                ValidateOptionalStringArray(qtElement, "modules", "qt.modules", diagnostics);
            }

            ValidateBuildShape(root, diagnostics);
            ValidateNativeShape(root, diagnostics);
            ValidateDevShape(root, diagnostics);
            ValidateModuleShape(root, diagnostics);

            return diagnostics.ToImmutable();
        }

        private static void ValidateBuildShape(JsonElement root, ImmutableArray<ConfigDiagnostic>.Builder diagnostics)
        {
            if (!root.TryGetProperty("build", out JsonElement buildElement))
            {
                return;
            }

            if (!ValidateRequiredObject(buildElement, "build", diagnostics))
            {
                return;
            }

            ValidateObjectMembers(
                buildElement,
                "build",
                ImmutableArray.Create("aot", "lint", "format", "sourceMaps", "incremental", "mode"),
                diagnostics);
            ValidateOptionalBoolean(buildElement, "aot", "build.aot", diagnostics);
            ValidateOptionalBoolean(buildElement, "lint", "build.lint", diagnostics);
            ValidateOptionalBoolean(buildElement, "format", "build.format", diagnostics);
            ValidateOptionalBoolean(buildElement, "sourceMaps", "build.sourceMaps", diagnostics);
            ValidateOptionalBoolean(buildElement, "incremental", "build.incremental", diagnostics);
            ValidateOptionalString(buildElement, "mode", "build.mode", diagnostics);
        }

        private static void ValidateNativeShape(JsonElement root, ImmutableArray<ConfigDiagnostic>.Builder diagnostics)
        {
            if (!root.TryGetProperty("native", out JsonElement nativeElement))
            {
                return;
            }

            if (!ValidateRequiredObject(nativeElement, "native", diagnostics))
            {
                return;
            }

            ValidateObjectMembers(nativeElement, "native", ImmutableArray.Create("prebuilt", "cmakePreset"), diagnostics);
            ValidateOptionalBoolean(nativeElement, "prebuilt", "native.prebuilt", diagnostics);
            ValidateOptionalString(nativeElement, "cmakePreset", "native.cmakePreset", diagnostics);
        }

        private static void ValidateDevShape(JsonElement root, ImmutableArray<ConfigDiagnostic>.Builder diagnostics)
        {
            if (!root.TryGetProperty("dev", out JsonElement devElement))
            {
                return;
            }

            if (!ValidateRequiredObject(devElement, "dev", diagnostics))
            {
                return;
            }

            ValidateObjectMembers(devElement, "dev", ImmutableArray.Create("hotReload", "watchPaths", "debounceMs"), diagnostics);
            ValidateOptionalBoolean(devElement, "hotReload", "dev.hotReload", diagnostics);
            ValidateOptionalStringArray(
                devElement,
                "watchPaths",
                "dev.watchPaths",
                diagnostics,
                rejectWhitespaceEntries: true);
            ValidateOptionalInteger(devElement, "debounceMs", "dev.debounceMs", diagnostics);
        }

        private static void ValidateModuleShape(JsonElement root, ImmutableArray<ConfigDiagnostic>.Builder diagnostics)
        {
            if (!root.TryGetProperty("module", out JsonElement moduleElement))
            {
                diagnostics.Add(CreateValidationDiagnostic("module", "The module section is required."));
                return;
            }

            if (!ValidateRequiredObject(moduleElement, "module", diagnostics))
            {
                return;
            }

            ValidateObjectMembers(moduleElement, "module", ImmutableArray.Create("prefix", "version"), diagnostics);
            if (!moduleElement.TryGetProperty("prefix", out JsonElement prefixElement))
            {
                diagnostics.Add(CreateValidationDiagnostic("module.prefix", "Module prefix is required."));
            }
            else if (prefixElement.ValueKind != JsonValueKind.String)
            {
                diagnostics.Add(CreateValidationDiagnostic("module.prefix", "Module prefix must be a string."));
            }

            if (!moduleElement.TryGetProperty("version", out JsonElement versionElement))
            {
                return;
            }

            if (!ValidateRequiredObject(versionElement, "module.version", diagnostics))
            {
                return;
            }

            ValidateObjectMembers(versionElement, "module.version", ImmutableArray.Create("major", "minor"), diagnostics);
            ValidateOptionalInteger(versionElement, "major", "module.version.major", diagnostics);
            ValidateOptionalInteger(versionElement, "minor", "module.version.minor", diagnostics);
        }

        private static void ValidateObjectMembers(
            JsonElement element,
            string pathPrefix,
            ImmutableArray<string> allowedProperties,
            ImmutableArray<ConfigDiagnostic>.Builder diagnostics)
        {
            HashSet<string> allowed = new(allowedProperties, StringComparer.Ordinal);
            IEnumerable<string> fields = element.EnumerateObject()
                .Where(property => !allowed.Contains(property.Name))
                .Select(property => pathPrefix.Length == 0
                    ? property.Name
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"{pathPrefix}.{property.Name}"));
            foreach (string field in fields)
            {
                diagnostics.Add(CreateValidationDiagnostic(field, $"Unknown configuration field '{field}'."));
            }
        }

        private static bool ValidateRequiredObject(
            JsonElement element,
            string field,
            ImmutableArray<ConfigDiagnostic>.Builder diagnostics)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                return true;
            }

            diagnostics.Add(CreateValidationDiagnostic(field, $"{field} must be a JSON object."));
            return false;
        }

        private static void ValidateOptionalString(
            JsonElement element,
            string propertyName,
            string field,
            ImmutableArray<ConfigDiagnostic>.Builder diagnostics,
            bool rejectWhitespace = false)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value))
            {
                return;
            }

            if (value.ValueKind is not JsonValueKind.String and not JsonValueKind.Null)
            {
                diagnostics.Add(CreateValidationDiagnostic(field, $"{field} must be a string."));
                return;
            }

            if (rejectWhitespace &&
                value.ValueKind == JsonValueKind.String &&
                string.IsNullOrWhiteSpace(value.GetString()))
            {
                diagnostics.Add(CreateValidationDiagnostic(field, $"{field} must not be empty."));
            }
        }

        private static void ValidateOptionalBoolean(
            JsonElement element,
            string propertyName,
            string field,
            ImmutableArray<ConfigDiagnostic>.Builder diagnostics)
        {
            if (element.TryGetProperty(propertyName, out JsonElement value) &&
                value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            {
                diagnostics.Add(CreateValidationDiagnostic(field, $"{field} must be a boolean."));
            }
        }

        private static void ValidateOptionalInteger(
            JsonElement element,
            string propertyName,
            string field,
            ImmutableArray<ConfigDiagnostic>.Builder diagnostics)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value))
            {
                return;
            }

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int _))
            {
                diagnostics.Add(CreateValidationDiagnostic(field, $"{field} must be an integer."));
            }
        }

        private static void ValidateOptionalStringArray(
            JsonElement element,
            string propertyName,
            string field,
            ImmutableArray<ConfigDiagnostic>.Builder diagnostics,
            bool rejectWhitespaceEntries = false)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value))
            {
                return;
            }

            if (value.ValueKind != JsonValueKind.Array)
            {
                diagnostics.Add(CreateValidationDiagnostic(field, $"{field} must be an array."));
                return;
            }

            int index = 0;
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    diagnostics.Add(CreateValidationDiagnostic(
                        string.Create(CultureInfo.InvariantCulture, $"{field}[{index}]"),
                        $"{field} entries must be strings."));
                }
                else if (rejectWhitespaceEntries && string.IsNullOrWhiteSpace(item.GetString()))
                {
                    diagnostics.Add(CreateValidationDiagnostic(
                        string.Create(CultureInfo.InvariantCulture, $"{field}[{index}]"),
                        $"{field} entries must not be empty."));
                }

                index++;
            }
        }

        private static QmlSharpConfig ApplyDefaults(ConfigFileModel model, string projectDir)
        {
            QmlSharpConfig defaults = CreateDefaultConfig();
            string modulePrefix = model.Module?.Prefix ?? string.Empty;
            QmlVersion moduleVersion = new(
                model.Module?.Version?.Major ?? defaults.Module.Version.Major,
                model.Module?.Version?.Minor ?? defaults.Module.Version.Minor);
            string? name = model.Name ?? DeriveName(modulePrefix);

            return new QmlSharpConfig
            {
                Entry = NormalizeOptionalPath(model.Entry, projectDir),
                OutDir = NormalizeRequiredPath(model.OutDir ?? defaults.OutDir, projectDir),
                Qt = new QtConfig
                {
                    Dir = NormalizeOptionalPath(model.Qt?.Dir, projectDir),
                    Modules = model.Qt?.Modules is null
                        ? defaults.Qt.Modules
                        : model.Qt.Modules.ToImmutableArray(),
                },
                Build = new BuildConfig
                {
                    Aot = model.Build?.Aot ?? defaults.Build.Aot,
                    Lint = model.Build?.Lint ?? defaults.Build.Lint,
                    Format = model.Build?.Format ?? defaults.Build.Format,
                    SourceMaps = model.Build?.SourceMaps ?? defaults.Build.SourceMaps,
                    Incremental = model.Build?.Incremental ?? defaults.Build.Incremental,
                    Mode = model.Build?.Mode ?? defaults.Build.Mode,
                },
                Native = new NativeConfig
                {
                    Prebuilt = model.Native?.Prebuilt ?? defaults.Native.Prebuilt,
                    CmakePreset = model.Native?.CmakePreset ?? defaults.Native.CmakePreset,
                },
                Dev = new DevConfig
                {
                    HotReload = model.Dev?.HotReload ?? defaults.Dev.HotReload,
                    WatchPaths = model.Dev?.WatchPaths is null
                        ? NormalizePaths(defaults.Dev.WatchPaths, projectDir)
                        : NormalizePaths(model.Dev.WatchPaths.ToImmutableArray(), projectDir),
                    DebounceMs = model.Dev?.DebounceMs ?? defaults.Dev.DebounceMs,
                },
                Module = new ModuleConfig
                {
                    Prefix = modulePrefix,
                    Version = moduleVersion,
                },
                Name = name,
                Version = model.Version ?? defaults.Version,
            };
        }

        private QmlSharpConfig ResolveQtDirectory(QmlSharpConfig config, string projectDir)
        {
            if (config.Qt.Dir is not null)
            {
                return config;
            }

            try
            {
                QtInstallation installation = _qtToolchain.DiscoverAsync(
                    new QtToolchainConfig
                    {
                        Cwd = projectDir,
                    }).GetAwaiter().GetResult();

                return config with
                {
                    Qt = config.Qt with
                    {
                        Dir = installation.RootDir,
                    },
                };
            }
            catch (QtInstallationNotFoundError ex)
            {
                throw new ConfigParseException(CreateBuildDiagnostic(
                    BuildDiagnosticCode.QtDirNotFound,
                    $"Qt directory could not be discovered: {ex.Message}",
                    "qt.dir"));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                throw new ConfigParseException(CreateBuildDiagnostic(
                    BuildDiagnosticCode.QtDirNotFound,
                    $"Qt directory could not be discovered: {ex.Message}",
                    "qt.dir"));
            }
        }

        private static QmlSharpConfig CreateDefaultConfig()
        {
            return new QmlSharpConfig
            {
                Qt = new QtConfig(),
                Module = new ModuleConfig
                {
                    Prefix = "QmlSharp.MyApp",
                    Version = new QmlVersion(1, 0),
                },
                Name = "MyApp",
                Version = "0.0.0",
            };
        }

        private string ReadConfig(string configPath)
        {
            try
            {
                return _readAllText(configPath);
            }
            catch (IOException ex)
            {
                throw new ConfigParseException(CreateBuildDiagnostic(
                    BuildDiagnosticCode.ConfigParseError,
                    $"qmlsharp.json could not be read: {ex.Message}",
                    configPath));
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new ConfigParseException(CreateBuildDiagnostic(
                    BuildDiagnosticCode.ConfigParseError,
                    $"qmlsharp.json could not be read: {ex.Message}",
                    configPath));
            }
        }

        private static void ThrowIfErrors(ImmutableArray<ConfigDiagnostic> diagnostics)
        {
            ImmutableArray<BuildDiagnostic> errors = diagnostics
                .Where(static diagnostic => diagnostic.Severity == ConfigDiagnosticSeverity.Error)
                .Select(diagnostic => CreateBuildDiagnostic(
                    diagnostic.Code,
                    $"{diagnostic.Field}: {diagnostic.Message}",
                    diagnostic.Field))
                .ToImmutableArray();

            if (!errors.IsDefaultOrEmpty)
            {
                throw new ConfigParseException(errors);
            }
        }

        private static BuildDiagnostic CreateBuildDiagnostic(string code, string message, string fieldOrPath)
        {
            return new BuildDiagnostic(
                code,
                BuildDiagnosticSeverity.Error,
                message,
                BuildPhase.ConfigLoading,
                fieldOrPath);
        }

        private static ConfigDiagnostic CreateValidationDiagnostic(string field, string message)
        {
            return new ConfigDiagnostic(
                BuildDiagnosticCode.ConfigValidationError,
                field,
                message,
                ConfigDiagnosticSeverity.Error);
        }

        private static ImmutableArray<string> NormalizePaths(ImmutableArray<string> paths, string projectDir)
        {
            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>(paths.Length);
            foreach (string path in paths)
            {
                builder.Add(NormalizeRequiredPath(path, projectDir));
            }

            return builder.ToImmutable();
        }

        private static string? NormalizeOptionalPath(string? path, string projectDir)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            return NormalizeRequiredPath(path, projectDir);
        }

        private static string NormalizeRequiredPath(string path)
        {
            return NormalizeRequiredPath(path, Directory.GetCurrentDirectory());
        }

        private static string NormalizeRequiredPath(string path, string basePath)
        {
            string trimmed = path.Trim();
            if (Path.IsPathRooted(trimmed))
            {
                return Path.GetFullPath(trimmed);
            }

            return Path.GetFullPath(Path.Join(basePath, trimmed));
        }

        private static string? DeriveName(string modulePrefix)
        {
            if (string.IsNullOrWhiteSpace(modulePrefix))
            {
                return null;
            }

            string[] segments = modulePrefix.Split('.', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length == 0 ? modulePrefix : segments[^1];
        }

        private sealed record ConfigFileModel
        {
            public string? Entry { get; init; }

            public string? OutDir { get; init; }

            public QtConfigModel? Qt { get; init; }

            public BuildConfigModel? Build { get; init; }

            public NativeConfigModel? Native { get; init; }

            public DevConfigModel? Dev { get; init; }

            public ModuleConfigModel? Module { get; init; }

            public string? Name { get; init; }

            public string? Version { get; init; }
        }

        private sealed record QtConfigModel
        {
            public string? Dir { get; init; }

            public string[]? Modules { get; init; }
        }

        private sealed record BuildConfigModel
        {
            public bool? Aot { get; init; }

            public bool? Lint { get; init; }

            public bool? Format { get; init; }

            public bool? SourceMaps { get; init; }

            public bool? Incremental { get; init; }

            public string? Mode { get; init; }
        }

        private sealed record NativeConfigModel
        {
            public bool? Prebuilt { get; init; }

            public string? CmakePreset { get; init; }
        }

        private sealed record DevConfigModel
        {
            public bool? HotReload { get; init; }

            public string[]? WatchPaths { get; init; }

            public int? DebounceMs { get; init; }
        }

        private sealed record ModuleConfigModel
        {
            public string? Prefix { get; init; }

            public QmlVersionModel? Version { get; init; }
        }

        private sealed record QmlVersionModel
        {
            public int? Major { get; init; }

            public int? Minor { get; init; }
        }
    }
}
