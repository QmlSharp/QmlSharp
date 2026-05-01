using QmlSharp.Registry;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>
    /// Maps normalized QML type names from the registry into generated C# signatures.
    /// </summary>
    public sealed class TypeMapper : ITypeMapper
    {
        private static readonly StringComparer Comparer = StringComparer.Ordinal;

        private ImmutableSortedDictionary<string, TypeMapping> mappings;

        public TypeMapper()
            : this(customMappings: [])
        {
        }

        public TypeMapper(IEnumerable<TypeMapping> customMappings)
        {
            ArgumentNullException.ThrowIfNull(customMappings);

            ImmutableSortedDictionary<string, TypeMapping>.Builder builder =
                ImmutableSortedDictionary.CreateBuilder<string, TypeMapping>(Comparer);

            foreach (TypeMapping mapping in CreateBuiltInMappings())
            {
                builder[mapping.QmlType] = mapping;
            }

            foreach (TypeMapping mapping in customMappings)
            {
                ValidateMapping(mapping);
                builder[mapping.QmlType] = mapping;
            }

            mappings = builder.ToImmutable();
        }

        public string MapToCSharp(string qmlType)
        {
            string normalizedType = NormalizeTypeName(qmlType);
            if (TryMapListType(normalizedType, out string listType))
            {
                return listType;
            }

            return mappings.TryGetValue(normalizedType, out TypeMapping? mapping)
                ? mapping.CSharpType
                : normalizedType;
        }

        public TypeMapping? GetMapping(string qmlType)
        {
            string normalizedType = NormalizeTypeName(qmlType);
            if (TryGetListElementType(normalizedType, out string? elementType))
            {
                string csharpType = MapListType(elementType);
                return new TypeMapping(
                    normalizedType,
                    csharpType,
                    IsValueType: false,
                    IsNullable: true,
                    DefaultValue: "null",
                    RequiresImport: "System.Collections.Generic");
            }

            return mappings.TryGetValue(normalizedType, out TypeMapping? mapping)
                ? mapping
                : null;
        }

        public string MapListType(string elementType)
        {
            string normalizedElementType = NormalizeTypeName(elementType);
            return $"IReadOnlyList<{MapToCSharp(normalizedElementType)}>";
        }

        public string GetSetterType(QmlProperty property)
        {
            ArgumentNullException.ThrowIfNull(property);

            return property.IsList
                ? MapListType(property.TypeName)
                : MapToCSharp(property.TypeName);
        }

        public string GetParameterType(QmlParameter parameter)
        {
            ArgumentNullException.ThrowIfNull(parameter);

            return MapToCSharp(parameter.TypeName);
        }

        public string GetReturnType(QmlMethod method)
        {
            ArgumentNullException.ThrowIfNull(method);

            if (string.IsNullOrWhiteSpace(method.ReturnType)
                || string.Equals(method.ReturnType, "void", StringComparison.Ordinal))
            {
                return "void";
            }

            return MapToCSharp(method.ReturnType);
        }

        public void RegisterCustomMapping(TypeMapping mapping)
        {
            ValidateMapping(mapping);
            mappings = mappings.SetItem(mapping.QmlType, mapping);
        }

        public IReadOnlyDictionary<string, TypeMapping> GetAllMappings()
        {
            return mappings;
        }

        private static ImmutableArray<TypeMapping> CreateBuiltInMappings()
        {
            return
            [
                Value("int", "int", null),
                Value("real", "double", null),
                Value("double", "double", null),
                Value("bool", "bool", null),
                Reference("string", "string", null),
                Value("color", "QmlColor", "QmlSharp.Core"),
                Reference("url", "string", null),
                Reference("var", "object", null),
                Reference("variant", "object", null),
                Value("date", "DateTime", "System"),
                Value("point", "QmlPoint", "QmlSharp.Core"),
                Value("size", "QmlSize", "QmlSharp.Core"),
                Value("rect", "QmlRect", "QmlSharp.Core"),
                Reference("font", "QmlFont", "QmlSharp.Core"),
                Value("vector2d", "Vector2", "QmlSharp.Core"),
                Value("vector3d", "Vector3", "QmlSharp.Core"),
                Value("vector4d", "Vector4", "QmlSharp.Core"),
                Value("quaternion", "Quaternion", "QmlSharp.Core"),
                Value("matrix4x4", "Matrix4x4", "QmlSharp.Core"),
                Value("enumeration", "int", null),
                Value("void", "void", null),
            ];
        }

        private static TypeMapping Value(string qmlType, string csharpType, string? requiresImport)
        {
            return new TypeMapping(
                qmlType,
                csharpType,
                IsValueType: true,
                IsNullable: true,
                DefaultValue: "default",
                RequiresImport: requiresImport);
        }

        private static TypeMapping Reference(string qmlType, string csharpType, string? requiresImport)
        {
            return new TypeMapping(
                qmlType,
                csharpType,
                IsValueType: false,
                IsNullable: true,
                DefaultValue: "null",
                RequiresImport: requiresImport);
        }

        private static void ValidateMapping(TypeMapping mapping)
        {
            ArgumentNullException.ThrowIfNull(mapping);

            if (string.IsNullOrWhiteSpace(mapping.QmlType))
            {
                throw new ArgumentException("A type mapping must include a QML type name.", nameof(mapping));
            }

            if (string.IsNullOrWhiteSpace(mapping.CSharpType))
            {
                throw new ArgumentException("A type mapping must include a C# type name.", nameof(mapping));
            }
        }

        private static string NormalizeTypeName(string qmlType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(qmlType);

            return qmlType.Trim();
        }

        private bool TryMapListType(string qmlType, out string csharpType)
        {
            if (TryGetListElementType(qmlType, out string? elementType))
            {
                csharpType = MapListType(elementType);
                return true;
            }

            csharpType = string.Empty;
            return false;
        }

        private static bool TryGetListElementType(string qmlType, out string elementType)
        {
            string normalizedType = qmlType.Trim();
            if (normalizedType.StartsWith("list<", StringComparison.Ordinal)
                && normalizedType.EndsWith(">", StringComparison.Ordinal)
                && normalizedType.Length > "list<>".Length)
            {
                elementType = normalizedType["list<".Length..^1].Trim();
                return elementType.Length > 0;
            }

            elementType = string.Empty;
            return false;
        }
    }
}
