using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Options
{
    public sealed class EmitContextTests
    {
        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void Context_BeginAndEndNode_RecordStableSourceMapSpan()
        {
            ResolvedEmitOptions options = ResolvedEmitOptions.From(null);
            SourceMapImpl sourceMap = new();
            EmitContext context = new(options, sourceMap);
            ObjectDefinitionNode node = new() { TypeName = "Item" };

            context.BeginNode(node);
            context.Writer.Write("Item {}");
            context.EndNode(node);

            OutputSpan? span = sourceMap.GetOutputSpan(node);

            Assert.NotNull(span);
            Assert.Equal(1, span.StartLine);
            Assert.Equal(1, span.StartColumn);
            Assert.Equal(1, span.EndLine);
            Assert.Equal(7, span.EndColumn);
            Assert.Same(node, sourceMap.GetInnermostNodeAt(1, 3));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void Context_EndNodeWithoutBegin_ThrowsInvalidOperationException()
        {
            ResolvedEmitOptions options = ResolvedEmitOptions.From(null);
            EmitContext context = new(options, new SourceMapImpl());
            ObjectDefinitionNode node = new() { TypeName = "Item" };

            _ = Assert.Throws<InvalidOperationException>(() => context.EndNode(node));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void Context_InitialIndentLevel_ConfiguresWriterIndentation()
        {
            ResolvedEmitOptions options = ResolvedEmitOptions.From(new EmitOptions { IndentSize = 2 });
            EmitContext context = new(options, sourceMap: null, initialIndentLevel: 2);

            context.Writer.WriteLine("width: 100");

            Assert.Equal("    width: 100\n", context.Writer.GetOutput());
        }
    }
}
