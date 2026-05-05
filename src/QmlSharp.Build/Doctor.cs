using System.Text.Json;
using QmlSharp.Qt.Tools;

namespace QmlSharp.Build
{
    /// <summary>Default environment diagnostic runner.</summary>
    public sealed class Doctor : IDoctor
    {
        private const string QtDirEnvironmentVariable = "QT_DIR";
        private const string RequiredQtVersionText = "6.11.0";
        private const string FallbackRequiredDotNetVersionText = "10.0.0";
        private const string RequiredCMakeVersionText = "3.21.0";
        private const string NativeTargetName = "qmlsharp_native";

        private readonly string _projectDir;
        private readonly IQtToolchain _qtToolchain;
        private readonly IConfigLoader _configLoader;
        private readonly IDoctorEnvironment _environment;

        /// <summary>Create a doctor for the current directory.</summary>
        public Doctor()
            : this(null)
        {
        }

        /// <summary>Create a doctor for a project directory.</summary>
        public Doctor(string? projectDir)
            : this(projectDir, new QtToolchain(), new ConfigLoader(), new DoctorEnvironment())
        {
        }

        internal Doctor(
            string? projectDir,
            IQtToolchain qtToolchain,
            IConfigLoader configLoader,
            IDoctorEnvironment environment)
        {
            ArgumentNullException.ThrowIfNull(qtToolchain);
            ArgumentNullException.ThrowIfNull(configLoader);
            ArgumentNullException.ThrowIfNull(environment);

            _projectDir = NormalizeProjectDir(projectDir, environment.CurrentDirectory);
            _qtToolchain = qtToolchain;
            _configLoader = configLoader;
            _environment = environment;
        }

        /// <inheritdoc />
        public async Task<ImmutableArray<DoctorCheckResult>> RunAllChecksAsync(QmlSharpConfig? config = null)
        {
            DoctorState state = await CreateStateAsync(config).ConfigureAwait(false);
            ImmutableArray<DoctorCheckResult>.Builder builder =
                ImmutableArray.CreateBuilder<DoctorCheckResult>(DoctorCheckId.All.Length);
            foreach (string checkId in DoctorCheckId.All)
            {
                builder.Add(await RunKnownCheckAsync(checkId, state).ConfigureAwait(false));
            }

            return builder.ToImmutable();
        }

