using System.Globalization;
using System.Text.Json;

namespace QmlSharp.Qt.Tools
{
    /// <summary>Default filesystem-backed implementation of <see cref="IQtToolchain"/>.</summary>
    public sealed class QtToolchain : IQtToolchain
    {
        private const string QtDirEnvironmentVariable = "QT_DIR";
        private const string ConfigDirectoryName = ".qmlsharp";
        private const string ConfigFileName = "config.json";

        private readonly Lock _cacheLock = new();
        private readonly Func<string, string?> _getEnvironmentVariable;
        private readonly Func<string, bool> _directoryExists;
        private readonly Func<string, bool> _fileExists;
        private readonly Func<string> _getCurrentDirectory;
        private QtInstallation? _installation;

        /// <summary>Create a toolchain using the current process environment and filesystem.</summary>
        public QtToolchain()
            : this(
                Environment.GetEnvironmentVariable,
                Directory.Exists,
                File.Exists,
                Directory.GetCurrentDirectory)
        {
        }

        internal QtToolchain(
            Func<string, string?> getEnvironmentVariable,
            Func<string, bool> directoryExists,
            Func<string, bool> fileExists,
            Func<string> getCurrentDirectory)
        {
            _getEnvironmentVariable = getEnvironmentVariable;
            _directoryExists = directoryExists;
            _fileExists = fileExists;
            _getCurrentDirectory = getCurrentDirectory;
        }

        /// <inheritdoc />
        public QtInstallation? Installation => _installation;

        /// <inheritdoc />
        public Task<QtInstallation> DiscoverAsync(QtToolchainConfig? config = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            lock (_cacheLock)
            {
                if (_installation is not null)
                {
                    return Task.FromResult(_installation);
                }

                QtToolchainConfig effectiveConfig = config ?? new QtToolchainConfig();
                ImmutableArray<string>.Builder attemptedSteps = ImmutableArray.CreateBuilder<string>();
                QtInstallation? discovered = DiscoverFromConfig(effectiveConfig, attemptedSteps);
                if (discovered is null)
                {
                    discovered = DiscoverFromEnvironment(effectiveConfig, attemptedSteps);
                }

                if (discovered is null)
                {
                    discovered = DiscoverFromConfigFiles(effectiveConfig, attemptedSteps);
                }

                if (discovered is null)
                {
                    discovered = DiscoverFromWellKnownDirectories(effectiveConfig, attemptedSteps);
                }

                if (discovered is null)
                {
                    discovered = DiscoverFromPath(effectiveConfig, attemptedSteps);
                }

                if (discovered is null)
                {
                    throw new QtInstallationNotFoundError(
                        "Qt installation was not found. Set QtToolchainConfig.QtDir, set QT_DIR, add .qmlsharp/config.json, install Qt in a well-known location, or put Qt tools on PATH.",
                        attemptedSteps.ToImmutable());
                }

                _installation = discovered;
                return Task.FromResult(discovered);
            }
        }

        /// <inheritdoc />
        public async Task<ToolAvailability> CheckToolsAsync(CancellationToken ct = default)
        {
            QtInstallation installation = await DiscoverAsync(ct: ct).ConfigureAwait(false);

            return new ToolAvailability
            {
                QmlFormat = CreateToolInfo(installation, "qmlformat"),
                QmlLint = CreateToolInfo(installation, "qmllint"),
                QmlCachegen = CreateToolInfo(installation, "qmlcachegen"),
                Qmltc = CreateToolInfo(installation, "qmltc"),
                QmlImportScanner = CreateToolInfo(installation, "qmlimportscanner"),
                QmlDom = CreateToolInfo(installation, "qmldom"),
                Qml = CreateToolInfo(installation, "qml"),
                Rcc = CreateToolInfo(installation, "rcc"),
                QmlTypeRegistrar = CreateToolInfo(installation, "qmltyperegistrar"),
                Moc = CreateToolInfo(installation, "moc"),
                QmlAotStats = CreateToolInfo(installation, "qmlaotstats"),
            };
        }

        /// <inheritdoc />
        public async Task<ToolInfo> GetToolInfoAsync(string toolName, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

            QtInstallation installation = await DiscoverAsync(ct: ct).ConfigureAwait(false);
            ToolInfo info = CreateToolInfo(installation, toolName);
            if (!info.Available)
            {
                throw new QtToolNotFoundError(toolName, info.Path);
            }

            return info;
        }

