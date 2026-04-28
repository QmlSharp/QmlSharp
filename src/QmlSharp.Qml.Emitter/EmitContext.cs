using QmlSharp.Qml.Ast;

namespace QmlSharp.Qml.Emitter
{
    /// <summary>
    /// Per-emission state container for the writer, options, and optional source map.
    /// </summary>
    internal sealed class EmitContext
    {
        private readonly Dictionary<AstNode, (int Line, int Column)> _openNodes = new(ReferenceEqualityComparer.Instance);

        internal EmitContext(ResolvedEmitOptions options, SourceMapImpl? sourceMap = null, int initialIndentLevel = 0)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Writer = new QmlWriter(Options, initialIndentLevel);
            SourceMap = sourceMap;
        }

        internal QmlWriter Writer { get; }

        internal ResolvedEmitOptions Options { get; }

        internal SourceMapImpl? SourceMap { get; }

        internal string OptionalSemicolonSuffix => Options.OptionalSemicolonSuffix;

        internal string GetSemicolonSuffix(bool syntacticallyRequired = false)
        {
            return Options.GetSemicolonSuffix(syntacticallyRequired);
        }

        internal void BeginNode(AstNode node)
        {
            ArgumentNullException.ThrowIfNull(node);

            if (SourceMap is null)
            {
                return;
            }

            _openNodes[node] = Writer.GetPosition();
        }

        internal void EndNode(AstNode node)
        {
            ArgumentNullException.ThrowIfNull(node);

            if (SourceMap is null)
            {
                return;
            }

            if (!_openNodes.Remove(node, out (int Line, int Column) start))
            {
                throw new InvalidOperationException("Cannot end a source-map node that was not started.");
            }

            SourceMap.AddEntry(node, Writer.GetSpanFrom(start));
        }
    }
}
