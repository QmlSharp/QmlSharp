#pragma warning disable MA0048

namespace QmlSharp.Registry.Querying
{
    /// <summary>
    /// Provides direct access to the underlying QmlRegistry data.
    /// This is the low-level data access interface; prefer IRegistryQuery
    /// for most query operations.
    /// </summary>
    public interface ITypeRegistry
    {
        QmlRegistry Registry { get; }

        IReadOnlyList<QmlModule> Modules { get; }

        IReadOnlyList<QmlType> Types { get; }

        string QtVersion { get; }

        int FormatVersion { get; }
    }

    /// <summary>
    /// Query engine for type lookup, inheritance resolution, and member queries.
    /// All methods are O(1) or O(depth) where depth is the inheritance chain length.
    /// Thread-safe — all data is immutable.
    /// </summary>
    public interface IRegistryQuery
    {
        QmlModule? FindModule(string moduleUri);

        IReadOnlyList<QmlModule> GetAllModules();

        IReadOnlyList<QmlType> GetModuleTypes(string moduleUri);

        QmlType? FindTypeByQualifiedName(string qualifiedName);

        QmlType? FindTypeByQmlName(string moduleUri, string qmlName);

        IReadOnlyList<QmlType> FindTypes(Func<QmlType, bool> predicate);

        IReadOnlyList<QmlType> GetInheritanceChain(string qualifiedName);

        bool InheritsFrom(string qualifiedName, string baseQualifiedName);

        ResolvedProperty? FindProperty(string qualifiedName, string propertyName);

        IReadOnlyList<ResolvedProperty> GetAllProperties(string qualifiedName);

        ResolvedSignal? FindSignal(string qualifiedName, string signalName);

        IReadOnlyList<ResolvedSignal> GetAllSignals(string qualifiedName);

        IReadOnlyList<ResolvedMethod> FindMethods(string qualifiedName, string methodName);

        IReadOnlyList<ResolvedMethod> GetAllMethods(string qualifiedName);

        IReadOnlyList<QmlType> GetCreatableTypes();

        IReadOnlyList<QmlType> GetValueTypes();

        IReadOnlyList<QmlType> GetSingletonTypes();

        IReadOnlyList<QmlType> GetAttachedTypes();

        IReadOnlyList<QmlType> GetSequenceTypes();
    }

    /// <summary>
    /// A property resolved through the inheritance chain, tracking
    /// which type in the chain declares the property.
    /// </summary>
    public sealed record ResolvedProperty(
        QmlProperty Property,
        QmlType DeclaringType,
        bool IsInherited);

    /// <summary>
    /// A signal resolved through the inheritance chain.
    /// </summary>
    public sealed record ResolvedSignal(
        QmlSignal Signal,
        QmlType DeclaringType,
        bool IsInherited);

    /// <summary>
    /// A method resolved through the inheritance chain.
    /// </summary>
    public sealed record ResolvedMethod(
        QmlMethod Method,
        QmlType DeclaringType,
        bool IsInherited);
}

#pragma warning restore MA0048
