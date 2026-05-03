namespace QmlSharp.Host.Effects
{
    /// <summary>Effect invocation routed from managed code to QML.</summary>
    public sealed record EffectInvocation(string InstanceId, string EffectName, string PayloadJson);
}
