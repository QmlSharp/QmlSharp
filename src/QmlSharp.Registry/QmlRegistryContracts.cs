#pragma warning disable MA0048

namespace QmlSharp.Registry
{
    /// <summary>
    /// The unified, immutable Qt type registry. Built once from three source
    /// formats and queried many times during compilation.
    /// </summary>
    public sealed record QmlRegistry(
        ImmutableArray<QmlModule> Modules,
        ImmutableDictionary<string, QmlType> TypesByQualifiedName,
        ImmutableArray<QmlType> Builtins,
        int FormatVersion,
        string QtVersion,
        DateTimeOffset BuildTimestamp);

    /// <summary>A QML module with URI, version, dependencies, and type list.</summary>
    public sealed record QmlModule(
        string Uri,
        QmlVersion Version,
        ImmutableArray<string> Dependencies,
        ImmutableArray<string> Imports,
        ImmutableArray<QmlModuleType> Types);

    /// <summary>Reference to a type within a module, with export metadata.</summary>
    public sealed record QmlModuleType(
        string QualifiedName,
        string QmlName,
        QmlVersion ExportVersion);

    /// <summary>A fully resolved QML type in the unified IR.</summary>
    public sealed record QmlType(
        string QualifiedName,
        string? QmlName,
        string? ModuleUri,
        AccessSemantics AccessSemantics,
        string? Prototype,
        string? DefaultProperty,
        string? AttachedType,
        string? Extension,
        bool IsSingleton,
        bool IsCreatable,
        ImmutableArray<QmlTypeExport> Exports,
        ImmutableArray<QmlProperty> Properties,
        ImmutableArray<QmlSignal> Signals,
        ImmutableArray<QmlMethod> Methods,
        ImmutableArray<QmlEnum> Enums,
        ImmutableArray<string> Interfaces);

    /// <summary>Access semantics classification for a type.</summary>
    public enum AccessSemantics
    {
        Reference,
        Value,
        Sequence,
        None,
    }

    /// <summary>A type export declaration (module + version).</summary>
    public sealed record QmlTypeExport(
        string Module,
        string Name,
        QmlVersion Version);

    /// <summary>A QML property in the unified IR.</summary>
    public sealed record QmlProperty(
        string Name,
        string TypeName,
        bool IsReadonly,
        bool IsList,
        bool IsRequired,
        string? DefaultValue,
        string? NotifySignal);

    /// <summary>A QML signal in the unified IR.</summary>
    public sealed record QmlSignal(
        string Name,
        ImmutableArray<QmlParameter> Parameters);

    /// <summary>A QML method (Q_INVOKABLE) in the unified IR.</summary>
    public sealed record QmlMethod(
        string Name,
        string? ReturnType,
        ImmutableArray<QmlParameter> Parameters);

    /// <summary>A signal or method parameter.</summary>
    public sealed record QmlParameter(
        string Name,
        string TypeName);

    /// <summary>A QML enum in the unified IR.</summary>
    public sealed record QmlEnum(
        string Name,
        bool IsFlag,
        ImmutableArray<QmlEnumValue> Values);

    /// <summary>A single enum value.</summary>
    public sealed record QmlEnumValue(
        string Name,
        int? Value);

    /// <summary>Module-qualified version.</summary>
    public sealed record QmlVersion(int Major, int Minor)
    {
        public override string ToString()
        {
            return $"{Major}.{Minor}";
        }
    }
}

#pragma warning restore MA0048
