namespace QmlSharp.Host.StateSynchronization
{
    /// <summary>Primitive or JSON-backed value kind selected for native state synchronization.</summary>
    public enum StateSyncValueKind
    {
        /// <summary>UTF-8 string fast path.</summary>
        String,

        /// <summary>32-bit integer fast path.</summary>
        Int32,

        /// <summary>Double-precision floating point fast path.</summary>
        Double,

        /// <summary>Boolean fast path encoded as an ABI integer.</summary>
        Boolean,

        /// <summary>UTF-8 JSON fallback path.</summary>
        Json
    }
}
