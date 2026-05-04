using BuildQmlVersion = QmlSharp.Build.QmlVersion;
using CompilerQmlVersion = QmlSharp.Compiler.QmlVersion;

namespace QmlSharp.Build.Tests.Infrastructure
{
    public static class BuildTestFixtures
    {
        public const string FileSystemDecision = "Use test-isolated real filesystem roots created under the OS temp directory.";

        public static QmlSharpConfig CreateDefaultConfig()
        {
            return new QmlSharpConfig
            {
                Entry = "./src/Program.cs",
                Qt = new QtConfig
                {
                    Dir = "C:/Qt/6.11.0/msvc2022_64",
                },
                Module = new ModuleConfig
                {
                    Prefix = "QmlSharp.MyApp",
                    Version = new BuildQmlVersion(1, 0),
                },
                Name = "MyApp",
                Version = "1.0.0",
            };
        }

        public static BuildContext CreateDefaultContext(string? projectDir = null)
        {
            QmlSharpConfig config = CreateDefaultConfig();
            string effectiveProjectDir = projectDir ?? Directory.GetCurrentDirectory();

            return new BuildContext
            {
                Config = config,
                ProjectDir = effectiveProjectDir,
                OutputDir = System.IO.Path.Combine(effectiveProjectDir, "dist"),
                QtDir = config.Qt.Dir ?? "C:/Qt/6.11.0/msvc2022_64",
            };
        }

        public static ViewModelSchema CreateCounterSchema()
        {
            return new ViewModelSchema(
                "1.0",
                "CounterViewModel",
                "MyApp",
                "QmlSharp.MyApp",
                new CompilerQmlVersion(1, 0),
                1,
                "CounterView::__qmlsharp_vm0",
                ImmutableArray.Create(new StateEntry("count", "int", "0", false, 303501554)),
                ImmutableArray.Create(new CommandEntry("increment", ImmutableArray<ParameterEntry>.Empty, 2140087481)),
                ImmutableArray.Create(new EffectEntry("showToast", "string", 1633635556, ImmutableArray<ParameterEntry>.Empty)),
                new LifecycleInfo(true, true, true));
        }

        public static ImmutableArray<ViewModelSchema> CreateNSchemas(int count)
        {
            ImmutableArray<ViewModelSchema>.Builder builder = ImmutableArray.CreateBuilder<ViewModelSchema>(count);
            for (int index = 0; index < count; index++)
            {
                ViewModelSchema schema = CreateCounterSchema() with
                {
                    ClassName = $"Counter{index}ViewModel",
                    CompilerSlotKey = $"Counter{index}View::__qmlsharp_vm0",
                };
                builder.Add(schema);
            }

            return builder.ToImmutable();
        }

        public static CppGenerationOptions CreateDefaultCppOptions(string? outputDir = null)
        {
            string effectiveOutputDir = outputDir ?? System.IO.Path.Combine(Directory.GetCurrentDirectory(), "dist");
            return new CppGenerationOptions
            {
                OutputDir = effectiveOutputDir,
                QtDir = "C:/Qt/6.11.0/msvc2022_64",
                AbiSourceDir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "native"),
            };
        }

        public static TempDirectory CreateFixtureProject(string testName)
        {
            TempDirectory directory = new($"qmlsharp-build-{testName}");
            _ = Directory.CreateDirectory(System.IO.Path.Combine(directory.Path, "src"));
            _ = Directory.CreateDirectory(System.IO.Path.Combine(directory.Path, "assets"));
            return directory;
        }

        public static TempDirectory CreateMockNuGetLayout(
            params (string PackageId, string Version, string? Manifest)[] packages)
        {
            TempDirectory directory = new("qmlsharp-build-nuget");
            foreach ((string packageId, string version, string? manifest) in packages)
            {
                string packageRoot = System.IO.Path.Combine(directory.Path, packageId, version);
                _ = Directory.CreateDirectory(packageRoot);
                if (manifest is not null)
                {
                    File.WriteAllText(System.IO.Path.Combine(packageRoot, "qmlsharp.module.json"), manifest);
                }
            }

            return directory;
        }

        public static BuildResult CreateSuccessfulBuildResult()
        {
            return new BuildResult
            {
                Success = true,
                PhaseResults = ImmutableArray<PhaseResult>.Empty,
                Diagnostics = ImmutableArray<BuildDiagnostic>.Empty,
                Stats = new BuildStats(TimeSpan.Zero, 0, 0, 0, 0, false),
            };
        }

        public static string FindRepositoryRoot()
        {
            DirectoryInfo? current = new(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(System.IO.Path.Combine(current.FullName, "QmlSharp.slnx")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root containing QmlSharp.slnx.");
        }
    }
}