        private QtInstallation? DiscoverFromConfig(
            QtToolchainConfig config,
            ImmutableArray<string>.Builder attemptedSteps)
        {
            string? qtDir = NormalizePath(config.QtDir);
            if (qtDir is null)
            {
                attemptedSteps.Add("explicit QtToolchainConfig.QtDir: not set");
                return null;
            }

            QtInstallation? installation = TryCreateInstallation(qtDir, config.ImportPaths, out string reason);
            attemptedSteps.Add($"explicit QtToolchainConfig.QtDir: {qtDir} ({reason})");
            if (installation is null)
            {
                throw new QtInstallationNotFoundError(
                    $"Explicit QtToolchainConfig.QtDir '{qtDir}' is not a usable Qt installation.",
                    attemptedSteps.ToImmutable());
            }

            return installation;
        }

        private QtInstallation? DiscoverFromEnvironment(
            QtToolchainConfig config,
            ImmutableArray<string>.Builder attemptedSteps)
        {
            string? qtDir = NormalizePath(_getEnvironmentVariable(QtDirEnvironmentVariable));
            if (qtDir is null)
            {
                attemptedSteps.Add("QT_DIR: not set");
                return null;
            }

            QtInstallation? installation = TryCreateInstallation(qtDir, config.ImportPaths, out string reason);
            attemptedSteps.Add($"QT_DIR: {qtDir} ({reason})");
            return installation;
        }

        private QtInstallation? DiscoverFromConfigFiles(
            QtToolchainConfig config,
            ImmutableArray<string>.Builder attemptedSteps)
        {
            foreach (string configPath in EnumerateConfigFileCandidates(config))
            {
                QtInstallation? installation = DiscoverFromConfigFile(configPath, config, attemptedSteps);
                if (installation is not null)
                {
                    return installation;
                }
            }

            attemptedSteps.Add(".qmlsharp/config.json: no usable config file found");
            return null;
        }

        private QtInstallation? DiscoverFromConfigFile(
            string configPath,
            QtToolchainConfig config,
            ImmutableArray<string>.Builder attemptedSteps)
        {
            if (!_fileExists(configPath))
            {
                return null;
            }

            try
            {
                using FileStream stream = File.OpenRead(configPath);
                using JsonDocument document = JsonDocument.Parse(stream);
                if (!document.RootElement.TryGetProperty("qtDir", out JsonElement qtDirElement)
                    || qtDirElement.ValueKind != JsonValueKind.String)
                {
                    attemptedSteps.Add($"{configPath}: missing string qtDir");
                    return null;
                }

                string? qtDir = NormalizePath(qtDirElement.GetString());
                if (qtDir is null)
                {
                    attemptedSteps.Add($"{configPath}: qtDir is empty");
                    return null;
                }

                ImmutableArray<string> importPaths = MergeImportPaths(
                    config.ImportPaths,
                    ReadImportPaths(document.RootElement));
                QtInstallation? installation = TryCreateInstallation(qtDir, importPaths, out string reason);
                attemptedSteps.Add($"{configPath}: {qtDir} ({reason})");
                return installation;
            }
            catch (JsonException ex)
            {
                attemptedSteps.Add($"{configPath}: invalid JSON ({ex.Message})");
                return null;
            }
            catch (IOException ex)
            {
                attemptedSteps.Add($"{configPath}: unreadable ({ex.Message})");
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                attemptedSteps.Add($"{configPath}: unreadable ({ex.Message})");
                return null;
            }
        }

        private QtInstallation? DiscoverFromWellKnownDirectories(
            QtToolchainConfig config,
            ImmutableArray<string>.Builder attemptedSteps)
        {
            foreach (string candidate in EnumerateWellKnownQtRoots())
            {
                QtInstallation? installation = TryCreateInstallation(candidate, config.ImportPaths, out string reason);
                attemptedSteps.Add($"auto-scan: {candidate} ({reason})");
                if (installation is not null)
                {
                    return installation;
                }
            }

            if (!attemptedSteps.Any(static step => step.StartsWith("auto-scan:", StringComparison.Ordinal)))
            {
                attemptedSteps.Add("auto-scan: no candidates");
            }

            return null;
        }

