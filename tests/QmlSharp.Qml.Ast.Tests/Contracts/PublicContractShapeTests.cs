using QmlSharp.Qml.Ast.Tests.Helpers;
using QmlSharp.Qml.Ast.Traversal;

namespace QmlSharp.Qml.Ast.Tests.Contracts
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class PublicContractShapeTests
    {
        [Fact]
        public void ASTC_08_Source_position_and_span_are_readonly_value_contracts()
        {
            Assert.True(typeof(SourcePosition).IsValueType);
            Assert.True(typeof(SourceSpan).IsValueType);
            Assert.True(typeof(SourcePosition).IsAssignableTo(typeof(IEquatable<SourcePosition>)));
            Assert.True(typeof(SourceSpan).IsAssignableTo(typeof(IEquatable<SourceSpan>)));

            SourcePosition start = new(1, 1, 0);
            SourcePosition end = new(1, 5, 4);
            SourceSpan span = new(start, end);

            Assert.Equal(start, span.Start);
            Assert.Equal(end, span.End);
        }

        [Fact]
        public void ASTC_09_Walker_context_exposes_immutable_path_without_interface_boxing()
        {
            QmlDocument document = new();
            ImmutableArray<AstNode> path = [document];
            WalkerContext context = new(path, document, depth: 1);

            Assert.Equal(typeof(ImmutableArray<AstNode>), typeof(WalkerContext).GetProperty(nameof(WalkerContext.Path))!.PropertyType);
            Assert.Equal(path, context.Path);
            Assert.Equal(document, context.Parent);
            Assert.Equal(1, context.Depth);
        }

        [Fact]
        public void ASTC_10_Ast_diagnostic_preserves_optional_span_and_node_contracts()
        {
            SourceSpan span = new(new SourcePosition(2, 3, 10), new SourcePosition(2, 8, 15));
            QmlDocument document = new();

            AstDiagnostic diagnostic = new()
            {
                Code = DiagnosticCode.E001_DuplicateId,
                Message = "Duplicate id 'root'.",
                Severity = DiagnosticSeverity.Error,
                Span = span,
                Node = document,
            };

            Assert.Equal(span, diagnostic.Span);
            Assert.Equal(document, diagnostic.Node);
        }
    }
}
