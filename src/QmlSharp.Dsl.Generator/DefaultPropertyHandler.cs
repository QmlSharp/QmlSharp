using QmlSharp.Registry;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>
    /// Analyzes QML default properties and generates child method metadata.
    /// </summary>
    public sealed class DefaultPropertyHandler : IDefaultPropertyHandler
    {
        public DefaultPropertyInfo? Analyze(ResolvedType type)
        {
            ArgumentNullException.ThrowIfNull(type);

            QmlType? declaringType = type.InheritanceChain.FirstOrDefault(qmlType =>
                !string.IsNullOrWhiteSpace(qmlType.DefaultProperty));
            if (declaringType?.DefaultProperty is null)
            {
                return null;
            }

            ResolvedProperty? defaultProperty = type.AllProperties.FirstOrDefault(property =>
                string.Equals(property.Property.Name, declaringType.DefaultProperty, StringComparison.Ordinal));

            bool isList = defaultProperty?.Property.IsList
                ?? IsKnownVisualChildrenDefaultProperty(declaringType.DefaultProperty);
            string elementType = defaultProperty is null
                ? InferElementTypeFromDefaultPropertyName(declaringType.DefaultProperty)
                : GetElementType(defaultProperty.Property);

            return new DefaultPropertyInfo(
                PropertyName: declaringType.DefaultProperty,
                ElementType: elementType,
                IsList: isList,
                GenerateChildMethod: true,
                GenerateChildrenMethod: isList);
        }

        public ImmutableArray<string> GenerateMethods(DefaultPropertyInfo info, GenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(info);
            ArgumentNullException.ThrowIfNull(context);

            ImmutableArray<string>.Builder methods = ImmutableArray.CreateBuilder<string>();
            if (info.GenerateChildMethod)
            {
                methods.Add("Child(IObjectBuilder obj)");
            }

            if (info.GenerateChildrenMethod)
            {
                methods.Add("Children(params IObjectBuilder[] objs)");
            }

            return methods.ToImmutable();
        }

        private static string GetElementType(QmlProperty property)
        {
            if (TryGetListElementType(property.TypeName, out string elementType))
            {
                return elementType;
            }

            return property.TypeName;
        }

        private static bool TryGetListElementType(string typeName, out string elementType)
        {
            string normalizedTypeName = typeName.Trim();
            if (normalizedTypeName.StartsWith("list<", StringComparison.Ordinal)
                && normalizedTypeName.EndsWith(">", StringComparison.Ordinal)
                && normalizedTypeName.Length > "list<>".Length)
            {
                elementType = normalizedTypeName["list<".Length..^1].Trim();
                return elementType.Length > 0;
            }

            elementType = string.Empty;
            return false;
        }

        private static bool IsKnownVisualChildrenDefaultProperty(string propertyName)
        {
            return string.Equals(propertyName, "data", StringComparison.Ordinal)
                || string.Equals(propertyName, "children", StringComparison.Ordinal);
        }

        private static string InferElementTypeFromDefaultPropertyName(string propertyName)
        {
            return IsKnownVisualChildrenDefaultProperty(propertyName) ? "Item" : "object";
        }
    }
}
