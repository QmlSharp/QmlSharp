using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Tests.Helpers;

namespace QmlSharp.Qml.Ast.Tests.Builder
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class ObjectBasicTests
    {
        [Fact]
        public void BO_01_Build_object_with_type_name_only()
        {
            QmlObjectBuilder builder = new("Item");
            ObjectDefinitionNode obj = builder.Build();

            Assert.Equal("Item", obj.TypeName);
            Assert.Empty(obj.Members);
        }

        [Fact]
        public void BO_02_Build_object_with_id()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .Id("myItem")
                .Build();

            _ = Assert.Single(obj.Members);
            IdAssignmentNode id = Assert.IsType<IdAssignmentNode>(obj.Members[0]);
            Assert.Equal("myItem", id.Id);
        }

        [Fact]
        public void BO_03_Build_object_with_number_binding()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Rectangle")
                .Binding("width", Values.Number(100))
                .Build();

            _ = Assert.Single(obj.Members);
            BindingNode binding = Assert.IsType<BindingNode>(obj.Members[0]);
            Assert.Equal("width", binding.PropertyName);
            NumberLiteral value = Assert.IsType<NumberLiteral>(binding.Value);
            Assert.Equal(100.0, value.Value);
        }

        [Fact]
        public void BO_04_Build_object_with_string_binding()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Text")
                .Binding("text", Values.String("hello"))
                .Build();

            _ = Assert.Single(obj.Members);
            BindingNode binding = Assert.IsType<BindingNode>(obj.Members[0]);
            StringLiteral value = Assert.IsType<StringLiteral>(binding.Value);
            Assert.Equal("hello", value.Value);
        }

        [Fact]
        public void BO_05_Build_object_with_boolean_binding()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .Binding("visible", Values.Boolean(true))
                .Build();

            _ = Assert.Single(obj.Members);
            BindingNode binding = Assert.IsType<BindingNode>(obj.Members[0]);
            BooleanLiteral value = Assert.IsType<BooleanLiteral>(binding.Value);
            Assert.True(value.Value);
        }

        [Fact]
        public void BO_06_Build_object_with_null_binding()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .Binding("model", Values.Null())
                .Build();

            _ = Assert.Single(obj.Members);
            BindingNode binding = Assert.IsType<BindingNode>(obj.Members[0]);
            _ = Assert.IsType<NullLiteral>(binding.Value);
        }

        [Fact]
        public void BO_07_Build_object_with_enum_reference_binding()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Image")
                .Binding("fillMode", Values.Enum("Image", "Stretch"))
                .Build();

            _ = Assert.Single(obj.Members);
            BindingNode binding = Assert.IsType<BindingNode>(obj.Members[0]);
            EnumReference value = Assert.IsType<EnumReference>(binding.Value);
            Assert.Equal("Image", value.TypeName);
            Assert.Equal("Stretch", value.MemberName);
        }

        [Fact]
        public void BO_08_Build_object_with_script_expression_binding()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .Binding("width", Values.Expression("parent.width * 0.5"))
                .Build();

            _ = Assert.Single(obj.Members);
            BindingNode binding = Assert.IsType<BindingNode>(obj.Members[0]);
            ScriptExpression value = Assert.IsType<ScriptExpression>(binding.Value);
            Assert.Equal("parent.width * 0.5", value.Code);
        }

        [Fact]
        public void BO_09_Build_object_with_multiple_bindings_preserves_order()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Rectangle")
                .Binding("width", Values.Number(100))
                .Binding("height", Values.Number(200))
                .Binding("color", Values.String("red"))
                .Build();

            Assert.Equal(3, obj.Members.Length);
            Assert.Equal("width", Assert.IsType<BindingNode>(obj.Members[0]).PropertyName);
            Assert.Equal("height", Assert.IsType<BindingNode>(obj.Members[1]).PropertyName);
            Assert.Equal("color", Assert.IsType<BindingNode>(obj.Members[2]).PropertyName);
        }
    }
}
