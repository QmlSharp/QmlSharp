using System.Collections.Immutable;
using QmlSharp.Core;
using QmlSharp.Dsl.Tests.Fixtures;
using QmlSharp.Qml.Ast;

#pragma warning disable IDE0058

namespace QmlSharp.Dsl.Tests.Runtime
{
    public sealed class RuntimeClosureCoverageTests
    {
        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void SetProperty_AllSupportedRuntimeValues_LowerToExpectedAstValues()
        {
            ObjectBuilder inlineBuilder = new("InlineChild");
            inlineBuilder.SetProperty("name", "fromBuilder");
            ObjectDefinitionNode inlineNode = new() { TypeName = "InlineNode" };
            BindingValue existingValue = QmlSharp.Qml.Ast.Values.Block("let x = 1;");

            ObjectBuilder builder = new("Item");
            builder
                .SetProperty("existing", existingValue)
                .SetProperty("builder", inlineBuilder)
                .SetProperty("node", inlineNode)
                .SetProperty("byte", (byte)1)
                .SetProperty("sbyte", (sbyte)-1)
                .SetProperty("short", (short)-2)
                .SetProperty("ushort", (ushort)2)
                .SetProperty("uint", 3U)
                .SetProperty("long", 4L)
                .SetProperty("ulong", 5UL)
                .SetProperty("float", 1.25F)
                .SetProperty("double", 2.5D)
                .SetProperty("decimal", 3.75M)
                .SetProperty("rgba", new QmlColor { R = 1, G = 2, B = 3, A = 4 })
                .SetProperty("rgb", new QmlColor { R = 5, G = 6, B = 7 })
                .SetProperty("emptyColor", new QmlColor())
                .SetProperty("point", new QmlPoint(1, 2))
                .SetProperty("size", new QmlSize(3, 4))
                .SetProperty("rect", new QmlRect(5, 6, 7, 8))
                .SetProperty("vector2", new Vector2(9, 10))
                .SetProperty("vector3", new Vector3(11, 12, 13))
                .SetProperty("vector4", new Vector4(14, 15, 16, 17))
                .SetProperty("quaternion", new Quaternion(1, 2, 3, 4))
                .SetProperty("matrix", new Matrix4x4(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16))
                .SetProperty("font", new QmlFont
                {
                    Family = "Inter",
                    PointSize = 12.5,
                    PixelSize = 16,
                    Weight = 700,
                    Bold = true,
                    Italic = false,
                    Underline = true,
                    Strikeout = false,
                })
                .SetProperty("enumValue", HorizontalAlignment.AlignRight)
                .SetProperty("array", new object?[] { 1, "two", null })
                .SetProperty("jsonFallback", new { Count = 3, Label = "ok" });

            ImmutableArray<AstNode> members = builder.Build().Members;

            Assert.Same(existingValue, BindingValueAt<ScriptBlock>(members, "existing"));
            Assert.Equal("InlineChild", BindingValueAt<ObjectValue>(members, "builder").Object.TypeName);
            Assert.Equal("InlineNode", BindingValueAt<ObjectValue>(members, "node").Object.TypeName);
            Assert.Equal(1, BindingValueAt<NumberLiteral>(members, "byte").Value);
            Assert.Equal(-1, BindingValueAt<NumberLiteral>(members, "sbyte").Value);
            Assert.Equal(-2, BindingValueAt<NumberLiteral>(members, "short").Value);
            Assert.Equal(2, BindingValueAt<NumberLiteral>(members, "ushort").Value);
            Assert.Equal(3, BindingValueAt<NumberLiteral>(members, "uint").Value);
            Assert.Equal(4, BindingValueAt<NumberLiteral>(members, "long").Value);
            Assert.Equal(5, BindingValueAt<NumberLiteral>(members, "ulong").Value);
            Assert.Equal(1.25, BindingValueAt<NumberLiteral>(members, "float").Value);
            Assert.Equal(2.5, BindingValueAt<NumberLiteral>(members, "double").Value);
            Assert.Equal(3.75, BindingValueAt<NumberLiteral>(members, "decimal").Value);
            Assert.Equal("#01020304", BindingValueAt<StringLiteral>(members, "rgba").Value);
            Assert.Equal("#050607FF", BindingValueAt<StringLiteral>(members, "rgb").Value);
            Assert.IsType<NullLiteral>(BindingValueAt<NullLiteral>(members, "emptyColor"));
            Assert.Equal("Qt.point(1, 2)", BindingValueAt<ScriptExpression>(members, "point").Code);
            Assert.Equal("Qt.size(3, 4)", BindingValueAt<ScriptExpression>(members, "size").Code);
            Assert.Equal("Qt.rect(5, 6, 7, 8)", BindingValueAt<ScriptExpression>(members, "rect").Code);
            Assert.Equal("Qt.vector2d(9, 10)", BindingValueAt<ScriptExpression>(members, "vector2").Code);
            Assert.Equal("Qt.vector3d(11, 12, 13)", BindingValueAt<ScriptExpression>(members, "vector3").Code);
            Assert.Equal("Qt.vector4d(14, 15, 16, 17)", BindingValueAt<ScriptExpression>(members, "vector4").Code);
            Assert.Equal("Qt.quaternion(1, 2, 3, 4)", BindingValueAt<ScriptExpression>(members, "quaternion").Code);
            Assert.Contains("Qt.matrix4x4(1, 2, 3, 4", BindingValueAt<ScriptExpression>(members, "matrix").Code, StringComparison.Ordinal);
            Assert.Contains("family: \"Inter\"", BindingValueAt<ScriptExpression>(members, "font").Code, StringComparison.Ordinal);
            Assert.Equal("HorizontalAlignment", BindingValueAt<EnumReference>(members, "enumValue").TypeName);
            Assert.Equal("AlignRight", BindingValueAt<EnumReference>(members, "enumValue").MemberName);
            Assert.Equal(3, BindingValueAt<ArrayValue>(members, "array").Elements.Length);
            Assert.Contains("\"Count\":3", BindingValueAt<ScriptExpression>(members, "jsonFallback").Code, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void QmlEnum_FromGeneratedEnum_ReturnsRuntimeEnumToken()
        {
            QmlEnumToken token = QmlEnum.From("Text", "HorizontalAlignment", HorizontalAlignment.AlignLeft);

            Assert.Equal("Text", token.OwnerTypeName);
            Assert.Equal("HorizontalAlignment", token.EnumName);
            Assert.Equal("AlignLeft", token.MemberName);
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void ObjectProxy_MetadataRestrictions_RejectUnsupportedGeneratedCalls()
        {
            ObjectBuilderMetadata metadata = new(
                ImmutableArray.Create(
                    new PropertyMethodMetadata("Width", "width", SupportsValue: false),
                    new PropertyMethodMetadata("Height", "height", SupportsBinding: false)),
                ImmutableArray<GroupedPropertyMethodMetadata>.Empty,
                ImmutableArray<AttachedPropertyMethodMetadata>.Empty,
                ImmutableArray<SignalMethodMetadata>.Empty);
            IRestrictedBuilder builder = ObjectFactory.Create<IRestrictedBuilder>("Rectangle", metadata);

            InvalidOperationException valueException = Assert.Throws<InvalidOperationException>(() => builder.Width(10));
            InvalidOperationException bindingException = Assert.Throws<InvalidOperationException>(() => builder.HeightBind("parent.height"));
            MissingMethodException missingException = Assert.Throws<MissingMethodException>(() => builder.Unsupported());

            Assert.Contains("does not support literal values", valueException.Message, StringComparison.Ordinal);
            Assert.Contains("does not support expression bindings", bindingException.Message, StringComparison.Ordinal);
            Assert.Contains("Unsupported", missingException.Message, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void CollectorProxy_MetadataRestrictions_RejectUnsupportedGeneratedCalls()
        {
            PropertyCollectorMetadata metadata = new(
                ImmutableArray.Create(
                    new PropertyMethodMetadata("Width", "width", SupportsValue: false),
                    new PropertyMethodMetadata("Height", "height", SupportsBinding: false)),
                ImmutableArray.Create(new SignalMethodMetadata("OnClicked", "onClicked")));
            IRestrictedCollector collector = PropertyCollectorFactory.Create<IRestrictedCollector>(metadata);

            InvalidOperationException valueException = Assert.Throws<InvalidOperationException>(() => collector.Width(10));
            InvalidOperationException bindingException = Assert.Throws<InvalidOperationException>(() => collector.HeightBind("parent.height"));
            MissingMethodException missingProperty = Assert.Throws<MissingMethodException>(() => collector.Color("red"));
            MissingMethodException missingSignal = Assert.Throws<MissingMethodException>(() => collector.OnPressed("pressed()"));

            Assert.Contains("does not support literal values", valueException.Message, StringComparison.Ordinal);
            Assert.Contains("does not support expression bindings", bindingException.Message, StringComparison.Ordinal);
            Assert.Contains("Color", missingProperty.Message, StringComparison.Ordinal);
            Assert.Contains("OnPressed", missingSignal.Message, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void ObjectProxy_BaseBuilderMethods_DispatchThroughRuntimeProxy()
        {
            IObjectBuilder builder = ObjectFactory.Create<IProxyBuilder>("Item");

            IObjectBuilder result = builder
                .Id("root")
                .SetProperty("width", 100)
                .SetBinding("height", "parent.height")
                .AddGrouped("border", border => border.SetProperty("width", 2))
                .AddAttached("Layout", layout => layout.SetProperty("fillWidth", true))
                .HandleSignal("onCompleted", "ready()")
                .Child(new ObjectBuilder("A"))
                .Children(new ObjectBuilder("B"), new ObjectBuilder("C"));
            ObjectDefinitionNode node = builder.Build();

            Assert.Same(builder, result);
            Assert.Equal("Item", builder.QmlTypeName);
            Assert.Collection(
                node.Members,
                member => Assert.IsType<IdAssignmentNode>(member),
                member => Assert.Equal("width", Assert.IsType<BindingNode>(member).PropertyName),
                member => Assert.Equal("height", Assert.IsType<BindingNode>(member).PropertyName),
                member => Assert.Equal("border", Assert.IsType<GroupedBindingNode>(member).GroupName),
                member => Assert.Equal("Layout", Assert.IsType<AttachedBindingNode>(member).AttachedTypeName),
                member => Assert.Equal("onCompleted", Assert.IsType<SignalHandlerNode>(member).HandlerName),
                member => Assert.Equal("A", Assert.IsType<ObjectDefinitionNode>(member).TypeName),
                member => Assert.Equal("B", Assert.IsType<ObjectDefinitionNode>(member).TypeName),
                member => Assert.Equal("C", Assert.IsType<ObjectDefinitionNode>(member).TypeName));
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void CollectorProxy_BaseCollectorMethods_DispatchThroughRuntimeProxy()
        {
            IPropertyCollector collector = PropertyCollectorFactory.Create<IRestrictedCollector>(PropertyCollectorMetadata.Empty);

            IPropertyCollector result = collector
                .SetProperty("width", 100)
                .SetBinding("height", "parent.height")
                .HandleSignal("onClicked", "clicked()");

            Assert.Same(collector, result);
            Assert.Collection(
                collector.Entries,
                entry => Assert.Equal("width", entry.PropertyName),
                entry => Assert.Equal("height", entry.PropertyName),
                entry => Assert.Equal("onClicked", entry.PropertyName));
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void SetProperty_DefaultFont_LowersToEmptyQtFontExpression()
        {
            ObjectBuilder builder = new("Text");

            builder.SetProperty("font", new QmlFont());

            Assert.Equal("Qt.font({})", BindingValueAt<ScriptExpression>(builder.Build().Members, "font").Code);
        }

        private static T BindingValueAt<T>(ImmutableArray<AstNode> members, string propertyName)
            where T : BindingValue
        {
            BindingNode binding = members
                .OfType<BindingNode>()
                .Single(node => StringComparer.Ordinal.Equals(node.PropertyName, propertyName));
            return Assert.IsType<T>(binding.Value);
        }

        private enum HorizontalAlignment
        {
            AlignLeft,
            AlignRight,
        }

        private interface IRestrictedBuilder : IObjectBuilder
        {
            IRestrictedBuilder Width(int value);

            IRestrictedBuilder HeightBind(string expression);

            IRestrictedBuilder Unsupported();
        }

        private interface IProxyBuilder : IObjectBuilder
        {
        }

        private interface IRestrictedCollector : IPropertyCollector
        {
            IRestrictedCollector Width(int value);

            IRestrictedCollector HeightBind(string expression);

            IRestrictedCollector Color(string value);

            IRestrictedCollector OnPressed(string body);
        }
    }
}

#pragma warning restore IDE0058
