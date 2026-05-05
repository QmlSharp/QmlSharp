#pragma warning disable MA0048

using System.Globalization;

namespace QmlSharp.Build
{
    /// <summary>Minimal build-system development session with watch and rebuild orchestration.</summary>
    public sealed class DevSession : IDevSession
    {
        private static readonly ImmutableHashSet<string> WatchedExtensions =
            ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, ".cs", ".json", ".qml");

        private readonly IConfigLoader configLoader;
        private readonly IBuildPipeline buildPipeline;
        private readonly IDevFileWatcherFactory watcherFactory;
        private readonly IDevDebouncerFactory debouncerFactory;
        private readonly IDevHostHook hostHook;
        private readonly Lock stateLock = new();
        private readonly Lock callbackLock = new();
        private readonly Lock pendingFileLock = new();
        private readonly SemaphoreSlim rebuildSemaphore = new(1, 1);
        private ImmutableArray<Action<BuildResult>> buildCompleteCallbacks =
            ImmutableArray<Action<BuildResult>>.Empty;
        private ImmutableArray<Action<DevSessionState>> stateChangedCallbacks =
            ImmutableArray<Action<DevSessionState>>.Empty;
        private ImmutableHashSet<string> pendingChangedFiles =
            ImmutableHashSet.Create<string>(StringComparer.Ordinal);
        private bool pendingFullRebuild;
        private ImmutableArray<IDevFileWatcher> watchers = ImmutableArray<IDevFileWatcher>.Empty;
        private DevSessionState state = DevSessionState.Stopped;
        private CancellationTokenSource? sessionCancellation;
        private IDevDebouncer? debouncer;
        private BuildContext? buildContext;
        private DevCommandOptions? startOptions;
        private string? entry;
        private bool disposed;
        private bool started;
        private bool hostDisposed;
        private bool hostStarted;

        /// <summary>Create a dev session with default filesystem watchers and no-op host hook.</summary>
        public DevSession(IConfigLoader configLoader, IBuildPipeline buildPipeline)
            : this(
                configLoader,
                buildPipeline,
                new FileSystemDevFileWatcherFactory(),
                new TimerDevDebouncerFactory(),
                NoOpDevHostHook.Instance)
        {
        }

        internal DevSession(
            IConfigLoader configLoader,
            IBuildPipeline buildPipeline,
            IDevFileWatcherFactory watcherFactory,
            IDevDebouncerFactory debouncerFactory,
            IDevHostHook hostHook)
        {
            ArgumentNullException.ThrowIfNull(configLoader);
            ArgumentNullException.ThrowIfNull(buildPipeline);
            ArgumentNullException.ThrowIfNull(watcherFactory);
            ArgumentNullException.ThrowIfNull(debouncerFactory);
            ArgumentNullException.ThrowIfNull(hostHook);

            this.configLoader = configLoader;
            this.buildPipeline = buildPipeline;
            this.watcherFactory = watcherFactory;
            this.debouncerFactory = debouncerFactory;
            this.hostHook = hostHook;
        }

        /// <inheritdoc />
        public DevSessionState State
        {
            get
            {
                lock (stateLock)
                {
                    return state;
                }
            }
        }

        /// <inheritdoc />
        public BuildResult? LastBuild { get; private set; }

