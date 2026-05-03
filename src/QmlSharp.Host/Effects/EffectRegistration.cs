namespace QmlSharp.Host.Effects
{
    /// <summary>Registration metadata for an effect exposed by a managed ViewModel instance.</summary>
    public sealed record EffectRegistration(string InstanceId, int EffectId, string EffectName);
}
