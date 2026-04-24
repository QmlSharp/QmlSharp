using QmlSharp.Registry.Normalization;

namespace QmlSharp.Registry.Tests.Normalization
{
    public sealed class TypeNameMapperTests
    {
        [Fact]
        public void TNM_01_Map_QString_to_string()
        {
            Assert.Equal("string", CreateMapper().ToQmlName("QString"));
        }

        [Fact]
        public void TNM_02_Map_QColor_to_color()
        {
            Assert.Equal("color", CreateMapper().ToQmlName("QColor"));
        }

        [Fact]
        public void TNM_03_Map_QFont_to_font()
        {
            Assert.Equal("font", CreateMapper().ToQmlName("QFont"));
        }

        [Fact]
        public void TNM_04_Map_QUrl_to_url()
        {
            Assert.Equal("url", CreateMapper().ToQmlName("QUrl"));
        }

        [Fact]
        public void TNM_05_Map_QDate_to_date()
        {
            Assert.Equal("date", CreateMapper().ToQmlName("QDate"));
        }

        [Fact]
        public void TNM_06_Map_QDateTime_to_date()
        {
            Assert.Equal("date", CreateMapper().ToQmlName("QDateTime"));
        }

        [Fact]
        public void TNM_07_Map_QPoint_to_point()
        {
            Assert.Equal("point", CreateMapper().ToQmlName("QPoint"));
        }

        [Fact]
        public void TNM_08_Map_QPointF_to_point()
        {
            Assert.Equal("point", CreateMapper().ToQmlName("QPointF"));
        }

        [Fact]
        public void TNM_09_Map_QSize_to_size()
        {
            Assert.Equal("size", CreateMapper().ToQmlName("QSize"));
        }

        [Fact]
        public void TNM_10_Map_QSizeF_to_size()
        {
            Assert.Equal("size", CreateMapper().ToQmlName("QSizeF"));
        }

        [Fact]
        public void TNM_11_Map_QRect_to_rect()
        {
            Assert.Equal("rect", CreateMapper().ToQmlName("QRect"));
        }

        [Fact]
        public void TNM_12_Map_QRectF_to_rect()
        {
            Assert.Equal("rect", CreateMapper().ToQmlName("QRectF"));
        }

        [Fact]
        public void TNM_13_Map_QVariant_to_var()
        {
            Assert.Equal("var", CreateMapper().ToQmlName("QVariant"));
        }

        [Fact]
        public void TNM_14_Map_QVariantList_to_list()
        {
            Assert.Equal("list", CreateMapper().ToQmlName("QVariantList"));
        }

        [Fact]
        public void TNM_15_Map_QVariantMap_to_var()
        {
            Assert.Equal("var", CreateMapper().ToQmlName("QVariantMap"));
        }

        [Fact]
        public void TNM_16_Map_qreal_to_double()
        {
            Assert.Equal("double", CreateMapper().ToQmlName("qreal"));
        }

        [Fact]
        public void TNM_17_Map_QJSValue_to_var()
        {
            Assert.Equal("var", CreateMapper().ToQmlName("QJSValue"));
        }

        [Fact]
        public void TNM_18_Map_QMatrix4x4_to_matrix4x4()
        {
            Assert.Equal("matrix4x4", CreateMapper().ToQmlName("QMatrix4x4"));
        }

        [Fact]
        public void TNM_19_Map_QQuaternion_to_quaternion()
        {
            Assert.Equal("quaternion", CreateMapper().ToQmlName("QQuaternion"));
        }

        [Fact]
        public void TNM_20_Map_QVector2D_to_vector2d()
        {
            Assert.Equal("vector2d", CreateMapper().ToQmlName("QVector2D"));
        }

        [Fact]
        public void TNM_21_Map_QVector3D_to_vector3d()
        {
            Assert.Equal("vector3d", CreateMapper().ToQmlName("QVector3D"));
        }

        [Fact]
        public void TNM_22_Map_QVector4D_to_vector4d()
        {
            Assert.Equal("vector4d", CreateMapper().ToQmlName("QVector4D"));
        }

        [Fact]
        public void TNM_23_Reverse_mapping_string_to_QString()
        {
            Assert.Equal("QString", CreateMapper().ToCppName("string"));
        }

        [Fact]
        public void TNM_24_Reverse_mapping_color_to_QColor()
        {
            Assert.Equal("QColor", CreateMapper().ToCppName("color"));
        }

        [Fact]
        public void TNM_25_Unknown_cpp_type_passes_through()
        {
            Assert.Equal("MyCustomType", CreateMapper().ToQmlName("MyCustomType"));
        }

        [Fact]
        public void TNM_26_Unknown_qml_type_passes_through()
        {
            Assert.Equal("myCustomType", CreateMapper().ToCppName("myCustomType"));
        }

        [Fact]
        public void TNM_27_HasMapping_returns_true_for_known_type()
        {
            Assert.True(CreateMapper().HasMapping("QString"));
        }

        [Fact]
        public void TNM_28_HasMapping_returns_false_for_unknown_type()
        {
            Assert.False(CreateMapper().HasMapping("UnknownType"));
        }

        [Fact]
        public void TNM_29_GetAllMappings_returns_23_or_more_entries()
        {
            IReadOnlyDictionary<string, string> mappings = CreateMapper().GetAllMappings();

            Assert.True(mappings.Count >= 23);
            Assert.Equal("string", mappings["QString"]);
            Assert.Equal("double", mappings["qreal"]);
        }

        [Fact]
        public void TNM_30_Custom_mapping_overrides_builtin()
        {
            ITypeNameMapper mapper = CreateMapper().WithCustomMappings(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["QString"] = "mystring",
                });

