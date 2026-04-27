using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Tests.Helpers;

namespace QmlSharp.Qml.Ast.Tests.Builder
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class GroupedAttachedTests
    {
        [Fact]
        public void BG_01_Add_grouped_binding()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Text")
                .GroupedBinding("font", font =>
                {
                    _ = font.Binding("pixelSize", Values.Number(14));
                    _ = font.Binding("bold", Values.Boolean(true));
                })
                .Build();

            _ = Assert.Single(obj.Members);
            GroupedBindingNode grouped = Assert.IsType<GroupedBindingNode>(obj.Members[0]);
            Assert.Equal("font", grouped.GroupName);
            Assert.Equal(2, grouped.Bindings.Length);
        }

        [Fact]
        public void BG_02_Add_attached_binding()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .AttachedBinding("Layout", layout =>
                {
                    _ = layout.Binding("fillWidth", Values.Boolean(true));
                    _ = layout.Binding("alignment", Values.Enum("Qt", "AlignCenter"));
                })
                .Build();

            _ = Assert.Single(obj.Members);
            AttachedBindingNode attached = Assert.IsType<AttachedBindingNode>(obj.Members[0]);
            Assert.Equal("Layout", attached.AttachedTypeName);
            Assert.Equal(2, attached.Bindings.Length);
        }

        [Fact]
        public void BG_03_Add_array_binding_with_multiple_elements()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .ArrayBinding("states",
                    Values.Object("State", s => { _ = s.Binding("name", Values.String("active")); }),
                    Values.Object("State", s => { _ = s.Binding("name", Values.String("inactive")); }))
                .Build();

            _ = Assert.Single(obj.Members);
            ArrayBindingNode array = Assert.IsType<ArrayBindingNode>(obj.Members[0]);
            Assert.Equal("states", array.PropertyName);
            Assert.Equal(2, array.Elements.Length);
        }

        [Fact]
        public void BG_04_Add_behavior_on_binding()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .BehaviorOn("x", "NumberAnimation", anim =>
                {
                    _ = anim.Binding("duration", Values.Number(200));
                })
                .Build();

            _ = Assert.Single(obj.Members);
            BehaviorOnNode behavior = Assert.IsType<BehaviorOnNode>(obj.Members[0]);
            Assert.Equal("x", behavior.PropertyName);
            Assert.Equal("NumberAnimation", behavior.Animation.TypeName);
            _ = Assert.Single(behavior.Animation.Members);
        }

        [Fact]
        public void BG_05_Grouped_binding_inner_order_preserved()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Text")
                .GroupedBinding("font", font =>
                {
                    _ = font.Binding("family", Values.String("Arial"));
                    _ = font.Binding("pixelSize", Values.Number(14));
                    _ = font.Binding("bold", Values.Boolean(true));
                })
                .Build();

            GroupedBindingNode grouped = Assert.IsType<GroupedBindingNode>(obj.Members[0]);
            Assert.Equal(3, grouped.Bindings.Length);
            Assert.Equal("family", grouped.Bindings[0].PropertyName);
            Assert.Equal("pixelSize", grouped.Bindings[1].PropertyName);
            Assert.Equal("bold", grouped.Bindings[2].PropertyName);
        }

        [Fact]
        public void BG_06_Attached_binding_with_single_property()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .AttachedBinding("Keys", keys =>
                {
                    _ = keys.Binding("enabled", Values.Boolean(true));
                })
                .Build();

            AttachedBindingNode attached = Assert.IsType<AttachedBindingNode>(obj.Members[0]);
            _ = Assert.Single(attached.Bindings);
        }
    }
}
