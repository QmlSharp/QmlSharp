namespace QmlSharp.Compiler
{
    /// <summary>
    /// Watches compiler input files and runs incremental compilation after debounced changes.
    /// </summary>
    public sealed class CompilerWatcher : ICompilerWatcher
    {
        private static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(200);

        private readonly Lock gate = new();
        private readonly ICompiler compiler;
        private readonly IIncrementalCompiler incrementalCompiler;
        private readonly ICSharpAnalyzer analyzer;
        private readonly ICompilerFileWatcherFactory fileWatcherFactory;
        private readonly TimeSpan debounce;
        private readonly SemaphoreSlim compileGate = new(1, 1);
        private readonly List<Action<Exception>> errorHandlers = [];

        private ICompilerFileWatcher? fileWatcher;
        private CancellationTokenSource? stopCancellation;
        private CancellationTokenSource? debounceCancellation;
        private CancellationTokenRegistration cancellationRegistration;
        private Task? pendingCompileTask;
        private Action<CompilationResult>? compiledCallback;
        private CompilerOptions? currentOptions;
        private WatcherStatus status = WatcherStatus.Idle;
        private bool disposed;
        private int generation;

        /// <summary>
        /// Initializes a new watcher using the default compiler pipeline.
        /// </summary>
        public CompilerWatcher()
            : this(new QmlCompiler())
        {
        }

        /// <summary>
        /// Initializes a new watcher around a compiler pipeline.
        /// </summary>
        public CompilerWatcher(ICompiler compiler)
            : this(
                compiler,
                new IncrementalCompiler(compiler),
                new CSharpAnalyzer(),
                new FileSystemCompilerFileWatcherFactory(),
                DefaultDebounce)
        {
        }

        internal CompilerWatcher(
            ICompiler compiler,
            IIncrementalCompiler incrementalCompiler,
            ICSharpAnalyzer analyzer,
            ICompilerFileWatcherFactory fileWatcherFactory,
            TimeSpan debounce)
        {
            if (debounce < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(debounce), debounce, "Debounce must be non-negative.");
            }

            this.compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            this.incrementalCompiler = incrementalCompiler ?? throw new ArgumentNullException(nameof(incrementalCompiler));
            this.analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            this.fileWatcherFactory = fileWatcherFactory ?? throw new ArgumentNullException(nameof(fileWatcherFactory));
            this.debounce = debounce;
        }

        /// <inheritdoc />
        public WatcherStatus Status
        {
            get
            {
                lock (gate)
                {
                    return status;
                }
            }
        }

        /// <inheritdoc />
        public async Task StartAsync(
            CompilerOptions compilerOptions,
            Action<CompilationResult>? onCompiled = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(compilerOptions);
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            CompilerOptions normalizedOptions = compilerOptions.ValidateAndNormalize();
            WatcherStartState startState = InitializeStart(normalizedOptions, onCompiled, cancellationToken);
            try
            {
                RegisterExternalCancellation(startState, cancellationToken);
                startState.Token.ThrowIfCancellationRequested();
                startState.Watcher.Start();
                await CompileNowAsync(startState.Generation, startState.Token).ConfigureAwait(false);
            }
            catch
            {
                await StopAsync().ConfigureAwait(false);
                throw;
            }
        }

        private WatcherStartState InitializeStart(
            CompilerOptions normalizedOptions,
            Action<CompilationResult>? onCompiled,
            CancellationToken cancellationToken)
        {
            lock (gate)
            {
                if (status is WatcherStatus.Watching or WatcherStatus.Compiling)
                {
                    throw new InvalidOperationException("The compiler watcher is already running.");
                }

                currentOptions = normalizedOptions;
                compiledCallback = onCompiled;
                stopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                fileWatcher = fileWatcherFactory.Create(normalizedOptions);
                fileWatcher.Changed += OnFileChanged;
                fileWatcher.Error += OnWatcherError;
                generation++;
                status = WatcherStatus.Watching;

                return new WatcherStartState(
                    generation,
                    fileWatcher,
                    stopCancellation,
                    stopCancellation.Token);
            }
        }

        private void RegisterExternalCancellation(
            WatcherStartState startState,
            CancellationToken cancellationToken)
        {
            CancellationTokenRegistration registration = cancellationToken.Register(static state =>
            {
                _ = ((CompilerWatcher)state!).StopAsync();
            }, this);

            lock (gate)
            {
                if (generation == startState.Generation
                    && stopCancellation == startState.StopCancellation
                    && status != WatcherStatus.Stopped)
                {
                    cancellationRegistration = registration;
                    return;
                }
            }

            registration.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException("The compiler watcher stopped during startup.", cancellationToken);
        }

        /// <inheritdoc />
        public async Task StopAsync()
        {
            Task? taskToAwait;
            ICompilerFileWatcher? watcherToDispose;
            CancellationTokenSource? cancellationToDispose;
            CancellationTokenSource? debounceToDispose;
            CancellationTokenRegistration registrationToDispose;

            lock (gate)
            {
                if (status == WatcherStatus.Stopped && fileWatcher is null)
                {
                    return;
                }

                generation++;
                status = WatcherStatus.Stopped;
                stopCancellation?.Cancel();
                debounceCancellation?.Cancel();

                taskToAwait = pendingCompileTask;
                watcherToDispose = fileWatcher;
                cancellationToDispose = stopCancellation;
                debounceToDispose = debounceCancellation;
                registrationToDispose = cancellationRegistration;

                fileWatcher = null;
                stopCancellation = null;
                debounceCancellation = null;
                cancellationRegistration = default;
                pendingCompileTask = null;
                compiledCallback = null;
                currentOptions = null;
            }

            if (watcherToDispose is not null)
            {
                watcherToDispose.Changed -= OnFileChanged;
                watcherToDispose.Error -= OnWatcherError;
                watcherToDispose.Stop();
                watcherToDispose.Dispose();
            }

            if (taskToAwait is not null)
            {
                try
                {
                    await taskToAwait.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Trace.TraceInformation("Compiler watcher shutdown canceled a pending compile task.");
                }
            }

            registrationToDispose.Dispose();
            debounceToDispose?.Dispose();
            cancellationToDispose?.Dispose();
        }

        /// <inheritdoc />
        public void OnError(Action<Exception> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            lock (gate)
            {
                errorHandlers.Add(handler);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            StopAsync().GetAwaiter().GetResult();
            compileGate.Dispose();
        }

        private void OnFileChanged(object? sender, CompilerFileChangedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.FilePath))
            {
                return;
            }

            int activeGeneration;
            CancellationToken token;
            CancellationTokenSource? previousDebounceCancellation;
            lock (gate)
            {
                if (stopCancellation is null || status == WatcherStatus.Stopped)
                {
                    return;
                }

                incrementalCompiler.Invalidate(ImmutableArray.Create(args.FilePath));
                activeGeneration = generation;
                previousDebounceCancellation = debounceCancellation;
                previousDebounceCancellation?.Cancel();
                debounceCancellation = CancellationTokenSource.CreateLinkedTokenSource(stopCancellation.Token);
                token = debounceCancellation.Token;
                pendingCompileTask = DebounceAndCompileAsync(activeGeneration, token);
            }

            previousDebounceCancellation?.Dispose();
        }

        private void OnWatcherError(object? sender, CompilerWatcherErrorEventArgs args)
        {
            SetStatus(WatcherStatus.Error);
            NotifyError(args.Exception);
        }

        private async Task DebounceAndCompileAsync(int activeGeneration, CancellationToken cancellationToken)
        {
            try
            {
                if (debounce > TimeSpan.Zero)
                {
                    await Task.Delay(debounce, cancellationToken).ConfigureAwait(false);
                }

                await CompileNowAsync(activeGeneration, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                System.Diagnostics.Trace.TraceInformation("Compiler watcher debounce was canceled before compilation.");
                return;
            }
        }

        private async Task CompileNowAsync(int activeGeneration, CancellationToken cancellationToken)
        {
            bool failed = false;
            bool lockTaken = false;
            try
            {
                await compileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                lockTaken = true;
                if (!IsActive(activeGeneration) || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                CompilerOptions activeOptions = GetActiveOptions(activeGeneration);
                SetStatus(WatcherStatus.Compiling);

                using ProjectContext context = analyzer.CreateProjectContext(activeOptions);
                CompilationResult result = incrementalCompiler.CompileIncremental(context, activeOptions);
                if (!result.Success || !IsActive(activeGeneration) || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                OutputResult output = compiler.WriteOutput(result, activeOptions);
                if (!output.Success || !IsActive(activeGeneration) || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                InvokeCompiledCallback(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                System.Diagnostics.Trace.TraceInformation("Compiler watcher compilation was canceled.");
                return;
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or ArgumentException
                or IOException
                or UnauthorizedAccessException
                or NotSupportedException)
            {
                failed = true;
                SetStatus(WatcherStatus.Error);
                NotifyError(exception);
            }
            finally
            {
                if (lockTaken)
                {
                    _ = compileGate.Release();
                }

                if (!failed && IsActive(activeGeneration))
                {
                    SetStatus(WatcherStatus.Watching);
                }
            }
        }

        private CompilerOptions GetActiveOptions(int activeGeneration)
        {
            lock (gate)
            {
                if (generation != activeGeneration || currentOptions is null)
                {
                    throw new OperationCanceledException("The compiler watcher stopped.");
                }

                return currentOptions;
            }
        }

        private bool IsActive(int activeGeneration)
        {
            lock (gate)
            {
                return generation == activeGeneration
                    && stopCancellation is not null
                    && status != WatcherStatus.Stopped;
            }
        }

        private void SetStatus(WatcherStatus nextStatus)
        {
            lock (gate)
            {
                if (status == WatcherStatus.Stopped && nextStatus != WatcherStatus.Stopped)
                {
                    return;
                }

                status = nextStatus;
            }
        }

        private void InvokeCompiledCallback(CompilationResult result)
        {
            Action<CompilationResult>? callback;
            lock (gate)
            {
                callback = compiledCallback;
            }

            if (callback is null)
            {
                return;
            }

            try
            {
                callback(result);
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or ArgumentException
                or IOException
                or NotSupportedException)
            {
                SetStatus(WatcherStatus.Error);
                NotifyError(exception);
            }
        }

        private void NotifyError(Exception exception)
        {
            ImmutableArray<Action<Exception>> handlers;
            lock (gate)
            {
                handlers = errorHandlers.ToImmutableArray();
            }

            foreach (Action<Exception> handler in handlers)
            {
                try
                {
                    handler(exception);
                }
                catch (Exception handlerException) when (handlerException is InvalidOperationException
                    or ArgumentException
                    or IOException
                    or NotSupportedException)
                {
                    System.Diagnostics.Trace.TraceError($"Compiler watcher error handler failed: {handlerException}");
                }
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        private sealed record WatcherStartState(
            int Generation,
            ICompilerFileWatcher Watcher,
            CancellationTokenSource StopCancellation,
            CancellationToken Token);
    }
}
