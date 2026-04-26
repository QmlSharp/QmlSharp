using QmlSharp.Qml.Ast.Builders;

namespace QmlSharp.Qml.Ast.Tests.Helpers
{
    internal static class FullSyntaxDocumentFactory
    {
        public static QmlDocument Create()
        {
            return new QmlDocumentBuilder()
                .AddPragma(PragmaName.Singleton)
                .AddPragma(PragmaName.ComponentBehavior, "Bound")
                .AddPragma(PragmaName.ListPropertyAssignBehavior, "ReplaceIfNotDefault")
                .AddPragma(PragmaName.FunctionSignatureBehavior, "Enforced")
                .AddPragma(PragmaName.NativeMethodBehavior, "AcceptThisObject")
                .AddPragma(PragmaName.ValueTypeBehavior, "Addressable")
                .AddPragma(PragmaName.NativeTextRendering)
                .AddPragma(PragmaName.Translator, "qsTr")
                .AddModuleImport("QtQuick", "2.15")
                .AddDirectoryImport("./components")
                .AddJavaScriptImport("utils.js", "Utils")
                .SetRootObject("Rectangle", root =>
                {
                    _ = root.Id("myRect")
                        .PropertyDeclaration("count", "int", Values.Number(0))
                        .PropertyDeclaration("label", "string", isRequired: true)
                        .PropertyDeclaration("data", "var", isDefault: true)
                        .PropertyDeclaration("sourceSize", "size", isReadonly: true)
                        .PropertyAlias("text", "label.text")
                        .Binding("width", Values.Number(100))
                        .Binding("height", Values.Number(200))
                        .Binding("color", Values.String("red"))
                        .Binding("visible", Values.Boolean(true))
                        .Binding("model", Values.Null())
                        .Binding("fillMode", Values.Enum("Image", "Stretch"))
                        .Binding("opacity", Values.Expression("parent.opacity * 0.5"))
                        .Binding("onCompleted", Values.Block("{ console.log(\"ready\"); }"))
                        .GroupedBinding("font", font =>
                        {
                            _ = font.Binding("pixelSize", Values.Number(14));
                            _ = font.Binding("bold", Values.Boolean(true));
                        })
                        .AttachedBinding("Layout", layout =>
                        {
                            _ = layout.Binding("fillWidth", Values.Boolean(true));
                            _ = layout.Binding("alignment", Values.Enum("Qt", "AlignCenter"));
                        })
                        .ArrayBinding("states",
                            Values.Object("State", s => { _ = s.Binding("name", Values.String("active")); }),
                            Values.Object("State", s => { _ = s.Binding("name", Values.String("inactive")); }))
                        .BehaviorOn("x", "NumberAnimation", anim =>
                        {
                            _ = anim.Binding("duration", Values.Number(200));
                        })
                        .SignalDeclaration("clicked")
                        .SignalDeclaration("positionChanged",
                            new ParameterDeclaration("x", "int"),
                            new ParameterDeclaration("y", "int"))
                        .SignalHandler("onClicked", SignalHandlerForm.Expression, "console.log(\"clicked\")")
                        .SignalHandler("onPressed", SignalHandlerForm.Block, "{ console.log(\"pressed\"); }")
                        .SignalHandler("onPositionChanged", SignalHandlerForm.Arrow, "{ console.log(x, y); }", ["x", "y"])
                        .FunctionDeclaration("compute", "{ return x + y; }", "int",
                            new ParameterDeclaration("x", "int"),
                            new ParameterDeclaration("y", "int"))
                        .FunctionDeclaration("doSomething", "{ console.log(\"done\"); }")
                        .EnumDeclaration("Status",
                            new EnumMember("Active", null),
                            new EnumMember("Inactive", 1),
                            new EnumMember("Pending", 2))
                        .InlineComponent("Badge", "Rectangle", badge =>
                        {
                            _ = badge.Binding("radius", Values.Number(8));
                            _ = badge.Binding("color", Values.String("blue"));
                        })
                        .Child("Text", text =>
                        {
                            _ = text.Binding("text", Values.String("hello"));
                            _ = text.Child("Item", inner =>
                            {
                                _ = inner.Binding("visible", Values.Boolean(false));
                            });
                        })
                        .Comment("// end of root object");
                })
                .Build();
        }

        public static ImmutableArray<PragmaName> AllPragmas()
        {
            return [.. Enum.GetValues<PragmaName>()];
        }

        public static ImmutableArray<ImportKind> AllImportKinds()
        {
            return [.. Enum.GetValues<ImportKind>()];
        }

        public static ImmutableArray<SignalHandlerForm> AllSignalHandlerForms()
        {
            return [.. Enum.GetValues<SignalHandlerForm>()];
        }
    }
}
