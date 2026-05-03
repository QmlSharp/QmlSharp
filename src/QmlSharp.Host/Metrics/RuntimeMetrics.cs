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

        /// <summary>Total hot reload attempts coordinated by this runtime.</summary>
        public long TotalHotReloads { get; init; }

        /// <summary>Total failed hot reload attempts coordinated by this runtime.</summary>
        public long TotalHotReloadFailures { get; init; }

        /// <summary>Duration of the most recent hot reload attempt.</summary>
        public TimeSpan LastHotReloadDuration { get; init; }
    }
}
