using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.Watcher
{
    public sealed class CompilerWatcherTests
    {
        private static readonly CompilerOptions Options = CompilerTestFixtures.DefaultOptions;

        [Fact]
        public async Task CompilerWatcher_StartAsyncRunsInitialCompileAndWritesOutput()
        {
            using TestHarness harness = new(TimeSpan.Zero);
            List<CompilationResult> results = [];

            await harness.Watcher.StartAsync(Options, results.Add);

            Assert.Equal(WatcherStatus.Watching, harness.Watcher.Status);
            Assert.True(harness.FileWatcher.Started);
            _ = Assert.Single(results);
            Assert.Equal(1, harness.IncrementalCompiler.CompileCalls);
            Assert.Equal(1, harness.Compiler.WriteOutputCalls);
        }

        [Fact]
        public async Task CompilerWatcher_CP09_FileChangeRecompilesAndInvokesCallback()
        {
            using TestHarness harness = new(TimeSpan.Zero);
            CallbackProbe callbacks = new(targetCount: 2);
            await harness.Watcher.StartAsync(Options, callbacks.Record);

            harness.FileWatcher.RaiseChanged("CounterView.cs");
            await callbacks.WaitAsync();

            Assert.Equal(WatcherStatus.Watching, harness.Watcher.Status);
            Assert.Equal(2, callbacks.Results.Count);
            Assert.Equal(2, harness.IncrementalCompiler.CompileCalls);
            Assert.Equal(2, harness.Compiler.WriteOutputCalls);
            Assert.Equal(new[] { "CounterView.cs" }, harness.IncrementalCompiler.Invalidations.Last());
        }

        [Fact]
        public async Task CompilerWatcher_CP10_InvalidatesOnlyChangedFileBeforeIncrementalCompile()
        {
            using TestHarness harness = new(TimeSpan.Zero);
            CallbackProbe callbacks = new(targetCount: 2);
            await harness.Watcher.StartAsync(Options, callbacks.Record);

            harness.FileWatcher.RaiseChanged("TodoView.cs");
            await callbacks.WaitAsync();

            CompilationResult changedResult = callbacks.Results.Last();
            CompilationUnit unit = Assert.Single(changedResult.Units);
            Assert.Equal("TodoView.cs", unit.SourceFilePath);
            Assert.Equal(new[] { "TodoView.cs" }, harness.IncrementalCompiler.Invalidations.Last());
        }

        [Fact]
        public async Task CompilerWatcher_DeletedFileInvalidatesAndRecompiles()
        {
            using TestHarness harness = new(TimeSpan.Zero);
            CallbackProbe callbacks = new(targetCount: 2);
            await harness.Watcher.StartAsync(Options, callbacks.Record);

            harness.FileWatcher.RaiseDeleted("CounterView.cs");
            await callbacks.WaitAsync();

            Assert.Equal(new[] { "CounterView.cs" }, harness.IncrementalCompiler.Invalidations.Last());
            Assert.Equal(2, harness.IncrementalCompiler.CompileCalls);
        }

        [Fact]
        public async Task CompilerWatcher_StopPreventsLaterCallbacksAndDisposesFileWatcher()
        {
            using TestHarness harness = new(TimeSpan.Zero);
            List<CompilationResult> results = [];
            await harness.Watcher.StartAsync(Options, results.Add);
            await harness.Watcher.StopAsync();

            harness.FileWatcher.RaiseChanged("CounterView.cs");

            Assert.Equal(WatcherStatus.Stopped, harness.Watcher.Status);
            _ = Assert.Single(results);
            Assert.Equal(1, harness.IncrementalCompiler.CompileCalls);
            Assert.Equal(1, harness.FileWatcher.StopCalls);
            Assert.Equal(1, harness.FileWatcher.DisposeCalls);
        }

        [Fact]
        public async Task CompilerWatcher_StopCancelsPendingDebounceWithoutCompiling()
        {
            using TestHarness harness = new(TimeSpan.FromHours(1));
            List<CompilationResult> results = [];
            await harness.Watcher.StartAsync(Options, results.Add);

            harness.FileWatcher.RaiseChanged("CounterView.cs");
            await harness.Watcher.StopAsync();

            _ = Assert.Single(results);
            Assert.Equal(1, harness.IncrementalCompiler.CompileCalls);
        }

        [Fact]
        public async Task CompilerWatcher_StartAsyncWithPreCanceledTokenDoesNotCreateWatcher()
        {
            using TestHarness harness = new(TimeSpan.Zero);
            using CancellationTokenSource cancellation = new();
            await cancellation.CancelAsync();

            _ = await Assert.ThrowsAsync<OperationCanceledException>(() => harness.Watcher.StartAsync(
                Options,
                cancellationToken: cancellation.Token));

            Assert.Equal(WatcherStatus.Idle, harness.Watcher.Status);
            Assert.Equal(0, harness.FileWatcherFactory.CreateCalls);
            Assert.False(harness.FileWatcher.Started);
        }

        [Fact]
        public async Task CompilerWatcher_ReplacedDebounceCompilesOnlyLatestChange()
        {
            using TestHarness harness = new(TimeSpan.FromMilliseconds(100));
            CallbackProbe callbacks = new(targetCount: 2);
            await harness.Watcher.StartAsync(Options, callbacks.Record);

            harness.FileWatcher.RaiseChanged("CounterView.cs");
            harness.FileWatcher.RaiseChanged("TodoView.cs");
            await callbacks.WaitAsync();

            Assert.Equal(2, harness.IncrementalCompiler.CompileCalls);
            Assert.Equal(new[] { "TodoView.cs" }, harness.IncrementalCompiler.Invalidations.Last());
            Assert.Equal("TodoView.cs", callbacks.Results.Last().Units.Single().SourceFilePath);
        }

        [Fact]
        public async Task CompilerWatcher_WatcherErrorInvokesErrorCallbackAndSetsErrorStatus()
        {
            using TestHarness harness = new(TimeSpan.Zero);
            TaskCompletionSource<Exception> error = CreateCompletionSource<Exception>();
            harness.Watcher.OnError(error.SetResult);
            await harness.Watcher.StartAsync(Options);

            InvalidOperationException expected = new("watcher failed");
            harness.FileWatcher.RaiseError(expected);
            Exception actual = await error.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Same(expected, actual);
            Assert.Equal(WatcherStatus.Error, harness.Watcher.Status);
        }

        [Fact]
        public async Task CompilerWatcher_CompileExceptionInvokesErrorCallback()
        {
            using TestHarness harness = new(TimeSpan.Zero);
            TaskCompletionSource<Exception> error = CreateCompletionSource<Exception>();
            harness.Watcher.OnError(error.SetResult);
            await harness.Watcher.StartAsync(Options);
            harness.IncrementalCompiler.NextException = new InvalidOperationException("incremental failed");

            harness.FileWatcher.RaiseChanged("CounterView.cs");
            Exception actual = await error.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal("incremental failed", actual.Message);
            Assert.Equal(WatcherStatus.Error, harness.Watcher.Status);
            Assert.Equal(1, harness.Compiler.WriteOutputCalls);
        }

        [Fact]
        public async Task CompilerWatcher_CancellationTokenStopsWatching()
        {
            using TestHarness harness = new(TimeSpan.Zero);
            using CancellationTokenSource cancellation = new();
            await harness.Watcher.StartAsync(Options, cancellationToken: cancellation.Token);

            cancellation.Cancel();
            _ = await harness.FileWatcher.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            harness.FileWatcher.RaiseChanged("CounterView.cs");

            Assert.Equal(WatcherStatus.Stopped, harness.Watcher.Status);
            Assert.Equal(1, harness.IncrementalCompiler.CompileCalls);
            Assert.Equal(1, harness.FileWatcher.DisposeCalls);
        }

        [Fact]
        public async Task CompilerWatcher_FailedCompilationDoesNotWriteOutputOrInvokeCompileCallback()
        {
            using TestHarness harness = new(TimeSpan.Zero);
            List<CompilationResult> results = [];
            await harness.Watcher.StartAsync(Options, results.Add);
            harness.IncrementalCompiler.NextResult = CreateResult("BrokenView.cs", success: false);

            harness.FileWatcher.RaiseChanged("BrokenView.cs");
            _ = await harness.IncrementalCompiler.CompileObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));

            _ = Assert.Single(results);
            Assert.Equal(2, harness.IncrementalCompiler.CompileCalls);
            Assert.Equal(1, harness.Compiler.WriteOutputCalls);
        }

        private static TaskCompletionSource<T> CreateCompletionSource<T>()
        {
            return new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static CompilationResult CreateResult(string sourceFilePath, bool success)
        {
            ImmutableArray<CompilerDiagnostic> diagnostics = success
                ? ImmutableArray<CompilerDiagnostic>.Empty
                : ImmutableArray.Create(new CompilerDiagnostic(
                    DiagnosticCodes.EmitFailed,
                    DiagnosticSeverity.Error,
                    "Intentional watcher test diagnostic.",
                    SourceLocation.FileOnly(sourceFilePath),
                    "watcher-test"));

            return CompilationResult.FromUnits(ImmutableArray.Create(new CompilationUnit
            {
                SourceFilePath = sourceFilePath,
                ViewClassName = Path.GetFileNameWithoutExtension(sourceFilePath),
                ViewModelClassName = $"{Path.GetFileNameWithoutExtension(sourceFilePath)}Model",
                QmlText = success ? $"{Path.GetFileNameWithoutExtension(sourceFilePath)} {{}}\n" : null,
                Diagnostics = diagnostics,
            }));
        }

        private sealed class TestHarness : IDisposable
        {
            private readonly FakeFileWatcherFactory fileWatcherFactory;

            public TestHarness(TimeSpan debounce)
            {
                Compiler = new FakeCompiler();
                Analyzer = new FakeAnalyzer();
                IncrementalCompiler = new FakeIncrementalCompiler();
                fileWatcherFactory = new FakeFileWatcherFactory();
                Watcher = new CompilerWatcher(
                    Compiler,
                    IncrementalCompiler,
                    Analyzer,
                    fileWatcherFactory,
                    debounce);
            }

            public CompilerWatcher Watcher { get; }

            public FakeCompiler Compiler { get; }

            public FakeAnalyzer Analyzer { get; }

            public FakeIncrementalCompiler IncrementalCompiler { get; }

            public FakeFileWatcherFactory FileWatcherFactory => fileWatcherFactory;

            public FakeFileWatcher FileWatcher => fileWatcherFactory.FileWatcher;

            public void Dispose()
            {
                Watcher.Dispose();
            }
        }

        private sealed class CallbackProbe
        {
            private readonly int targetCount;
            private readonly TaskCompletionSource<object?> completed = CreateCompletionSource<object?>();

            public CallbackProbe(int targetCount)
            {
                this.targetCount = targetCount;
            }

            public List<CompilationResult> Results { get; } = [];

            public void Record(CompilationResult result)
            {
                Results.Add(result);
                if (Results.Count >= targetCount)
                {
                    _ = completed.TrySetResult(null);
                }
            }

            public Task WaitAsync()
            {
                return completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        private sealed class FakeFileWatcherFactory : ICompilerFileWatcherFactory
        {
            public FakeFileWatcher FileWatcher { get; } = new();

            public int CreateCalls { get; private set; }

            public ICompilerFileWatcher Create(CompilerOptions options)
            {
                ArgumentNullException.ThrowIfNull(options);
                CreateCalls++;
                return FileWatcher;
            }
        }

        private sealed class FakeFileWatcher : ICompilerFileWatcher
        {
            public event EventHandler<CompilerFileChangedEventArgs>? Changed;

            public event EventHandler<CompilerWatcherErrorEventArgs>? Error;

            public bool Started { get; private set; }

            public int StopCalls { get; private set; }

            public int DisposeCalls { get; private set; }

            public TaskCompletionSource<object?> Disposed { get; } = CreateCompletionSource<object?>();

            public void Start()
            {
                Started = true;
            }

            public void Stop()
            {
                StopCalls++;
                Started = false;
            }

            public void Dispose()
            {
                DisposeCalls++;
                _ = Disposed.TrySetResult(null);
            }

            public void RaiseChanged(string filePath)
            {
                Changed?.Invoke(this, new CompilerFileChangedEventArgs(filePath, CompilerFileChangeKind.Changed));
            }

            public void RaiseDeleted(string filePath)
            {
                Changed?.Invoke(this, new CompilerFileChangedEventArgs(filePath, CompilerFileChangeKind.Deleted));
            }

            public void RaiseError(Exception exception)
            {
                Error?.Invoke(this, new CompilerWatcherErrorEventArgs(exception));
            }
        }

        private sealed class FakeCompiler : ICompiler
        {
            public int WriteOutputCalls { get; private set; }

            public CompilationResult Compile(CompilerOptions options)
            {
                throw new NotSupportedException("Compiler watcher tests use incremental compilation.");
            }

            public CompilationUnit CompileFile(string filePath, ProjectContext context, CompilerOptions options)
            {
                throw new NotSupportedException("Compiler watcher tests use incremental compilation.");
            }

            public OutputResult WriteOutput(CompilationResult result, CompilerOptions options)
            {
                ArgumentNullException.ThrowIfNull(result);
                ArgumentNullException.ThrowIfNull(options);
                WriteOutputCalls++;
                return new OutputResult(
                    ImmutableArray.Create(Path.Join(options.OutputDir, "CounterView.qml")),
                    ImmutableArray.Create(Path.Join(options.OutputDir, "CounterViewModel.schema.json")),
                    Path.Join(options.OutputDir, "event-bindings.json"),
                    ImmutableArray<string>.Empty,
                    64);
            }

            public void OnProgress(Action<CompilationProgress> callback)
            {
                ArgumentNullException.ThrowIfNull(callback);
            }
        }

        private sealed class FakeIncrementalCompiler : IIncrementalCompiler
        {
            private readonly DependencyGraph dependencyGraph = new();
            private ImmutableArray<string> lastInvalidation = ImmutableArray<string>.Empty;

            public int CompileCalls { get; private set; }

            public List<string[]> Invalidations { get; } = [];

            public CompilationResult? NextResult { get; set; }

            public Exception? NextException { get; set; }

            public TaskCompletionSource<object?> CompileObserved { get; } = CreateCompletionSource<object?>();

            public CompilationResult CompileIncremental(ProjectContext context, CompilerOptions options)
            {
                ArgumentNullException.ThrowIfNull(context);
                ArgumentNullException.ThrowIfNull(options);
                CompileCalls++;
                _ = CompileObserved.TrySetResult(null);

                if (NextException is not null)
                {
                    Exception exception = NextException;
                    NextException = null;
                    throw exception;
                }

                if (NextResult is not null)
                {
                    CompilationResult result = NextResult;
                    NextResult = null;
                    return result;
                }

                string sourceFilePath = lastInvalidation.IsDefaultOrEmpty
                    ? "InitialView.cs"
                    : lastInvalidation[0];
                return CreateResult(sourceFilePath, success: true);
            }

            public ImmutableArray<string> GetDirtyFiles(ProjectContext context)
            {
                ArgumentNullException.ThrowIfNull(context);
                return lastInvalidation;
            }

            public DependencyGraph GetDependencyGraph()
            {
                return dependencyGraph;
            }

            public void Invalidate(ImmutableArray<string> filePaths)
            {
                lastInvalidation = filePaths.IsDefault ? ImmutableArray<string>.Empty : filePaths;
                Invalidations.Add(lastInvalidation.ToArray());
            }

            public void ClearCache()
            {
                lastInvalidation = ImmutableArray<string>.Empty;
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

        private sealed class FakeAnalyzer : ICSharpAnalyzer
        {
            public ProjectContext CreateProjectContext(CompilerOptions options)
            {
                ArgumentNullException.ThrowIfNull(options);
                CSharpCompilation compilation = CSharpCompilation.Create(
                    "WatcherTests",
                    new[]
                    {
                        CSharpSyntaxTree.ParseText("public sealed class InitialView { }", path: "InitialView.cs"),
                    });
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
    }
}
