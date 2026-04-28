using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Options
{
    public sealed class EmitOptionsBehaviorTests
    {
        [Fact]
        [Trait("Category", TestCategories.Options)]
        public void OPT_01_IndentStyleTabs_EmissionUsesTabIndentation()
        {
            string output = Emit(MemberDocument(), new EmitOptions { IndentStyle = IndentStyle.Tabs });

            Assert.Equal("Item {\n\twidth: 100\n}\n", output);
        }

        [Theory]
        [InlineData(2, "Item {\n  width: 100\n}\n")]
        [InlineData(8, "Item {\n        width: 100\n}\n")]
        [Trait("Category", TestCategories.Options)]
        public void OPT_02_OPT_03_IndentSize_EmissionUsesConfiguredSpaceCount(int indentSize, string expected)
        {
            string output = Emit(MemberDocument(), new EmitOptions { IndentSize = indentSize });

            Assert.Equal(expected, output);
        }

        [Fact]
        [Trait("Category", TestCategories.Options)]
        public void OPT_04_NewlineCrLf_EmissionUsesOnlyCrLf()
        {
            string output = Emit(MemberDocument(), new EmitOptions { Newline = NewlineStyle.CrLf });

            Assert.Equal("Item {\r\n    width: 100\r\n}\r\n", output);
            LineEndingAssert.ContainsOnlyCrLf(output);
        }

        [Fact]
        [Trait("Category", TestCategories.Options)]
        public void OPT_05_NewlineLf_EmissionUsesOnlyLf()
        {
            string output = Emit(MemberDocument(), new EmitOptions { Newline = NewlineStyle.Lf });

            Assert.Equal("Item {\n    width: 100\n}\n", output);
            LineEndingAssert.ContainsOnlyLf(output);
        }

        [Theory]
        [InlineData(QuoteStyle.Single, "Text {\n    text: 'hello'\n}\n")]
        [InlineData(QuoteStyle.Double, "Text {\n    text: \"hello\"\n}\n")]
        [InlineData(QuoteStyle.Preserve, "Text {\n    text: \"hello\"\n}\n")]
        [Trait("Category", TestCategories.Options)]
        public void OPT_06_OPT_07_OPT_08_QuoteStyle_EmissionUsesConfiguredQuotePolicy(QuoteStyle quoteStyle, string expected)
        {
            string output = Emit(TextDocument(), new EmitOptions { QuoteStyle = quoteStyle });

            Assert.Equal(expected, output);
        }

        [Fact]
        [Trait("Category", TestCategories.Options)]
        public void OPT_09_EmitCommentsFalse_OmitsCommentsAndKeepsCanonicalBlankLinesBetweenRemainingObjects()
        {
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members =
                    [
                        new ObjectDefinitionNode { TypeName = "Text" },
                        new CommentNode { Text = "// omitted" },
                        new ObjectDefinitionNode { TypeName = "Rectangle" },
                    ],
                },
            };

            string output = Emit(document, new EmitOptions { EmitComments = false });

            Assert.Equal("Item {\n    Text {}\n\n    Rectangle {}\n}\n", output);
        }

        [Fact]
        [Trait("Category", TestCategories.Options)]
        public void OPT_10_EmitGeneratedHeader_EmissionPrependsHeader()
        {
            string output = Emit(MemberDocument(), new EmitOptions { EmitGeneratedHeader = true, GeneratedHeaderText = "Generated test" });

            Assert.Equal("// Generated test\n\nItem {\n    width: 100\n}\n", output);
        }

        [Fact]
        [Trait("Category", TestCategories.Options)]
        public void OPT_11_TrailingNewlineFalse_EmissionOmitsFinalNewlineOnly()
        {
            string output = Emit(MemberDocument(), new EmitOptions { TrailingNewline = false });

            Assert.Equal("Item {\n    width: 100\n}", output);
        }

        [Fact]
        [Trait("Category", TestCategories.Options)]
        public void OPT_12_NormalizeTrue_EmissionReordersMembersByCategory()
        {
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members =
                    [
                        new BindingNode { PropertyName = "width", Value = Values.Number(100) },
                        new IdAssignmentNode { Id = "root" },
                    ],
                },
            };

            string output = Emit(document, new EmitOptions { Normalize = true });

            Assert.Equal("Item {\n    id: root\n    width: 100\n}\n", output);
        }

        [Fact]
        [Trait("Category", TestCategories.Options)]
        public void OPT_13_SortImportsTrue_EmissionSortsImportsOrdinally()
        {
            QmlDocument document = new()
            {
                Imports =
                [
                    new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtQuick.Controls", Version = "6.0" },
                    new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtCore" },
                ],
                RootObject = new ObjectDefinitionNode { TypeName = "Item" },
            };

            string output = Emit(document, new EmitOptions { SortImports = true });

            Assert.Equal("import QtCore\nimport QtQuick.Controls 6.0\n\nItem {}\n", output);
        }

        [Theory]
        [InlineData(SemicolonRule.Always, "Item {\n    width: 100;\n}\n")]
        [InlineData(SemicolonRule.Essential, "Item {\n    width: 100\n}\n")]
        [InlineData(SemicolonRule.Omit, "Item {\n    width: 100\n}\n")]
        [Trait("Category", TestCategories.Options)]
        public void OPT_14_OPT_15_OPT_16_SemicolonRule_EmissionUsesConfiguredSemicolonPolicy(SemicolonRule rule, string expected)
        {
            string output = Emit(MemberDocument(), new EmitOptions { SemicolonRule = rule });

            Assert.Equal(expected, output);
        }

        private static QmlDocument MemberDocument()
        {
            return new QmlDocument
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members =
                    [
                        new BindingNode { PropertyName = "width", Value = Values.Number(100) },
                    ],
                },
            };
        }

        private static QmlDocument TextDocument()
        {
            return new QmlDocument
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Text",
                    Members =
                    [
                        new BindingNode { PropertyName = "text", Value = Values.String("hello") },
                    ],
                },
            };
        }

        private static string Emit(QmlDocument document, EmitOptions options)
        {
            IQmlEmitter emitter = new QmlEmitter();

            return emitter.Emit(document, options);
        }
    }
}
