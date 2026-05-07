using QmlSharp.Cli;

namespace QmlSharp.DevTools.Tests
{
    public sealed class DevCommandCliIntegrationTests
    {
        [Fact]
        public async Task DC01_DevCommand_StartsDevServerAndStopsOnCancellation()
        {
            using TempProject project = TempProject.Create("dc01-dev-cli-start");
            using CancellationTokenSource cancellation = new();
            RecordingDevServerFactory factory = new()
            {
                OnStart = cancellation.Cancel,
            };
            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = await QmlSharpCli.InvokeAsync(
                new[] { "dev", "--project-dir", project.Path },
                CreateServices(project.Config, factory),
                output,
                error,
                cancellation.Token);

            Assert.Equal(CliExitCode.Cancelled, exitCode);
            Assert.Equal(1, factory.Server.StartCalls);
            Assert.Equal(1, factory.Server.StopCalls);
            Assert.NotNull(factory.Context);
            Assert.Contains("Starting QmlSharp dev server", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("Dev server ready", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("Dev server stopped", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task DC02_DevCommand_EntryOverrideIsAppliedBeforeServerCreation()
        {
            using TempProject project = TempProject.Create("dc02-dev-cli-entry");
            using CancellationTokenSource cancellation = new();
            RecordingDevServerFactory factory = new()
            {
                OnStart = cancellation.Cancel,
            };
            string overrideEntry = Path.Join("src", "AltProgram.cs");

            int exitCode = await InvokeDevAsync(
                project,
                factory,
                cancellation.Token,
                "--entry",
                overrideEntry);

            Assert.Equal(CliExitCode.Cancelled, exitCode);
            Assert.NotNull(factory.Context);
            string expectedEntry = Path.GetFullPath(Path.Join(project.Path, overrideEntry));
            Assert.Equal(expectedEntry, factory.Context.CommandOptions.Entry);
            Assert.Equal(expectedEntry, factory.Context.EffectiveConfig.Entry);
            Assert.Equal(expectedEntry, factory.Context.BuildContext.Config.Entry);
        }

        [Fact]
        public async Task DC03_DevCommand_HeadlessFlagIsPropagatedToServerOptions()
        {
            using TempProject project = TempProject.Create("dc03-dev-cli-headless");
            using CancellationTokenSource cancellation = new();
            RecordingDevServerFactory factory = new()
            {
                OnStart = cancellation.Cancel,
            };

            int exitCode = await InvokeDevAsync(
                project,
                factory,
                cancellation.Token,
                "--headless");

            Assert.Equal(CliExitCode.Cancelled, exitCode);
            Assert.NotNull(factory.Context);
            Assert.True(factory.Context.CommandOptions.Headless);
            Assert.True(factory.Context.ServerOptions.Headless);
        }

        [Fact]
        public async Task DC04_DevCommand_InitialServerErrorMapsToBuildExitCode()
        {
            using TempProject project = TempProject.Create("dc04-dev-cli-start-error");
            RecordingDevServerFactory factory = new()
            {
                StatusAfterStart = DevServerStatus.Error,
            };
            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = await QmlSharpCli.InvokeAsync(
                new[] { "dev", "--project-dir", project.Path },
                CreateServices(project.Config, factory),
                output,
                error);

            Assert.Equal(CliExitCode.BuildError, exitCode);
            Assert.Equal(1, factory.Server.StartCalls);
            Assert.Equal(1, factory.Server.StopCalls);
            Assert.Contains("Dev server failed to start", error.ToString(), StringComparison.Ordinal);
            Assert.Contains("Dev server stopped", output.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task DevCommand_MissingEntryDoesNotCreateServer()
        {
            using TempProject project = TempProject.Create("dev-cli-missing-entry");
            RecordingDevServerFactory factory = new();
            QmlSharpConfig config = project.Config with
            {
                Entry = null,
            };

            int exitCode = await InvokeDevAsync(project, factory, CancellationToken.None, config);

            Assert.Equal(CliExitCode.ConfigOrCommandError, exitCode);
            Assert.Null(factory.Context);
            Assert.Equal(0, factory.Server.StartCalls);
        }

        private static async Task<int> InvokeDevAsync(
            TempProject project,
            RecordingDevServerFactory factory,
            CancellationToken cancellationToken,
            params string[] extraArgs)
        {
            return await InvokeDevAsync(project, factory, cancellationToken, project.Config, extraArgs);
        }

        private static async Task<int> InvokeDevAsync(
            TempProject project,
            RecordingDevServerFactory factory,
            CancellationToken cancellationToken,
            QmlSharpConfig config,
            params string[] extraArgs)
        {
            ImmutableArray<string> args = ImmutableArray.Create("dev", "--project-dir", project.Path)
                .AddRange(extraArgs);
            using StringWriter output = new();
            using StringWriter error = new();
            return await QmlSharpCli.InvokeAsync(
                args.ToArray(),
                CreateServices(config, factory),
                output,
                error,
                cancellationToken);
        }

        private static CliCommandServices CreateServices(
            QmlSharpConfig config,
            RecordingDevServerFactory factory)
        {
            FakeConfigLoader configLoader = new(config);
            return new CliCommandServices
            {
                ConfigLoader = configLoader,
                BuildPipeline = new ThrowingBuildPipeline(),
                DevServerFactory = factory,
                Doctor = new ThrowingDoctor(),
                InitService = new ThrowingInitService(),
                CleanService = new ThrowingCleanService(),
            };
        }

        private sealed class TempProject : IDisposable
        {
            private TempProject(string path)
            {
                Path = path;
                _ = Directory.CreateDirectory(System.IO.Path.Join(path, "src"));
                File.WriteAllText(System.IO.Path.Join(path, "src", "Program.cs"), "public static class Program {}\n");
                Config = new QmlSharpConfig
                {
                    Entry = System.IO.Path.Join(path, "src", "Program.cs"),
                    OutDir = System.IO.Path.Join(path, "dist"),
                    Qt = new QtConfig
                    {
                        Dir = "C:/Qt/6.11.0/msvc2022_64",
                        Modules = ImmutableArray.Create("QtQuick"),
                    },
                    Dev = new DevConfig
                    {
                        WatchPaths = ImmutableArray.Create(System.IO.Path.Join(path, "src")),
                        DebounceMs = 25,
                    },
                    Module = new ModuleConfig
                    {
                        Prefix = "QmlSharp.DevCliTests",
                    },
                };
            }

            public string Path { get; }

            public QmlSharpConfig Config { get; }

            public static TempProject Create(string name)
            {
                string path = System.IO.Path.Join(
                    System.IO.Path.GetTempPath(),
                    "qmlsharp-" + name + "-" + Guid.NewGuid().ToString("N"));
                return new TempProject(path);
            }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
        }

        private sealed class RecordingDevServerFactory : ICliDevServerFactory
        {
            public DevServerCreationContext? Context { get; private set; }

            public RecordingDevServer Server { get; } = new();

            public Action? OnStart { get; init; }

            public DevServerStatus StatusAfterStart { get; init; } = DevServerStatus.Running;

            public IDevServer Create(DevServerCreationContext context)
            {
                Context = context;
                Server.OnStart = OnStart;
                Server.StatusAfterStart = StatusAfterStart;
                return Server;
            }
        }

        private sealed class RecordingDevServer : IDevServer
        {
            public Action? OnStart { get; set; }

            public DevServerStatus StatusAfterStart { get; set; } = DevServerStatus.Running;

            public int StartCalls { get; private set; }

            public int StopCalls { get; private set; }

            public DevServerStatus Status { get; private set; } = DevServerStatus.Idle;

            public ServerStats Stats { get; } = new(
                1,
                0,
                0,
                0,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero);

            public IRepl? Repl => null;

            public event Action<DevServerStatusChangedEvent> OnStatusChanged = static _ => { };

            public Task StartAsync(CancellationToken cancellationToken = default)
            {
                StartCalls++;
                Status = StatusAfterStart;
                OnStatusChanged(new DevServerStatusChangedEvent(
                    DevServerStatus.Idle,
                    Status,
                    DateTimeOffset.UtcNow,
                    "test"));
                OnStart?.Invoke();
                return Task.CompletedTask;
            }

            public Task StopAsync()
            {
                StopCalls++;
                Status = DevServerStatus.Idle;
                return Task.CompletedTask;
            }

            public Task<HotReloadResult> RebuildAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task RestartAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }

        private sealed class FakeConfigLoader : IConfigLoader
        {
            private readonly QmlSharpConfig config;

            public FakeConfigLoader(QmlSharpConfig config)
            {
                this.config = config;
            }

            public QmlSharpConfig Load(string projectDir)
            {
                return config;
            }

            public ImmutableArray<ConfigDiagnostic> Validate(QmlSharpConfig inputConfig)
            {
                return ImmutableArray<ConfigDiagnostic>.Empty;
            }

            public QmlSharpConfig GetDefaults()
            {
                return config;
            }
        }

        private sealed class ThrowingBuildPipeline : IBuildPipeline
        {
            public Task<BuildResult> BuildAsync(BuildContext context, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<BuildResult> BuildPhasesAsync(
                BuildContext context,
                ImmutableArray<BuildPhase> phases,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public void OnProgress(Action<BuildProgress> callback)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class ThrowingDoctor : IDoctor
        {
            public Task<ImmutableArray<DoctorCheckResult>> RunAllChecksAsync(QmlSharpConfig? config = null)
            {
                throw new NotSupportedException();
            }

            public Task<DoctorCheckResult> RunCheckAsync(string checkId)
            {
                throw new NotSupportedException();
            }

            public Task<ImmutableArray<DoctorFixResult>> AutoFixAsync(
                ImmutableArray<DoctorCheckResult> failedChecks)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class ThrowingInitService : IInitService
        {
            public Task<CommandServiceResult> InitAsync(
                InitCommandOptions options,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class ThrowingCleanService : ICleanService
        {
            public Task<CommandServiceResult> CleanAsync(
                CleanCommandOptions options,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }
    }
}
