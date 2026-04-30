#pragma warning disable MA0048

namespace QmlSharp.Dsl.Generator
{
    /// <summary>Emits C# source code from generated type metadata.</summary>
    public interface ICodeEmitter
    {
        string EmitTypeFile(GeneratedTypeCode typeCode, CodeEmitOptions options);

        string EmitIndexFile(
            ImmutableArray<GeneratedTypeCode> types,
            ImmutableArray<GeneratedEnum> enums);

        string EmitProjectFile(GeneratedPackage package);

        string EmitReadme(GeneratedPackage package);

        string EmitCommonTypes();

        string EmitViewModelHelpers(ViewModelBindingInfo integration);
    }

    /// <summary>Options controlling code emission behavior.</summary>
    public sealed record CodeEmitOptions(
        bool GenerateXmlDoc,
        bool MarkDeprecated,
        string? HeaderComment);

    /// <summary>All generated code metadata for a single QML type.</summary>
    public sealed record GeneratedTypeCode(
        string QmlName,
        string ModuleUri,
        string FactoryName,
        string PropsInterfaceName,
        string BuilderInterfaceName,
        string? FactoryMethodCode,
        ImmutableArray<GeneratedProperty> Properties,
        ImmutableArray<GeneratedSignal> Signals,
        ImmutableArray<GeneratedMethod> Methods,
        ImmutableArray<GeneratedEnum> Enums,
        ImmutableArray<GeneratedAttachedType> AttachedTypes,
        DefaultPropertyInfo? DefaultProperty,
        bool IsCreatable,
        bool IsDeprecated);

    /// <summary>A single generated output file.</summary>
    public sealed record GeneratedFile(
        string RelativePath,
        string Content,
        GeneratedFileKind Kind);

    /// <summary>Classification of generated file types.</summary>
    public enum GeneratedFileKind
    {
        TypeFile,
        IndexFile,
        ProjectFile,
        ReadmeFile,
        CommonTypes,
        ViewModelHelper,
    }
}

#pragma warning restore MA0048
