using QmlSharp.Host.InstanceRegistry;

namespace QmlSharp.Host.Instances
{
    /// <summary>Detailed diagnostic information for a managed ViewModel instance.</summary>
    public sealed record InstanceInfo(
        string InstanceId,
        string ClassName,
        string SchemaId,
        string CompilerSlotKey,
        InstanceState State,
        IReadOnlyDictionary<string, object?> Properties,
        int QueuedCommandCount,
        long CommandsDispatched,
        long EffectsEmitted,
        DateTimeOffset CreatedAt,
        DateTimeOffset? DisposedAt);
}
