using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Fixtures
{
    internal sealed class TestRegistryQuery : IRegistryQuery
    {
        private static readonly IReadOnlyList<QmlSharp.Registry.Querying.ResolvedProperty> EmptyProperties = [];
        private static readonly IReadOnlyList<QmlSharp.Registry.Querying.ResolvedSignal> EmptySignals = [];
        private static readonly IReadOnlyList<QmlSharp.Registry.Querying.ResolvedMethod> EmptyMethods = [];

        private readonly IReadOnlyDictionary<string, QmlModule> modulesByUri;
        private readonly IReadOnlyDictionary<string, QmlType> typesByQualifiedName;

        public TestRegistryQuery(IReadOnlyList<QmlModule> modules, IReadOnlyList<QmlType> types, string qtVersion)
        {
            Modules = modules;
            Types = types;
            QtVersion = qtVersion;
            modulesByUri = modules.ToDictionary(module => module.Uri, module => module, StringComparer.Ordinal);
            typesByQualifiedName = types.ToDictionary(type => type.QualifiedName, type => type, StringComparer.Ordinal);
        }

        public IReadOnlyList<QmlModule> Modules { get; }

        public IReadOnlyList<QmlType> Types { get; }

        public string QtVersion { get; }

        public QmlModule? FindModule(string moduleUri)
        {
            return modulesByUri.GetValueOrDefault(moduleUri);
        }

        public IReadOnlyList<QmlModule> GetAllModules()
        {
            return Modules;
        }

        public IReadOnlyList<QmlType> GetModuleTypes(string moduleUri)
        {
            return Types
                .Where(type => string.Equals(type.ModuleUri, moduleUri, StringComparison.Ordinal))
                .OrderBy(type => type.QmlName ?? type.QualifiedName, StringComparer.Ordinal)
                .ToArray();
        }

        public QmlType? FindTypeByQualifiedName(string qualifiedName)
        {
            return typesByQualifiedName.GetValueOrDefault(qualifiedName);
        }

        public QmlType? FindTypeByQmlName(string moduleUri, string qmlName)
        {
            return Types.FirstOrDefault(type =>
                string.Equals(type.ModuleUri, moduleUri, StringComparison.Ordinal)
                && string.Equals(type.QmlName, qmlName, StringComparison.Ordinal));
        }

        public IReadOnlyList<QmlType> FindTypes(Func<QmlType, bool> predicate)
        {
            return Types.Where(predicate).ToArray();
        }

        public IReadOnlyList<QmlType> GetInheritanceChain(string qualifiedName)
        {
            List<QmlType> chain = [];
            HashSet<string> visited = new(StringComparer.Ordinal);
            string? current = qualifiedName;

            while (current is not null
                && visited.Add(current)
                && typesByQualifiedName.TryGetValue(current, out QmlType? type))
            {
                chain.Add(type);
                current = type.Prototype;
            }

            return chain;
        }

        public bool InheritsFrom(string qualifiedName, string baseQualifiedName)
        {
            return GetInheritanceChain(qualifiedName)
                .Skip(1)
                .Any(type => string.Equals(type.QualifiedName, baseQualifiedName, StringComparison.Ordinal));
        }

        public QmlSharp.Registry.Querying.ResolvedProperty? FindProperty(string qualifiedName, string propertyName)
        {
            return GetAllProperties(qualifiedName)
                .FirstOrDefault(property => string.Equals(property.Property.Name, propertyName, StringComparison.Ordinal));
        }

        public IReadOnlyList<QmlSharp.Registry.Querying.ResolvedProperty> GetAllProperties(string qualifiedName)
        {
            IReadOnlyList<QmlType> chain = GetInheritanceChain(qualifiedName);
            if (chain.Count == 0)
            {
                return EmptyProperties;
            }

            List<QmlSharp.Registry.Querying.ResolvedProperty> properties = [];
            HashSet<string> seen = new(StringComparer.Ordinal);
            for (int index = 0; index < chain.Count; index++)
            {
                QmlType type = chain[index];
                foreach (QmlProperty property in type.Properties.Where(property => seen.Add(property.Name)))
                {
                    properties.Add(new QmlSharp.Registry.Querying.ResolvedProperty(property, type, index > 0));
                }
            }

            return properties;
        }

        public QmlSharp.Registry.Querying.ResolvedSignal? FindSignal(string qualifiedName, string signalName)
        {
            return GetAllSignals(qualifiedName)
                .FirstOrDefault(signal => string.Equals(signal.Signal.Name, signalName, StringComparison.Ordinal));
        }

        public IReadOnlyList<QmlSharp.Registry.Querying.ResolvedSignal> GetAllSignals(string qualifiedName)
        {
            IReadOnlyList<QmlType> chain = GetInheritanceChain(qualifiedName);
            if (chain.Count == 0)
            {
                return EmptySignals;
            }

            List<QmlSharp.Registry.Querying.ResolvedSignal> signals = [];
            for (int index = 0; index < chain.Count; index++)
            {
                QmlType type = chain[index];
                foreach (QmlSignal signal in type.Signals)
                {
                    signals.Add(new QmlSharp.Registry.Querying.ResolvedSignal(signal, type, index > 0));
                }
            }

            return signals;
        }

        public IReadOnlyList<QmlSharp.Registry.Querying.ResolvedMethod> FindMethods(string qualifiedName, string methodName)
        {
            return GetAllMethods(qualifiedName)
                .Where(method => string.Equals(method.Method.Name, methodName, StringComparison.Ordinal))
                .ToArray();
        }

        public IReadOnlyList<QmlSharp.Registry.Querying.ResolvedMethod> GetAllMethods(string qualifiedName)
        {
            IReadOnlyList<QmlType> chain = GetInheritanceChain(qualifiedName);
            if (chain.Count == 0)
            {
                return EmptyMethods;
            }

            List<QmlSharp.Registry.Querying.ResolvedMethod> methods = [];
            for (int index = 0; index < chain.Count; index++)
            {
                QmlType type = chain[index];
                foreach (QmlMethod method in type.Methods)
                {
                    methods.Add(new QmlSharp.Registry.Querying.ResolvedMethod(method, type, index > 0));
                }
            }

            return methods;
        }

        public IReadOnlyList<QmlType> GetCreatableTypes()
        {
            return Types.Where(type => type.IsCreatable).ToArray();
        }

        public IReadOnlyList<QmlType> GetValueTypes()
        {
            return Types.Where(type => type.AccessSemantics == AccessSemantics.Value).ToArray();
        }

        public IReadOnlyList<QmlType> GetSingletonTypes()
        {
            return Types.Where(type => type.IsSingleton).ToArray();
        }

        public IReadOnlyList<QmlType> GetAttachedTypes()
        {
            return Types.Where(type => type.AttachedType is not null).ToArray();
        }

        public IReadOnlyList<QmlType> GetSequenceTypes()
        {
            return Types.Where(type => type.AccessSemantics == AccessSemantics.Sequence).ToArray();
        }

        public IReadOnlyList<QmlType> GetTypesByQualifiedName(params string[] qualifiedNames)
        {
            return qualifiedNames.Select(name => typesByQualifiedName[name]).ToArray();
        }
    }
}
