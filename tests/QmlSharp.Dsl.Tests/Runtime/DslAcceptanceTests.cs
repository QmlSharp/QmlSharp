using System.Collections.Immutable;
using QmlSharp.Core;
using QmlSharp.Dsl.Tests.Fixtures;
using QmlSharp.Qml.Ast;

#pragma warning disable IDE0058

namespace QmlSharp.Dsl.Tests.Runtime
{
    public sealed class DslAcceptanceTests
    {
        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void GeneratedStyleDsl_BasicSettersBindingsSignalsAndId_ProduceExpectedAst()
        {
            IRectangleBuilder rectangle = CreateRectangle();

            IRectangleBuilder result = rectangle
                .Id("myRect")
                .Width(100)
                .HeightBind("parent.height / 2")
                .Color("red")
                .OnColorChanged("console.log(\"changed\")");

            ObjectDefinitionNode node = rectangle.Build();

            Assert.Same(rectangle, result);
            Assert.Equal("Rectangle", node.TypeName);
            Assert.Collection(
                node.Members,
                member => Assert.Equal("myRect", Assert.IsType<IdAssignmentNode>(member).Id),
                member => AssertNumberBinding(member, "width", 100),
                member => AssertExpressionBinding(member, "height", "parent.height / 2"),
                member => AssertStringBinding(member, "color", "red"),
                member =>
                {
                    SignalHandlerNode handler = Assert.IsType<SignalHandlerNode>(member);
                    Assert.Equal("onColorChanged", handler.HandlerName);
                    Assert.Equal("console.log(\"changed\")", handler.Code);
                });
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void GeneratedStyleDsl_ChildAndChildren_PreserveNestedObjectOrder()
        {
            IColumnBuilder column = CreateColumn();

            column
                .Spacing(10)
                .Child(CreateText().Text("First"))
                .Children(
                    CreateRectangle().Width(100).Height(50).Color("green"),
                    CreateText().Text("Third"));

            ObjectDefinitionNode node = column.Build();

            Assert.Equal("Column", node.TypeName);
            Assert.Collection(
                node.Members,
                member => AssertNumberBinding(member, "spacing", 10),
                member => Assert.Equal("Text", Assert.IsType<ObjectDefinitionNode>(member).TypeName),
                member => Assert.Equal("Rectangle", Assert.IsType<ObjectDefinitionNode>(member).TypeName),
                member => Assert.Equal("Text", Assert.IsType<ObjectDefinitionNode>(member).TypeName));
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void GeneratedStyleDsl_GroupedAttachedAndEnumProperties_ProduceExpectedAst()
        {
            IRectangleBuilder rectangle = CreateRectangle();

            rectangle
                .Border(border => border.Width(2).Color("black"))
                .Layout(layout => layout.FillWidth(true).FillHeightBind("parent.enabled"))
                .Child(CreateText().Text("Hello").WrapMode(TextWrapMode.WordWrap));

            ObjectDefinitionNode node = rectangle.Build();

            GroupedBindingNode grouped = Assert.IsType<GroupedBindingNode>(node.Members[0]);
            Assert.Equal("border", grouped.GroupName);
            Assert.Collection(
                grouped.Bindings,
                binding => AssertNumberBinding(binding, "width", 2),
                binding => AssertStringBinding(binding, "color", "black"));

            AttachedBindingNode attached = Assert.IsType<AttachedBindingNode>(node.Members[1]);
            Assert.Equal("Layout", attached.AttachedTypeName);
            Assert.Collection(
                attached.Bindings,
                binding => AssertBooleanBinding(binding, "fillWidth", expected: true),
                binding => AssertExpressionBinding(binding, "fillHeight", "parent.enabled"));

            ObjectDefinitionNode text = Assert.IsType<ObjectDefinitionNode>(node.Members[2]);
            BindingNode enumBinding = Assert.IsType<BindingNode>(text.Members[1]);
            EnumReference enumReference = Assert.IsType<EnumReference>(enumBinding.Value);
            Assert.Equal("wrapMode", enumBinding.PropertyName);
            Assert.Equal(nameof(TextWrapMode), enumReference.TypeName);
            Assert.Equal(nameof(TextWrapMode.WordWrap), enumReference.MemberName);
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void GeneratedStyleDsl_ComplexNestedScene_ProducesAstWithExpectedStructure()
        {
            IObjectBuilder scene = CreateItem()
                .Id("root")
                .Width(800)
                .Height(600)
                .Children(
                    CreateColumn()
                        .Spacing(20)
                        .Children(
                            CreateText().Text("Welcome").WrapMode(TextWrapMode.WordWrap),
                            CreateRectangle()
                                .Width(100)
                                .Height(100)
                                .Color("navy")
                                .Border(border => border.Width(1).Color("white"))),
                    CreateButton()
                        .Text("Submit")
                        .Checked(false)
                        .OnClicked("submitForm()"));

            ObjectDefinitionNode root = scene.Build();

            Assert.Equal("Item", root.TypeName);
            Assert.Collection(
                root.Members,
                member => Assert.Equal("root", Assert.IsType<IdAssignmentNode>(member).Id),
                member => AssertNumberBinding(member, "width", 800),
                member => AssertNumberBinding(member, "height", 600),
                member =>
                {
                    ObjectDefinitionNode column = Assert.IsType<ObjectDefinitionNode>(member);
                    Assert.Equal("Column", column.TypeName);
                    Assert.Equal(3, column.Members.Length);
                },
                member =>
                {
                    ObjectDefinitionNode button = Assert.IsType<ObjectDefinitionNode>(member);
                    Assert.Equal("Button", button.TypeName);
                    Assert.Contains(button.Members, nested => nested is SignalHandlerNode { HandlerName: "onClicked" });
                });
        }

        private static IItemBuilder CreateItem()
        {
            return ObjectFactory.Create<IItemBuilder>("Item", CreateItemMetadata());
        }

        private static IColumnBuilder CreateColumn()
        {
            return ObjectFactory.Create<IColumnBuilder>("Column", CreateColumnMetadata());
        }

        private static IRectangleBuilder CreateRectangle()
        {
            return ObjectFactory.Create<IRectangleBuilder>("Rectangle", CreateRectangleMetadata());
        }

        private static ITextBuilder CreateText()
        {
            return ObjectFactory.Create<ITextBuilder>("Text", CreateTextMetadata());
        }

        private static IButtonBuilder CreateButton()
        {
            return ObjectFactory.Create<IButtonBuilder>("Button", CreateButtonMetadata());
        }

        private static ObjectBuilderMetadata CreateItemMetadata()
        {
            return new ObjectBuilderMetadata(
                ImmutableArray.Create(
                    new PropertyMethodMetadata("Width", "width"),
                    new PropertyMethodMetadata("Height", "height")),
                ImmutableArray<GroupedPropertyMethodMetadata>.Empty,
                ImmutableArray<AttachedPropertyMethodMetadata>.Empty,
                ImmutableArray<SignalMethodMetadata>.Empty);
        }

        private static ObjectBuilderMetadata CreateColumnMetadata()
        {
            return new ObjectBuilderMetadata(
                ImmutableArray.Create(new PropertyMethodMetadata("Spacing", "spacing")),
                ImmutableArray<GroupedPropertyMethodMetadata>.Empty,
                ImmutableArray<AttachedPropertyMethodMetadata>.Empty,
                ImmutableArray<SignalMethodMetadata>.Empty);
        }

        private static ObjectBuilderMetadata CreateRectangleMetadata()
        {
            PropertyCollectorMetadata border = new(
                ImmutableArray.Create(
                    new PropertyMethodMetadata("Width", "width"),
                    new PropertyMethodMetadata("Color", "color")),
                ImmutableArray<SignalMethodMetadata>.Empty);
            PropertyCollectorMetadata layout = new(
                ImmutableArray.Create(
                    new PropertyMethodMetadata("FillWidth", "fillWidth"),
                    new PropertyMethodMetadata("FillHeight", "fillHeight")),
                ImmutableArray<SignalMethodMetadata>.Empty);

            return new ObjectBuilderMetadata(
                ImmutableArray.Create(
                    new PropertyMethodMetadata("Width", "width"),
                    new PropertyMethodMetadata("Height", "height"),
                    new PropertyMethodMetadata("Color", "color")),
                ImmutableArray.Create(new GroupedPropertyMethodMetadata("Border", "border", border)),
                ImmutableArray.Create(new AttachedPropertyMethodMetadata("Layout", "Layout", layout)),
                ImmutableArray.Create(new SignalMethodMetadata("OnColorChanged", "onColorChanged")));
        }

        private static ObjectBuilderMetadata CreateTextMetadata()
        {
            return new ObjectBuilderMetadata(
                ImmutableArray.Create(
                    new PropertyMethodMetadata("Text", "text"),
                    new PropertyMethodMetadata("WrapMode", "wrapMode")),
                ImmutableArray<GroupedPropertyMethodMetadata>.Empty,
                ImmutableArray<AttachedPropertyMethodMetadata>.Empty,
                ImmutableArray<SignalMethodMetadata>.Empty);
        }

        private static ObjectBuilderMetadata CreateButtonMetadata()
        {
            return new ObjectBuilderMetadata(
                ImmutableArray.Create(
                    new PropertyMethodMetadata("Text", "text"),
                    new PropertyMethodMetadata("Checked", "checked")),
                ImmutableArray<GroupedPropertyMethodMetadata>.Empty,
                ImmutableArray<AttachedPropertyMethodMetadata>.Empty,
                ImmutableArray.Create(new SignalMethodMetadata("OnClicked", "onClicked")));
        }

        private static void AssertNumberBinding(AstNode member, string propertyName, double expected)
        {
            BindingNode binding = Assert.IsType<BindingNode>(member);
            Assert.Equal(propertyName, binding.PropertyName);
            Assert.Equal(expected, Assert.IsType<NumberLiteral>(binding.Value).Value);
        }

        private static void AssertStringBinding(AstNode member, string propertyName, string expected)
        {
            BindingNode binding = Assert.IsType<BindingNode>(member);
            Assert.Equal(propertyName, binding.PropertyName);
            Assert.Equal(expected, Assert.IsType<StringLiteral>(binding.Value).Value);
        }

        private static void AssertBooleanBinding(AstNode member, string propertyName, bool expected)
        {
            BindingNode binding = Assert.IsType<BindingNode>(member);
            Assert.Equal(propertyName, binding.PropertyName);
            Assert.Equal(expected, Assert.IsType<BooleanLiteral>(binding.Value).Value);
        }

        private static void AssertExpressionBinding(AstNode member, string propertyName, string expected)
        {
            BindingNode binding = Assert.IsType<BindingNode>(member);
            Assert.Equal(propertyName, binding.PropertyName);
            Assert.Equal(expected, Assert.IsType<ScriptExpression>(binding.Value).Code);
        }

        private enum TextWrapMode
        {
            NoWrap,
            WordWrap,
        }

        private interface IItemBuilder : IObjectBuilder
        {
            new IItemBuilder Id(string id);

            IItemBuilder Width(double value);

            IItemBuilder Height(double value);

            new IItemBuilder Children(params IObjectBuilder[] children);
        }

        private interface IColumnBuilder : IObjectBuilder
        {
            IColumnBuilder Spacing(double value);

            new IColumnBuilder Child(IObjectBuilder child);

            new IColumnBuilder Children(params IObjectBuilder[] children);
        }

        private interface IRectangleBuilder : IObjectBuilder
        {
            new IRectangleBuilder Id(string id);

            IRectangleBuilder Width(double value);

            IRectangleBuilder Height(double value);

            IRectangleBuilder HeightBind(string expression);

            IRectangleBuilder Color(QmlColor value);

            IRectangleBuilder Border(Action<IBorderBuilder> configure);

            IRectangleBuilder Layout(Action<ILayoutBuilder> configure);

            new IRectangleBuilder Child(IObjectBuilder child);

            IRectangleBuilder OnColorChanged(string body);
        }

        private interface ITextBuilder : IObjectBuilder
        {
            ITextBuilder Text(string value);

            ITextBuilder WrapMode(TextWrapMode value);
        }

        private interface IButtonBuilder : IObjectBuilder
        {
            IButtonBuilder Text(string value);

            IButtonBuilder Checked(bool value);

            IButtonBuilder OnClicked(string body);
        }

        private interface IBorderBuilder : IPropertyCollector
        {
            IBorderBuilder Width(double value);

            IBorderBuilder Color(QmlColor value);
        }

        private interface ILayoutBuilder : IPropertyCollector
        {
            ILayoutBuilder FillWidth(bool value);

            ILayoutBuilder FillHeightBind(string expression);
        }
    }
}

#pragma warning restore IDE0058
