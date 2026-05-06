#pragma warning disable CA1003, MA0046, MA0048

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Monitors source files and emits debounced change batches.
    /// </summary>
    public interface IFileWatcher : IDisposable
    {
        /// <summary>Starts monitoring the configured paths.</summary>
        void Start();

        /// <summary>Stops monitoring. No more events after this returns.</summary>
        void Stop();

        /// <summary>Fires after the debounce window closes with one or more changes.</summary>
        event Action<FileChangeBatch> OnChange;

        /// <summary>Gets the current watcher status.</summary>
        FileWatcherStatus Status { get; }
    }

    /// <summary>A debounced batch of file changes.</summary>
    /// <param name="Changes">Files that changed since the last batch.</param>
    /// <param name="FirstChangeAt">When the first change in this batch was detected.</param>
    /// <param name="EmittedAt">When the debounce window closed and the batch was emitted.</param>
    public sealed record FileChangeBatch(
        IReadOnlyList<FileChange> Changes,
        DateTimeOffset FirstChangeAt,
        DateTimeOffset EmittedAt);

    /// <summary>A single file change event.</summary>
    /// <param name="FilePath">Absolute path to the changed file.</param>
    /// <param name="Kind">Type of change.</param>
    /// <param name="Timestamp">When the operating system reported the change.</param>
    public sealed record FileChange(
        string FilePath,
        FileChangeKind Kind,
        DateTimeOffset Timestamp);

    /// <summary>File change types.</summary>
    public enum FileChangeKind
    {
        /// <summary>A file was created.</summary>
        Created,

        /// <summary>A file was modified.</summary>
        Modified,

        /// <summary>A file was deleted.</summary>
        Deleted,

        /// <summary>A file was renamed.</summary>
        Renamed,
    }

    /// <summary>File watcher lifecycle states.</summary>
    public enum FileWatcherStatus
    {
        /// <summary>The watcher is configured but not running.</summary>
        Idle,

        /// <summary>The watcher is actively monitoring files.</summary>
        Running,

        /// <summary>The watcher has been disposed.</summary>
        Disposed,
    }

    /// <summary>Configuration for file watching.</summary>
    /// <param name="WatchPaths">Root directories to watch.</param>
    /// <param name="DebounceMs">Debounce window in milliseconds.</param>
    /// <param name="IncludePatterns">Glob patterns to include.</param>
    /// <param name="ExcludePatterns">Glob patterns to exclude.</param>
    /// <param name="UsePolling">Whether to use polling instead of operating-system events.</param>
    /// <param name="PollIntervalMs">Polling interval in milliseconds when polling is enabled.</param>
    public sealed record FileWatcherOptions(
        IReadOnlyList<string> WatchPaths,
        int DebounceMs = 200,
        IReadOnlyList<string>? IncludePatterns = null,
        IReadOnlyList<string>? ExcludePatterns = null,
        bool UsePolling = false,
        int PollIntervalMs = 500);

#pragma warning restore CA1003, MA0046, MA0048
}
