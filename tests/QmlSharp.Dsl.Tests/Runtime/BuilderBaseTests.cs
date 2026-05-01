using QmlSharp.Dsl.Tests.Fixtures;
using QmlSharp.Qml.Ast;

#pragma warning disable IDE0058

namespace QmlSharp.Dsl.Tests.Runtime
{
    public sealed class BuilderBaseTests
    {
        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void Constructor_SetsQmlTypeName()
        {
            ObjectBuilder builder = new("Rectangle");

            Assert.Equal("Rectangle", builder.QmlTypeName);
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void Id_AddsIdAssignmentAndReturnsSameBuilder()
        {
            ObjectBuilder builder = new("Rectangle");

            IObjectBuilder result = builder.Id("myRect");
            ObjectDefinitionNode node = builder.Build();

            Assert.Same(builder, result);
            IdAssignmentNode id = Assert.IsType<IdAssignmentNode>(Assert.Single(node.Members));
            Assert.Equal("myRect", id.Id);
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void Child_AddsChildObjectAndReturnsSameBuilder()
        {
            ObjectBuilder parent = new("Column");
            ObjectBuilder child = new("Rectangle");

            IObjectBuilder result = parent.Child(child);
            ObjectDefinitionNode node = parent.Build();

            Assert.Same(parent, result);
            ObjectDefinitionNode childNode = Assert.IsType<ObjectDefinitionNode>(Assert.Single(node.Members));
            Assert.Equal("Rectangle", childNode.TypeName);
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void SetProperty_Primitives_CreateExpectedBindingValues()
        {
            ObjectBuilder builder = new("Rectangle");

            builder
                .SetProperty("width", 100)
                .SetProperty("visible", true)
                .SetProperty("color", "red")
                .SetProperty("model", null);
            ObjectDefinitionNode node = builder.Build();

            Assert.Equal(4, node.Members.Length);
            AssertBinding<NumberLiteral>(node, 0, "width");
            AssertBinding<BooleanLiteral>(node, 1, "visible");
            AssertBinding<StringLiteral>(node, 2, "color");
            AssertBinding<NullLiteral>(node, 3, "model");
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void SetBinding_AddsScriptExpressionBinding()
        {
            ObjectBuilder builder = new("Rectangle");

            builder.SetBinding("width", "parent.width");
            ObjectDefinitionNode node = builder.Build();

            BindingNode binding = AssertBinding<ScriptExpression>(node, 0, "width");
            ScriptExpression expression = Assert.IsType<ScriptExpression>(binding.Value);
            Assert.Equal("parent.width", expression.Code);
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void QmlEnumToken_AddsEnumReferenceBinding()
        {
            ObjectBuilder builder = new("Text");
            QmlEnumToken token = QmlEnum.Create("Text", "HorizontalAlignment", "AlignLeft");

            builder.SetProperty("horizontalAlignment", token);
            ObjectDefinitionNode node = builder.Build();

            BindingNode binding = AssertBinding<EnumReference>(node, 0, "horizontalAlignment");
            EnumReference enumReference = Assert.IsType<EnumReference>(binding.Value);
            Assert.Equal("Text", enumReference.TypeName);
            Assert.Equal("AlignLeft", enumReference.MemberName);
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void HandleSignal_AddsBlockSignalHandler()
        {
            ObjectBuilder builder = new("MouseArea");

            builder.HandleSignal("onClicked", "console.log(\"clicked\")");
            ObjectDefinitionNode node = builder.Build();

            SignalHandlerNode handler = Assert.IsType<SignalHandlerNode>(Assert.Single(node.Members));
            Assert.Equal("onClicked", handler.HandlerName);
            Assert.Equal(SignalHandlerForm.Block, handler.Form);
            Assert.Equal("console.log(\"clicked\")", handler.Code);
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void AddGrouped_AddsGroupedBindingsInOrder()
        {
            ObjectBuilder builder = new("Rectangle");

            builder.AddGrouped("border", border => border.SetProperty("width", 2).SetProperty("color", "black"));
            ObjectDefinitionNode node = builder.Build();

            GroupedBindingNode grouped = Assert.IsType<GroupedBindingNode>(Assert.Single(node.Members));
            Assert.Equal("border", grouped.GroupName);
            Assert.Collection(
                grouped.Bindings,
                binding => Assert.Equal("width", binding.PropertyName),
                binding => Assert.Equal("color", binding.PropertyName));
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void AddAttached_AddsAttachedBindings()
        {
            ObjectBuilder builder = new("Rectangle");

            builder.AddAttached("Layout", layout => layout.SetProperty("fillWidth", true));
            ObjectDefinitionNode node = builder.Build();

            AttachedBindingNode attached = Assert.IsType<AttachedBindingNode>(Assert.Single(node.Members));
            Assert.Equal("Layout", attached.AttachedTypeName);
            BindingNode binding = Assert.Single(attached.Bindings);
            Assert.Equal("fillWidth", binding.PropertyName);
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void Children_PreservesOrderingWithInterleavedChildCalls()
        {
            ObjectBuilder builder = new("Column");

            builder
                .Child(new ObjectBuilder("A"))
                .Children(new ObjectBuilder("B"), new ObjectBuilder("C"))
                .Child(new ObjectBuilder("D"));
            ObjectDefinitionNode node = builder.Build();

            Assert.Collection(
                node.Members,
                member => Assert.Equal("A", Assert.IsType<ObjectDefinitionNode>(member).TypeName),
                member => Assert.Equal("B", Assert.IsType<ObjectDefinitionNode>(member).TypeName),
                member => Assert.Equal("C", Assert.IsType<ObjectDefinitionNode>(member).TypeName),
                member => Assert.Equal("D", Assert.IsType<ObjectDefinitionNode>(member).TypeName));
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void Build_ProducesObjectDefinitionNodeWithMembersInCallOrder()
        {
            ObjectBuilder builder = new("Item");

            builder
                .Id("root")
                .SetProperty("width", 800)
                .SetProperty("height", 600)
                .Child(new ObjectBuilder("Rectangle").SetProperty("color", "red"));
            ObjectDefinitionNode node = builder.Build();

            Assert.Equal("Item", node.TypeName);
            Assert.Collection(
                node.Members,
                member => Assert.IsType<IdAssignmentNode>(member),
                member => Assert.Equal("width", Assert.IsType<BindingNode>(member).PropertyName),
                member => Assert.Equal("height", Assert.IsType<BindingNode>(member).PropertyName),
                member => Assert.Equal("Rectangle", Assert.IsType<ObjectDefinitionNode>(member).TypeName));
        }

        private static BindingNode AssertBinding<TValue>(ObjectDefinitionNode node, int index, string propertyName)
            where TValue : BindingValue
        {
            BindingNode binding = Assert.IsType<BindingNode>(node.Members[index]);
            Assert.Equal(propertyName, binding.PropertyName);
            Assert.IsType<TValue>(binding.Value);
            return binding;
        }
    }
}

#pragma warning restore IDE0058
