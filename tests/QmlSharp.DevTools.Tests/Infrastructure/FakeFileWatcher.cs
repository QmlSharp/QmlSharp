namespace QmlSharp.DevTools.Tests.Infrastructure
{
    public sealed class FakeFileWatcher : IFileWatcher
    {
        public event Action<FileChangeBatch> OnChange = static _ => { };

        public FileWatcherStatus Status { get; private set; } = FileWatcherStatus.Idle;

        public void Start()
        {
            Status = FileWatcherStatus.Running;
        }

        public void Stop()
        {
            Status = FileWatcherStatus.Disposed;
        }

        public void Emit(FileChangeBatch batch)
        {
            OnChange(batch);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
