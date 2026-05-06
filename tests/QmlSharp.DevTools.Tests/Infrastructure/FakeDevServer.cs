namespace QmlSharp.DevTools.Tests.Infrastructure
{
    public sealed class FakeDevServer : IDevServer
    {
        public int RebuildCalls { get; private set; }

        public int RestartCalls { get; private set; }

        public int StartCalls { get; private set; }

        public int StopCalls { get; private set; }

        public HotReloadResult RebuildResult { get; set; } = new(
            Success: true,
            InstancesMatched: 0,
            InstancesOrphaned: 0,
            InstancesNew: 0,
            new HotReloadPhases(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
            TotalTime: TimeSpan.FromMilliseconds(1),
            ErrorMessage: null,
            FailedStep: null);

        public DevServerStatus Status { get; set; } = DevServerStatus.Running;

        public ServerStats Stats { get; set; } = new(
            BuildCount: 1,
            RebuildCount: 0,
            HotReloadCount: 0,
            ErrorCount: 0,
            TotalBuildTime: TimeSpan.Zero,
            TotalHotReloadTime: TimeSpan.Zero,
            Uptime: TimeSpan.Zero);

        public IRepl? Repl => null;

        public event Action<DevServerStatusChangedEvent> OnStatusChanged = static _ => { };

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCalls++;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopCalls++;
            return Task.CompletedTask;
        }

        public Task<HotReloadResult> RebuildAsync(CancellationToken cancellationToken = default)
        {
            RebuildCalls++;
            return Task.FromResult(RebuildResult);
        }

        public Task RestartAsync(CancellationToken cancellationToken = default)
        {
            RestartCalls++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public void RaiseStatusChanged(DevServerStatusChangedEvent statusChanged)
        {
            OnStatusChanged(statusChanged);
        }
    }
}
