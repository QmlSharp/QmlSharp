using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QmlSharp.Compiler.Tests.Fixtures;
using QmlSharp.Qml.Emitter;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Compiler.Tests.Performance
{
    public sealed class CompilerPerformanceTests
    {
        private static readonly IRegistryQuery Registry = RegistryFixture.CreateQtQuickAndControlsRegistry();

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public void PF01_SingleFileFirstCompile_StaysUnderBudget()
        {
            WarmUpCompilerRuntime();
            using ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            ICompiler compiler = CreateCompiler(context);

            TimeSpan elapsed = MeasureBestOf(3, () =>
            {
                CompilationResult result = compiler.Compile(CompilerTestFixtures.DefaultOptions);
                Assert.True(result.Success);
                _ = Assert.Single(result.Units);
            });

            AssertUnderBudget("PF-01 single file first compile", elapsed, TimeSpan.FromMilliseconds(750));
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public void PF02_SingleFileIncrementalNoChange_StaysUnderBudget()
        {
            WarmUpCompilerRuntime();
            using ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            IncrementalCompiler incrementalCompiler = CreateIncrementalCompiler(new ProjectContextStaticCompiler(context));
            _ = incrementalCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);

            TimeSpan elapsed = MeasureBestOf(3, () =>
            {
                CompilationResult result = incrementalCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);
                Assert.True(result.Success);
                _ = Assert.Single(result.Units);
            });

            AssertUnderBudget("PF-02 single file incremental no-change", elapsed, TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public void PF03_TenFileProjectFullCompile_StaysUnderBudget()
        {
            WarmUpCompilerRuntime();
            using ProjectContext context = CompilerTestFixtures.CreateNFileContext(10);
            ICompiler compiler = CreateCompiler(context);

            TimeSpan elapsed = Measure(() =>
            {
                CompilationResult result = compiler.Compile(CompilerTestFixtures.CreateNFileOptions(10));
                Assert.True(result.Success);
                Assert.Equal(10, result.Units.Length);
            });

            AssertUnderBudget("PF-03 10-file full compile", elapsed, TimeSpan.FromSeconds(3));
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public void PF04_FiftyFileProjectFullCompile_StaysUnderBudget()
        {
            WarmUpCompilerRuntime();
            using ProjectContext context = CompilerTestFixtures.CreateNFileContext(50);
            ICompiler compiler = CreateCompiler(context);

            TimeSpan elapsed = Measure(() =>
            {
                CompilationResult result = compiler.Compile(CompilerTestFixtures.CreateNFileOptions(50));
                Assert.True(result.Success);
                Assert.Equal(50, result.Units.Length);
            });

            AssertUnderBudget("PF-04 50-file full compile", elapsed, GetFiftyFileFullCompileBudget());
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public async Task PF05_WatchModeIncrementalUpdate_StaysUnderBudget()
        {
            TestFileWatcherFactory fileWatcherFactory = new();
            FastIncrementalCompiler incrementalCompiler = new();
            FastOutputCompiler outputCompiler = new();
            CompilerWatcher watcher = new(
                outputCompiler,
                incrementalCompiler,
                new FastAnalyzer(),
                fileWatcherFactory,
                TimeSpan.Zero);
            CallbackProbe callbacks = new(targetCount: 2);
            await watcher.StartAsync(CompilerTestFixtures.DefaultOptions, callbacks.Record);

            Stopwatch stopwatch = Stopwatch.StartNew();
            fileWatcherFactory.FileWatcher.RaiseChanged("CounterView.cs");
            await callbacks.WaitAsync();
            stopwatch.Stop();
            await watcher.StopAsync();

            Assert.Equal(2, incrementalCompiler.CompileCalls);
            AssertUnderBudget("PF-05 watch mode incremental update", stopwatch.Elapsed, TimeSpan.FromMilliseconds(200));
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public void PF06_SingleSchemaJsonSerialization_StaysUnderBudget()
        {
            ViewModelSchemaSerializer serializer = new();
            ViewModelSchema schema = CompilerTestFixtures.CreateTodoSchema();
            _ = serializer.Serialize(schema);

            TimeSpan elapsed = MeasureBestOf(5, () =>
            {
                string json = serializer.Serialize(schema);
                Assert.Contains("\"className\": \"TodoViewModel\"", json, StringComparison.Ordinal);
            });

            AssertUnderBudget("PF-06 schema JSON serialization", elapsed, TimeSpan.FromMilliseconds(5));
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public void PF07_SingleFileSourceMapGeneration_StaysUnderBudget()
        {
            SourceMapManager manager = new();
            _ = BuildSourceMapJson(manager);

            TimeSpan elapsed = MeasureBestOf(5, () =>
            {
                string json = BuildSourceMapJson(manager);
                Assert.Contains("\"mappings\"", json, StringComparison.Ordinal);
            });

            AssertUnderBudget("PF-07 source map generation", elapsed, TimeSpan.FromMilliseconds(10));
        }

        private static void WarmUpCompilerRuntime()
        {
            using ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            CompilationResult result = CreateCompiler(context).Compile(CompilerTestFixtures.DefaultOptions);
            Assert.True(result.Success);
        }

        private static IncrementalCompiler CreateIncrementalCompiler(ICompiler compiler)
        {
            return new IncrementalCompiler(
                compiler,
                new CSharpAnalyzer(),
                new ViewModelExtractor(),
                static () => new IdAllocator(),
                new EventBindingsBuilder(),
                new ViewModelSchemaSerializer(),
                new SourceMapManager());
        }

        private static ICompiler CreateCompiler(ProjectContext context)
        {
            return new QmlSharp.Compiler.QmlCompiler(
                new StaticAnalyzer(context),
                new ViewModelExtractor(),
                static () => new IdAllocator(),
                new DslTransformer(),
                new ImportResolver(),
                new PostProcessor(),
                new QmlEmitter(),
                Registry,
                new SourceMapManager(),
                new EventBindingsBuilder(),
                new CompilerOutputWriter());
        }

        private static string BuildSourceMapJson(SourceMapManager manager)
        {
            ISourceMapBuilder builder = manager.CreateBuilder("CounterView.cs", "CounterView.qml");
            for (int index = 1; index <= 50; index++)
            {
                builder.AddMapping(new SourceMapMapping(
                    outputLine: index,
                    outputColumn: 1,
                    sourceFilePath: "CounterView.cs",
                    sourceLine: index + 10,
                    sourceColumn: 5,
                    symbol: $"Symbol{index}",
                    nodeKind: "Binding"));
            }

            return manager.Serialize(builder.Build());
        }

        private static TimeSpan Measure(Action action)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        private static TimeSpan MeasureBestOf(int iterations, Action action)
        {
            TimeSpan best = TimeSpan.MaxValue;
            for (int index = 0; index < iterations; index++)
            {
                TimeSpan elapsed = Measure(action);
                if (elapsed < best)
                {
                    best = elapsed;
                }
            }

            return best;
        }

        private static TimeSpan GetFiftyFileFullCompileBudget()
        {
            return IsWindowsContinuousIntegration()
                ? TimeSpan.FromSeconds(30)
                : TimeSpan.FromSeconds(10);
        }

        private static bool IsWindowsContinuousIntegration()
        {
            return OperatingSystem.IsWindows()
                && string.Equals(
                    Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
                    "true",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void AssertUnderBudget(string operation, TimeSpan elapsed, TimeSpan budget)
        {
            Assert.True(
                elapsed <= budget,
                $"{operation} took {elapsed.TotalMilliseconds:0.###} ms; budget is {budget.TotalMilliseconds:0.###} ms.");
        }

        private sealed class StaticAnalyzer : ICSharpAnalyzer
        {
            private readonly ProjectContext projectContext;
            private readonly CSharpAnalyzer inner = new();

            public StaticAnalyzer(ProjectContext context)
            {
                projectContext = context;
            }

            public ProjectContext CreateProjectContext(CompilerOptions options)
            {
                ArgumentNullException.ThrowIfNull(options);
                return projectContext;
            }

            public ImmutableArray<DiscoveredView> DiscoverViews(ProjectContext context)
            {
                return inner.DiscoverViews(context);
            }

            public ImmutableArray<DiscoveredViewModel> DiscoverViewModels(ProjectContext context)
            {
                return inner.DiscoverViewModels(context);
            }

            public ImmutableArray<DiscoveredImport> DiscoverImports(ProjectContext context, string filePath)
            {
                return inner.DiscoverImports(context, filePath);
            }

            public SemanticModel GetSemanticModel(ProjectContext context, string filePath)
            {
                return inner.GetSemanticModel(context, filePath);
            }
        }

        private sealed class ProjectContextStaticCompiler : ICompiler
        {
            private readonly ICompiler innerCompiler;

            public ProjectContextStaticCompiler(ProjectContext context)
            {
                innerCompiler = CreateCompiler(context);
            }

            public CompilationResult Compile(CompilerOptions options)
            {
                return innerCompiler.Compile(options);
            }

            public CompilationUnit CompileFile(string filePath, ProjectContext context, CompilerOptions options)
            {
                return innerCompiler.CompileFile(filePath, context, options);
            }

            public OutputResult WriteOutput(CompilationResult result, CompilerOptions options)
            {
                return innerCompiler.WriteOutput(result, options);
            }

            public void OnProgress(Action<CompilationProgress> callback)
            {
                innerCompiler.OnProgress(callback);
            }
        }

        private sealed class TestFileWatcherFactory : ICompilerFileWatcherFactory
        {
            public TestFileWatcher FileWatcher { get; } = new();

            public ICompilerFileWatcher Create(CompilerOptions options)
            {
                ArgumentNullException.ThrowIfNull(options);
                return FileWatcher;
            }
        }

        private sealed class TestFileWatcher : ICompilerFileWatcher
        {
            public event EventHandler<CompilerFileChangedEventArgs>? Changed;

            public event EventHandler<CompilerWatcherErrorEventArgs>? Error;

            public void Start()
            {
            }

            public void Stop()
            {
            }

            public void Dispose()
            {
            }

            public void RaiseChanged(string filePath)
            {
                Changed?.Invoke(this, new CompilerFileChangedEventArgs(filePath, CompilerFileChangeKind.Changed));
            }

            public void RaiseError(Exception exception)
            {
                Error?.Invoke(this, new CompilerWatcherErrorEventArgs(exception));
            }
        }

        private sealed class FastAnalyzer : ICSharpAnalyzer
        {
            public ProjectContext CreateProjectContext(CompilerOptions options)
            {
                ArgumentNullException.ThrowIfNull(options);
                CSharpCompilation compilation = CSharpCompilation.Create(
                    "PerformanceWatcher",
                    new[] { CSharpSyntaxTree.ParseText("public sealed class InitialView { }", path: "InitialView.cs") });
                return new ProjectContext(
                    compilation,
                    ImmutableArray.Create("InitialView.cs"),
                    options,
                    new DiagnosticReporter());
            }

            public ImmutableArray<DiscoveredView> DiscoverViews(ProjectContext context)
            {
                ArgumentNullException.ThrowIfNull(context);
                return ImmutableArray<DiscoveredView>.Empty;
            }

            public ImmutableArray<DiscoveredViewModel> DiscoverViewModels(ProjectContext context)
            {
                ArgumentNullException.ThrowIfNull(context);
                return ImmutableArray<DiscoveredViewModel>.Empty;
            }

            public ImmutableArray<DiscoveredImport> DiscoverImports(ProjectContext context, string filePath)
            {
                ArgumentNullException.ThrowIfNull(context);
                ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
                return ImmutableArray<DiscoveredImport>.Empty;
            }

            public SemanticModel GetSemanticModel(ProjectContext context, string filePath)
            {
                ArgumentNullException.ThrowIfNull(context);
                ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
                return context.Compilation.GetSemanticModel(context.Compilation.SyntaxTrees.Single());
            }
        }

        private sealed class FastIncrementalCompiler : IIncrementalCompiler
        {
            private readonly DependencyGraph dependencyGraph = new();
            private ImmutableArray<string> invalidatedFiles = ImmutableArray<string>.Empty;

            public int CompileCalls { get; private set; }

            public CompilationResult CompileIncremental(ProjectContext context, CompilerOptions options)
            {
                ArgumentNullException.ThrowIfNull(context);
                ArgumentNullException.ThrowIfNull(options);
                CompileCalls++;
                string sourceFilePath = invalidatedFiles.IsDefaultOrEmpty ? "InitialView.cs" : invalidatedFiles[0];
                return CompilationResult.FromUnits(ImmutableArray.Create(new CompilationUnit
                {
                    SourceFilePath = sourceFilePath,
                    ViewClassName = Path.GetFileNameWithoutExtension(sourceFilePath),
                    ViewModelClassName = $"{Path.GetFileNameWithoutExtension(sourceFilePath)}Model",
                    QmlText = $"{Path.GetFileNameWithoutExtension(sourceFilePath)} {{}}\n",
                }));
            }

            public ImmutableArray<string> GetDirtyFiles(ProjectContext context)
            {
                ArgumentNullException.ThrowIfNull(context);
                return invalidatedFiles;
            }

            public DependencyGraph GetDependencyGraph()
            {
                return dependencyGraph;
            }

            public void Invalidate(ImmutableArray<string> filePaths)
            {
                invalidatedFiles = filePaths.IsDefault ? ImmutableArray<string>.Empty : filePaths;
            }

            public void ClearCache()
            {
                invalidatedFiles = ImmutableArray<string>.Empty;
            }

            public void SaveCache(string cacheDir)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(cacheDir);
            }

            public void LoadCache(string cacheDir)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(cacheDir);
            }
        }

        private sealed class FastOutputCompiler : ICompiler
        {
            public CompilationResult Compile(CompilerOptions options)
            {
                throw new NotSupportedException("Watcher performance tests use incremental compilation.");
            }

            public CompilationUnit CompileFile(string filePath, ProjectContext context, CompilerOptions options)
            {
                throw new NotSupportedException("Watcher performance tests use incremental compilation.");
            }

            public OutputResult WriteOutput(CompilationResult result, CompilerOptions options)
            {
                ArgumentNullException.ThrowIfNull(result);
                ArgumentNullException.ThrowIfNull(options);
                return new OutputResult(
                    ImmutableArray.Create(Path.Join(options.OutputDir, "InitialView.qml")),
                    ImmutableArray<string>.Empty,
                    Path.Join(options.OutputDir, "event-bindings.json"),
                    ImmutableArray<string>.Empty,
                    32);
            }

            public void OnProgress(Action<CompilationProgress> callback)
            {
                ArgumentNullException.ThrowIfNull(callback);
            }
        }

        private sealed class CallbackProbe
        {
            private readonly int targetCount;
            private readonly TaskCompletionSource<object?> completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public CallbackProbe(int targetCount)
            {
                this.targetCount = targetCount;
            }

            public void Record(CompilationResult result)
            {
                ArgumentNullException.ThrowIfNull(result);
                if (Interlocked.Increment(ref observedCount) >= targetCount)
                {
                    _ = completed.TrySetResult(null);
                }
            }

            private int observedCount;

            public Task WaitAsync()
            {
                return completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }
}
