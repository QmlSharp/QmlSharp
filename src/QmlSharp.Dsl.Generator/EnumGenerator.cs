using System.Text;
using QmlSharp.Registry;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>
    /// Generates structured C# enum metadata from normalized QML enum definitions.
    /// </summary>
    public sealed class EnumGenerator : IEnumGenerator
    {
        public GeneratedEnum Generate(QmlEnum enumDef, QmlType ownerType, GenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(enumDef);
            ArgumentNullException.ThrowIfNull(ownerType);
            ArgumentNullException.ThrowIfNull(context);

            string enumName = GetGeneratedEnumName(enumDef, ownerType, context);
            ImmutableArray<GeneratedEnumMember> members = GenerateMembers(enumDef, ownerType, context);
            bool isFlag = enumDef.IsFlag;

            return new GeneratedEnum(
                Name: enumName,
                Alias: enumDef.Alias,
                IsFlag: isFlag,
                IsScoped: enumDef.IsScoped,
                Members: members,
                Code: BuildCode(enumName, isFlag, members),
                OwnerType: ownerType);
        }

        public ImmutableArray<GeneratedEnum> GenerateAll(
            ImmutableArray<QmlEnum> enums,
            QmlType ownerType,
            GenerationContext context)
        {
            ArgumentNullException.ThrowIfNull(ownerType);
            ArgumentNullException.ThrowIfNull(context);

            if (enums.IsDefaultOrEmpty)
            {
                return ImmutableArray<GeneratedEnum>.Empty;
            }

            ImmutableArray<GeneratedEnum>.Builder generatedEnums = ImmutableArray.CreateBuilder<GeneratedEnum>();
            HashSet<string> generatedNames = new(StringComparer.Ordinal);
            foreach (QmlEnum enumDef in enums.OrderBy(enumDef => enumDef.Name, StringComparer.Ordinal))
            {
                string canonicalName = GetCanonicalEnumName(enumDef, ownerType);
                if (!generatedNames.Add(canonicalName))
                {
                    throw new EnumNameCollisionException(canonicalName, ownerType);
                }

                GeneratedEnum generated = Generate(enumDef, ownerType, context);
                generatedEnums.Add(generated);
            }

            return generatedEnums.ToImmutable();
        }

        private static string GetGeneratedEnumName(QmlEnum enumDef, QmlType ownerType, GenerationContext context)
        {
            string ownerName = MemberNameUtilities.ToPascalCase(ownerType.QmlName ?? ownerType.QualifiedName);
            string prefixedName = GetCanonicalEnumName(enumDef, ownerType);
            return context.NameRegistry.RegisterEnumName(prefixedName, ownerName);
        }

        private static string GetCanonicalEnumName(QmlEnum enumDef, QmlType ownerType)
        {
            string ownerName = MemberNameUtilities.ToPascalCase(ownerType.QmlName ?? ownerType.QualifiedName);
            string rawEnumName = enumDef.Alias ?? enumDef.Name;
            string localEnumName = rawEnumName.Contains('.', StringComparison.Ordinal)
                ? rawEnumName[(rawEnumName.LastIndexOf(".", StringComparison.Ordinal) + 1)..]
                : rawEnumName;
            string candidateName = MemberNameUtilities.ToPascalCase(localEnumName);
            return candidateName.StartsWith(ownerName, StringComparison.Ordinal)
                ? candidateName
                : $"{ownerName}{candidateName}";
        }

        private static ImmutableArray<GeneratedEnumMember> GenerateMembers(QmlEnum enumDef, QmlType ownerType, GenerationContext context)
        {
            ImmutableArray<GeneratedEnumMember>.Builder members = ImmutableArray.CreateBuilder<GeneratedEnumMember>();
            HashSet<string> memberNames = new(StringComparer.Ordinal);

            foreach (QmlEnumValue value in enumDef.Values
                         .OrderBy(value => value.Value ?? int.MaxValue)
                         .ThenBy(value => value.Name, StringComparer.Ordinal))
            {
                string memberName = context.NameRegistry.ToSafeIdentifier(MemberNameUtilities.ToPascalCase(value.Name));
                if (!memberNames.Add(memberName))
                {
                    throw new DuplicateEnumMemberException(enumDef.Name, memberName, ownerType);
                }

                members.Add(new GeneratedEnumMember(memberName, value.Value));
            }

            return members.ToImmutable();
        }

        private static string BuildCode(string enumName, bool isFlag, ImmutableArray<GeneratedEnumMember> members)
        {
            StringBuilder builder = new();
            if (isFlag)
            {
                builder.AppendLine("[Flags]");
            }

            builder.AppendLine($"public enum {enumName}");
            builder.AppendLine("{");
            for (int index = 0; index < members.Length; index++)
            {
                GeneratedEnumMember member = members[index];
                string valueSuffix = member.Value.HasValue ? $" = {member.Value.Value}" : string.Empty;
                string comma = index == members.Length - 1 ? string.Empty : ",";
                builder.AppendLine($"    {member.Name}{valueSuffix}{comma}");
            }

            builder.Append('}');
            return builder.ToString();
        }
    }
}
