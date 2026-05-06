namespace QmlSharp.DevTools.Tests.Infrastructure
{
    public sealed class FakeHotReloadOrchestrator : IHotReloadOrchestrator
    {
        private readonly object syncRoot = new();
        private readonly Queue<HotReloadResult> results = new();
        private readonly List<CompilationResult> requests = new();

        public IReadOnlyList<CompilationResult> Requests
        {
            get
            {
                lock (syncRoot)
                {
                    return requests.ToImmutableArray();
                }
            }
        }

        public Func<CompilationResult, CancellationToken, Task<HotReloadResult>>? OnReloadAsync { get; set; }

        public event Action<HotReloadStartingEvent> OnBefore = static _ => { };

        public event Action<HotReloadCompletedEvent> OnAfter = static _ => { };

        public void QueueResult(HotReloadResult result)
        {
            lock (syncRoot)
            {
                results.Enqueue(result);
            }
        }

        public async Task<HotReloadResult> ReloadAsync(
            CompilationResult result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(result);

            lock (syncRoot)
            {
                requests.Add(result);
            }

            cancellationToken.ThrowIfCancellationRequested();
            OnBefore(new HotReloadStartingEvent(OldInstanceCount: 0, DateTimeOffset.UnixEpoch));
            HotReloadResult reloadResult = OnReloadAsync is null
                ? DequeueResult()
                : await OnReloadAsync(result, cancellationToken).ConfigureAwait(false);
            OnAfter(new HotReloadCompletedEvent(reloadResult, DateTimeOffset.UnixEpoch));
            return reloadResult;
        }

        private HotReloadResult DequeueResult()
        {
            lock (syncRoot)
            {
                if (results.Count == 0)
                {
                    return SuccessfulResult();
                }

                return results.Dequeue();
            }
        }

        public static HotReloadResult SuccessfulResult()
        {
            return new HotReloadResult(
                Success: true,
                InstancesMatched: 1,
                InstancesOrphaned: 0,
                InstancesNew: 0,
                new HotReloadPhases(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
                TotalTime: TimeSpan.FromMilliseconds(1),
                ErrorMessage: null,
                FailedStep: null);
        }
    }
}
