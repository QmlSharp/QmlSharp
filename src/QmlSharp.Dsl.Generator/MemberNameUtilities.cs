using System.Text;
using QmlSharp.Registry;

namespace QmlSharp.Dsl.Generator
{
    internal static class MemberNameUtilities
    {
        public static string GetBuilderInterfaceName(QmlType type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return $"I{ToPascalCase(type.QmlName ?? type.QualifiedName)}Builder";
        }

        public static string ToPascalCase(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            string trimmedName = name.Trim();
            StringBuilder builder = new(trimmedName.Length);
            bool capitalizeNext = true;
            for (int index = 0; index < trimmedName.Length; index++)
            {
                char character = trimmedName[index];
                if (!char.IsLetterOrDigit(character) && character != '_')
                {
                    capitalizeNext = true;
                    continue;
                }

                if (builder.Length == 0 && !char.IsLetter(character) && character != '_')
                {
                    builder.Append('_');
                }

                builder.Append(capitalizeNext ? char.ToUpperInvariant(character) : character);
                capitalizeNext = false;
            }

            return builder.Length == 0 ? "_" : builder.ToString();
        }

        public static string ToSafeParameterName(string name, INameRegistry nameRegistry)
        {
            ArgumentNullException.ThrowIfNull(nameRegistry);
            string fallbackName = string.IsNullOrWhiteSpace(name) ? "value" : name;
            string safeName = nameRegistry.ToSafeIdentifier(fallbackName);
            if (safeName.Length == 0 || string.Equals(safeName, "_", StringComparison.Ordinal))
            {
                return "value";
            }

            return char.IsUpper(safeName[0])
                ? string.Concat(char.ToLowerInvariant(safeName[0]).ToString(), safeName[1..])
                : safeName;
        }
    }
}
