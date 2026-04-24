using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Parsing;

namespace QmlSharp.Registry.Tests.Parsing
{
    public sealed class MetatypesParserTests
    {
        [Fact]
        public void MTP_01_Parse_empty_JSON_array_returns_empty_entries()
        {
            ParseResult<RawMetatypesFile> result = CreateParser().ParseContent("[]", @"fixtures\metatypes\empty.json");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(@"fixtures\metatypes\empty.json", result.Value!.SourcePath);
            Assert.Empty(result.Value.Entries);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void MTP_02_Parse_entry_with_inputFile()
        {
            RawMetatypesEntry entry = Assert.Single(ParseFixture("minimal.json").Entries);

            Assert.Equal("qquickitem.h", entry.InputFile);
        }

        [Fact]
        public void MTP_03_Parse_class_with_className()
        {
            Assert.Equal("QQuickItem", GetClass(ParseFixture("minimal.json"), "QQuickItem").ClassName);
        }

        [Fact]
        public void MTP_04_Parse_class_with_qualifiedClassName()
        {
            Assert.Equal("QQuickItem", GetClass(ParseFixture("minimal.json"), "QQuickItem").QualifiedClassName);
        }

        [Fact]
        public void MTP_05_Parse_object_class_QObject_derived()
        {
            Assert.True(GetClass(ParseFixture("minimal.json"), "QQuickItem").IsObject);
        }

        [Fact]
        public void MTP_06_Parse_gadget_class()
        {
            Assert.True(GetClass(ParseFixture("multiple-classes.json"), "QQuickPaletteGadget").IsGadget);
        }

        [Fact]
        public void MTP_07_Parse_namespace_class()
        {
            Assert.True(GetClass(ParseFixture("multiple-classes.json"), "QQuickNamespace").IsNamespace);
        }

        [Fact]
        public void MTP_08_Parse_superClasses()
        {
            RawMetatypesSuperClass superClass = Assert.Single(GetClass(ParseFixture("minimal.json"), "QQuickItem").SuperClasses);

            Assert.Equal("QObject", superClass.Name);
            Assert.Equal("public", superClass.Access);
        }

        [Fact]
        public void MTP_09_Parse_classInfos_with_QML_Element()
        {
            RawMetatypesClassInfo classInfo = Assert.Single(
                GetClass(ParseFixture("minimal.json"), "QQuickItem").ClassInfos.Where(classInfo => classInfo.Name == "QML.Element"));

            Assert.Equal("Item", classInfo.Value);
        }

        [Fact]
        public void MTP_10_Parse_classInfos_with_QML_Attached()
        {
            RawMetatypesClassInfo classInfo = Assert.Single(
                GetClass(ParseFixture("multiple-classes.json"), "QQuickRichItem").ClassInfos.Where(classInfo => classInfo.Name == "QML.Attached"));

            Assert.Equal("QQuickKeysAttached", classInfo.Value);
        }

        [Fact]
        public void MTP_11_Parse_classInfos_with_QML_Foreign()
        {
            RawMetatypesClassInfo classInfo = Assert.Single(
                GetClass(ParseFixture("multiple-classes.json"), "QQuickPaletteGadget").ClassInfos.Where(classInfo => classInfo.Name == "QML.Foreign"));

            Assert.Equal("QPalette", classInfo.Value);
        }

        [Fact]
        public void MTP_12_Parse_property_with_all_supported_fields()
        {
            RawMetatypesProperty property = Assert.Single(
                GetClass(ParseFixture("multiple-classes.json"), "QQuickRichItem").Properties.Where(property => property.Name == "title"));

            Assert.Equal("QString", property.Type);
            Assert.Equal("title", property.Read);
            Assert.Equal("setTitle", property.Write);
            Assert.Equal("titleChanged", property.Notify);
            Assert.Equal("bindableTitle", property.BindableProperty);
            Assert.Equal(2, property.Revision);
            Assert.Equal(3, property.Index);
            Assert.True(property.IsReadonly);
            Assert.True(property.IsConstant);
            Assert.True(property.IsFinal);
            Assert.True(property.IsRequired);
        }

        [Fact]
        public void MTP_13_Parse_readonly_property()
        {
            RawMetatypesProperty property = Assert.Single(
                GetClass(ParseFixture("multiple-classes.json"), "QQuickRichItem").Properties.Where(property => property.Name == "title"));

            Assert.True(property.IsReadonly);
        }

        [Fact]
        public void MTP_14_Parse_required_property()
        {
            RawMetatypesProperty property = Assert.Single(
                GetClass(ParseFixture("multiple-classes.json"), "QQuickRichItem").Properties.Where(property => property.Name == "title"));

            Assert.True(property.IsRequired);
        }

        [Fact]
        public void MTP_15_Parse_signal_with_arguments()
        {
            RawMetatypesSignal signal = Assert.Single(
                GetClass(ParseFixture("multiple-classes.json"), "QQuickRichItem").Signals.Where(signal => signal.Name == "titleChanged"));

            RawMetatypesParameter argument = Assert.Single(signal.Arguments);
            Assert.Equal("value", argument.Name);
            Assert.Equal("QString", argument.Type);
            Assert.Equal(2, signal.Revision);
        }

        [Fact]
        public void MTP_16_Parse_method_with_return_type_and_arguments()
        {
            RawMetatypesMethod method = Assert.Single(
                GetClass(ParseFixture("multiple-classes.json"), "QQuickRichItem").Methods.Where(method => method.Name == "mapToItem"));

            Assert.Equal("QPointF", method.ReturnType);
            Assert.False(method.IsCloned);
            Assert.Equal(3, method.Revision);
            Assert.Collection(
                method.Arguments,
                argument =>
                {
                    Assert.Equal("item", argument.Name);
                    Assert.Equal("QQuickItem*", argument.Type);
                },
                argument =>
                {
                    Assert.Equal("point", argument.Name);
                    Assert.Equal("QPointF", argument.Type);
                });
        }

        [Fact]
        public void MTP_17_Parse_enum_with_values()
        {
            RawMetatypesEnum @enum = Assert.Single(
                GetClass(ParseFixture("multiple-classes.json"), "QQuickRichItem").Enums.Where(@enum => @enum.Name == "TransformOrigin"));

            Assert.Equal(["TopLeft", "Center", "BottomRight"], @enum.Values.ToArray());
        }

        [Fact]
        public void MTP_18_Parse_multiple_classes_in_one_entry()
        {
            RawMetatypesEntry entry = Assert.Single(ParseFixture("multiple-classes.json").Entries);

            Assert.Equal(
                ["QQuickRichItem", "QQuickPaletteGadget", "QQuickNamespace"],
                entry.Classes.Select(candidate => candidate.ClassName).ToArray());
        }

        [Fact]
        public void MTP_19_Parse_actual_qtquick_excerpt_fixture_without_errors()
        {
            ParseResult<RawMetatypesFile> result = CreateParser().Parse(GetFixturePath("qtquick-excerpt.json"));

            Assert.True(result.IsSuccess);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

            RawMetatypesClass item = GetClass(result.Value!, "QQuickItem");
            RawMetatypesClass accessible = GetClass(result.Value!, "QQuickAccessibleAttached");

            Assert.Contains(item.ClassInfos, classInfo => classInfo.Name == "QML.Attached" && classInfo.Value == "QQuickKeysAttached");
            Assert.Contains(item.Properties, property => property.Name == "width" && property.BindableProperty == "bindableWidth");
            Assert.Contains(item.Methods, method => method.Name == "grabToImage" && method.ReturnType == "bool");
            Assert.Contains(item.Enums, @enum => @enum.Name == "TransformOrigin");
            Assert.Contains(accessible.ClassInfos, classInfo => classInfo.Name == "QML.Element" && classInfo.Value == "Accessible");
            Assert.Contains(accessible.Properties, property => property.Name == "role");
        }

        [Fact]
        public void MTP_20_Invalid_JSON_produces_REG030_with_source_location()
        {
            ParseResult<RawMetatypesFile> result = CreateParser().ParseContent(
                "[\r\n  { invalid json\r\n]",
                @"fixtures\metatypes\invalid.json");

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value!.Entries);

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics.Where(diagnostic => diagnostic.Code == DiagnosticCodes.MetatypesJsonError));
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(@"fixtures\metatypes\invalid.json", diagnostic.FilePath);
            Assert.True(diagnostic.Line is > 0);
            Assert.True(diagnostic.Column is > 0);
        }

