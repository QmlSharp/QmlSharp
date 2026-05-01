using QmlSharp.Registry;

namespace QmlSharp.Dsl.Generator.Tests.TypeMapper
{
    public sealed class TypeMapperTests
    {
        public static TheoryData<string, string> BuiltInMappings { get; } = new()
        {
            { "int", "int" },
            { "real", "double" },
            { "double", "double" },
            { "bool", "bool" },
            { "string", "string" },
            { "color", "QmlColor" },
            { "url", "string" },
            { "var", "object" },
            { "variant", "object" },
            { "list", "IReadOnlyList<object>" },
            { "date", "DateTime" },
            { "point", "QmlPoint" },
            { "size", "QmlSize" },
            { "rect", "QmlRect" },
            { "font", "QmlFont" },
            { "enumeration", "int" },
        };

        public static TheoryData<string, string> ValueTypeMappings { get; } = new()
        {
            { "color", "QmlColor" },
            { "point", "QmlPoint" },
            { "size", "QmlSize" },
            { "rect", "QmlRect" },
            { "vector2d", "Vector2" },
            { "vector3d", "Vector3" },
            { "vector4d", "Vector4" },
            { "quaternion", "Quaternion" },
            { "matrix4x4", "Matrix4x4" },
        };

        [Theory]
        [MemberData(nameof(BuiltInMappings))]
        public void MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType(
            string qmlType,
            string expectedCSharpType)
        {
            QmlSharp.Dsl.Generator.TypeMapper mapper = new();

            string actual = mapper.MapToCSharp(qmlType);

            Assert.Equal(expectedCSharpType, actual);
        }

        [Theory]
        [MemberData(nameof(ValueTypeMappings))]
        public void MapToCSharp_ReadmeValueTypes_ReturnsExpectedCSharpContracts(
            string qmlType,
            string expectedCSharpType)
        {
            QmlSharp.Dsl.Generator.TypeMapper mapper = new();

            TypeMapping? mapping = mapper.GetMapping(qmlType);

            Assert.NotNull(mapping);
            Assert.Equal(expectedCSharpType, mapper.MapToCSharp(qmlType));
            Assert.Equal(expectedCSharpType, mapping.CSharpType);
            Assert.True(mapping.IsValueType);
            Assert.Equal("QmlSharp.Core", mapping.RequiresImport);
        }

        [Fact]
        public void MapListType_TM15_ListElement_ReturnsReadOnlyListOfMappedElement()
        {
            QmlSharp.Dsl.Generator.TypeMapper mapper = new();

            string actual = mapper.MapListType("Item");

            Assert.Equal("IReadOnlyList<Item>", actual);
        }

        [Fact]
        public void MapToCSharp_TM15_ListSyntax_ReturnsReadOnlyListOfMappedElement()
        {
            QmlSharp.Dsl.Generator.TypeMapper mapper = new();

            string actual = mapper.MapToCSharp("list<color>");

            Assert.Equal("IReadOnlyList<QmlColor>", actual);
        }

        [Fact]
        public void MapToCSharp_TM15_BareList_ReturnsReadOnlyListOfObject()
        {
            QmlSharp.Dsl.Generator.TypeMapper mapper = new();

            string actual = mapper.MapToCSharp("list");

            Assert.Equal("IReadOnlyList<object>", actual);
        }

        [Fact]
        public void GetMapping_TM15_BareList_ReturnsStableListMappingRecord()
        {
            QmlSharp.Dsl.Generator.TypeMapper mapper = new();

            TypeMapping? mapping = mapper.GetMapping("list");

            Assert.NotNull(mapping);
            Assert.Equal("list", mapping.QmlType);
            Assert.Equal("IReadOnlyList<object>", mapping.CSharpType);
            Assert.False(mapping.IsValueType);
            Assert.True(mapping.IsNullable);
            Assert.Equal("null", mapping.DefaultValue);
            Assert.Equal("System.Collections.Generic", mapping.RequiresImport);
        }

        [Fact]
        public void GetMapping_TM15_ListSyntax_ReturnsStableListMappingRecord()
        {
            QmlSharp.Dsl.Generator.TypeMapper mapper = new();

            TypeMapping? mapping = mapper.GetMapping("list<Item>");

            Assert.NotNull(mapping);
            Assert.Equal("list<Item>", mapping.QmlType);
            Assert.Equal("IReadOnlyList<Item>", mapping.CSharpType);
            Assert.False(mapping.IsValueType);
            Assert.True(mapping.IsNullable);
            Assert.Equal("null", mapping.DefaultValue);
            Assert.Equal("System.Collections.Generic", mapping.RequiresImport);
        }

