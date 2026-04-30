#pragma warning disable MA0048

using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>Orchestrates the full DSL generation pipeline.</summary>
    public interface IGenerationPipeline
    {
        Task<GenerationResult> Generate(IRegistryQuery registry, GenerationOptions options);

        Task<GeneratedPackage> GenerateModule(
            IRegistryQuery registry,
            string moduleUri,
            GenerationOptions options);

        Task<GeneratedTypeCode> GenerateType(
            IRegistryQuery registry,
            string qualifiedName,
            GenerationOptions options);

        void OnProgress(Action<GenerationProgress> callback);
    }

    /// <summary>Result of a full generation pipeline run.</summary>
    public sealed record GenerationResult(
        ImmutableArray<GeneratedPackage> Packages,
        GenerationStats Stats,
        ImmutableArray<GenerationWarning> Warnings,
        ImmutableArray<SkippedType> SkippedTypes);

    /// <summary>Aggregate generation statistics.</summary>
    public sealed record GenerationStats(
        int TotalPackages,
        int TotalTypes,
        int TotalFiles,
        long TotalBytes,
        TimeSpan ElapsedTime);

    /// <summary>A warning produced during generation.</summary>
    public sealed record GenerationWarning(
        GenerationWarningCode Code,
        string Message,
        string? TypeName,
        string? ModuleUri);

    /// <summary>A type that was skipped during generation.</summary>
    public sealed record SkippedType(
        string TypeName,
        string ModuleUri,
        string Reason);

    /// <summary>Progress report emitted during generation.</summary>
    public sealed record GenerationProgress(
        GenerationPhase Phase,
        int CurrentStep,
        int TotalSteps,
        string? Detail);

    /// <summary>Phases of the generation pipeline.</summary>
    public enum GenerationPhase
    {
        ResolvingInheritance,
        GeneratingProperties,
        GeneratingSignals,
        GeneratingMethods,
        GeneratingEnums,
        GeneratingAttachedProps,
        GeneratingDefaultProps,
        GeneratingViewModels,
        EmittingCode,
        Packaging,
        Writing,
        Done,
    }

    /// <summary>Warning codes produced during generation.</summary>
    public enum GenerationWarningCode
    {
        UnresolvedBaseType,
        UnresolvedTypeReference,
        NameCollision,
        CircularInheritance,
        UnsupportedType,
        EmptyModule,
        DeprecatedType,
    }

    /// <summary>Options controlling the full generation pipeline.</summary>
    public sealed record GenerationOptions(
        ITypeMapper? TypeMapper,
        InheritanceOptions Inheritance,
        PropertyOptions Properties,
        SignalOptions Signals,
        EnumOptions Enums,
        FilterOptions Filter,
        ViewModelOptions ViewModel,
        CodeEmitOptions Emit,
        PackagerOptions Packager);

    /// <summary>Inheritance resolution options.</summary>
    public sealed record InheritanceOptions(
        int MaxDepth,
        bool IncludeQtObjectProperties);

    /// <summary>Property generation options.</summary>
    public sealed record PropertyOptions(
        bool GenerateBindMethods,
        bool GenerateReadonlyGetters,
        bool GenerateGroupedBuilders);

    /// <summary>Signal generation options.</summary>
    public sealed record SignalOptions(
        string HandlerPrefix,
        bool SimplifyNoArgHandlers);

    /// <summary>Enum generation options.</summary>
    public sealed record EnumOptions(
        bool GenerateFlagHelpers);

    /// <summary>Filtering options for generated types.</summary>
    public sealed record FilterOptions(
        bool CreatableOnly,
        ImmutableArray<string>? ExcludeTypes,
        bool ExcludeInternal,
        bool ExcludeDeprecated,
        QmlVersionRange? VersionRange);

    /// <summary>A version range filter.</summary>
    public sealed record QmlVersionRange(
        QmlVersion? MinVersion,
        QmlVersion? MaxVersion);

    /// <summary>ViewModel integration options.</summary>
    public sealed record ViewModelOptions(
        bool Enabled,
        string ProxyPrefix);

    /// <summary>Shared context passed through generator phases.</summary>
    public sealed record GenerationContext(
        ITypeMapper TypeMapper,
        INameRegistry NameRegistry,
        IRegistryQuery Registry,
        GenerationOptions Options,
        string CurrentModuleUri);
}

#pragma warning restore MA0048
