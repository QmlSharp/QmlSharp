using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Basic
{
    public sealed class BasicEmitTests
    {
        private static readonly EmitOptions ExpandedObjects = new()
        {
            SingleLineEmptyObjects = false,
        };

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_01_EmptyDocument_EmitsRootObjectOnly()
        {
            QmlDocument document = EmptyDocument();

            AssertBasicOutput(document, "Item {}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_02_EmptyDocument_SingleLineEmptyObjectsTrue_EmitsSingleLineObject()
        {
            QmlDocument document = EmptyDocument();

            AssertBasicOutput(document, "Item {}\n", new EmitOptions { SingleLineEmptyObjects = true });
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_03_EmptyDocument_SingleLineEmptyObjectsFalse_EmitsExpandedObject()
        {
            QmlDocument document = EmptyDocument();

            AssertBasicOutput(document, "Item {\n}\n", ExpandedObjects);
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_04_DocumentWithSinglePragma_EmitsPragmaBeforeRootObject()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddPragma(PragmaName.Singleton)
                .SetRootObject("Item", _ => { })
                .Build();

            AssertBasicOutput(document, "pragma Singleton\n\nItem {}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_05_DocumentWithPragmaValue_EmitsPragmaValue()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddPragma(PragmaName.ComponentBehavior, "Bound")
                .SetRootObject("Item", _ => { })
                .Build();

            AssertBasicOutput(document, "pragma ComponentBehavior: Bound\n\nItem {}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_06_DocumentWithAllPragmaForms_EmitsEachPragmaInDeclarationOrder()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddPragma(PragmaName.Singleton)
                .AddPragma(PragmaName.ComponentBehavior, "Bound")
                .AddPragma(PragmaName.ListPropertyAssignBehavior, "Replace")
                .AddPragma(PragmaName.FunctionSignatureBehavior, "Enforced")
                .AddPragma(PragmaName.NativeMethodBehavior, "AcceptThisObject")
                .AddPragma(PragmaName.ValueTypeBehavior, "Addressable")
                .AddPragma(PragmaName.NativeTextRendering)
                .AddPragma(PragmaName.Translator, "App")
                .SetRootObject("Item", _ => { })
                .Build();

            AssertBasicOutput(
                document,
                "pragma Singleton\n"
                    + "pragma ComponentBehavior: Bound\n"
                    + "pragma ListPropertyAssignBehavior: Replace\n"
                    + "pragma FunctionSignatureBehavior: Enforced\n"
                    + "pragma NativeMethodBehavior: AcceptThisObject\n"
                    + "pragma ValueTypeBehavior: Addressable\n"
                    + "pragma NativeTextRendering\n"
                    + "pragma Translator: App\n"
                    + "\n"
                    + "Item {}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_07_DocumentWithSingleModuleImport_EmitsModuleImport()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddModuleImport("QtQuick", "2.15")
                .SetRootObject("Item", _ => { })
                .Build();

            AssertBasicOutput(document, "import QtQuick 2.15\n\nItem {}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_08_DocumentWithModuleImportWithoutVersion_EmitsVersionlessImport()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddModuleImport("QtQuick")
                .SetRootObject("Item", _ => { })
                .Build();

            AssertBasicOutput(document, "import QtQuick\n\nItem {}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_09_DocumentWithModuleImportQualifier_EmitsQualifiedImport()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddModuleImport("QtQuick", "2.15", "QQ")
                .SetRootObject("Item", _ => { })
                .Build();

            AssertBasicOutput(document, "import QtQuick 2.15 as QQ\n\nItem {}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_09B_DocumentWithVersionlessModuleImportQualifier_EmitsQualifiedImport()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddModuleImport("QtQuick", qualifier: "QQ")
                .SetRootObject("Item", _ => { })
                .Build();

            AssertBasicOutput(document, "import QtQuick as QQ\n\nItem {}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_10_DocumentWithDirectoryImport_EmitsPathImport()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddDirectoryImport("./components")
                .SetRootObject("Item", _ => { })
                .Build();

            AssertBasicOutput(document, "import \"./components\"\n\nItem {}\n");
        }

        [Theory]
        [InlineData("./quoted\"components", "import \"./quoted\\\"components\"\n\nItem {}\n")]
        [InlineData("..\\shared\\components", "import \"..\\\\shared\\\\components\"\n\nItem {}\n")]
        [Trait("Category", TestCategories.Basic)]
        public void EB_10B_DocumentWithPathImportEscapesQuotedPathLiteral(string path, string expected)
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddDirectoryImport(path)
                .SetRootObject("Item", _ => { })
                .Build();

            AssertBasicOutput(document, expected);
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_11_DocumentWithJavaScriptImport_EmitsQualifiedJavaScriptImport()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddJavaScriptImport("utils.js", "Utils")
                .SetRootObject("Item", _ => { })
                .Build();

            AssertBasicOutput(document, "import \"utils.js\" as Utils\n\nItem {}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_11B_DocumentWithJavaScriptImportWithoutQualifier_ThrowsInvalidOperationException()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddImport(new ImportNode
                {
                    ImportKind = ImportKind.JavaScript,
                    Path = "utils.js",
                })
                .SetRootObject("Item", _ => { })
                .Build();
            IQmlEmitter emitter = new QmlEmitter();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => emitter.Emit(document));

            Assert.Contains("JavaScript imports require a qualifier", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_11C_DocumentWithJavaScriptImportEscapesPathLiteral()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddJavaScriptImport("scripts\\utils.js", "Utils")
                .SetRootObject("Item", _ => { })
                .Build();

            AssertBasicOutput(document, "import \"scripts\\\\utils.js\" as Utils\n\nItem {}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_12_DocumentWithMixedImports_PreservesDeclarationOrder()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddModuleImport("QtQuick", "2.15")
                .AddDirectoryImport("./components")
                .AddJavaScriptImport("utils.js", "Utils")
                .SetRootObject("Item", _ => { })
                .Build();

            AssertBasicOutput(
                document,
                "import QtQuick 2.15\n"
                    + "import \"./components\"\n"
                    + "import \"utils.js\" as Utils\n"
                    + "\n"
                    + "Item {}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_13_DocumentWithPragmasAndImports_EmitsPragmasThenImportsThenRoot()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddPragma(PragmaName.Singleton)
                .AddModuleImport("QtQuick", "2.15")
                .SetRootObject("Item", _ => { })
                .Build();

            AssertBasicOutput(document, "pragma Singleton\n\nimport QtQuick 2.15\n\nItem {}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_14_BlankLineBetweenSectionsEnabled_InsertsBlankLines()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddPragma(PragmaName.Singleton)
                .AddModuleImport("QtQuick", "2.15")
                .SetRootObject("Item", _ => { })
                .Build();

            AssertBasicOutput(
                document,
                "pragma Singleton\n\nimport QtQuick 2.15\n\nItem {}\n",
                new EmitOptions { InsertBlankLinesBetweenSections = true });
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_15_BlankLineBetweenSectionsDisabled_OmitsBlankLines()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddPragma(PragmaName.Singleton)
                .AddModuleImport("QtQuick", "2.15")
                .SetRootObject("Item", _ => { })
                .Build();

            AssertBasicOutput(
                document,
                "pragma Singleton\nimport QtQuick 2.15\nItem {}\n",
                new EmitOptions { InsertBlankLinesBetweenSections = false });
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_16_ObjectWithSingleBinding_EmitsIndentedBinding()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .SetRootObject("Rectangle", root => root.Binding("width", Values.Number(100)))
                .Build();

            AssertBasicOutput(document, "Rectangle {\n    width: 100\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_17_ObjectWithMultipleBindings_EmitsEachBindingInOrder()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .SetRootObject(
                    "Rectangle",
                    root => root
                        .Binding("width", Values.Number(100))
                        .Binding("height", Values.Number(200))
                        .Binding("visible", Values.Boolean(true))
                        .Binding("text", Values.String("ready"))
                        .Binding("model", Values.Null()))
                .Build();

            AssertBasicOutput(
                document,
                "Rectangle {\n"
                    + "    width: 100\n"
                    + "    height: 200\n"
                    + "    visible: true\n"
                    + "    text: \"ready\"\n"
                    + "    model: null\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_18_NestedObjectsTwoLevels_EmitsIndentedChildObject()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .SetRootObject(
                    "Item",
                    root => root.Child("Rectangle", child => child.Binding("width", Values.Number(100))))
                .Build();

            AssertBasicOutput(
                document,
                "Item {\n"
                    + "    Rectangle {\n"
                    + "        width: 100\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_19_DeeplyNestedObjectsFourLevels_EmitsIncreasingIndentation()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .SetRootObject(
                    "Window",
                    root => root.Child(
                        "Item",
                        item => item.Child(
                            "Rectangle",
                            rectangle => rectangle.Child(
                                "Text",
                                text => text.Binding("text", Values.String("deep"))))))
                .Build();

            AssertBasicOutput(
                document,
                "Window {\n"
                    + "    Item {\n"
                    + "        Rectangle {\n"
                    + "            Text {\n"
                    + "                text: \"deep\"\n"
                    + "            }\n"
                    + "        }\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_20_BlankLinesBetweenSiblingObjectsEnabled_InsertsBlankLine()
        {
            QmlDocument document = SiblingObjectDocument();

            AssertBasicOutput(
                document,
                "Item {\n"
                    + "    Text {}\n"
                    + "\n"
                    + "    Rectangle {}\n"
                    + "}\n",
                new EmitOptions { InsertBlankLinesBetweenObjects = true });
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_21_BlankLinesBetweenSiblingObjectsDisabled_OmitsBlankLine()
        {
            QmlDocument document = SiblingObjectDocument();

            AssertBasicOutput(
                document,
                "Item {\n"
                    + "    Text {}\n"
                    + "    Rectangle {}\n"
                    + "}\n",
                new EmitOptions { InsertBlankLinesBetweenObjects = false });
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_22_GeneratedHeaderEnabled_PrependsDefaultHeader()
        {
            QmlDocument document = EmptyDocument();

            AssertBasicOutput(document, "// Generated by QmlSharp\n\nItem {}\n", new EmitOptions { EmitGeneratedHeader = true });
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_23_GeneratedHeaderWithCustomText_PrependsCustomHeader()
        {
            QmlDocument document = EmptyDocument();

            AssertBasicOutput(
                document,
                "// Custom header text\n\nItem {}\n",
                new EmitOptions { EmitGeneratedHeader = true, GeneratedHeaderText = "Custom header text" });
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_24_TrailingNewlineEnabled_OutputEndsWithNewline()
        {
            QmlDocument document = EmptyDocument();

            AssertBasicOutput(document, "Item {}\n", new EmitOptions { TrailingNewline = true });
        }

        [Fact]
        [Trait("Category", TestCategories.Basic)]
        public void EB_25_TrailingNewlineDisabled_OutputDoesNotEndWithNewline()
        {
            QmlDocument document = EmptyDocument();

            AssertBasicOutput(document, "Item {}", new EmitOptions { TrailingNewline = false });
        }

        private static QmlDocument EmptyDocument()
        {
            return new QmlDocumentBuilder()
                .SetRootObject("Item", _ => { })
                .Build();
        }

        private static QmlDocument SiblingObjectDocument()
        {
            return new QmlDocumentBuilder()
                .SetRootObject(
                    "Item",
                    root => root
                        .Child("Text")
                        .Child("Rectangle"))
                .Build();
        }

        private static void AssertBasicOutput(QmlDocument document, string expected, EmitOptions? options = null)
        {
            IQmlEmitter emitter = new QmlEmitter();

            string actual = emitter.Emit(document, options);

            Assert.Equal(expected, actual);
            AssertConfiguredLineEndings(actual, options);
        }

        private static void AssertConfiguredLineEndings(string output, EmitOptions? options)
        {
            NewlineStyle newline = options?.Newline ?? NewlineStyle.Lf;
            if (newline == NewlineStyle.CrLf)
            {
                LineEndingAssert.ContainsOnlyCrLf(output);
                return;
            }

            LineEndingAssert.ContainsOnlyLf(output);
        }
    }
}
