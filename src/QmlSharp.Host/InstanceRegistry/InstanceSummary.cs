namespace QmlSharp.Host.InstanceRegistry
{
    /// <summary>Brief diagnostic summary for an active or recently destroyed instance.</summary>
    public sealed record InstanceSummary(string InstanceId, string ClassName, InstanceState State);
}
