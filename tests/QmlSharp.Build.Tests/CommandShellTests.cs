using System.Text.Json;
using QmlSharp.Build.Tests.Infrastructure;
using QmlSharp.Cli;
using QmlSharp.DevTools;

namespace QmlSharp.Build.Tests
{
    public sealed class CommandShellTests
    {
        [Fact]
        public void RootCommand_RegistersExpectedCommandShells()
        {
            using StringWriter output = new();
            using StringWriter error = new();

            System.CommandLine.RootCommand root = QmlSharpCli.CreateRootCommand(
                CreateServices(),
                output,
                error);

            ImmutableArray<string> names = root.Subcommands.Select(static command => command.Name).ToImmutableArray();
            Assert.Contains("build", names);
            Assert.Contains("dev", names);
            Assert.Contains("doctor", names);
            Assert.Contains("init", names);
            Assert.Contains("clean", names);
        }

        [Fact]
        public async Task CommandShellBuildPipeline_BuildsAndPublishesProgress()
        {
            CommandShellBuildPipeline pipeline = new();
            List<BuildProgress> progress = new();
            pipeline.OnProgress(progress.Add);
            BuildContext context = BuildTestFixtures.CreateDefaultContext();

            BuildResult fullBuild = await pipeline.BuildAsync(context);
            BuildResult phaseBuild = await pipeline.BuildPhasesAsync(
                context,
                ImmutableArray.Create(BuildPhase.ConfigLoading));

            Assert.True(fullBuild.Success);
            Assert.True(phaseBuild.Success);
            Assert.Equal(2, pipeline.BuildCallCount);
            Assert.Same(context, pipeline.LastContext);
            Assert.Equal(2, progress.Count);
            Assert.All(progress, static item => Assert.Equal(BuildPhase.ConfigLoading, item.Phase));
        }

        [Fact]
        public async Task CommandShellDefaultServices_DelegateToInitCleanAndDoctor()
        {
            using TempDirectory root = new("qmlsharp-command-shell-defaults");
            string targetDirectory = Path.Join(root.Path, "Shell App");
            CommandShellInitService initService = new();
            CommandServiceResult init = await initService.InitAsync(new InitCommandOptions
            {
                TargetDir = targetDirectory,
            });
            string dist = Path.Join(targetDirectory, "dist");
            _ = Directory.CreateDirectory(dist);
            File.WriteAllText(Path.Join(dist, "manifest.json"), "{}");
            CommandShellCleanService cleanService = new();
            CommandShellDoctor doctor = new(targetDirectory);

            CommandServiceResult clean = await cleanService.CleanAsync(new CleanCommandOptions
            {
                ProjectDir = targetDirectory,
            });
            DoctorCheckResult configCheck = await doctor.RunCheckAsync(DoctorCheckId.ConfigValid);
            ImmutableArray<DoctorFixResult> fixes = await doctor.AutoFixAsync(ImmutableArray<DoctorCheckResult>.Empty);

            Assert.True(init.Success);
            Assert.True(File.Exists(Path.Join(targetDirectory, "qmlsharp.json")));
            Assert.True(clean.Success);
            Assert.False(Directory.Exists(dist));
            Assert.Equal(DoctorCheckStatus.Pass, configCheck.Status);
            Assert.Empty(fixes);
        }

        [Fact]
        public async Task BuildCommand_ParsingBindsOptionsAndCreatesBuildContext()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("bc-options");
            RecordingBuildPipeline pipeline = new(CreateSuccessfulBuildResult());
            CliCommandServices services = CreateServices(
                configLoader: new FakeConfigLoader(BuildTestFixtures.CreateDefaultConfig()),
                buildPipeline: pipeline);

            int exitCode = await InvokeAsync(
                new[] { "build", "--force", "--files", "Counter*.cs", "--library", "--project-dir", project.Path },
                services);

            Assert.Equal(CliExitCode.Success, exitCode);
            Assert.Equal(1, pipeline.BuildCallCount);
            Assert.NotNull(pipeline.LastContext);
            Assert.True(pipeline.LastContext.ForceRebuild);
            Assert.True(pipeline.LastContext.LibraryMode);
            Assert.False(pipeline.LastContext.DryRun);
            Assert.Equal("Counter*.cs", pipeline.LastContext.FileFilter);
            Assert.Equal(Path.GetFullPath(project.Path), pipeline.LastContext.ProjectDir);
        }

