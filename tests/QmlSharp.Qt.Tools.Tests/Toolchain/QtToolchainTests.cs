using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.Toolchain
{
    [Collection(QtEnvironmentCollection.Name)]
    [Trait("Category", TestCategories.Toolchain)]
    public sealed class QtToolchainTests
    {
        [Fact]
        public async Task TC001_Discover_WithExplicitQtDir_ReturnsInstallationMetadata()
        {
            using TemporaryQtInstallation qt = TemporaryQtInstallation.Create(includeAllTools: true);
            string extraImportPath = Path.Join(qt.RootDir, "imports");
            _ = Directory.CreateDirectory(extraImportPath);
            QtToolchain toolchain = new();

            QtInstallation installation = await toolchain.DiscoverAsync(new QtToolchainConfig
            {
                QtDir = qt.RootDir,
                ImportPaths = [extraImportPath],
            });

            Assert.Equal(qt.RootDir, installation.RootDir);
            Assert.Equal(Path.Join(qt.RootDir, "bin"), installation.BinDir);
            Assert.Equal(Path.Join(qt.RootDir, "qml"), installation.QmlDir);
            Assert.Equal(Path.Join(qt.RootDir, "lib"), installation.LibDir);
            Assert.Equal("6.11.0", installation.Version.String);
            Assert.Equal(ExpectedPlatform, installation.Platform);
            Assert.Equal([Path.Join(qt.RootDir, "qml"), extraImportPath], installation.ImportPaths.ToArray());
        }

        [Fact]
        public async Task TC002_Discover_WithInvalidExplicitQtDir_ThrowsAttemptedPath()
        {
            string invalidPath = Path.Join(Path.GetTempPath(), "qmlsharp-missing-qt", Guid.NewGuid().ToString("N"));
            QtToolchain toolchain = new();

            QtInstallationNotFoundError error = await Assert.ThrowsAsync<QtInstallationNotFoundError>(
                () => toolchain.DiscoverAsync(new QtToolchainConfig { QtDir = invalidPath }));

            Assert.Contains(invalidPath, error.Message, StringComparison.Ordinal);
            Assert.Contains(error.AttemptedSteps, step => step.Contains(invalidPath, StringComparison.Ordinal));
        }

        [Fact]
        public async Task TC003_Discover_UsesQtDirEnvironmentAndExplicitConfigWins()
        {
            using TemporaryQtInstallation environmentQt = TemporaryQtInstallation.Create(includeAllTools: true);
            using TemporaryQtInstallation explicitQt = TemporaryQtInstallation.Create(includeAllTools: true);
            using EnvironmentVariableScope scope = new EnvironmentVariableScope();
            scope.Set(QtToolsTestEnvironment.QtDirVariableName, environmentQt.RootDir);
            scope.Set("QMLSHARP_QT_DIR", environmentQt.RootDir);

            QtInstallation fromEnvironment = await new QtToolchain().DiscoverAsync();
            QtInstallation fromExplicit = await new QtToolchain().DiscoverAsync(
                new QtToolchainConfig { QtDir = explicitQt.RootDir });

            Assert.Equal(environmentQt.RootDir, fromEnvironment.RootDir);
            Assert.Equal(explicitQt.RootDir, fromExplicit.RootDir);
        }

        [Fact]
        public async Task TC004_CheckToolsAsync_OnCompleteInstallation_ReportsAllToolsAvailable()
        {
            using TemporaryQtInstallation qt = TemporaryQtInstallation.Create(includeAllTools: true);
            QtToolchain toolchain = new();
            _ = await toolchain.DiscoverAsync(new QtToolchainConfig { QtDir = qt.RootDir });

            ToolAvailability availability = await toolchain.CheckToolsAsync();

            ToolInfo[] tools =
            [
                availability.QmlFormat,
                availability.QmlLint,
                availability.QmlCachegen,
                availability.Qmltc,
                availability.QmlImportScanner,
                availability.QmlDom,
                availability.Qml,
                availability.Rcc,
                availability.QmlTypeRegistrar,
                availability.Moc,
                availability.QmlAotStats,
            ];
            Assert.All(tools, static tool =>
            {
                Assert.True(tool.Available);
                Assert.True(Path.IsPathFullyQualified(tool.Path));
                Assert.Equal("6.11.0", tool.Version);
            });
        }

        [Fact]
        public async Task TC005_CheckToolsAsync_OnPartialInstallation_ReportsMissingTools()
        {
            using TemporaryQtInstallation qt = TemporaryQtInstallation.Create(includeAllTools: false);
            qt.AddTool("qmlformat");
            qt.AddTool("qmllint");
            QtToolchain toolchain = new();
            _ = await toolchain.DiscoverAsync(new QtToolchainConfig { QtDir = qt.RootDir });

            ToolAvailability availability = await toolchain.CheckToolsAsync();

            Assert.True(availability.QmlFormat.Available);
            Assert.True(availability.QmlLint.Available);
            Assert.False(availability.QmlCachegen.Available);
            Assert.Null(availability.QmlCachegen.Version);
            Assert.False(availability.QmlAotStats.Available);
        }

        [Fact]
        public async Task TC006_GetToolInfoAsync_ForQmlformat_ReturnsValidAbsolutePath()
        {
            using TemporaryQtInstallation qt = TemporaryQtInstallation.Create(includeAllTools: true);
            QtToolchain toolchain = new();
            _ = await toolchain.DiscoverAsync(new QtToolchainConfig { QtDir = qt.RootDir });

            ToolInfo info = await toolchain.GetToolInfoAsync("qmlformat");

            Assert.Equal("qmlformat", info.Name);
            Assert.True(info.Available);
            Assert.Equal(Path.Join(qt.RootDir, "bin", ExecutableName("qmlformat")), info.Path);
            Assert.Equal("6.11.0", info.Version);
        }

        [Fact]
        public async Task TC007_Discover_ParsesQtVersionFromInstallationPath()
        {
            using TemporaryQtInstallation qt = TemporaryQtInstallation.Create(includeAllTools: true);

            QtInstallation installation = await new QtToolchain().DiscoverAsync(
                new QtToolchainConfig { QtDir = qt.RootDir });

            Assert.Equal(6, installation.Version.Major);
            Assert.Equal(11, installation.Version.Minor);
            Assert.Equal(0, installation.Version.Patch);
            Assert.Equal("6.11.0", installation.Version.String);
        }

        [Fact]
        public async Task TC008_Discover_DetectsHostPlatform()
        {
            using TemporaryQtInstallation qt = TemporaryQtInstallation.Create(includeAllTools: true);

            QtInstallation installation = await new QtToolchain().DiscoverAsync(
                new QtToolchainConfig { QtDir = qt.RootDir });

            Assert.Equal(ExpectedPlatform, installation.Platform);
        }

        [Fact]
        public async Task TC009_Discover_WithAdditionalImportPaths_AppendsThemAfterQmlDirectory()
        {
            using TemporaryQtInstallation qt = TemporaryQtInstallation.Create(includeAllTools: true);
            string firstExtra = Path.Join(qt.RootDir, "extra1");
            string secondExtra = Path.Join(qt.RootDir, "extra2");

            QtInstallation installation = await new QtToolchain().DiscoverAsync(new QtToolchainConfig
            {
                QtDir = qt.RootDir,
                ImportPaths = [firstExtra, secondExtra],
            });

            Assert.Equal([Path.Join(qt.RootDir, "qml"), firstExtra, secondExtra], installation.ImportPaths.ToArray());
        }

        [Fact]
        public async Task DiscoverAsync_CachesFirstResultForToolchainLifetime()
        {
            using TemporaryQtInstallation firstQt = TemporaryQtInstallation.Create(includeAllTools: true);
            using TemporaryQtInstallation secondQt = TemporaryQtInstallation.Create(includeAllTools: true);
            QtToolchain toolchain = new();

            QtInstallation first = await toolchain.DiscoverAsync(new QtToolchainConfig { QtDir = firstQt.RootDir });
            QtInstallation second = await toolchain.DiscoverAsync(new QtToolchainConfig { QtDir = secondQt.RootDir });

            Assert.Same(first, second);
            Assert.Equal(firstQt.RootDir, second.RootDir);
        }

        [Fact]
        public async Task DiscoverAsync_ReadsProjectConfigFile()
        {
            using TemporaryQtInstallation qt = TemporaryQtInstallation.Create(includeAllTools: true);
            using TemporaryDirectory project = TemporaryDirectory.Create("qmlsharp-project-");
            string configDirectory = Path.Join(project.Path, ".qmlsharp");
            _ = Directory.CreateDirectory(configDirectory);
            await File.WriteAllTextAsync(
                Path.Join(configDirectory, "config.json"),
                $$"""{ "qtDir": "{{EscapeJson(qt.RootDir)}}", "importPaths": ["{{EscapeJson(Path.Join(qt.RootDir, "configured-imports"))}}"] }""");
            using EnvironmentVariableScope scope = new EnvironmentVariableScope();
            scope.Set(QtToolsTestEnvironment.QtDirVariableName, null);

            QtInstallation installation = await new QtToolchain().DiscoverAsync(
                new QtToolchainConfig { Cwd = project.Path });

            Assert.Equal(qt.RootDir, installation.RootDir);
            Assert.Contains(Path.Join(qt.RootDir, "configured-imports"), installation.ImportPaths);
        }

        [Fact]
        public async Task DiscoverAsync_AutoScansHomeQtDirectory()
        {
            using TemporaryDirectory home = TemporaryDirectory.Create("qmlsharp-home-");
            using TemporaryQtInstallation qt = TemporaryQtInstallation.Create(
                Path.Join(home.Path, "Qt", "6.11.0", "testkit"),
                includeAllTools: true);
            using EnvironmentVariableScope scope = new EnvironmentVariableScope();
            scope.Set(QtToolsTestEnvironment.QtDirVariableName, null);
            scope.Set("PATH", string.Empty);
            scope.Set("USERPROFILE", home.Path);
            scope.Set("HOME", home.Path);

            QtInstallation installation = await new QtToolchain().DiscoverAsync();

            Assert.Equal(qt.RootDir, installation.RootDir);
        }

        [Fact]
        public async Task DiscoverAsync_UsesPathWhenQtBinIsOnPath()
        {
            using TemporaryQtInstallation qt = TemporaryQtInstallation.Create(includeAllTools: true);
            QtToolchain toolchain = new(
                name => name switch
                {
                    "PATH" => Path.Join(qt.RootDir, "bin"),
                    _ => null,
                },
                path => IsUnderRoot(path, qt.RootDir) && Directory.Exists(path),
                path => IsUnderRoot(path, qt.RootDir) && File.Exists(path),
                () => qt.RootDir);

            QtInstallation installation = await toolchain.DiscoverAsync();

            Assert.Equal(qt.RootDir, installation.RootDir);
        }

        [RequiresQtFact]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RequiresQt_DiscoverAndCheckTools_WithRealQtInstallation()
        {
            QtToolchain toolchain = new();

            QtInstallation installation = await toolchain.DiscoverAsync();
            ToolAvailability availability = await toolchain.CheckToolsAsync();

            Assert.True(Directory.Exists(installation.RootDir));
            Assert.Equal("6.11.0", installation.Version.String);
            Assert.True(availability.QmlFormat.Available);
            Assert.True(availability.QmlLint.Available);
            Assert.True(availability.QmlCachegen.Available);
            Assert.True(availability.Qmltc.Available);
            Assert.True(availability.QmlImportScanner.Available);
            Assert.True(availability.QmlDom.Available);
            Assert.True(availability.Qml.Available);
            Assert.True(availability.Rcc.Available);
            Assert.True(availability.QmlTypeRegistrar.Available);
            Assert.True(availability.Moc.Available);
            Assert.True(availability.QmlAotStats.Available);
        }

        private static string ExpectedPlatform
        {
            get
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
        }

        private static string ExecutableName(string toolName)
        {
            return OperatingSystem.IsWindows() ? toolName + ".exe" : toolName;
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        private static bool IsUnderRoot(string path, string root)
        {
            StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(root);
            return fullPath.StartsWith(fullRoot, comparison);
        }

        private sealed class TemporaryQtInstallation : IDisposable
        {
            private static readonly string[] AllToolNames =
            [
                "qmlformat",
                "qmllint",
                "qmlcachegen",
                "qmltc",
                "qmlimportscanner",
                "qmldom",
                "qml",
                "rcc",
                "qmltyperegistrar",
                "moc",
                "qmlaotstats",
            ];

            private readonly bool _ownsRootParent;

            private TemporaryQtInstallation(string rootDir, bool ownsRootParent)
            {
                RootDir = Path.GetFullPath(rootDir);
                _ownsRootParent = ownsRootParent;
            }

            public string RootDir { get; }

            public static TemporaryQtInstallation Create(bool includeAllTools)
            {
                TemporaryDirectory directory = TemporaryDirectory.Create("qmlsharp-fake-qt-");
                string rootDir = Path.Join(directory.Path, "Qt", "6.11.0", "msvc2022_64");
                TemporaryQtInstallation qt = new(rootDir, ownsRootParent: true);
                qt.CreateLayout(includeAllTools);
                directory.ReleaseOwnership();
                return qt;
            }

            public static TemporaryQtInstallation Create(string rootDir, bool includeAllTools)
            {
                TemporaryQtInstallation qt = new(rootDir, ownsRootParent: false);
                qt.CreateLayout(includeAllTools);
                return qt;
            }

            public void AddTool(string toolName)
            {
                string binDir = Path.Join(RootDir, "bin");
                string toolPath = Path.Join(binDir, ExecutableName(toolName));
                _ = Directory.CreateDirectory(binDir);
                File.WriteAllText(toolPath, string.Empty);
            }

            public void Dispose()
            {
                string deletePath = _ownsRootParent
                    ? Path.GetFullPath(Path.Join(RootDir, "..", "..", ".."))
                    : RootDir;
                Directory.Delete(deletePath, recursive: true);
            }

            private void CreateLayout(bool includeAllTools)
            {
                _ = Directory.CreateDirectory(Path.Join(RootDir, "bin"));
                _ = Directory.CreateDirectory(Path.Join(RootDir, "qml"));
                _ = Directory.CreateDirectory(Path.Join(RootDir, "lib"));

                if (includeAllTools)
                {
                    foreach (string toolName in AllToolNames)
                    {
                        AddTool(toolName);
                    }
                }
            }
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            private bool _ownsDirectory = true;

            private TemporaryDirectory(string path)
            {
                Path = path;
            }

            public string Path { get; }

            public static TemporaryDirectory Create(string prefix)
            {
                string path = System.IO.Path.Join(System.IO.Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
                _ = Directory.CreateDirectory(path);
                return new TemporaryDirectory(path);
            }

            public void ReleaseOwnership()
            {
                _ownsDirectory = false;
            }

            public void Dispose()
            {
                if (_ownsDirectory && Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
        }

        private sealed class EnvironmentVariableScope : IDisposable
        {
            private readonly Dictionary<string, string?> _originalValues = [];

            public void Set(string name, string? value)
            {
                if (!_originalValues.ContainsKey(name))
                {
                    _originalValues.Add(name, Environment.GetEnvironmentVariable(name));
                }

                Environment.SetEnvironmentVariable(name, value);
            }

            public void Dispose()
            {
                foreach (KeyValuePair<string, string?> item in _originalValues)
                {
                    Environment.SetEnvironmentVariable(item.Key, item.Value);
                }
            }
        }
    }
}
