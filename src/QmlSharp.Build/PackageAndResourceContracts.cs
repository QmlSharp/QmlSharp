#pragma warning disable MA0048

namespace QmlSharp.Build
{
    /// <summary>Discovers QmlSharp-related NuGet package manifests.</summary>
    public interface IPackageResolver
    {
        /// <summary>Scans NuGet dependencies for QmlSharp packages with manifests.</summary>
        ImmutableArray<ResolvedPackage> Resolve(string projectDir);

        /// <summary>Collects QML import paths from resolved packages.</summary>
        ImmutableArray<string> CollectImportPaths(ImmutableArray<ResolvedPackage> packages);

        /// <summary>Collects third-party schema files from resolved packages.</summary>
        ImmutableArray<string> CollectSchemas(ImmutableArray<ResolvedPackage> packages);
    }

    /// <summary>A resolved QmlSharp NuGet package.</summary>
    /// <param name="PackageId">Package identifier.</param>
    /// <param name="Version">Package version.</param>
    /// <param name="PackagePath">Resolved package path.</param>
    /// <param name="Manifest">Optional QmlSharp module manifest.</param>
    public sealed record ResolvedPackage(
        string PackageId,
        string Version,
        string PackagePath,
        PackageManifest? Manifest);

    /// <summary>QmlSharp package manifest from qmlsharp.module.json.</summary>
    /// <param name="PackageId">Package identifier.</param>
    /// <param name="ModuleUri">QML module URI.</param>
    /// <param name="ModuleVersion">QML module version.</param>
    /// <param name="QmlImportPaths">QML import paths provided by the package.</param>
    /// <param name="SchemaFiles">Schema files provided by the package.</param>
    public sealed record PackageManifest(
        string PackageId,
        string ModuleUri,
        QmlVersion ModuleVersion,
        ImmutableArray<string> QmlImportPaths,
        ImmutableArray<string> SchemaFiles);

    /// <summary>Collects and bundles application assets.</summary>
    public interface IResourceBundler
    {
        /// <summary>Scans asset directories and collects resources.</summary>
        ImmutableArray<ResourceEntry> Collect(QmlSharpConfig config);

        /// <summary>Copies collected resources to the output directory.</summary>
        ResourceBundleResult Bundle(ImmutableArray<ResourceEntry> resources, string outputDir);

        /// <summary>Generates QRC XML for collected resources.</summary>
        string GenerateQrc(ImmutableArray<ResourceEntry> resources);
    }

    /// <summary>A single resource entry discovered during scanning.</summary>
    /// <param name="SourcePath">Original source path.</param>
    /// <param name="RelativePath">Relative path in output.</param>
    /// <param name="Type">Resource type.</param>
    /// <param name="SizeBytes">Resource size in bytes.</param>
    public sealed record ResourceEntry(
        string SourcePath,
        string RelativePath,
        ResourceType Type,
        long SizeBytes);

    /// <summary>Resource type classification.</summary>
    public enum ResourceType
    {
        /// <summary>Image asset.</summary>
        Image,

        /// <summary>Font asset.</summary>
        Font,

        /// <summary>Icon asset.</summary>
        Icon,

        /// <summary>QML asset.</summary>
        Qml,

        /// <summary>Other asset.</summary>
        Other,
    }

    /// <summary>Result of resource bundling.</summary>
    /// <param name="FilesCopied">Number of copied files.</param>
    /// <param name="TotalBytes">Total copied byte count.</param>
    /// <param name="OutputPaths">Output paths written.</param>
    /// <param name="QrcPath">Optional QRC output path.</param>
    public sealed record ResourceBundleResult(
        int FilesCopied,
        long TotalBytes,
        ImmutableArray<string> OutputPaths,
        string? QrcPath);
}

#pragma warning restore MA0048
