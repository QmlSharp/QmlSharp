using QmlSharp.Registry;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>
    /// Generates structured fluent property metadata for generated C# builder surfaces.
    /// </summary>
    public sealed class PropGenerator : IPropGenerator
    {
        public GeneratedProperty Generate(ResolvedProperty property, QmlType ownerType, GenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(ownerType);
            ArgumentNullException.ThrowIfNull(context);
            return Generate(property, context, GetBuilderInterfaceName(ownerType));
        }

        public ImmutableArray<GeneratedProperty> GenerateAll(
            ResolvedType type,
            GenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(context);

            ImmutableArray<ResolvedProperty> properties = type.AllProperties;
            if (properties.IsDefaultOrEmpty)
            {
                return ImmutableArray<GeneratedProperty>.Empty;
            }

            return properties
                .OrderBy(property => property.Property.Name, StringComparer.Ordinal)
                .Select(property => Generate(property, type.Type, context))
                .ToImmutableArray();
        }

        public ImmutableArray<GroupedPropertyInfo> DetectGroupedProperties(
            ResolvedType type)
        {
            ArgumentNullException.ThrowIfNull(type);

            ImmutableArray<ResolvedProperty> properties = type.AllProperties;
            if (properties.IsDefaultOrEmpty)
            {
                return ImmutableArray<GroupedPropertyInfo>.Empty;
            }

            ThrowIfGroupedPropertyConflicts(properties);
            string ownerBuilderInterfaceName = GetBuilderInterfaceName(type.Type);

            return properties
                .Where(property => TrySplitGroupedPropertyName(property.Property.Name, out _, out _))
                .GroupBy(property => GetGroupName(property.Property.Name), StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group =>
                {
                    ImmutableArray<ResolvedProperty> subProperties = group
                        .OrderBy(property => property.Property.Name, StringComparer.Ordinal)
                        .ToImmutableArray();
                    string groupName = ToPascalCase(group.Key);
                    return new GroupedPropertyInfo(
                        GroupName: groupName,
                        SubProperties: subProperties,
                        BuilderSignature: $"{ownerBuilderInterfaceName} {groupName}(Action<I{groupName}Builder> setup)");
                })
                .ToImmutableArray();
        }

        private static GeneratedProperty Generate(
            ResolvedProperty property,
            GenerationContext context,
            string builderInterfaceName)
        {
            ArgumentNullException.ThrowIfNull(property);

            if (string.IsNullOrWhiteSpace(property.Property.TypeName)
                || string.Equals(context.TypeMapper.GetSetterType(property.Property), "void", StringComparison.Ordinal))
            {
                throw new UnsupportedPropertyTypeException(property.Property.Name, property.Property.TypeName, property.DeclaredBy);
            }

            string name = context.NameRegistry.RegisterPropertyName(property.Property.Name, builderInterfaceName);
            string csharpType = context.TypeMapper.GetSetterType(property.Property);
            string setterSignature = property.Property.IsReadonly
                ? string.Empty
                : $"{builderInterfaceName} {name}({csharpType} value)";
            string? bindSignature = property.Property.IsReadonly || !context.Options.Properties.GenerateBindMethods
                ? null
                : $"{builderInterfaceName} {name}Bind(string expr)";

            return new GeneratedProperty(
                Name: name,
                SetterSignature: setterSignature,
                BindSignature: bindSignature,
                XmlDoc: $"<summary>Sets the QML {property.Property.Name} property.</summary>",
                DeclaredBy: property.DeclaredBy,
                IsReadOnly: property.Property.IsReadonly,
                IsRequired: property.Property.IsRequired,
                CSharpType: csharpType);
        }

        private static string GetBuilderInterfaceName(QmlType type)
        {
            return $"I{ToPascalCase(type.QmlName ?? type.QualifiedName)}Builder";
        }

        private static string GetGroupName(string propertyName)
        {
            return propertyName[..propertyName.IndexOf('.', StringComparison.Ordinal)];
        }

        private static void ThrowIfGroupedPropertyConflicts(ImmutableArray<ResolvedProperty> properties)
        {
            HashSet<string> directPropertyNames = properties
                .Where(property => !property.Property.Name.Contains('.', StringComparison.Ordinal))
                .Select(property => property.Property.Name)
                .ToHashSet(StringComparer.Ordinal);

            string[] conflictingGroups = properties
                .Where(property => TrySplitGroupedPropertyName(property.Property.Name, out string? groupName, out _)
                    && directPropertyNames.Contains(groupName))
                .Select(property => GetGroupName(property.Property.Name))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            if (conflictingGroups.Length > 0)
            {
                throw new GroupedPropertyConflictException(conflictingGroups[0]);
            }
        }

        private static bool TrySplitGroupedPropertyName(string propertyName, out string groupName, out string subPropertyName)
        {
            int separatorIndex = propertyName.IndexOf('.', StringComparison.Ordinal);
            if (separatorIndex <= 0 || separatorIndex == propertyName.Length - 1)
            {
                groupName = string.Empty;
                subPropertyName = string.Empty;
                return false;
            }

            groupName = propertyName[..separatorIndex];
            subPropertyName = propertyName[(separatorIndex + 1)..];
            return true;
        }

        private static string ToPascalCase(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            string[] parts = name
                .Split(['.', '-', '_', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 0)
            {
                return "_";
            }

            return string.Concat(parts.Select(Capitalize));
        }

        private static string Capitalize(string value)
        {
            if (value.Length == 0)
            {
                return value;
            }

            return string.Concat(char.ToUpperInvariant(value[0]).ToString(), value[1..]);
        }
    }
}
