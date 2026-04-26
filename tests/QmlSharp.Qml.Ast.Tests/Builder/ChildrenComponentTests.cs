using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Tests.Helpers;

namespace QmlSharp.Qml.Ast.Tests.Builder
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class ChildrenComponentTests
    {
        [Fact]
        public void BC_01_Add_child_object()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Column")
                .Child("Text", text =>
                {
                    _ = text.Binding("text", Values.String("hello"));
                })
                .Build();

            _ = Assert.Single(obj.Members);
            ObjectDefinitionNode child = Assert.IsType<ObjectDefinitionNode>(obj.Members[0]);
            Assert.Equal("Text", child.TypeName);
            _ = Assert.Single(child.Members);
        }

        [Fact]
        public void BC_02_Add_nested_children_3_levels_deep()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Column")
                .Child("Row", row =>
                {
                    _ = row.Child("Rectangle", rect =>
                    {
                        _ = rect.Binding("color", Values.String("red"));
                    });
                })
                .Build();

            ObjectDefinitionNode level1 = Assert.IsType<ObjectDefinitionNode>(obj.Members[0]);
            Assert.Equal("Row", level1.TypeName);

            ObjectDefinitionNode level2 = Assert.IsType<ObjectDefinitionNode>(level1.Members[0]);
            Assert.Equal("Rectangle", level2.TypeName);

            BindingNode binding = Assert.IsType<BindingNode>(level2.Members[0]);
            Assert.Equal("color", binding.PropertyName);
        }

        [Fact]
        public void BC_03_Add_inline_component()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .InlineComponent("Badge", "Rectangle", badge =>
                {
                    _ = badge.Binding("radius", Values.Number(8));
                })
                .Build();

            _ = Assert.Single(obj.Members);
            InlineComponentNode component = Assert.IsType<InlineComponentNode>(obj.Members[0]);
            Assert.Equal("Badge", component.Name);
            Assert.Equal("Rectangle", component.Body.TypeName);
            _ = Assert.Single(component.Body.Members);
        }

        [Fact]
        public void BC_04_Add_enum_declaration()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .EnumDeclaration("Status",
                    new EnumMember("Active", null),
                    new EnumMember("Inactive", 1),
                    new EnumMember("Pending", 2))
                .Build();

            _ = Assert.Single(obj.Members);
            EnumDeclarationNode enumDecl = Assert.IsType<EnumDeclarationNode>(obj.Members[0]);
            Assert.Equal("Status", enumDecl.Name);
            Assert.Equal(3, enumDecl.Members.Length);
            Assert.Equal("Active", enumDecl.Members[0].Name);
            Assert.Null(enumDecl.Members[0].Value);
            Assert.Equal("Inactive", enumDecl.Members[1].Name);
            Assert.Equal(1, enumDecl.Members[1].Value);
        }

        [Fact]
        public void BC_05_Add_comment_member()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .Comment("// property section")
                .Binding("width", Values.Number(100))
                .Comment("/* block comment */", isBlock: true)
                .Build();

            Assert.Equal(3, obj.Members.Length);

            CommentNode lineComment = Assert.IsType<CommentNode>(obj.Members[0]);
            Assert.Equal("// property section", lineComment.Text);
            Assert.False(lineComment.IsBlock);

            _ = Assert.IsType<BindingNode>(obj.Members[1]);

            CommentNode blockComment = Assert.IsType<CommentNode>(obj.Members[2]);
            Assert.Equal("/* block comment */", blockComment.Text);
            Assert.True(blockComment.IsBlock);
        }
    }
}
