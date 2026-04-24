#pragma warning disable MA0048

using System.Collections.Frozen;

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
        DateTimeOffset BuildTimestamp)
    {
        internal QmlRegistryLookupIndexes LookupIndexes { get; init; } = QmlRegistryLookupIndexes.Empty;

        internal QmlRegistry WithLookupIndexes()
        {
            return this with
            {
                LookupIndexes = QmlRegistryLookupIndexes.Create(this),
            };
        }
    }

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

    internal sealed record QmlRegistryLookupIndexes(
        FrozenDictionary<string, QmlModule> ModulesByUri,
        FrozenDictionary<string, ImmutableArray<QmlType>> TypesByModuleUri,
        FrozenDictionary<(string ModuleUri, string QmlName), QmlType> TypesByModuleAndQmlName,
        FrozenDictionary<string, ImmutableArray<string>> InheritanceChainsByQualifiedName,
        FrozenDictionary<string, QmlType> BuiltinsByQualifiedName)
    {
        public static QmlRegistryLookupIndexes Empty { get; } = new(
            ModulesByUri: new Dictionary<string, QmlModule>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal),
            TypesByModuleUri: new Dictionary<string, ImmutableArray<QmlType>>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal),
            TypesByModuleAndQmlName: new Dictionary<(string ModuleUri, string QmlName), QmlType>(EqualityComparer<(string ModuleUri, string QmlName)>.Default)
                .ToFrozenDictionary(EqualityComparer<(string ModuleUri, string QmlName)>.Default),
            InheritanceChainsByQualifiedName: new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal),
            BuiltinsByQualifiedName: new Dictionary<string, QmlType>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal));

        public static QmlRegistryLookupIndexes Create(QmlRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            FrozenDictionary<string, QmlModule> modulesByUri = registry.Modules
                .OrderBy(module => module.Uri, StringComparer.Ordinal)
                .GroupBy(module => module.Uri, StringComparer.Ordinal)
                .ToDictionary(
                    grouping => grouping.Key,
                    grouping => grouping.First(),
                    StringComparer.Ordinal)
                .ToFrozenDictionary(StringComparer.Ordinal);

            FrozenDictionary<string, ImmutableArray<QmlType>> typesByModuleUri = registry.TypesByQualifiedName.Values
                .Where(type => type.ModuleUri is not null)
                .GroupBy(type => type.ModuleUri!, StringComparer.Ordinal)
                .ToDictionary(
                    grouping => grouping.Key,
                    grouping => grouping
                        .OrderBy(type => type.QmlName ?? type.QualifiedName, StringComparer.Ordinal)
                        .ThenBy(type => type.QualifiedName, StringComparer.Ordinal)
                        .ToImmutableArray(),
                    StringComparer.Ordinal)
                .ToFrozenDictionary(StringComparer.Ordinal);

            Dictionary<(string ModuleUri, string QmlName), QmlType> typesByModuleAndQmlName =
                new(EqualityComparer<(string ModuleUri, string QmlName)>.Default);

            foreach (QmlType type in registry.TypesByQualifiedName.Values
                .Where(type => type.ModuleUri is not null && type.QmlName is not null)
                .OrderBy(type => type.ModuleUri, StringComparer.Ordinal)
                .ThenBy(type => type.QmlName, StringComparer.Ordinal)
                .ThenBy(type => type.QualifiedName, StringComparer.Ordinal))
            {
                _ = typesByModuleAndQmlName.TryAdd((type.ModuleUri!, type.QmlName!), type);
            }

            FrozenDictionary<string, ImmutableArray<string>> inheritanceChainsByQualifiedName = registry.TypesByQualifiedName.Keys
                .OrderBy(typeName => typeName, StringComparer.Ordinal)
                .ToDictionary(
                    typeName => typeName,
                    typeName => BuildInheritanceChain(typeName, registry.TypesByQualifiedName),
                    StringComparer.Ordinal)
                .ToFrozenDictionary(StringComparer.Ordinal);

            FrozenDictionary<string, QmlType> builtinsByQualifiedName = registry.Builtins
                .OrderBy(type => type.QualifiedName, StringComparer.Ordinal)
                .ToDictionary(type => type.QualifiedName, StringComparer.Ordinal)
                .ToFrozenDictionary(StringComparer.Ordinal);

            return new QmlRegistryLookupIndexes(
                ModulesByUri: modulesByUri,
                TypesByModuleUri: typesByModuleUri,
                TypesByModuleAndQmlName: typesByModuleAndQmlName.ToFrozenDictionary(EqualityComparer<(string ModuleUri, string QmlName)>.Default),
                InheritanceChainsByQualifiedName: inheritanceChainsByQualifiedName,
                BuiltinsByQualifiedName: builtinsByQualifiedName);
        }

        private static ImmutableArray<string> BuildInheritanceChain(
            string qualifiedName,
            ImmutableDictionary<string, QmlType> typesByQualifiedName)
        {
            ImmutableArray<string>.Builder chain = ImmutableArray.CreateBuilder<string>();
            HashSet<string> visited = new(StringComparer.Ordinal);
            string? currentTypeName = qualifiedName;

            while (currentTypeName is not null
                && typesByQualifiedName.TryGetValue(currentTypeName, out QmlType? currentType)
                && visited.Add(currentTypeName))
            {
                chain.Add(currentTypeName);
                currentTypeName = currentType.Prototype;
            }

            return chain.ToImmutable();
        }
    }
}

#pragma warning restore MA0048
