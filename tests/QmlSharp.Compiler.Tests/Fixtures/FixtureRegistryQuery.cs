using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Compiler.Tests.Fixtures
{
    internal sealed class FixtureRegistryQuery : IRegistryQuery
    {
        private readonly QmlRegistry registry;

        public FixtureRegistryQuery(QmlRegistry registry)
        {
            this.registry = registry;
        }

        public QmlModule? FindModule(string moduleUri)
        {
            return registry.Modules.FirstOrDefault(module => StringComparer.Ordinal.Equals(module.Uri, moduleUri));
        }

        public ResolvedProperty? FindProperty(string qualifiedName, string propertyName)
        {
            return GetAllProperties(qualifiedName).FirstOrDefault(property => StringComparer.Ordinal.Equals(property.Property.Name, propertyName));
        }

        public ResolvedSignal? FindSignal(string qualifiedName, string signalName)
        {
            return GetAllSignals(qualifiedName).FirstOrDefault(signal => StringComparer.Ordinal.Equals(signal.Signal.Name, signalName));
        }

        public IReadOnlyList<ResolvedMethod> FindMethods(string qualifiedName, string methodName)
        {
            return GetAllMethods(qualifiedName).Where(method => StringComparer.Ordinal.Equals(method.Method.Name, methodName)).ToImmutableArray();
        }

        public QmlType? FindTypeByQualifiedName(string qualifiedName)
        {
            return registry.TypesByQualifiedName.GetValueOrDefault(qualifiedName);
        }

        public QmlType? FindTypeByQmlName(string moduleUri, string qmlName)
        {
            return registry.TypesByQualifiedName.Values.FirstOrDefault(type =>
                StringComparer.Ordinal.Equals(type.ModuleUri, moduleUri)
                && StringComparer.Ordinal.Equals(type.QmlName, qmlName));
        }

        public IReadOnlyList<QmlType> FindTypes(Func<QmlType, bool> predicate)
        {
            return registry.TypesByQualifiedName.Values.Where(predicate).ToImmutableArray();
        }

        public IReadOnlyList<QmlModule> GetAllModules()
        {
            return registry.Modules;
        }

        public IReadOnlyList<ResolvedMethod> GetAllMethods(string qualifiedName)
        {
            return GetInheritanceChain(qualifiedName)
                .SelectMany((type, index) => type.Methods.Select(method => new ResolvedMethod(method, type, index > 0)))
                .ToImmutableArray();
        }

        public IReadOnlyList<ResolvedProperty> GetAllProperties(string qualifiedName)
        {
            return GetInheritanceChain(qualifiedName)
                .SelectMany((type, index) => type.Properties.Select(property => new ResolvedProperty(property, type, index > 0)))
                .ToImmutableArray();
        }

        public IReadOnlyList<ResolvedSignal> GetAllSignals(string qualifiedName)
        {
            return GetInheritanceChain(qualifiedName)
                .SelectMany((type, index) => type.Signals.Select(signal => new ResolvedSignal(signal, type, index > 0)))
                .ToImmutableArray();
        }

        public IReadOnlyList<QmlType> GetAttachedTypes()
        {
            return registry.TypesByQualifiedName.Values.Where(type => type.AttachedType is not null).ToImmutableArray();
        }

        public IReadOnlyList<QmlType> GetCreatableTypes()
        {
            return registry.TypesByQualifiedName.Values.Where(type => type.IsCreatable).ToImmutableArray();
        }

        public IReadOnlyList<QmlType> GetInheritanceChain(string qualifiedName)
        {
            ImmutableArray<QmlType>.Builder results = ImmutableArray.CreateBuilder<QmlType>();
            QmlType? current = FindTypeByQualifiedName(qualifiedName);

            while (current is not null)
            {
                results.Add(current);
                current = current.Prototype is null ? null : FindTypeByQualifiedName(current.Prototype);
            }

            return results.ToImmutable();
        }

        public IReadOnlyList<QmlType> GetModuleTypes(string moduleUri)
        {
            return registry.TypesByQualifiedName.Values
                .Where(type => StringComparer.Ordinal.Equals(type.ModuleUri, moduleUri))
                .ToImmutableArray();
        }

        public IReadOnlyList<QmlType> GetSequenceTypes()
        {
            return registry.TypesByQualifiedName.Values
                .Where(type => type.AccessSemantics == AccessSemantics.Sequence)
                .ToImmutableArray();
        }

        public IReadOnlyList<QmlType> GetSingletonTypes()
        {
            return registry.TypesByQualifiedName.Values.Where(type => type.IsSingleton).ToImmutableArray();
        }

        public IReadOnlyList<QmlType> GetValueTypes()
        {
            return registry.TypesByQualifiedName.Values
                .Where(type => type.AccessSemantics == AccessSemantics.Value)
                .ToImmutableArray();
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
    }
}
