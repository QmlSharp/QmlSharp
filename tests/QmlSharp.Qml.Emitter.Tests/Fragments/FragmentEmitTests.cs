using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Fragments
{
    public sealed class FragmentEmitTests
    {
        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void FE_01_BindingFragment_EmitsSingleBindingLine()
        {
            BindingNode binding = new()
            {
                PropertyName = "width",
                Value = Values.Number(100),
            };

            string actual = EmitNode(binding);

            Assert.Equal("width: 100", actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void FE_02_ObjectFragment_EmitsObjectWithoutDocumentWrapper()
        {
            ObjectDefinitionNode obj = Object("Rectangle", root => root.Binding("width", Values.Number(100)));

            string actual = EmitNode(obj);

            Assert.Equal("Rectangle {\n    width: 100\n}", actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void FE_03_ImportFragment_EmitsSingleImportLine()
        {
            ImportNode import = new()
            {
                ImportKind = ImportKind.Module,
                ModuleUri = "QtQuick",
                Version = "2.15",
            };

            string actual = EmitNode(import);

            Assert.Equal("import QtQuick 2.15", actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void FE_03_ImportFragment_PreservesTrailingComment()
        {
            ImportNode import = new()
            {
                ImportKind = ImportKind.Module,
                ModuleUri = "QtQuick",
                TrailingComment = new CommentNode { Text = "// keep import reason" },
            };

            string preserved = EmitNode(import);
            string omitted = EmitNode(import, new FragmentEmitOptions { PreserveComments = false });

            Assert.Equal("import QtQuick // keep import reason", preserved);
            Assert.Equal("import QtQuick", omitted);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void FE_04_PragmaFragment_EmitsSinglePragmaLine()
        {
            PragmaNode pragma = new()
            {
                Name = PragmaName.Singleton,
            };

            string actual = EmitNode(pragma);

            Assert.Equal("pragma Singleton", actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void FE_04_PragmaFragment_PreservesTrailingComment()
        {
            PragmaNode pragma = new()
            {
                Name = PragmaName.Singleton,
                TrailingComment = new CommentNode { Text = "// singleton type" },
            };

            string actual = EmitNode(pragma);

            Assert.Equal("pragma Singleton // singleton type", actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void FE_05_IndentLevelTwo_StartsAtConfiguredBaseIndent()
        {
            BindingNode binding = new()
            {
                PropertyName = "width",
                Value = Values.Number(100),
            };

            string actual = EmitNode(binding, new FragmentEmitOptions { IndentLevel = 2 });

            Assert.Equal("        width: 100", actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void FE_06_IndentLevelZero_StartsWithoutBaseIndent()
        {
            ObjectDefinitionNode obj = Object("Item", root => root.Binding("width", Values.Number(100)));

            string actual = EmitNode(obj, new FragmentEmitOptions { IndentLevel = 0 });

            Assert.StartsWith("Item {", actual, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void FE_07_CustomEmitOptions_AreInheritedByFragment()
        {
            BindingNode binding = new()
            {
                PropertyName = "text",
                Value = Values.String("hello"),
            };
            FragmentEmitOptions options = new()
            {
                Options = new EmitOptions
                {
                    IndentSize = 2,
                    QuoteStyle = QuoteStyle.Single,
                    SemicolonRule = SemicolonRule.Always,
                },
                IndentLevel = 1,
            };

            string actual = EmitNode(binding, options);

            Assert.Equal("  text: 'hello';", actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void Fragment_SignalHandler_EmitsSingleMemberFragment()
        {
            SignalHandlerNode handler = new()
            {
                HandlerName = "onClicked",
                Form = SignalHandlerForm.Expression,
                Code = "activate()",
            };

            string actual = EmitNode(handler);

            Assert.Equal("onClicked: activate()", actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void Fragment_GroupedBinding_EmitsNestedGroup()
        {
            GroupedBindingNode grouped = new()
            {
                GroupName = "font",
                Bindings =
                [
                    new BindingNode { PropertyName = "pixelSize", Value = Values.Number(14) },
                    new BindingNode { PropertyName = "bold", Value = Values.Boolean(true) },
                ],
            };

            string actual = EmitNode(grouped);

            Assert.Equal("font {\n    pixelSize: 14\n    bold: true\n}", actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void Fragment_AttachedBinding_EmitsAttachedPropertyLines()
        {
            AttachedBindingNode attached = new()
            {
                AttachedTypeName = "Layout",
                Bindings =
                [
                    new BindingNode { PropertyName = "fillWidth", Value = Values.Boolean(true) },
                    new BindingNode { PropertyName = "margins", Value = Values.Number(10) },
                ],
            };

            string actual = EmitNode(attached);

            Assert.Equal("Layout.fillWidth: true\nLayout.margins: 10", actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void Fragment_ArrayBinding_EmitsArrayBinding()
        {
            ArrayBindingNode array = new()
            {
                PropertyName = "states",
                Elements =
                [
                    Values.Object(Object("State", state => state.Binding("name", Values.String("active")))),
                    Values.Object(Object("State", state => state.Binding("name", Values.String("inactive")))),
                ],
            };

            string actual = EmitNode(array);

            Assert.Equal(
                "states: [\n"
                    + "    State { name: \"active\" },\n"
                    + "    State { name: \"inactive\" }\n"
                    + "]",
                actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void Fragment_BindingValue_EmitsRawValueWithoutPropertyName()
        {
            BindingValue value = Values.Array(Values.Number(1), Values.String("two"));

            string actual = EmitValue(value);

            Assert.Equal("[1, \"two\"]", actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void Fragment_MultilineBindingValue_EmitsValueWithBaseIndent()
        {
            BindingValue value = Values.Block("first()\nsecond()");

            string actual = EmitValue(value, new FragmentEmitOptions { IndentLevel = 1 });

            Assert.Equal("    {\n        first()\n        second()\n    }", actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void Fragment_Comment_RespectsPreserveCommentsOverride()
        {
            CommentNode comment = new()
            {
                Text = "// fragment comment",
            };

            string preserved = EmitNode(comment);
            string omitted = EmitNode(comment, new FragmentEmitOptions { PreserveComments = false });

            Assert.Equal("// fragment comment", preserved);
            Assert.Equal(string.Empty, omitted);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void Fragment_TrailingNewlineOption_PreservesFinalNewline()
        {
            BindingNode binding = new()
            {
                PropertyName = "width",
                Value = Values.Number(100),
            };

            string actual = EmitNode(binding, new FragmentEmitOptions { IncludeTrailingNewline = true });

            Assert.Equal("width: 100\n", actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void Fragment_DisallowDocumentOnlyConstructs_RejectsImportWithoutPartialOutput()
        {
            ImportNode import = new()
            {
                ImportKind = ImportKind.Module,
                ModuleUri = "QtQuick",
            };
            IQmlEmitter emitter = new QmlEmitter();

            NotSupportedException exception = Assert.Throws<NotSupportedException>(
                () => emitter.EmitFragment(import, new FragmentEmitOptions { AllowDocumentOnlyConstructs = false }));

            Assert.Equal("Unsupported AST node kind 'Import' in fragment emission when document-only constructs are disabled.", exception.Message);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void Fragment_DocumentFragment_EmitsCompleteDocumentByDefault()
        {
            QmlDocument document = new()
            {
                Imports =
                [
                    new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtQuick" },
                ],
                RootObject = new ObjectDefinitionNode { TypeName = "Item" },
            };

            string actual = EmitNode(document);

            Assert.Equal("import QtQuick\n\nItem {}", actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void Fragment_UnsupportedNode_ThrowsDeterministicExceptionNamingKind()
        {
            IQmlEmitter emitter = new QmlEmitter();

            NotSupportedException exception = Assert.Throws<NotSupportedException>(
                () => emitter.EmitFragment(new UnsupportedFragmentNode()));

            Assert.Equal("Unsupported AST node kind 'Document' in fragment emission.", exception.Message);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void Fragment_ObjectSubtree_AgreesWithFullDocumentRootObject()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .SetRootObject("Rectangle", root => root.Binding("width", Values.Number(100)))
                .Build();
            IQmlEmitter emitter = new QmlEmitter();

            string fullDocument = emitter.Emit(document, new EmitOptions { TrailingNewline = false });
            string fragment = emitter.EmitFragment(document.RootObject);

            Assert.Equal(fullDocument, fragment);
        }

        [Fact]
        [Trait("Category", TestCategories.Fragments)]
        public void Fragment_NegativeIndent_ThrowsArgumentOutOfRangeException()
        {
            IQmlEmitter emitter = new QmlEmitter();

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => emitter.EmitFragment(new BindingNode { PropertyName = "width", Value = Values.Number(100) }, new FragmentEmitOptions { IndentLevel = -1 }));

            Assert.Equal("options", exception.ParamName);
        }

        private static string EmitNode(AstNode node, FragmentEmitOptions? options = null)
        {
            IQmlEmitter emitter = new QmlEmitter();
            string actual = emitter.EmitFragment(node, options);

            LineEndingAssert.ContainsOnlyLf(actual);

            return actual;
        }

        private static string EmitValue(BindingValue value, FragmentEmitOptions? options = null)
        {
            IQmlEmitter emitter = new QmlEmitter();
            string actual = emitter.EmitFragment(value, options);

            LineEndingAssert.ContainsOnlyLf(actual);

            return actual;
        }

        private static ObjectDefinitionNode Object(string typeName, Action<QmlObjectBuilder> configure)
        {
            QmlObjectBuilder builder = new(typeName);
            configure(builder);

            return builder.Build();
        }

        private sealed record UnsupportedFragmentNode : AstNode
        {
            public override NodeKind Kind => NodeKind.Document;
        }
    }
}
