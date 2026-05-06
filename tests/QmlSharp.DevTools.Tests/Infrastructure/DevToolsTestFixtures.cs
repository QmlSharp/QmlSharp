namespace QmlSharp.DevTools.Tests.Infrastructure
{
    public static class DevToolsTestFixtures
    {
        public static CompilationResult SuccessfulCompilationResult()
        {
            return CompilationResult.FromUnits(ImmutableArray<CompilationUnit>.Empty);
        }

        public static CompilationResult CompilationResultWithSchema(
            ViewModelSchema? schema = null,
            long elapsedMilliseconds = 1,
            string qmlSourcePath = "C:/repo/dist/qml/CounterView.qml")
        {
            ViewModelSchema effectiveSchema = schema ?? ViewModelSchema();
            CompilationUnit unit = new()
            {
                SourceFilePath = "C:/repo/src/CounterView.cs",
                ViewClassName = "CounterView",
                ViewModelClassName = effectiveSchema.ClassName,
                QmlText = "import QtQuick\nItem {}\n",
                Schema = effectiveSchema,
                SourceMap = SourceMap.Empty("C:/repo/src/CounterView.cs", qmlSourcePath),
            };

            return CompilationResult.FromUnits(ImmutableArray.Create(unit), elapsedMilliseconds: elapsedMilliseconds);
        }

        public static CompilationResult FailedCompilationResult()
        {
            CompilerDiagnostic diagnostic = new(
                DiagnosticCodes.RoslynCompilationFailed,
                DiagnosticSeverity.Error,
                "compile failed",
                SourceLocation.FileOnly("C:/repo/src/CounterView.cs"),
                Phase: "Analyze");
            return CompilationResult.FromUnits(ImmutableArray<CompilationUnit>.Empty, ImmutableArray.Create(diagnostic));
        }

        public static ViewModelSchema ViewModelSchema(
            ImmutableArray<StateEntry> properties = default,
            ImmutableArray<CommandEntry> commands = default,
            ImmutableArray<EffectEntry> effects = default,
            string className = "CounterViewModel",
            string moduleUri = "Test.App",
            string compilerSlotKey = "CounterView::__qmlsharp_vm0")
        {
            ImmutableArray<StateEntry> effectiveProperties = properties.IsDefault
                ? ImmutableArray.Create(State("count", "int"))
                : properties;
            ImmutableArray<CommandEntry> effectiveCommands = commands.IsDefault
                ? ImmutableArray.Create(Command("increment"))
                : commands;
            ImmutableArray<EffectEntry> effectiveEffects = effects.IsDefault
                ? ImmutableArray.Create(Effect("countChanged", "int"))
                : effects;

            return new ViewModelSchema(
                SchemaVersion: "2.0",
                className,
                ModuleName: "TestApp",
                moduleUri,
                new QmlSharp.Compiler.QmlVersion(1, 0),
                Version: 2,
                compilerSlotKey,
                effectiveProperties,
                effectiveCommands,
                effectiveEffects,
                new LifecycleInfo(OnMounted: false, OnUnmounting: false, HotReload: true));
        }

        public static StateEntry State(string name, string type, bool readOnly = false)
        {
            return new StateEntry(name, type, DefaultValue: null, readOnly, MemberId: StableMemberId(name));
        }

        public static CommandEntry Command(params string[] parameterNames)
        {
            return Command("command", parameterNames);
        }

        public static CommandEntry Command(string name, params string[] parameterNames)
        {
            ImmutableArray<ParameterEntry> parameters = parameterNames
                .Select(static parameter => new ParameterEntry(parameter, "int"))
                .ToImmutableArray();
            return new CommandEntry(name, parameters, CommandId: StableMemberId(name));
        }

        public static EffectEntry Effect(string name, string payloadType)
        {
            ImmutableArray<ParameterEntry> parameters = payloadType == "void"
                ? ImmutableArray<ParameterEntry>.Empty
                : ImmutableArray.Create(new ParameterEntry("payload", payloadType));
            return new EffectEntry(name, payloadType, EffectId: StableMemberId(name), parameters);
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

        public static BuildResult SuccessfulBuildResultWithSchema(string schemaPath, TimeSpan? duration = null)
        {
            return SuccessfulBuildResult() with
            {
                Artifacts = new BuildArtifacts
                {
                    SchemaFiles = ImmutableArray.Create(schemaPath),
                },
                Stats = new BuildStats(
                    duration ?? TimeSpan.FromMilliseconds(1),
                    FilesCompiled: 1,
                    SchemasGenerated: 1,
                    CppFilesGenerated: 0,
                    AssetsCollected: 0,
                    NativeLibBuilt: false),
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

        private static int StableMemberId(string value)
        {
            int hash = 17;
            foreach (char character in value)
            {
                hash = (hash * 31) + character;
            }

            return hash < 0 ? -hash : hash;
        }
    }
}