        /// <inheritdoc />
        public async Task StartAsync(DevCommandOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            ThrowIfDisposed();
            using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bool callerCancelled = false;

            try
            {
                InitializeStart(linkedCancellation);
                await StartCoreAsync(options, linkedCancellation.Token).ConfigureAwait(false);
                await Task.Delay(Timeout.InfiniteTimeSpan, linkedCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Session-owned cancellation is the normal stop path; caller cancellation is handled below.
                _ = linkedCancellation;
            }
            catch (OperationCanceledException)
            {
                callerCancelled = true;
            }
            finally
            {
                await DisposeResourcesAsync().ConfigureAwait(false);
                ClearSessionCancellation(linkedCancellation);
            }

            if (callerCancelled)
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        private async Task StartCoreAsync(DevCommandOptions options, CancellationToken cancellationToken)
        {
            SetState(DevSessionState.Starting);

            QmlSharpConfig config = configLoader.Load(options.ProjectDir);
            string effectiveEntry = ResolveEntry(options, config);
            string projectDir = Path.GetFullPath(options.ProjectDir);
            QmlSharpConfig effectiveConfig = config with
            {
                Entry = effectiveEntry,
                OutDir = ResolvePath(projectDir, config.OutDir),
                Dev = config.Dev with
                {
                    WatchPaths = ResolveWatchPaths(projectDir, config.Dev.WatchPaths),
                },
            };

            entry = effectiveEntry;
            startOptions = options with
            {
                Entry = effectiveEntry,
                ProjectDir = projectDir,
            };
            buildContext = CreateBuildContext(effectiveConfig, projectDir);
            debouncer = debouncerFactory.Create(TimeSpan.FromMilliseconds(effectiveConfig.Dev.DebounceMs));

            _ = await RunBuildAsync(
                isInitialBuild: true,
                changedFiles: ImmutableArray<string>.Empty,
                cancellationToken).ConfigureAwait(false);

            StartWatchers(effectiveConfig.Dev.WatchPaths);
        }

        /// <inheritdoc />
        public Task<BuildResult> RebuildAsync()
        {
            return RebuildAsync(ImmutableArray<string>.Empty);
        }

        /// <inheritdoc />
        public void OnBuildComplete(Action<BuildResult> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            lock (callbackLock)
            {
                buildCompleteCallbacks = buildCompleteCallbacks.Add(callback);
            }
        }

        /// <inheritdoc />
        public void OnStateChanged(Action<DevSessionState> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            lock (callbackLock)
            {
                stateChangedCallbacks = stateChangedCallbacks.Add(callback);
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            CancellationTokenSource? cancellationToCancel;
            lock (stateLock)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                cancellationToCancel = sessionCancellation;
                sessionCancellation = null;
            }

            try
            {
                cancellationToCancel?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // A concurrent stop can dispose the linked source before DisposeAsync observes it.
                _ = cancellationToCancel;
            }

            await DisposeResourcesAsync().ConfigureAwait(false);
            rebuildSemaphore.Dispose();
            GC.SuppressFinalize(this);
        }

        private static BuildContext CreateBuildContext(QmlSharpConfig config, string projectDir)
        {
            return new BuildContext
            {
                Config = config,
                ProjectDir = projectDir,
                OutputDir = config.OutDir,
                QtDir = config.Qt.Dir ?? string.Empty,
                ForceRebuild = false,
                LibraryMode = false,
                DryRun = false,
                FileFilter = null,
            };
        }

        private static string ResolveEntry(DevCommandOptions options, QmlSharpConfig config)
        {
            string? candidate = string.IsNullOrWhiteSpace(options.Entry) ? config.Entry : options.Entry;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                throw new ConfigParseException(new BuildDiagnostic(
                    BuildDiagnosticCode.ConfigValidationError,
                    BuildDiagnosticSeverity.Error,
                    "Dev requires an entry from qmlsharp.json or --entry.",
                    BuildPhase.ConfigLoading,
                    "entry"));
            }

            return ResolvePath(Path.GetFullPath(options.ProjectDir), candidate);
        }

        private static ImmutableArray<string> ResolveWatchPaths(
            string projectDir,
            ImmutableArray<string> watchPaths)
        {
            ImmutableArray<string> effectiveWatchPaths = watchPaths.IsDefaultOrEmpty
                ? ImmutableArray.Create("./src")
                : watchPaths;
            return effectiveWatchPaths
                .Select(path => ResolvePath(projectDir, path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static string ResolvePath(string projectDir, string path)
        {
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Join(projectDir, path));
        }

        private void InitializeStart(CancellationTokenSource linkedCancellation)
        {
            lock (stateLock)
            {
                if (started && state != DevSessionState.Stopped)
                {
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.InvariantCulture, "Cannot start DevSession while state is '{0}'.", state));
                }

                started = true;
                sessionCancellation = linkedCancellation;
            }
        }

        private void ClearSessionCancellation(CancellationTokenSource linkedCancellation)
        {
            lock (stateLock)
            {
                if (ReferenceEquals(sessionCancellation, linkedCancellation))
                {
                    sessionCancellation = null;
                }
            }
        }

        private void StartWatchers(ImmutableArray<string> watchPaths)
        {
            ImmutableArray<IDevFileWatcher> createdWatchers = watcherFactory.CreateWatchers(watchPaths);
            foreach (IDevFileWatcher watcher in createdWatchers)
            {
                watcher.Changed += OnWatcherChanged;
                watcher.Error += OnWatcherError;
                watcher.Start();
            }

            watchers = createdWatchers;
        }

        private void OnWatcherChanged(object? sender, DevFileChangedEventArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            BuildContext? context = buildContext;
            IDevDebouncer? currentDebouncer = debouncer;
            CancellationTokenSource? currentCancellation = sessionCancellation;
            if (context is null || currentDebouncer is null || currentCancellation is null)
            {
                return;
            }

            string fullPath = Path.GetFullPath(args.FullPath);
            if (ShouldIgnoreChange(fullPath, context.OutputDir))
            {
                return;
            }

            lock (pendingFileLock)
            {
                pendingChangedFiles = pendingChangedFiles.Add(fullPath);
            }

            currentDebouncer.Schedule(ProcessDebouncedChangesAsync);
        }

        private void OnWatcherError(object? sender, DevFileWatcherErrorEventArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            IDevDebouncer? currentDebouncer = debouncer;
            CancellationTokenSource? currentCancellation = sessionCancellation;
            if (currentDebouncer is null || currentCancellation is null)
            {
                return;
            }

            lock (pendingFileLock)
            {
                pendingFullRebuild = true;
            }

            currentDebouncer.Schedule(ProcessDebouncedChangesAsync);
        }

        private async Task ProcessDebouncedChangesAsync(CancellationToken cancellationToken)
        {
            (bool rebuildRequested, ImmutableArray<string> changedFiles) = TakePendingChanges();
            if (!rebuildRequested)
            {
                return;
            }

            using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                sessionCancellation?.Token ?? CancellationToken.None);
            _ = await RebuildAsync(changedFiles, linkedCancellation.Token).ConfigureAwait(false);
        }

