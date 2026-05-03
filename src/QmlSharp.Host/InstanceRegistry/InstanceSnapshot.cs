namespace QmlSharp.Host.Instances
{
    /// <summary>Snapshot of an instance's state before a reload or diagnostic capture.</summary>
    public sealed record InstanceSnapshot(
        string InstanceId,
        string ClassName,
        string SchemaId,
        string CompilerSlotKey,
        IReadOnlyDictionary<string, object?> State,
        DateTimeOffset CreatedAt,
        DateTimeOffset? DisposedAt);
}
