using System.Collections.Immutable;
using QmlSharp.Dsl.Tests.Fixtures;
using QmlSharp.Qml.Ast;

#pragma warning disable IDE0058

namespace QmlSharp.Dsl.Tests.Runtime
{
    public sealed class ObjectFactoryProxyTests
    {
        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void Create_ReturnsGeneratedInterfaceProxyThatBuildsAst()
        {
            IRectangleBuilder rectangle = ObjectFactory.Create<IRectangleBuilder>("Rectangle");

            IRectangleBuilder result = rectangle
                .Id("root")
                .Width(100)
                .HeightBind("parent.height")
                .OnClicked("console.log(\"clicked\")");
            ObjectDefinitionNode node = rectangle.Build();

            Assert.Same(rectangle, result);
            Assert.Equal("Rectangle", rectangle.QmlTypeName);
            Assert.Collection(
                node.Members,
                member => Assert.IsType<IdAssignmentNode>(member),
                member => Assert.Equal("width", Assert.IsType<BindingNode>(member).PropertyName),
                member => Assert.Equal("height", Assert.IsType<BindingNode>(member).PropertyName),
                member => Assert.Equal("onClicked", Assert.IsType<SignalHandlerNode>(member).HandlerName));
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void Create_UsesMetadataForGroupedAndAttachedCallbacks()
        {
            PropertyCollectorMetadata borderCollector = new(
                ImmutableArray.Create(
                    new PropertyMethodMetadata("Width", "width"),
                    new PropertyMethodMetadata("Color", "color")),
                ImmutableArray<SignalMethodMetadata>.Empty);
            PropertyCollectorMetadata layoutCollector = new(
                ImmutableArray.Create(new PropertyMethodMetadata("FillWidth", "fillWidth")),
                ImmutableArray<SignalMethodMetadata>.Empty);
            ObjectBuilderMetadata metadata = new(
                ImmutableArray<PropertyMethodMetadata>.Empty,
                ImmutableArray.Create(new GroupedPropertyMethodMetadata("Border", "border", borderCollector)),
                ImmutableArray.Create(new AttachedPropertyMethodMetadata("Layout", "Layout", layoutCollector)),
                ImmutableArray<SignalMethodMetadata>.Empty);
            IRectangleBuilder rectangle = ObjectFactory.Create<IRectangleBuilder>("Rectangle", metadata);

            rectangle
                .Border(border => border.Width(2).Color("black"))
                .Layout(layout => layout.FillWidth(true));
            ObjectDefinitionNode node = rectangle.Build();

            GroupedBindingNode grouped = Assert.IsType<GroupedBindingNode>(node.Members[0]);
            AttachedBindingNode attached = Assert.IsType<AttachedBindingNode>(node.Members[1]);
            Assert.Equal("border", grouped.GroupName);
            Assert.Equal("Layout", attached.AttachedTypeName);
            Assert.Equal("width", grouped.Bindings[0].PropertyName);
            Assert.Equal("fillWidth", attached.Bindings[0].PropertyName);
        }

        private interface IRectangleBuilder : IObjectBuilder
        {
            new IRectangleBuilder Id(string id);

            IRectangleBuilder Width(int value);

            IRectangleBuilder HeightBind(string expression);

            IRectangleBuilder OnClicked(string body);

            IRectangleBuilder Border(Action<IBorderBuilder> configure);

            IRectangleBuilder Layout(Action<ILayoutBuilder> configure);
        }

        private interface IBorderBuilder : IPropertyCollector
        {
            IBorderBuilder Width(int value);

            IBorderBuilder Color(string value);
        }

        private interface ILayoutBuilder : IPropertyCollector
        {
            ILayoutBuilder FillWidth(bool value);
        }
    }
}

#pragma warning restore IDE0058
