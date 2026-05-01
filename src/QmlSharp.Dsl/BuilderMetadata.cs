using System.Collections.Immutable;

#pragma warning disable MA0048

namespace QmlSharp.Dsl
{
    /// <summary>
    /// Metadata for mapping generated builder method names to QML members.
    /// </summary>
    /// <param name="Properties">Property setter method metadata.</param>
    /// <param name="GroupedProperties">Grouped property callback method metadata.</param>
    /// <param name="AttachedProperties">Attached property callback method metadata.</param>
    /// <param name="Signals">Signal handler method metadata.</param>
    public sealed record ObjectBuilderMetadata(
        ImmutableArray<PropertyMethodMetadata> Properties,
        ImmutableArray<GroupedPropertyMethodMetadata> GroupedProperties,
        ImmutableArray<AttachedPropertyMethodMetadata> AttachedProperties,
        ImmutableArray<SignalMethodMetadata> Signals)
    {
        /// <summary>Gets empty metadata. Convention-based mapping is used where possible.</summary>
        public static ObjectBuilderMetadata Empty { get; } = new(
            ImmutableArray<PropertyMethodMetadata>.Empty,
            ImmutableArray<GroupedPropertyMethodMetadata>.Empty,
            ImmutableArray<AttachedPropertyMethodMetadata>.Empty,
            ImmutableArray<SignalMethodMetadata>.Empty);
    }

    /// <summary>
    /// Metadata for a generated property setter method.
    /// </summary>
    /// <param name="MethodName">Generated method name.</param>
    /// <param name="PropertyName">QML property name.</param>
    /// <param name="SupportsValue">Whether a literal setter is available.</param>
    /// <param name="SupportsBinding">Whether a binding-expression setter is available.</param>
    public sealed record PropertyMethodMetadata(
        string MethodName,
        string PropertyName,
        bool SupportsValue = true,
        bool SupportsBinding = true);

    /// <summary>
    /// Metadata for a grouped property callback method.
    /// </summary>
    /// <param name="MethodName">Generated method name.</param>
    /// <param name="GroupName">QML group name.</param>
    /// <param name="Collector">Collector metadata for the callback surface.</param>
    public sealed record GroupedPropertyMethodMetadata(
        string MethodName,
        string GroupName,
        PropertyCollectorMetadata Collector);

    /// <summary>
    /// Metadata for an attached property callback method.
    /// </summary>
    /// <param name="MethodName">Generated method name.</param>
    /// <param name="AttachedTypeName">QML attached type name.</param>
    /// <param name="Collector">Collector metadata for the callback surface.</param>
    public sealed record AttachedPropertyMethodMetadata(
        string MethodName,
        string AttachedTypeName,
        PropertyCollectorMetadata Collector);

    /// <summary>
    /// Metadata for a generated signal handler method.
    /// </summary>
    /// <param name="MethodName">Generated method name.</param>
    /// <param name="HandlerName">QML handler name.</param>
    public sealed record SignalMethodMetadata(string MethodName, string HandlerName);

    /// <summary>
    /// Metadata for generated grouped or attached property collector interfaces.
    /// </summary>
    /// <param name="Properties">Allowed property methods.</param>
    /// <param name="Signals">Allowed signal handler methods.</param>
    public sealed record PropertyCollectorMetadata(
        ImmutableArray<PropertyMethodMetadata> Properties,
        ImmutableArray<SignalMethodMetadata> Signals)
    {
        /// <summary>Gets empty metadata. Convention-based mapping is used where possible.</summary>
        public static PropertyCollectorMetadata Empty { get; } = new(
            ImmutableArray<PropertyMethodMetadata>.Empty,
            ImmutableArray<SignalMethodMetadata>.Empty);
    }
}

#pragma warning restore MA0048
