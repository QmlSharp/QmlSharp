namespace QmlSharp.Host.InstanceRegistry
{
    /// <summary>Brief diagnostic summary for an active or recently destroyed instance.</summary>
    public sealed record InstanceSummary(string InstanceId, string ClassName, InstanceState State)
    {
        /// <summary>Stable schema identifier associated with the instance.</summary>
        public string? SchemaId { get; init; }

        /// <summary>Compiler-assigned key used for hot reload matching.</summary>
        public string? CompilerSlotKey { get; init; }

        /// <summary>Number of commands currently queued for the instance.</summary>
        public int QueuedCommandCount { get; init; }
    }
}
