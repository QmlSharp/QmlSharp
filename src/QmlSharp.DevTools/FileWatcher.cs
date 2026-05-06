#pragma warning disable CA1003, MA0048

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Monitors configured source roots and emits debounced file change batches.
    /// </summary>
    public sealed class FileWatcher : IFileWatcher
    {
        private static readonly ImmutableArray<string> DefaultIncludePatterns =
            ImmutableArray.Create("**/*.cs");

        private static readonly ImmutableArray<string> DefaultExcludePatterns =
            ImmutableArray.Create("**/bin/**", "**/obj/**", "**/.git/**");

        private readonly Lock gate = new();
        private readonly Lock deliveryGate = new();
        private readonly FileWatcherOptions options;
        private readonly IFileSystemWatcherFactory fileSystemWatcherFactory;
        private readonly IDevToolsTimerFactory timerFactory;
        private readonly IDevToolsClock clock;
        private readonly FilePatternMatcher matcher;
        private readonly Dictionary<string, FileChange> pendingChanges;
        private IFileSystemWatcher? fileSystemWatcher;
        private IDevToolsTimer? debounceTimer;
        private DateTimeOffset? firstPendingChangeAt;

        /// <summary>
        /// Initializes a new file watcher with default filesystem and timer implementations.
        /// </summary>
        /// <param name="options">Watcher configuration.</param>
        public FileWatcher(FileWatcherOptions options)
            : this(options, null, null, null)
        {
        }

        internal FileWatcher(
            FileWatcherOptions options,
            IFileSystemWatcherFactory? fileSystemWatcherFactory,
            IDevToolsTimerFactory? timerFactory,
            IDevToolsClock? clock)
        {
            ArgumentNullException.ThrowIfNull(options);

            this.clock = clock ?? SystemDevToolsClock.Instance;
            this.timerFactory = timerFactory ?? new SystemDevToolsTimerFactory();
            this.options = NormalizeOptions(options);
            this.fileSystemWatcherFactory = fileSystemWatcherFactory ??
                new DefaultFileSystemWatcherFactory(this.timerFactory, this.clock);
            matcher = new FilePatternMatcher(
                this.options.WatchPaths,
                this.options.IncludePatterns ?? DefaultIncludePatterns,
                this.options.ExcludePatterns ?? ImmutableArray<string>.Empty);
            pendingChanges = new Dictionary<string, FileChange>(PathComparisonComparer.Instance);
        }

        /// <inheritdoc />
        public event Action<FileChangeBatch> OnChange = static _ => { };

        /// <inheritdoc />
        public FileWatcherStatus Status { get; private set; } = FileWatcherStatus.Idle;

        /// <inheritdoc />
        public void Start()
        {
            IFileSystemWatcher watcherToStart;

            lock (gate)
            {
                if (Status == FileWatcherStatus.Disposed)
                {
                    throw new ObjectDisposedException(nameof(FileWatcher));
                }

                if (Status == FileWatcherStatus.Running)
                {
                    return;
                }

                watcherToStart = fileSystemWatcherFactory.Create(options);
                watcherToStart.Changed += OnRawChanged;
                fileSystemWatcher = watcherToStart;
                Status = FileWatcherStatus.Running;
            }

            try
            {
                watcherToStart.Start();
            }
            catch
            {
                lock (gate)
                {
                    if (ReferenceEquals(fileSystemWatcher, watcherToStart))
                    {
                        fileSystemWatcher = null;
                        Status = FileWatcherStatus.Idle;
                    }
                }

                watcherToStart.Changed -= OnRawChanged;
                watcherToStart.Dispose();
                throw;
            }
        }

        /// <inheritdoc />
        public void Stop()
        {
            IFileSystemWatcher? watcherToDispose;
            IDevToolsTimer? timerToDispose;

            lock (gate)
            {
                if (Status == FileWatcherStatus.Disposed)
                {
                    return;
                }

                Status = FileWatcherStatus.Disposed;
                watcherToDispose = fileSystemWatcher;
                timerToDispose = debounceTimer;
                fileSystemWatcher = null;
                debounceTimer = null;
                firstPendingChangeAt = null;
                pendingChanges.Clear();
            }

            if (watcherToDispose is not null)
            {
                watcherToDispose.Changed -= OnRawChanged;
                watcherToDispose.Stop();
                watcherToDispose.Dispose();
            }

            timerToDispose?.Dispose();
            WaitForBatchDeliveryToComplete();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Stop();
        }

        private static FileWatcherOptions NormalizeOptions(FileWatcherOptions options)
        {
            ImmutableArray<string> watchPaths = NormalizeWatchPaths(options.WatchPaths);
            ImmutableArray<string> includePatterns = NormalizeIncludePatterns(options.IncludePatterns);
            ImmutableArray<string> excludePatterns = NormalizeExcludePatterns(options.ExcludePatterns);
            int debounceMs = NormalizeNonNegativeMilliseconds(options.DebounceMs, nameof(options.DebounceMs));
            int pollIntervalMs = NormalizeNonNegativeMilliseconds(options.PollIntervalMs, nameof(options.PollIntervalMs));

            return new FileWatcherOptions(
                watchPaths,
                debounceMs,
                includePatterns,
                excludePatterns,
                options.UsePolling,
                pollIntervalMs);
        }

        private static ImmutableArray<string> NormalizeWatchPaths(IReadOnlyList<string> watchPaths)
        {
            ArgumentNullException.ThrowIfNull(watchPaths);
            if (watchPaths.Count == 0)
            {
                throw new ArgumentException("At least one watch path is required.", nameof(watchPaths));
            }

            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>(watchPaths.Count);
            foreach (string watchPath in watchPaths)
            {
                if (string.IsNullOrWhiteSpace(watchPath))
                {
                    throw new ArgumentException("Watch paths cannot contain null or whitespace values.", nameof(watchPaths));
                }

                builder.Add(Path.GetFullPath(watchPath));
            }

            return builder
                .Distinct(PathComparisonComparer.Instance)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static ImmutableArray<string> NormalizeIncludePatterns(IReadOnlyList<string>? includePatterns)
        {
            ImmutableArray<string> normalized = NormalizePatterns(includePatterns);
            return normalized.IsDefaultOrEmpty ? DefaultIncludePatterns : normalized;
        }

        private static ImmutableArray<string> NormalizeExcludePatterns(IReadOnlyList<string>? excludePatterns)
        {
            return excludePatterns is null ? DefaultExcludePatterns : NormalizePatterns(excludePatterns);
        }

        private static ImmutableArray<string> NormalizePatterns(IReadOnlyList<string>? patterns)
        {
            if (patterns is null || patterns.Count == 0)
            {
                return ImmutableArray<string>.Empty;
            }

            return patterns
                .Where(static pattern => !string.IsNullOrWhiteSpace(pattern))
                .Select(static pattern => pattern.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static pattern => pattern, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static int NormalizeNonNegativeMilliseconds(int value, string parameterName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Milliseconds must be non-negative.");
            }

            return value;
        }

        private void OnRawChanged(FileChange change)
        {
            ArgumentNullException.ThrowIfNull(change);

            FileChange normalizedChange = NormalizeChange(change);
            lock (gate)
            {
                if (Status != FileWatcherStatus.Running || !matcher.Includes(normalizedChange.FilePath))
                {
                    return;
                }

                if (pendingChanges.Count == 0)
                {
                    firstPendingChangeAt = normalizedChange.Timestamp;
                }

                pendingChanges[normalizedChange.FilePath] = normalizedChange;
                debounceTimer ??= timerFactory.CreateTimer(FlushPendingChanges);
                debounceTimer.Change(
                    TimeSpan.FromMilliseconds(options.DebounceMs),
                    Timeout.InfiniteTimeSpan);
            }
        }

        private FileChange NormalizeChange(FileChange change)
        {
            string filePath = Path.GetFullPath(change.FilePath);
            DateTimeOffset timestamp = change.Timestamp == default ? clock.UtcNow : change.Timestamp;
            return change with
            {
                FilePath = filePath,
                Timestamp = timestamp,
            };
        }

        private void FlushPendingChanges()
        {
            FileChangeBatch? batch = null;

            lock (gate)
            {
                if (Status != FileWatcherStatus.Running || pendingChanges.Count == 0)
                {
                    return;
                }

                DateTimeOffset emittedAt = clock.UtcNow;
                ImmutableArray<FileChange> changes = pendingChanges.Values
                    .OrderBy(static change => change.FilePath, StringComparer.Ordinal)
                    .ToImmutableArray();
                batch = new FileChangeBatch(changes, firstPendingChangeAt ?? emittedAt, emittedAt);
                pendingChanges.Clear();
                firstPendingChangeAt = null;
                debounceTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }

            lock (deliveryGate)
            {
                lock (gate)
                {
                    if (Status != FileWatcherStatus.Running)
                    {
                        return;
                    }
                }

                OnChange(batch);
            }
        }

        private void WaitForBatchDeliveryToComplete()
        {
            lock (deliveryGate)
            {
                // Stop must not return while a debounced batch is still being delivered.
            }
        }
    }

    internal sealed class DefaultFileSystemWatcherFactory : IFileSystemWatcherFactory
    {
        private readonly IDevToolsTimerFactory timerFactory;
        private readonly IDevToolsClock clock;

        public DefaultFileSystemWatcherFactory(IDevToolsTimerFactory timerFactory, IDevToolsClock clock)
        {
            ArgumentNullException.ThrowIfNull(timerFactory);
            ArgumentNullException.ThrowIfNull(clock);

            this.timerFactory = timerFactory;
            this.clock = clock;
        }

        public IFileSystemWatcher Create(FileWatcherOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (options.UsePolling)
            {
                return new PollingFileSystemWatcher(options, timerFactory, clock);
            }

            ImmutableArray<IFileSystemWatcher> watchers = options.WatchPaths
                .Where(Directory.Exists)
                .Select(static path => (IFileSystemWatcher)new DirectoryFileSystemWatcher(path))
                .ToImmutableArray();
            return new CompositeFileSystemWatcher(watchers);
        }
    }

    internal sealed class CompositeFileSystemWatcher : IFileSystemWatcher
    {
        private readonly ImmutableArray<IFileSystemWatcher> watchers;
        private bool disposed;

        public CompositeFileSystemWatcher(ImmutableArray<IFileSystemWatcher> watchers)
        {
            this.watchers = watchers.IsDefault ? ImmutableArray<IFileSystemWatcher>.Empty : watchers;
            foreach (IFileSystemWatcher watcher in this.watchers)
            {
                watcher.Changed += OnChanged;
            }
        }

        public event Action<FileChange> Changed = static _ => { };

        public void Start()
        {
            ThrowIfDisposed();
            foreach (IFileSystemWatcher watcher in watchers)
            {
                watcher.Start();
            }
        }

        public void Stop()
        {
            if (disposed)
            {
                return;
            }

            foreach (IFileSystemWatcher watcher in watchers)
            {
                watcher.Stop();
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (IFileSystemWatcher watcher in watchers)
            {
                watcher.Changed -= OnChanged;
                watcher.Dispose();
            }
        }

        private void OnChanged(FileChange change)
        {
            Changed(change);
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }
    }

    internal sealed class DirectoryFileSystemWatcher : IFileSystemWatcher
    {
        private readonly IDevToolsClock clock;
        private readonly FileSystemWatcher watcher;
        private bool disposed;

        public DirectoryFileSystemWatcher(string rootDirectory)
            : this(rootDirectory, SystemDevToolsClock.Instance)
        {
        }

        internal DirectoryFileSystemWatcher(string rootDirectory, IDevToolsClock clock)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
            ArgumentNullException.ThrowIfNull(clock);

            this.clock = clock;
            watcher = new FileSystemWatcher(rootDirectory)
            {
                Filter = "*",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.CreationTime
                    | NotifyFilters.Size,
            };
            watcher.Changed += OnModified;
            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
        }

        public event Action<FileChange> Changed = static _ => { };

        public void Start()
        {
            ThrowIfDisposed();
            watcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            if (disposed)
            {
                return;
            }

            watcher.EnableRaisingEvents = false;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnModified;
            watcher.Created -= OnCreated;
            watcher.Deleted -= OnDeleted;
            watcher.Renamed -= OnRenamed;
            watcher.Dispose();
        }

        private void OnCreated(object sender, FileSystemEventArgs args)
        {
            RaiseChanged(args.FullPath, FileChangeKind.Created);
        }

        private void OnModified(object sender, FileSystemEventArgs args)
        {
            RaiseChanged(args.FullPath, FileChangeKind.Modified);
        }

        private void OnDeleted(object sender, FileSystemEventArgs args)
        {
            RaiseChanged(args.FullPath, FileChangeKind.Deleted);
        }

        private void OnRenamed(object sender, RenamedEventArgs args)
        {
            RaiseChanged(args.FullPath, FileChangeKind.Renamed);
        }

        private void RaiseChanged(string filePath, FileChangeKind kind)
        {
            if (disposed)
            {
                return;
            }

            Changed(new FileChange(Path.GetFullPath(filePath), kind, clock.UtcNow));
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }
    }

    internal sealed class PollingFileSystemWatcher : IFileSystemWatcher
    {
        private readonly Lock gate = new();
        private readonly FileWatcherOptions options;
        private readonly IDevToolsTimerFactory timerFactory;
        private readonly IDevToolsClock clock;
        private Dictionary<string, FileSnapshot> snapshots = new(PathComparisonComparer.Instance);
        private IDevToolsTimer? timer;
        private bool running;
        private bool disposed;

        public PollingFileSystemWatcher(
            FileWatcherOptions options,
            IDevToolsTimerFactory timerFactory,
            IDevToolsClock clock)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(timerFactory);
            ArgumentNullException.ThrowIfNull(clock);

            this.options = options;
            this.timerFactory = timerFactory;
            this.clock = clock;
        }

        public event Action<FileChange> Changed = static _ => { };

        public void Start()
        {
            IDevToolsTimer timerToStart;

            lock (gate)
            {
                ThrowIfDisposed();
                if (running)
                {
                    return;
                }

                snapshots = Scan();
                timerToStart = timerFactory.CreateTimer(Poll);
                timer = timerToStart;
                running = true;
            }

            TimeSpan pollInterval = TimeSpan.FromMilliseconds(options.PollIntervalMs);
            timerToStart.Change(pollInterval, pollInterval);
        }

        public void Stop()
        {
            IDevToolsTimer? timerToDispose;

            lock (gate)
            {
                if (disposed || !running)
                {
                    return;
                }

                running = false;
                timerToDispose = timer;
                timer = null;
                snapshots.Clear();
            }

            timerToDispose?.Dispose();
        }

        public void Dispose()
        {
            IDevToolsTimer? timerToDispose;

            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                running = false;
                timerToDispose = timer;
                timer = null;
                snapshots.Clear();
            }

            timerToDispose?.Dispose();
        }

        private static Dictionary<string, FileSnapshot> CreateSnapshotDictionary()
        {
            return new Dictionary<string, FileSnapshot>(PathComparisonComparer.Instance);
        }

        private void Poll()
        {
            ImmutableArray<FileChange> changes;

            lock (gate)
            {
                if (!running || disposed)
                {
                    return;
                }

                Dictionary<string, FileSnapshot> current = Scan();
                changes = CompareSnapshots(snapshots, current);
                snapshots = current;
            }

            foreach (FileChange change in changes)
            {
                Changed(change);
            }
        }

        private Dictionary<string, FileSnapshot> Scan()
        {
            Dictionary<string, FileSnapshot> result = CreateSnapshotDictionary();
            foreach (string watchPath in options.WatchPaths.OrderBy(static path => path, StringComparer.Ordinal))
            {
                ImmutableArray<string> files = EnumerateFiles(watchPath);
                foreach (string file in files)
                {
                    if (TryCreateSnapshot(file, out FileSnapshot? snapshot))
                    {
                        result[snapshot.FilePath] = snapshot;
                    }
                }
            }

            return result;
        }

        private static ImmutableArray<string> EnumerateFiles(string watchPath)
        {
            if (!Directory.Exists(watchPath))
            {
                return ImmutableArray<string>.Empty;
            }

            try
            {
                EnumerationOptions enumerationOptions = new()
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                };
                return Directory
                    .EnumerateFiles(watchPath, "*", enumerationOptions)
                    .OrderBy(static path => path, StringComparer.Ordinal)
                    .ToImmutableArray();
            }
            catch (DirectoryNotFoundException)
            {
                return ImmutableArray<string>.Empty;
            }
            catch (IOException)
            {
                return ImmutableArray<string>.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return ImmutableArray<string>.Empty;
            }
        }

        private static bool TryCreateSnapshot(string filePath, [NotNullWhen(true)] out FileSnapshot? snapshot)
        {
            try
            {
                FileInfo info = new(filePath);
                snapshot = new FileSnapshot(
                    info.FullName,
                    info.LastWriteTimeUtc.Ticks,
                    info.Length);
                return true;
            }
            catch (DirectoryNotFoundException)
            {
                snapshot = null;
                return false;
            }
            catch (FileNotFoundException)
            {
                snapshot = null;
                return false;
            }
            catch (IOException)
            {
                snapshot = null;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                snapshot = null;
                return false;
            }
        }

        private ImmutableArray<FileChange> CompareSnapshots(
            IReadOnlyDictionary<string, FileSnapshot> previous,
            IReadOnlyDictionary<string, FileSnapshot> current)
        {
            ImmutableArray<FileChange>.Builder changes = ImmutableArray.CreateBuilder<FileChange>();
            List<FileSnapshot> deleted = previous
                .Where(entry => !current.ContainsKey(entry.Key))
                .Select(static entry => entry.Value)
                .OrderBy(static snapshot => snapshot.FilePath, StringComparer.Ordinal)
                .ToList();
            List<FileSnapshot> created = current
                .Where(entry => !previous.ContainsKey(entry.Key))
                .Select(static entry => entry.Value)
                .OrderBy(static snapshot => snapshot.FilePath, StringComparer.Ordinal)
                .ToList();

            AddRenamedChanges(deleted, created, changes);
            foreach (FileSnapshot snapshot in created)
            {
                changes.Add(ToChange(snapshot, FileChangeKind.Created));
            }

            foreach (FileSnapshot snapshot in deleted)
            {
                changes.Add(ToChange(snapshot, FileChangeKind.Deleted));
            }

            IEnumerable<FileSnapshot> modified = current
                .Where(entry =>
                    previous.TryGetValue(entry.Key, out FileSnapshot? oldSnapshot) &&
                    (entry.Value.LastWriteUtcTicks != oldSnapshot.LastWriteUtcTicks ||
                        entry.Value.Length != oldSnapshot.Length))
                .Select(static entry => entry.Value)
                .OrderBy(static snapshot => snapshot.FilePath, StringComparer.Ordinal);
            foreach (FileSnapshot snapshot in modified)
            {
                changes.Add(ToChange(snapshot, FileChangeKind.Modified));
            }

            return changes
                .OrderBy(static change => change.FilePath, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private void AddRenamedChanges(
            List<FileSnapshot> deleted,
            List<FileSnapshot> created,
            ImmutableArray<FileChange>.Builder changes)
        {
            for (int deletedIndex = deleted.Count - 1; deletedIndex >= 0; deletedIndex--)
            {
                FileSnapshot oldSnapshot = deleted[deletedIndex];
                int createdIndex = created.FindIndex(candidate =>
                    candidate.Length == oldSnapshot.Length &&
                    candidate.LastWriteUtcTicks == oldSnapshot.LastWriteUtcTicks);
                if (createdIndex < 0)
                {
                    continue;
                }

                FileSnapshot newSnapshot = created[createdIndex];
                deleted.RemoveAt(deletedIndex);
                created.RemoveAt(createdIndex);
                changes.Add(ToChange(newSnapshot, FileChangeKind.Renamed));
            }
        }

        private FileChange ToChange(FileSnapshot snapshot, FileChangeKind kind)
        {
            return new FileChange(snapshot.FilePath, kind, clock.UtcNow);
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }
    }

    internal sealed record FileSnapshot(
        string FilePath,
        long LastWriteUtcTicks,
        long Length);

    internal sealed class FilePatternMatcher
    {
        private readonly ImmutableArray<string> watchRoots;
        private readonly ImmutableArray<FilePattern> includePatterns;
        private readonly ImmutableArray<FilePattern> excludePatterns;

        public FilePatternMatcher(
            IReadOnlyList<string> watchRoots,
            IReadOnlyList<string> includePatterns,
            IReadOnlyList<string> excludePatterns)
        {
            ArgumentNullException.ThrowIfNull(watchRoots);
            ArgumentNullException.ThrowIfNull(includePatterns);
            ArgumentNullException.ThrowIfNull(excludePatterns);

            this.watchRoots = watchRoots
                .Select(static root => Path.GetFullPath(root))
                .ToImmutableArray();
            this.includePatterns = includePatterns.Select(static pattern => new FilePattern(pattern)).ToImmutableArray();
            this.excludePatterns = excludePatterns.Select(static pattern => new FilePattern(pattern)).ToImmutableArray();
        }

        public bool Includes(string filePath)
        {
            ImmutableArray<string> candidates = CreateCandidates(filePath);
            return MatchesAny(includePatterns, candidates) && !MatchesAny(excludePatterns, candidates);
        }

        private static bool MatchesAny(ImmutableArray<FilePattern> patterns, ImmutableArray<string> candidates)
        {
            return patterns.Any(pattern => candidates.Any(pattern.IsMatch));
        }

        private ImmutableArray<string> CreateCandidates(string filePath)
        {
            string fullPath = Path.GetFullPath(filePath);
            ImmutableArray<string>.Builder candidates = ImmutableArray.CreateBuilder<string>();
            candidates.Add(NormalizePathForPattern(fullPath));
            foreach (string root in watchRoots)
            {
                if (!PathComparisonComparer.IsPathInDirectory(fullPath, root))
                {
                    continue;
                }

                string relative = Path.GetRelativePath(root, fullPath);
                if (!string.IsNullOrWhiteSpace(relative) && !string.Equals(relative, ".", StringComparison.Ordinal))
                {
                    candidates.Add(NormalizePathForPattern(relative));
                }
            }

            return candidates
                .Distinct(StringComparer.Ordinal)
                .ToImmutableArray();
        }

        internal static string NormalizePathForPattern(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }
    }

    internal sealed class FilePattern
    {
        private static readonly char[] WildcardCharacters = { '*', '?' };

        private readonly string pattern;
        private readonly Regex? regex;

        public FilePattern(string pattern)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

            this.pattern = FilePatternMatcher.NormalizePathForPattern(pattern.Trim());
            if (this.pattern.IndexOfAny(WildcardCharacters) >= 0)
            {
                regex = new Regex(
                    BuildRegexPattern(this.pattern),
                    RegexOptions.CultureInvariant | PathComparisonComparer.PatternRegexOptions,
                    TimeSpan.FromSeconds(1));
            }
        }

        public bool IsMatch(string candidate)
        {
            string normalizedCandidate = FilePatternMatcher.NormalizePathForPattern(candidate);
            if (regex is not null)
            {
                return regex.IsMatch(normalizedCandidate);
            }

            return normalizedCandidate.Equals(pattern, PathComparisonComparer.PathStringComparison) ||
                normalizedCandidate.EndsWith("/" + pattern, PathComparisonComparer.PathStringComparison);
        }

        private static string BuildRegexPattern(string globPattern)
        {
            StringBuilder builder = new();
            builder.Append('^');
            int index = 0;
            while (index < globPattern.Length)
            {
                char current = globPattern[index];
                if (current == '*')
                {
                    if (index + 1 < globPattern.Length && globPattern[index + 1] == '*')
                    {
                        if (index + 2 < globPattern.Length && globPattern[index + 2] == '/')
                        {
                            builder.Append("(?:.*/)?");
                            index += 3;
                        }
                        else
                        {
                            builder.Append(".*");
                            index += 2;
                        }
                    }
                    else
                    {
                        builder.Append("[^/]*");
                        index++;
                    }
                }
                else if (current == '?')
                {
                    builder.Append("[^/]");
                    index++;
                }
                else
                {
                    builder.Append(Regex.Escape(current.ToString()));
                    index++;
                }
            }

            builder.Append('$');
            return builder.ToString();
        }
    }

    internal sealed class SystemDevToolsClock : IDevToolsClock
    {
        public static SystemDevToolsClock Instance { get; } = new();

        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public long GetTimestamp()
        {
            return Stopwatch.GetTimestamp();
        }

        public TimeSpan GetElapsedTime(long startTimestamp)
        {
            return Stopwatch.GetElapsedTime(startTimestamp);
        }
    }

    internal sealed class SystemDevToolsTimerFactory : IDevToolsTimerFactory
    {
        public IDevToolsTimer CreateTimer(Action callback)
        {
            return new SystemDevToolsTimer(callback);
        }
    }

    internal sealed class SystemDevToolsTimer : IDevToolsTimer
    {
        private readonly Lock gate = new();
        private readonly Timer timer;
        private bool disposed;

        public SystemDevToolsTimer(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);

            Tick += callback;
            timer = new Timer(OnTimer);
        }

        public event Action Tick = static () => { };

        public void Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                _ = timer.Change(dueTime, period);
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                timer.Dispose();
            }
        }

        private void OnTimer(object? state)
        {
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }
            }

            Tick();
        }
    }

    internal sealed class PathComparisonComparer : IEqualityComparer<string>
    {
        public static PathComparisonComparer Instance { get; } = new();

        public static StringComparer StringComparer { get; } = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        public static StringComparison PathStringComparison { get; } = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        public static RegexOptions PatternRegexOptions { get; } = OperatingSystem.IsWindows()
            ? RegexOptions.IgnoreCase
            : RegexOptions.None;

        public bool Equals(string? x, string? y)
        {
            return StringComparer.Equals(x, y);
        }

        public int GetHashCode(string obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            return StringComparer.GetHashCode(obj);
        }

        public static bool IsPathInDirectory(string path, string directory)
        {
            string normalizedPath = Path.GetFullPath(path);
            string normalizedDirectory = EnsureTrailingSeparator(Path.GetFullPath(directory));
            return normalizedPath.StartsWith(normalizedDirectory, PathStringComparison);
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
    }
}

#pragma warning restore CA1003, MA0048
