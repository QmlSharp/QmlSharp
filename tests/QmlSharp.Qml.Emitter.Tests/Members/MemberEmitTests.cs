using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Members
{
    public sealed class MemberEmitTests
    {
        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_01_IdAssignment_EmitsIdBinding()
        {
            QmlDocument document = Document("Item", root => root.Id("myItem"));

            AssertMemberOutput(document, "Item {\n    id: myItem\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_02_PropertyDeclarationNoInitializer_EmitsTypeAndName()
        {
            QmlDocument document = Document("Item", root => root.PropertyDeclaration("count", "int"));

            AssertMemberOutput(document, "Item {\n    property int count\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_03_PropertyDeclarationWithInitializer_EmitsValue()
        {
            QmlDocument document = Document("Item", root => root.PropertyDeclaration("count", "int", Values.Number(0)));

            AssertMemberOutput(document, "Item {\n    property int count: 0\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_04_PropertyDeclarationDefaultModifier_EmitsDefaultBeforeProperty()
        {
            QmlDocument document = Document("Item", root => root.PropertyDeclaration("contentItem", "Item", isDefault: true));

            AssertMemberOutput(document, "Item {\n    default property Item contentItem\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_05_PropertyDeclarationRequiredModifier_EmitsRequiredBeforeProperty()
        {
            QmlDocument document = Document("Item", root => root.PropertyDeclaration("title", "string", isRequired: true));

            AssertMemberOutput(document, "Item {\n    required property string title\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_06_PropertyDeclarationReadonlyModifier_EmitsReadonlyBeforeProperty()
        {
            QmlDocument document = Document("Item", root => root.PropertyDeclaration("total", "int", isReadonly: true));

            AssertMemberOutput(document, "Item {\n    readonly property int total\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_07_PropertyDeclarationCombinedModifiers_UsesCanonicalModifierOrder()
        {
            QmlDocument document = Document(
                "Item",
                root => root.PropertyDeclaration("name", "string", isDefault: true, isRequired: true, isReadonly: true));

            AssertMemberOutput(document, "Item {\n    default required readonly property string name\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_08_PropertyDeclarationListType_EmitsGenericListType()
        {
            QmlDocument document = Document("Item", root => root.PropertyDeclaration("children", "list<Item>"));

            AssertMemberOutput(document, "Item {\n    property list<Item> children\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_09_PropertyDeclarationVarType_EmitsVarType()
        {
            QmlDocument document = Document("Item", root => root.PropertyDeclaration("data", "var"));

            AssertMemberOutput(document, "Item {\n    property var data\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_10_PropertyAlias_EmitsAliasTarget()
        {
            QmlDocument document = Document("Item", root => root.PropertyAlias("text", "label.text"));

            AssertMemberOutput(document, "Item {\n    property alias text: label.text\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_11_PropertyAliasDefaultModifier_EmitsDefaultAlias()
        {
            QmlDocument document = Document("Item", root => root.PropertyAlias("content", "innerItem", isDefault: true));

            AssertMemberOutput(document, "Item {\n    default property alias content: innerItem\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_12_SignalDeclarationNoParameters_EmitsEmptyParameterList()
        {
            QmlDocument document = Document("Item", root => root.SignalDeclaration("clicked"));

            AssertMemberOutput(document, "Item {\n    signal clicked()\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_13_SignalDeclarationWithParameters_EmitsTypedParameters()
        {
            QmlDocument document = Document(
                "Item",
                root => root.SignalDeclaration("positionChanged", new ParameterDeclaration("x", "int"), new ParameterDeclaration("y", "int")));

            AssertMemberOutput(document, "Item {\n    signal positionChanged(x: int, y: int)\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_14_SignalDeclarationSingleParameter_EmitsSingleTypedParameter()
        {
            QmlDocument document = Document("Item", root => root.SignalDeclaration("toggled", new ParameterDeclaration("checked", "bool")));

            AssertMemberOutput(document, "Item {\n    signal toggled(checked: bool)\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_15_SignalHandlerExpressionForm_EmitsRawExpression()
        {
            QmlDocument document = Document("Item", root => root.SignalHandler("onClicked", SignalHandlerForm.Expression, "console.log(\"clicked\")"));

            AssertMemberOutput(document, "Item {\n    onClicked: console.log(\"clicked\")\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_16_SignalHandlerBlockForm_EmitsIndentedBlock()
        {
            QmlDocument document = Document("Item", root => root.SignalHandler("onClicked", SignalHandlerForm.Block, "stmt1\nstmt2"));

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    onClicked: {\n"
                    + "        stmt1\n"
                    + "        stmt2\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_17_SignalHandlerArrowForm_EmitsParametersAndBlock()
        {
            QmlDocument document = Document(
                "Item",
                root => root.SignalHandler("onPressed", SignalHandlerForm.Arrow, "handle(mouseX)\naccept()", ["mouseX", "mouseY"]));

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    onPressed: (mouseX, mouseY) => {\n"
                    + "        handle(mouseX)\n"
                    + "        accept()\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_18_SignalHandlerArrowFormNoParameters_EmitsEmptyParameterList()
        {
            QmlDocument document = Document("Item", root => root.SignalHandler("onTriggered", SignalHandlerForm.Arrow, "doSomething()", []));

            AssertMemberOutput(document, "Item {\n    onTriggered: () => { doSomething() }\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_19_FunctionDeclarationNoParameters_EmitsFunctionBlock()
        {
            QmlDocument document = Document("Item", root => root.FunctionDeclaration("doSomething", "console.log(\"done\")"));

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    function doSomething() {\n"
                    + "        console.log(\"done\")\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_20_FunctionDeclarationWithParameters_EmitsTypedParametersAndReturnType()
        {
            QmlDocument document = Document(
                "Item",
                root => root.FunctionDeclaration(
                    "add",
                    "return a + b",
                    "int",
                    new ParameterDeclaration("a", "int"),
                    new ParameterDeclaration("b", "int")));

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    function add(a: int, b: int): int {\n"
                    + "        return a + b\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_21_FunctionDeclarationNoReturnType_OmitsReturnTypeSeparator()
        {
            QmlDocument document = Document("Item", root => root.FunctionDeclaration("doWork", "work()"));

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    function doWork() {\n"
                    + "        work()\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_22_FunctionDeclarationWithReturnType_EmitsReturnType()
        {
            QmlDocument document = Document("Item", root => root.FunctionDeclaration("getValue", "return \"hello\"", "string"));

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    function getValue(): string {\n"
                    + "        return \"hello\"\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_23_BlankLinesBetweenFunctionsEnabled_InsertsBlankLine()
        {
            QmlDocument document = Document(
                "Item",
                root => root
                    .FunctionDeclaration("first", "one()")
                    .FunctionDeclaration("second", "two()"));

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    function first() {\n"
                    + "        one()\n"
                    + "    }\n"
                    + "\n"
                    + "    function second() {\n"
                    + "        two()\n"
                    + "    }\n"
                    + "}\n",
                new EmitOptions { InsertBlankLinesBetweenFunctions = true });
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_24_BlankLinesBetweenFunctionsDisabled_OmitsBlankLine()
        {
            QmlDocument document = Document(
                "Item",
                root => root
                    .FunctionDeclaration("first", "one()")
                    .FunctionDeclaration("second", "two()"));

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    function first() {\n"
                    + "        one()\n"
                    + "    }\n"
                    + "    function second() {\n"
                    + "        two()\n"
                    + "    }\n"
                    + "}\n",
                new EmitOptions { InsertBlankLinesBetweenFunctions = false });
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_25_EnumDeclaration_EmitsMembersInOrder()
        {
            QmlDocument document = Document("Item", root => root.EnumDeclaration("Status", new EnumMember("Active", null), new EnumMember("Inactive", null)));

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    enum Status {\n"
                    + "        Active,\n"
                    + "        Inactive\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_26_EnumDeclarationWithExplicitValues_EmitsValues()
        {
            QmlDocument document = Document("Item", root => root.EnumDeclaration("Priority", new EnumMember("Low", 0), new EnumMember("High", 10)));

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    enum Priority {\n"
                    + "        Low = 0,\n"
                    + "        High = 10\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_27_EnumDeclarationSingleMember_EmitsNoTrailingComma()
        {
            QmlDocument document = Document("Item", root => root.EnumDeclaration("Direction", new EnumMember("Left", null)));

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    enum Direction {\n"
                    + "        Left\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_27B_EnumDeclarationInNestedObject_ThrowsInvalidOperationException()
        {
            QmlDocument document = Document(
                "Item",
                root => root.Child("QtObject", child => child.EnumDeclaration("Status", new EnumMember("Active", null))));
            IQmlEmitter emitter = new QmlEmitter();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => emitter.Emit(document));

            Assert.Equal("Enum declarations may only be emitted as members of the document root object.", exception.Message);
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_27C_EnumDeclarationInsideInlineComponentBody_ThrowsInvalidOperationException()
        {
            QmlDocument document = Document(
                "Item",
                root => root.InlineComponent("Foo", "Rectangle", foo => foo.EnumDeclaration("Status", new EnumMember("Active", null))));
            IQmlEmitter emitter = new QmlEmitter();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => emitter.Emit(document));

            Assert.Equal("Enum declarations may only be emitted as members of the document root object.", exception.Message);
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_28_InlineComponent_EmitsComponentDeclaration()
        {
            QmlDocument document = Document("Item", root => root.InlineComponent("Foo", "Rectangle", foo => foo.Binding("color", Values.String("red"))));

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    component Foo: Rectangle {\n"
                    + "        color: \"red\"\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_29_InlineComponentWithChildren_EmitsNestedComponentBody()
        {
            QmlDocument document = Document(
                "Item",
                root => root.InlineComponent("Foo", "Rectangle", foo => foo.Child("Text", text => text.Binding("text", Values.String("child")))));

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    component Foo: Rectangle {\n"
                    + "        Text {\n"
                    + "            text: \"child\"\n"
                    + "        }\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_29B_InlineComponentInNestedObject_ThrowsInvalidOperationException()
        {
            QmlDocument document = Document(
                "Item",
                root => root.Child("QtObject", child => child.InlineComponent("Foo", "Rectangle", _ => { })));
            IQmlEmitter emitter = new QmlEmitter();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => emitter.Emit(document));

            Assert.Equal("Inline components may only be emitted as members of the document root object.", exception.Message);
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_29C_InlineComponentInsideInlineComponentBody_ThrowsInvalidOperationException()
        {
            QmlDocument document = Document(
                "Item",
                root => root.InlineComponent("Foo", "Rectangle", foo => foo.InlineComponent("Bar", "QtObject", _ => { })));
            IQmlEmitter emitter = new QmlEmitter();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => emitter.Emit(document));

            Assert.Equal("Inline components may only be emitted as members of the document root object.", exception.Message);
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_30_LineCommentLeading_EmitsBeforeNextMember()
        {
            BindingNode binding = new()
            {
                PropertyName = "width",
                Value = Values.Number(100),
                LeadingComments = [new CommentNode { Text = "// this is a comment" }],
            };
            QmlDocument document = Document("Item", binding);

            AssertMemberOutput(document, "Item {\n    // this is a comment\n    width: 100\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_31_BlockCommentLeading_EmitsBeforeNextMember()
        {
            BindingNode binding = new()
            {
                PropertyName = "width",
                Value = Values.Number(100),
                LeadingComments = [new CommentNode { Text = "/* block comment */", IsBlock = true }],
            };
            QmlDocument document = Document("Item", binding);

            AssertMemberOutput(document, "Item {\n    /* block comment */\n    width: 100\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_32_TrailingComment_EmitsOnSameLine()
        {
            BindingNode binding = new()
            {
                PropertyName = "width",
                Value = Values.Number(100),
                TrailingComment = new CommentNode { Text = "// inline comment" },
            };
            QmlDocument document = Document("Item", binding);

            AssertMemberOutput(document, "Item {\n    width: 100 // inline comment\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_33_StandaloneCommentBetweenMembers_EmitsInMemberOrder()
        {
            QmlDocument document = Document(
                "Item",
                new BindingNode { PropertyName = "width", Value = Values.Number(100) },
                new CommentNode { Text = "// between" },
                new BindingNode { PropertyName = "height", Value = Values.Number(200) });

            AssertMemberOutput(document, "Item {\n    width: 100\n    // between\n    height: 200\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MM_34_CommentsDisabled_OmitsAttachedAndStandaloneComments()
        {
            BindingNode binding = new()
            {
                PropertyName = "width",
                Value = Values.Number(100),
                LeadingComments = [new CommentNode { Text = "// leading" }],
                TrailingComment = new CommentNode { Text = "// trailing" },
            };
            QmlDocument document = Document("Item", new CommentNode { Text = "// standalone" }, binding);

            AssertMemberOutput(document, "Item {\n    width: 100\n}\n", new EmitOptions { EmitComments = false });
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void MemberOrder_MixedMembers_PreservesAstOrder()
        {
            QmlDocument document = Document(
                "Item",
                new BindingNode { PropertyName = "width", Value = Values.Number(100) },
                new SignalDeclarationNode { Name = "clicked" },
                new PropertyDeclarationNode { Name = "count", TypeName = "int" },
                new FunctionDeclarationNode { Name = "run", Body = "work()" });

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    width: 100\n"
                    + "    signal clicked()\n"
                    + "    property int count\n"
                    + "    function run() {\n"
                    + "        work()\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void SemicolonRuleAlways_MembersWithOptionalSemicolons_EmitSemicolonsBeforeComments()
        {
            BindingNode binding = new()
            {
                PropertyName = "width",
                Value = Values.Number(100),
                TrailingComment = new CommentNode { Text = "// keep" },
            };
            QmlDocument document = Document(
                "Item",
                new IdAssignmentNode { Id = "root" },
                new PropertyDeclarationNode { Name = "count", TypeName = "int" },
                binding,
                new SignalDeclarationNode { Name = "clicked" });

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    id: root;\n"
                    + "    property int count;\n"
                    + "    width: 100; // keep\n"
                    + "    signal clicked();\n"
                    + "}\n",
                new EmitOptions { SemicolonRule = SemicolonRule.Always });
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void FunctionDeclarationBody_PreservesIntentionalJavaScriptText()
        {
            QmlDocument document = Document("Item", root => root.FunctionDeclaration("choose", "if (ready) {\nreturn \"yes\"\n}\nreturn \"no\""));

            AssertMemberOutput(
                document,
                "Item {\n"
                    + "    function choose() {\n"
                    + "        if (ready) {\n"
                    + "        return \"yes\"\n"
                    + "        }\n"
                    + "        return \"no\"\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Members)]
        public void UnsupportedObjectMember_ThrowsDeterministicException()
        {
            QmlDocument document = Document("Item", new UnsupportedMemberNode());
            IQmlEmitter emitter = new QmlEmitter();

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => emitter.Emit(document));

            Assert.Equal("Unsupported AST node kind 'Document' in object member emission.", exception.Message);
        }

        private static QmlDocument Document(string rootTypeName, Action<QmlObjectBuilder> configure)
        {
            return new QmlDocumentBuilder()
                .SetRootObject(rootTypeName, configure)
                .Build();
        }

        private static QmlDocument Document(string rootTypeName, params AstNode[] members)
        {
            return new QmlDocument
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = rootTypeName,
                    Members = [.. members],
                },
            };
        }

        private static void AssertMemberOutput(QmlDocument document, string expected, EmitOptions? options = null)
        {
            IQmlEmitter emitter = new QmlEmitter();

            string actual = emitter.Emit(document, options);

            Assert.Equal(expected, actual);
            LineEndingAssert.ContainsOnlyLf(actual);
        }

        private sealed record UnsupportedMemberNode : AstNode
        {
            public override NodeKind Kind => NodeKind.Document;
        }
    }
}
