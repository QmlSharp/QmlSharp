#pragma warning disable MA0048

using QmlSharp.Registry.Diagnostics;

namespace QmlSharp.Registry.Parsing
{
    /// <summary>Top-level parsed .qmltypes file.</summary>
    public sealed record RawQmltypesFile(
        string SourcePath,
        ImmutableArray<RawQmltypesComponent> Components,
        ImmutableArray<RegistryDiagnostic> Diagnostics);

    /// <summary>A single Component definition from a .qmltypes file.</summary>
    public sealed record RawQmltypesComponent(
        string Name,
        string? AccessSemantics,
        string? Prototype,
        string? DefaultProperty,
        string? AttachedType,
        string? Extension,
        bool IsSingleton,
        bool IsCreatable,
        ImmutableArray<string> Exports,
        ImmutableArray<int> ExportMetaObjectRevisions,
        ImmutableArray<string> Interfaces,
        ImmutableArray<RawQmltypesProperty> Properties,
        ImmutableArray<RawQmltypesSignal> Signals,
        ImmutableArray<RawQmltypesMethod> Methods,
        ImmutableArray<RawQmltypesEnum> Enums);

    /// <summary>A property definition from a .qmltypes Component.</summary>
    public sealed record RawQmltypesProperty(
        string Name,
        string Type,
        bool IsReadonly,
        bool IsList,
        bool IsPointer,
        bool IsRequired,
        string? Read,
        string? Write,
        string? Notify,
        string? BindableProperty,
        int Revision);

    /// <summary>A signal definition from a .qmltypes Component.</summary>
    public sealed record RawQmltypesSignal(
        string Name,
        ImmutableArray<RawQmltypesParameter> Parameters,
        int Revision);

    /// <summary>A method definition from a .qmltypes Component.</summary>
    public sealed record RawQmltypesMethod(
        string Name,
        string? ReturnType,
        ImmutableArray<RawQmltypesParameter> Parameters,
        int Revision);

    /// <summary>A parameter to a signal or method.</summary>
    public sealed record RawQmltypesParameter(
        string Name,
        string Type);

    /// <summary>An enum definition from a .qmltypes Component.</summary>
    public sealed record RawQmltypesEnum(
        string Name,
        string? Alias,
        bool IsFlag,
        ImmutableArray<string> Values);
}

#pragma warning restore MA0048
