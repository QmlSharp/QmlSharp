using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QmlSharp.Compiler.Tests.Fixtures;
using QmlSharp.Qml.Emitter;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Compiler.Tests.Incremental
{
    public sealed class IncrementalCompilerTests
    {
        private static readonly IRegistryQuery Registry = RegistryFixture.CreateQtQuickAndControlsRegistry();

        [Fact]
        public void IncrementalCompiler_IC01_FirstCompileMarksAllFilesDirtyAndPopulatesGraph()
        {
            using ProjectContext context = CreateContext();
            IncrementalCompiler incrementalCompiler = CreateIncrementalCompiler(out CountingCompiler compiler);

            ImmutableArray<string> dirtyFiles = incrementalCompiler.GetDirtyFiles(context);
            CompilationResult result = incrementalCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);

            Assert.Equal(SourceFileNames(), dirtyFiles);
            Assert.True(result.Success);
            Assert.Equal(2, result.Units.Length);
            Assert.Equal(2, compiler.CompileFileCalls);
            Assert.Equal(new[] { "CounterViewModel" }, incrementalCompiler.GetDependencyGraph().GetDependenciesOf("CounterView.cs"));
            Assert.Equal(new[] { "CounterView.cs" }, incrementalCompiler.GetDependencyGraph().GetDependentsOf("CounterViewModel"));
        }

        [Fact]
        public void IncrementalCompiler_IC02_NoChangeCompileUsesCachedUnits()
        {
            using ProjectContext context = CreateContext();
            IncrementalCompiler incrementalCompiler = CreateIncrementalCompiler(out CountingCompiler compiler);
            _ = incrementalCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);

            ImmutableArray<string> dirtyFiles = incrementalCompiler.GetDirtyFiles(context);
            CompilationResult result = incrementalCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);

            Assert.Empty(dirtyFiles);
            Assert.True(result.Success);
            Assert.Equal(2, result.Units.Length);
            Assert.Equal(2, compiler.CompileFileCalls);
            Assert.All(result.Units, static unit => Assert.False(string.IsNullOrWhiteSpace(unit.QmlText)));
        }

        [Fact]
        public void IncrementalCompiler_IC03_SingleViewFileChangeInvalidatesOnlyThatView()
        {
            using ProjectContext initialContext = CreateContext();
            IncrementalCompiler incrementalCompiler = CreateIncrementalCompiler(out CountingCompiler compiler);
            _ = incrementalCompiler.CompileIncremental(initialContext, CompilerTestFixtures.DefaultOptions);
            using ProjectContext changedContext = CreateContext(("CounterView.cs", CompilerSourceFixtures.CounterViewSource.Replace("\"+\"", "\"++\"", StringComparison.Ordinal)));

            ImmutableArray<string> dirtyFiles = incrementalCompiler.GetDirtyFiles(changedContext);
            CompilationResult result = incrementalCompiler.CompileIncremental(changedContext, CompilerTestFixtures.DefaultOptions);

            Assert.Equal(new[] { "CounterView.cs" }, dirtyFiles);
            Assert.Equal(3, compiler.CompileFileCalls);
            CompilationUnit counterUnit = result.Units.Single(static unit => unit.ViewClassName == "CounterView");
            Assert.Contains("++", counterUnit.QmlText, StringComparison.Ordinal);
        }

        [Fact]
        public void IncrementalCompiler_IC04_ViewModelSchemaChangeInvalidatesDependentView()
        {
            using ProjectContext initialContext = CreateContext();
            IncrementalCompiler incrementalCompiler = CreateIncrementalCompiler(out CountingCompiler compiler);
            _ = incrementalCompiler.CompileIncremental(initialContext, CompilerTestFixtures.DefaultOptions);
            string changedViewModel = CompilerSourceFixtures.CounterViewModelSource.Replace(
                "[State] public int Count { get; set; }",
                "[State] public int Count { get; set; }\n    [State] public bool Enabled { get; set; }",
                StringComparison.Ordinal);
            using ProjectContext changedContext = CreateContext(("CounterViewModel.cs", changedViewModel));

            ImmutableArray<string> dirtyFiles = incrementalCompiler.GetDirtyFiles(changedContext);
            CompilationResult result = incrementalCompiler.CompileIncremental(changedContext, CompilerTestFixtures.DefaultOptions);

            Assert.Equal(new[] { "CounterView.cs", "CounterViewModel.cs" }, dirtyFiles);
            Assert.Equal(3, compiler.CompileFileCalls);
            CompilationUnit counterUnit = result.Units.Single(static unit => unit.ViewClassName == "CounterView");
            Assert.Contains(counterUnit.Schema!.Properties, static property => property.Name == "enabled");
        }

        [Fact]
        public void IncrementalCompiler_QmlTsSchemaStableViewModelEditDoesNotInvalidateDependentView()
        {
            using ProjectContext initialContext = CreateContext();
            IncrementalCompiler incrementalCompiler = CreateIncrementalCompiler(out CountingCompiler compiler);
            _ = incrementalCompiler.CompileIncremental(initialContext, CompilerTestFixtures.DefaultOptions);
            string changedViewModel = CompilerSourceFixtures.CounterViewModelSource.Replace("Count++;", "Count += 1;", StringComparison.Ordinal);
            using ProjectContext changedContext = CreateContext(("CounterViewModel.cs", changedViewModel));

            ImmutableArray<string> dirtyFiles = incrementalCompiler.GetDirtyFiles(changedContext);
            CompilationResult result = incrementalCompiler.CompileIncremental(changedContext, CompilerTestFixtures.DefaultOptions);

            Assert.Equal(new[] { "CounterViewModel.cs" }, dirtyFiles);
            Assert.Equal(2, compiler.CompileFileCalls);
            Assert.True(result.Success);
        }

        [Fact]
        public void IncrementalCompiler_IC06_ExplicitInvalidatePropagatesToDependents()
        {
            using ProjectContext context = CreateContext();
            IncrementalCompiler incrementalCompiler = CreateIncrementalCompiler(out CountingCompiler compiler);
            _ = incrementalCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);

            incrementalCompiler.Invalidate(ImmutableArray.Create("CounterViewModel.cs"));
            ImmutableArray<string> dirtyFiles = incrementalCompiler.GetDirtyFiles(context);
            _ = incrementalCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);

            Assert.Equal(new[] { "CounterView.cs", "CounterViewModel.cs" }, dirtyFiles);
            Assert.Equal(3, compiler.CompileFileCalls);
        }

        [Fact]
        public void IncrementalCompiler_IC07_SaveCacheAndLoadCacheRoundTripsAcrossInstances()
        {
            using ProjectContext context = CreateContext();
            using TempOutputDirectory temp = new();
            IncrementalCompiler firstCompiler = CreateIncrementalCompiler(out _);
            _ = firstCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);
            firstCompiler.SaveCache(temp.Path);
            IncrementalCompiler secondCompiler = CreateIncrementalCompiler(out CountingCompiler compiler);
            secondCompiler.LoadCache(temp.Path);

            ImmutableArray<string> dirtyFiles = secondCompiler.GetDirtyFiles(context);
            CompilationResult result = secondCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);

            Assert.Empty(dirtyFiles);
            Assert.True(result.Success);
            Assert.Equal(0, compiler.CompileFileCalls);
            Assert.Equal(new[] { "TodoView.cs" }, secondCompiler.GetDependencyGraph().GetDependentsOf("TodoViewModel"));
        }

        [Fact]
        public void IncrementalCompiler_IC08_ClearCacheMakesAllFilesDirty()
        {
            using ProjectContext context = CreateContext();
            IncrementalCompiler incrementalCompiler = CreateIncrementalCompiler(out _);
            _ = incrementalCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);

            incrementalCompiler.ClearCache();
            ImmutableArray<string> dirtyFiles = incrementalCompiler.GetDirtyFiles(context);

            Assert.Equal(SourceFileNames(), dirtyFiles);
        }

        [Fact]
        public void IncrementalCompiler_OptionChangeInvalidatesCache()
        {
            using ProjectContext context = CreateContext();
            IncrementalCompiler incrementalCompiler = CreateIncrementalCompiler(out CountingCompiler compiler);
            _ = incrementalCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);
            CompilerOptions changedOptions = CompilerTestFixtures.DefaultOptions with
            {
                MaxAllowedSeverity = DiagnosticSeverity.Error,
            };

            ImmutableArray<string> dirtyFiles = incrementalCompiler.GetDirtyFiles(new ProjectContext(
                context.Compilation,
                context.SourceFiles,
                changedOptions,
                new DiagnosticReporter()));
            _ = incrementalCompiler.CompileIncremental(context, changedOptions);

            Assert.Equal(SourceFileNames(), dirtyFiles);
            Assert.Equal(4, compiler.CompileFileCalls);
        }

        [Fact]
        public void IncrementalCompiler_SourceMapOptionChangeInvalidatesAffectedUnits()
        {
            using ProjectContext context = CreateContext();
            IncrementalCompiler incrementalCompiler = CreateIncrementalCompiler(out CountingCompiler compiler);
            _ = incrementalCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);
            CompilerOptions changedOptions = CompilerTestFixtures.DefaultOptions with
            {
                GenerateSourceMaps = false,
            };

            _ = incrementalCompiler.CompileIncremental(context, changedOptions);

            Assert.Equal(4, compiler.CompileFileCalls);
        }

        [Fact]
        public void IncrementalCompiler_ModuleUriAndVersionChangeInvalidatesSchemasAndViews()
        {
            using ProjectContext context = CreateContext();
            IncrementalCompiler incrementalCompiler = CreateIncrementalCompiler(out CountingCompiler compiler);
            _ = incrementalCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);
            CompilerOptions changedOptions = CompilerTestFixtures.DefaultOptions with
            {
                ModuleUriPrefix = "QmlSharp.OtherApp",
                ModuleVersion = new QmlVersion(2, 5),
            };

            CompilationResult result = incrementalCompiler.CompileIncremental(context, changedOptions);

            Assert.Equal(4, compiler.CompileFileCalls);
            Assert.All(result.Units, unit =>
            {
                Assert.Equal("QmlSharp.OtherApp", unit.Schema!.ModuleUri);
                Assert.Equal(new QmlVersion(2, 5), unit.Schema.ModuleVersion);
            });
        }

        [Fact]
        public void IncrementalCompiler_CacheFileFormatIsDeterministicAndVersioned()
        {
            using ProjectContext context = CreateContext();
            using TempOutputDirectory temp = new();
            IncrementalCompiler incrementalCompiler = CreateIncrementalCompiler(out _);
            _ = incrementalCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);

            incrementalCompiler.SaveCache(temp.Path);
            string first = File.ReadAllText(Path.Join(temp.Path, "incremental-cache.json"));
            incrementalCompiler.SaveCache(temp.Path);
            string second = File.ReadAllText(Path.Join(temp.Path, "incremental-cache.json"));

            Assert.Equal(first, second);
            Assert.StartsWith("{\n  \"cacheVersion\": \"1.0\"", first, StringComparison.Ordinal);
            Assert.Contains("\"dependencies\": [", first, StringComparison.Ordinal);
        }

        [Fact]
        public void IncrementalCompiler_CacheVersionMismatchClearsLoadedCache()
        {
            using ProjectContext context = CreateContext();
            using TempOutputDirectory temp = new();
            IncrementalCompiler firstCompiler = CreateIncrementalCompiler(out _);
            _ = firstCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);
            firstCompiler.SaveCache(temp.Path);
            string cachePath = Path.Join(temp.Path, "incremental-cache.json");
            File.WriteAllText(
                cachePath,
                File.ReadAllText(cachePath).Replace("\"cacheVersion\": \"1.0\"", "\"cacheVersion\": \"0.0\"", StringComparison.Ordinal));
            IncrementalCompiler secondCompiler = CreateIncrementalCompiler(out _);

            secondCompiler.LoadCache(temp.Path);
            ImmutableArray<string> dirtyFiles = secondCompiler.GetDirtyFiles(context);

            Assert.Equal(SourceFileNames(), dirtyFiles);
        }

        [Fact]
        public void IncrementalCompiler_CachedFailedUnitRestoresDiagnostics()
        {
            using ProjectContext context = CreateContext();
            using TempOutputDirectory temp = new();
            DiagnosticCompiler firstInnerCompiler = new();
            IncrementalCompiler firstCompiler = CreateIncrementalCompiler(firstInnerCompiler);
            CompilationResult firstResult = firstCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);
            firstCompiler.SaveCache(temp.Path);
            DiagnosticCompiler secondInnerCompiler = new();
            IncrementalCompiler secondCompiler = CreateIncrementalCompiler(secondInnerCompiler);
            secondCompiler.LoadCache(temp.Path);

            CompilationResult secondResult = secondCompiler.CompileIncremental(context, CompilerTestFixtures.DefaultOptions);

            Assert.False(firstResult.Success);
            Assert.False(secondResult.Success);
            Assert.Equal(2, firstInnerCompiler.CompileFileCalls);
            Assert.Equal(0, secondInnerCompiler.CompileFileCalls);
            CompilerDiagnostic diagnostic = Assert.Single(secondResult.Diagnostics);
            Assert.Equal(DiagnosticCodes.EmitFailed, diagnostic.Code);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("CounterView.cs", diagnostic.Location?.FilePath);
        }

        [Fact]
        public void IncrementalCompiler_MalformedCacheMissingRequiredPropertyClearsCache()
        {
            using ProjectContext context = CreateContext();
            using TempOutputDirectory temp = new();
            string cachePath = Path.Join(temp.Path, "incremental-cache.json");
            File.WriteAllText(
                cachePath,
                "{\n  \"cacheVersion\": \"1.0\",\n  \"units\": []\n}\n");
            IncrementalCompiler incrementalCompiler = CreateIncrementalCompiler(out _);

            incrementalCompiler.LoadCache(temp.Path);
            ImmutableArray<string> dirtyFiles = incrementalCompiler.GetDirtyFiles(context);

            Assert.Equal(SourceFileNames(), dirtyFiles);
        }

        private static IncrementalCompiler CreateIncrementalCompiler(out CountingCompiler countingCompiler)
        {
            ICompiler innerCompiler = new QmlSharp.Compiler.QmlCompiler(
                new StaticAnalyzer(),
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
            countingCompiler = new CountingCompiler(innerCompiler);
            return CreateIncrementalCompiler(countingCompiler);
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

        private static ProjectContext CreateContext(params (string FileName, string Source)[] replacements)
        {
            Dictionary<string, string> replacementMap = replacements.ToDictionary(
                static replacement => replacement.FileName,
                static replacement => replacement.Source,
                StringComparer.Ordinal);
            ImmutableArray<(string FileName, string Source)> sources = CompilerSourceFixtures.MultiFileProject()
                .Select(source => replacementMap.TryGetValue(source.FileName, out string? replacement)
                    ? (source.FileName, replacement)
                    : source)
                .ToImmutableArray();
            CSharpCompilation compilation = RoslynTestHelper.CreateCompilation(sources);
            return new ProjectContext(
                compilation,
                sources.Select(static source => source.FileName).ToImmutableArray(),
                CompilerTestFixtures.DefaultOptions,
                new DiagnosticReporter());
        }

        private static string[] SourceFileNames()
        {
            return new[] { "CounterView.cs", "CounterViewModel.cs", "TodoView.cs", "TodoViewModel.cs" };
        }

        private sealed class CountingCompiler : ICompiler
        {
            private readonly ICompiler innerCompiler;

            public CountingCompiler(ICompiler innerCompiler)
            {
                this.innerCompiler = innerCompiler;
            }

            public int CompileFileCalls { get; private set; }

            public CompilationResult Compile(CompilerOptions options)
            {
                return innerCompiler.Compile(options);
            }

            public CompilationUnit CompileFile(string filePath, ProjectContext context, CompilerOptions options)
            {
                CompileFileCalls++;
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

        private sealed class StaticAnalyzer : ICSharpAnalyzer
        {
            private readonly CSharpAnalyzer innerAnalyzer = new();

            public ProjectContext CreateProjectContext(CompilerOptions options)
            {
                throw new NotSupportedException("Incremental tests call CompileFile with an existing ProjectContext.");
            }

            public ImmutableArray<DiscoveredView> DiscoverViews(ProjectContext context)
            {
                return innerAnalyzer.DiscoverViews(context);
            }

            public ImmutableArray<DiscoveredViewModel> DiscoverViewModels(ProjectContext context)
            {
                return innerAnalyzer.DiscoverViewModels(context);
            }

            public ImmutableArray<DiscoveredImport> DiscoverImports(ProjectContext context, string filePath)
            {
                return innerAnalyzer.DiscoverImports(context, filePath);
            }

            public SemanticModel GetSemanticModel(ProjectContext context, string filePath)
            {
                return innerAnalyzer.GetSemanticModel(context, filePath);
            }
        }

        private sealed class DiagnosticCompiler : ICompiler
        {
            public int CompileFileCalls { get; private set; }

            public CompilationResult Compile(CompilerOptions options)
            {
                throw new NotSupportedException("Incremental tests call CompileFile with an existing ProjectContext.");
            }

            public CompilationUnit CompileFile(string filePath, ProjectContext context, CompilerOptions options)
            {
                CompileFileCalls++;
                string viewClassName = Path.GetFileNameWithoutExtension(filePath);
                string viewModelClassName = StringComparer.Ordinal.Equals(viewClassName, "CounterView")
                    ? "CounterViewModel"
                    : "TodoViewModel";
                ImmutableArray<CompilerDiagnostic> diagnostics = StringComparer.Ordinal.Equals(viewClassName, "CounterView")
                    ? ImmutableArray.Create(new CompilerDiagnostic(
                        DiagnosticCodes.EmitFailed,
                        DiagnosticSeverity.Error,
                        "Intentional cached diagnostic.",
                        SourceLocation.FileOnly(filePath),
                        "incremental-test"))
                    : ImmutableArray<CompilerDiagnostic>.Empty;

                return new CompilationUnit
                {
                    SourceFilePath = filePath,
                    ViewClassName = viewClassName,
                    ViewModelClassName = viewModelClassName,
                    QmlText = $"{viewClassName} {{}}\n",
                    Diagnostics = diagnostics,
                };
            }

            public OutputResult WriteOutput(CompilationResult result, CompilerOptions options)
            {
                throw new NotSupportedException("Incremental tests do not write output.");
            }

            public void OnProgress(Action<CompilationProgress> callback)
            {
                ArgumentNullException.ThrowIfNull(callback);
            }
        }
    }
}
