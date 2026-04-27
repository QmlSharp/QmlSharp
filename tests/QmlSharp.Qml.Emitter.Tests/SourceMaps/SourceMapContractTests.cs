using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.SourceMaps
{
    public sealed class SourceMapContractTests
    {
        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void EmitResult_CarriesTextAndSourceMap()
        {
            QmlDocument document = AstFixtureFactory.MinimalDocument();
            OutputSpan span = new()
            {
                StartLine = 1,
                StartColumn = 1,
                EndLine = 1,
                EndColumn = 7,
            };
            TestSourceMap sourceMap = new(
                [
                    new SourceMapEntry
                    {
                        Node = document.RootObject,
                        NodeKind = document.RootObject.Kind.ToString(),
                        OutputSpan = span,
                    },
                ]);
            EmitResult result = new()
            {
                Text = "Item {}",
                SourceMap = sourceMap,
            };

            Assert.Equal("Item {}", result.Text);
            Assert.Same(sourceMap, result.SourceMap);
            SourceMapAssert.ValidSpan(result.SourceMap.Entries[0].OutputSpan);
        }

        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void SourceMapJson_Dtos_AreImmutableArrayBackedAndSerializable()
        {
            OutputSpan span = new()
            {
                StartLine = 1,
                StartColumn = 1,
                EndLine = 2,
                EndColumn = 1,
            };
            SourceMapJson json = new()
            {
                Entries =
                [
                    new SourceMapEntryJson
                    {
                        NodeKind = NodeKind.ObjectDefinition.ToString(),
                        Span = span,
                    },
                ],
            };

            SourceMapEntryJson entry = Assert.Single(json.Entries);

            Assert.Equal("ObjectDefinition", entry.NodeKind);
            Assert.Same(span, entry.Span);
        }

        private sealed class TestSourceMap : ISourceMap
        {
            private readonly ImmutableArray<SourceMapEntry> _entries;

            public TestSourceMap(ImmutableArray<SourceMapEntry> entries)
            {
                _entries = entries;
            }

            public IReadOnlyList<SourceMapEntry> Entries => _entries;

            public OutputSpan? GetOutputSpan(AstNode node)
            {
                foreach (SourceMapEntry entry in _entries)
                {
                    if (ReferenceEquals(entry.Node, node))
                    {
                        return entry.OutputSpan;
                    }
                }

                return null;
            }

            public IReadOnlyList<AstNode> GetNodesAtLine(int line)
            {
                ImmutableArray<AstNode>.Builder nodes = ImmutableArray.CreateBuilder<AstNode>();

                foreach (SourceMapEntry entry in _entries)
                {
                    if (entry.OutputSpan.StartLine <= line && entry.OutputSpan.EndLine >= line)
                    {
                        nodes.Add(entry.Node);
                    }
                }

                return nodes.ToImmutable();
            }

            public AstNode? GetInnermostNodeAt(int line, int column)
            {
                foreach (SourceMapEntry entry in _entries)
                {
                    OutputSpan span = entry.OutputSpan;
                    bool contains = span.StartLine <= line
                        && span.EndLine >= line
                        && span.StartColumn <= column
                        && span.EndColumn >= column;

                    if (contains)
                    {
                        return entry.Node;
                    }
                }

                return null;
            }

            public SourceMapJson ToJson()
            {
                ImmutableArray<SourceMapEntryJson>.Builder entries = ImmutableArray.CreateBuilder<SourceMapEntryJson>();

                foreach (SourceMapEntry entry in _entries)
                {
                    entries.Add(
                        new SourceMapEntryJson
                        {
                            NodeKind = entry.NodeKind,
                            Span = entry.OutputSpan,
                        });
                }

                return new SourceMapJson
                {
                    Entries = entries.ToImmutable(),
                };
            }
        }
    }
}
