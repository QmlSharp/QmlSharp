namespace QmlSharp.DevTools.Tests.Infrastructure
{
    public sealed class FakeNativeHost : IDevToolsNativeHost
    {
        private readonly List<string> reloadedQmlPaths = new();
        private readonly List<OverlayError> shownErrors = new();
        private readonly List<string> evaluatedInputs = new();
        private readonly List<string> syncedInstanceIds = new();
        private readonly List<IReadOnlyDictionary<string, object?>> syncedStates = new();
        private readonly List<string> callOrder = new();

        public IReadOnlyList<string> ReloadedQmlPaths => reloadedQmlPaths;

        public IReadOnlyList<OverlayError> ShownErrors => shownErrors;

        public IReadOnlyList<string> EvaluatedInputs => evaluatedInputs;

        public IReadOnlyList<string> SyncedInstanceIds => syncedInstanceIds;

        public IReadOnlyList<IReadOnlyDictionary<string, object?>> SyncedStates => syncedStates;

        public IReadOnlyList<string> CallOrder => callOrder;

        public IReadOnlyList<InstanceSnapshot> Snapshots { get; set; } =
            ImmutableArray<InstanceSnapshot>.Empty;

        public IReadOnlyList<InstanceInfo> Instances { get; set; } =
            ImmutableArray<InstanceInfo>.Empty;

        public RuntimeMetrics Metrics { get; set; } = DevToolsTestFixtures.RuntimeMetrics();

        public bool HideOverlayCalled { get; private set; }

        public bool RestoreSnapshotsCalled { get; private set; }

        public string QmlEvaluationResult { get; set; } = string.Empty;

        public Exception? CaptureException { get; set; }

        public Exception? ReloadException { get; set; }

        public Exception? SyncException { get; set; }

        public Exception? RestoreException { get; set; }

        public Action? BeforeCapture { get; set; }

        public Action? BeforeReload { get; set; }

        public Action? BeforeSync { get; set; }

        public Action? BeforeRestore { get; set; }

        public Task<IReadOnlyList<InstanceSnapshot>> CaptureSnapshotsAsync(CancellationToken cancellationToken = default)
        {
            callOrder.Add("capture");
            BeforeCapture?.Invoke();
            if (CaptureException is not null)
            {
                throw CaptureException;
            }

            return Task.FromResult(Snapshots);
        }

        public Task ReloadQmlAsync(string qmlSourcePath, CancellationToken cancellationToken = default)
        {
            callOrder.Add("reload");
            BeforeReload?.Invoke();
            if (ReloadException is not null)
            {
                throw ReloadException;
            }

            reloadedQmlPaths.Add(qmlSourcePath);
            return Task.CompletedTask;
        }

        public Task SyncStateBatchAsync(
            string instanceId,
            IReadOnlyDictionary<string, object?> state,
            CancellationToken cancellationToken = default)
        {
            callOrder.Add("sync");
            BeforeSync?.Invoke();
            if (SyncException is not null)
            {
                throw SyncException;
            }

            syncedInstanceIds.Add(instanceId);
            syncedStates.Add(state);
            return Task.CompletedTask;
        }

        public Task RestoreSnapshotsAsync(
            IReadOnlyList<InstanceSnapshot> snapshots,
            CancellationToken cancellationToken = default)
        {
            callOrder.Add("restore");
            BeforeRestore?.Invoke();
            if (RestoreException is not null)
            {
                throw RestoreException;
            }

            RestoreSnapshotsCalled = true;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<InstanceInfo>> GetInstancesAsync(CancellationToken cancellationToken = default)
        {
            callOrder.Add("instances");
            return Task.FromResult(Instances);
        }

        public Task ShowOverlayAsync(OverlayError error, CancellationToken cancellationToken = default)
        {
            shownErrors.Add(error);
            return Task.CompletedTask;
        }

        public Task HideOverlayAsync(CancellationToken cancellationToken = default)
        {
            HideOverlayCalled = true;
            return Task.CompletedTask;
        }

        public Task<string> EvaluateQmlAsync(string input, CancellationToken cancellationToken = default)
        {
            evaluatedInputs.Add(input);
            return Task.FromResult(QmlEvaluationResult);
        }

        public Task<RuntimeMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Metrics);
        }
    }
}