        /// <inheritdoc />
        public async Task<DoctorCheckResult> RunCheckAsync(string checkId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(checkId);

            if (!DoctorCheckId.All.Contains(checkId, StringComparer.Ordinal))
            {
                return new DoctorCheckResult(
                    checkId,
                    "Unknown doctor check",
                    DoctorCheckStatus.Fail,
                    $"Unknown doctor check '{checkId}'.",
                    $"Use one of: {string.Join(", ", DoctorCheckId.All)}.",
                    false);
            }

            DoctorState state = await CreateStateAsync(config: null).ConfigureAwait(false);
            return await RunKnownCheckAsync(checkId, state).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<ImmutableArray<DoctorFixResult>> AutoFixAsync(
            ImmutableArray<DoctorCheckResult> failedChecks)
        {
            ImmutableArray<DoctorFixResult>.Builder fixes =
                ImmutableArray.CreateBuilder<DoctorFixResult>(failedChecks.Length);
            foreach (DoctorCheckResult check in failedChecks)
            {
                if (!string.Equals(check.CheckId, DoctorCheckId.NuGetResolved, StringComparison.Ordinal))
                {
                    fixes.Add(new DoctorFixResult(
                        check.CheckId,
                        false,
                        $"Check '{check.CheckId}' does not have an approved automatic fix."));
                    continue;
                }

                fixes.Add(await RestoreNuGetAsync(CancellationToken.None).ConfigureAwait(false));
            }

            return fixes.ToImmutable();
        }

        private async Task<DoctorState> CreateStateAsync(QmlSharpConfig? config)
        {
            QmlSharpConfig? effectiveConfig = config;
            ConfigParseException? configException = null;
            if (effectiveConfig is null)
            {
                try
                {
                    effectiveConfig = _configLoader.Load(_projectDir);
                }
                catch (ConfigParseException ex)
                {
                    configException = ex;
                }
            }

            string? qtDir = effectiveConfig?.Qt.Dir ?? _environment.GetEnvironmentVariable(QtDirEnvironmentVariable);
            QtInstallation? installation = null;
            Exception? qtException = null;
            try
            {
                installation = await _qtToolchain.DiscoverAsync(
                    new QtToolchainConfig
                    {
                        QtDir = qtDir,
                        Cwd = _projectDir,
                    }).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is QtInstallationNotFoundError or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                qtException = ex;
            }

            return new DoctorState(effectiveConfig, configException, installation, qtException);
        }

        private Task<DoctorCheckResult> RunKnownCheckAsync(string checkId, DoctorState state)
        {
            return checkId switch
            {
                DoctorCheckId.QtInstalled => Task.FromResult(CheckQtInstalled(state)),
                DoctorCheckId.QtVersion => Task.FromResult(CheckQtVersion(state)),
                DoctorCheckId.QmlFormatAvailable => Task.FromResult(CheckQtTool(
                    state,
                    DoctorCheckId.QmlFormatAvailable,
                    "qmlformat tool",
                    "qmlformat",
                    required: true)),
                DoctorCheckId.QmlLintAvailable => Task.FromResult(CheckQtTool(
                    state,
                    DoctorCheckId.QmlLintAvailable,
                    "qmllint tool",
                    "qmllint",
                    required: true)),
                DoctorCheckId.QmlCachegenAvailable => Task.FromResult(CheckQtTool(
                    state,
                    DoctorCheckId.QmlCachegenAvailable,
                    "qmlcachegen AOT compiler",
                    "qmlcachegen",
                    required: false)),
                DoctorCheckId.DotNetVersion => CheckDotNetVersionAsync(),
                DoctorCheckId.CMakeAvailable => CheckCMakeAvailableAsync(),
                DoctorCheckId.CMakeVersion => CheckCMakeVersionAsync(),
                DoctorCheckId.MsvcAvailable => CheckMsvcAvailableAsync(),
                DoctorCheckId.ClangAvailable => CheckClangAvailableAsync(),
                DoctorCheckId.NinjaAvailable => CheckNinjaAvailableAsync(),
                DoctorCheckId.ConfigValid => Task.FromResult(CheckConfigValid(state)),
                DoctorCheckId.NativeLibExists => Task.FromResult(CheckNativeLibExists(state)),
                DoctorCheckId.NuGetResolved => Task.FromResult(CheckNuGetResolved()),
                _ => Task.FromResult(new DoctorCheckResult(
                    checkId,
                    checkId,
                    DoctorCheckStatus.Fail,
                    $"Unknown doctor check '{checkId}'.",
                    null,
                    false)),
            };
        }

        private static DoctorCheckResult CheckQtInstalled(DoctorState state)
        {
            if (state.QtInstallation is not null)
            {
                return Pass(
                    DoctorCheckId.QtInstalled,
                    "Qt SDK installed",
                    $"Qt found at {state.QtInstallation.RootDir}.");
            }

            return Fail(
                DoctorCheckId.QtInstalled,
                "Qt SDK installed",
                state.QtException?.Message ?? "Qt SDK was not found.",
                "Install Qt 6.11.0 and set QT_DIR to the Qt kit directory.",
                autoFixable: false);
        }

        private static DoctorCheckResult CheckQtVersion(DoctorState state)
        {
            if (state.QtInstallation is null)
            {
                return Fail(
                    DoctorCheckId.QtVersion,
                    "Qt version",
                    "Cannot check Qt version because Qt SDK was not found.",
                    "Install Qt 6.11.0 and set QT_DIR to the Qt kit directory.",
                    autoFixable: false);
            }

            Version actual = ToVersion(state.QtInstallation.Version);
            Version required = ParseRequiredVersion(RequiredQtVersionText);
            if (actual >= required)
            {
                return Pass(
                    DoctorCheckId.QtVersion,
                    $"Qt version >= {RequiredQtVersionText}",
                    $"Found Qt {state.QtInstallation.Version.String}.");
            }

            return Fail(
                DoctorCheckId.QtVersion,
                $"Qt version >= {RequiredQtVersionText}",
                $"Found Qt {state.QtInstallation.Version.String}; QmlSharp requires Qt {RequiredQtVersionText} or newer.",
                "Install the Qt 6.11.0 SDK and update QT_DIR.",
                autoFixable: false);
        }

        private DoctorCheckResult CheckQtTool(
            DoctorState state,
            string checkId,
            string description,
            string toolName,
            bool required)
        {
            ImmutableArray<string> extraDirectories = GetQtToolDirectories(state);
            string? path = FindExecutable(toolName, extraDirectories);
            if (path is not null)
            {
                return Pass(checkId, description, $"{toolName} found at {path}.");
            }

            DoctorCheckStatus status = required ? DoctorCheckStatus.Fail : DoctorCheckStatus.Warning;
            return new DoctorCheckResult(
                checkId,
                description,
                status,
                $"{toolName} was not found in QT_DIR/bin or PATH.",
                required
                    ? $"Install Qt 6.11.0 with {toolName}, or add the Qt bin directory to PATH."
                    : $"Install Qt AOT tools or disable build.aot when {toolName} is unavailable.",
                false);
        }

        private async Task<DoctorCheckResult> CheckDotNetVersionAsync()
        {
            string? dotnet = FindExecutable("dotnet");
            if (dotnet is null)
            {
                return Fail(
                    DoctorCheckId.DotNetVersion,
                    ".NET SDK version",
                    ".NET SDK was not found on PATH.",
                    "Install the .NET SDK pinned by global.json and ensure dotnet is on PATH.",
                    autoFixable: false);
            }

            DoctorProcessResult result = await _environment.RunAsync(
                dotnet,
                ImmutableArray.Create("--version"),
                _projectDir,
                CancellationToken.None).ConfigureAwait(false);
            if (!result.Started || !result.Success)
            {
                return Fail(
                    DoctorCheckId.DotNetVersion,
                    ".NET SDK version",
                    "dotnet --version failed: " + result.CombinedOutput,
                    "Install or repair the .NET SDK pinned by global.json.",
                    autoFixable: false);
            }

            string versionText = result.Stdout.Trim();
            Version actual = ParseRequiredVersion(versionText);
            Version required = GetRequiredDotNetVersion();
            if (actual >= required)
            {
                return Pass(
                    DoctorCheckId.DotNetVersion,
                    $".NET SDK version >= {required}",
                    $"Found .NET SDK {versionText}.");
            }

            return Fail(
                DoctorCheckId.DotNetVersion,
                $".NET SDK version >= {required}",
                $"Found .NET SDK {versionText}; QmlSharp requires {required} or newer.",
                "Install the .NET SDK pinned by global.json.",
                autoFixable: false);
        }

        private async Task<DoctorCheckResult> CheckCMakeAvailableAsync()
        {
            string? cmake = FindExecutable("cmake");
            if (cmake is null)
            {
                return Fail(
                    DoctorCheckId.CMakeAvailable,
                    "CMake available",
                    "cmake was not found on PATH.",
                    "Install CMake 3.21 or newer and ensure cmake is on PATH.",
                    autoFixable: false);
            }

            DoctorProcessResult result = await _environment.RunAsync(
                cmake,
                ImmutableArray.Create("--version"),
                _projectDir,
                CancellationToken.None).ConfigureAwait(false);
            if (result.Started && result.Success)
            {
                return Pass(DoctorCheckId.CMakeAvailable, "CMake available", FirstLine(result.CombinedOutput));
            }

            string detail = result.Started
                ? "cmake --version failed: " + result.CombinedOutput
                : "cmake executable could not be started.";
            return Fail(
                DoctorCheckId.CMakeAvailable,
                "CMake available",
                detail,
                "Install CMake 3.21 or newer and ensure cmake is on PATH.",
                autoFixable: false);
        }

        private async Task<DoctorCheckResult> CheckCMakeVersionAsync()
        {
            string? cmake = FindExecutable("cmake");
            if (cmake is null)
            {
                return Fail(
                    DoctorCheckId.CMakeVersion,
                    $"CMake version >= {RequiredCMakeVersionText}",
                    "Cannot check CMake version because cmake was not found.",
                    "Install CMake 3.21 or newer and ensure cmake is on PATH.",
                    autoFixable: false);
            }

            DoctorProcessResult result = await _environment.RunAsync(
                cmake,
                ImmutableArray.Create("--version"),
                _projectDir,
                CancellationToken.None).ConfigureAwait(false);
            if (!result.Started || !result.Success)
            {
                return Fail(
                    DoctorCheckId.CMakeVersion,
                    $"CMake version >= {RequiredCMakeVersionText}",
                    "cmake --version failed: " + result.CombinedOutput,
                    "Install CMake 3.21 or newer and ensure cmake is on PATH.",
                    autoFixable: false);
            }

            string firstLine = FirstLine(result.CombinedOutput);
            Version actual = ParseFirstVersion(firstLine);
            Version required = ParseRequiredVersion(RequiredCMakeVersionText);
            if (actual >= required)
            {
                return Pass(
                    DoctorCheckId.CMakeVersion,
                    $"CMake version >= {RequiredCMakeVersionText}",
                    firstLine);
            }

            return Fail(
                DoctorCheckId.CMakeVersion,
                $"CMake version >= {RequiredCMakeVersionText}",
                $"{firstLine}; QmlSharp requires CMake {RequiredCMakeVersionText} or newer.",
                "Install CMake 3.21 or newer and ensure cmake is on PATH.",
                autoFixable: false);
        }

        private async Task<DoctorCheckResult> CheckMsvcAvailableAsync()
        {
            if (_environment.CurrentPlatform is not PlatformTarget.WindowsX64)
            {
                return Skipped(
                    DoctorCheckId.MsvcAvailable,
                    "MSVC compiler available",
                    "MSVC is only required on Windows.");
            }

            string? cl = FindExecutable("cl");
            if (cl is null)
            {
                return Fail(
                    DoctorCheckId.MsvcAvailable,
                    "MSVC compiler available",
                    "cl.exe was not found on PATH.",
                    "Run from a Visual Studio developer shell or install Visual Studio Build Tools.",
                    autoFixable: false);
            }

            DoctorProcessResult result = await _environment.RunAsync(
                cl,
                ImmutableArray<string>.Empty,
                _projectDir,
                CancellationToken.None).ConfigureAwait(false);
            return result.Started
                ? Pass(DoctorCheckId.MsvcAvailable, "MSVC compiler available", FirstLine(result.CombinedOutput))
                : Fail(
                    DoctorCheckId.MsvcAvailable,
                    "MSVC compiler available",
                    "cl.exe could not be started.",
                    "Run from a Visual Studio developer shell or install Visual Studio Build Tools.",
                    autoFixable: false);
        }

        private async Task<DoctorCheckResult> CheckClangAvailableAsync()
        {
            string? clang = _environment.CurrentPlatform is PlatformTarget.WindowsX64
                ? FindExecutable("clang-cl")
                : FindExecutable("clang++") ?? FindExecutable("clang");
            if (clang is null)
            {
                string detail = _environment.CurrentPlatform is PlatformTarget.WindowsX64
                    ? "clang-cl was not found on PATH."
                    : "clang++ or clang was not found on PATH.";
                return Fail(
                    DoctorCheckId.ClangAvailable,
                    "Clang compiler available",
                    detail,
                    "Install clang and ensure it is on PATH.",
                    autoFixable: false);
            }

            DoctorProcessResult result = await _environment.RunAsync(
                clang,
                ImmutableArray.Create("--version"),
                _projectDir,
                CancellationToken.None).ConfigureAwait(false);
            return result.Started
                ? Pass(DoctorCheckId.ClangAvailable, "Clang compiler available", FirstLine(result.CombinedOutput))
                : Fail(
                    DoctorCheckId.ClangAvailable,
                    "Clang compiler available",
                    "clang could not be started.",
                    "Install clang and ensure it is on PATH.",
                    autoFixable: false);
        }

        private async Task<DoctorCheckResult> CheckNinjaAvailableAsync()
        {
            string? ninja = FindExecutable("ninja");
            if (ninja is null)
            {
                return new DoctorCheckResult(
                    DoctorCheckId.NinjaAvailable,
                    "Ninja build system available",
                    DoctorCheckStatus.Warning,
                    "ninja was not found on PATH.",
                    "Install Ninja for faster native builds, or use another CMake generator.",
                    false);
            }

            DoctorProcessResult result = await _environment.RunAsync(
                ninja,
                ImmutableArray.Create("--version"),
                _projectDir,
                CancellationToken.None).ConfigureAwait(false);
            return result.Started
                ? Pass(DoctorCheckId.NinjaAvailable, "Ninja build system available", FirstLine(result.CombinedOutput))
                : new DoctorCheckResult(
                    DoctorCheckId.NinjaAvailable,
                    "Ninja build system available",
                    DoctorCheckStatus.Warning,
                    "ninja was found but could not be started.",
                    "Install Ninja for faster native builds, or use another CMake generator.",
                    false);
        }

        private DoctorCheckResult CheckConfigValid(DoctorState state)
        {
            if (state.ConfigException is not null)
            {
                return Fail(
                    DoctorCheckId.ConfigValid,
                    "qmlsharp.json valid",
                    state.ConfigException.Message,
                    "Fix qmlsharp.json and rerun qmlsharp doctor.",
                    autoFixable: false);
            }

            if (state.Config is null)
            {
                return Fail(
                    DoctorCheckId.ConfigValid,
                    "qmlsharp.json valid",
                    "qmlsharp.json could not be loaded.",
                    "Create qmlsharp.json with `dotnet qmlsharp init` or run doctor from a QmlSharp project.",
                    autoFixable: false);
            }

            ImmutableArray<ConfigDiagnostic> diagnostics = _configLoader.Validate(state.Config);
            ImmutableArray<ConfigDiagnostic> errors = diagnostics
                .Where(static diagnostic => diagnostic.Severity == ConfigDiagnosticSeverity.Error)
                .ToImmutableArray();
            if (errors.IsDefaultOrEmpty)
            {
                return Pass(DoctorCheckId.ConfigValid, "qmlsharp.json valid", "Configuration is valid.");
            }

            string detail = string.Join("; ", errors.Select(static diagnostic =>
                $"{diagnostic.Field}: {diagnostic.Message}"));
            return Fail(
                DoctorCheckId.ConfigValid,
                "qmlsharp.json valid",
                detail,
                "Fix qmlsharp.json and rerun qmlsharp doctor.",
                autoFixable: false);
        }

        private DoctorCheckResult CheckNativeLibExists(DoctorState state)
        {
            string outputDir = state.Config?.OutDir ?? Path.Join(_projectDir, "dist");
            string nativePath = Path.Join(outputDir, "native", GetNativeLibraryFileName(_environment.CurrentPlatform));
            if (_environment.FileExists(nativePath))
            {
                return Pass(
                    DoctorCheckId.NativeLibExists,
                    "Native library exists",
                    $"Native library found at {nativePath}.");
            }

            return Fail(
                DoctorCheckId.NativeLibExists,
                "Native library exists",
                $"Expected native library was not found at {nativePath}.",
                "Run `dotnet qmlsharp build` to produce the native library.",
                autoFixable: false);
        }

        private DoctorCheckResult CheckNuGetResolved()
        {
            string? projectFile = FindProjectFile();
            if (projectFile is null)
            {
                return Skipped(
                    DoctorCheckId.NuGetResolved,
                    "NuGet packages restored",
                    "No .csproj file was found in the project directory.");
            }

            string assetsPath = Path.Join(Path.GetDirectoryName(projectFile)!, "obj", "project.assets.json");
            if (_environment.FileExists(assetsPath))
            {
                return Pass(
                    DoctorCheckId.NuGetResolved,
                    "NuGet packages restored",
                    $"NuGet assets file found at {assetsPath}.");
            }

            return Fail(
                DoctorCheckId.NuGetResolved,
                "NuGet packages restored",
                $"NuGet assets file was not found at {assetsPath}.",
                "Run `dotnet restore` for the project.",
                autoFixable: true);
        }

        private async Task<DoctorFixResult> RestoreNuGetAsync(CancellationToken cancellationToken)
        {
            string? projectFile = FindProjectFile();
            if (projectFile is null)
            {
                return new DoctorFixResult(
                    DoctorCheckId.NuGetResolved,
                    false,
                    "No .csproj file was found for dotnet restore.");
            }

            string? dotnet = FindExecutable("dotnet");
            if (dotnet is null)
            {
                return new DoctorFixResult(
                    DoctorCheckId.NuGetResolved,
                    false,
                    "dotnet was not found on PATH.");
            }

            DoctorProcessResult result = await _environment.RunAsync(
                dotnet,
                ImmutableArray.Create("restore", projectFile),
                Path.GetDirectoryName(projectFile),
                cancellationToken).ConfigureAwait(false);
            string assetsPath = Path.Join(Path.GetDirectoryName(projectFile)!, "obj", "project.assets.json");
            bool fixedCheck = result.Success && _environment.FileExists(assetsPath);
            return new DoctorFixResult(
                DoctorCheckId.NuGetResolved,
                fixedCheck,
                fixedCheck
                    ? "NuGet packages restored."
                    : "dotnet restore did not produce project.assets.json: " + result.CombinedOutput);
        }

        private ImmutableArray<string> GetQtToolDirectories(DoctorState state)
        {
            if (state.QtInstallation is null)
            {
                return ImmutableArray<string>.Empty;
            }

            return ImmutableArray.Create(
                state.QtInstallation.BinDir,
                Path.Join(state.QtInstallation.RootDir, "libexec"),
                Path.Join(state.QtInstallation.RootDir, "lib", "qt6", "bin"),
                Path.Join(state.QtInstallation.RootDir, "lib", "qt6", "libexec"),
                Path.Join(state.QtInstallation.RootDir, "lib", "qt", "bin"),
                Path.Join(state.QtInstallation.RootDir, "lib", "qt", "libexec"));
        }

        private string? FindExecutable(string executableName)
        {
            return FindExecutable(executableName, ImmutableArray<string>.Empty);
        }

        private string? FindExecutable(string executableName, ImmutableArray<string> additionalDirectories)
        {
            return EnumerateExecutableCandidates(executableName, additionalDirectories)
                .FirstOrDefault(candidatePath => _environment.FileExists(candidatePath));
        }

        private IEnumerable<string> EnumerateExecutableCandidates(
            string executableName,
            ImmutableArray<string> additionalDirectories)
        {
            if (Path.IsPathRooted(executableName))
            {
                foreach (string candidate in AddPlatformExecutableExtensions(executableName))
                {
                    yield return candidate;
                }

                yield break;
            }

            foreach (string directory in additionalDirectories.Where(static directory => !string.IsNullOrWhiteSpace(directory)))
            {
                foreach (string candidate in AddPlatformExecutableExtensions(Path.Join(directory, executableName)))
                {
                    yield return candidate;
                }
            }

            string pathValue = _environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string directory in pathValue.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                foreach (string candidate in AddPlatformExecutableExtensions(Path.Join(directory, executableName)))
                {
                    yield return candidate;
                }
            }
        }

