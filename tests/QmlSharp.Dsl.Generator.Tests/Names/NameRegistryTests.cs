namespace QmlSharp.Dsl.Generator.Tests.Names
{
    public sealed class NameRegistryTests
    {
        public static TheoryData<string> CSharpKeywords { get; } = new()
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
        };

        [Fact]
        public void RegisterPropertyName_NR01_DefaultKeyword_ReturnsEscapedIdentifier()
        {
            NameRegistry registry = new();

            string actual = registry.RegisterPropertyName("default", "Rectangle");

            Assert.Equal("@default", actual);
        }

        [Fact]
        public void RegisterPropertyName_NR02_ClassKeyword_ReturnsEscapedIdentifier()
        {
            NameRegistry registry = new();

            string actual = registry.RegisterPropertyName("class", "Rectangle");

            Assert.Equal("@class", actual);
        }

        [Fact]
        public void RegisterTypeName_NR03_CSharpBuiltInTypeCollision_PrefixesQml()
        {
            NameRegistry registry = new();

            string actual = registry.RegisterTypeName("Object", "QtQml");

            Assert.Equal("QmlObject", actual);
        }

        [Fact]
        public void RegisterMethodName_NR04_PropertyCollision_AppendsMethodSuffix()
        {
            NameRegistry registry = new();
            string propertyName = registry.RegisterPropertyName("focus", "Item");

            string actual = registry.RegisterMethodName("focus", "Item");

            Assert.Equal("Focus", propertyName);
            Assert.Equal("FocusMethod", actual);
        }

        [Fact]
        public void RegisterTypeName_NR05_CrossModuleCollision_ReturnsModuleQualifiedIdentifier()
        {
            NameRegistry registry = new();
            string quickButton = registry.RegisterTypeName("Button", "QtQuick");

            string controlsButton = registry.RegisterTypeName("Button", "QtQuick.Controls");

            Assert.Equal("Button", quickButton);
            Assert.Equal("QtQuickControlsButton", controlsButton);
        }

        [Theory]
        [MemberData(nameof(CSharpKeywords))]
        public void IsReservedWord_NR06_AllCSharpKeywords_ReturnTrue(string keyword)
        {
            NameRegistry registry = new();

            bool actual = registry.IsReservedWord(keyword);

            Assert.True(actual, $"Expected '{keyword}' to be treated as a reserved word.");
        }

        [Fact]
        public void RegisterPropertyName_PascalCaseConversion_ReturnsCSharpPropertyName()
        {
            NameRegistry registry = new();

            string actual = registry.RegisterPropertyName("pressAndHold", "MouseArea");

            Assert.Equal("PressAndHold", actual);
        }

        [Fact]
        public void RegisterEnumName_InvalidCharacters_ReturnsSafePascalCaseIdentifier()
        {
            NameRegistry registry = new();

            string actual = registry.RegisterEnumName("horizontal-center", "Item");

            Assert.Equal("HorizontalCenter", actual);
        }

        [Fact]
        public void RegisterEnumName_PropertyCollision_ReturnsDeterministicSafeIdentifier()
        {
            NameRegistry registry = new();
            string propertyName = registry.RegisterPropertyName("state", "Item");

            string actual = registry.RegisterEnumName("state", "Item");

            Assert.Equal("State", propertyName);
            Assert.Equal("State2", actual);
        }

        [Fact]
        public void RegisterEnumName_MethodCollision_ReturnsDeterministicSafeIdentifier()
        {
            NameRegistry registry = new();
            string methodName = registry.RegisterMethodName("state", "Item");

            string actual = registry.RegisterEnumName("state", "Item");

            Assert.Equal("State", methodName);
            Assert.Equal("State2", actual);
        }

        [Fact]
        public void RegisterPropertyAndMethodName_EnumCollision_ReturnsDeterministicSafeIdentifier()
        {
            NameRegistry registry = new();
            string enumName = registry.RegisterEnumName("state", "Item");

            string propertyName = registry.RegisterPropertyName("state", "Item");
            string methodName = registry.RegisterMethodName("state", "Item");

            Assert.Equal("State", enumName);
            Assert.Equal("State2", propertyName);
            Assert.Equal("State3", methodName);
        }

        [Fact]
        public void RegisterPropertyName_SameOwnerDuplicate_ReturnsDeterministicNumericSuffixes()
        {
            NameRegistry registry = new();

            string first = registry.RegisterPropertyName("state", "Item");
            string second = registry.RegisterPropertyName("state", "Item");
            string third = registry.RegisterPropertyName("state", "Item");

            Assert.Equal("State", first);
            Assert.Equal("State2", second);
            Assert.Equal("State3", third);
        }

        [Fact]
        public void RegisterPropertyName_DifferentOwners_DoNotCollide()
        {
            NameRegistry registry = new();

            string itemName = registry.RegisterPropertyName("state", "Item");
            string textName = registry.RegisterPropertyName("state", "Text");

            Assert.Equal("State", itemName);
            Assert.Equal("State", textName);
        }

        [Fact]
        public void ToSafeIdentifier_InvalidCharacters_RemovesInvalidCharacters()
        {
            NameRegistry registry = new();

            string actual = registry.ToSafeIdentifier("9 invalid-name!");

            Assert.Equal("_9invalidname", actual);
        }

        [Fact]
        public void RegisterTypeName_RepeatedSameModuleType_ReturnsExistingName()
        {
            NameRegistry registry = new();

            string first = registry.RegisterTypeName("Button", "QtQuick.Controls");
            string second = registry.RegisterTypeName("Button", "QtQuick.Controls");

            Assert.Equal("Button", first);
            Assert.Equal(first, second);
        }
    }
}
