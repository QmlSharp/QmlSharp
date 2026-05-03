using QmlSharp.Host.Instances;

namespace QmlSharp.Host.HotReload
{
    internal static class InstanceMatcher
    {
        internal static InstanceMatchResult Match(
            IReadOnlyList<InstanceSnapshot> oldSnapshots,
            IReadOnlyList<ManagedViewModelInstance> newInstances)
        {
            ArgumentNullException.ThrowIfNull(oldSnapshots);
            ArgumentNullException.ThrowIfNull(newInstances);

            Dictionary<InstanceMatchKey, Queue<ManagedViewModelInstance>> newInstancesByKey = [];
            foreach (ManagedViewModelInstance newInstance in newInstances)
            {
                InstanceMatchKey key = new(newInstance.ClassName, newInstance.CompilerSlotKey);
                if (!newInstancesByKey.TryGetValue(key, out Queue<ManagedViewModelInstance>? queue))
                {
                    queue = new Queue<ManagedViewModelInstance>();
                    newInstancesByKey.Add(key, queue);
                }

                queue.Enqueue(newInstance);
            }

            List<InstanceMatch> matched = [];
            List<InstanceSnapshot> orphaned = [];
            HashSet<string> matchedNewInstanceIds = new(StringComparer.Ordinal);
            foreach (InstanceSnapshot oldSnapshot in oldSnapshots)
            {
                InstanceMatchKey key = new(oldSnapshot.ClassName, oldSnapshot.CompilerSlotKey);
                if (!newInstancesByKey.TryGetValue(key, out Queue<ManagedViewModelInstance>? queue) || queue.Count == 0)
                {
                    orphaned.Add(oldSnapshot);
                    continue;
                }

                ManagedViewModelInstance newInstance = queue.Dequeue();
                matched.Add(new InstanceMatch(oldSnapshot, newInstance));
                _ = matchedNewInstanceIds.Add(newInstance.InstanceId);
            }

            ManagedViewModelInstance[] unmatched = newInstances
                .Where(newInstance => !matchedNewInstanceIds.Contains(newInstance.InstanceId))
                .ToArray();

            return new InstanceMatchResult(matched, orphaned, unmatched);
        }

        private readonly record struct InstanceMatchKey(string ClassName, string CompilerSlotKey);
    }

}
