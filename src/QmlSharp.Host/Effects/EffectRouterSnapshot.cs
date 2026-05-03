namespace QmlSharp.Host.Effects
{
    internal sealed record EffectRouterSnapshot(IReadOnlyList<EffectRouterSnapshot.Registration> Registrations)
    {
        internal sealed record Registration(int EffectId, string EffectName);
    }
}
