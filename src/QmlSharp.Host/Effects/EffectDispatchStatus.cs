namespace QmlSharp.Host.Effects
{
    /// <summary>Structured result status for effect dispatch attempts.</summary>
    public enum EffectDispatchStatus
    {
        /// <summary>The effect was dispatched to native interop.</summary>
        Dispatched,

        /// <summary>The effect was broadcast through native interop.</summary>
        Broadcast,

        /// <summary>The target instance is not registered.</summary>
        UnknownInstance,

        /// <summary>The target effect identifier or name is not registered.</summary>
        UnknownEffect,

        /// <summary>The effect payload was not valid JSON.</summary>
        InvalidPayload,

        /// <summary>The native effect dispatch failed.</summary>
        NativeFailure,

        /// <summary>The router was disposed.</summary>
        Disposed
    }
}
