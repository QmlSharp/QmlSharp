namespace QmlSharp.Host.Metrics
{
    /// <summary>Runtime counters surfaced by the host for diagnostics and dev tools.</summary>
    public sealed record RuntimeMetrics(
        int ActiveInstanceCount,
        long TotalInstancesCreated,
        long TotalInstancesDestroyed,
        long TotalStateSyncs,
        long TotalCommandsDispatched,
        long TotalEffectsEmitted,
        TimeSpan Uptime);
}