        private (bool RebuildRequested, ImmutableArray<string> ChangedFiles) TakePendingChanges()
        {
            lock (pendingFileLock)
            {
                ImmutableArray<string> changedFiles = pendingChangedFiles
                    .OrderBy(static path => path, StringComparer.Ordinal)
                    .ToImmutableArray();
                bool rebuildRequested = pendingFullRebuild || !changedFiles.IsDefaultOrEmpty;
                pendingChangedFiles = pendingChangedFiles.Clear();
                pendingFullRebuild = false;
                return (rebuildRequested, changedFiles);
            }
        }

        private Task<BuildResult> RebuildAsync(ImmutableArray<string> changedFiles)
        {
            CancellationToken cancellationToken = sessionCancellation?.Token ?? CancellationToken.None;
            return RebuildAsync(changedFiles, cancellationToken);
        }

        private async Task<BuildResult> RebuildAsync(
            ImmutableArray<string> changedFiles,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (buildContext is null)
            {
                throw new InvalidOperationException(
                    "Cannot rebuild DevSession before StartAsync has initialized the build context.");
            }

            return await RunBuildAsync(
                isInitialBuild: false,
                changedFiles: NormalizeChangedFiles(changedFiles),
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<BuildResult> RunBuildAsync(
            bool isInitialBuild,
            ImmutableArray<string> changedFiles,
            CancellationToken cancellationToken)
        {
            await rebuildSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                BuildContext context = buildContext ??
                    throw new InvalidOperationException("Build context was not initialized.");
                DevCommandOptions options = startOptions ??
                    throw new InvalidOperationException("Dev session options were not initialized.");
                string effectiveEntry = entry ??
                    throw new InvalidOperationException("Dev session entry was not initialized.");

                SetState(isInitialBuild ? DevSessionState.Building : DevSessionState.Rebuilding);
                BuildResult result = await ExecuteBuildAsync(context, cancellationToken).ConfigureAwait(false);
                LastBuild = result;
                NotifyBuildComplete(result);

                if (result.Success)
                {
                    bool hostSucceeded = await ApplyHostHookAsync(
                        result,
                        changedFiles,
                        options,
                        context,
                        effectiveEntry,
                        cancellationToken).ConfigureAwait(false);
                    SetState(hostSucceeded ? DevSessionState.Running : DevSessionState.Error);
                }
                else
                {
                    SetState(DevSessionState.Error);
                }

                return result;
            }
            finally
            {
                _ = rebuildSemaphore.Release();
            }
        }

        private async Task<BuildResult> ExecuteBuildAsync(BuildContext context, CancellationToken cancellationToken)
        {
            try
            {
                return await buildPipeline.BuildAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (!IsFatalException(exception))
            {
                BuildDiagnostic diagnostic = new(
                    BuildDiagnosticCode.InternalError,
                    BuildDiagnosticSeverity.Error,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Dev session build failed unexpectedly: {0}",
                        exception.Message),
                    null,
                    null);
                return new BuildResult
                {
                    Success = false,
                    PhaseResults = ImmutableArray<PhaseResult>.Empty,
                    Diagnostics = ImmutableArray.Create(diagnostic),
                    Stats = new BuildStats(TimeSpan.Zero, 0, 0, 0, 0, false),
                };
            }
        }

        private static bool IsFatalException(Exception exception)
        {
            return exception is OutOfMemoryException or
                StackOverflowException or
                AccessViolationException or
                AppDomainUnloadedException or
                BadImageFormatException;
        }

        private async Task<bool> ApplyHostHookAsync(
            BuildResult result,
            ImmutableArray<string> changedFiles,
            DevCommandOptions options,
            BuildContext context,
            string effectiveEntry,
            CancellationToken cancellationToken)
        {
            if (options.Headless)
            {
                return true;
            }

            try
            {
                if (!hostStarted)
                {
                    await hostHook.StartAsync(
                        new DevHostStartRequest(options, context.ProjectDir, context.OutputDir, effectiveEntry, result),
                        cancellationToken).ConfigureAwait(false);
                    hostStarted = true;
                    return true;
                }

                if (!context.Config.Dev.HotReload)
                {
                    return true;
                }

                await hostHook.ReloadAsync(
                    new DevHostReloadRequest(
                        options,
                        context.ProjectDir,
                        context.OutputDir,
                        effectiveEntry,
                        changedFiles,
                        result),
                    cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private bool ShouldIgnoreChange(string fullPath, string outputDir)
        {
            if (IsPathInDirectory(fullPath, outputDir))
            {
                return true;
            }

            string extension = Path.GetExtension(fullPath);
            return !WatchedExtensions.Contains(extension);
        }

        private static bool IsPathInDirectory(string path, string directory)
        {
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            string normalizedPath = Path.GetFullPath(path);
            string normalizedDirectory = EnsureTrailingSeparator(Path.GetFullPath(directory));
            return normalizedPath.StartsWith(normalizedDirectory, comparison);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar) ||
                path.EndsWith(Path.AltDirectorySeparatorChar))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static ImmutableArray<string> NormalizeChangedFiles(ImmutableArray<string> changedFiles)
        {
            return changedFiles.IsDefaultOrEmpty
                ? ImmutableArray<string>.Empty
                : changedFiles
                    .Where(static file => !string.IsNullOrWhiteSpace(file))
                    .Select(static file => Path.GetFullPath(file))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static file => file, StringComparer.Ordinal)
                    .ToImmutableArray();
        }

        private async Task DisposeResourcesAsync()
        {
            ImmutableArray<IDevFileWatcher> watchersToDispose = watchers;
            IDevDebouncer? debouncerToDispose = debouncer;
            bool shouldDisposeHost = !hostDisposed;

            watchers = ImmutableArray<IDevFileWatcher>.Empty;
            debouncer = null;
            hostDisposed = true;
            hostStarted = false;

            foreach (IDevFileWatcher watcher in watchersToDispose)
            {
                watcher.Changed -= OnWatcherChanged;
                watcher.Error -= OnWatcherError;
                await watcher.DisposeAsync().ConfigureAwait(false);
            }

            if (debouncerToDispose is not null)
            {
                await debouncerToDispose.DisposeAsync().ConfigureAwait(false);
            }

            if (shouldDisposeHost)
            {
                await hostHook.StopAsync(CancellationToken.None).ConfigureAwait(false);
                await hostHook.DisposeAsync().ConfigureAwait(false);
            }

            SetState(DevSessionState.Stopped);
        }

        private void SetState(DevSessionState nextState)
        {
            ImmutableArray<Action<DevSessionState>> callbacks;
            lock (stateLock)
            {
                if (state == nextState)
                {
                    return;
                }

                state = nextState;
            }

            lock (callbackLock)
            {
                callbacks = stateChangedCallbacks;
            }

            foreach (Action<DevSessionState> callback in callbacks)
            {
                callback(nextState);
            }
        }

        private void NotifyBuildComplete(BuildResult result)
        {
            ImmutableArray<Action<BuildResult>> callbacks;
            lock (callbackLock)
            {
                callbacks = buildCompleteCallbacks;
            }

            foreach (Action<BuildResult> callback in callbacks)
            {
                callback(result);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(DevSession));
            }
        }
    }

