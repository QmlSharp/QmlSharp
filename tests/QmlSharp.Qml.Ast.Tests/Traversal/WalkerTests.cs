using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Tests.Helpers;
using QmlSharp.Qml.Ast.Traversal;

namespace QmlSharp.Qml.Ast.Tests.Traversal
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class WalkerTests
    {
        [Fact]
        public void WK_01_Enter_and_leave_callbacks_follow_depth_first_order()
        {
            QmlDocument document = CreateWalkerOrderDocument();
            List<string> events = [];

            QmlAstWalker.Walk(
                document,
                enter: (node, _) =>
                {
                    if (node is ObjectDefinitionNode objectNode)
                    {
                        events.Add($"enter:{objectNode.TypeName}");
                    }

                    return true;
                },
                leave: (node, _) =>
                {
                    if (node is ObjectDefinitionNode objectNode)
                    {
                        events.Add($"leave:{objectNode.TypeName}");
                    }
                });

            Assert.Equal(
                ["enter:Column", "enter:Text", "leave:Text", "enter:Rectangle", "leave:Rectangle", "leave:Column"],
                events);
        }

        [Fact]
        public void WK_02_Path_tracks_ancestors_from_root_to_parent()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .SetRootObject("Column", root =>
                {
                    _ = root.Child("Text");
                })
                .Build();

            ImmutableArray<AstNode> textPath = ImmutableArray<AstNode>.Empty;

            QmlAstWalker.Walk(
                document,
                enter: (node, context) =>
                {
                    if (node is ObjectDefinitionNode objectNode && objectNode.TypeName == "Text")
                    {
                        textPath = context.Path;
                    }

                    return true;
                },
                leave: null);

            Assert.Equal(2, textPath.Length);
            _ = Assert.IsType<QmlDocument>(textPath[0]);
            Assert.Equal("Column", Assert.IsType<ObjectDefinitionNode>(textPath[1]).TypeName);
        }

        [Fact]
        public void WK_03_Parent_is_reported_as_immediate_parent()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .SetRootObject("Column", root =>
                {
                    _ = root.Child("Text");
                })
                .Build();

            AstNode? rootParent = null;
            AstNode? textParent = null;

            QmlAstWalker.Walk(
                document,
                enter: (node, context) =>
                {
                    if (node is ObjectDefinitionNode objectNode && objectNode.TypeName == "Column")
                    {
                        rootParent = context.Parent;
                    }

                    if (node is ObjectDefinitionNode childNode && childNode.TypeName == "Text")
                    {
                        textParent = context.Parent;
                    }

                    return true;
                },
                leave: null);

            _ = Assert.IsType<QmlDocument>(rootParent);
            Assert.Equal("Column", Assert.IsType<ObjectDefinitionNode>(textParent).TypeName);
        }

        [Fact]
        public void WK_04_Returning_false_from_enter_skips_children_and_leave_for_skipped_node()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .SetRootObject("Column", root =>
                {
                    _ = root.Child("Text", text =>
                    {
                        _ = text.Child("MouseArea");
                    });
                })
                .Build();

            List<string> entered = [];
            List<string> left = [];

            QmlAstWalker.Walk(
                document,
                enter: (node, _) =>
                {
                    if (node is ObjectDefinitionNode objectNode)
                    {
                        entered.Add(objectNode.TypeName);
                        return objectNode.TypeName != "Text";
                    }

                    return true;
                },
                leave: (node, _) =>
                {
                    if (node is ObjectDefinitionNode objectNode)
                    {
                        left.Add(objectNode.TypeName);
                    }
                });

            Assert.Equal(["Column", "Text"], entered);
            Assert.Equal(["Column"], left);
            Assert.DoesNotContain("MouseArea", entered);
            Assert.DoesNotContain("Text", left);
        }

        [Fact]
        public void WK_05_Depth_is_reported_correctly()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .SetRootObject("Column", root =>
                {
                    _ = root.Child("Text", text =>
                    {
                        _ = text.Child("MouseArea");
                    });
                })
                .Build();

            int documentDepth = -1;
            int columnDepth = -1;
            int textDepth = -1;
            int mouseAreaDepth = -1;

            QmlAstWalker.Walk(
                document,
                enter: (node, context) =>
                {
                    if (node is QmlDocument)
                    {
                        documentDepth = context.Depth;
                    }

                    if (node is ObjectDefinitionNode objectNode && objectNode.TypeName == "Column")
                    {
                        columnDepth = context.Depth;
                    }

                    if (node is ObjectDefinitionNode childNode && childNode.TypeName == "Text")
                    {
                        textDepth = context.Depth;
                    }

                    if (node is ObjectDefinitionNode grandChildNode && grandChildNode.TypeName == "MouseArea")
                    {
                        mouseAreaDepth = context.Depth;
                    }

                    return true;
                },
                leave: null);

            Assert.Equal(0, documentDepth);
            Assert.Equal(1, columnDepth);
            Assert.Equal(2, textDepth);
            Assert.Equal(3, mouseAreaDepth);
        }

        [Fact]
        public void WK_06_Walker_handles_empty_members_array()
        {
            QmlDocument document = AstFixtures.MinimalDocument();
            List<NodeKind> enteredKinds = [];
            List<NodeKind> leftKinds = [];

            QmlAstWalker.Walk(
                document,
                enter: (node, _) =>
                {
                    enteredKinds.Add(node.Kind);
                    return true;
                },
                leave: (node, _) =>
                {
                    leftKinds.Add(node.Kind);
                });

            Assert.Equal([NodeKind.Document, NodeKind.ObjectDefinition], enteredKinds);
            Assert.Equal([NodeKind.ObjectDefinition, NodeKind.Document], leftKinds);
        }

        [Fact]
        public void WK_07_Walker_visits_members_in_declaration_order()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .SetRootObject("Item", root =>
                {
                    _ = root.Binding("width", Values.Number(100))
                        .Comment("// member comment")
                        .PropertyDeclaration("count", "int", Values.Number(0))
                        .Child("Text");
                })
                .Build();

            List<NodeKind> rootMemberKinds = [];

            QmlAstWalker.Walk(
                document,
                enter: (node, context) =>
                {
                    if (context.Parent is ObjectDefinitionNode parentNode && parentNode.TypeName == "Item")
                    {
                        rootMemberKinds.Add(node.Kind);
                    }

                    return true;
                },
                leave: null);

            Assert.Equal(
                [NodeKind.Binding, NodeKind.Comment, NodeKind.PropertyDeclaration, NodeKind.ObjectDefinition],
                rootMemberKinds);
        }

        [Fact]
        public void Walker_visits_comments_including_leading_and_trailing_comments()
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

            QmlAstWalker.Walk(
                document,
                enter: (node, _) =>
                {
                    if (node is CommentNode commentNode)
                    {
                        comments.Add(commentNode.Text);
                    }

                    return true;
                },
                leave: null);

            Assert.Contains("// leading", comments);
            Assert.Contains("// trailing", comments);
            Assert.Contains("// member", comments);
        }

        [Fact]
        public void Walker_traverses_object_and_array_values_inside_bindings()
        {
            QmlDocument document = CreateBindingValueDocument();
            List<string> typeNames = [];

            QmlAstWalker.Walk(
                document,
                enter: (node, _) =>
                {
                    if (node is ObjectDefinitionNode objectNode)
                    {
                        typeNames.Add(objectNode.TypeName);
                    }

                    return true;
                },
                leave: null);

            Assert.Contains("Font", typeNames);
            Assert.Contains("PaletteItem", typeNames);
            Assert.Contains("NestedItem", typeNames);
        }

        [Fact]
        public void Walker_context_path_remains_stable_when_read_after_traversal()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .SetRootObject("Column", root =>
                {
                    _ = root.Child("Text");
                    _ = root.Child("Rectangle");
                })
                .Build();

            WalkerContext? textContext = null;
            WalkerContext? rectangleContext = null;

            QmlAstWalker.Walk(
                document,
                enter: (node, context) =>
                {
                    if (node is ObjectDefinitionNode objectNode && objectNode.TypeName == "Text")
                    {
                        textContext = context;
                    }

                    if (node is ObjectDefinitionNode siblingNode && siblingNode.TypeName == "Rectangle")
                    {
                        rectangleContext = context;
                    }

                    return true;
                },
                leave: null);

            Assert.NotNull(textContext);
            Assert.NotNull(rectangleContext);
            ImmutableArray<AstNode> textPath = textContext.Path;
            ImmutableArray<AstNode> rectanglePath = rectangleContext.Path;

            Assert.Equal(2, textPath.Length);
            Assert.Equal(2, rectanglePath.Length);
            _ = Assert.IsType<QmlDocument>(textPath[0]);
            Assert.Equal("Column", Assert.IsType<ObjectDefinitionNode>(textPath[1]).TypeName);
            _ = Assert.IsType<QmlDocument>(rectanglePath[0]);
            Assert.Equal("Column", Assert.IsType<ObjectDefinitionNode>(rectanglePath[1]).TypeName);
        }

        private static QmlDocument CreateWalkerOrderDocument()
        {
            return new QmlDocumentBuilder()
                .SetRootObject("Column", root =>
                {
                    _ = root.Child("Text");
                    _ = root.Child("Rectangle");
                })
                .Build();
        }

        private static QmlDocument CreateBindingValueDocument()
        {
            return new QmlDocumentBuilder()
                .SetRootObject("Item", root =>
                {
                    _ = root.Binding("font", Values.Object("Font", font =>
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
                                }))));
                })
                .Build();
        }
    }
}