            Assert.Equal("mystring", mapper.ToQmlName("QString"));
            Assert.Equal("QString", mapper.ToCppName("mystring"));
        }

        [Fact]
        public void TNM_31_Custom_mapping_adds_new_entry()
        {
            ITypeNameMapper mapper = CreateMapper().WithCustomMappings(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MyType"] = "custom",
                });

            Assert.Equal("custom", mapper.ToQmlName("MyType"));
            Assert.Equal("MyType", mapper.ToCppName("custom"));
            Assert.True(mapper.HasMapping("MyType"));
        }

        [Fact]
        public void WithCustomMappings_returns_new_mapper_without_mutating_original_mapper()
        {
            TypeNameMapper original = CreateMapper();
            ITypeNameMapper customized = original.WithCustomMappings(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["QString"] = "customString",
                });

            Assert.NotSame(original, customized);
            Assert.Equal("string", original.ToQmlName("QString"));
            Assert.Equal("customString", customized.ToQmlName("QString"));
            Assert.Equal("QString", original.ToCppName("string"));
            Assert.Equal("QString", customized.ToCppName("customString"));
        }

        [Fact]
        public void WithCustomMappings_snapshots_the_input_dictionary_to_preserve_immutability()
        {
            Dictionary<string, string> input = new(StringComparer.Ordinal)
            {
                ["MyType"] = "custom",
            };

            ITypeNameMapper mapper = CreateMapper().WithCustomMappings(input);
            input["MyType"] = "mutated";

            Assert.Equal("custom", mapper.ToQmlName("MyType"));
            Assert.Equal("MyType", mapper.ToCppName("custom"));
            Assert.Equal("mutated", input["MyType"]);
        }

        [Fact]
        public void Type_name_mapping_is_case_sensitive()
        {
            ITypeNameMapper mapper = CreateMapper().WithCustomMappings(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["MyType"] = "CustomType",
                });

            Assert.Equal("qstring", mapper.ToQmlName("qstring"));
            Assert.Equal("String", mapper.ToCppName("String"));
            Assert.Equal("mytype", mapper.ToQmlName("mytype"));
            Assert.Equal("customtype", mapper.ToCppName("customtype"));
        }

        [Fact]
        public void Reverse_mapping_uses_canonical_cpp_names_for_duplicate_builtin_qml_names()
        {
            TypeNameMapper mapper = CreateMapper();

            Assert.Equal("QDateTime", mapper.ToCppName("date"));
            Assert.Equal("QPointF", mapper.ToCppName("point"));
            Assert.Equal("QSizeF", mapper.ToCppName("size"));
            Assert.Equal("QRectF", mapper.ToCppName("rect"));
            Assert.Equal("QVariant", mapper.ToCppName("var"));
            Assert.Equal("double", mapper.ToCppName("double"));
            Assert.Equal("QVariantList", mapper.ToCppName("list"));
        }

        [Fact]
        public void Custom_reverse_mapping_overrides_built_in_collision_canonically()
        {
            ITypeNameMapper mapper = CreateMapper().WithCustomMappings(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["JsonValue"] = "var",
                });

            Assert.Equal("JsonValue", mapper.ToCppName("var"));
            Assert.Equal("var", mapper.ToQmlName("JsonValue"));
        }

        [Fact]
        public void Maps_real_qt_numeric_and_sequence_aliases()
        {
            TypeNameMapper mapper = CreateMapper();

            Assert.Equal("double", mapper.ToQmlName("float"));
            Assert.Equal("list<string>", mapper.ToQmlName("QStringList"));
            Assert.Equal("list", mapper.ToQmlName("QList<QVariant>"));
        }

        [Fact]
        public void Reverse_mapping_uses_canonical_cpp_names_for_sequence_collisions()
        {
            TypeNameMapper mapper = CreateMapper();

            Assert.Equal("QVariantList", mapper.ToCppName("list"));
            Assert.Equal("QStringList", mapper.ToCppName("list<string>"));
        }

        [Fact]
        public void GetAllMappings_returns_merged_snapshot_for_custom_mapper()
        {
            TypeNameMapper original = CreateMapper();
            ITypeNameMapper customized = original.WithCustomMappings(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["QString"] = "textual",
                    ["MyType"] = "myType",
                });

            IReadOnlyDictionary<string, string> originalMappings = original.GetAllMappings();
            IReadOnlyDictionary<string, string> customizedMappings = customized.GetAllMappings();

            Assert.Equal("string", originalMappings["QString"]);
            Assert.False(originalMappings.ContainsKey("MyType"));
            Assert.Equal("textual", customizedMappings["QString"]);
            Assert.Equal("myType", customizedMappings["MyType"]);
        }

        [Fact]
        public void Custom_reverse_mapping_with_duplicate_qml_names_is_deterministic()
        {
            ITypeNameMapper mapper = CreateMapper().WithCustomMappings(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["SecondCppType"] = "sharedCustomType",
                    ["FirstCppType"] = "sharedCustomType",
                });

            Assert.Equal("FirstCppType", mapper.ToCppName("sharedCustomType"));
            Assert.Equal("sharedCustomType", mapper.ToQmlName("SecondCppType"));
        }

        [Theory]
        [InlineData("", "qmlName")]
        [InlineData(" ", "qmlName")]
        [InlineData("CppName", "")]
        [InlineData("CppName", " ")]
        public void WithCustomMappings_rejects_blank_keys_and_values(string cppTypeName, string qmlTypeName)
        {
            IReadOnlyDictionary<string, string> mappings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [cppTypeName] = qmlTypeName,
            };

            _ = Assert.Throws<ArgumentException>(() => CreateMapper().WithCustomMappings(mappings));
        }

        private static TypeNameMapper CreateMapper()
        {
            return new TypeNameMapper();
        }
    }
}