    internal interface IDevFileWatcher : IAsyncDisposable
    {
        event EventHandler<DevFileChangedEventArgs>? Changed;

        event EventHandler<DevFileWatcherErrorEventArgs>? Error;

        void Start();
    }

    internal interface IDevFileWatcherFactory
    {
        ImmutableArray<IDevFileWatcher> CreateWatchers(ImmutableArray<string> watchPaths);
    }

    internal sealed class DevFileChangedEventArgs : EventArgs
    {
        public DevFileChangedEventArgs(string fullPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
            FullPath = fullPath;
        }

        public string FullPath { get; }
    }

    internal sealed class DevFileWatcherErrorEventArgs : EventArgs
    {
        public DevFileWatcherErrorEventArgs(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            Exception = exception;
        }

        public Exception Exception { get; }
    }

    internal interface IDevDebouncer : IAsyncDisposable
    {
        void Schedule(Func<CancellationToken, Task> action);

        Task FlushAsync(CancellationToken cancellationToken = default);
    }

    internal interface IDevDebouncerFactory
    {
        IDevDebouncer Create(TimeSpan delay);
    }

    internal sealed class FileSystemDevFileWatcherFactory : IDevFileWatcherFactory
    {
        public ImmutableArray<IDevFileWatcher> CreateWatchers(ImmutableArray<string> watchPaths)
        {
            return watchPaths
                .Where(Directory.Exists)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .Select(static path => (IDevFileWatcher)new FileSystemDevFileWatcher(path))
                .ToImmutableArray();
        }
    }

