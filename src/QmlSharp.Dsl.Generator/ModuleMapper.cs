using System.Collections.ObjectModel;
using System.Text;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>
    /// Maps Qt module URIs to deterministic QmlSharp NuGet package names and namespaces.
    /// </summary>
    public sealed class ModuleMapper : IModuleMapper
    {
        private const string DefaultPackagePrefix = "QmlSharp";

        private static readonly IReadOnlyDictionary<string, string> BuiltInMappings =
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["QtQml"] = "QmlSharp.QtQml",
                    ["QtQuick"] = "QmlSharp.QtQuick",
                    ["QtQuick.Controls"] = "QmlSharp.QtQuick.Controls",
                    ["QtQuick.Layouts"] = "QmlSharp.QtQuick.Layouts",
                    ["QtQuick.Window"] = "QmlSharp.QtQuick.Window",
                    ["QtQuick.Dialogs"] = "QmlSharp.QtQuick.Dialogs",
                });

        private static readonly IReadOnlyDictionary<string, int> BuiltInPriorities =
            new ReadOnlyDictionary<string, int>(
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["QtQml"] = 0,
                    ["QtQuick"] = 0,
                    ["QtQuick.Controls"] = 0,
                    ["QtQuick.Layouts"] = 0,
                    ["QtQuick.Window"] = 1,
                    ["QtQuick.Dialogs"] = 1,
                });

        private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
        {
            "abstract",
            "as",
            "base",
            "bool",
            "break",
            "byte",
            "case",
            "catch",
            "char",
            "checked",
            "class",
            "const",
            "continue",
            "decimal",
            "default",
            "delegate",
            "do",
            "double",
            "else",
            "enum",
            "event",
            "explicit",
            "extern",
            "false",
            "finally",
            "fixed",
            "float",
            "for",
            "foreach",
            "goto",
            "if",
            "implicit",
            "in",
            "int",
            "interface",
            "internal",
            "is",
            "lock",
            "long",
            "namespace",
            "new",
            "null",
            "object",
            "operator",
            "out",
            "override",
            "params",
            "private",
            "protected",
            "public",
            "readonly",
            "ref",
            "return",
            "sbyte",
            "sealed",
            "short",
            "sizeof",
            "stackalloc",
            "static",
            "string",
            "struct",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "uint",
            "ulong",
            "unchecked",
            "unsafe",
            "ushort",
            "using",
            "virtual",
            "void",
            "volatile",
            "while",
        };

        private readonly string packagePrefix;
        private readonly IReadOnlyDictionary<string, string> mappings;
        private readonly IReadOnlyDictionary<string, int> priorities;

        public ModuleMapper(
            string? packagePrefix = null,
            IReadOnlyDictionary<string, string>? customMappings = null,
            IReadOnlyDictionary<string, int>? customPriorities = null)
        {
            this.packagePrefix = string.IsNullOrWhiteSpace(packagePrefix)
                ? DefaultPackagePrefix
                : TrimPackagePrefix(packagePrefix);

            Dictionary<string, string> mergedMappings = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> mapping in BuiltInMappings.OrderBy(static mapping => mapping.Key, StringComparer.Ordinal))
            {
                mergedMappings[mapping.Key] = ApplyPrefix(mapping.Value, this.packagePrefix);
            }

            if (customMappings is not null)
            {
                foreach (KeyValuePair<string, string> mapping in customMappings.OrderBy(static mapping => mapping.Key, StringComparer.Ordinal))
                {
                    ThrowIfBlank(mapping.Key, nameof(customMappings));
                    ThrowIfBlank(mapping.Value, nameof(customMappings));
                    mergedMappings[mapping.Key] = mapping.Value;
                }
            }

            mappings = new ReadOnlyDictionary<string, string>(mergedMappings);

            Dictionary<string, int> mergedPriorities = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> priority in BuiltInPriorities.OrderBy(static priority => priority.Key, StringComparer.Ordinal))
            {
                mergedPriorities[priority.Key] = priority.Value;
            }

            if (customPriorities is not null)
            {
                foreach (KeyValuePair<string, int> priority in customPriorities.OrderBy(static priority => priority.Key, StringComparer.Ordinal))
                {
                    ThrowIfBlank(priority.Key, nameof(customPriorities));
                    mergedPriorities[priority.Key] = priority.Value;
                }
            }

            priorities = new ReadOnlyDictionary<string, int>(mergedPriorities);
        }

        public string ToPackageName(string moduleUri)
        {
            ThrowIfBlank(moduleUri, nameof(moduleUri));

            if (mappings.TryGetValue(moduleUri, out string? mappedPackageName))
            {
                return mappedPackageName;
            }

            return $"{packagePrefix}.{ToDottedIdentifier(moduleUri)}";
        }

        public string ToModuleUri(string packageName)
        {
            ThrowIfBlank(packageName, nameof(packageName));

            KeyValuePair<string, string> mapped = mappings
                .OrderBy(static mapping => mapping.Key, StringComparer.Ordinal)
                .FirstOrDefault(mapping => string.Equals(mapping.Value, packageName, StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(mapped.Key))
            {
                return mapped.Key;
            }

            string prefix = $"{packagePrefix}.";
            if (packageName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return packageName[prefix.Length..];
            }

            return packageName;
        }

        public string ToNamespace(string moduleUri)
        {
            return ToDottedIdentifier(ToPackageName(moduleUri));
        }

        public int GetPriority(string moduleUri)
        {
            ThrowIfBlank(moduleUri, nameof(moduleUri));
            return priorities.GetValueOrDefault(moduleUri, 2);
        }

        public IReadOnlyDictionary<string, string> GetAllMappings()
        {
            return new ReadOnlyDictionary<string, string>(
                mappings
                    .OrderBy(static mapping => mapping.Key, StringComparer.Ordinal)
                    .ToDictionary(static mapping => mapping.Key, static mapping => mapping.Value, StringComparer.Ordinal));
        }

        private static string ApplyPrefix(string packageName, string prefix)
        {
            const string defaultPrefixWithSeparator = DefaultPackagePrefix + ".";
            if (string.Equals(prefix, DefaultPackagePrefix, StringComparison.Ordinal)
                || !packageName.StartsWith(defaultPrefixWithSeparator, StringComparison.Ordinal))
            {
                return packageName;
            }

            return $"{prefix}.{packageName[defaultPrefixWithSeparator.Length..]}";
        }

        private static string TrimPackagePrefix(string value)
        {
            return value.Trim().Trim('.');
        }

        private static string ToDottedIdentifier(string value)
        {
            string[] segments = value
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length == 0)
            {
                return "_";
            }

            return string.Join(".", segments.Select(ToIdentifierSegment));
        }

        private static string ToIdentifierSegment(string segment)
        {
            StringBuilder builder = new();
            bool previousWasSeparator = true;

            foreach (char character in segment)
            {
                if (char.IsLetterOrDigit(character) || character == '_')
                {
                    builder.Append(previousWasSeparator ? char.ToUpperInvariant(character) : character);
                    previousWasSeparator = false;
                    continue;
                }

                previousWasSeparator = true;
            }

            string identifier = builder.Length == 0 ? "_" : builder.ToString();
            if (char.IsDigit(identifier[0]))
            {
                identifier = $"_{identifier}";
            }

            if (CSharpKeywords.Contains(identifier))
            {
                identifier = $"_{identifier}";
            }

            return identifier;
        }

        private static void ThrowIfBlank(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value must not be blank.", parameterName);
            }
        }
    }
}
