#pragma warning disable MA0048

using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>Organizes generated type code into NuGet-ready package structures.</summary>
    public interface IModulePackager
    {
        GeneratedPackage PackageModule(
            QmlModule module,
            IReadOnlyDictionary<string, GeneratedTypeCode> resolvedTypes,
            PackagerOptions options);

        Task<WrittenPackageInfo> WritePackage(GeneratedPackage package, string outputDir);

        ImmutableArray<GeneratedPackage> PackageAll(
            IRegistryQuery registry,
            IReadOnlyDictionary<string, GeneratedTypeCode> allTypes,
            PackagerOptions options);
    }

    /// <summary>Maps between Qt module URIs and NuGet package names.</summary>
    public interface IModuleMapper
    {
        string ToPackageName(string moduleUri);

        string ToModuleUri(string packageName);

        int GetPriority(string moduleUri);

        IReadOnlyDictionary<string, string> GetAllMappings();
    }

    /// <summary>A generated NuGet package ready to be written to disk.</summary>
    public sealed record GeneratedPackage(
        string PackageName,
        string ModuleUri,
        ImmutableArray<GeneratedFile> Files,
        int Types,
        ImmutableArray<string> Dependencies,
        PackageStats Stats);

    /// <summary>Statistics for a generated package.</summary>
    public sealed record PackageStats(
        int TotalTypes,
        int CreatableTypes,
        int NonCreatableTypes,
        int EnumCount,
        int AttachedTypeCount,
        int TotalLinesOfCode,
        int TotalFileSize);

    /// <summary>Options controlling package generation.</summary>
    public sealed record PackagerOptions(
        string OutputDir,
        string PackageVersion,
        string? PackagePrefix,
        bool GenerateReadme,
        bool GenerateProjectFile);

    /// <summary>Result of writing a package to disk.</summary>
    public sealed record WrittenPackageInfo(
        string PackageName,
        string OutputPath,
        int FileCount,
        long TotalBytes);
}

#pragma warning restore MA0048
