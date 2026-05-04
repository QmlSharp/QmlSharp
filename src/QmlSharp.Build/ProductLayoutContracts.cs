#pragma warning disable MA0048

namespace QmlSharp.Build
{
    /// <summary>Manages the product output directory layout and manifest generation.</summary>
    public interface IProductLayout
    {
        /// <summary>Creates the output directory structure.</summary>
        void CreateDirectoryStructure(string outputDir);

        /// <summary>Assembles all build artifacts into the output directory.</summary>
        ProductAssemblyResult Assemble(BuildContext context, BuildArtifacts artifacts);

        /// <summary>Generates manifest.json with build metadata.</summary>
        string GenerateManifest(BuildResult result, BuildContext context);

        /// <summary>Validates that the output directory contains expected artifacts.</summary>
        ImmutableArray<BuildDiagnostic> ValidateOutput(string outputDir);
    }

    /// <summary>All artifacts collected from pipeline phases.</summary>
    public sealed record BuildArtifacts
    {
        /// <summary>Generated QML file paths.</summary>
        public ImmutableArray<string> QmlFiles { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>Generated schema file paths.</summary>
        public ImmutableArray<string> SchemaFiles { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>Generated event-bindings.json path.</summary>
        public string? EventBindingsFile { get; init; }

        /// <summary>Generated source map file paths.</summary>
        public ImmutableArray<string> SourceMapFiles { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>Generated module metadata file paths.</summary>
        public ImmutableArray<string> ModuleMetadataFiles { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>Bundled asset file paths.</summary>
        public ImmutableArray<string> AssetFiles { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>Compiled native library path.</summary>
        public string? NativeLibraryPath { get; init; }

        /// <summary>Compiled C# entry assembly path.</summary>
        public string? AssemblyPath { get; init; }
    }

    /// <summary>Result of product assembly.</summary>
    /// <param name="Success">True when assembly succeeded.</param>
    /// <param name="FilesCopied">Number of copied files.</param>
    /// <param name="TotalBytes">Total copied byte count.</param>
    /// <param name="OutputFiles">Output files written.</param>
    public sealed record ProductAssemblyResult(
        bool Success,
        int FilesCopied,
        long TotalBytes,
        ImmutableArray<string> OutputFiles);

    /// <summary>Product manifest written to manifest.json.</summary>
    /// <param name="ProjectName">Project name.</param>
    /// <param name="Version">Project version.</param>
    /// <param name="BuildMode">Build mode.</param>
    /// <param name="BuildTimestamp">Build timestamp.</param>
    /// <param name="QtVersion">Qt version.</param>
    /// <param name="DotNetVersion">.NET version.</param>
    /// <param name="QmlModules">QML modules in the product.</param>
    /// <param name="ViewModels">ViewModel names in the product.</param>
    /// <param name="FileHashes">Output file hashes.</param>
    /// <param name="NativeLib">Relative native library path.</param>
    /// <param name="ManagedAssembly">Relative managed assembly path.</param>
    public sealed record ProductManifest(
        string ProjectName,
        string Version,
        string BuildMode,
        DateTimeOffset BuildTimestamp,
        string QtVersion,
        string DotNetVersion,
        ImmutableArray<string> QmlModules,
        ImmutableArray<string> ViewModels,
        ImmutableDictionary<string, string> FileHashes,
        string NativeLib,
        string ManagedAssembly);
}

#pragma warning restore MA0048
