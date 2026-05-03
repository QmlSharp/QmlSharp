namespace QmlSharp.Host.StateSynchronization
{
    /// <summary>Schema-derived state property metadata used to validate managed sync calls.</summary>
    public sealed record StateSyncPropertyMetadata(string Name, StateSyncValueKind ValueKind);
}
