using System.Collections.Frozen;

namespace QmlSharp.Registry.Normalization
{
    /// <summary>
    /// Maps between C++ type names and their canonical QML equivalents.
    /// </summary>
    public sealed class TypeNameMapper : ITypeNameMapper
    {
        private static readonly ImmutableArray<KeyValuePair<string, string>> BuiltInMappings =
        [
            new KeyValuePair<string, string>("bool", "bool"),
            new KeyValuePair<string, string>("int", "int"),
            new KeyValuePair<string, string>("double", "double"),
            new KeyValuePair<string, string>("float", "double"),
            new KeyValuePair<string, string>("void", "void"),
            new KeyValuePair<string, string>("QString", "string"),
            new KeyValuePair<string, string>("QStringList", "list<string>"),
            new KeyValuePair<string, string>("QColor", "color"),
            new KeyValuePair<string, string>("QFont", "font"),
            new KeyValuePair<string, string>("QUrl", "url"),
            new KeyValuePair<string, string>("QDateTime", "date"),
            new KeyValuePair<string, string>("QDate", "date"),
            new KeyValuePair<string, string>("QPointF", "point"),
            new KeyValuePair<string, string>("QPoint", "point"),
            new KeyValuePair<string, string>("QSizeF", "size"),
            new KeyValuePair<string, string>("QSize", "size"),
            new KeyValuePair<string, string>("QRectF", "rect"),
            new KeyValuePair<string, string>("QRect", "rect"),
            new KeyValuePair<string, string>("QVariant", "var"),
            new KeyValuePair<string, string>("QVariantMap", "var"),
            new KeyValuePair<string, string>("QJSValue", "var"),
            new KeyValuePair<string, string>("QVariantList", "list"),
            new KeyValuePair<string, string>("QList<QVariant>", "list"),
            new KeyValuePair<string, string>("qreal", "double"),
            new KeyValuePair<string, string>("QMatrix4x4", "matrix4x4"),
            new KeyValuePair<string, string>("QQuaternion", "quaternion"),
            new KeyValuePair<string, string>("QVector2D", "vector2d"),
            new KeyValuePair<string, string>("QVector3D", "vector3d"),
            new KeyValuePair<string, string>("QVector4D", "vector4d"),
        ];

        private readonly FrozenDictionary<string, string> cppToQml;
        private readonly FrozenDictionary<string, string> customMappings;
        private readonly FrozenDictionary<string, string> qmlToCpp;

        public TypeNameMapper()
            : this(new Dictionary<string, string>(StringComparer.Ordinal))
        {
        }

        private TypeNameMapper(IReadOnlyDictionary<string, string> customMappings)
        {
            this.customMappings = CreateValidatedCustomMappings(customMappings);
            cppToQml = CreateForwardMappings(this.customMappings);
            qmlToCpp = CreateReverseMappings(this.customMappings);
        }

        public IReadOnlyDictionary<string, string> GetAllMappings()
        {
            return cppToQml;
        }

        public bool HasMapping(string cppTypeName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cppTypeName);

            return cppToQml.ContainsKey(cppTypeName);
        }

        public string ToCppName(string qmlTypeName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(qmlTypeName);

            return qmlToCpp.GetValueOrDefault(qmlTypeName) ?? qmlTypeName;
        }

        public string ToQmlName(string cppTypeName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cppTypeName);

            return cppToQml.GetValueOrDefault(cppTypeName) ?? cppTypeName;
        }

        public ITypeNameMapper WithCustomMappings(IReadOnlyDictionary<string, string> customMappings)
        {
            ArgumentNullException.ThrowIfNull(customMappings);

            Dictionary<string, string> mergedCustomMappings = new(customMappings.Count + this.customMappings.Count, StringComparer.Ordinal);

            foreach ((string cppTypeName, string qmlTypeName) in this.customMappings)
            {
                mergedCustomMappings[cppTypeName] = qmlTypeName;
            }

            foreach ((string cppTypeName, string qmlTypeName) in EnumerateValidatedMappings(customMappings))
            {
                mergedCustomMappings[cppTypeName] = qmlTypeName;
            }

            return new TypeNameMapper(mergedCustomMappings);
        }

        private static FrozenDictionary<string, string> CreateForwardMappings(IReadOnlyDictionary<string, string> customMappings)
        {
            Dictionary<string, string> mappings = new(BuiltInMappings.Length + customMappings.Count, StringComparer.Ordinal);

            foreach ((string cppTypeName, string qmlTypeName) in BuiltInMappings)
            {
                mappings[cppTypeName] = qmlTypeName;
            }

            foreach ((string cppTypeName, string qmlTypeName) in customMappings)
            {
                mappings[cppTypeName] = qmlTypeName;
            }

            return mappings.ToFrozenDictionary(StringComparer.Ordinal);
        }

        private static FrozenDictionary<string, string> CreateReverseMappings(IReadOnlyDictionary<string, string> customMappings)
        {
            Dictionary<string, string> mappings = new(StringComparer.Ordinal);
            HashSet<string> overriddenBuiltInCppTypes = customMappings.Keys.ToHashSet(StringComparer.Ordinal);

            foreach ((string cppTypeName, string qmlTypeName) in EnumerateDeterministicMappings(customMappings))
            {
                _ = mappings.TryAdd(qmlTypeName, cppTypeName);
            }

            foreach ((string cppTypeName, string qmlTypeName) in BuiltInMappings)
            {
                if (overriddenBuiltInCppTypes.Contains(cppTypeName))
                {
                    continue;
                }

                _ = mappings.TryAdd(qmlTypeName, cppTypeName);
            }

            return mappings.ToFrozenDictionary(StringComparer.Ordinal);
        }

        private static FrozenDictionary<string, string> CreateValidatedCustomMappings(IReadOnlyDictionary<string, string> customMappings)
        {
            return EnumerateValidatedMappings(customMappings)
                .ToDictionary(mapping => mapping.Key, mapping => mapping.Value, StringComparer.Ordinal)
                .ToFrozenDictionary(StringComparer.Ordinal);
        }

        private static IEnumerable<KeyValuePair<string, string>> EnumerateDeterministicMappings(IReadOnlyDictionary<string, string> customMappings)
        {
            return customMappings
                .OrderBy(mapping => mapping.Key, StringComparer.Ordinal)
                .Select(mapping => new KeyValuePair<string, string>(mapping.Key, mapping.Value));
        }

        private static IEnumerable<KeyValuePair<string, string>> EnumerateValidatedMappings(IReadOnlyDictionary<string, string> mappings)
        {
            foreach ((string cppTypeName, string qmlTypeName) in EnumerateDeterministicMappings(mappings))
            {
                if (string.IsNullOrWhiteSpace(cppTypeName))
                {
                    throw new ArgumentException("Custom mapping keys must not be null, empty, or whitespace.", nameof(mappings));
                }

                if (string.IsNullOrWhiteSpace(qmlTypeName))
                {
                    throw new ArgumentException("Custom mapping values must not be null, empty, or whitespace.", nameof(mappings));
                }

                yield return new KeyValuePair<string, string>(cppTypeName, qmlTypeName);
            }
        }
    }
}
