namespace QmlSharp.Build.Tests.Infrastructure
{
    public sealed class MockCompiler : ICompiler
    {
        private readonly List<Action<CompilationProgress>> progressCallbacks = [];

        public CompilationResult CompilationResult { get; set; } =
            CompilationResult.FromUnits(ImmutableArray<CompilationUnit>.Empty);

        public OutputResult OutputResult { get; set; } =
            new(
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty,
                null,
                ImmutableArray<string>.Empty,
                0);

        public Func<CompilationResult, CompilerOptions, OutputResult>? WriteOutputHandler { get; set; }

        public Exception? CompileException { get; set; }

        public int CompileCallCount { get; private set; }

        public int WriteOutputCallCount { get; private set; }

        public CompilerOptions? LastOptions { get; private set; }

        public CompilerOptions? LastWriteOptions { get; private set; }

        public CompilationResult Compile(CompilerOptions options)
        {
            if (CompileException is not null)
            {
                throw CompileException;
            }

            CompileCallCount++;
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
            WriteOutputCallCount++;
            LastWriteOptions = options;
            if (WriteOutputHandler is not null)
            {
                return WriteOutputHandler(result, options);
            }

            return OutputResult;
        }

        public void OnProgress(Action<CompilationProgress> callback)
        {
            progressCallbacks.Add(callback);
        }
    }
}
