using QmlSharp.Host.Instances;

namespace QmlSharp.Host.HotReload
{
    internal sealed record InstanceMatch(InstanceSnapshot Old, ManagedViewModelInstance New);
}
