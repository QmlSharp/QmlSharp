using QmlSharp.Host.Diagnostics;

namespace QmlSharp.Host.Effects
{
    /// <summary>Structured result for effect dispatch attempts.</summary>
    public sealed record EffectDispatchResult(
        EffectDispatchStatus Status,
        string? Message = null,
        RuntimeDiagnostic? Diagnostic = null)
    {
        /// <summary>Returns true when the effect reached native interop.</summary>
        public bool Succeeded => Status is EffectDispatchStatus.Dispatched or EffectDispatchStatus.Broadcast;

        /// <summary>Creates a successful single-instance dispatch result.</summary>
        public static EffectDispatchResult Dispatched()
        {
            return new EffectDispatchResult(EffectDispatchStatus.Dispatched);
        }

        /// <summary>Creates a successful broadcast result.</summary>
        public static EffectDispatchResult Broadcast()
        {
            return new EffectDispatchResult(EffectDispatchStatus.Broadcast);
        }
    }
}
