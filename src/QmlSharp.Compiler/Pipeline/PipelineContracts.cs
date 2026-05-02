#pragma warning disable MA0048

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Top-level compiler pipeline.
    /// </summary>
    public interface ICompiler
    {
        /// <summary>Compiles the configured project.</summary>
        CompilationResult Compile(CompilerOptions options);

        /// <summary>Compiles one file in an existing project context.</summary>
        CompilationUnit CompileFile(string filePath, ProjectContext context, CompilerOptions options);

        /// <summary>Writes compilation artifacts to disk.</summary>
        OutputResult WriteOutput(CompilationResult result, CompilerOptions options);

        /// <summary>Registers a compilation progress callback.</summary>
        void OnProgress(Action<CompilationProgress> callback);
    }

    /// <summary>
    /// Watches source changes and triggers incremental compiler work.
    /// </summary>
    public interface ICompilerWatcher : IDisposable
    {
        /// <summary>Starts watching and compiling changes.</summary>
        Task StartAsync(
            CompilerOptions options,
            Action<CompilationResult>? onCompiled = null,
            CancellationToken cancellationToken = default);

        /// <summary>Stops watching.</summary>
        Task StopAsync();

        /// <summary>Gets the current watcher status.</summary>
        WatcherStatus Status { get; }

        /// <summary>Registers an error callback.</summary>
        void OnError(Action<Exception> handler);
    }

    /// <summary>Result of writing compiler outputs.</summary>
    public sealed record OutputResult(
        ImmutableArray<string> QmlFiles,
        ImmutableArray<string> SchemaFiles,
        string? EventBindingsFile,
        ImmutableArray<string> SourceMapFiles,
        long TotalBytes);

    /// <summary>Compilation progress notification.</summary>
    public sealed record CompilationProgress(CompilationPhase Phase, int CurrentFile, int TotalFiles, string? Detail);

    /// <summary>Compiler pipeline phases.</summary>
    public enum CompilationPhase
    {
        /// <summary>Project loading phase.</summary>
        LoadingProject,

        /// <summary>Roslyn analysis phase.</summary>
        Analyzing,

        /// <summary>ViewModel extraction phase.</summary>
        ExtractingViewModels,

        /// <summary>DSL transformation phase.</summary>
        TransformingDsl,

        /// <summary>V2 post-processing phase.</summary>
        PostProcessing,

        /// <summary>QML emission phase.</summary>
        EmittingQml,

        /// <summary>Artifact writing phase.</summary>
        WritingArtifacts,

        /// <summary>Compilation is complete.</summary>
        Done,
    }

    /// <summary>Compiler watcher status.</summary>
    public enum WatcherStatus
    {
        /// <summary>The watcher is idle.</summary>
        Idle,

        /// <summary>The watcher is active.</summary>
        Watching,

        /// <summary>The watcher is compiling.</summary>
        Compiling,

        /// <summary>The watcher encountered an error.</summary>
        Error,

        /// <summary>The watcher stopped.</summary>
        Stopped,
    }
}

#pragma warning restore MA0048
