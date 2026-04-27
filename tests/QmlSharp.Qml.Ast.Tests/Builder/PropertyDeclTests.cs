using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Tests.Helpers;

namespace QmlSharp.Qml.Ast.Tests.Builder
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class PropertyDeclTests
    {
        [Fact]
        public void BP_01_Declare_simple_property()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .PropertyDeclaration("count", "int")
                .Build();

            _ = Assert.Single(obj.Members);
            PropertyDeclarationNode prop = Assert.IsType<PropertyDeclarationNode>(obj.Members[0]);
            Assert.Equal("count", prop.Name);
            Assert.Equal("int", prop.TypeName);
            Assert.False(prop.IsDefault);
            Assert.False(prop.IsRequired);
            Assert.False(prop.IsReadonly);
            Assert.Null(prop.InitialValue);
        }

        [Fact]
        public void BP_02_Declare_property_with_initial_value()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .PropertyDeclaration("count", "int", Values.Number(0))
                .Build();

            PropertyDeclarationNode prop = Assert.IsType<PropertyDeclarationNode>(obj.Members[0]);
            Assert.NotNull(prop.InitialValue);
            NumberLiteral value = Assert.IsType<NumberLiteral>(prop.InitialValue);
            Assert.Equal(0.0, value.Value);
        }

        [Fact]
        public void BP_03_Declare_default_property()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .PropertyDeclaration("content", "Item", isDefault: true)
                .Build();

            PropertyDeclarationNode prop = Assert.IsType<PropertyDeclarationNode>(obj.Members[0]);
            Assert.True(prop.IsDefault);
            Assert.False(prop.IsRequired);
            Assert.False(prop.IsReadonly);
        }

        [Fact]
        public void BP_04_Declare_required_property()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .PropertyDeclaration("name", "string", isRequired: true)
                .Build();

            PropertyDeclarationNode prop = Assert.IsType<PropertyDeclarationNode>(obj.Members[0]);
            Assert.True(prop.IsRequired);
        }

        [Fact]
        public void BP_05_Declare_readonly_property()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .PropertyDeclaration("sourceSize", "size", isReadonly: true)
                .Build();

            PropertyDeclarationNode prop = Assert.IsType<PropertyDeclarationNode>(obj.Members[0]);
            Assert.True(prop.IsReadonly);
        }

        [Fact]
        public void BP_06_Declare_property_with_combined_modifiers()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .PropertyDeclaration("data", "var", isDefault: true, isRequired: true)
                .Build();

            PropertyDeclarationNode prop = Assert.IsType<PropertyDeclarationNode>(obj.Members[0]);
            Assert.True(prop.IsDefault);
            Assert.True(prop.IsRequired);
        }

        [Fact]
        public void BP_07_Declare_property_alias()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .PropertyAlias("text", "label.text")
                .Build();

            _ = Assert.Single(obj.Members);
            PropertyAliasNode alias = Assert.IsType<PropertyAliasNode>(obj.Members[0]);
            Assert.Equal("text", alias.Name);
            Assert.Equal("label.text", alias.Target);
        }

        [Fact]
        public void BP_08_Declare_property_with_list_type()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .PropertyDeclaration("children", "list<Item>")
                .Build();

            PropertyDeclarationNode prop = Assert.IsType<PropertyDeclarationNode>(obj.Members[0]);
            Assert.Equal("list<Item>", prop.TypeName);
        }
    }
}
