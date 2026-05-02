namespace QmlSharp.Compiler
{
    /// <summary>
    /// Configuration for the QmlSharp compiler pipeline.
    /// </summary>
    public sealed record CompilerOptions
    {
        /// <summary>Gets the MSBuild project file path to compile.</summary>
        public required string ProjectPath { get; init; }

        /// <summary>Gets the output directory for generated compiler artifacts.</summary>
        public required string OutputDir { get; init; }

        /// <summary>Gets the source-map output directory.</summary>
        public string? SourceMapDir { get; init; }

        /// <summary>Gets a value indicating whether source maps should be generated.</summary>
        public bool GenerateSourceMaps { get; init; } = true;

        /// <summary>Gets a value indicating whether generated QML should be formatted.</summary>
        public bool FormatQml { get; init; }

        /// <summary>Gets a value indicating whether generated QML should be linted.</summary>
        public bool LintQml { get; init; }

        /// <summary>Gets the QML module version for generated registrations.</summary>
        public QmlVersion ModuleVersion { get; init; } = new(1, 0);

        /// <summary>Gets the authoritative QML module URI prefix.</summary>
        public required string ModuleUriPrefix { get; init; }

        /// <summary>Gets source include glob patterns.</summary>
        public ImmutableArray<string> IncludePatterns { get; init; } = ImmutableArray.Create("**/*.cs");

        /// <summary>Gets source exclude glob patterns.</summary>
        public ImmutableArray<string> ExcludePatterns { get; init; } =
            ImmutableArray.Create("**/obj/**", "**/bin/**", "**/*Tests*/**");

        /// <summary>Gets the maximum diagnostic severity that still allows compilation to proceed.</summary>
        public DiagnosticSeverity MaxAllowedSeverity { get; init; } = DiagnosticSeverity.Warning;

        /// <summary>Gets a value indicating whether incremental compilation is enabled.</summary>
        public bool Incremental { get; init; } = true;

        /// <summary>Gets the incremental compiler cache directory.</summary>
        public string? CacheDir { get; init; }

        /// <summary>Gets additional Roslyn analyzer assembly paths.</summary>
        public ImmutableArray<string> AdditionalAnalyzers { get; init; } = ImmutableArray<string>.Empty;
    }
}
