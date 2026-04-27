using QmlSharp.Qml.Ast.Tests.Helpers;

namespace QmlSharp.Qml.Ast.Tests
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class ValueFactoryTests
    {
        [Fact]
        public void VF_01_Values_Number_from_integer_creates_NumberLiteral()
        {
            NumberLiteral value = Values.Number(42);

            Assert.Equal(BindingValueKind.NumberLiteral, value.Kind);
            Assert.Equal(42.0, value.Value);
        }

        [Fact]
        public void VF_02_Values_Number_from_decimal_creates_NumberLiteral()
        {
            NumberLiteral value = Values.Number(3.14);

            Assert.Equal(BindingValueKind.NumberLiteral, value.Kind);
            Assert.Equal(3.14, value.Value);
        }

        [Fact]
        public void VF_03_Values_String_creates_StringLiteral()
        {
            StringLiteral value = Values.String("hello");

            Assert.Equal(BindingValueKind.StringLiteral, value.Kind);
            Assert.Equal("hello", value.Value);
        }

        [Fact]
        public void VF_04_Values_Boolean_true_creates_BooleanLiteral()
        {
            BooleanLiteral value = Values.Boolean(true);

            Assert.Equal(BindingValueKind.BooleanLiteral, value.Kind);
            Assert.True(value.Value);
        }

        [Fact]
        public void VF_05_Values_Boolean_false_creates_BooleanLiteral()
        {
            BooleanLiteral value = Values.Boolean(false);

            Assert.Equal(BindingValueKind.BooleanLiteral, value.Kind);
            Assert.False(value.Value);
        }

        [Fact]
        public void VF_06_Values_Null_returns_singleton_NullLiteral()
        {
            NullLiteral value = Values.Null();

            Assert.Equal(BindingValueKind.NullLiteral, value.Kind);
            Assert.Same(NullLiteral.Instance, value);
        }

        [Fact]
        public void VF_07_Values_Enum_creates_EnumReference()
        {
            EnumReference value = Values.Enum("Image", "Stretch");

            Assert.Equal(BindingValueKind.EnumReference, value.Kind);
            Assert.Equal("Image", value.TypeName);
            Assert.Equal("Stretch", value.MemberName);
        }

        [Fact]
        public void VF_08_Values_Expression_creates_ScriptExpression()
        {
            ScriptExpression value = Values.Expression("parent.width * 0.5");

            Assert.Equal(BindingValueKind.ScriptExpression, value.Kind);
            Assert.Equal("parent.width * 0.5", value.Code);
        }

        [Fact]
        public void VF_09_Values_Block_creates_ScriptBlock()
        {
            ScriptBlock value = Values.Block("{ var x = 1; return x; }");

            Assert.Equal(BindingValueKind.ScriptBlock, value.Kind);
            Assert.Equal("{ var x = 1; return x; }", value.Code);
        }

        [Fact]
        public void VF_10_Values_Object_wraps_ObjectDefinitionNode()
        {
            ObjectDefinitionNode obj = new() { TypeName = "Rectangle" };
            ObjectValue value = Values.Object(obj);

            Assert.Equal(BindingValueKind.ObjectValue, value.Kind);
            Assert.Equal("Rectangle", value.Object.TypeName);
        }

        [Fact]
        public void VF_10b_Values_Object_via_builder_callback()
        {
            ObjectValue value = Values.Object("Rectangle", root =>
            {
                _ = root.Binding("width", Values.Number(100));
            });

            Assert.Equal(BindingValueKind.ObjectValue, value.Kind);
            Assert.Equal("Rectangle", value.Object.TypeName);
            _ = Assert.Single(value.Object.Members);
        }

        [Fact]
        public void VF_11_Values_Array_creates_ArrayValue_preserving_order()
        {
            ArrayValue value = Values.Array(
                Values.Number(1),
                Values.String("two"),
                Values.Null());

            Assert.Equal(BindingValueKind.ArrayValue, value.Kind);
            Assert.Equal(3, value.Elements.Length);
            _ = Assert.IsType<NumberLiteral>(value.Elements[0]);
            _ = Assert.IsType<StringLiteral>(value.Elements[1]);
            _ = Assert.IsType<NullLiteral>(value.Elements[2]);
        }

        [Fact]
        public void VF_11b_Values_Array_from_ImmutableArray()
        {
            ImmutableArray<BindingValue> elements = [Values.Number(1), Values.String("a")];
            ArrayValue value = Values.Array(elements);

            Assert.Equal(2, value.Elements.Length);
            Assert.Equal(elements, value.Elements);
        }
    }
}
