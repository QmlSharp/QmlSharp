using System.Reflection;
using QmlSharp.Build.Tests.Infrastructure;
using BuildQmlVersion = QmlSharp.Build.QmlVersion;

namespace QmlSharp.Build.Tests
{
    public sealed class ConfigLoaderTests
    {
        [Fact]
        public void CL01_LoadValidQmlsharpJsonWithAllFields_PopulatesConfig()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cl01-config");
            string qtDir = CreateDirectory(project.Path, "qt");
            string configPath = WriteConfig(
                project.Path,
                $$"""
                {
                  "entry": "./src/App.cs",
                  "outDir": "./build-output",
                  "qt": {
                    "dir": "{{ToJsonPath(qtDir)}}",
                    "modules": ["QtQuick", "QtQuick.Controls", "QtQuick.Layouts"]
                  },
                  "build": {
                    "aot": true,
                    "lint": false,
                    "format": false,
                    "sourceMaps": false,
                    "incremental": false,
                    "mode": "production"
                  },
                  "native": {
                    "prebuilt": true,
                    "cmakePreset": "windows-ci"
                  },
                  "dev": {
                    "hotReload": false,
                    "watchPaths": ["./src", "./ui"],
                    "debounceMs": 50
                  },
                  "module": {
                    "prefix": "Com.Example.App",
                    "version": { "major": 2, "minor": 3 }
                  },
                  "name": "ExampleApp",
                  "version": "1.2.3"
                }
                """);
            MockQtToolchain toolchain = new(CreateQtInstallation(qtDir));
            ConfigLoader loader = new(toolchain);

            QmlSharpConfig config = loader.Load(project.Path);

            Assert.Equal(Path.Join(project.Path, "src", "App.cs"), config.Entry);
            Assert.Equal(Path.Join(project.Path, "build-output"), config.OutDir);
            Assert.Equal(qtDir, config.Qt.Dir);
            Assert.True(config.Qt.Modules.SequenceEqual(ImmutableArray.Create("QtQuick", "QtQuick.Controls", "QtQuick.Layouts")));
            Assert.True(config.Build.Aot);
            Assert.False(config.Build.Lint);
            Assert.False(config.Build.Format);
            Assert.False(config.Build.SourceMaps);
            Assert.False(config.Build.Incremental);
            Assert.Equal("production", config.Build.Mode);
            Assert.True(config.Native.Prebuilt);
            Assert.Equal("windows-ci", config.Native.CmakePreset);
            Assert.False(config.Dev.HotReload);
            Assert.True(config.Dev.WatchPaths.SequenceEqual(ImmutableArray.Create(Path.Join(project.Path, "src"), Path.Join(project.Path, "ui"))));
            Assert.Equal(50, config.Dev.DebounceMs);
            Assert.Equal("Com.Example.App", config.Module.Prefix);
            Assert.Equal(new BuildQmlVersion(2, 3), config.Module.Version);
            Assert.Equal("ExampleApp", config.Name);
            Assert.Equal("1.2.3", config.Version);
            Assert.Equal(0, toolchain.DiscoverCallCount);
            Assert.EndsWith("qmlsharp.json", configPath, StringComparison.Ordinal);
        }

        [Fact]
        public void CL02_LoadMinimalQmlsharpJson_AppliesDefaults()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cl02-config");
            string qtDir = CreateDirectory(project.Path, "qt");
            _ = WriteConfig(
                project.Path,
                $$"""
                {
                  "qt": { "dir": "{{ToJsonPath(qtDir)}}" },
                  "module": { "prefix": "QmlSharp.Minimal" }
                }
                """);
            ConfigLoader loader = new(new MockQtToolchain(CreateQtInstallation(qtDir)));

            QmlSharpConfig config = loader.Load(project.Path);

