using System.Collections.Immutable;
using QmlSharp.Qml.Ast;

namespace QmlSharp.Qml.Emitter
{
    internal sealed class SourceMapImpl : ISourceMap
    {
        private readonly Dictionary<AstNode, SourceMapEntry> _byNode = new(ReferenceEqualityComparer.Instance);
        private readonly List<SourceMapEntry> _entries = [];

        public IReadOnlyList<SourceMapEntry> Entries => _entries;

        public OutputSpan? GetOutputSpan(AstNode node)
        {
            ArgumentNullException.ThrowIfNull(node);

            return _byNode.TryGetValue(node, out SourceMapEntry? entry) ? entry.OutputSpan : null;
        }

        public IReadOnlyList<AstNode> GetNodesAtLine(int line)
        {
            if (line <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(line), line, "Line must be 1-based.");
            }

            return _entries
                .Where(entry => entry.OutputSpan.StartLine <= line && entry.OutputSpan.EndLine >= line)
                .Select(entry => entry.Node)
                .ToImmutableArray();
        }

        public AstNode? GetInnermostNodeAt(int line, int column)
        {
            if (line <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(line), line, "Line must be 1-based.");
            }

            if (column <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(column), column, "Column must be 1-based.");
            }

            SourceMapEntry? best = null;
            foreach (SourceMapEntry entry in _entries)
            {
                if (!Contains(entry.OutputSpan, line, column))
                {
                    continue;
                }

                if (best is null || SpanSize(entry.OutputSpan) <= SpanSize(best.OutputSpan))
                {
                    best = entry;
                }
            }

            return best?.Node;
        }

        public SourceMapJson ToJson()
        {
            ImmutableArray<SourceMapEntryJson>.Builder entries = ImmutableArray.CreateBuilder<SourceMapEntryJson>(_entries.Count);
            foreach (SourceMapEntry entry in _entries)
            {
                entries.Add(new SourceMapEntryJson
                {
                    NodeKind = entry.NodeKind,
                    SourceSpan = entry.SourceSpan,
                    Span = entry.OutputSpan,
                });
            }

            return new SourceMapJson { Entries = entries.ToImmutable() };
        }

        internal void AddEntry(AstNode node, OutputSpan span)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(span);

            SourceMapEntry entry = new()
            {
                Node = node,
                NodeKind = node.Kind.ToString(),
                SourceSpan = node.Span,
                OutputSpan = span,
            };

            _byNode[node] = entry;
            InsertInOutputOrder(entry);
        }

        private void InsertInOutputOrder(SourceMapEntry entry)
        {
            for (int index = 0; index < _entries.Count; index++)
            {
                if (CompareOutputOrder(entry, _entries[index]) < 0)
                {
                    _entries.Insert(index, entry);
                    return;
                }
            }

            _entries.Add(entry);
        }

        private static int CompareOutputOrder(SourceMapEntry left, SourceMapEntry right)
        {
            int startLine = left.OutputSpan.StartLine.CompareTo(right.OutputSpan.StartLine);
            if (startLine != 0)
            {
                return startLine;
            }

            int startColumn = left.OutputSpan.StartColumn.CompareTo(right.OutputSpan.StartColumn);
            if (startColumn != 0)
            {
                return startColumn;
            }

            int endLine = left.OutputSpan.EndLine.CompareTo(right.OutputSpan.EndLine);
            if (endLine != 0)
            {
                return endLine;
            }

            int endColumn = left.OutputSpan.EndColumn.CompareTo(right.OutputSpan.EndColumn);
            if (endColumn != 0)
            {
                return endColumn;
            }

            return string.Compare(left.NodeKind, right.NodeKind, StringComparison.Ordinal);
        }

        private static bool Contains(OutputSpan span, int line, int column)
        {
            if (line < span.StartLine || line > span.EndLine)
            {
                return false;
            }

            if (line == span.StartLine && column < span.StartColumn)
            {
                return false;
            }

            return line != span.EndLine || column <= span.EndColumn;
        }

        private static int SpanSize(OutputSpan span)
        {
            return ((span.EndLine - span.StartLine) * 10_000) + span.EndColumn - span.StartColumn;
        }
    }
}