        [Fact]
        public async Task BC03_BuildDryRun_DoesNotCallPipelineOrWriteOutputDirectory()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("bc03-dry-run");
            QmlSharpConfig config = BuildTestFixtures.CreateDefaultConfig() with
            {
                OutDir = Path.Join(project.Path, "dist"),
            };
            RecordingBuildPipeline pipeline = new(CreateSuccessfulBuildResult());
            CliCommandServices services = CreateServices(
                configLoader: new FakeConfigLoader(config),
                buildPipeline: pipeline);

            int exitCode = await InvokeAsync(
                new[] { "build", "--dry-run", "--project-dir", project.Path },
                services);

            Assert.Equal(CliExitCode.Success, exitCode);
            Assert.Equal(0, pipeline.BuildCallCount);
            Assert.False(Directory.Exists(Path.Join(project.Path, "dist")));
        }

        [Fact]
        public async Task BC04_BuildJson_WritesMachineReadableEnvelope()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("bc04-json");
            BuildDiagnostic diagnostic = new(
                BuildDiagnosticCode.QmlLintError,
                BuildDiagnosticSeverity.Warning,
                "mock warning",
                BuildPhase.QmlValidation,
                "Counter.qml");
            BuildResult buildResult = CreateSuccessfulBuildResult() with
            {
                Diagnostics = ImmutableArray.Create(diagnostic),
            };
            CliCommandServices services = CreateServices(
                configLoader: new FakeConfigLoader(BuildTestFixtures.CreateDefaultConfig()),
                buildPipeline: new RecordingBuildPipeline(buildResult));
            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = await QmlSharpCli.InvokeAsync(
                new[] { "build", "--json", "--project-dir", project.Path },
                services,
                output,
                error);

            Assert.Equal(CliExitCode.Success, exitCode);
            using JsonDocument document = JsonDocument.Parse(output.ToString());
            JsonElement root = document.RootElement;
            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.Equal("success", root.GetProperty("status").GetString());
            Assert.Equal(CliExitCode.Success, root.GetProperty("exitCode").GetInt32());
            Assert.Equal("build", root.GetProperty("command").GetString());
            Assert.True(root.TryGetProperty("stats", out JsonElement stats));
            Assert.Equal(0, stats.GetProperty("filesCompiled").GetInt32());
            Assert.Equal("QMLSHARP-B060", root.GetProperty("diagnostics")[0].GetProperty("code").GetString());
            Assert.Equal(string.Empty, error.ToString());
        }

        [Fact]
        public async Task BC05_LibraryBuild_MayOmitEntry()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("bc05-library");
            RecordingBuildPipeline pipeline = new(CreateSuccessfulBuildResult());
            QmlSharpConfig config = BuildTestFixtures.CreateDefaultConfig() with
            {
                Entry = null,
            };
            CliCommandServices services = CreateServices(
                configLoader: new FakeConfigLoader(config),
                buildPipeline: pipeline);

            int exitCode = await InvokeAsync(new[] { "build", "--library", "--project-dir", project.Path }, services);

            Assert.Equal(CliExitCode.Success, exitCode);
            Assert.Equal(1, pipeline.BuildCallCount);
            Assert.NotNull(pipeline.LastContext);
            Assert.True(pipeline.LastContext.LibraryMode);
        }

        [Fact]
        public async Task BuildCommand_NormalBuildWithoutEntry_ReturnsCommandError()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("build-missing-entry");
            RecordingBuildPipeline pipeline = new(CreateSuccessfulBuildResult());
            QmlSharpConfig config = BuildTestFixtures.CreateDefaultConfig() with
            {
                Entry = null,
            };
            CliCommandServices services = CreateServices(
                configLoader: new FakeConfigLoader(config),
                buildPipeline: pipeline);

            int exitCode = await InvokeAsync(new[] { "build", "--project-dir", project.Path }, services);

            Assert.Equal(CliExitCode.ConfigOrCommandError, exitCode);
            Assert.Equal(0, pipeline.BuildCallCount);
        }

        [Fact]
        public async Task BuildCommand_BuildFailureMapsToExitCodeOne()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("build-error");
            BuildDiagnostic diagnostic = new(
                BuildDiagnosticCode.CompilationFailed,
                BuildDiagnosticSeverity.Error,
                "Compilation failed.",
                BuildPhase.CSharpCompilation,
                "Program.cs");
            BuildResult buildResult = CreateFailedBuildResult(diagnostic);
            CliCommandServices services = CreateServices(
                configLoader: new FakeConfigLoader(BuildTestFixtures.CreateDefaultConfig()),
                buildPipeline: new RecordingBuildPipeline(buildResult));

            int exitCode = await InvokeAsync(new[] { "build", "--project-dir", project.Path }, services);

            Assert.Equal(CliExitCode.BuildError, exitCode);
        }

        [Fact]
        public async Task BuildCommand_CancellationMapsToStableExitCode()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("build-cancelled");
            RecordingBuildPipeline pipeline = new(CreateSuccessfulBuildResult())
            {
                ThrowCancellation = true,
            };
            CliCommandServices services = CreateServices(
                configLoader: new FakeConfigLoader(BuildTestFixtures.CreateDefaultConfig()),
                buildPipeline: pipeline);
            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = await QmlSharpCli.InvokeAsync(
                new[] { "build", "--json", "--project-dir", project.Path },
                services,
                output,
                error);

            Assert.Equal(CliExitCode.Cancelled, exitCode);
            using JsonDocument document = JsonDocument.Parse(output.ToString());
            Assert.False(document.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("cancelled", document.RootElement.GetProperty("status").GetString());
            Assert.Equal(CliExitCode.Cancelled, document.RootElement.GetProperty("exitCode").GetInt32());
        }

        [Fact]
        public async Task DC03_DC04_DevHeadlessWithEntryOverrideBindsOptions()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("dc03-dc04-dev");
            using CancellationTokenSource cancellation = new();
            RecordingDevServerFactory devServerFactory = new()
            {
                OnStart = cancellation.Cancel,
            };
            QmlSharpConfig config = BuildTestFixtures.CreateDefaultConfig() with
            {
                Entry = null,
            };
            CliCommandServices services = CreateServices(
                configLoader: new FakeConfigLoader(config),
                devServerFactory: devServerFactory);

            int exitCode = await InvokeAsync(
                new[] { "dev", "--headless", "--entry", "./src/AltProgram.cs", "--project-dir", project.Path },
                services,
                cancellation.Token);

            Assert.Equal(CliExitCode.Cancelled, exitCode);
            Assert.Equal(1, devServerFactory.CreatedServer.StartCallCount);
            Assert.NotNull(devServerFactory.LastContext);
            Assert.True(devServerFactory.LastContext.CommandOptions.Headless);
            Assert.True(devServerFactory.LastContext.ServerOptions.Headless);
            Assert.Equal(
                Path.GetFullPath(Path.Join(project.Path, "src", "AltProgram.cs")),
                devServerFactory.LastContext.CommandOptions.Entry);
        }

        [Fact]
        public async Task DevCommand_MissingEntryReturnsCommandError()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("dev-missing-entry");
            RecordingDevServerFactory devServerFactory = new();
            QmlSharpConfig config = BuildTestFixtures.CreateDefaultConfig() with
            {
                Entry = null,
            };
            CliCommandServices services = CreateServices(
                configLoader: new FakeConfigLoader(config),
                devServerFactory: devServerFactory);

            int exitCode = await InvokeAsync(new[] { "dev", "--project-dir", project.Path }, services);

            Assert.Equal(CliExitCode.ConfigOrCommandError, exitCode);
            Assert.Null(devServerFactory.LastContext);
            Assert.Equal(0, devServerFactory.CreatedServer.StartCallCount);
        }

        [Fact]
        public async Task IN03_InitInNonEmptyDirectoryReturnsCommandError()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("in03-init");
            File.WriteAllText(Path.Join(project.Path, "existing.txt"), "already here");
            CliCommandServices services = CreateServices(initService: new CommandShellInitService());

            int exitCode = await InvokeAsync(new[] { "init", "--target-dir", project.Path }, services);

            Assert.Equal(CliExitCode.ConfigOrCommandError, exitCode);
        }

        [Fact]
        public async Task InitCommand_ParsingBindsTemplateAndTargetDirectory()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("init-options");
            RecordingInitService initService = new(CommandServiceResult.Succeeded("Init."));
            CliCommandServices services = CreateServices(initService: initService);

            int exitCode = await InvokeAsync(
                new[] { "init", "--template", "counter", "--target-dir", project.Path },
                services);

            Assert.Equal(CliExitCode.Success, exitCode);
            Assert.NotNull(initService.LastOptions);
            Assert.Equal("counter", initService.LastOptions.Template);
            Assert.Equal(project.Path, initService.LastOptions.TargetDir);
        }

        [Fact]
        public async Task DR01_DoctorAllPassReturnsSuccess()
        {
            CliCommandServices services = CreateServices(
                doctor: new FakeDoctor(ImmutableArray.Create(new DoctorCheckResult(
                    DoctorCheckId.QtInstalled,
                    "Qt SDK",
                    DoctorCheckStatus.Pass,
                    "Qt 6.11.0",
                    null,
                    false))));

            int exitCode = await InvokeAsync(new[] { "doctor" }, services);

            Assert.Equal(CliExitCode.Success, exitCode);
        }

        [Fact]
        public async Task DoctorCommand_FixOptionRunsAutoFix()
        {
            DoctorCheckResult failedCheck = CreateFailedDoctorCheck(DoctorCheckId.QtInstalled, "Qt SDK");
            FakeDoctor doctor = new(ImmutableArray.Create(failedCheck))
            {
                FixResults = ImmutableArray.Create(new DoctorFixResult(DoctorCheckId.QtInstalled, true, "Fixed.")),
            };
            CliCommandServices services = CreateServices(doctor: doctor);

            int exitCode = await InvokeAsync(new[] { "doctor", "--fix" }, services);

            Assert.Equal(CliExitCode.Success, exitCode);
            Assert.Equal(1, doctor.FixCallCount);
        }

        [Fact]
        public async Task DoctorCommand_FixWithNoResultsKeepsFailure()
        {
            DoctorCheckResult failedCheck = CreateFailedDoctorCheck(DoctorCheckId.QtInstalled, "Qt SDK");
            FakeDoctor doctor = new(ImmutableArray.Create(failedCheck));
            CliCommandServices services = CreateServices(doctor: doctor);
            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = await QmlSharpCli.InvokeAsync(
                new[] { "doctor", "--fix" },
                services,
                output,
                error);

            Assert.Equal(CliExitCode.BuildError, exitCode);
            Assert.Equal(1, doctor.FixCallCount);
            Assert.Contains(DoctorCheckId.QtInstalled, error.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, output.ToString());
        }

        [Fact]
        public async Task DoctorCommand_FixWithPartialResultsKeepsUnfixedFailures()
        {
            DoctorCheckResult qtCheck = CreateFailedDoctorCheck(DoctorCheckId.QtInstalled, "Qt SDK");
            DoctorCheckResult dotNetCheck = CreateFailedDoctorCheck(DoctorCheckId.DotNetVersion, ".NET SDK");
            FakeDoctor doctor = new(ImmutableArray.Create(qtCheck, dotNetCheck))
            {
                FixResults = ImmutableArray.Create(new DoctorFixResult(DoctorCheckId.QtInstalled, true, "Fixed.")),
            };
            CliCommandServices services = CreateServices(doctor: doctor);
            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = await QmlSharpCli.InvokeAsync(
                new[] { "doctor", "--fix" },
                services,
                output,
                error);

            string errorText = error.ToString();
            Assert.Equal(CliExitCode.BuildError, exitCode);
            Assert.Equal(1, doctor.FixCallCount);
            Assert.Contains(DoctorCheckId.DotNetVersion, errorText, StringComparison.Ordinal);
            Assert.DoesNotContain(DoctorCheckId.QtInstalled, errorText, StringComparison.Ordinal);
            Assert.Equal(string.Empty, output.ToString());
        }

        [Fact]
        public async Task CN03_CleanWhenDistDoesNotExistReturnsSuccess()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("cn03-clean");
            RecordingCleanService cleanService = new(CommandServiceResult.Succeeded("Clean no-op."));
            CliCommandServices services = CreateServices(cleanService: cleanService);

            int exitCode = await InvokeAsync(new[] { "clean", "--project-dir", project.Path }, services);

            Assert.Equal(CliExitCode.Success, exitCode);
            Assert.Equal(1, cleanService.CallCount);
            Assert.NotNull(cleanService.LastOptions);
            Assert.Equal(project.Path, cleanService.LastOptions.ProjectDir);
            Assert.False(cleanService.LastOptions.Cache);
        }

        [Fact]
        public async Task CleanCommand_ParsingBindsCacheOption()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("clean-cache");
            RecordingCleanService cleanService = new(CommandServiceResult.Succeeded("Clean no-op."));
            CliCommandServices services = CreateServices(cleanService: cleanService);

            int exitCode = await InvokeAsync(new[] { "clean", "--cache", "--project-dir", project.Path }, services);

            Assert.Equal(CliExitCode.Success, exitCode);
            Assert.NotNull(cleanService.LastOptions);
            Assert.True(cleanService.LastOptions.Cache);
        }

        [Fact]
        public async Task ParseErrors_MapToCommandErrorExitCode()
        {
            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = await QmlSharpCli.InvokeAsync(
                new[] { "build", "--not-a-real-option" },
                CreateServices(),
                output,
                error);

            Assert.Equal(CliExitCode.ConfigOrCommandError, exitCode);
            Assert.Contains("--not-a-real-option", error.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task RootInvocation_CancellationMapsToStableExitCode()
        {
            using CancellationTokenSource cancellation = new();
            await cancellation.CancelAsync();
            using StringWriter output = new();
            using StringWriter error = new();

            int exitCode = await QmlSharpCli.InvokeAsync(
                new[] { "clean" },
                CreateServices(),
                output,
                error,
                cancellation.Token);

            Assert.Equal(CliExitCode.Cancelled, exitCode);
        }

        private static async Task<int> InvokeAsync(string[] args, CliCommandServices services)
        {
            using StringWriter output = new();
            using StringWriter error = new();
            return await QmlSharpCli.InvokeAsync(args, services, output, error);
        }

        private static async Task<int> InvokeAsync(
            string[] args,
            CliCommandServices services,
            CancellationToken cancellationToken)
        {
            using StringWriter output = new();
            using StringWriter error = new();
            return await QmlSharpCli.InvokeAsync(args, services, output, error, cancellationToken);
        }

        private static CliCommandServices CreateServices(
            IConfigLoader? configLoader = null,
            IBuildPipeline? buildPipeline = null,
            ICliDevServerFactory? devServerFactory = null,
            IDoctor? doctor = null,
            IInitService? initService = null,
            ICleanService? cleanService = null)
        {
            return new CliCommandServices
            {
                ConfigLoader = configLoader ?? new FakeConfigLoader(BuildTestFixtures.CreateDefaultConfig()),
                BuildPipeline = buildPipeline ?? new RecordingBuildPipeline(CreateSuccessfulBuildResult()),
                DevServerFactory = devServerFactory ?? new RecordingDevServerFactory(),
                Doctor = doctor ?? new FakeDoctor(ImmutableArray<DoctorCheckResult>.Empty),
                InitService = initService ?? new RecordingInitService(CommandServiceResult.Succeeded("Init.")),
                CleanService = cleanService ?? new RecordingCleanService(CommandServiceResult.Succeeded("Clean.")),
            };
        }

        private static BuildResult CreateSuccessfulBuildResult()
        {
            return new BuildResult
            {
                Success = true,
                PhaseResults = ImmutableArray<PhaseResult>.Empty,
                Diagnostics = ImmutableArray<BuildDiagnostic>.Empty,
                Stats = new BuildStats(TimeSpan.Zero, 0, 0, 0, 0, false),
            };
        }

        private static BuildResult CreateFailedBuildResult(BuildDiagnostic diagnostic)
        {
            return new BuildResult
            {
                Success = false,
                PhaseResults = ImmutableArray<PhaseResult>.Empty,
                Diagnostics = ImmutableArray.Create(diagnostic),
                Stats = new BuildStats(TimeSpan.Zero, 0, 0, 0, 0, false),
            };
        }

        private static DoctorCheckResult CreateFailedDoctorCheck(string checkId, string description)
        {
            return new DoctorCheckResult(
                checkId,
                description,
                DoctorCheckStatus.Fail,
                null,
                $"{description} failed.",
                true);
        }

        private sealed class FakeConfigLoader : IConfigLoader
        {
            private readonly QmlSharpConfig _config;

            public FakeConfigLoader(QmlSharpConfig config)
            {
                _config = config;
            }

            public QmlSharpConfig Load(string projectDir)
            {
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

        private sealed class RecordingBuildPipeline : IBuildPipeline
        {
            private readonly BuildResult _result;

            public RecordingBuildPipeline(BuildResult result)
            {
                _result = result;
            }

            public bool ThrowCancellation { get; init; }

            public int BuildCallCount { get; private set; }

            public BuildContext? LastContext { get; private set; }

            public Task<BuildResult> BuildAsync(BuildContext context, CancellationToken cancellationToken = default)
            {
                if (ThrowCancellation)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                BuildCallCount++;
                LastContext = context;
                return Task.FromResult(_result);
            }

            public Task<BuildResult> BuildPhasesAsync(
                BuildContext context,
                ImmutableArray<BuildPhase> phases,
                CancellationToken cancellationToken = default)
            {
                return BuildAsync(context, cancellationToken);
            }

            public void OnProgress(Action<BuildProgress> callback)
            {
            }
        }

        private sealed class RecordingDevServerFactory : ICliDevServerFactory
        {
            public DevServerCreationContext? LastContext { get; private set; }

            public Action? OnStart { get; init; }

            public RecordingDevServer CreatedServer { get; } = new();

            public IDevServer Create(DevServerCreationContext context)
            {
                LastContext = context;
                CreatedServer.OnStart = OnStart;
                return CreatedServer;
            }
        }

        private sealed class RecordingDevServer : IDevServer
        {
            public Action? OnStart { get; set; }

            public int StartCallCount { get; private set; }

            public int StopCallCount { get; private set; }

            public DevServerStatus Status { get; private set; } = DevServerStatus.Running;

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
                StartCallCount++;
                OnStart?.Invoke();
                return Task.CompletedTask;
            }

            public Task StopAsync()
            {
                StopCallCount++;
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

        private sealed class FakeDoctor : IDoctor
        {
            private readonly ImmutableArray<DoctorCheckResult> _checks;

            public FakeDoctor(ImmutableArray<DoctorCheckResult> checks)
            {
                _checks = checks;
            }

            public ImmutableArray<DoctorFixResult> FixResults { get; init; } =
                ImmutableArray<DoctorFixResult>.Empty;

            public int FixCallCount { get; private set; }

            public Task<ImmutableArray<DoctorCheckResult>> RunAllChecksAsync(QmlSharpConfig? config = null)
            {
                return Task.FromResult(_checks);
            }

            public Task<DoctorCheckResult> RunCheckAsync(string checkId)
            {
                return Task.FromResult(new DoctorCheckResult(
                    checkId,
                    checkId,
                    DoctorCheckStatus.Pass,
                    null,
                    null,
                    false));
            }

            public Task<ImmutableArray<DoctorFixResult>> AutoFixAsync(
                ImmutableArray<DoctorCheckResult> failedChecks)
            {
                FixCallCount++;
                return Task.FromResult(FixResults);
            }
        }

        private sealed class RecordingInitService : IInitService
        {
            private readonly CommandServiceResult _result;

            public RecordingInitService(CommandServiceResult result)
            {
                _result = result;
            }

            public Task<CommandServiceResult> InitAsync(
                InitCommandOptions options,
                CancellationToken cancellationToken = default)
            {
                LastOptions = options;
                return Task.FromResult(_result);
            }

            public InitCommandOptions? LastOptions { get; private set; }
        }

        private sealed class RecordingCleanService : ICleanService
        {
            private readonly CommandServiceResult _result;

            public RecordingCleanService(CommandServiceResult result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }

            public CleanCommandOptions? LastOptions { get; private set; }

            public Task<CommandServiceResult> CleanAsync(
                CleanCommandOptions options,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                LastOptions = options;
                return Task.FromResult(_result);
            }
        }
    }
}
