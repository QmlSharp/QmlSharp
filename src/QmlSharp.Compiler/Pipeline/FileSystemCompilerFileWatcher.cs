#pragma warning disable MA0048

namespace QmlSharp.Compiler
{
    internal sealed class FileSystemCompilerFileWatcherFactory : ICompilerFileWatcherFactory
    {
        public ICompilerFileWatcher Create(CompilerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            CompilerOptions normalizedOptions = options.ValidateAndNormalize();
            string projectDirectory = Path.GetDirectoryName(Path.GetFullPath(normalizedOptions.ProjectPath))
                ?? Directory.GetCurrentDirectory();
            return new FileSystemCompilerFileWatcher(projectDirectory);
        }
    }

    internal sealed class FileSystemCompilerFileWatcher : ICompilerFileWatcher
    {
        private readonly FileSystemWatcher watcher;
        private bool disposed;

        public FileSystemCompilerFileWatcher(string rootDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

            watcher = new FileSystemWatcher(rootDirectory)
            {
                Filter = "*.cs",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.CreationTime
                    | NotifyFilters.Size,
            };

            watcher.Changed += OnChanged;
            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;
        }

        public event EventHandler<CompilerFileChangedEventArgs>? Changed;

        public event EventHandler<CompilerWatcherErrorEventArgs>? Error;

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
            watcher.Changed -= OnChanged;
            watcher.Created -= OnCreated;
            watcher.Deleted -= OnDeleted;
            watcher.Renamed -= OnRenamed;
            watcher.Error -= OnError;
            watcher.Dispose();
        }

        private void OnChanged(object sender, FileSystemEventArgs args)
        {
            RaiseChanged(args.FullPath, CompilerFileChangeKind.Changed);
        }

        private void OnCreated(object sender, FileSystemEventArgs args)
        {
            RaiseChanged(args.FullPath, CompilerFileChangeKind.Created);
        }

        private void OnDeleted(object sender, FileSystemEventArgs args)
        {
            RaiseChanged(args.FullPath, CompilerFileChangeKind.Deleted);
        }

        private void OnRenamed(object sender, RenamedEventArgs args)
        {
            RaiseChanged(args.FullPath, CompilerFileChangeKind.Renamed);
        }

        private void OnError(object sender, ErrorEventArgs args)
        {
            Error?.Invoke(this, new CompilerWatcherErrorEventArgs(args.GetException()));
        }

        private void RaiseChanged(string filePath, CompilerFileChangeKind kind)
        {
            Changed?.Invoke(this, new CompilerFileChangedEventArgs(filePath, kind));
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }
    }
}

#pragma warning restore MA0048
