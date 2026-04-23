#pragma warning disable MA0048

namespace QmlSharp.Registry.Snapshots
{
    /// <summary>
    /// Serializes and deserializes QmlRegistry for caching.
    /// The snapshot format includes a format version and Qt version
    /// for cache validity checking.
    /// </summary>
    public interface IRegistrySnapshot
    {
        byte[] Serialize(QmlRegistry registry);

        QmlRegistry Deserialize(byte[] data);

        void SaveToFile(QmlRegistry registry, string filePath);

        QmlRegistry LoadFromFile(string filePath);

        SnapshotValidity CheckValidity(string filePath);
    }
}

#pragma warning restore MA0048
