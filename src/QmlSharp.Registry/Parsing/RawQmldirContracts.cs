#pragma warning disable MA0048

using QmlSharp.Registry.Diagnostics;

namespace QmlSharp.Registry.Parsing
{
    /// <summary>Top-level parsed qmldir file.</summary>
    public sealed record RawQmldirFile(
        string SourcePath,
        string? Module,
        ImmutableArray<RawQmldirPlugin> Plugins,
        string? Classname,
        ImmutableArray<RawQmldirImport> Imports,
        ImmutableArray<RawQmldirImport> Depends,
        ImmutableArray<RawQmldirTypeEntry> TypeEntries,
        ImmutableArray<string> Designersupported,
        string? Typeinfo,
        ImmutableArray<RegistryDiagnostic> Diagnostics);

    /// <summary>A plugin declaration in qmldir.</summary>
    public sealed record RawQmldirPlugin(
        string Name,
        string? Path);

    /// <summary>An import or depends directive in qmldir.</summary>
    public sealed record RawQmldirImport(
        string Module,
        string? Version);

    /// <summary>A type entry mapping in qmldir.</summary>
    public sealed record RawQmldirTypeEntry(
        string Name,
        string Version,
        string FilePath,
        bool IsSingleton,
        bool IsInternal,
        string? StyleSelector);
}

#pragma warning restore MA0048
