using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Parsing;

namespace QmlSharp.Registry.Tests.Parsing
{
    public sealed class QmltypesParserTests
    {
        [Fact]
        public void QTP_01_Parse_empty_qmltypes_file_returns_empty_components()
        {
            ParseResult<RawQmltypesFile> result = CreateParser().ParseContent(string.Empty, @"fixtures\qmltypes\empty.qmltypes");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(@"fixtures\qmltypes\empty.qmltypes", result.Value!.SourcePath);
            Assert.Empty(result.Value.Components);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void QTP_02_Parse_single_Component_with_name_only()
        {
            string fixturePath = GetFixturePath("minimal.qmltypes");

            ParseResult<RawQmltypesFile> result = CreateParser().Parse(fixturePath);

            Assert.True(result.IsSuccess);
            RawQmltypesComponent component = Assert.Single(result.Value!.Components);
            Assert.Equal(fixturePath, result.Value.SourcePath);
            Assert.Equal("QQuickItem", component.Name);
        }

        [Fact]
        public void QTP_03_Parse_accessSemantics_reference()
        {
            Assert.Equal("reference", ParseSingleComponent(CreateAccessSemanticsContent("reference")).AccessSemantics);
        }

        [Fact]
        public void QTP_04_Parse_accessSemantics_value()
        {
            Assert.Equal("value", ParseSingleComponent(CreateAccessSemanticsContent("value")).AccessSemantics);
        }

        [Fact]
        public void QTP_05_Parse_accessSemantics_sequence()
        {
            Assert.Equal("sequence", ParseSingleComponent(CreateAccessSemanticsContent("sequence")).AccessSemantics);
        }

        [Fact]
        public void QTP_06_Parse_accessSemantics_none()
        {
            Assert.Equal("none", ParseSingleComponent(CreateAccessSemanticsContent("none")).AccessSemantics);
        }

        [Fact]
        public void QTP_07_Parse_prototype()
        {
            Assert.Equal("QQuickItem", ParseRichComponent().Prototype);
        }

        [Fact]
        public void QTP_08_Parse_defaultProperty()
        {
            Assert.Equal("data", ParseRichComponent().DefaultProperty);
        }

        [Fact]
        public void QTP_09_Parse_attachedType()
        {
            Assert.Equal("QQuickKeysAttached", ParseRichComponent().AttachedType);
        }

        [Fact]
        public void QTP_10_Parse_singleton_flag()
        {
            Assert.True(ParseRichComponent().IsSingleton);
        }

        [Fact]
        public void QTP_11_Parse_single_export()
        {
            RawQmltypesComponent component = ParseSingleComponent(
                CreateComponentContent(
                    "name: \"QQuickSolo\"",
                    "exports: [\"QtQuick/Solo 2.0\"]"));

            string export = Assert.Single(component.Exports);
            Assert.Equal("QtQuick/Solo 2.0", export);
        }

        [Fact]
        public void QTP_12_Parse_multiple_exports()
        {
            Assert.Equal(
                ["QtQuick/Fancy 2.0", "QtQuick/Fancy 2.15"],
                ParseRichComponent().Exports.ToArray());
        }

        [Fact]
        public void QTP_13_Parse_exportMetaObjectRevisions()
        {
            RawQmltypesComponent component = ParseRichComponent();

            Assert.Equal([512, 527], component.ExportMetaObjectRevisions.ToArray());
            Assert.Equal(new QmlVersion(2, 0), DecodeRevision(component.ExportMetaObjectRevisions[0]));
            Assert.Equal(new QmlVersion(2, 15), DecodeRevision(component.ExportMetaObjectRevisions[1]));
        }

        [Fact]
        public void QTP_14_Parse_string_property()
        {
            RawQmltypesProperty property = Assert.Single(
                ParseRichComponent().Properties.Where(property => property.Name == "title"));

            Assert.Equal("QString", property.Type);
        }

        [Fact]
        public void QTP_15_Parse_readonly_property()
        {
            RawQmltypesProperty property = Assert.Single(
                ParseRichComponent().Properties.Where(property => property.Name == "children"));

            Assert.True(property.IsReadonly);
        }

        [Fact]
        public void QTP_16_Parse_list_property()
        {
            RawQmltypesProperty property = Assert.Single(
                ParseRichComponent().Properties.Where(property => property.Name == "children"));

            Assert.True(property.IsList);
        }

        [Fact]
        public void QTP_17_Parse_required_property()
        {
            RawQmltypesProperty property = Assert.Single(
                ParseRichComponent().Properties.Where(property => property.Name == "requiredName"));

            Assert.True(property.IsRequired);
        }

        [Fact]
        public void QTP_18_Parse_property_with_read_write_notify()
        {
            RawQmltypesProperty property = Assert.Single(
                ParseRichComponent().Properties.Where(property => property.Name == "title"));

            Assert.Equal("title", property.Read);
            Assert.Equal("setTitle", property.Write);
            Assert.Equal("titleChanged", property.Notify);
        }

        [Fact]
        public void QTP_19_Parse_signal_with_no_parameters()
        {
            RawQmltypesSignal signal = Assert.Single(
                ParseRichComponent().Signals.Where(signal => signal.Name == "triggered"));

            Assert.Empty(signal.Parameters);
        }

        [Fact]
        public void QTP_20_Parse_signal_with_parameters()
        {
            RawQmltypesSignal signal = Assert.Single(
                ParseRichComponent().Signals.Where(signal => signal.Name == "accepted"));

            Assert.Collection(
                signal.Parameters,
                parameter =>
                {
                    Assert.Equal("index", parameter.Name);
                    Assert.Equal("int", parameter.Type);
                },
                parameter =>
                {
                    Assert.Equal("label", parameter.Name);
                    Assert.Equal("QString", parameter.Type);
                });
        }

        [Fact]
        public void QTP_21_Parse_method_with_no_return_type()
        {
            RawQmltypesMethod method = Assert.Single(
                ParseRichComponent().Methods.Where(method => method.Name == "reset"));

            Assert.Null(method.ReturnType);
        }

        [Fact]
        public void QTP_22_Parse_method_with_return_type()
        {
            RawQmltypesMethod method = Assert.Single(
                ParseRichComponent().Methods.Where(method => method.Name == "compute"));

            Assert.Equal("int", method.ReturnType);
        }

        [Fact]
        public void QTP_23_Parse_method_with_parameters()
        {
            RawQmltypesMethod method = Assert.Single(
                ParseRichComponent().Methods.Where(method => method.Name == "compute"));

            Assert.Collection(
                method.Parameters,
                parameter =>
                {
                    Assert.Equal("count", parameter.Name);
                    Assert.Equal("int", parameter.Type);
                },
                parameter =>
                {
                    Assert.Equal("enabled", parameter.Name);
                    Assert.Equal("bool", parameter.Type);
                });
        }

        [Fact]
        public void QTP_24_Parse_enum_values()
        {
            RawQmltypesEnum @enum = Assert.Single(
                ParseRichComponent().Enums.Where(@enum => @enum.Name == "State"));

            Assert.Equal(["Idle", "Busy"], @enum.Values.ToArray());
        }

        [Fact]
        public void QTP_25_Parse_flag_enum()
        {
            RawQmltypesEnum @enum = Assert.Single(
                ParseRichComponent().Enums.Where(@enum => @enum.Name == "Modes"));

            Assert.True(@enum.IsFlag);
        }

        [Fact]
        public void QTP_26_Parse_enum_alias()
        {
            RawQmltypesEnum @enum = Assert.Single(
                ParseRichComponent().Enums.Where(@enum => @enum.Name == "Modes"));

            Assert.Equal("Mode", @enum.Alias);
        }

        [Fact]
        public void QTP_27_Parse_multiple_components()
        {
            ParseResult<RawQmltypesFile> result = CreateParser().Parse(GetFixturePath("multiple-components.qmltypes"));

            Assert.True(result.IsSuccess);
            Assert.Equal(
                ["QQuickItem", "QQuickRectangle", "QQuickText"],
                result.Value!.Components.Select(component => component.Name).ToArray());
        }

        [Fact]
        public void QTP_28_Parse_component_with_all_member_types()
        {
            RawQmltypesComponent component = ParseRichComponent();

            Assert.NotEmpty(component.Properties);
            Assert.NotEmpty(component.Signals);
            Assert.NotEmpty(component.Methods);
            Assert.NotEmpty(component.Enums);
        }

        [Fact]
        public void QTP_29_Parse_actual_qtquick_plugins_qmltypes_excerpt()
        {
            ParseResult<RawQmltypesFile> result = CreateParser().Parse(GetFixturePath("qtquick-excerpt.qmltypes"));

            Assert.True(result.IsSuccess);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

            RawQmltypesComponent item = Assert.Single(result.Value!.Components.Where(component => component.Name == "QQuickItem"));
            RawQmltypesComponent rectangle = Assert.Single(result.Value.Components.Where(component => component.Name == "QQuickRectangle"));

            Assert.Contains("QQmlParserStatus", item.Interfaces);
            Assert.Contains(item.Enums, @enum => @enum.Name == "TransformOrigin");
            Assert.Contains(item.Methods, method => method.Name == "childAt");
            Assert.Contains(rectangle.Properties, property => property.Name == "color");
        }

        [Fact]
        public void QTP_30_Invalid_syntax_produces_diagnostic()
        {
            const string content = """
Component {
    name: "Broken"
    Property {
        name "oops"
        type: "QString"
    }
}
""";

            ParseResult<RawQmltypesFile> result = CreateParser().ParseContent(content, @"fixtures\qmltypes\invalid-syntax.qmltypes");

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics.Where(diagnostic => diagnostic.Code == DiagnosticCodes.QmltypesSyntaxError));
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(@"fixtures\qmltypes\invalid-syntax.qmltypes", diagnostic.FilePath);
            Assert.Equal(4, diagnostic.Line);
            Assert.Equal(14, diagnostic.Column);
        }

