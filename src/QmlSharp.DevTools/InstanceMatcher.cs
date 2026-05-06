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

    /// <summary>
    /// Matches old ViewModel snapshots to freshly created instances by V2 identity.
    /// </summary>
    internal sealed class InstanceMatcher : IInstanceMatcher
    {
        /// <inheritdoc />
        public MatchResult Match(
            IReadOnlyList<InstanceSnapshot> oldSnapshots,
            IReadOnlyList<InstanceInfo> newInstances)
        {
            ArgumentNullException.ThrowIfNull(oldSnapshots);
            ArgumentNullException.ThrowIfNull(newInstances);

            Dictionary<InstanceMatchKey, Queue<InstanceInfo>> newInstancesByKey = new();
            foreach (InstanceInfo newInstance in newInstances)
            {
                InstanceMatchKey key = new(newInstance.ClassName, newInstance.CompilerSlotKey);
                if (!newInstancesByKey.TryGetValue(key, out Queue<InstanceInfo>? queue))
                {
                    queue = new Queue<InstanceInfo>();
                    newInstancesByKey.Add(key, queue);
                }

                queue.Enqueue(newInstance);
            }

            List<(InstanceSnapshot Old, InstanceInfo New)> matched = new();
            List<InstanceSnapshot> orphaned = new();
            HashSet<string> matchedNewInstanceIds = new(StringComparer.Ordinal);
            foreach (InstanceSnapshot oldSnapshot in oldSnapshots)
            {
                InstanceMatchKey key = new(oldSnapshot.ClassName, oldSnapshot.CompilerSlotKey);
                if (!newInstancesByKey.TryGetValue(key, out Queue<InstanceInfo>? queue) || queue.Count == 0)
                {
                    orphaned.Add(oldSnapshot);
                    continue;
                }

                InstanceInfo newInstance = queue.Dequeue();
                matched.Add((oldSnapshot, newInstance));
                _ = matchedNewInstanceIds.Add(newInstance.InstanceId);
            }

            InstanceInfo[] unmatched = newInstances
                .Where(newInstance => !matchedNewInstanceIds.Contains(newInstance.InstanceId))
                .ToArray();

            return new MatchResult(matched, orphaned, unmatched);
        }

        private readonly record struct InstanceMatchKey(string ClassName, string CompilerSlotKey);
    }

#pragma warning restore MA0048
}
