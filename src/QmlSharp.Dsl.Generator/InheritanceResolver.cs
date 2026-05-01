#pragma warning disable MA0048

using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>Default implementation of QML type inheritance resolution.</summary>
    public sealed class InheritanceResolver : IInheritanceResolver
    {
        private const int DefaultMaxDepth = 32;

        private readonly InheritanceOptions options;

        public InheritanceResolver()
            : this(new InheritanceOptions(DefaultMaxDepth, IncludeQtObjectProperties: true))
        {
        }

        public InheritanceResolver(InheritanceOptions options)
        {
            if (options.MaxDepth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxDepth, "MaxDepth must be at least 1.");
            }

            this.options = options;
        }

        public ResolvedType Resolve(QmlType type, IRegistryQuery registry)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(registry);

            ImmutableArray<QmlType> inheritanceChain = BuildInheritanceChain(type, registry);

            return new ResolvedType(
                Type: type,
                InheritanceChain: inheritanceChain,
                AllProperties: ResolveProperties(inheritanceChain),
                AllSignals: ResolveSignals(inheritanceChain),
                AllMethods: ResolveMethods(inheritanceChain),
                AllEnums: ResolveEnums(inheritanceChain),
                AttachedType: ResolveOptionalType(type.AttachedType, type, registry),
                ExtensionType: ResolveOptionalType(type.Extension, type, registry));
        }

        public IReadOnlyDictionary<string, ResolvedType> ResolveModule(QmlModule module, IRegistryQuery registry)
        {
            ArgumentNullException.ThrowIfNull(module);
            ArgumentNullException.ThrowIfNull(registry);

            Dictionary<string, ResolvedType> resolvedTypes = new(StringComparer.Ordinal);
            foreach (QmlType type in module.Types
                         .OrderBy(moduleType => moduleType.QmlName, StringComparer.Ordinal)
                         .ThenBy(moduleType => moduleType.QualifiedName, StringComparer.Ordinal)
                         .Select(moduleType => registry.FindTypeByQualifiedName(moduleType.QualifiedName)
                             ?? registry.FindTypeByQmlName(module.Uri, moduleType.QmlName))
                         .Where(type => type is not null)
                         .Select(type => type!))
            {
                try
                {
                    ResolvedType resolvedType = Resolve(type, registry);
                    string key = type.QmlName ?? type.QualifiedName;
                    resolvedTypes[key] = resolvedType;
                }
                catch (DslGenerationException exception) when (IsSkippableResolverDiagnostic(exception))
                {
                    continue;
                }
            }

            return resolvedTypes;
        }

        public IReadOnlyList<QmlType> GetInheritanceChain(QmlType type, IRegistryQuery registry)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(registry);

            return BuildInheritanceChain(type, registry);
        }

        public IReadOnlyList<QmlType> GetDirectSubtypes(string typeName, IRegistryQuery registry)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
            ArgumentNullException.ThrowIfNull(registry);

            QmlType? targetType = ResolveTypeName(typeName, moduleUri: null, registry);
            string targetName = targetType?.QualifiedName ?? typeName;

            return registry.FindTypes(type =>
                    string.Equals(ResolveTypeName(type.Prototype, type.ModuleUri, registry)?.QualifiedName, targetName, StringComparison.Ordinal))
                .OrderBy(type => type.QmlName ?? type.QualifiedName, StringComparer.Ordinal)
                .ThenBy(type => type.QualifiedName, StringComparer.Ordinal)
                .ToArray();
        }

        public bool IsSubtypeOf(QmlType a, string b, IRegistryQuery registry)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentException.ThrowIfNullOrWhiteSpace(b);
            ArgumentNullException.ThrowIfNull(registry);

            QmlType? targetType = ResolveTypeName(b, a.ModuleUri, registry);
            string targetName = targetType?.QualifiedName ?? b;

            return BuildInheritanceChain(a, registry)
                .Skip(1)
                .Any(type =>
                    string.Equals(type.QualifiedName, targetName, StringComparison.Ordinal)
                    || string.Equals(type.QmlName, b, StringComparison.Ordinal));
        }

        private ImmutableArray<QmlType> BuildInheritanceChain(QmlType type, IRegistryQuery registry)
        {
            ImmutableArray<QmlType>.Builder chain = ImmutableArray.CreateBuilder<QmlType>();
            Dictionary<string, int> visitedIndexes = new(StringComparer.Ordinal);
            QmlType currentType = type;

            while (true)
            {
                if (visitedIndexes.TryGetValue(currentType.QualifiedName, out int cycleStartIndex))
                {
                    ImmutableArray<string> cycle = chain
                        .Skip(cycleStartIndex)
                        .Select(typeInCycle => typeInCycle.QualifiedName)
                        .Append(currentType.QualifiedName)
                        .ToImmutableArray();
                    throw new CircularInheritanceException(cycle);
                }

                if (chain.Count >= options.MaxDepth)
                {
                    throw new MaxDepthExceededException(type.QualifiedName, options.MaxDepth);
                }

                visitedIndexes.Add(currentType.QualifiedName, chain.Count);
                chain.Add(currentType);

                if (string.IsNullOrWhiteSpace(currentType.Prototype))
                {
                    break;
                }

                QmlType? nextType = ResolveTypeName(currentType.Prototype, currentType.ModuleUri, registry);
                if (nextType is null)
                {
                    throw new TypeResolutionException(currentType.QualifiedName, currentType.Prototype);
                }

                currentType = nextType;
            }

            return FilterQtObjectPropertiesChain(chain.ToImmutable());
        }

        private ImmutableArray<QmlType> FilterQtObjectPropertiesChain(ImmutableArray<QmlType> chain)
        {
            if (options.IncludeQtObjectProperties)
            {
                return chain;
            }

            return chain
                .Select(type => IsQtObjectType(type) ? type with { Properties = ImmutableArray<QmlProperty>.Empty } : type)
                .ToImmutableArray();
        }

        private static ImmutableArray<ResolvedProperty> ResolveProperties(ImmutableArray<QmlType> inheritanceChain)
        {
            ImmutableArray<ResolvedProperty>.Builder resolvedProperties = ImmutableArray.CreateBuilder<ResolvedProperty>();
            HashSet<string> emittedPropertyNames = new(StringComparer.Ordinal);

            for (int index = 0; index < inheritanceChain.Length; index++)
            {
                QmlType declaringType = inheritanceChain[index];
                foreach (QmlProperty property in declaringType.Properties.OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    if (!emittedPropertyNames.Add(property.Name))
                    {
                        continue;
                    }

                    bool overridesAncestor = inheritanceChain
                        .Skip(index + 1)
                        .Any(ancestor => ancestor.Properties.Any(ancestorProperty =>
                            string.Equals(ancestorProperty.Name, property.Name, StringComparison.Ordinal)));
                    resolvedProperties.Add(new ResolvedProperty(property, declaringType, overridesAncestor));
                }
            }

            return resolvedProperties.ToImmutable();
        }

        private static ImmutableArray<ResolvedSignal> ResolveSignals(ImmutableArray<QmlType> inheritanceChain)
        {
            ImmutableArray<ResolvedSignal>.Builder resolvedSignals = ImmutableArray.CreateBuilder<ResolvedSignal>();
            HashSet<string> emittedSignalKeys = new(StringComparer.Ordinal);

            foreach (QmlType declaringType in inheritanceChain)
            {
                foreach (QmlSignal signal in declaringType.Signals
                             .OrderBy(signal => BuildSignalKey(signal), StringComparer.Ordinal)
                             .Where(signal => emittedSignalKeys.Add(BuildSignalKey(signal))))
                {
                    resolvedSignals.Add(new ResolvedSignal(signal, declaringType));
                }
            }

            return resolvedSignals.ToImmutable();
        }

        private static ImmutableArray<ResolvedMethod> ResolveMethods(ImmutableArray<QmlType> inheritanceChain)
        {
            ImmutableArray<ResolvedMethod>.Builder resolvedMethods = ImmutableArray.CreateBuilder<ResolvedMethod>();
            HashSet<string> emittedMethodKeys = new(StringComparer.Ordinal);

            foreach (QmlType declaringType in inheritanceChain)
            {
                foreach (QmlMethod method in declaringType.Methods
                             .OrderBy(method => BuildMethodKey(method), StringComparer.Ordinal)
                             .Where(method => emittedMethodKeys.Add(BuildMethodKey(method))))
                {
                    resolvedMethods.Add(new ResolvedMethod(method, declaringType));
                }
            }

            return resolvedMethods.ToImmutable();
        }

        private static ImmutableArray<QmlEnum> ResolveEnums(ImmutableArray<QmlType> inheritanceChain)
        {
            ImmutableArray<QmlEnum>.Builder resolvedEnums = ImmutableArray.CreateBuilder<QmlEnum>();
            HashSet<string> emittedEnumNames = new(StringComparer.Ordinal);

            foreach (QmlType declaringType in inheritanceChain)
            {
                foreach (QmlEnum enumDefinition in declaringType.Enums
                             .OrderBy(enumDefinition => enumDefinition.Name, StringComparer.Ordinal)
                             .Where(enumDefinition => emittedEnumNames.Add(enumDefinition.Name)))
                {
                    resolvedEnums.Add(enumDefinition);
                }
            }

            return resolvedEnums.ToImmutable();
        }

        private static QmlType? ResolveOptionalType(string? typeName, QmlType ownerType, IRegistryQuery registry)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            return ResolveTypeName(typeName, ownerType.ModuleUri, registry);
        }

        private static QmlType? ResolveTypeName(string? typeName, string? moduleUri, IRegistryQuery registry)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            QmlType? qualifiedMatch = registry.FindTypeByQualifiedName(typeName);
            if (qualifiedMatch is not null)
            {
                return qualifiedMatch;
            }

            if (moduleUri is not null)
            {
                QmlType? moduleQmlNameMatch = registry.FindTypeByQmlName(moduleUri, typeName);
                if (moduleQmlNameMatch is not null)
                {
                    return moduleQmlNameMatch;
                }
            }

            QmlType[] globalQmlNameMatches = registry.FindTypes(type => string.Equals(type.QmlName, typeName, StringComparison.Ordinal)).ToArray();
            return globalQmlNameMatches.Length == 1 ? globalQmlNameMatches[0] : null;
        }

        private static bool IsSkippableResolverDiagnostic(DslGenerationException exception)
        {
            return string.Equals(exception.DiagnosticCode, DslDiagnosticCodes.UnresolvedBaseType, StringComparison.Ordinal)
                || string.Equals(exception.DiagnosticCode, DslDiagnosticCodes.CircularInheritance, StringComparison.Ordinal)
                || string.Equals(exception.DiagnosticCode, DslDiagnosticCodes.MaxDepthExceeded, StringComparison.Ordinal);
        }

        private static bool IsQtObjectType(QmlType type)
        {
            return string.Equals(type.QualifiedName, "QObject", StringComparison.Ordinal)
                || string.Equals(type.QmlName, "QtObject", StringComparison.Ordinal);
        }

        private static string BuildSignalKey(QmlSignal signal)
        {
            return $"{signal.Name}({BuildParameterKey(signal.Parameters)})";
        }

        private static string BuildMethodKey(QmlMethod method)
        {
            return $"{method.Name}({BuildParameterKey(method.Parameters)})";
        }

        private static string BuildParameterKey(ImmutableArray<QmlParameter> parameters)
        {
            return string.Join(
                ";",
                parameters.Select(parameter => $"{parameter.TypeName.Length}:{parameter.TypeName}"));
        }
    }
}

#pragma warning restore MA0048
