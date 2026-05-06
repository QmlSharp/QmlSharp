#pragma warning disable MA0048

using QmlSharp.Host.Instances;

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Contract for matching old ViewModel snapshots to new instances by class name and compiler slot key.
    /// </summary>
    internal interface IInstanceMatcher
    {
        /// <summary>Matches old snapshots to new instances.</summary>
        MatchResult Match(
            IReadOnlyList<InstanceSnapshot> oldSnapshots,
            IReadOnlyList<InstanceInfo> newInstances);
    }

    /// <summary>Result of instance matching.</summary>
    /// <param name="Matched">Matched old snapshot and new instance pairs.</param>
    /// <param name="Orphaned">Old instances with no match.</param>
    /// <param name="Unmatched">New instances with no match.</param>
    internal sealed record MatchResult(
        IReadOnlyList<(InstanceSnapshot Old, InstanceInfo New)> Matched,
        IReadOnlyList<InstanceSnapshot> Orphaned,
        IReadOnlyList<InstanceInfo> Unmatched);

#pragma warning restore MA0048
}
