#pragma warning disable MA0048

namespace QmlSharp.Compiler
{
    internal interface ICompilerFileWatcherFactory
    {
        ICompilerFileWatcher Create(CompilerOptions options);
    }

    internal interface ICompilerFileWatcher : IDisposable
    {
        event EventHandler<CompilerFileChangedEventArgs>? Changed;

        event EventHandler<CompilerWatcherErrorEventArgs>? Error;

        void Start();

        void Stop();
    }

    internal sealed class CompilerFileChangedEventArgs : EventArgs
    {
        public CompilerFileChangedEventArgs(string filePath, CompilerFileChangeKind kind)
        {
            FilePath = filePath;
            Kind = kind;
        }

        public string FilePath { get; }

        public CompilerFileChangeKind Kind { get; }
    }

    internal sealed class CompilerWatcherErrorEventArgs : EventArgs
    {
        public CompilerWatcherErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }

        public Exception Exception { get; }
    }

    internal enum CompilerFileChangeKind
    {
        Changed,
        Created,
        Deleted,
        Renamed,
    }
}

#pragma warning restore MA0048