        [Fact]
        public void Invalid_JSON_location_reports_character_column_for_non_ascii_input()
        {
            ParseResult<RawMetatypesFile> result = CreateParser().ParseContent(
                "[\r\n  { \"π\": true, invalid json\r\n]",
                @"fixtures\metatypes\non-ascii-invalid.json");

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics.Where(diagnostic => diagnostic.Code == DiagnosticCodes.MetatypesJsonError));

            Assert.Equal(2, diagnostic.Line);
            Assert.Equal(16, diagnostic.Column);
        }

        [Fact]
        public void Parse_real_qt_shapes_with_empty_metadata_values_without_diagnostics()
        {
            const string content = """
[
  {
    "inputFile": "metadata-only.h",
    "outputRevision": 69
  },
  {
    "inputFile": "real-shapes.h",
    "classes": [
      {
        "className": "RealShape",
        "qualifiedClassName": "RealShape",
        "object": true,
        "classInfos": [
          { "name": "QML.UncreatableReason", "value": "" }
        ],
        "properties": [
          {
            "name": "",
            "type": "QVariant",
            "read": "modelData",
            "notify": "modelDataChanged"
          }
        ]
      }
    ]
  }
]
""";

            ParseResult<RawMetatypesFile> result = CreateParser().ParseContent(content, @"fixtures\metatypes\real-shapes.json");

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Diagnostics);
            Assert.Empty(result.Value!.Entries[0].Classes);

            RawMetatypesClass parsedClass = Assert.Single(result.Value.Entries[1].Classes);
            Assert.Equal(string.Empty, Assert.Single(parsedClass.ClassInfos).Value);
            Assert.Equal(string.Empty, Assert.Single(parsedClass.Properties).Name);
        }

        [Fact]
        public void Parse_missing_required_fields_produces_REG031_and_continues_with_partial_results()
        {
            const string content = """
[
  {
    "inputFile": "broken.h",
    "classes": [
      {
        "qualifiedClassName": "Broken",
        "object": true
      }
    ]
  },
  {
    "inputFile": "valid.h",
    "classes": [
      {
        "className": "Valid",
        "qualifiedClassName": "Valid",
        "object": true,
        "properties": [],
        "signals": [],
        "methods": [],
        "enums": []
      }
    ]
  }
]
""";

            ParseResult<RawMetatypesFile> result = CreateParser().ParseContent(content, @"fixtures\metatypes\missing-class-name.json");

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Value);

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics.Where(diagnostic => diagnostic.Code == DiagnosticCodes.MetatypesMissingField));
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(@"fixtures\metatypes\missing-class-name.json", diagnostic.FilePath);

            Assert.Collection(
                result.Value!.Entries,
                entry => Assert.Empty(entry.Classes),
                entry => Assert.Equal("Valid", Assert.Single(entry.Classes).ClassName));
        }

        [Fact]
        public void Parse_top_level_non_array_reports_shape_error()
        {
            ParseResult<RawMetatypesFile> result = CreateParser().ParseContent(
                "{}",
                @"fixtures\metatypes\object-root.json");

            Assert.False(result.IsSuccess);
            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticCodes.MetatypesMissingField, diagnostic.Code);
            Assert.Contains("top-level JSON array", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Parse_nested_non_object_members_report_shape_errors_and_preserve_valid_entries()
        {
            const string content = """
[
    true,
    {
        "inputFile": "broken.h",
        "classes": [
            42,
            {
                "className": "Broken",
                "superClasses": [true],
                "classInfos": [false],
                "properties": [1],
                "signals": [2],
                "methods": [3],
                "enums": [
                    {
                        "name": "Mode",
                        "values": ["Ok", null, { "bad": 1 }]
                    }
                ]
            }
        ]
    },
    {
        "inputFile": "valid.h",
        "classes": [
            {
                "className": "Valid"
            }
        ]
    }
]
""";

            ParseResult<RawMetatypesFile> result = CreateParser().ParseContent(content, @"fixtures\metatypes\shape-errors.json");

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("entry[0] must be a JSON object", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("entry[1].classes[0] must be a JSON object", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("superClasses[0] must be a JSON object", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("classInfos[0] must be a JSON object", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("properties[0] must be a JSON object", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("signals[0] must be a JSON object", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("methods[0] must be a JSON object", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("values[1] must be a string, number, or boolean value", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("values[2] must be a string, number, or boolean value", StringComparison.Ordinal));
            Assert.Equal(["Broken", "Valid"], result.Value!.Entries.SelectMany(entry => entry.Classes).Select(@class => @class.ClassName).ToArray());
        }

        [Fact]
        public void Parse_string_and_numeric_scalar_variants_are_coerced_without_shape_errors()
        {
            const string content = """
[
    {
        "inputFile": "coerced.h",
        "classes": [
            {
                "className": "Coerced",
                "object": "1",
                "gadget": "false",
                "namespace": 0,
                "properties": [
                    {
                        "name": "value",
                        "type": "int",
                        "readonly": "true",
                        "constant": "1",
                        "final": 1,
                        "required": "0",
                        "revision": "7",
                        "index": "9"
                    }
                ],
                "signals": [
                    {
                        "name": "changed",
                        "revision": "2",
                        "arguments": [
                            { "type": "int" }
                        ]
                    }
                ],
                "methods": [
                    {
                        "name": "setValue",
                        "revision": "3",
                        "isCloned": "1",
                        "arguments": [
                            { "type": "int" }
                        ]
                    }
                ],
                "enums": [
                    {
                        "name": "Mode",
                        "isFlag": "true",
                        "isClass": 0,
                        "values": [1, true, "Ok"]
                    }
                ]
            }
        ]
    }
]
""";

            ParseResult<RawMetatypesFile> result = CreateParser().ParseContent(content, @"fixtures\metatypes\coerced-values.json");

            Assert.True(result.IsSuccess);
            RawMetatypesClass parsedClass = Assert.Single(result.Value!.Entries.SelectMany(entry => entry.Classes));
            RawMetatypesProperty property = Assert.Single(parsedClass.Properties);
            RawMetatypesSignal signal = Assert.Single(parsedClass.Signals);
            RawMetatypesMethod method = Assert.Single(parsedClass.Methods);
            RawMetatypesEnum @enum = Assert.Single(parsedClass.Enums);

            Assert.True(parsedClass.IsObject);
            Assert.False(parsedClass.IsGadget);
            Assert.False(parsedClass.IsNamespace);
            Assert.True(property.IsReadonly);
            Assert.True(property.IsConstant);
            Assert.True(property.IsFinal);
            Assert.False(property.IsRequired);
            Assert.Equal(7, property.Revision);
            Assert.Equal(9, property.Index);
            Assert.Equal(2, signal.Revision);
            Assert.Equal(3, method.Revision);
            Assert.True(method.IsCloned);
            Assert.True(@enum.IsFlag);
            Assert.False(@enum.IsClass);
            Assert.Contains(@enum.Values, value => string.Equals(value, "1", StringComparison.Ordinal));
            Assert.Contains(@enum.Values, value => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Parse_missing_and_invalid_required_member_fields_report_shape_errors()
        {
            const string content = """
[
    {
        "inputFile": "invalid-members.h",
        "classes": [
            {
                "className": "InvalidMembers",
                "qualifiedClassName": 17,
                "superClasses": [ {} ],
                "classInfos": [ { "value": 1 } ],
                "properties": [ { "name": 3, "type": 4, "read": 5, "revision": "oops", "readonly": "maybe" } ],
                "signals": [ { "arguments": [ {} ] } ],
                "methods": [ { "arguments": [ {} ] } ],
                "enums": [ { "name": "", "values": 5 } ]
            }
        ]
    }
]
""";

            ParseResult<RawMetatypesFile> result = CreateParser().ParseContent(content, @"fixtures\metatypes\invalid-members.json");

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("property 'qualifiedClassName' must be a string", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("superClasses[0] is missing required string property 'name'", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("classInfos[0] is missing required string property 'name'", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("methods[0] is missing required string property 'name'", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("classInfos[0] property 'value' must be a string", StringComparison.Ordinal));
        }

        private static IMetatypesParser CreateParser()
        {
            return new MetatypesParser();
        }

        private static RawMetatypesFile ParseFixture(string fixtureName)
        {
            ParseResult<RawMetatypesFile> result = CreateParser().Parse(GetFixturePath(fixtureName));
            Assert.True(result.IsSuccess);
            return result.Value!;
        }

        private static RawMetatypesClass GetClass(RawMetatypesFile file, string className)
        {
            return Assert.Single(file.Entries.SelectMany(entry => entry.Classes), candidate => candidate.ClassName == className);
        }

        private static string GetFixturePath(string fixtureName)
        {
            if (Path.IsPathRooted(fixtureName))
            {
                throw new ArgumentException("Fixture name must be a relative path.", nameof(fixtureName));
            }

            return Path.Join(AppContext.BaseDirectory, "fixtures", "metatypes", fixtureName);
        }
    }
}