    internal sealed class FileSystemDevFileWatcher : IDevFileWatcher
    {
        private readonly FileSystemWatcher watcher;

        public FileSystemDevFileWatcher(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName |
                    NotifyFilters.LastWrite |
                    NotifyFilters.DirectoryName |
                    NotifyFilters.CreationTime,
            };
            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;
        }

        public event EventHandler<DevFileChangedEventArgs>? Changed;

        public event EventHandler<DevFileWatcherErrorEventArgs>? Error;

        public void Start()
        {
            watcher.EnableRaisingEvents = true;
        }

        public ValueTask DisposeAsync()
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnChanged;
            watcher.Created -= OnChanged;
            watcher.Deleted -= OnChanged;
            watcher.Renamed -= OnRenamed;
            watcher.Error -= OnError;
            watcher.Dispose();
            return ValueTask.CompletedTask;
        }

        private void OnChanged(object? _, FileSystemEventArgs args)
        {
            Changed?.Invoke(this, new DevFileChangedEventArgs(args.FullPath));
        }

        private void OnRenamed(object? _, RenamedEventArgs args)
        {
            Changed?.Invoke(this, new DevFileChangedEventArgs(args.FullPath));
        }

        private void OnError(object? _, ErrorEventArgs args)
        {
            Error?.Invoke(this, new DevFileWatcherErrorEventArgs(args.GetException()));
        }
    }

    internal sealed class TimerDevDebouncerFactory : IDevDebouncerFactory
    {
        public IDevDebouncer Create(TimeSpan delay)
        {
            return new TimerDevDebouncer(delay);
        }
    }

    internal sealed class TimerDevDebouncer : IDevDebouncer
    {
        private readonly TimeSpan delay;
        private readonly Lock gate = new();
        private CancellationTokenSource? pendingCancellation;
        private Task pendingTask = Task.CompletedTask;
        private bool disposed;

        public TimerDevDebouncer(TimeSpan delay)
        {
            this.delay = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        }

        public void Schedule(Func<CancellationToken, Task> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            CancellationTokenSource cancellation;
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                pendingCancellation?.Cancel();
                pendingCancellation?.Dispose();
                cancellation = new CancellationTokenSource();
                pendingCancellation = cancellation;
                pendingTask = RunAsync(action, cancellation.Token);
            }
        }

        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            Task task;
            lock (gate)
            {
                task = pendingTask;
            }

            return task.WaitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            lock (gate)
            {
                if (disposed)
                {
                    return ValueTask.CompletedTask;
                }

                disposed = true;
                pendingCancellation?.Cancel();
                pendingCancellation?.Dispose();
                pendingCancellation = null;
            }

            return ValueTask.CompletedTask;
        }

        private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                await action(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Superseded debounce work is expected whenever a newer file event arrives.
                _ = cancellationToken;
            }
        }
    }

    internal sealed class NoOpDevHostHook : IDevHostHook
    {
        public static NoOpDevHostHook Instance { get; } = new();

        public Task StartAsync(DevHostStartRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task ReloadAsync(DevHostReloadRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}

#pragma warning restore MA0048
