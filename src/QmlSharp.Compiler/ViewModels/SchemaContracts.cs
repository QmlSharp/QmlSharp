#pragma warning disable MA0048

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Extracts ViewModel schema metadata from discovered C# ViewModel classes.
    /// </summary>
    public interface IViewModelExtractor
    {
        /// <summary>Extracts one ViewModel schema.</summary>
        ViewModelSchema Extract(DiscoveredViewModel viewModel, ProjectContext context, IIdAllocator idAllocator);

        /// <summary>Extracts one ViewModel schema for a specific View binding.</summary>
        ViewModelSchema Extract(DiscoveredView view, ProjectContext context, IIdAllocator idAllocator);

        /// <summary>Extracts all ViewModel schemas.</summary>
        ImmutableArray<ViewModelSchema> ExtractAll(
            ImmutableArray<DiscoveredViewModel> viewModels,
            ProjectContext context,
            IIdAllocator idAllocator);

        /// <summary>Validates ViewModel attribute usage.</summary>
        ImmutableArray<CompilerDiagnostic> Validate(DiscoveredViewModel viewModel, ProjectContext context);
    }

    /// <summary>
    /// Internal ViewModel schema model used by downstream compiler stages.
    /// </summary>
    public sealed record ViewModelSchema(
        string SchemaVersion,
        string ClassName,
        string ModuleName,
        string ModuleUri,
        QmlVersion ModuleVersion,
        int Version,
        string CompilerSlotKey,
        ImmutableArray<StateEntry> Properties,
        ImmutableArray<CommandEntry> Commands,
        ImmutableArray<EffectEntry> Effects,
        LifecycleInfo Lifecycle);

    /// <summary>A ViewModel state property entry.</summary>
    public sealed record StateEntry(
        string Name,
        string Type,
        string? DefaultValue,
        bool ReadOnly,
        int MemberId,
        string? SourceName = null,
        bool Deferred = false);

    /// <summary>A ViewModel command entry.</summary>
    public sealed record CommandEntry(
        string Name,
        ImmutableArray<ParameterEntry> Parameters,
        int CommandId,
        string? SourceName = null,
        bool Async = false);

    /// <summary>A ViewModel effect entry.</summary>
    public sealed record EffectEntry(
        string Name,
        string PayloadType,
        int EffectId,
        ImmutableArray<ParameterEntry> Parameters,
        string? SourceName = null);

    /// <summary>A command or effect parameter entry.</summary>
    public sealed record ParameterEntry(string Name, string Type);

    /// <summary>ViewModel lifecycle capability flags.</summary>
    public sealed record LifecycleInfo(bool OnMounted, bool OnUnmounting, bool HotReload);
}

#pragma warning restore MA0048
