#pragma warning disable MA0048

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using QmlSharp.Registry.Querying;

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
        ImmutableArray<QmlType> AllTypes,
        FrozenDictionary<string, QmlType> TypesByQualifiedName,
        FrozenDictionary<string, QmlModule> ModulesByUri,
        FrozenDictionary<string, ImmutableArray<QmlType>> TypesByModuleUri,
        FrozenDictionary<(string ModuleUri, string QmlName), QmlType> TypesByModuleAndQmlName,
        FrozenDictionary<string, ImmutableArray<string>> InheritanceChainsByQualifiedName,
        FrozenDictionary<string, ImmutableArray<QmlType>> InheritanceTypeChainsByQualifiedName,
        FrozenDictionary<string, ImmutableArray<ResolvedProperty>> PropertiesByQualifiedName,
        FrozenDictionary<(string QualifiedName, string PropertyName), ResolvedProperty> PropertiesByQualifiedNameAndName,
        FrozenDictionary<string, ImmutableArray<ResolvedSignal>> SignalsByQualifiedName,
        FrozenDictionary<(string QualifiedName, string SignalName), ImmutableArray<ResolvedSignal>> SignalsByQualifiedNameAndName,
        FrozenDictionary<string, ImmutableArray<ResolvedMethod>> MethodsByQualifiedName,
        FrozenDictionary<(string QualifiedName, string MethodName), ImmutableArray<ResolvedMethod>> MethodsByQualifiedNameAndName,
        FrozenDictionary<string, QmlType> BuiltinsByQualifiedName,
        ImmutableArray<QmlType> CreatableTypes,
        ImmutableArray<QmlType> ValueTypes,
        ImmutableArray<QmlType> SingletonTypes,
        ImmutableArray<QmlType> AttachedTypes,
        ImmutableArray<QmlType> SequenceTypes)
    {
        public bool IsPopulated =>
            TypesByQualifiedName.Count > 0
            || ModulesByUri.Count > 0
            || BuiltinsByQualifiedName.Count > 0;

        public static QmlRegistryLookupIndexes Empty { get; } = new(
            AllTypes: ImmutableArray<QmlType>.Empty,
            TypesByQualifiedName: new Dictionary<string, QmlType>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal),
            ModulesByUri: new Dictionary<string, QmlModule>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal),
            TypesByModuleUri: new Dictionary<string, ImmutableArray<QmlType>>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal),
            TypesByModuleAndQmlName: new Dictionary<(string ModuleUri, string QmlName), QmlType>(EqualityComparer<(string ModuleUri, string QmlName)>.Default)
                .ToFrozenDictionary(EqualityComparer<(string ModuleUri, string QmlName)>.Default),
            InheritanceChainsByQualifiedName: new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal),
            InheritanceTypeChainsByQualifiedName: new Dictionary<string, ImmutableArray<QmlType>>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal),
            PropertiesByQualifiedName: new Dictionary<string, ImmutableArray<ResolvedProperty>>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal),
            PropertiesByQualifiedNameAndName: new Dictionary<(string QualifiedName, string PropertyName), ResolvedProperty>(EqualityComparer<(string QualifiedName, string PropertyName)>.Default)
                .ToFrozenDictionary(EqualityComparer<(string QualifiedName, string PropertyName)>.Default),
            SignalsByQualifiedName: new Dictionary<string, ImmutableArray<ResolvedSignal>>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal),
            SignalsByQualifiedNameAndName: new Dictionary<(string QualifiedName, string SignalName), ImmutableArray<ResolvedSignal>>(EqualityComparer<(string QualifiedName, string SignalName)>.Default)
                .ToFrozenDictionary(EqualityComparer<(string QualifiedName, string SignalName)>.Default),
            MethodsByQualifiedName: new Dictionary<string, ImmutableArray<ResolvedMethod>>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal),
            MethodsByQualifiedNameAndName: new Dictionary<(string QualifiedName, string MethodName), ImmutableArray<ResolvedMethod>>(EqualityComparer<(string QualifiedName, string MethodName)>.Default)
                .ToFrozenDictionary(EqualityComparer<(string QualifiedName, string MethodName)>.Default),
            BuiltinsByQualifiedName: new Dictionary<string, QmlType>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal),
            CreatableTypes: ImmutableArray<QmlType>.Empty,
            ValueTypes: ImmutableArray<QmlType>.Empty,
            SingletonTypes: ImmutableArray<QmlType>.Empty,
            AttachedTypes: ImmutableArray<QmlType>.Empty,
            SequenceTypes: ImmutableArray<QmlType>.Empty);

        [SuppressMessage("Maintainability", "MA0051:Method is too long", Justification = "Registry query indexes are built in one deterministic pass to keep the immutable lookup graph coherent.")]
        public static QmlRegistryLookupIndexes Create(QmlRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            ImmutableArray<QmlType> allTypes = registry.TypesByQualifiedName.Values
                .OrderBy(type => type.QualifiedName, StringComparer.Ordinal)
                .ToImmutableArray();

            FrozenDictionary<string, QmlType> typesByQualifiedName = allTypes
                .ToDictionary(type => type.QualifiedName, type => type, StringComparer.Ordinal)
                .ToFrozenDictionary(StringComparer.Ordinal);

            FrozenDictionary<string, QmlModule> modulesByUri = registry.Modules
                .OrderBy(module => module.Uri, StringComparer.Ordinal)
                .GroupBy(module => module.Uri, StringComparer.Ordinal)
                .ToDictionary(
                    grouping => grouping.Key,
                    grouping => grouping.First(),
                    StringComparer.Ordinal)
                .ToFrozenDictionary(StringComparer.Ordinal);

            FrozenDictionary<string, ImmutableArray<QmlType>> typesByModuleUri = allTypes
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
            Dictionary<string, ImmutableArray<string>> inheritanceChainsByQualifiedName = new(StringComparer.Ordinal);
            Dictionary<string, ImmutableArray<QmlType>> inheritanceTypeChainsByQualifiedName = new(StringComparer.Ordinal);
            Dictionary<string, ImmutableArray<ResolvedProperty>> propertiesByQualifiedName = new(StringComparer.Ordinal);
            Dictionary<(string QualifiedName, string PropertyName), ResolvedProperty> propertiesByQualifiedNameAndName =
                new(EqualityComparer<(string QualifiedName, string PropertyName)>.Default);
            Dictionary<string, ImmutableArray<ResolvedSignal>> signalsByQualifiedName = new(StringComparer.Ordinal);
            Dictionary<(string QualifiedName, string SignalName), ImmutableArray<ResolvedSignal>> signalsByQualifiedNameAndName =
                new(EqualityComparer<(string QualifiedName, string SignalName)>.Default);
            Dictionary<string, ImmutableArray<ResolvedMethod>> methodsByQualifiedName = new(StringComparer.Ordinal);
            Dictionary<(string QualifiedName, string MethodName), ImmutableArray<ResolvedMethod>> methodsByQualifiedNameAndName =
                new(EqualityComparer<(string QualifiedName, string MethodName)>.Default);

            foreach (QmlType type in allTypes)
            {
                if (type.ModuleUri is not null && type.QmlName is not null)
                {
                    _ = typesByModuleAndQmlName.TryAdd((type.ModuleUri, type.QmlName), type);
                }

                ImmutableArray<string> inheritanceChain = BuildInheritanceChain(type.QualifiedName, typesByQualifiedName);
                ImmutableArray<QmlType> inheritanceTypeChain = inheritanceChain
                    .Select(typeName => typesByQualifiedName[typeName])
                    .ToImmutableArray();

                inheritanceChainsByQualifiedName.Add(type.QualifiedName, inheritanceChain);
                inheritanceTypeChainsByQualifiedName.Add(type.QualifiedName, inheritanceTypeChain);

                ImmutableArray<ResolvedProperty> resolvedProperties = BuildResolvedProperties(inheritanceTypeChain);
                propertiesByQualifiedName.Add(type.QualifiedName, resolvedProperties);
                foreach (ResolvedProperty property in resolvedProperties)
                {
                    _ = propertiesByQualifiedNameAndName.TryAdd((type.QualifiedName, property.Property.Name), property);
                }

                ImmutableArray<ResolvedSignal> resolvedSignals = BuildResolvedSignals(inheritanceTypeChain);
                signalsByQualifiedName.Add(type.QualifiedName, resolvedSignals);
                foreach (IGrouping<string, ResolvedSignal> signalGroup in resolvedSignals.GroupBy(signal => signal.Signal.Name, StringComparer.Ordinal))
                {
                    signalsByQualifiedNameAndName.Add((type.QualifiedName, signalGroup.Key), signalGroup.ToImmutableArray());
                }

                ImmutableArray<ResolvedMethod> resolvedMethods = BuildResolvedMethods(inheritanceTypeChain);
                methodsByQualifiedName.Add(type.QualifiedName, resolvedMethods);
                foreach (IGrouping<string, ResolvedMethod> methodGroup in resolvedMethods.GroupBy(method => method.Method.Name, StringComparer.Ordinal))
                {
                    methodsByQualifiedNameAndName.Add((type.QualifiedName, methodGroup.Key), methodGroup.ToImmutableArray());
                }
            }

            FrozenDictionary<string, QmlType> builtinsByQualifiedName = registry.Builtins
                .OrderBy(type => type.QualifiedName, StringComparer.Ordinal)
                .ToDictionary(type => type.QualifiedName, type => type, StringComparer.Ordinal)
                .ToFrozenDictionary(StringComparer.Ordinal);

            return new QmlRegistryLookupIndexes(
                AllTypes: allTypes,
                TypesByQualifiedName: typesByQualifiedName,
                ModulesByUri: modulesByUri,
                TypesByModuleUri: typesByModuleUri,
                TypesByModuleAndQmlName: typesByModuleAndQmlName.ToFrozenDictionary(EqualityComparer<(string ModuleUri, string QmlName)>.Default),
                InheritanceChainsByQualifiedName: inheritanceChainsByQualifiedName.ToFrozenDictionary(StringComparer.Ordinal),
                InheritanceTypeChainsByQualifiedName: inheritanceTypeChainsByQualifiedName.ToFrozenDictionary(StringComparer.Ordinal),
                PropertiesByQualifiedName: propertiesByQualifiedName.ToFrozenDictionary(StringComparer.Ordinal),
                PropertiesByQualifiedNameAndName: propertiesByQualifiedNameAndName.ToFrozenDictionary(EqualityComparer<(string QualifiedName, string PropertyName)>.Default),
                SignalsByQualifiedName: signalsByQualifiedName.ToFrozenDictionary(StringComparer.Ordinal),
                SignalsByQualifiedNameAndName: signalsByQualifiedNameAndName.ToFrozenDictionary(EqualityComparer<(string QualifiedName, string SignalName)>.Default),
                MethodsByQualifiedName: methodsByQualifiedName.ToFrozenDictionary(StringComparer.Ordinal),
                MethodsByQualifiedNameAndName: methodsByQualifiedNameAndName.ToFrozenDictionary(EqualityComparer<(string QualifiedName, string MethodName)>.Default),
                BuiltinsByQualifiedName: builtinsByQualifiedName,
                CreatableTypes: SelectCategoryTypes(allTypes, type => type.IsCreatable),
                ValueTypes: SelectCategoryTypes(allTypes, type => type.AccessSemantics == AccessSemantics.Value),
                SingletonTypes: SelectCategoryTypes(allTypes, type => type.IsSingleton),
                AttachedTypes: SelectCategoryTypes(allTypes, type => type.AttachedType is not null),
                SequenceTypes: SelectCategoryTypes(allTypes, type => type.AccessSemantics == AccessSemantics.Sequence));
        }

        private static ImmutableArray<string> BuildInheritanceChain(
            string qualifiedName,
            IReadOnlyDictionary<string, QmlType> typesByQualifiedName)
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

        private static ImmutableArray<ResolvedProperty> BuildResolvedProperties(IReadOnlyList<QmlType> inheritanceTypeChain)
        {
            ImmutableArray<ResolvedProperty>.Builder results = ImmutableArray.CreateBuilder<ResolvedProperty>();
            HashSet<string> seenProperties = new(StringComparer.Ordinal);

            for (int index = 0; index < inheritanceTypeChain.Count; index++)
            {
                QmlType type = inheritanceTypeChain[index];
                foreach (QmlProperty property in type.Properties.Where(property => seenProperties.Add(property.Name)))
                {
                    results.Add(new ResolvedProperty(property, type, index > 0));
                }
            }

            return results.ToImmutable();
        }

        private static ImmutableArray<ResolvedSignal> BuildResolvedSignals(IReadOnlyList<QmlType> inheritanceTypeChain)
        {
            ImmutableArray<ResolvedSignal>.Builder results = ImmutableArray.CreateBuilder<ResolvedSignal>();
            HashSet<string> seenSignals = new(StringComparer.Ordinal);

            for (int index = 0; index < inheritanceTypeChain.Count; index++)
            {
                QmlType type = inheritanceTypeChain[index];
                foreach (QmlSignal signal in type.Signals.Where(signal => seenSignals.Add(BuildSignalKey(signal))))
                {
                    results.Add(new ResolvedSignal(signal, type, index > 0));
                }
            }

            return results.ToImmutable();
        }

        private static string BuildSignalKey(QmlSignal signal)
        {
            return $"{signal.Name}({string.Join(";", signal.Parameters.Select(parameter => $"{parameter.TypeName.Length}:{parameter.TypeName}"))})";
        }

        private static ImmutableArray<ResolvedMethod> BuildResolvedMethods(IReadOnlyList<QmlType> inheritanceTypeChain)
        {
            ImmutableArray<ResolvedMethod>.Builder results = ImmutableArray.CreateBuilder<ResolvedMethod>();

            for (int index = 0; index < inheritanceTypeChain.Count; index++)
            {
                QmlType type = inheritanceTypeChain[index];
                foreach (QmlMethod method in type.Methods)
                {
                    results.Add(new ResolvedMethod(method, type, index > 0));
                }
            }

            return results.ToImmutable();
        }

        private static ImmutableArray<QmlType> SelectCategoryTypes(
            IEnumerable<QmlType> allTypes,
            Func<QmlType, bool> predicate)
        {
            return allTypes
                .Where(predicate)
                .OrderBy(type => type.QmlName ?? type.QualifiedName, StringComparer.Ordinal)
                .ThenBy(type => type.QualifiedName, StringComparer.Ordinal)
                .ToImmutableArray();
        }
    }
}

#pragma warning restore MA0048
