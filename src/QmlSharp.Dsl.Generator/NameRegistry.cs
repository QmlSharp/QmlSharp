using System.Collections.Frozen;
using System.Text;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>
    /// Converts registry names to safe generated C# identifiers and resolves collisions.
    /// </summary>
    public sealed class NameRegistry : INameRegistry
    {
        private static readonly StringComparer Comparer = StringComparer.Ordinal;

        private static readonly FrozenSet<string> ReservedWords = CreateReservedWords();

        private static readonly FrozenSet<string> BuiltInTypeNames = new[]
        {
            "Action",
            "Boolean",
            "Byte",
            "Char",
            "DateTime",
            "Decimal",
            "Dictionary",
            "Double",
            "Enum",
            "Func",
            "IEnumerable",
            "IReadOnlyList",
            "Int16",
            "Int32",
            "Int64",
            "IntPtr",
            "List",
            "Object",
            "SByte",
            "Single",
            "String",
            "Task",
            "Type",
            "UInt16",
            "UInt32",
            "UInt64",
            "UIntPtr",
            "ValueType",
            "Void",
        }.ToFrozenSet(Comparer);

        private readonly Dictionary<(string ModuleUri, string QmlName), string> typeNamesByModuleAndQmlName = new(EqualityComparer<(string ModuleUri, string QmlName)>.Default);
        private readonly Dictionary<string, string> typeOwnersByName = new(Comparer);
        private readonly Dictionary<string, OwnerNameScope> ownerScopes = new(Comparer);

        public string RegisterTypeName(string qmlName, string moduleUri)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(qmlName);
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleUri);

            (string ModuleUri, string QmlName) key = (moduleUri, qmlName);
            if (typeNamesByModuleAndQmlName.TryGetValue(key, out string? existingName))
            {
                return existingName;
            }

            string candidateName = ConvertQmlNameToCSharpName(qmlName);
            if (IsBuiltInTypeCollision(candidateName))
            {
                candidateName = $"Qml{candidateName}";
            }

            if (typeOwnersByName.TryGetValue(candidateName, out string? existingModule)
                && !string.Equals(existingModule, moduleUri, StringComparison.Ordinal))
            {
                candidateName = ConvertQmlNameToCSharpName($"{moduleUri}.{qmlName}");
            }

            string resolvedName = ResolveTypeName(candidateName, moduleUri);
            typeNamesByModuleAndQmlName.Add(key, resolvedName);
            typeOwnersByName[resolvedName] = moduleUri;
            return resolvedName;
        }

        public string RegisterPropertyName(string propertyName, string ownerType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerType);

            OwnerNameScope scope = GetOwnerScope(ownerType);
            string candidateName = ConvertQmlNameToCSharpName(propertyName);
            if (scope.MethodNames.Contains(candidateName))
            {
                candidateName = $"{candidateName}Property";
            }

            string resolvedName = ResolveMemberName(candidateName, scope.PropertyNames);
            scope.PropertyNames.Add(resolvedName);
            return resolvedName;
        }

        public string RegisterMethodName(string methodName, string ownerType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerType);

            OwnerNameScope scope = GetOwnerScope(ownerType);
            string candidateName = ConvertQmlNameToCSharpName(methodName);
            if (scope.PropertyNames.Contains(candidateName))
            {
                candidateName = $"{candidateName}Method";
            }

            string resolvedName = ResolveMemberName(candidateName, scope.MethodNames);
            scope.MethodNames.Add(resolvedName);
            return resolvedName;
        }

        public string RegisterEnumName(string enumName, string ownerType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(enumName);
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerType);

            OwnerNameScope scope = GetOwnerScope(ownerType);
            string candidateName = ConvertQmlNameToCSharpName(enumName);
            string resolvedName = ResolveMemberName(candidateName, scope.EnumNames);
            scope.EnumNames.Add(resolvedName);
            return resolvedName;
        }

        public bool IsReservedWord(string name)
        {
            return !string.IsNullOrEmpty(name)
                && ReservedWords.Contains(name);
        }

        public string ToSafeIdentifier(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            string trimmedName = name.Trim();
            if (IsReservedWord(trimmedName))
            {
                return $"@{trimmedName}";
            }

            StringBuilder builder = new(trimmedName.Length + 1);
            for (int index = 0; index < trimmedName.Length; index++)
            {
                char character = trimmedName[index];
                if (index == 0)
                {
                    if (IsIdentifierStart(character))
                    {
                        builder.Append(character);
                    }
                    else if (IsIdentifierPart(character))
                    {
                        builder.Append('_');
                        builder.Append(character);
                    }
                }
                else if (IsIdentifierPart(character))
                {
                    builder.Append(character);
                }
            }

            if (builder.Length == 0)
            {
                return "_";
            }

            string identifier = builder.ToString();
            return IsReservedWord(identifier) ? $"@{identifier}" : identifier;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Maintainability",
            "MA0051:Method is too long",
            Justification = "The C# keyword inventory is intentionally centralized for deterministic name escaping.")]
        private static FrozenSet<string> CreateReservedWords()
        {
            return new[]
            {
                "abstract",
                "add",
                "alias",
                "and",
                "args",
                "as",
                "ascending",
                "async",
                "await",
                "base",
                "bool",
                "break",
                "by",
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
                "descending",
                "do",
                "double",
                "dynamic",
                "else",
                "enum",
                "equals",
                "event",
                "explicit",
                "extern",
                "false",
                "field",
                "file",
                "finally",
                "fixed",
                "float",
                "for",
                "foreach",
                "from",
                "get",
                "global",
                "goto",
                "group",
                "if",
                "implicit",
                "in",
                "init",
                "int",
                "interface",
                "internal",
                "into",
                "is",
                "join",
                "let",
                "lock",
                "long",
                "managed",
                "nameof",
                "namespace",
                "new",
                "nint",
                "not",
                "notnull",
                "nuint",
                "null",
                "object",
                "on",
                "operator",
                "or",
                "orderby",
                "out",
                "override",
                "params",
                "partial",
                "private",
                "protected",
                "public",
                "readonly",
                "record",
                "ref",
                "remove",
                "required",
                "return",
                "sbyte",
                "scoped",
                "sealed",
                "select",
                "set",
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
                "unmanaged",
                "unsafe",
                "ushort",
                "using",
                "value",
                "var",
                "virtual",
                "void",
                "volatile",
                "when",
                "where",
                "while",
                "with",
                "yield",
            }.ToFrozenSet(Comparer);
        }

        private static bool IsBuiltInTypeCollision(string name)
        {
            return BuiltInTypeNames.Contains(name);
        }

        private static string FallbackTypeName(string candidateName, string moduleUri)
        {
            if (!string.IsNullOrWhiteSpace(candidateName))
            {
                return candidateName;
            }

            return ConvertQmlNameToCSharpName(moduleUri);
        }

        private string ResolveTypeName(string candidateName, string moduleUri)
        {
            string resolvedName = FallbackTypeName(candidateName, moduleUri);
            if (!typeOwnersByName.ContainsKey(resolvedName))
            {
                return resolvedName;
            }

            int suffix = 2;
            while (typeOwnersByName.ContainsKey($"{resolvedName}{suffix}"))
            {
                suffix++;
            }

            return $"{resolvedName}{suffix}";
        }

        private static string ResolveMemberName(string candidateName, HashSet<string> usedNames)
        {
            if (!usedNames.Contains(candidateName))
            {
                return candidateName;
            }

            int suffix = 2;
            while (usedNames.Contains($"{candidateName}{suffix}"))
            {
                suffix++;
            }

            return $"{candidateName}{suffix}";
        }

        private static string ConvertQmlNameToCSharpName(string name)
        {
            string trimmedName = name.Trim();
            if (ReservedWords.Contains(trimmedName))
            {
                return $"@{trimmedName}";
            }

            StringBuilder builder = new(trimmedName.Length + 1);
            bool capitalizeNext = true;
            for (int index = 0; index < trimmedName.Length; index++)
            {
                char character = trimmedName[index];
                if (!IsIdentifierPart(character))
                {
                    capitalizeNext = true;
                    continue;
                }

                if (builder.Length == 0 && !IsIdentifierStart(character))
                {
                    builder.Append('_');
                }

                builder.Append(capitalizeNext ? char.ToUpperInvariant(character) : character);
                capitalizeNext = false;
            }

            if (builder.Length == 0)
            {
                return "_";
            }

            string candidateName = builder.ToString();
            return ReservedWords.Contains(candidateName) ? $"@{candidateName}" : candidateName;
        }

        private static bool IsIdentifierStart(char character)
        {
            return character == '_' || char.IsLetter(character);
        }

        private static bool IsIdentifierPart(char character)
        {
            return character == '_' || char.IsLetterOrDigit(character);
        }

        private OwnerNameScope GetOwnerScope(string ownerType)
        {
            if (!ownerScopes.TryGetValue(ownerType, out OwnerNameScope? scope))
            {
                scope = new OwnerNameScope();
                ownerScopes.Add(ownerType, scope);
            }

            return scope;
        }

        private sealed class OwnerNameScope
        {
            public HashSet<string> PropertyNames { get; } = new(Comparer);

            public HashSet<string> MethodNames { get; } = new(Comparer);

            public HashSet<string> EnumNames { get; } = new(Comparer);
        }
    }
}
