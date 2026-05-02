using System.Text;

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Allocates deterministic schema IDs and compiler slot keys.
    /// </summary>
    public sealed class IdAllocator : IIdAllocator
    {
        private const int HashMask = 0x7fffffff;
        private readonly Dictionary<string, int> allocatedIds = new(StringComparer.Ordinal);
        private readonly Dictionary<int, string> usedIds = new();

        /// <inheritdoc />
        public int AllocateMemberId(string className, string memberName)
        {
            return AllocateHashId($"mem:{CreateMemberKey(className, memberName)}");
        }

        /// <inheritdoc />
        public int AllocateCommandId(string className, string commandName)
        {
            return AllocateHashId($"cmd:{CreateMemberKey(className, commandName)}");
        }

        /// <inheritdoc />
        public int AllocateEffectId(string className, string effectName)
        {
            return AllocateHashId($"eff:{CreateMemberKey(className, effectName)}");
        }

        /// <inheritdoc />
        public string GenerateSlotKey(string viewClassName, int slotIndex)
        {
            if (string.IsNullOrWhiteSpace(viewClassName))
            {
                throw new ArgumentException("View class name is required.", nameof(viewClassName));
            }

            ArgumentOutOfRangeException.ThrowIfNegative(slotIndex);

            return $"{viewClassName}::__qmlsharp_vm{slotIndex}";
        }

        /// <inheritdoc />
        public int ComputeHash(string key)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            uint hash = 0x811c9dc5;
            byte[] bytes = Encoding.UTF8.GetBytes(key);
            foreach (byte value in bytes)
            {
                hash ^= value;
                hash *= 0x01000193;
            }

            return unchecked((int)(hash & HashMask));
        }

        private int AllocateHashId(string key)
        {
            if (allocatedIds.TryGetValue(key, out int existingId))
            {
                return existingId;
            }

            int candidate = ComputeHash(key);
            if (candidate == 0)
            {
                candidate = 1;
            }

            while (usedIds.TryGetValue(candidate, out string? existingKey)
                && !StringComparer.Ordinal.Equals(existingKey, key))
            {
                candidate = (candidate + 1) & HashMask;
                if (candidate == 0)
                {
                    candidate = 1;
                }
            }

            allocatedIds.Add(key, candidate);
            usedIds[candidate] = key;
            return candidate;
        }

        private static string CreateMemberKey(string className, string memberName)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                throw new ArgumentException("Class name is required.", nameof(className));
            }

            if (string.IsNullOrWhiteSpace(memberName))
            {
                throw new ArgumentException("Member name is required.", nameof(memberName));
            }

            return $"{className}.{memberName}";
        }
    }
}
