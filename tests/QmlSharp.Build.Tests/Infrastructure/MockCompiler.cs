namespace QmlSharp.Build.Tests.Infrastructure
{
    public sealed class MockCompiler : ICompiler
    {
        private readonly List<Action<CompilationProgress>> progressCallbacks = [];

        public CompilationResult CompilationResult { get; set; } =
            CompilationResult.FromUnits(ImmutableArray<CompilationUnit>.Empty);

        public CompilerOptions? LastOptions { get; private set; }

        public CompilationResult Compile(CompilerOptions options)
        {
            LastOptions = options;
            foreach (Action<CompilationProgress> callback in progressCallbacks)
            {
                callback(new CompilationProgress(CompilationPhase.Done, 0, 0, "mock"));
            }

            return CompilationResult;
        }

        public CompilationUnit CompileFile(string filePath, ProjectContext context, CompilerOptions options)
        {
            return new CompilationUnit
            {
                SourceFilePath = filePath,
                ViewClassName = "MockView",
                ViewModelClassName = "MockViewModel",
            };
        }

        public OutputResult WriteOutput(CompilationResult result, CompilerOptions options)
        {
            return new OutputResult(
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty,
                null,
                ImmutableArray<string>.Empty,
                0);
        }

        public void OnProgress(Action<CompilationProgress> callback)
        {
            progressCallbacks.Add(callback);
        }
    }
}
