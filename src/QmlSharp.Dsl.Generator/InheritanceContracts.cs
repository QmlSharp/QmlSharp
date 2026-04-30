#pragma warning disable MA0048

using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>Resolves QML type inheritance chains and inherited members.</summary>
    public interface IInheritanceResolver
    {
        ResolvedType Resolve(QmlType type, IRegistryQuery registry);

        IReadOnlyDictionary<string, ResolvedType> ResolveModule(QmlModule module, IRegistryQuery registry);

        IReadOnlyList<QmlType> GetInheritanceChain(QmlType type, IRegistryQuery registry);

        IReadOnlyList<QmlType> GetDirectSubtypes(string typeName, IRegistryQuery registry);

        bool IsSubtypeOf(QmlType a, string b, IRegistryQuery registry);
    }

    /// <summary>A QML type with inherited members resolved.</summary>
    public sealed record ResolvedType(
        QmlType Type,
        ImmutableArray<QmlType> InheritanceChain,
        ImmutableArray<ResolvedProperty> AllProperties,
        ImmutableArray<ResolvedSignal> AllSignals,
        ImmutableArray<ResolvedMethod> AllMethods,
        ImmutableArray<QmlEnum> AllEnums,
        QmlType? AttachedType,
        QmlType? ExtensionType);

    /// <summary>A resolved property and the type that declares it.</summary>
    public sealed record ResolvedProperty(
        QmlProperty Property,
        QmlType DeclaredBy,
        bool IsOverridden);

    /// <summary>A resolved signal and the type that declares it.</summary>
    public sealed record ResolvedSignal(
        QmlSignal Signal,
        QmlType DeclaredBy);

    /// <summary>A resolved method and the type that declares it.</summary>
    public sealed record ResolvedMethod(
        QmlMethod Method,
        QmlType DeclaredBy);
}

#pragma warning restore MA0048
