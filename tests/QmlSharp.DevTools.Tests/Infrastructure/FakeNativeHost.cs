namespace QmlSharp.DevTools.Tests.Infrastructure
{
    public sealed class FakeNativeHost : IDevToolsNativeHost
    {
        private readonly List<string> reloadedQmlPaths = new();
        private readonly List<OverlayError> shownErrors = new();
        private readonly List<string> evaluatedInputs = new();
        private readonly List<string> syncedInstanceIds = new();

        public IReadOnlyList<string> ReloadedQmlPaths => reloadedQmlPaths;

        public IReadOnlyList<OverlayError> ShownErrors => shownErrors;

        public IReadOnlyList<string> EvaluatedInputs => evaluatedInputs;

        public IReadOnlyList<string> SyncedInstanceIds => syncedInstanceIds;

        public IReadOnlyList<InstanceSnapshot> Snapshots { get; set; } =
            ImmutableArray<InstanceSnapshot>.Empty;

        public IReadOnlyList<InstanceInfo> Instances { get; set; } =
            ImmutableArray<InstanceInfo>.Empty;

        public RuntimeMetrics Metrics { get; set; } = DevToolsTestFixtures.RuntimeMetrics();

        public bool HideOverlayCalled { get; private set; }

        public bool RestoreSnapshotsCalled { get; private set; }

        public string QmlEvaluationResult { get; set; } = string.Empty;

        public Task<IReadOnlyList<InstanceSnapshot>> CaptureSnapshotsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Snapshots);
        }

        public Task ReloadQmlAsync(string qmlSourcePath, CancellationToken cancellationToken = default)
        {
            reloadedQmlPaths.Add(qmlSourcePath);
            return Task.CompletedTask;
        }

        public Task SyncStateBatchAsync(
            string instanceId,
            IReadOnlyDictionary<string, object?> state,
            CancellationToken cancellationToken = default)
        {
            syncedInstanceIds.Add(instanceId);
            return Task.CompletedTask;
        }

        public Task RestoreSnapshotsAsync(
            IReadOnlyList<InstanceSnapshot> snapshots,
            CancellationToken cancellationToken = default)
        {
            RestoreSnapshotsCalled = true;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<InstanceInfo>> GetInstancesAsync(CancellationToken cancellationToken = default)
        {
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
