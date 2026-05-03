namespace QmlSharp.Host.StateSynchronization
{
    /// <summary>Schema-derived metadata for one ViewModel's synchronizable state properties.</summary>
    public sealed class StateSyncSchemaMetadata
    {
        private readonly Dictionary<string, StateSyncPropertyMetadata> propertiesByName;

        public StateSyncSchemaMetadata(string schemaId, IEnumerable<StateSyncPropertyMetadata> properties)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
            ArgumentNullException.ThrowIfNull(properties);

            SchemaId = schemaId;
            propertiesByName = new Dictionary<string, StateSyncPropertyMetadata>(StringComparer.Ordinal);
            foreach (StateSyncPropertyMetadata property in properties)
            {
                if (string.IsNullOrWhiteSpace(property.Name))
                {
                    throw new ArgumentException("State property names must be non-empty.", nameof(properties));
                }

                propertiesByName.Add(property.Name, property);
            }
        }

        /// <summary>The schema identifier associated with the metadata.</summary>
        public string SchemaId { get; }

        /// <summary>State properties keyed by their exact schema-defined names.</summary>
        public IReadOnlyDictionary<string, StateSyncPropertyMetadata> PropertiesByName => propertiesByName;

        internal bool TryFindProperty(string propertyName, out StateSyncPropertyMetadata property)
        {
            return propertiesByName.TryGetValue(propertyName, out property!);
        }
    }
}
