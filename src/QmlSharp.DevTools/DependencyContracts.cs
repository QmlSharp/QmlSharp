#pragma warning disable CA1003, MA0046, MA0048

using QmlSharp.Build;
using QmlSharp.Compiler;
using QmlSharp.Host.Instances;
using QmlSharp.Host.Metrics;

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Facade over compiler services consumed by dev-tools.
    /// </summary>
    public interface IDevToolsCompiler
    {
        /// <summary>Runs a full compile for the configured project.</summary>
        Task<CompilationResult> CompileAsync(
            CompilerOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>Runs an incremental compile for the changed files.</summary>
        Task<CompilationResult> CompileChangedAsync(
            CompilerOptions options,
            FileChangeBatch changes,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Facade over native host operations consumed by dev-tools.
    /// </summary>
    public interface IDevToolsNativeHost
    {
        /// <summary>Captures active instance snapshots before a reload.</summary>
        Task<IReadOnlyList<InstanceSnapshot>> CaptureSnapshotsAsync(CancellationToken cancellationToken = default);

        /// <summary>Reloads generated QML through the native host.</summary>
        Task ReloadQmlAsync(string qmlSourcePath, CancellationToken cancellationToken = default);

        /// <summary>Pushes state from an old instance snapshot into a new instance.</summary>
        Task SyncStateBatchAsync(
            string instanceId,
            IReadOnlyDictionary<string, object?> state,
            CancellationToken cancellationToken = default);

        /// <summary>Restores native snapshot state after reload.</summary>
        Task RestoreSnapshotsAsync(
            IReadOnlyList<InstanceSnapshot> snapshots,
            CancellationToken cancellationToken = default);

        /// <summary>Gets currently active instance information.</summary>
        Task<IReadOnlyList<InstanceInfo>> GetInstancesAsync(CancellationToken cancellationToken = default);

        /// <summary>Shows a native error overlay.</summary>
        Task ShowOverlayAsync(OverlayError error, CancellationToken cancellationToken = default);

        /// <summary>Hides the native error overlay.</summary>
        Task HideOverlayAsync(CancellationToken cancellationToken = default);

        /// <summary>Evaluates QML in the native engine.</summary>
        Task<string> EvaluateQmlAsync(string input, CancellationToken cancellationToken = default);

        /// <summary>Gets runtime metrics from the native host.</summary>
        Task<RuntimeMetrics> GetMetricsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Facade over the build pipeline consumed by dev-tools.
    /// </summary>
    public interface IDevToolsBuildPipeline
    {
        /// <summary>Runs the full build pipeline.</summary>
        Task<BuildResult> BuildAsync(BuildContext context, CancellationToken cancellationToken = default);

        /// <summary>Runs the selected build phases.</summary>
        Task<BuildResult> BuildPhasesAsync(
            BuildContext context,
            ImmutableArray<BuildPhase> phases,
            CancellationToken cancellationToken = default);

        /// <summary>Registers a build progress callback.</summary>
        void OnProgress(Action<BuildProgress> callback);
    }

    /// <summary>
    /// Facade over build-system configuration loading consumed by dev-tools.
    /// </summary>
    public interface IDevToolsConfigLoader
    {
        /// <summary>Loads configuration from a project directory.</summary>
        QmlSharpConfig Load(string projectDir);

        /// <summary>Validates a configuration object.</summary>
        ImmutableArray<ConfigDiagnostic> Validate(QmlSharpConfig config);

        /// <summary>Gets default configuration.</summary>
        QmlSharpConfig GetDefaults();
    }

    /// <summary>Clock abstraction for deterministic tests.</summary>
    public interface IDevToolsClock
    {
        /// <summary>Gets the current UTC timestamp.</summary>
        DateTimeOffset UtcNow { get; }

        /// <summary>Gets a monotonic timestamp.</summary>
        long GetTimestamp();

        /// <summary>Gets elapsed time from a previous timestamp.</summary>
        TimeSpan GetElapsedTime(long startTimestamp);
    }

    /// <summary>Timer abstraction for deterministic tests.</summary>
    public interface IDevToolsTimer : IDisposable
    {
        /// <summary>Raised when the timer fires.</summary>
        event Action Tick;

        /// <summary>Changes timer scheduling.</summary>
        void Change(TimeSpan dueTime, TimeSpan period);
    }

    /// <summary>Factory for creating deterministic timer abstractions.</summary>
    public interface IDevToolsTimerFactory
    {
        /// <summary>Creates a timer.</summary>
        IDevToolsTimer CreateTimer(Action callback);
    }

    /// <summary>Console writer abstraction used by DevConsole tests.</summary>
    public interface IConsoleWriter
    {
        /// <summary>Writes text without a trailing newline.</summary>
        void Write(string text);

        /// <summary>Writes text with a trailing newline.</summary>
        void WriteLine(string text);
    }

    /// <summary>File-system watcher abstraction used by the higher-level file watcher.</summary>
    public interface IFileSystemWatcher : IDisposable
    {
        /// <summary>Raised for raw file changes.</summary>
        event Action<FileChange> Changed;

        /// <summary>Starts watching.</summary>
        void Start();

        /// <summary>Stops watching.</summary>
        void Stop();
    }

    /// <summary>Factory for raw file-system watcher abstractions.</summary>
    public interface IFileSystemWatcherFactory
    {
        /// <summary>Creates a watcher for the specified options.</summary>
        IFileSystemWatcher Create(FileWatcherOptions options);
    }

#pragma warning restore CA1003, MA0046, MA0048
}