        private QtInstallation? DiscoverFromPath(
            QtToolchainConfig config,
            ImmutableArray<string>.Builder attemptedSteps)
        {
            string path = _getEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string binDir in path
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(static pathEntry => NormalizePath(pathEntry) ?? string.Empty))
            {
                if (binDir.Length == 0)
                {
                    continue;
                }

                string qmlformat = Path.Join(binDir, GetExecutableName("qmlformat"));
                if (!_fileExists(qmlformat))
                {
                    attemptedSteps.Add($"PATH: {binDir} (qmlformat not found)");
                    continue;
                }

                string? root = Directory.GetParent(binDir)?.FullName;
                if (root is null)
                {
                    attemptedSteps.Add($"PATH: {binDir} (no parent Qt root)");
                    continue;
                }

                QtInstallation? installation = TryCreateInstallation(root, config.ImportPaths, out string reason);
                attemptedSteps.Add($"PATH: {binDir} -> {root} ({reason})");
                if (installation is not null)
                {
                    return installation;
                }
            }

            if (!attemptedSteps.Any(static step => step.StartsWith("PATH:", StringComparison.Ordinal)))
            {
                attemptedSteps.Add("PATH: no entries");
            }

            return null;
        }

        private QtInstallation? TryCreateInstallation(
            string rootDir,
            ImmutableArray<string> extraImportPaths,
            out string reason)
        {
            string root = NormalizePath(rootDir) ?? string.Empty;
            if (root.Length == 0)
            {
                reason = "empty path";
                return null;
            }

            if (!_directoryExists(root))
            {
                reason = "root directory not found";
                return null;
            }

            string binDir = Path.Join(root, "bin");
            if (!_directoryExists(binDir))
            {
                reason = "bin directory not found";
                return null;
            }

            string qmlDir = Path.Join(root, "qml");
            if (!_directoryExists(qmlDir))
            {
                reason = "qml import directory not found";
                return null;
            }

            string qmlformat = Path.Join(binDir, GetExecutableName("qmlformat"));
            if (!_fileExists(qmlformat))
            {
                reason = "qmlformat not found";
                return null;
            }

            reason = "found";
            return new QtInstallation
            {
                RootDir = root,
                BinDir = binDir,
                QmlDir = qmlDir,
                LibDir = Path.Join(root, "lib"),
                Version = DetectVersion(root),
                Platform = DetectPlatform(),
                ImportPaths = MergeImportPaths([qmlDir], extraImportPaths),
            };
        }

        private ToolInfo CreateToolInfo(QtInstallation installation, string toolName)
        {
            string normalizedName = toolName.Trim();
            string executableName = GetExecutableName(normalizedName);
            string fallbackPath = Path.Join(installation.BinDir, executableName);
            string path = EnumerateToolPathCandidates(installation, executableName)
                .FirstOrDefault(_fileExists) ?? fallbackPath;
            bool available = _fileExists(path);

            return new ToolInfo
            {
                Name = normalizedName,
                Path = path,
                Available = available,
                Version = available ? installation.Version.String : null,
            };
        }

        private static IEnumerable<string> EnumerateToolPathCandidates(QtInstallation installation, string executableName)
        {
            yield return Path.Join(installation.BinDir, executableName);
            yield return Path.Join(installation.RootDir, "libexec", executableName);
            yield return Path.Join(installation.RootDir, "lib", "qt6", "bin", executableName);
            yield return Path.Join(installation.RootDir, "lib", "qt6", "libexec", executableName);
            yield return Path.Join(installation.RootDir, "lib", "qt", "bin", executableName);
            yield return Path.Join(installation.RootDir, "lib", "qt", "libexec", executableName);
        }

        private IEnumerable<string> EnumerateConfigFileCandidates(QtToolchainConfig config)
        {
            HashSet<string> yielded = new(GetPathComparer());
            string startDirectory = NormalizePath(config.Cwd) ?? _getCurrentDirectory();
            DirectoryInfo? directory = new(startDirectory);

            while (directory is not null)
            {
                string candidate = Path.Join(directory.FullName, ConfigDirectoryName, ConfigFileName);
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }

                directory = directory.Parent;
            }

