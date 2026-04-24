namespace QmlSharp.Registry.Querying
{
    internal sealed class RegistryQuery : IRegistryQuery
    {
        private static readonly ImmutableArray<QmlType> EmptyTypes = ImmutableArray<QmlType>.Empty;
        private static readonly ImmutableArray<ResolvedProperty> EmptyProperties = ImmutableArray<ResolvedProperty>.Empty;
        private static readonly ImmutableArray<ResolvedSignal> EmptySignals = ImmutableArray<ResolvedSignal>.Empty;
        private static readonly ImmutableArray<ResolvedMethod> EmptyMethods = ImmutableArray<ResolvedMethod>.Empty;

        private readonly QmlRegistry registry;

        public RegistryQuery(QmlRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);

            this.registry = registry.LookupIndexes.IsPopulated
                ? registry
                : registry.WithLookupIndexes();
        }

        public QmlModule? FindModule(string moduleUri)
        {
            if (string.IsNullOrEmpty(moduleUri))
            {
                return null;
            }

            return registry.LookupIndexes.ModulesByUri.TryGetValue(moduleUri, out QmlModule? module)
                ? module
                : null;
        }

        public IReadOnlyList<QmlModule> GetAllModules()
        {
            return registry.Modules;
        }

        public IReadOnlyList<QmlType> GetModuleTypes(string moduleUri)
        {
            if (string.IsNullOrEmpty(moduleUri))
            {
                return EmptyTypes;
            }

            return registry.LookupIndexes.TypesByModuleUri.TryGetValue(moduleUri, out ImmutableArray<QmlType> types)
                ? types
                : EmptyTypes;
        }

        public QmlType? FindTypeByQualifiedName(string qualifiedName)
        {
            if (string.IsNullOrEmpty(qualifiedName))
            {
                return null;
            }

            if (registry.LookupIndexes.TypesByQualifiedName.TryGetValue(qualifiedName, out QmlType? type))
            {
                return type;
            }

            return registry.LookupIndexes.BuiltinsByQualifiedName.TryGetValue(qualifiedName, out QmlType? builtin)
                ? builtin
                : null;
        }

        public QmlType? FindTypeByQmlName(string moduleUri, string qmlName)
        {
            if (string.IsNullOrEmpty(moduleUri) || string.IsNullOrEmpty(qmlName))
            {
                return null;
            }

            return registry.LookupIndexes.TypesByModuleAndQmlName.TryGetValue((moduleUri, qmlName), out QmlType? type)
                ? type
                : null;
        }

        public IReadOnlyList<QmlType> FindTypes(Func<QmlType, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            return registry.LookupIndexes.AllTypes
                .Where(predicate)
                .ToImmutableArray();
        }

        public IReadOnlyList<QmlType> GetInheritanceChain(string qualifiedName)
        {
            if (string.IsNullOrEmpty(qualifiedName))
            {
                return EmptyTypes;
            }

            return registry.LookupIndexes.InheritanceTypeChainsByQualifiedName.TryGetValue(qualifiedName, out ImmutableArray<QmlType> chain)
                ? chain
                : EmptyTypes;
        }

        public bool InheritsFrom(string qualifiedName, string baseQualifiedName)
        {
            if (string.IsNullOrEmpty(qualifiedName)
                || string.IsNullOrEmpty(baseQualifiedName)
                || StringComparer.Ordinal.Equals(qualifiedName, baseQualifiedName))
            {
                return false;
            }

            return GetInheritanceChain(qualifiedName)
                .Skip(1)
                .Any(type => StringComparer.Ordinal.Equals(type.QualifiedName, baseQualifiedName));
        }

        public ResolvedProperty? FindProperty(string qualifiedName, string propertyName)
        {
            if (string.IsNullOrEmpty(qualifiedName) || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            return registry.LookupIndexes.PropertiesByQualifiedNameAndName.TryGetValue((qualifiedName, propertyName), out ResolvedProperty? property)
                ? property
                : null;
        }

        public IReadOnlyList<ResolvedProperty> GetAllProperties(string qualifiedName)
        {
            if (string.IsNullOrEmpty(qualifiedName))
            {
                return EmptyProperties;
            }

            return registry.LookupIndexes.PropertiesByQualifiedName.TryGetValue(qualifiedName, out ImmutableArray<ResolvedProperty> properties)
                ? properties
                : EmptyProperties;
        }

        public ResolvedSignal? FindSignal(string qualifiedName, string signalName)
        {
            if (string.IsNullOrEmpty(qualifiedName) || string.IsNullOrEmpty(signalName))
            {
                return null;
            }

            return registry.LookupIndexes.SignalsByQualifiedNameAndName.TryGetValue((qualifiedName, signalName), out ResolvedSignal? signal)
                ? signal
                : null;
        }

        public IReadOnlyList<ResolvedSignal> GetAllSignals(string qualifiedName)
        {
            if (string.IsNullOrEmpty(qualifiedName))
            {
                return EmptySignals;
            }

            return registry.LookupIndexes.SignalsByQualifiedName.TryGetValue(qualifiedName, out ImmutableArray<ResolvedSignal> signals)
                ? signals
                : EmptySignals;
        }

        public IReadOnlyList<ResolvedMethod> FindMethods(string qualifiedName, string methodName)
        {
            if (string.IsNullOrEmpty(qualifiedName) || string.IsNullOrEmpty(methodName))
            {
                return EmptyMethods;
            }

            return registry.LookupIndexes.MethodsByQualifiedNameAndName.TryGetValue((qualifiedName, methodName), out ImmutableArray<ResolvedMethod> methods)
                ? methods
                : EmptyMethods;
        }

        public IReadOnlyList<ResolvedMethod> GetAllMethods(string qualifiedName)
        {
            if (string.IsNullOrEmpty(qualifiedName))
            {
                return EmptyMethods;
            }

            return registry.LookupIndexes.MethodsByQualifiedName.TryGetValue(qualifiedName, out ImmutableArray<ResolvedMethod> methods)
                ? methods
                : EmptyMethods;
        }

        public IReadOnlyList<QmlType> GetCreatableTypes()
        {
            return registry.LookupIndexes.CreatableTypes;
        }

        public IReadOnlyList<QmlType> GetValueTypes()
        {
            return registry.LookupIndexes.ValueTypes;
        }

        public IReadOnlyList<QmlType> GetSingletonTypes()
        {
            return registry.LookupIndexes.SingletonTypes;
        }

        public IReadOnlyList<QmlType> GetAttachedTypes()
        {
            return registry.LookupIndexes.AttachedTypes;
        }

        public IReadOnlyList<QmlType> GetSequenceTypes()
        {
            return registry.LookupIndexes.SequenceTypes;
        }
    }
}
