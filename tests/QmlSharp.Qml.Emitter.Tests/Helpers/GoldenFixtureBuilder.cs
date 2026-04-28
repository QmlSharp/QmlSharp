using QmlSharp.Qml.Ast.Builders;

namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    internal sealed record GoldenFixture(string FileName, QmlDocument Document, EmitOptions Options, string ParityNote);

    internal static class GoldenFixtureBuilder
    {
        public static ImmutableArray<string> ExpectedFileNames { get; } =
        [
            "minimal.qml",
            "full-syntax.qml",
            "nested.qml",
            "all-bindings.qml",
            "all-members.qml",
            "comments.qml",
            "normalized.qml",
        ];

        public static ImmutableArray<GoldenFixture> All()
        {
            return
            [
                Build("minimal.qml"),
                Build("full-syntax.qml"),
                Build("nested.qml"),
                Build("all-bindings.qml"),
                Build("all-members.qml"),
                Build("comments.qml"),
                Build("normalized.qml"),
            ];
        }

        public static GoldenFixture Build(string fileName)
        {
            return fileName switch
            {
                "minimal.qml" => new GoldenFixture(
                    fileName,
                    MinimalDocument(),
                    new EmitOptions(),
                    "QmlTS minimal.qml was root-only (`Item { }`). QmlSharp keeps the Step 03.10 test-spec import baseline."),
                "full-syntax.qml" => new GoldenFixture(
                    fileName,
                    FullSyntaxDocument(),
                    new EmitOptions(),
                    "Preserves QmlTS full-syntax fixture intent with C# AST names and QmlSharp multiline function formatting."),
                "nested.qml" => new GoldenFixture(
                    fileName,
                    NestedDocument(),
                    new EmitOptions(),
                    "Preserves QmlTS nested-object depth intent with QmlSharp brace and indentation policy."),
                "all-bindings.qml" => new GoldenFixture(
                    fileName,
                    AllBindingsDocument(),
                    new EmitOptions(),
                    "Preserves QmlTS all-binding-kind intent and adds explicit object-value coverage from the QmlSharp test spec."),
                "all-members.qml" => new GoldenFixture(
                    fileName,
                    AllMembersDocument(),
                    new EmitOptions(),
                    "Preserves QmlTS all-member-kind intent and includes all three QmlSharp signal handler forms."),
                "comments.qml" => new GoldenFixture(
                    fileName,
                    CommentsDocument(),
                    new EmitOptions(),
                    "Preserves QmlTS comment intent and adds document/root attached comments supported by the C# AST."),
                "normalized.qml" => new GoldenFixture(
                    fileName,
                    NormalizedDocument(),
                    new EmitOptions { Normalize = true, SortImports = true },
                    "Preserves QmlTS normalization intent with an intentionally unordered QmlSharp AST."),
                _ => throw new ArgumentOutOfRangeException(nameof(fileName), fileName, "Unknown golden fixture."),
            };
        }

        private static QmlDocument MinimalDocument()
        {
            return new QmlDocumentBuilder()
                .AddModuleImport("QtQuick", "2.15")
                .SetRootObject("Item", _ => { })
                .Build();
        }

        private static QmlDocument FullSyntaxDocument()
        {
            return new QmlDocumentBuilder()
                .AddPragma(PragmaName.Singleton)
                .AddPragma(PragmaName.ComponentBehavior, "Bound")
                .AddPragma(PragmaName.NativeTextRendering)
                .AddModuleImport("QtQuick", "6.0")
                .AddModuleImport("QtQuick.Controls")
                .AddDirectoryImport("../components")
                .AddJavaScriptImport("logic.js", "Logic")
                .SetRootObject("ApplicationWindow", root =>
                {
                    _ = root.Id("root")
                        .Comment("// QmlSharp full-syntax golden fixture")
                        .PropertyDeclaration("count", "int", Values.Number(0))
                        .PropertyDeclaration("title", "string", Values.String("My App"))
                        .PropertyDeclaration("items", "list<Item>")
                        .PropertyAlias("mainContent", "contentArea.data")
                        .SignalDeclaration("activated", new ParameterDeclaration("x", "int"), new ParameterDeclaration("y", "int"))
                        .SignalHandler("onActivated", SignalHandlerForm.Arrow, "console.log(x, y)", ["x", "y"])
                        .Binding("width", Values.Number(800))
                        .Binding("height", Values.Number(600))
                        .Binding("visible", Values.Boolean(true))
                        .Binding("opacity", Values.Expression("enabled ? 1.0 : 0.5"))
                        .GroupedBinding("anchors", anchors => anchors.Binding("fill", Values.Expression("parent")))
                        .AttachedBinding(
                            "Layout",
                            layout => layout
                                .Binding("fillWidth", Values.Boolean(true))
                                .Binding("preferredHeight", Values.Number(100)))
                        .ArrayBinding(
                            "states",
                            Values.Object("State", state => state.Binding("name", Values.String("idle"))),
                            Values.Object("State", state => state.Binding("name", Values.String("active"))))
                        .BehaviorOn("opacity", "NumberAnimation", animation => animation.Binding("duration", Values.Number(200)))
                        .FunctionDeclaration("doStuff", "return x > 0", "bool", new ParameterDeclaration("x", "int"))
                        .Child("Rectangle", rectangle =>
                        {
                            _ = rectangle.Id("contentArea")
                                .Binding("color", Values.String("white"))
                                .Child("Text", text => text
                                    .Binding("text", Values.String("Hello"))
                                    .GroupedBinding("font", font => font.Binding("pixelSize", Values.Number(24))));
                        })
                        .InlineComponent("MyButton", "Button", button => button.Binding("text", Values.String("Click")))
                        .EnumDeclaration(
                            "Status",
                            new EnumMember("Idle", 0),
                            new EnumMember("Loading", 1),
                            new EnumMember("Done", 2));
                })
                .Build();
        }

        private static QmlDocument NestedDocument()
        {
            return new QmlDocumentBuilder()
                .AddModuleImport("QtQuick")
                .SetRootObject("Item", root => root
                    .Child("Column", column => column
                        .Child("Row", row => row
                            .Child("Rectangle", rectangle => rectangle
                                .Child("Text", text => text.Binding("text", Values.String("Deep")))))))
                .Build();
        }

        private static QmlDocument AllBindingsDocument()
        {
            return new QmlDocumentBuilder()
                .AddModuleImport("QtQuick")
                .SetRootObject("Item", root => root
                    .Binding("width", Values.Number(400))
                    .Binding("opacity", Values.Number(0.5))
                    .Binding("x", Values.Number(-10))
                    .Binding("text", Values.String("hello"))
                    .Binding("visible", Values.Boolean(true))
                    .Binding("enabled", Values.Boolean(false))
                    .Binding("model", Values.Null())
                    .Binding("fillMode", Values.Enum("Image", "Stretch"))
                    .Binding("color", Values.Expression("enabled ? \"blue\" : \"gray\""))
                    .Binding("onCompleted", Values.Block("count++\nready = true"))
                    .Binding("font", Values.Object("Font", font => font.Binding("pixelSize", Values.Number(14))))
                    .Binding("metadata", Values.Array(Values.String("idle"), Values.Number(1), Values.Expression("root.width")))
                    .GroupedBinding("anchors", anchors => anchors
                        .Binding("left", Values.Expression("parent.left"))
                        .Binding("right", Values.Expression("parent.right")))
                    .AttachedBinding("Layout", layout => layout.Binding("fillWidth", Values.Boolean(true)))
                    .ArrayBinding(
                        "states",
                        Values.Object("State", state => state.Binding("name", Values.String("idle"))),
                        Values.Object("State", state => state.Binding("name", Values.String("active"))))
                    .BehaviorOn("opacity", "NumberAnimation", animation => animation.Binding("duration", Values.Number(200))))
                .Build();
        }

        private static QmlDocument AllMembersDocument()
        {
            return new QmlDocumentBuilder()
                .AddModuleImport("QtQuick")
                .SetRootObject("Item", root => root
                    .Id("root")
                    .Comment("// property and signal members")
                    .PropertyDeclaration("count", "int", Values.Number(0))
                    .PropertyDeclaration("name", "string")
                    .PropertyDeclaration("area", "real", Values.Expression("w * h"), isReadonly: true)
                    .PropertyDeclaration("title", "string", isRequired: true)
                    .PropertyDeclaration("content", "Item", isDefault: true)
                    .PropertyAlias("text", "label.text")
                    .SignalDeclaration("clicked")
                    .SignalDeclaration("moved", new ParameterDeclaration("x", "int"), new ParameterDeclaration("y", "int"))
                    .SignalHandler("onClicked", SignalHandlerForm.Expression, "doStuff()")
                    .SignalHandler("onMoved", SignalHandlerForm.Block, "console.log(\"moved\")")
                    .SignalHandler("onPressed", SignalHandlerForm.Arrow, "handle(event)", ["event"])
                    .FunctionDeclaration("calculate", "return a + b", "int", new ParameterDeclaration("a", "int"), new ParameterDeclaration("b", "int"))
                    .FunctionDeclaration("reset", "count = 0")
                    .Binding("width", Values.Number(100))
                    .Binding("height", Values.Number(200))
                    .Child("Rectangle", rectangle => rectangle.Binding("color", Values.String("red")))
                    .InlineComponent("Badge", "Text", badge => badge.Binding("text", Values.String("!")))
                    .EnumDeclaration(
                        "Direction",
                        new EnumMember("Up", null),
                        new EnumMember("Down", null),
                        new EnumMember("Left", null),
                        new EnumMember("Right", null)))
                .Build();
        }

        private static QmlDocument CommentsDocument()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddModuleImport("QtQuick")
                .SetRootObject("Item", _ => { })
                .Build();

            ObjectDefinitionNode root = document.RootObject with
            {
                LeadingComments = [new CommentNode { Text = "// Root object comment" }],
                TrailingComment = new CommentNode { Text = "// end root" },
                Members =
                [
                    new CommentNode { Text = "// This is a line comment" },
                    new BindingNode { PropertyName = "width", Value = Values.Number(100) },
                    new CommentNode { Text = "/* Block comment */", IsBlock = true },
                    new BindingNode
                    {
                        PropertyName = "height",
                        Value = Values.Number(200),
                        LeadingComments = [new CommentNode { Text = "// Height binding below" }],
                    },
                    new BindingNode
                    {
                        PropertyName = "opacity",
                        Value = Values.Number(1),
                        TrailingComment = new CommentNode { Text = "// full opacity" },
                    },
                ],
            };

            return document with
            {
                LeadingComments = [new CommentNode { Text = "// File comment" }],
                RootObject = root,
            };
        }

        private static QmlDocument NormalizedDocument()
        {
            return new QmlDocument
            {
                Imports =
                [
                    new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtQuick.Controls" },
                    new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtQuick" },
                ],
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members =
                    [
                        new FunctionDeclarationNode { Name = "calc", Body = "return 42" },
                        new ObjectDefinitionNode
                        {
                            TypeName = "Rectangle",
                            Members = [new BindingNode { PropertyName = "color", Value = Values.String("red") }],
                        },
                        new BindingNode { PropertyName = "width", Value = Values.Number(100) },
                        new IdAssignmentNode { Id = "root" },
                        new EnumDeclarationNode
                        {
                            Name = "Direction",
                            Members = [new EnumMember("Left", null), new EnumMember("Right", null)],
                        },
                        new PropertyDeclarationNode { Name = "count", TypeName = "int", InitialValue = Values.Number(0) },
                        new SignalHandlerNode { HandlerName = "onClicked", Form = SignalHandlerForm.Expression, Code = "doStuff()" },
                        new InlineComponentNode
                        {
                            Name = "Badge",
                            Body = new ObjectDefinitionNode
                            {
                                TypeName = "Text",
                                Members = [new BindingNode { PropertyName = "text", Value = Values.String("!") }],
                            },
                        },
                        new SignalDeclarationNode { Name = "clicked" },
                    ],
                },
            };
        }
    }
}
