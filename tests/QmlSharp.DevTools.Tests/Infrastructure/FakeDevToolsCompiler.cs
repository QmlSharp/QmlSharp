namespace QmlSharp.DevTools.Tests.Infrastructure
{
    public sealed class FakeDevToolsCompiler : IDevToolsCompiler
    {
        private readonly Queue<CompilationResult> results = new();
        private readonly List<FakeCompilerRequest> requests = new();

        public IReadOnlyList<FakeCompilerRequest> Requests => requests;

        public void QueueResult(CompilationResult result)
        {
            results.Enqueue(result);
        }

        public Task<CompilationResult> CompileAsync(
            CompilerOptions options,
            CancellationToken cancellationToken = default)
        {
            requests.Add(new FakeCompilerRequest(options, Changes: null));
            return Task.FromResult(DequeueResult());
        }

        public Task<CompilationResult> CompileChangedAsync(
            CompilerOptions options,
            FileChangeBatch changes,
            CancellationToken cancellationToken = default)
        {
            requests.Add(new FakeCompilerRequest(options, changes));
            return Task.FromResult(DequeueResult());
        }

        private CompilationResult DequeueResult()
        {
            if (results.Count == 0)
            {
                return DevToolsTestFixtures.SuccessfulCompilationResult();
            }

            return results.Dequeue();
        }
    }

    public sealed record FakeCompilerRequest(
        CompilerOptions Options,
        FileChangeBatch? Changes);
}
