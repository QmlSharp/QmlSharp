namespace QmlSharp.DevTools.Tests.Infrastructure
{
    public sealed class FakeDevToolsCompiler : IDevToolsCompiler
    {
        private readonly object syncRoot = new();
        private readonly Queue<CompilationResult> results = new();
        private readonly List<FakeCompilerRequest> requests = new();

        public IReadOnlyList<FakeCompilerRequest> Requests
        {
            get
            {
                lock (syncRoot)
                {
                    return requests.ToImmutableArray();
                }
            }
        }

        public Func<FakeCompilerRequest, CancellationToken, Task<CompilationResult>>? OnCompileAsync { get; set; }

        public void QueueResult(CompilationResult result)
        {
            lock (syncRoot)
            {
                results.Enqueue(result);
            }
        }

        public Task<CompilationResult> CompileAsync(
            CompilerOptions options,
            CancellationToken cancellationToken = default)
        {
            return CompileCoreAsync(new FakeCompilerRequest(options, Changes: null), cancellationToken);
        }

        public Task<CompilationResult> CompileChangedAsync(
            CompilerOptions options,
            FileChangeBatch changes,
            CancellationToken cancellationToken = default)
        {
            return CompileCoreAsync(new FakeCompilerRequest(options, changes), cancellationToken);
        }

        private async Task<CompilationResult> CompileCoreAsync(
            FakeCompilerRequest request,
            CancellationToken cancellationToken)
        {
            lock (syncRoot)
            {
                requests.Add(request);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (OnCompileAsync is not null)
            {
                return await OnCompileAsync(request, cancellationToken).ConfigureAwait(false);
            }

            return DequeueResult();
        }

        private CompilationResult DequeueResult()
        {
            lock (syncRoot)
            {
                if (results.Count == 0)
                {
                    return DevToolsTestFixtures.SuccessfulCompilationResult();
                }

                return results.Dequeue();
            }
        }
    }

    public sealed record FakeCompilerRequest(
        CompilerOptions Options,
        FileChangeBatch? Changes);
}