        private IEnumerable<string> AddPlatformExecutableExtensions(string path)
        {
            if (_environment.CurrentPlatform is not PlatformTarget.WindowsX64 || Path.HasExtension(path))
            {
                yield return path;
                yield break;
            }

            yield return path;
            yield return path + ".exe";
            yield return path + ".cmd";
            yield return path + ".bat";
        }

        private Version GetRequiredDotNetVersion()
        {
            string? globalJsonPath = FindNearestFile(_projectDir, "global.json");
            if (globalJsonPath is null)
            {
                return ParseRequiredVersion(FallbackRequiredDotNetVersionText);
            }

            try
            {
                using Stream stream = _environment.OpenRead(globalJsonPath);
                using JsonDocument document = JsonDocument.Parse(stream);
                if (document.RootElement.TryGetProperty("sdk", out JsonElement sdk) &&
                    sdk.TryGetProperty("version", out JsonElement versionElement) &&
                    versionElement.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(versionElement.GetString()))
                {
                    return ParseRequiredVersion(versionElement.GetString()!);
                }
            }
            catch (JsonException)
            {
                return ParseRequiredVersion(FallbackRequiredDotNetVersionText);
            }
            catch (IOException)
            {
                return ParseRequiredVersion(FallbackRequiredDotNetVersionText);
            }
            catch (UnauthorizedAccessException)
            {
                return ParseRequiredVersion(FallbackRequiredDotNetVersionText);
            }

            return ParseRequiredVersion(FallbackRequiredDotNetVersionText);
        }

