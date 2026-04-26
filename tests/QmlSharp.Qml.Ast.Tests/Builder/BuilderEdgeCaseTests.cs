using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Tests.Helpers;

namespace QmlSharp.Qml.Ast.Tests.Builder
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class BuilderEdgeCaseTests
    {
        [Fact]
        public void CS_01_Build_without_root_object_throws_InvalidOperationException()
        {
            QmlDocumentBuilder builder = new();
            _ = builder.AddPragma(PragmaName.Singleton);
            _ = builder.AddModuleImport("QtQuick", "2.15");

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
            Assert.Contains("Root object", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CS_02_Document_output_is_immutable_after_builder_mutation()
        {
            QmlDocumentBuilder builder = new();
            _ = builder.SetRootObject("Item", _ => { });

            QmlDocument first = builder.Build();
            Assert.Empty(first.Pragmas);

            _ = builder.AddPragma(PragmaName.Singleton);
            QmlDocument second = builder.Build();

            Assert.Empty(first.Pragmas);
            _ = Assert.Single(second.Pragmas);
        }

        [Fact]
        public void CS_03_Object_output_is_immutable_after_builder_mutation()
        {
            QmlObjectBuilder builder = new("Item");
            _ = builder.Binding("width", Values.Number(100));

            ObjectDefinitionNode first = builder.Build();
            _ = Assert.Single(first.Members);

            _ = builder.Binding("height", Values.Number(200));
            ObjectDefinitionNode second = builder.Build();

            _ = Assert.Single(first.Members);
            Assert.Equal(2, second.Members.Length);
        }

        [Fact]
        public void CS_04_Child_with_no_configure_creates_empty_object()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Column")
                .Child("Spacer")
                .Build();

            ObjectDefinitionNode child = Assert.IsType<ObjectDefinitionNode>(obj.Members[0]);
            Assert.Equal("Spacer", child.TypeName);
            Assert.Empty(child.Members);
        }

        [Fact]
        public void CS_05_BehaviorOn_with_no_configure_creates_empty_animation()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .BehaviorOn("opacity", "NumberAnimation")
                .Build();

            BehaviorOnNode behavior = Assert.IsType<BehaviorOnNode>(obj.Members[0]);
            Assert.Equal("NumberAnimation", behavior.Animation.TypeName);
            Assert.Empty(behavior.Animation.Members);
        }

        [Fact]
        public void CS_06_All_member_types_preserve_declaration_order()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .Id("root")
                .PropertyDeclaration("count", "int")
                .PropertyAlias("text", "label.text")
                .Binding("width", Values.Number(100))
                .GroupedBinding("font", f => f.Binding("pixelSize", Values.Number(14)))
                .AttachedBinding("Layout", l => l.Binding("fillWidth", Values.Boolean(true)))
                .ArrayBinding("states", Values.Object("State", _ => { }))
                .BehaviorOn("x", "NumberAnimation")
                .SignalDeclaration("clicked")
                .SignalHandler("onClicked", SignalHandlerForm.Expression, "doStuff()")
                .FunctionDeclaration("doStuff", "{ }")
                .EnumDeclaration("Status", new EnumMember("Active", null))
                .InlineComponent("Badge", "Rectangle", _ => { })
                .Child("Text")
                .Comment("// end")
                .Build();

            Assert.Equal(15, obj.Members.Length);
            _ = Assert.IsType<IdAssignmentNode>(obj.Members[0]);
            _ = Assert.IsType<PropertyDeclarationNode>(obj.Members[1]);
            _ = Assert.IsType<PropertyAliasNode>(obj.Members[2]);
            _ = Assert.IsType<BindingNode>(obj.Members[3]);
            _ = Assert.IsType<GroupedBindingNode>(obj.Members[4]);
            _ = Assert.IsType<AttachedBindingNode>(obj.Members[5]);
            _ = Assert.IsType<ArrayBindingNode>(obj.Members[6]);
            _ = Assert.IsType<BehaviorOnNode>(obj.Members[7]);
            _ = Assert.IsType<SignalDeclarationNode>(obj.Members[8]);
            _ = Assert.IsType<SignalHandlerNode>(obj.Members[9]);
            _ = Assert.IsType<FunctionDeclarationNode>(obj.Members[10]);
            _ = Assert.IsType<EnumDeclarationNode>(obj.Members[11]);
            _ = Assert.IsType<InlineComponentNode>(obj.Members[12]);
            _ = Assert.IsType<ObjectDefinitionNode>(obj.Members[13]);
            _ = Assert.IsType<CommentNode>(obj.Members[14]);
        }
    }
}
