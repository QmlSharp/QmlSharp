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
        TimeSpan Uptime)
    {
        /// <summary>Number of destroyed instances retained for diagnostics.</summary>
        public int DestroyedInstanceCount { get; init; }

        /// <summary>Total queued commands across active instances.</summary>
        public int QueuedCommandCount { get; init; }
    }
}