            string? home = NormalizePath(_getEnvironmentVariable("USERPROFILE"))
                ?? NormalizePath(_getEnvironmentVariable("HOME"));
            if (home is not null)
            {
                string candidate = Path.Join(home, ConfigDirectoryName, ConfigFileName);
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private IEnumerable<string> EnumerateWellKnownQtRoots()
        {
            HashSet<string> candidates = new(GetPathComparer());
            foreach (string root in EnumerateWellKnownQtParents().Where(_directoryExists))
            {
                foreach (string candidate in EnumerateKnownChildren(root).Where(candidates.Add))
                {
                    yield return candidate;
                }
            }
        }

        private IEnumerable<string> EnumerateWellKnownQtParents()
        {
            string? home = NormalizePath(_getEnvironmentVariable("USERPROFILE"))
                ?? NormalizePath(_getEnvironmentVariable("HOME"));
            if (home is not null)
            {
                yield return Path.Join(home, "Qt");
            }

            if (OperatingSystem.IsWindows())
            {
                yield return @"C:\Qt";
            }
            else if (OperatingSystem.IsMacOS())
            {
                yield return "/opt/Qt";
                yield return "/usr/local/Qt";
                yield return "/Applications/Qt";
            }
            else
            {
                yield return "/opt/Qt";
                yield return "/usr/local/Qt";
                yield return "/usr/lib/qt6";
                yield return "/usr/lib/qt";
            }
        }

        private IEnumerable<string> EnumerateKnownChildren(string root)
        {
            yield return root;

            foreach (string versionDirectory in SafeEnumerateDirectories(root))
            {
                yield return versionDirectory;

                foreach (string kitDirectory in SafeEnumerateDirectories(versionDirectory))
                {
                    yield return kitDirectory;
                }
            }
        }

        private IEnumerable<string> SafeEnumerateDirectories(string directory)
        {
            try
            {
                return Directory
                    .EnumerateDirectories(directory)
                    .OrderBy(static path => path, GetPathComparer())
                    .ToArray();
            }
            catch (IOException)
            {
                return [];
            }
            catch (UnauthorizedAccessException)
            {
                return [];
            }
        }

        private static ImmutableArray<string> ReadImportPaths(JsonElement root)
        {
            if (!root.TryGetProperty("importPaths", out JsonElement importPathsElement)
                || importPathsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
            foreach (string value in importPathsElement
                .EnumerateArray()
                .Where(static element => element.ValueKind == JsonValueKind.String)
                .Select(static element => NormalizePath(element.GetString()))
                .Where(static value => value is not null)
                .Select(static value => value!))
            {
                builder.Add(value);
            }

            return builder.ToImmutable();
        }

        private static ImmutableArray<string> MergeImportPaths(
            ImmutableArray<string> first,
            ImmutableArray<string> second)
        {
            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
            HashSet<string> seen = new(GetPathComparer());
            AddImportPaths(first, builder, seen);
            AddImportPaths(second, builder, seen);
            return builder.ToImmutable();
        }

        private static void AddImportPaths(
            ImmutableArray<string> paths,
            ImmutableArray<string>.Builder builder,
            HashSet<string> seen)
        {
            foreach (string normalized in paths
                .Select(NormalizePath)
                .Where(static normalized => normalized is not null)
                .Select(static normalized => normalized!)
                .Where(seen.Add))
            {
                builder.Add(normalized);
            }
        }

        private static QtVersion DetectVersion(string rootDir)
        {
            DirectoryInfo? directory = new(rootDir);
            while (directory is not null)
            {
                if (TryParseVersion(directory.Name, out QtVersion? version) && version is not null)
                {
                    return version;
                }

                directory = directory.Parent;
            }

            return new QtVersion { Major = 0, Minor = 0, Patch = 0 };
        }

        private static bool TryParseVersion(string value, out QtVersion? version)
        {
            version = null;
            string[] parts = value.Split('.');
            if (parts.Length < 2 || parts.Length > 3)
            {
                return false;
            }

            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major)
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minor))
            {
                return false;
            }

            int patch = 0;
            if (parts.Length == 3
                && !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out patch))
            {
                return false;
            }

            version = new QtVersion { Major = major, Minor = minor, Patch = patch };
            return true;
        }

        private static string DetectPlatform()
        {
            if (OperatingSystem.IsWindows())
            {
                return "windows";
            }

            if (OperatingSystem.IsMacOS())
            {
                return "macos";
            }

            return "linux";
        }

        private static string GetExecutableName(string toolName)
        {
            if (OperatingSystem.IsWindows())
            {
                return toolName + ".exe";
            }

            return toolName;
        }

        private static string? NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string normalizedPath = path.Trim();
            if (normalizedPath.Length >= 2
                && normalizedPath.StartsWith('"')
                && normalizedPath.EndsWith('"'))
            {
                normalizedPath = normalizedPath[1..^1].Trim();
            }

            if (normalizedPath.Length == 0)
            {
                return null;
            }

            try
            {
                return Path.GetFullPath(normalizedPath);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (PathTooLongException)
            {
                return null;
            }
        }

        private static StringComparer GetPathComparer()
        {
            return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }
    }
}