        [Fact]
        public void MapToCSharp_TM17_UnknownType_ReturnsPassThrough()
        {
            QmlSharp.Dsl.Generator.TypeMapper mapper = new();

            string actual = mapper.MapToCSharp("MyCustomType");

            Assert.Equal("MyCustomType", actual);
            Assert.Null(mapper.GetMapping("MyCustomType"));
        }

        [Fact]
        public void RegisterCustomMapping_TM18_CustomMapping_OverridesBuiltInMapping()
        {
            QmlSharp.Dsl.Generator.TypeMapper mapper = new();
            TypeMapping customMapping = new(
                QmlType: "int",
                CSharpType: "MyInt",
                IsValueType: true,
                IsNullable: false,
                DefaultValue: "MyInt.Zero",
                RequiresImport: "MyApp.Types");

            mapper.RegisterCustomMapping(customMapping);

            Assert.Equal("MyInt", mapper.MapToCSharp("int"));
            Assert.Equal(customMapping, mapper.GetMapping("int"));
        }

        [Fact]
        public void GetSetterType_ListProperty_ReturnsReadOnlyListOfMappedElement()
        {
            QmlSharp.Dsl.Generator.TypeMapper mapper = new();
            QmlProperty property = new(
                Name: "children",
                TypeName: "Item",
                IsReadonly: false,
                IsList: true,
                IsRequired: false,
                DefaultValue: null,
                NotifySignal: null);

            string actual = mapper.GetSetterType(property);

            Assert.Equal("IReadOnlyList<Item>", actual);
        }

        [Fact]
        public void GetParameterType_MethodParameter_ReturnsMappedType()
        {
            QmlSharp.Dsl.Generator.TypeMapper mapper = new();
            QmlParameter parameter = new("position", "point");

            string actual = mapper.GetParameterType(parameter);

            Assert.Equal("QmlPoint", actual);
        }

        [Fact]
        public void GetReturnType_MethodWithoutReturnType_ReturnsVoid()
        {
            QmlSharp.Dsl.Generator.TypeMapper mapper = new();
            QmlMethod method = new(
                Name: "forceActiveFocus",
                ReturnType: null,
                Parameters: ImmutableArray<QmlParameter>.Empty);

            string actual = mapper.GetReturnType(method);

            Assert.Equal("void", actual);
        }

        [Fact]
        public void GetReturnType_MethodWithReturnType_ReturnsMappedType()
        {
            QmlSharp.Dsl.Generator.TypeMapper mapper = new();
            QmlMethod method = new(
                Name: "pointAt",
                ReturnType: "point",
                Parameters: ImmutableArray<QmlParameter>.Empty);

            string actual = mapper.GetReturnType(method);

            Assert.Equal("QmlPoint", actual);
        }

        [Fact]
        public void GetAllMappings_BuiltInsAndCustomMappings_ReturnsStableOrdinalOrder()
        {
            QmlSharp.Dsl.Generator.TypeMapper mapper = new();
            mapper.RegisterCustomMapping(new TypeMapping("zCustom", "ZCustom", false, true, "null", "MyApp"));
            mapper.RegisterCustomMapping(new TypeMapping("alphaCustom", "AlphaCustom", false, true, "null", "MyApp"));

            string[] keys = mapper.GetAllMappings().Keys.ToArray();
            string[] sortedKeys = keys.Order(StringComparer.Ordinal).ToArray();

            Assert.Equal(sortedKeys, keys);
        }

        [Fact]
        public void QmlTsParity_CSharpSpecificMappings_RecordIntentionalDivergence()
        {
            QmlSharp.Dsl.Generator.TypeMapper mapper = new();

            Assert.Equal("string", mapper.MapToCSharp("url"));
            Assert.Equal("DateTime", mapper.MapToCSharp("date"));
            Assert.Equal("object", mapper.MapToCSharp("var"));
            Assert.Equal("object", mapper.MapToCSharp("variant"));
            Assert.Equal("MyCustomType", mapper.MapToCSharp("MyCustomType"));
        }
    }
}