        [Fact]
        public void Parse_comments_and_whitespace_do_not_affect_parsing()
        {
            const string content = """
/* header comment */

Component /* inline comment */ {
    // property comment
    name: "Commented"

    Property {
        name: "title"; /* trailing block comment */
        type: "QString"
    }
}
""";

            RawQmltypesComponent component = ParseSingleComponent(content);

            Assert.Equal("Commented", component.Name);
            Assert.Contains(component.Properties, property => property.Name == "title");
        }

        [Fact]
        public void Parse_component_extension_isCreatable_interfaces_and_bindable_metadata()
        {
            RawQmltypesComponent component = ParseRichComponent();
            RawQmltypesProperty property = Assert.Single(component.Properties.Where(candidate => candidate.Name == "title"));

            Assert.Equal("QQuickFancyExtension", component.Extension);
            Assert.False(component.IsCreatable);
            Assert.Equal(["QQmlParserStatus", "QQuickFancyInterface"], component.Interfaces.ToArray());
            Assert.Equal("bindableTitle", property.BindableProperty);
        }

        [Fact]
        public void Parse_reports_REG011_for_unexpected_tokens_when_context_is_known()
        {
            const string content = """
Component {
    name: "Broken"
    123
    Signal { name: "ready" }
}
""";

            ParseResult<RawQmltypesFile> result = CreateParser().ParseContent(content, @"fixtures\qmltypes\unexpected-token.qmltypes");

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics.Where(diagnostic => diagnostic.Code == DiagnosticCodes.QmltypesUnexpectedToken));
            RawQmltypesComponent component = Assert.Single(result.Value!.Components);

