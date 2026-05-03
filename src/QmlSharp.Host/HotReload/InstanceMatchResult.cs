using QmlSharp.Host.Instances;

namespace QmlSharp.Host.HotReload
{
    internal sealed record InstanceMatchResult(
        IReadOnlyList<InstanceMatch> Matched,
        IReadOnlyList<InstanceSnapshot> Orphaned,
        IReadOnlyList<ManagedViewModelInstance> Unmatched);
}
