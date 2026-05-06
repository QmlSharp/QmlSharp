namespace QmlSharp.DevTools.Tests.Infrastructure
{
    public static class DevToolsTestFixtures
    {
        public static CompilationResult SuccessfulCompilationResult()
        {
            return CompilationResult.FromUnits(ImmutableArray<CompilationUnit>.Empty);
        }

        public static CompilerOptions CompilerOptions()
        {
            return new CompilerOptions
            {
                ProjectPath = "App.csproj",
                OutputDir = "obj/qmlsharp",
                ModuleUriPrefix = "Test.App",
            };
        }

        public static FileChangeBatch FileChangeBatch()
        {
            DateTimeOffset timestamp = DateTimeOffset.Parse("2026-05-06T00:00:00Z", null, System.Globalization.DateTimeStyles.AssumeUniversal);
            FileChange change = new("C:/repo/src/App.cs", FileChangeKind.Modified, timestamp);
            return new FileChangeBatch(ImmutableArray.Create(change), timestamp, timestamp);
        }

        public static BuildResult SuccessfulBuildResult()
        {
            return new BuildResult
            {
                Success = true,
                PhaseResults = ImmutableArray<PhaseResult>.Empty,
                Diagnostics = ImmutableArray<BuildDiagnostic>.Empty,
                Stats = new BuildStats(TimeSpan.Zero, 0, 0, 0, 0, NativeLibBuilt: false),
            };
        }

        public static BuildContext BuildContext()
        {
            QmlSharpConfig config = new()
            {
                Qt = new QtConfig(),
                Module = new ModuleConfig { Prefix = "Test.App" },
            };

            return new BuildContext
            {
                Config = config,
                ProjectDir = "C:/repo",
                OutputDir = "C:/repo/dist",
                QtDir = "C:/Qt/6.11.0/msvc2022_64",
            };
        }

        public static InstanceSnapshot InstanceSnapshot()
        {
            return new InstanceSnapshot(
                "instance-1",
                "CounterViewModel",
                "schema-1",
                "slot-1",
                ImmutableDictionary<string, object?>.Empty,
                DateTimeOffset.UnixEpoch,
                DisposedAt: null);
        }

        public static InstanceInfo InstanceInfo()
        {
            return new InstanceInfo(
                "instance-1",
                "CounterViewModel",
                "schema-1",
                "slot-1",
                InstanceState.Active,
                ImmutableDictionary<string, object?>.Empty,
                QueuedCommandCount: 0,
                CommandsDispatched: 0,
                EffectsEmitted: 0,
                DateTimeOffset.UnixEpoch,
                DisposedAt: null);
        }

        public static RuntimeMetrics RuntimeMetrics()
        {
            return new RuntimeMetrics(
                ActiveInstanceCount: 1,
                TotalInstancesCreated: 1,
                TotalInstancesDestroyed: 0,
                TotalStateSyncs: 0,
                TotalCommandsDispatched: 0,
                TotalEffectsEmitted: 0,
                Uptime: TimeSpan.Zero);
        }

        public static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Join(directory.FullName, "QmlSharp.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not locate QmlSharp repository root.");
        }
    }
}