            Assert.Equal(3, diagnostic.Line);
            Assert.Equal(5, diagnostic.Column);
            Assert.Contains("Component", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains(component.Signals, signal => signal.Name == "ready");
        }

        [Fact]
        public void Parse_reports_REG011_for_unexpected_characters_and_recovers()
        {
            const string content = """
Component {
    name: "Recovered"
    @
    Signal { name: "stillHere" }
}
""";

            ParseResult<RawQmltypesFile> result = CreateParser().ParseContent(content, @"fixtures\qmltypes\unexpected-character.qmltypes");

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics.Where(diagnostic => diagnostic.Code == DiagnosticCodes.QmltypesUnexpectedToken));
            RawQmltypesComponent component = Assert.Single(result.Value!.Components);

            Assert.False(result.IsSuccess);
            Assert.Equal(@"fixtures\qmltypes\unexpected-character.qmltypes", diagnostic.FilePath);
            Assert.Equal(3, diagnostic.Line);
            Assert.Equal(5, diagnostic.Column);
            Assert.Contains("Unexpected character '@'", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains(component.Signals, signal => signal.Name == "stillHere");
        }

        [Fact]
        public void Parse_preserves_line_and_column_for_unterminated_block_diagnostics()
        {
            const string content = """
Component {
    name: "Broken"
""";

            ParseResult<RawQmltypesFile> result = CreateParser().ParseContent(content, @"fixtures\qmltypes\unterminated.qmltypes");

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics.Where(diagnostic => diagnostic.Code == DiagnosticCodes.QmltypesSyntaxError));
            Assert.Equal(2, diagnostic.Line);
            Assert.Equal(19, diagnostic.Column);
            Assert.Contains("Expected rbrace", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Parse_reports_REG010_for_unterminated_string_literal()
        {
            const string content = """
Component {
    name: "Broken
}
""";

            ParseResult<RawQmltypesFile> result = CreateParser().ParseContent(content, @"fixtures\qmltypes\unterminated-string.qmltypes");

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics.Where(diagnostic => diagnostic.Message == "Unterminated string literal."));
            RawQmltypesComponent component = Assert.Single(result.Value!.Components);

            Assert.False(result.IsSuccess);
            Assert.Equal(DiagnosticCodes.QmltypesSyntaxError, diagnostic.Code);
            Assert.Equal(@"fixtures\qmltypes\unterminated-string.qmltypes", diagnostic.FilePath);
            Assert.Equal(2, diagnostic.Line);
            Assert.Equal(11, diagnostic.Column);
            Assert.Equal("Broken\n}", component.Name);
        }