            Assert.Null(config.Entry);
            Assert.Equal(Path.Join(project.Path, "dist"), config.OutDir);
            Assert.True(config.Qt.Modules.SequenceEqual(ImmutableArray.Create("QtQuick", "QtQuick.Controls")));
            Assert.False(config.Build.Aot);
            Assert.True(config.Build.Lint);
            Assert.True(config.Build.Format);
            Assert.True(config.Build.SourceMaps);
            Assert.True(config.Build.Incremental);
            Assert.Equal("development", config.Build.Mode);
            Assert.False(config.Native.Prebuilt);
            Assert.Equal("default", config.Native.CmakePreset);
            Assert.True(config.Dev.HotReload);
            Assert.True(config.Dev.WatchPaths.SequenceEqual(ImmutableArray.Create(Path.Join(project.Path, "src"))));
            Assert.Equal(200, config.Dev.DebounceMs);
            Assert.Equal(new BuildQmlVersion(1, 0), config.Module.Version);
            Assert.Equal("Minimal", config.Name);
            Assert.Equal("0.0.0", config.Version);
        }

        [Fact]
        public void CL03_LoadMissingEntry_DoesNotApplyCommandContextRules()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cl03-config");
            string qtDir = CreateDirectory(project.Path, "qt");
            _ = WriteConfig(
                project.Path,
                $$"""
                {
                  "qt": { "dir": "{{ToJsonPath(qtDir)}}" },
                  "module": { "prefix": "QmlSharp.Library" }
                }
                """);
            ConfigLoader loader = new(new MockQtToolchain(CreateQtInstallation(qtDir)));

            QmlSharpConfig config = loader.Load(project.Path);
            ImmutableArray<ConfigDiagnostic> diagnostics = loader.Validate(config);

            Assert.Null(config.Entry);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CL04_LoadMissingQtSection_ReportsConfigValidationDiagnostic()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cl04-config");
            _ = WriteConfig(
                project.Path,
                """
                {
                  "module": { "prefix": "QmlSharp.MissingQt" }
                }
                """);
            ConfigLoader loader = new(new MockQtToolchain(CreateMissingQtException()));

            ConfigParseException exception = Assert.Throws<ConfigParseException>(() => loader.Load(project.Path));

            Assert.Equal(BuildDiagnosticCode.ConfigValidationError, exception.Code);
            Assert.Equal("qt", exception.Diagnostic.FilePath);
        }

        [Fact]
        public void CL05_LoadMissingModuleSection_ReportsConfigValidationDiagnostic()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cl05-config");
            string qtDir = CreateDirectory(project.Path, "qt");
            _ = WriteConfig(
                project.Path,
                $$"""
                {
                  "qt": { "dir": "{{ToJsonPath(qtDir)}}" }
                }
                """);
            ConfigLoader loader = new(new MockQtToolchain(CreateQtInstallation(qtDir)));

            ConfigParseException exception = Assert.Throws<ConfigParseException>(() => loader.Load(project.Path));

            Assert.Equal(BuildDiagnosticCode.ConfigValidationError, exception.Code);
            Assert.Equal("module", exception.Diagnostic.FilePath);
        }

        [Fact]
        public void CL06_LoadNonexistentFile_ReportsConfigNotFound()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cl06-config");
            ConfigLoader loader = new(new MockQtToolchain(CreateMissingQtException()));

            ConfigParseException exception = Assert.Throws<ConfigParseException>(() => loader.Load(project.Path));

            Assert.Equal(BuildDiagnosticCode.ConfigNotFound, exception.Code);
            Assert.Contains("qmlsharp.json", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void CL07_LoadMalformedJson_ReportsConfigParseError()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cl07-config");
            _ = WriteConfig(project.Path, "{ \"qt\": { ");
            ConfigLoader loader = new(new MockQtToolchain(CreateMissingQtException()));

            ConfigParseException exception = Assert.Throws<ConfigParseException>(() => loader.Load(project.Path));

            Assert.Equal(BuildDiagnosticCode.ConfigParseError, exception.Code);
        }

        [Fact]
        public void CV01_ValidateValidConfig_ReturnsNoDiagnostics()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cv01-config");
            string qtDir = CreateDirectory(project.Path, "qt");
            ConfigLoader loader = new(new MockQtToolchain(CreateQtInstallation(qtDir)));

            ImmutableArray<ConfigDiagnostic> diagnostics = loader.Validate(CreateConfig(qtDir));

            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CV02_ValidateInvalidQtDir_ReportsQtDirNotFound()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cv02-config");
            ConfigLoader loader = new(new MockQtToolchain(CreateMissingQtException()));
            string missingQtDir = Path.Join(project.Path, "missing-qt");

            ImmutableArray<ConfigDiagnostic> diagnostics = loader.Validate(CreateConfig(missingQtDir));

            ConfigDiagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Equal(BuildDiagnosticCode.QtDirNotFound, diagnostic.Code);
            Assert.Equal("qt.dir", diagnostic.Field);
        }

        [Fact]
        public void CV03_ValidateInvalidBuildMode_ReportsFieldDiagnostic()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cv03-config");
            string qtDir = CreateDirectory(project.Path, "qt");
            ConfigLoader loader = new(new MockQtToolchain(CreateQtInstallation(qtDir)));
            QmlSharpConfig config = CreateConfig(qtDir) with
            {
                Build = new BuildConfig { Mode = "staging" },
            };

            ConfigDiagnostic diagnostic = Assert.Single(loader.Validate(config));

            Assert.Equal(BuildDiagnosticCode.ConfigValidationError, diagnostic.Code);
            Assert.Equal("build.mode", diagnostic.Field);
        }

        [Fact]
        public void CV04_ValidateNegativeDebounce_ReportsFieldDiagnostic()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cv04-config");
            string qtDir = CreateDirectory(project.Path, "qt");
            ConfigLoader loader = new(new MockQtToolchain(CreateQtInstallation(qtDir)));
            QmlSharpConfig config = CreateConfig(qtDir) with
            {
                Dev = new DevConfig { DebounceMs = -1 },
            };

            ConfigDiagnostic diagnostic = Assert.Single(loader.Validate(config));

            Assert.Equal(BuildDiagnosticCode.ConfigValidationError, diagnostic.Code);
            Assert.Equal("dev.debounceMs", diagnostic.Field);
        }

        [Fact]
        public void CV05_ValidateEmptyModulePrefix_ReportsFieldDiagnostic()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cv05-config");
            string qtDir = CreateDirectory(project.Path, "qt");
            ConfigLoader loader = new(new MockQtToolchain(CreateQtInstallation(qtDir)));
            QmlSharpConfig config = CreateConfig(qtDir) with
            {
                Module = new ModuleConfig
                {
                    Prefix = " ",
                },
            };

            ConfigDiagnostic diagnostic = Assert.Single(loader.Validate(config));

            Assert.Equal(BuildDiagnosticCode.ConfigValidationError, diagnostic.Code);
            Assert.Equal("module.prefix", diagnostic.Field);
        }

        [Fact]
        public void CV06_ValidateNegativeModuleVersion_ReportsFieldDiagnostic()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cv06-config");
            string qtDir = CreateDirectory(project.Path, "qt");
            ConfigLoader loader = new(new MockQtToolchain(CreateQtInstallation(qtDir)));
            QmlSharpConfig config = CreateConfig(qtDir) with
            {
                Module = new ModuleConfig
                {
                    Prefix = "QmlSharp.BadVersion",
                    Version = new BuildQmlVersion(-1, 0),
                },
            };

            ConfigDiagnostic diagnostic = Assert.Single(loader.Validate(config));

            Assert.Equal(BuildDiagnosticCode.ConfigValidationError, diagnostic.Code);
            Assert.Equal("module.version", diagnostic.Field);
        }

        [Fact]
        public void CV07_GetDefaults_ReturnsConfigThatPassesContextFreeValidation()
        {
            ConfigLoader loader = new(new MockQtToolchain(CreateMissingQtException()));

            QmlSharpConfig defaults = loader.GetDefaults();
            ImmutableArray<ConfigDiagnostic> diagnostics = loader.Validate(defaults);

            Assert.Null(defaults.Entry);
            Assert.Equal("./dist", defaults.OutDir);
            Assert.Null(defaults.Qt.Dir);
            Assert.Equal("QmlSharp.MyApp", defaults.Module.Prefix);
            Assert.Equal(new BuildQmlVersion(1, 0), defaults.Module.Version);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CV08_LoadNullQtDir_CallsQtDiscoveryAndPopulatesResolvedPath()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cv08-config");
            string qtDir = CreateDirectory(project.Path, "qt-discovered");
            _ = WriteConfig(
                project.Path,
                """
                {
                  "qt": { "dir": null },
                  "module": { "prefix": "QmlSharp.DiscoveredQt" }
                }
                """);
            MockQtToolchain toolchain = new(CreateQtInstallation(qtDir));
            ConfigLoader loader = new(toolchain);

            QmlSharpConfig config = loader.Load(project.Path);

            Assert.Equal(qtDir, config.Qt.Dir);
            Assert.Equal(1, toolchain.DiscoverCallCount);
            Assert.NotNull(toolchain.LastConfig);
            Assert.Equal(project.Path, toolchain.LastConfig.Cwd);
        }

        [Fact]
        public void LoadNullQtDir_WhenDiscoveryFails_ReportsQtDirNotFound()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("qt-discovery-failure");
            _ = WriteConfig(
                project.Path,
                """
                {
                  "qt": { "dir": null },
                  "module": { "prefix": "QmlSharp.MissingQt" }
                }
                """);
            MockQtToolchain toolchain = new(CreateMissingQtException());
            ConfigLoader loader = new(toolchain);

            ConfigParseException exception = Assert.Throws<ConfigParseException>(() => loader.Load(project.Path));

            Assert.Equal(BuildDiagnosticCode.QtDirNotFound, exception.Code);
            Assert.Equal("qt.dir", exception.Diagnostic.FilePath);
            Assert.Equal(1, toolchain.DiscoverCallCount);
        }

        [Fact]
        public void LoadRelativePaths_NormalizesAgainstProjectDirectory()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("path-normalization");
            string qtDir = CreateDirectory(project.Path, "local-qt");
            _ = WriteConfig(
                project.Path,
                """
                {
                  "entry": "./src/Program.cs",
                  "outDir": "./dist-custom",
                  "qt": { "dir": "./local-qt" },
                  "dev": { "watchPaths": ["./src", "./views"] },
                  "module": { "prefix": "QmlSharp.Paths" }
                }
                """);
            MockQtToolchain toolchain = new(CreateQtInstallation(qtDir));
            ConfigLoader loader = new(toolchain);

            QmlSharpConfig config = loader.Load(project.Path);

            Assert.Equal(Path.Join(project.Path, "src", "Program.cs"), config.Entry);
            Assert.Equal(Path.Join(project.Path, "dist-custom"), config.OutDir);
            Assert.Equal(qtDir, config.Qt.Dir);
            Assert.True(config.Dev.WatchPaths.SequenceEqual(ImmutableArray.Create(Path.Join(project.Path, "src"), Path.Join(project.Path, "views"))));
            Assert.Equal(0, toolchain.DiscoverCallCount);
        }

        [Fact]
        public void V1V2RuntimeFields_AreRejectedByStaticQmlSharpConfigSchema()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("v2-fields");
            string qtDir = CreateDirectory(project.Path, "qt");
            _ = WriteConfig(
                project.Path,
                $$"""
                {
                  "qt": { "dir": "{{ToJsonPath(qtDir)}}" },
                  "module": { "prefix": "QmlSharp.V2Only" },
                  "runtime": "v1",
                  "v1Compat": true,
                  "runtimeVersion": 2
                }
                """);
            ConfigLoader loader = new(new MockQtToolchain(CreateQtInstallation(qtDir)));

            ConfigParseException exception = Assert.Throws<ConfigParseException>(() => loader.Load(project.Path));
            ImmutableArray<string> fields = exception.Diagnostics
                .Select(static diagnostic => diagnostic.FilePath ?? string.Empty)
                .ToImmutableArray();

            Assert.Equal(BuildDiagnosticCode.ConfigValidationError, exception.Code);
            Assert.Contains("runtime", fields);
            Assert.Contains("v1Compat", fields);
            Assert.Contains("runtimeVersion", fields);
            Assert.DoesNotContain(
                typeof(QmlSharpConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance),
                static property => property.Name is "Runtime" or "RuntimeVersion" or "V1Compat");
        }

        private static QmlSharpConfig CreateConfig(string qtDir)
        {
            return new QmlSharpConfig
            {
                Entry = "./src/Program.cs",
                Qt = new QtConfig
                {
                    Dir = qtDir,
                },
                Module = new ModuleConfig
                {
                    Prefix = "QmlSharp.Valid",
                    Version = new BuildQmlVersion(1, 0),
                },
            };
        }

        private static string WriteConfig(string projectDir, string json)
        {
            string configPath = Path.Join(projectDir, "qmlsharp.json");
            File.WriteAllText(configPath, json);
            return configPath;
        }

        private static string CreateDirectory(string projectDir, string name)
        {
            string path = Path.Join(projectDir, name);
            _ = Directory.CreateDirectory(path);
            return path;
        }

        private static QtInstallation CreateQtInstallation(string qtDir)
        {
            return new QtInstallation
            {
                RootDir = qtDir,
                BinDir = Path.Join(qtDir, "bin"),
                QmlDir = Path.Join(qtDir, "qml"),
                LibDir = Path.Join(qtDir, "lib"),
                Version = new QtVersion { Major = 6, Minor = 11, Patch = 0 },
                Platform = "windows",
            };
        }

        private static QtInstallationNotFoundError CreateMissingQtException()
        {
            return new QtInstallationNotFoundError(
                "Qt was not found.",
                ImmutableArray.Create("QT_DIR: not set"));
        }

        private static string ToJsonPath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
