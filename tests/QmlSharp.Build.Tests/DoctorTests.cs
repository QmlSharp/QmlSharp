using System.Diagnostics;
using System.Runtime.InteropServices;
using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class DoctorTests
    {
        [Fact]
        public async Task DR01_DoctorWithAllToolsPresent_ReturnsNoFailures()
        {
            using DoctorFixture fixture = DoctorFixture.CreateHealthy();
            Doctor doctor = fixture.CreateDoctor();

            ImmutableArray<DoctorCheckResult> checks = await doctor.RunAllChecksAsync(fixture.Config);

            Assert.True(DoctorCheckId.All.SequenceEqual(checks.Select(static check => check.CheckId)));
            Assert.DoesNotContain(checks, static check => check.Status == DoctorCheckStatus.Fail);
            Assert.Contains(checks, static check => check.CheckId == DoctorCheckId.QmlFormatAvailable && check.Status == DoctorCheckStatus.Pass);
            Assert.Contains(checks, static check => check.CheckId == DoctorCheckId.NuGetResolved && check.Status == DoctorCheckStatus.Pass);
        }

        [Fact]
        public async Task DR02_DoctorWithMissingQtSdk_FailsQtInstalledWithFixHint()
        {
            using DoctorFixture fixture = DoctorFixture.CreateHealthy();
            fixture.QtToolchain.DiscoveryException = new QtInstallationNotFoundError(
                "Qt installation was not found.",
                ImmutableArray.Create("QT_DIR: not set"));
            Doctor doctor = fixture.CreateDoctor();

            DoctorCheckResult result = await doctor.RunCheckAsync(DoctorCheckId.QtInstalled);

            Assert.Equal(DoctorCheckStatus.Fail, result.Status);
            Assert.Contains("Qt", result.FixHint, StringComparison.OrdinalIgnoreCase);
            Assert.False(result.AutoFixable);
        }

        [Fact]
        public async Task DR03_DoctorWithQtBelowBaseline_FailsQtVersionWithFoundVersion()
        {
            using DoctorFixture fixture = DoctorFixture.CreateHealthy(qtVersion: new QtVersion { Major = 6, Minor = 10, Patch = 0 });
            Doctor doctor = fixture.CreateDoctor();

            DoctorCheckResult result = await doctor.RunCheckAsync(DoctorCheckId.QtVersion);

            Assert.Equal(DoctorCheckStatus.Fail, result.Status);
            Assert.Contains("6.10.0", result.Detail, StringComparison.Ordinal);
            Assert.Contains("6.11.0", result.Detail, StringComparison.Ordinal);
        }

        [Fact]
        public async Task DR04_DoctorWithMissingCMake_FailsCMakeAvailable()
        {
            using DoctorFixture fixture = DoctorFixture.CreateHealthy();
            fixture.Environment.RemoveExecutable("cmake");
            Doctor doctor = fixture.CreateDoctor();

            DoctorCheckResult result = await doctor.RunCheckAsync(DoctorCheckId.CMakeAvailable);

            Assert.Equal(DoctorCheckStatus.Fail, result.Status);
            Assert.Contains("cmake", result.Detail, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DR05_DoctorWithMissingDotNetSdk_FailsDotNetVersion()
        {
            using DoctorFixture fixture = DoctorFixture.CreateHealthy();
            fixture.Environment.RemoveExecutable("dotnet");
            Doctor doctor = fixture.CreateDoctor();

            DoctorCheckResult result = await doctor.RunCheckAsync(DoctorCheckId.DotNetVersion);

            Assert.Equal(DoctorCheckStatus.Fail, result.Status);
            Assert.Contains(".NET", result.Detail, StringComparison.Ordinal);
        }

        [Fact]
        public async Task DR06_DoctorWithInvalidConfig_FailsConfigValidWithFieldError()
        {
            using DoctorFixture fixture = DoctorFixture.CreateHealthy();
            BuildDiagnostic diagnostic = new(
                BuildDiagnosticCode.ConfigValidationError,
                BuildDiagnosticSeverity.Error,
                "module.prefix: Module prefix must not be empty.",
                BuildPhase.ConfigLoading,
                "module.prefix");
            fixture.ConfigLoader.Exception = new ConfigParseException(diagnostic);
            Doctor doctor = fixture.CreateDoctor();

            DoctorCheckResult result = await doctor.RunCheckAsync(DoctorCheckId.ConfigValid);

            Assert.Equal(DoctorCheckStatus.Fail, result.Status);
            Assert.Contains("module.prefix", result.Detail, StringComparison.Ordinal);
        }

        [Fact]
        public async Task DR07_DoctorFix_RestoresNuGetPackages()
        {
            using DoctorFixture fixture = DoctorFixture.CreateHealthy(includeNuGetAssets: false);
            Doctor doctor = fixture.CreateDoctor();
            DoctorCheckResult failed = await doctor.RunCheckAsync(DoctorCheckId.NuGetResolved);

            ImmutableArray<DoctorFixResult> fixes = await doctor.AutoFixAsync(ImmutableArray.Create(failed));

            DoctorFixResult fix = Assert.Single(fixes);
            Assert.True(fix.Fixed);
            Assert.True(File.Exists(Path.Join(fixture.ProjectDir, "obj", "project.assets.json")));
        }

        [Fact]
        public async Task DR08_DoctorReportsPlatformSpecificChecks()
        {
            using DoctorFixture windowsFixture = DoctorFixture.CreateHealthy(platform: PlatformTarget.WindowsX64);
            Doctor windowsDoctor = windowsFixture.CreateDoctor();
            DoctorCheckResult windowsMsvc = await windowsDoctor.RunCheckAsync(DoctorCheckId.MsvcAvailable);
            DoctorCheckResult windowsClang = await windowsDoctor.RunCheckAsync(DoctorCheckId.ClangAvailable);

            Assert.Equal(DoctorCheckStatus.Pass, windowsMsvc.Status);
            Assert.Equal(DoctorCheckStatus.Skipped, windowsClang.Status);

            using DoctorFixture linuxFixture = DoctorFixture.CreateHealthy(platform: PlatformTarget.LinuxX64);
            Doctor linuxDoctor = linuxFixture.CreateDoctor();
            DoctorCheckResult linuxMsvc = await linuxDoctor.RunCheckAsync(DoctorCheckId.MsvcAvailable);
            DoctorCheckResult linuxClang = await linuxDoctor.RunCheckAsync(DoctorCheckId.ClangAvailable);

            Assert.Equal(DoctorCheckStatus.Skipped, linuxMsvc.Status);
            Assert.Equal(DoctorCheckStatus.Pass, linuxClang.Status);
        }

        [Fact]
        public async Task Doctor_RunCheckById_ReturnsOnlyRequestedCheck()
        {
            using DoctorFixture fixture = DoctorFixture.CreateHealthy();
            Doctor doctor = fixture.CreateDoctor();

            DoctorCheckResult result = await doctor.RunCheckAsync(DoctorCheckId.QmlLintAvailable);

            Assert.Equal(DoctorCheckId.QmlLintAvailable, result.CheckId);
            Assert.Equal(DoctorCheckStatus.Pass, result.Status);
        }

        [Fact]
        public async Task Doctor_OptionalToolsReportWarningsWhenMissing()
        {
            using DoctorFixture fixture = DoctorFixture.CreateHealthy();
            fixture.Environment.RemoveExecutable("qmlcachegen");
            fixture.Environment.RemoveExecutable("ninja");
            Doctor doctor = fixture.CreateDoctor();

            DoctorCheckResult qmlcachegen = await doctor.RunCheckAsync(DoctorCheckId.QmlCachegenAvailable);
            DoctorCheckResult ninja = await doctor.RunCheckAsync(DoctorCheckId.NinjaAvailable);

            Assert.Equal(DoctorCheckStatus.Warning, qmlcachegen.Status);
            Assert.Equal(DoctorCheckStatus.Warning, ninja.Status);
        }

        [Fact]
        [Trait("Category", BuildTestCategories.Integration)]
        [Trait("Category", BuildTestCategories.RequiresQt)]
        public async Task Doctor_RealQtTools_UseQtDir()
        {
            string qtDir = Environment.GetEnvironmentVariable("QT_DIR") ??
                throw new InvalidOperationException("QT_DIR must be set for RequiresQt tests.");
            Doctor doctor = new(BuildTestFixtures.FindRepositoryRoot());

            ImmutableArray<DoctorCheckResult> checks = await doctor.RunAllChecksAsync(new QmlSharpConfig
            {
                Entry = "./src/Program.cs",
                OutDir = Path.Join(BuildTestFixtures.FindRepositoryRoot(), "dist"),
                Qt = new QtConfig
                {
                    Dir = qtDir,
                },
                Module = new ModuleConfig
                {
                    Prefix = "QmlSharp.MyApp",
                },
            });

            Assert.Contains(checks, static check => check.CheckId == DoctorCheckId.QtInstalled && check.Status == DoctorCheckStatus.Pass);
            Assert.Contains(checks, static check => check.CheckId == DoctorCheckId.QmlFormatAvailable && check.Status == DoctorCheckStatus.Pass);
            Assert.Contains(checks, static check => check.CheckId == DoctorCheckId.QmlLintAvailable && check.Status == DoctorCheckStatus.Pass);
        }

        [Fact]
        [Trait("Category", BuildTestCategories.Integration)]
        [Trait("Category", BuildTestCategories.RequiresCMake)]
        public async Task Doctor_RealCMake_ReportsAvailableVersion()
        {
            Doctor doctor = new(BuildTestFixtures.FindRepositoryRoot());

            DoctorCheckResult available = await doctor.RunCheckAsync(DoctorCheckId.CMakeAvailable);
            DoctorCheckResult version = await doctor.RunCheckAsync(DoctorCheckId.CMakeVersion);

            Assert.Equal(DoctorCheckStatus.Pass, available.Status);
            Assert.Equal(DoctorCheckStatus.Pass, version.Status);
        }

        [Fact]
        [Trait("Category", BuildTestCategories.Integration)]
        [Trait("Category", BuildTestCategories.RequiresDotNet)]
        public async Task Doctor_RealDotNet_ReportsRepositorySdk()
        {
            Doctor doctor = new(BuildTestFixtures.FindRepositoryRoot());

            DoctorCheckResult result = await doctor.RunCheckAsync(DoctorCheckId.DotNetVersion);

            Assert.Equal(DoctorCheckStatus.Pass, result.Status);
            Assert.Contains(".NET SDK", result.Description, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", BuildTestCategories.Performance)]
        public async Task PF08_DoctorAllChecksWithMockedTools_CompletesUnderThreeSeconds()
        {
            using DoctorFixture fixture = DoctorFixture.CreateHealthy();
            Doctor doctor = fixture.CreateDoctor();
            Stopwatch stopwatch = Stopwatch.StartNew();

            ImmutableArray<DoctorCheckResult> checks = await doctor.RunAllChecksAsync(fixture.Config);

            stopwatch.Stop();
            Assert.DoesNotContain(checks, static check => check.Status == DoctorCheckStatus.Fail);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Doctor took {stopwatch.Elapsed}.");
        }

        private sealed class DoctorFixture : IDisposable
        {
            private readonly TempDirectory _tempDirectory;

            private DoctorFixture(
                TempDirectory tempDirectory,
                QmlSharpConfig config,
                FakeQtToolchain qtToolchain,
                FakeConfigLoader configLoader,
                FakeDoctorEnvironment environment)
            {
                _tempDirectory = tempDirectory;
                Config = config;
                QtToolchain = qtToolchain;
                ConfigLoader = configLoader;
                Environment = environment;
            }

            public string ProjectDir => _tempDirectory.Path;

            public QmlSharpConfig Config { get; }

            public FakeQtToolchain QtToolchain { get; }

            public FakeConfigLoader ConfigLoader { get; }

            public FakeDoctorEnvironment Environment { get; }

            public static DoctorFixture CreateHealthy(
                PlatformTarget? platform = null,
                QtVersion? qtVersion = null,
                bool includeNuGetAssets = true)
            {
                PlatformTarget target = platform ?? GetCurrentPlatform();
                TempDirectory project = BuildTestFixtures.CreateFixtureProject("doctor");
                string fakeBin = Path.Join(project.Path, "bin");
                string qtDir = Path.Join(project.Path, "Qt", "6.11.0", "mock");
                string qtBin = Path.Join(qtDir, "bin");
                string qtQml = Path.Join(qtDir, "qml");
                string qtLib = Path.Join(qtDir, "lib");
                _ = Directory.CreateDirectory(fakeBin);
                _ = Directory.CreateDirectory(qtBin);
                _ = Directory.CreateDirectory(qtQml);
                _ = Directory.CreateDirectory(qtLib);
                CreateExecutable(qtBin, "qmlformat", target);
                CreateExecutable(qtBin, "qmllint", target);
                CreateExecutable(qtBin, "qmlcachegen", target);
                CreateExecutable(fakeBin, "dotnet", target);
                CreateExecutable(fakeBin, "cmake", target);
                CreateExecutable(fakeBin, "ninja", target);
                CreateExecutable(fakeBin, "cl", target);
                CreateExecutable(fakeBin, "clang++", target);
                WriteProject(project.Path, includeNuGetAssets);

                string outputDir = Path.Join(project.Path, "dist");
                string nativeDir = Path.Join(outputDir, "native");
                _ = Directory.CreateDirectory(nativeDir);
                File.WriteAllText(Path.Join(nativeDir, GetNativeLibraryFileName(target)), "native");

                QmlSharpConfig config = BuildTestFixtures.CreateDefaultConfig() with
                {
                    OutDir = outputDir,
                    Qt = new QtConfig
                    {
                        Dir = qtDir,
                        Modules = ImmutableArray.Create("QtQuick"),
                    },
                };
                FakeDoctorEnvironment environment = new(project.Path, target);
                environment.SetEnvironmentVariable("PATH", fakeBin);
                environment.SetEnvironmentVariable("QT_DIR", qtDir);
                environment.SetProcessResult("dotnet", new DoctorProcessResult(true, 0, "10.0.203\n", string.Empty));
                environment.SetProcessResult("cmake", new DoctorProcessResult(true, 0, "cmake version 3.31.0\n", string.Empty));
                environment.SetProcessResult("ninja", new DoctorProcessResult(true, 0, "1.11.1\n", string.Empty));
                environment.SetProcessResult("cl", new DoctorProcessResult(true, 0, "Microsoft (R) C/C++ Optimizing Compiler Version 19.40\n", string.Empty));
                environment.SetProcessResult("clang++", new DoctorProcessResult(true, 0, "clang version 18.0.0\n", string.Empty));
                environment.RestoreHandler = projectFile =>
                {
                    string objDir = Path.Join(Path.GetDirectoryName(projectFile)!, "obj");
                    _ = Directory.CreateDirectory(objDir);
                    File.WriteAllText(Path.Join(objDir, "project.assets.json"), "{}");
                };

                QtInstallation installation = new()
                {
                    RootDir = qtDir,
                    BinDir = qtBin,
                    QmlDir = qtQml,
                    LibDir = qtLib,
                    Version = qtVersion ?? new QtVersion { Major = 6, Minor = 11, Patch = 0 },
                    Platform = "mock",
                };
                FakeQtToolchain qtToolchain = new(installation);
                FakeConfigLoader configLoader = new(config);
                return new DoctorFixture(project, config, qtToolchain, configLoader, environment);
            }

            public Doctor CreateDoctor()
            {
                return new Doctor(ProjectDir, QtToolchain, ConfigLoader, Environment);
            }

            public void Dispose()
            {
                _tempDirectory.Dispose();
            }

            private static void WriteProject(string projectDir, bool includeNuGetAssets)
            {
                File.WriteAllText(
                    Path.Join(projectDir, "DoctorFixture.csproj"),
                    """
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>net10.0</TargetFramework>
                      </PropertyGroup>
                    </Project>
                    """);
                if (includeNuGetAssets)
                {
                    string objDir = Path.Join(projectDir, "obj");
                    _ = Directory.CreateDirectory(objDir);
                    File.WriteAllText(Path.Join(objDir, "project.assets.json"), "{}");
                }
            }

            private static void CreateExecutable(string directory, string name, PlatformTarget target)
            {
                string fileName = target is PlatformTarget.WindowsX64 ? name + ".exe" : name;
                File.WriteAllText(Path.Join(directory, fileName), "mock");
            }

            private static PlatformTarget GetCurrentPlatform()
            {
                if (OperatingSystem.IsWindows())
                {
                    return PlatformTarget.WindowsX64;
                }

                if (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    return PlatformTarget.MacOsArm64;
                }

                if (OperatingSystem.IsMacOS())
                {
                    return PlatformTarget.MacOsX64;
                }

                return PlatformTarget.LinuxX64;
            }

            private static string GetNativeLibraryFileName(PlatformTarget target)
            {
                return target switch
                {
                    PlatformTarget.WindowsX64 => "qmlsharp_native.dll",
                    PlatformTarget.MacOsArm64 or PlatformTarget.MacOsX64 => "libqmlsharp_native.dylib",
                    _ => "libqmlsharp_native.so",
                };
            }
        }

        private sealed class FakeQtToolchain : IQtToolchain
        {
            private readonly QtInstallation _installation;

            public FakeQtToolchain(QtInstallation installation)
            {
                _installation = installation;
            }

            public Exception? DiscoveryException { get; set; }

            public QtInstallation? Installation { get; private set; }

            public Task<QtInstallation> DiscoverAsync(QtToolchainConfig? config = null, CancellationToken ct = default)
            {
                if (DiscoveryException is not null)
                {
                    throw DiscoveryException;
                }

                Installation = _installation;
                return Task.FromResult(_installation);
            }

            public Task<ToolAvailability> CheckToolsAsync(CancellationToken ct = default)
            {
                throw new NotSupportedException("Doctor tests resolve Qt tools through the fake filesystem.");
            }

            public Task<ToolInfo> GetToolInfoAsync(string toolName, CancellationToken ct = default)
            {
                throw new NotSupportedException("Doctor tests resolve Qt tools through the fake filesystem.");
            }
        }

        private sealed class FakeConfigLoader : IConfigLoader
        {
            private readonly QmlSharpConfig _config;

            public FakeConfigLoader(QmlSharpConfig config)
            {
                _config = config;
            }

            public ConfigParseException? Exception { get; set; }

            public QmlSharpConfig Load(string projectDir)
            {
                if (Exception is not null)
                {
                    throw Exception;
                }

                return _config;
            }

            public ImmutableArray<ConfigDiagnostic> Validate(QmlSharpConfig config)
            {
                return ImmutableArray<ConfigDiagnostic>.Empty;
            }

            public QmlSharpConfig GetDefaults()
            {
                return _config;
            }
        }

        private sealed class FakeDoctorEnvironment : IDoctorEnvironment
        {
            private readonly Dictionary<string, string> _environment = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, DoctorProcessResult> _processResults = new(StringComparer.OrdinalIgnoreCase);

            public FakeDoctorEnvironment(string currentDirectory, PlatformTarget platform)
            {
                CurrentDirectory = currentDirectory;
                CurrentPlatform = platform;
            }

            public string CurrentDirectory { get; }

            public PlatformTarget CurrentPlatform { get; }

            public Action<string>? RestoreHandler { get; set; }

            public string? GetEnvironmentVariable(string name)
            {
                return _environment.TryGetValue(name, out string? value) ? value : null;
            }

            public bool FileExists(string path)
            {
                return File.Exists(path);
            }

            public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
            {
                return Directory.Exists(path)
                    ? Directory.EnumerateFiles(path, searchPattern, searchOption)
                    : [];
            }

            public Stream OpenRead(string path)
            {
                return File.OpenRead(path);
            }

            public Task<DoctorProcessResult> RunAsync(
                string executablePath,
                ImmutableArray<string> arguments,
                string? workingDirectory,
                CancellationToken cancellationToken)
            {
                string name = Path.GetFileNameWithoutExtension(executablePath);
                if (string.Equals(name, "dotnet", StringComparison.OrdinalIgnoreCase) &&
                    arguments.Length >= 2 &&
                    string.Equals(arguments[0], "restore", StringComparison.Ordinal))
                {
                    RestoreHandler?.Invoke(arguments[1]);
                    return Task.FromResult(new DoctorProcessResult(true, 0, "Restored\n", string.Empty));
                }

                if (_processResults.TryGetValue(name, out DoctorProcessResult? result))
                {
                    return Task.FromResult(result);
                }

                return Task.FromResult(new DoctorProcessResult(false, -1, string.Empty, "not found"));
            }

            public void SetEnvironmentVariable(string name, string value)
            {
                _environment[name] = value;
            }

            public void SetProcessResult(string name, DoctorProcessResult result)
            {
                _processResults[name] = result;
            }

            public void RemoveExecutable(string name)
            {
                string? path = FindExecutableFile(name);
                if (path is not null)
                {
                    File.Delete(path);
                }

                _ = _processResults.Remove(name);
            }

            private string? FindExecutableFile(string name)
            {
                string pathValue = GetEnvironmentVariable("PATH") ?? string.Empty;
                foreach (string directory in pathValue.Split(
                    Path.PathSeparator,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    string candidate = Path.Join(directory, CurrentPlatform is PlatformTarget.WindowsX64 ? name + ".exe" : name);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                string qtDir = GetEnvironmentVariable("QT_DIR") ?? string.Empty;
                string qtCandidate = Path.Join(qtDir, "bin", CurrentPlatform is PlatformTarget.WindowsX64 ? name + ".exe" : name);
                return File.Exists(qtCandidate) ? qtCandidate : null;
            }
        }
    }
}
