using System.Collections.Immutable;
using QmlSharp.Qml.Ast;

namespace QmlSharp.Qml.Emitter
{
    internal sealed class SourceMapImpl : ISourceMap
    {
        private readonly Dictionary<AstNode, SourceMapEntry> _byNode = new(ReferenceEqualityComparer.Instance);
        private readonly List<SourceMapEntry> _entries = [];
        private bool _entriesNeedSort;

        public IReadOnlyList<SourceMapEntry> Entries
        {
            get
            {
                EnsureEntriesSorted();
                return _entries;
            }
        }

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

            EnsureEntriesSorted();
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
            EnsureEntriesSorted();
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
            EnsureEntriesSorted();
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

            ValidateOutputSpan(span);
            ValidateSourceSpan(node.Span);

            SourceMapEntry entry = new()
            {
                Node = node,
                NodeKind = node.Kind.ToString(),
                SourceSpan = node.Span,
                OutputSpan = span,
            };

            _byNode[node] = entry;
            _entries.Add(entry);
            _entriesNeedSort = true;
        }

        private void EnsureEntriesSorted()
        {
            if (!_entriesNeedSort)
            {
                return;
            }

            _entries.Sort(CompareOutputOrder);
            _entriesNeedSort = false;
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

        private static void ValidateOutputSpan(OutputSpan span)
        {
            if (span.StartLine <= 0)
            {
                throw new InvalidOperationException("Source map output spans must use 1-based start lines.");
            }

            if (span.StartColumn <= 0)
            {
                throw new InvalidOperationException("Source map output spans must use 1-based start columns.");
            }

            if (span.EndLine <= 0)
            {
                throw new InvalidOperationException("Source map output spans must use 1-based end lines.");
            }

            if (span.EndColumn <= 0)
            {
                throw new InvalidOperationException("Source map output spans must use 1-based end columns.");
            }

            if (span.EndLine < span.StartLine
                || (span.EndLine == span.StartLine && span.EndColumn < span.StartColumn))
            {
                throw new InvalidOperationException("Source map output spans must end at or after their start position.");
            }
        }

        private static void ValidateSourceSpan(SourceSpan? span)
        {
            if (span is null)
            {
                return;
            }

            if (span.Start.Line <= 0 || span.Start.Column <= 0 || span.Start.Offset < 0)
            {
                throw new InvalidOperationException("Source map source spans must use 1-based start line/column and non-negative offsets.");
            }

            if (span.End.Line <= 0 || span.End.Column <= 0 || span.End.Offset < 0)
            {
                throw new InvalidOperationException("Source map source spans must use 1-based end line/column and non-negative offsets.");
            }

            if (span.End.Line < span.Start.Line
                || (span.End.Line == span.Start.Line && span.End.Column < span.Start.Column)
                || span.End.Offset < span.Start.Offset)
            {
                throw new InvalidOperationException("Source map source spans must end at or after their start position.");
            }
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
