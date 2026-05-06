namespace QmlSharp.DevTools.Tests.Infrastructure
{
    public sealed class FakeBuildPipeline : IDevToolsBuildPipeline
    {
        private readonly Queue<BuildResult> results = new();
        private readonly List<FakeBuildRequest> requests = new();
        private readonly List<Action<BuildProgress>> callbacks = new();

        public IReadOnlyList<FakeBuildRequest> Requests => requests;

        public IReadOnlyList<Action<BuildProgress>> ProgressCallbacks => callbacks;

        public void QueueResult(BuildResult result)
        {
            results.Enqueue(result);
        }

        public Task<BuildResult> BuildAsync(
            BuildContext context,
            CancellationToken cancellationToken = default)
        {
            requests.Add(new FakeBuildRequest(context, ImmutableArray<BuildPhase>.Empty));
            return Task.FromResult(DequeueResult());
        }

        public Task<BuildResult> BuildPhasesAsync(
            BuildContext context,
            ImmutableArray<BuildPhase> phases,
            CancellationToken cancellationToken = default)
        {
            requests.Add(new FakeBuildRequest(context, phases));
            return Task.FromResult(DequeueResult());
        }

        public void OnProgress(Action<BuildProgress> callback)
        {
            callbacks.Add(callback);
        }

        private BuildResult DequeueResult()
        {
            if (results.Count == 0)
            {
                return DevToolsTestFixtures.SuccessfulBuildResult();
            }

            return results.Dequeue();
        }
    }

    public sealed record FakeBuildRequest(
        BuildContext Context,
        ImmutableArray<BuildPhase> Phases);
}
