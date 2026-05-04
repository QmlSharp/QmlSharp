#pragma warning disable MA0048

namespace QmlSharp.Build
{
    /// <summary>Orchestrates the full 8-stage QmlSharp build pipeline.</summary>
    public interface IBuildPipeline
    {
        /// <summary>Runs the full build pipeline from Build Stage 1 to Build Stage 8.</summary>
        Task<BuildResult> BuildAsync(BuildContext context, CancellationToken cancellationToken = default);

        /// <summary>Runs only the specified phases.</summary>
        Task<BuildResult> BuildPhasesAsync(
            BuildContext context,
            ImmutableArray<BuildPhase> phases,
            CancellationToken cancellationToken = default);

        /// <summary>Registers a progress callback for build status reporting.</summary>
        void OnProgress(Action<BuildProgress> callback);
    }

    /// <summary>Immutable context for a single build invocation.</summary>
    public sealed record BuildContext
    {
        /// <summary>Resolved QmlSharp configuration.</summary>
        public required QmlSharpConfig Config { get; init; }

        /// <summary>Project root directory.</summary>
        public required string ProjectDir { get; init; }

        /// <summary>Output directory for this invocation.</summary>
        public required string OutputDir { get; init; }

        /// <summary>Resolved Qt SDK directory.</summary>
        public required string QtDir { get; init; }

        /// <summary>True when the build should bypass incremental cache state.</summary>
        public bool ForceRebuild { get; init; }

        /// <summary>True when building reusable library output.</summary>
        public bool LibraryMode { get; init; }

        /// <summary>Optional source file filter.</summary>
        public string? FileFilter { get; init; }
    }

    /// <summary>The 8 canonical build stages.</summary>
    public enum BuildPhase
    {
        /// <summary>Build Stage 1: configuration loading.</summary>
        ConfigLoading = 1,

        /// <summary>Build Stage 2: C# compilation.</summary>
        CSharpCompilation = 2,

        /// <summary>Build Stage 3: module metadata generation.</summary>
        ModuleMetadata = 3,

        /// <summary>Build Stage 4: dependency resolution.</summary>
        DependencyResolution = 4,

        /// <summary>Build Stage 5: asset bundling.</summary>
        AssetBundling = 5,

        /// <summary>Build Stage 6: QML validation.</summary>
        QmlValidation = 6,

        /// <summary>Build Stage 7: C++ code generation and native build.</summary>
        CppCodeGenAndBuild = 7,

        /// <summary>Build Stage 8: output assembly.</summary>
        OutputAssembly = 8,
    }

    /// <summary>Complete result of a build pipeline execution.</summary>
    public sealed record BuildResult
    {
        /// <summary>True when the build succeeded.</summary>
        public required bool Success { get; init; }

        /// <summary>Per-phase results.</summary>
        public required ImmutableArray<PhaseResult> PhaseResults { get; init; }

        /// <summary>Build diagnostics.</summary>
        public required ImmutableArray<BuildDiagnostic> Diagnostics { get; init; }

        /// <summary>Build timing and output statistics.</summary>
        public required BuildStats Stats { get; init; }

        /// <summary>Product manifest when output assembly produced one.</summary>
        public ProductManifest? Manifest { get; init; }
    }

    /// <summary>Result of a single build stage.</summary>
    /// <param name="Phase">The build phase.</param>
    /// <param name="Success">True when the phase succeeded.</param>
    /// <param name="Duration">Phase execution duration.</param>
    /// <param name="Diagnostics">Diagnostics produced by the phase.</param>
    public sealed record PhaseResult(
        BuildPhase Phase,
        bool Success,
        TimeSpan Duration,
        ImmutableArray<BuildDiagnostic> Diagnostics);

    /// <summary>Build timing statistics.</summary>
    /// <param name="TotalDuration">Total build duration.</param>
    /// <param name="FilesCompiled">Number of C# files compiled.</param>
    /// <param name="SchemasGenerated">Number of schemas generated.</param>
    /// <param name="CppFilesGenerated">Number of C++ files generated.</param>
    /// <param name="AssetsCollected">Number of assets collected.</param>
    /// <param name="NativeLibBuilt">Whether the native library was built.</param>
    public sealed record BuildStats(
        TimeSpan TotalDuration,
        int FilesCompiled,
        int SchemasGenerated,
        int CppFilesGenerated,
        int AssetsCollected,
        bool NativeLibBuilt);

    /// <summary>Build progress report.</summary>
    /// <param name="Phase">Current build phase.</param>
    /// <param name="Description">Progress description.</param>
    /// <param name="CurrentStep">Current step number.</param>
    /// <param name="TotalSteps">Total step count.</param>
    public sealed record BuildProgress(
        BuildPhase Phase,
        string Description,
        int CurrentStep,
        int TotalSteps);
}

#pragma warning restore MA0048
