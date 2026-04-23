#pragma warning disable MA0048

using QmlSharp.Registry.Diagnostics;

namespace QmlSharp.Registry.Parsing
{
    /// <summary>Top-level parsed metatypes.json file (array of entries).</summary>
    public sealed record RawMetatypesFile(
        string SourcePath,
        ImmutableArray<RawMetatypesEntry> Entries,
        ImmutableArray<RegistryDiagnostic> Diagnostics);

    /// <summary>A single entry in a metatypes.json file.</summary>
    public sealed record RawMetatypesEntry(
        string? InputFile,
        ImmutableArray<RawMetatypesClass> Classes);

    /// <summary>A class definition from metatypes.json.</summary>
    public sealed record RawMetatypesClass(
        string ClassName,
        string? QualifiedClassName,
        bool IsObject,
        bool IsGadget,
        bool IsNamespace,
        ImmutableArray<RawMetatypesSuperClass> SuperClasses,
        ImmutableArray<RawMetatypesClassInfo> ClassInfos,
        ImmutableArray<RawMetatypesProperty> Properties,
        ImmutableArray<RawMetatypesSignal> Signals,
        ImmutableArray<RawMetatypesMethod> Methods,
        ImmutableArray<RawMetatypesEnum> Enums);

    /// <summary>Super class reference with access specifier.</summary>
    public sealed record RawMetatypesSuperClass(
        string Name,
        string Access);

    /// <summary>Class info key-value pair (e.g., QML.Element, QML.Attached, QML.Foreign).</summary>
    public sealed record RawMetatypesClassInfo(
        string Name,
        string Value);

    /// <summary>Property from metatypes.json.</summary>
    public sealed record RawMetatypesProperty(
        string Name,
        string Type,
        string? Read,
        string? Write,
        string? Notify,
        string? BindableProperty,
        int Revision,
        int Index,
        bool IsReadonly,
        bool IsConstant,
        bool IsFinal,
        bool IsRequired);

    /// <summary>Signal from metatypes.json.</summary>
    public sealed record RawMetatypesSignal(
        string Name,
        ImmutableArray<RawMetatypesParameter> Arguments,
        int Revision);

    /// <summary>Method from metatypes.json.</summary>
    public sealed record RawMetatypesMethod(
        string Name,
        string? ReturnType,
        ImmutableArray<RawMetatypesParameter> Arguments,
        int Revision,
        bool IsCloned);

    /// <summary>Parameter to a signal or method.</summary>
    public sealed record RawMetatypesParameter(
        string Name,
        string Type);

    /// <summary>Enum from metatypes.json.</summary>
    public sealed record RawMetatypesEnum(
        string Name,
        string? Alias,
        bool IsFlag,
        bool IsClass,
        ImmutableArray<string> Values);
}

#pragma warning restore MA0048