        private string? FindProjectFile()
        {
            ImmutableArray<string> projectFiles = _environment
                .EnumerateFiles(_projectDir, "*.csproj", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();
            return projectFiles.Length == 0 ? null : projectFiles[0];
        }

        private string? FindNearestFile(string startDirectory, string fileName)
        {
            DirectoryInfo? directory = new(startDirectory);
            while (directory is not null)
            {
                string candidate = Path.Join(directory.FullName, fileName);
                if (_environment.FileExists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return null;
        }

        private static DoctorCheckResult Pass(string checkId, string description, string detail)
        {
            return new DoctorCheckResult(checkId, description, DoctorCheckStatus.Pass, detail, null, false);
        }

        private static DoctorCheckResult Fail(
            string checkId,
            string description,
            string detail,
            string fixHint,
            bool autoFixable)
        {
            return new DoctorCheckResult(
                checkId,
                description,
                DoctorCheckStatus.Fail,
                detail,
                fixHint,
                autoFixable);
        }

        private static DoctorCheckResult Skipped(string checkId, string description, string detail)
        {
            return new DoctorCheckResult(
                checkId,
                description,
                DoctorCheckStatus.Skipped,
                detail,
                null,
                false);
        }

        private static string GetNativeLibraryFileName(PlatformTarget target)
        {
            return target switch
            {
                PlatformTarget.WindowsX64 => NativeTargetName + ".dll",
                PlatformTarget.MacOsArm64 or PlatformTarget.MacOsX64 => "lib" + NativeTargetName + ".dylib",
                PlatformTarget.LinuxX64 => "lib" + NativeTargetName + ".so",
                _ => "lib" + NativeTargetName + ".so",
            };
        }

        private static Version ToVersion(QtVersion version)
        {
            return new Version(version.Major, version.Minor, version.Patch);
        }

        private static Version ParseRequiredVersion(string value)
        {
            Version parsed = ParseFirstVersion(value);
            return parsed;
        }

        private static Version ParseFirstVersion(string value)
        {
            string text = value.Trim();
            for (int index = 0; index < text.Length; index++)
            {
                if (!char.IsDigit(text[index]))
                {
                    continue;
                }

                int end = index;
                while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.'))
                {
                    end++;
                }

                string versionText = text[index..end].Trim('.');
                if (Version.TryParse(versionText, out Version? version))
                {
                    if (version.Build < 0)
                    {
                        return new Version(version.Major, version.Minor, 0);
                    }

                    return version;
                }
            }

            return new Version(0, 0, 0);
        }

        private static string FirstLine(string value)
        {
            string[] lines = value.Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return lines.Length == 0 ? value.Trim() : lines[0];
        }

        private static string NormalizeProjectDir(string? projectDir, string currentDirectory)
        {
            string path = string.IsNullOrWhiteSpace(projectDir) ? currentDirectory : projectDir;
            return Path.GetFullPath(path);
        }

        private sealed record DoctorState(
            QmlSharpConfig? Config,
            ConfigParseException? ConfigException,
            QtInstallation? QtInstallation,
            Exception? QtException);
    }

}
