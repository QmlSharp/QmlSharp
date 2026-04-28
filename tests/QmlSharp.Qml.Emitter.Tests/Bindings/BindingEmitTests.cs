using System.Globalization;
using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Bindings
{
    public sealed class BindingEmitTests
    {
        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_01_NumberLiteralInteger_EmitsWithoutDecimalPoint()
        {
            QmlDocument document = Document("Rectangle", root => root.Binding("width", Values.Number(42)));

            AssertBindingOutput(document, "Rectangle {\n    width: 42\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_02_NumberLiteralDecimal_UsesInvariantDecimalSeparator()
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
                QmlDocument document = Document("Rectangle", root => root.Binding("opacity", Values.Number(3.14)));

                AssertBindingOutput(document, "Rectangle {\n    opacity: 3.14\n}\n");
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_03_NumberLiteralNegative_EmitsMinusSign()
        {
            QmlDocument document = Document("Item", root => root.Binding("x", Values.Number(-1)));

            AssertBindingOutput(document, "Item {\n    x: -1\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_04_NumberLiteralZero_EmitsZero()
        {
            QmlDocument document = Document("Item", root => root.Binding("width", Values.Number(0)));

            AssertBindingOutput(document, "Item {\n    width: 0\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_05_StringLiteralDoubleQuotes_EmitsQuotedString()
        {
            QmlDocument document = Document("Text", root => root.Binding("text", Values.String("hello")));

            AssertBindingOutput(document, "Text {\n    text: \"hello\"\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_06_StringLiteralSingleQuotes_UsesSingleQuoteOption()
        {
            QmlDocument document = Document("Text", root => root.Binding("text", Values.String("hello")));

            AssertBindingOutput(document, "Text {\n    text: 'hello'\n}\n", new EmitOptions { QuoteStyle = QuoteStyle.Single });
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_07_StringLiteralPreserveQuotes_FallsBackToDoubleWhenAstHasNoQuoteMetadata()
        {
            QmlDocument document = Document("Text", root => root.Binding("text", Values.String("hello")));

            AssertBindingOutput(document, "Text {\n    text: \"hello\"\n}\n", new EmitOptions { QuoteStyle = QuoteStyle.Preserve });
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_08_StringLiteralEscapeCharacters_EscapesQuotesBackslashesAndControlCharacters()
        {
            QmlDocument document = Document("Text", root => root.Binding("text", Values.String("line1\nline2 \"quoted\" \\ path \u0001")));

            AssertBindingOutput(document, "Text {\n    text: \"line1\\nline2 \\\"quoted\\\" \\\\ path \\u0001\"\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_09_StringLiteralEmptyString_EmitsEmptyQuotes()
        {
            QmlDocument document = Document("Text", root => root.Binding("text", Values.String(string.Empty)));

            AssertBindingOutput(document, "Text {\n    text: \"\"\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_10_BooleanLiteralTrue_EmitsTrueKeyword()
        {
            QmlDocument document = Document("Item", root => root.Binding("visible", Values.Boolean(true)));

            AssertBindingOutput(document, "Item {\n    visible: true\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_11_BooleanLiteralFalse_EmitsFalseKeyword()
        {
            QmlDocument document = Document("Item", root => root.Binding("visible", Values.Boolean(false)));

            AssertBindingOutput(document, "Item {\n    visible: false\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_12_NullLiteral_EmitsNullKeyword()
        {
            QmlDocument document = Document("Item", root => root.Binding("model", Values.Null()));

            AssertBindingOutput(document, "Item {\n    model: null\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_13_EnumReference_EmitsTypeAndMemberWithoutQuotes()
        {
            QmlDocument document = Document("Image", root => root.Binding("fillMode", Values.Enum("Image", "Stretch")));

            AssertBindingOutput(document, "Image {\n    fillMode: Image.Stretch\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_14_ScriptExpressionSimple_EmitsRawExpressionWithoutQuotes()
        {
            QmlDocument document = Document("Item", root => root.Binding("width", Values.Expression("parent.width * 0.5")));

            AssertBindingOutput(document, "Item {\n    width: parent.width * 0.5\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_14B_ScriptExpressionMultiline_IndentsContinuationLines()
        {
            QmlDocument document = Document("Item", root => root.Binding("width", Values.Expression("parent.width\n+ 10")));

            AssertBindingOutput(document, "Item {\n    width: parent.width\n        + 10\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_15_ScriptExpressionWithTernary_DoesNotQuoteInnerStringLiterals()
        {
            QmlDocument document = Document("Rectangle", root => root.Binding("color", Values.Expression("active ? \"red\" : \"gray\"")));

            AssertBindingOutput(document, "Rectangle {\n    color: active ? \"red\" : \"gray\"\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_16_ScriptBlockSingleStatement_EmitsInlineBlock()
        {
            QmlDocument document = Document("Item", root => root.Binding("onCompleted", Values.Block("console.log(\"done\")")));

            AssertBindingOutput(document, "Item {\n    onCompleted: { console.log(\"done\") }\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_16B_ScriptBlockWithExistingInlineBraces_DoesNotDoubleWrap()
        {
            QmlDocument document = Document("Item", root => root.Binding("onCompleted", Values.Block("{ console.log(\"done\") }")));

            AssertBindingOutput(document, "Item {\n    onCompleted: { console.log(\"done\") }\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_17_ScriptBlockMultiline_EmitsIndentedBlock()
        {
            QmlDocument document = Document("Item", root => root.Binding("onCompleted", Values.Block("console.log(\"done\")\nready = true")));

            AssertBindingOutput(
                document,
                "Item {\n"
                    + "    onCompleted: {\n"
                    + "        console.log(\"done\")\n"
                    + "        ready = true\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_17B_ScriptBlockWithExistingMultilineBraces_DoesNotDoubleWrap()
        {
            QmlDocument document = Document("Item", root => root.Binding("onCompleted", Values.Block("{\nconsole.log(\"done\")\nready = true\n}")));

            AssertBindingOutput(
                document,
                "Item {\n"
                    + "    onCompleted: {\n"
                    + "        console.log(\"done\")\n"
                    + "        ready = true\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_18_ObjectValueInline_EmitsSingleBindingObjectOnOneLine()
        {
            QmlDocument document = Document(
                "Text",
                root => root.Binding("font", Values.Object("Font", font => font.Binding("pixelSize", Values.Number(14)))));

            AssertBindingOutput(document, "Text {\n    font: Font { pixelSize: 14 }\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_19_ObjectValueWithMultipleMembers_EmitsNestedObjectBlock()
        {
            QmlDocument document = Document(
                "Text",
                root => root.Binding(
                    "font",
                    Values.Object(
                        "Font",
                        font => font
                            .Binding("pixelSize", Values.Number(14))
                            .Binding("bold", Values.Boolean(true)))));

            AssertBindingOutput(
                document,
                "Text {\n"
                    + "    font: Font {\n"
                    + "        pixelSize: 14\n"
                    + "        bold: true\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_20_ArrayValueWithObjects_EmitsObjectElementsWithoutGenericStringFallback()
        {
            QmlDocument document = Document(
                "Item",
                root => root.Binding(
                    "states",
                    Values.Array(
                        Values.Object("State", state => state.Binding("name", Values.String("active"))),
                        Values.Object("State", state => state.Binding("name", Values.String("inactive"))))));

            AssertBindingOutput(
                document,
                "Item {\n"
                    + "    states: [State { name: \"active\" }, State { name: \"inactive\" }]\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_20B_ArrayValueWithPrimitiveObjectAndExpression_EmitsEachElementByKind()
        {
            QmlDocument document = Document(
                "Item",
                root => root.Binding(
                    "values",
                    Values.Array(
                        Values.Number(1),
                        Values.Expression("parent.width"),
                        Values.Object("QtObject", obj => obj.Binding("objectName", Values.String("nested"))))));

            AssertBindingOutput(
                document,
                "Item {\n"
                    + "    values: [1, parent.width, QtObject { objectName: \"nested\" }]\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_21_ArrayValueEmpty_EmitsEmptyArray()
        {
            QmlDocument document = Document("Item", root => root.Binding("items", Values.Array()));

            AssertBindingOutput(document, "Item {\n    items: []\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_22_GroupedBindingMultipleProperties_EmitsNestedGroupBlock()
        {
            QmlDocument document = Document(
                "Text",
                root => root.GroupedBinding(
                    "font",
                    font => font
                        .Binding("pixelSize", Values.Number(14))
                        .Binding("bold", Values.Boolean(true))));

            AssertBindingOutput(
                document,
                "Text {\n"
                    + "    font {\n"
                    + "        pixelSize: 14\n"
                    + "        bold: true\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_23_GroupedBindingSingleProperty_EmitsNestedGroupBlock()
        {
            QmlDocument document = Document(
                "Text",
                root => root.GroupedBinding("font", font => font.Binding("pixelSize", Values.Number(14))));

            AssertBindingOutput(
                document,
                "Text {\n"
                    + "    font {\n"
                    + "        pixelSize: 14\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_24_AttachedBindingSingleProperty_EmitsDottedPropertyBinding()
        {
            QmlDocument document = Document(
                "Item",
                root => root.AttachedBinding("Layout", layout => layout.Binding("fillWidth", Values.Boolean(true))));

            AssertBindingOutput(document, "Item {\n    Layout.fillWidth: true\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_25_AttachedBindingMultipleProperties_EmitsDottedPropertyBindings()
        {
            QmlDocument document = Document(
                "Item",
                root => root.AttachedBinding(
                    "Layout",
                    layout => layout
                        .Binding("fillWidth", Values.Boolean(true))
                        .Binding("margins", Values.Number(10))));

            AssertBindingOutput(
                document,
                "Item {\n"
                    + "    Layout.fillWidth: true\n"
                    + "    Layout.margins: 10\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_25B_AttachedBindingWithoutProperties_EmitsNoInvalidBlock()
        {
            QmlDocument document = Document("Item", root => root.AttachedBinding("Layout", _ => { }));

            AssertBindingOutput(document, "Item {\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void AttachedBindingComments_PreserveChildAndAttachedTrailingPlacement()
        {
            BindingNode fillWidth = new()
            {
                PropertyName = "fillWidth",
                Value = Values.Boolean(true),
                LeadingComments = [new CommentNode { Text = "// Fill width binding" }],
                TrailingComment = new CommentNode { Text = "// child trailing" },
            };
            BindingNode margins = new()
            {
                PropertyName = "margins",
                Value = Values.Number(10),
                TrailingComment = new CommentNode { Text = "// last child trailing" },
            };
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members =
                    [
                        new AttachedBindingNode
                        {
                            AttachedTypeName = "Layout",
                            Bindings = [fillWidth, margins],
                            TrailingComment = new CommentNode { Text = "// attached trailing" },
                        },
                    ],
                },
            };

            AssertBindingOutput(
                document,
                "Item {\n"
                    + "    // Fill width binding\n"
                    + "    Layout.fillWidth: true // child trailing\n"
                    + "    Layout.margins: 10 // last child trailing // attached trailing\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_26_ArrayBinding_EmitsMultilineArrayElements()
        {
            QmlDocument document = Document(
                "Item",
                root => root.ArrayBinding(
                    "states",
                    Values.Object("State", state => state.Binding("name", Values.String("active"))),
                    Values.Object("State", state => state.Binding("name", Values.String("inactive")))));

            AssertBindingOutput(
                document,
                "Item {\n"
                    + "    states: [\n"
                    + "        State { name: \"active\" },\n"
                    + "        State { name: \"inactive\" }\n"
                    + "    ]\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void ArrayBindingTrailingComment_EmitsOnClosingBracketLine()
        {
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members =
                    [
                        new ArrayBindingNode
                        {
                            PropertyName = "states",
                            Elements = [Values.String("active"), Values.String("inactive")],
                            TrailingComment = new CommentNode { Text = "// states trailing" },
                        },
                    ],
                },
            };

            AssertBindingOutput(
                document,
                "Item {\n"
                    + "    states: [\n"
                    + "        \"active\",\n"
                    + "        \"inactive\"\n"
                    + "    ] // states trailing\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Bindings)]
        public void BV_27_BehaviorOnBinding_EmitsBehaviorBlockWithAnimationObject()
        {
            QmlDocument document = Document(
                "Item",
                root => root.BehaviorOn("x", "NumberAnimation", animation => animation.Binding("duration", Values.Number(200))));

            AssertBindingOutput(
                document,
                "Item {\n"
                    + "    Behavior on x {\n"
                    + "        NumberAnimation {\n"
                    + "            duration: 200\n"
                    + "        }\n"
                    + "    }\n"
                    + "}\n");
        }

        private static QmlDocument Document(string rootTypeName, Action<QmlObjectBuilder> configure)
        {
            return new QmlDocumentBuilder()
                .SetRootObject(rootTypeName, configure)
                .Build();
        }

        private static void AssertBindingOutput(QmlDocument document, string expected, EmitOptions? options = null)
        {
            IQmlEmitter emitter = new QmlEmitter();

            string actual = emitter.Emit(document, options);

            Assert.Equal(expected, actual);
            LineEndingAssert.ContainsOnlyLf(actual);
        }
    }
}
