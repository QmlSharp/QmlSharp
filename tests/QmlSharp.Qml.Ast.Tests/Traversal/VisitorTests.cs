using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Tests.Helpers;
using QmlSharp.Qml.Ast.Traversal;

namespace QmlSharp.Qml.Ast.Tests.Traversal
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class VisitorTests
    {
        [Fact]
        public void VI_01_Visitor_collects_all_ObjectDefinition_type_names()
        {
            QmlDocument document = CreateComplexDocument();
            List<string> typeNames = [];
            RecordingVisitor visitor = new()
            {
                ObjectDefinitionHandler = node =>
                {
                    typeNames.Add(node.TypeName);
                    return true;
                },
            };

            visitor.Accept(document);

            Assert.Equal(["Column", "Text", "Rectangle", "MouseArea"], typeNames);
        }

        [Fact]
        public void VI_02_Visitor_collects_all_Binding_property_names()
        {
            QmlDocument document = CreateBindingValueDocument();
            List<string> propertyNames = [];
            RecordingVisitor visitor = new()
            {
                BindingHandler = node =>
                {
                    propertyNames.Add(node.PropertyName);
                    return true;
                },
            };

            visitor.Accept(document);

            Assert.Equal(["width", "font", "pixelSize", "palette", "role", "enabled", "text"], propertyNames);
        }

        [Fact]
        public void VI_03_Returning_false_from_VisitObjectDefinition_skips_subtree()
        {
            QmlDocument document = CreateComplexDocument();
            List<string> typeNames = [];
            RecordingVisitor visitor = new()
            {
                ObjectDefinitionHandler = node =>
                {
                    typeNames.Add(node.TypeName);
                    return node.TypeName != "Rectangle";
                },
            };

            visitor.Accept(document);

            Assert.Equal(["Column", "Text", "Rectangle"], typeNames);
            Assert.DoesNotContain("MouseArea", typeNames);
        }

        [Fact]
        public void VI_04_Partial_visitor_overriding_only_binding_visit_is_supported()
        {
            QmlDocument document = AstFixtures.FullSyntaxDocument();
            List<string> propertyNames = [];
            BindingOnlyVisitor visitor = new(propertyNames);

            visitor.Accept(document);

            Assert.NotEmpty(propertyNames);
            Assert.Contains("width", propertyNames);
            Assert.Contains("name", propertyNames);
        }

        [Fact]
        public void VI_05_Empty_document_visits_only_document_and_root_object()
        {
            QmlDocument document = AstFixtures.MinimalDocument();
            List<NodeKind> visitedKinds = [];
            RecordingVisitor visitor = new()
            {
                NodeHandler = node =>
                {
                    visitedKinds.Add(node.Kind);
                    return true;
                },
            };

            visitor.Accept(document);

            Assert.Equal([NodeKind.Document, NodeKind.ObjectDefinition], visitedKinds);
        }

        [Fact]
        public void VI_06_Deeply_nested_tree_is_visited_in_depth_first_order()
        {
            QmlDocument document = CreateDeepDocument();
            List<string> typeNames = [];
            RecordingVisitor visitor = new()
            {
                ObjectDefinitionHandler = node =>
                {
                    typeNames.Add(node.TypeName);
                    return true;
                },
            };

            visitor.Accept(document);

            Assert.Equal(["A", "B", "C", "D", "E"], typeNames);
        }

        [Fact]
        public void VI_07_Full_syntax_document_visits_every_NodeKind()
        {
            QmlDocument document = AstFixtures.FullSyntaxDocument();
            HashSet<NodeKind> visitedKinds = [];
            RecordingVisitor visitor = new()
            {
                NodeHandler = node =>
                {
                    _ = visitedKinds.Add(node.Kind);
                    return true;
                },
            };

            visitor.Accept(document);

            ImmutableArray<NodeKind> allKinds = [.. Enum.GetValues<NodeKind>()];
            foreach (NodeKind nodeKind in allKinds)
            {
                Assert.Contains(nodeKind, visitedKinds);
            }
        }

        [Fact]
        public void Visitor_visits_comments_including_leading_and_trailing_comments()
        {
            CommentNode leadingComment = new() { Text = "// leading", IsBlock = false };
            CommentNode trailingComment = new() { Text = "// trailing", IsBlock = false };
            BindingNode binding = new()
            {
                PropertyName = "width",
                Value = Values.Number(100),
                LeadingComments = [leadingComment],
                TrailingComment = trailingComment,
            };
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members = [binding, new CommentNode { Text = "// member", IsBlock = false }],
                },
            };

            List<string> comments = [];
            RecordingVisitor visitor = new()
            {
                CommentHandler = node =>
                {
                    comments.Add(node.Text);
                    return true;
                },
            };

            visitor.Accept(document);

            Assert.Contains("// leading", comments);
            Assert.Contains("// trailing", comments);
            Assert.Contains("// member", comments);
        }

        [Fact]
        public void Visitor_traverses_object_and_array_values_inside_bindings()
        {
            QmlDocument document = CreateBindingValueDocument();
            List<string> typeNames = [];
            RecordingVisitor visitor = new()
            {
                ObjectDefinitionHandler = node =>
                {
                    typeNames.Add(node.TypeName);
                    return true;
                },
            };

            visitor.Accept(document);

            Assert.Contains("Font", typeNames);
            Assert.Contains("PaletteItem", typeNames);
            Assert.Contains("NestedItem", typeNames);
        }

        private static QmlDocument CreateComplexDocument()
        {
            return new QmlDocumentBuilder()
                .AddModuleImport("QtQuick", "2.15")
                .SetRootObject("Column", root =>
                {
                    _ = root.Id("root")
                        .Child("Text", text =>
                        {
                            _ = text.Id("label")
                                .Binding("text", Values.String("hello"));
                        })
                        .Child("Rectangle", rectangle =>
                        {
                            _ = rectangle.Id("rect")
                                .Child("MouseArea", area =>
                                {
                                    _ = area.Id("area");
                                });
                        });
                })
                .Build();
        }

        private static QmlDocument CreateDeepDocument()
        {
            return new QmlDocumentBuilder()
                .SetRootObject("A", a =>
                {
                    _ = a.Child("B", b =>
                    {
                        _ = b.Child("C", c =>
                        {
                            _ = c.Child("D", d =>
                            {
                                _ = d.Child("E");
                            });
                        });
                    });
                })
                .Build();
        }

        private static QmlDocument CreateBindingValueDocument()
        {
            return new QmlDocumentBuilder()
                .SetRootObject("Item", root =>
                {
                    _ = root.Binding("width", Values.Number(100))
                        .Binding("font", Values.Object("Font", font =>
                        {
                            _ = font.Binding("pixelSize", Values.Number(12));
                        }))
                        .Binding("palette", Values.Array(
                            Values.Object("PaletteItem", item =>
                            {
                                _ = item.Binding("role", Values.String("Window"));
                            }),
                            Values.Array(
                                Values.Object("NestedItem", nested =>
                                {
                                    _ = nested.Binding("enabled", Values.Boolean(true));
                                }))))
                        .Child("Text", text =>
                        {
                            _ = text.Binding("text", Values.String("hello"));
                        });
                })
                .Build();
        }

        private sealed class BindingOnlyVisitor : QmlAstVisitor
        {
            private readonly List<string> _propertyNames;

            public BindingOnlyVisitor(List<string> propertyNames)
            {
                _propertyNames = propertyNames;
            }

            public override bool VisitBinding(BindingNode node)
            {
                _propertyNames.Add(node.PropertyName);
                return true;
            }
        }

        private sealed class RecordingVisitor : QmlAstVisitor
        {
            public Func<AstNode, bool>? NodeHandler { get; init; }

            public Func<QmlDocument, bool>? DocumentHandler { get; init; }

            public Func<ObjectDefinitionNode, bool>? ObjectDefinitionHandler { get; init; }

            public Func<BindingNode, bool>? BindingHandler { get; init; }

            public Func<CommentNode, bool>? CommentHandler { get; init; }

            public override bool Visit(AstNode node)
            {
                if (NodeHandler is not null && !NodeHandler(node))
                {
                    return false;
                }

                return base.Visit(node);
            }

            public override bool VisitDocument(QmlDocument node) =>
                DocumentHandler?.Invoke(node) ?? base.VisitDocument(node);

            public override bool VisitObjectDefinition(ObjectDefinitionNode node) =>
                ObjectDefinitionHandler?.Invoke(node) ?? base.VisitObjectDefinition(node);

            public override bool VisitBinding(BindingNode node) =>
                BindingHandler?.Invoke(node) ?? base.VisitBinding(node);

            public override bool VisitComment(CommentNode node) =>
                CommentHandler?.Invoke(node) ?? base.VisitComment(node);
        }
    }
}
