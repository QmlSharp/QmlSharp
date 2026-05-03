namespace QmlSharp.Host.StateSynchronization
{
    /// <summary>Describes one state value routed from managed code to a native instance.</summary>
    public sealed record StateSyncChange(string InstanceId, string PropertyName, StateSyncValueKind ValueKind);
}