        [Fact]
        public void Parse_reports_REG010_for_unterminated_block_comment()
        {
            const string content = """
Component {
    name: "Broken"
    /* unfinished comment
}
""";

            ParseResult<RawQmltypesFile> result = CreateParser().ParseContent(content, @"fixtures\qmltypes\unterminated-comment.qmltypes");

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics.Where(diagnostic => diagnostic.Message == "Unterminated block comment."));
            RawQmltypesComponent component = Assert.Single(result.Value!.Components);

            Assert.False(result.IsSuccess);
            Assert.Equal(DiagnosticCodes.QmltypesSyntaxError, diagnostic.Code);
            Assert.Equal(@"fixtures\qmltypes\unterminated-comment.qmltypes", diagnostic.FilePath);
            Assert.Equal(3, diagnostic.Line);
            Assert.Equal(5, diagnostic.Column);
            Assert.Equal("Broken", component.Name);
        }

        [Fact]
        public void Parse_recovers_after_property_syntax_error_and_continues_with_later_components()
        {
            const string content = """
Component {
    name: "Broken"
    Property {
        name "oops"
        type: "QString"
    }
    Signal { name: "stillHere" }
}

Component {
    name: "Recovered"
    Property { name: "value"; type: "int" }
}
""";

            ParseResult<RawQmltypesFile> result = CreateParser().ParseContent(content, @"fixtures\qmltypes\recovery.qmltypes");

            Assert.NotNull(result.Value);
            Assert.Equal(["Broken", "Recovered"], result.Value!.Components.Select(component => component.Name).ToArray());
            Assert.Contains(result.Value.Components[0].Signals, signal => signal.Name == "stillHere");
            Assert.Contains(result.Value.Components[1].Properties, property => property.Name == "value");
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.QmltypesSyntaxError);
        }

        [Fact]
        public void Parse_missing_colon_recovery_does_not_consume_block_terminator()
        {
            const string content = """
Component {
    name: "Recovered"
    Property {
        name
    }
    Signal { name: "stillHere" }
}
""";

            ParseResult<RawQmltypesFile> result = CreateParser().ParseContent(content, @"fixtures\qmltypes\missing-colon-before-brace.qmltypes");

            RawQmltypesComponent component = Assert.Single(result.Value!.Components);

            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.QmltypesSyntaxError);
            Assert.Contains(component.Signals, signal => signal.Name == "stillHere");
        }

        [Fact]
        public void Parse_missing_colon_recovery_does_not_consume_following_child_block()
        {
            const string content = """
Component {
    name: "Recovered"
    Property {
        name Signal { name: "nestedReady" }
    }
    Method { name: "stillHere" }
}
""";

            ParseResult<RawQmltypesFile> result = CreateParser().ParseContent(content, @"fixtures\qmltypes\missing-colon-before-child.qmltypes");

            RawQmltypesComponent component = Assert.Single(result.Value!.Components);

            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.QmltypesSyntaxError);
            Assert.Contains(component.Methods, method => method.Name == "stillHere");
        }

        [Fact]
        public void Parse_ignores_import_lines_and_non_component_top_level_blocks()
        {
            const string content = """
import QtQuick 2.15

Helper {
    name: "Ignored"
}

Module {
    Component {
        name: "Visible"
    }
}
""";

            ParseResult<RawQmltypesFile> result = CreateParser().ParseContent(content, @"fixtures\qmltypes\module-wrapper.qmltypes");

            Assert.True(result.IsSuccess);
            RawQmltypesComponent component = Assert.Single(result.Value!.Components);
            Assert.Equal("Visible", component.Name);
        }

        [Fact]
        public void Parse_export_meta_object_revisions_accepts_string_and_negative_number_entries()
        {
            RawQmltypesComponent component = ParseSingleComponent(
                CreateComponentContent(
                    "name: \"QQuickVersioned\"",
                    "exportMetaObjectRevisions: [1, \"2\", -3, true]"));

            Assert.Equal([1, 2, -3], component.ExportMetaObjectRevisions.ToArray());
        }

        [Fact]
        public void Parse_string_escapes_and_unknown_escape_sequences_are_preserved()
        {
            RawQmltypesComponent component = ParseSingleComponent(
                CreateComponentContent(
                    "name: \"line\\nnext\\tquote\\\"slash\\\\tail\\x\""));

            Assert.Equal("line\nnext\tquote\"slash\\tailx", component.Name);
        }

        [Fact]
        public void Parse_reports_REG011_for_unexpected_top_level_identifier_tokens()
        {
            ParseResult<RawQmltypesFile> result = CreateParser().ParseContent(
                "DanglingIdentifier",
                @"fixtures\qmltypes\dangling-identifier.qmltypes");

            RegistryDiagnostic diagnostic = Assert.Single(result.Diagnostics.Where(diagnostic => diagnostic.Code == DiagnosticCodes.QmltypesUnexpectedToken));
            Assert.Equal(1, diagnostic.Line);
            Assert.Equal(1, diagnostic.Column);
            Assert.Contains("top-level declaration", diagnostic.Message, StringComparison.Ordinal);
            Assert.Empty(result.Value!.Components);
        }

        [Fact]
        public void Parse_reports_REG011_for_unexpected_object_property_values_and_array_items()
        {
            const string content = """
Component {
    name: "Recovered"
    exports: [;, "QtQuick/Fancy 2.0"]
}
""";

            ParseResult<RawQmltypesFile> result = CreateParser().ParseContent(content, @"fixtures\qmltypes\unexpected-object-values.qmltypes");

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("array value for property 'exports'", StringComparison.Ordinal));
        }

        private static IQmltypesParser CreateParser()
        {
            return new QmltypesParser();
        }

        private static string CreateAccessSemanticsContent(string accessSemantics)
        {
            return CreateComponentContent(
                "name: \"QQuickSemantic\"",
                $"accessSemantics: \"{accessSemantics}\"");
        }

        private static string CreateComponentContent(params string[] bodyLines)
        {
            return string.Join(
                Environment.NewLine,
                ["Component {", .. bodyLines.Select(line => $"    {line}"), "}"]);
        }

        private static QmlVersion DecodeRevision(int encodedRevision)
        {
            return new QmlVersion(encodedRevision / 256, encodedRevision % 256);
        }

        private static string GetFixturePath(string fileName)
        {
            if (Path.IsPathRooted(fileName))
            {
                throw new ArgumentException("Fixture file name must be a relative path.", nameof(fileName));
            }

            return Path.Join(AppContext.BaseDirectory, "fixtures", "qmltypes", fileName);
        }

        private static RawQmltypesComponent ParseRichComponent()
        {
            return ParseSingleComponent(CreateRichComponentContent());
        }

        private static RawQmltypesComponent ParseSingleComponent(string content)
        {
            ParseResult<RawQmltypesFile> result = CreateParser().ParseContent(content, @"fixtures\qmltypes\inline.qmltypes");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            return Assert.Single(result.Value!.Components);
        }

        private static string CreateRichComponentContent()
        {
            return """
Component {
    name: "QQuickFancy"
    accessSemantics: "reference"
    prototype: "QQuickItem"
    defaultProperty: "data"
    attachedType: "QQuickKeysAttached"
    extension: "QQuickFancyExtension"
    isSingleton: true
    isCreatable: false
    exports: ["QtQuick/Fancy 2.0", "QtQuick/Fancy 2.15"]
    exportMetaObjectRevisions: [512, 527]
    interfaces: ["QQmlParserStatus", "QQuickFancyInterface"]

    Property {
        name: "title"
        type: "QString"
        read: "title"
        write: "setTitle"
        notify: "titleChanged"
        bindable: "bindableTitle"
    }

    Property {
        name: "children"
        type: "QObject"
        isReadonly: true
        isList: true
    }

    Property {
        name: "requiredName"
        type: "QString"
        isRequired: true
    }

    Signal {
        name: "triggered"
    }

    Signal {
        name: "accepted"
        Parameter { name: "index"; type: "int" }
        Parameter { name: "label"; type: "QString" }
    }

    Method {
        name: "reset"
    }

    Method {
        name: "compute"
        type: "int"
        Parameter { name: "count"; type: "int" }
        Parameter { name: "enabled"; type: "bool" }
    }

    Enum {
        name: "State"
        values: ["Idle", "Busy"]
    }

    Enum {
        name: "Modes"
        alias: "Mode"
        isFlag: true
        values: ["Fast", "Safe"]
    }
}
""";
        }
    }
}
