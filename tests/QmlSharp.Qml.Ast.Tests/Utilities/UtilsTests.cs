using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Tests.Helpers;
using QmlSharp.Qml.Ast.Utilities;

namespace QmlSharp.Qml.Ast.Tests.Utilities
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class UtilsTests
    {
        [Fact]
        public void UT_01_CollectIds_returns_all_ids_in_declaration_order()
        {
            QmlDocument document = CreateUtilityDocument();

            ImmutableArray<string> ids = QmlAstUtils.CollectIds(document);

            Assert.Equal(["root", "label", "subtitle", "box"], ids.ToArray());
        }

        [Fact]
        public void UT_02_CollectIds_on_document_with_no_ids_returns_empty_array()
        {
            QmlDocument document = AstFixtures.MinimalDocument();

            ImmutableArray<string> ids = QmlAstUtils.CollectIds(document);

            Assert.Empty(ids);
        }

        [Fact]
        public void UT_03_CollectTypeNames_returns_distinct_type_names_in_first_seen_order()
        {
            QmlDocument document = CreateUtilityDocument();

            ImmutableArray<string> typeNames = QmlAstUtils.CollectTypeNames(document);

            Assert.Equal(["Column", "string", "Text", "Rectangle", "MouseArea"], typeNames.ToArray());
        }

        [Fact]
        public void CollectTypeNames_includes_inline_component_body_and_property_declaration_types()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .SetRootObject("Item", root =>
                {
                    _ = root.PropertyDeclaration("model", "list<Person>")
                        .SignalDeclaration("selected", new ParameterDeclaration("person", "Person"))
                        .FunctionDeclaration("format", "{ return ''; }", "string", new ParameterDeclaration("value", "int"))
                        .InlineComponent("Badge", "Rectangle", badge =>
                        {
                            _ = badge.PropertyDeclaration("status", "Status")
                                .Child("Text");
                        });
                })
                .Build();

            ImmutableArray<string> typeNames = QmlAstUtils.CollectTypeNames(document);

            Assert.Equal(["Item", "list<Person>", "Person", "int", "string", "Badge", "Rectangle", "Status", "Text"], typeNames.ToArray());
        }

        [Fact]
        public void UT_04_FindObjectsByType_returns_all_matching_objects_in_traversal_order()
        {
            QmlDocument document = CreateUtilityDocument();

            ImmutableArray<ObjectDefinitionNode> textObjects = QmlAstUtils.FindObjectsByType(document, "Text");

            Assert.Equal(2, textObjects.Length);
            Assert.All(textObjects, objectNode => Assert.Equal("Text", objectNode.TypeName));
            Assert.Equal("label", QmlAstUtils.GetObjectId(textObjects[0]));
            Assert.Equal("subtitle", QmlAstUtils.GetObjectId(textObjects[1]));
        }

        [Fact]
        public void UT_05_FindObjectsByType_with_no_matches_returns_empty_array()
        {
            QmlDocument document = CreateUtilityDocument();

            ImmutableArray<ObjectDefinitionNode> matches = QmlAstUtils.FindObjectsByType(document, "Button");

            Assert.Empty(matches);
        }

        [Fact]
        public void UT_06_FindObjectById_returns_object_containing_the_id()
        {
            QmlDocument document = CreateUtilityDocument();

            ObjectDefinitionNode? match = QmlAstUtils.FindObjectById(document, "box");

            Assert.NotNull(match);
            Assert.Equal("Rectangle", match.TypeName);
        }

        [Fact]
        public void FindObjectById_finds_ids_through_nested_child_objects()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .SetRootObject("Column", root =>
                {
                    _ = root.Child("Rectangle", rectangle =>
                    {
                        _ = rectangle.Child("MouseArea", mouseArea =>
                        {
                            _ = mouseArea.Id("mouse");
                        });
                    });
                })
                .Build();

            ObjectDefinitionNode? match = QmlAstUtils.FindObjectById(document, "mouse");

            Assert.NotNull(match);
            Assert.Equal("MouseArea", match.TypeName);
        }

        [Fact]
        public void UT_07_FindObjectById_with_nonexistent_id_returns_null()
        {
            QmlDocument document = CreateUtilityDocument();

            ObjectDefinitionNode? match = QmlAstUtils.FindObjectById(document, "missing");

            Assert.Null(match);
        }

        [Fact]
        public void UT_08_GetBindingValue_retrieves_binding_value_by_property_name()
        {
            QmlDocument document = CreateUtilityDocument();

            BindingValue? value = QmlAstUtils.GetBindingValue(document.RootObject, "width");

            NumberLiteral numberLiteral = Assert.IsType<NumberLiteral>(value);
            Assert.Equal(400, numberLiteral.Value);
        }

        [Fact]
        public void GetBindingValue_returns_first_direct_ordinal_property_match()
        {
            ObjectDefinitionNode obj = new()
            {
                TypeName = "Item",
                Members =
                [
                    new BindingNode { PropertyName = "width", Value = Values.Number(100) },
                    new BindingNode { PropertyName = "Width", Value = Values.Number(200) },
                    new BindingNode { PropertyName = "width", Value = Values.Number(300) },
                ],
            };

            BindingValue? lowercaseValue = QmlAstUtils.GetBindingValue(obj, "width");
            BindingValue? uppercaseValue = QmlAstUtils.GetBindingValue(obj, "Width");

            NumberLiteral lowercaseNumber = Assert.IsType<NumberLiteral>(lowercaseValue);
            NumberLiteral uppercaseNumber = Assert.IsType<NumberLiteral>(uppercaseValue);
            Assert.Equal(100, lowercaseNumber.Value);
            Assert.Equal(200, uppercaseNumber.Value);
        }

        [Fact]
        public void UT_09_GetBindingValue_for_unbound_property_returns_null()
        {
            QmlDocument document = CreateUtilityDocument();

            BindingValue? value = QmlAstUtils.GetBindingValue(document.RootObject, "height");

            Assert.Null(value);
        }

        [Fact]
        public void UT_10_GetChildren_of_object_definition_returns_immediate_members()
        {
            QmlDocument document = CreateUtilityDocument();

            ImmutableArray<AstNode> children = QmlAstUtils.GetChildren(document.RootObject);

            Assert.Equal(
                [
                    NodeKind.IdAssignment,
                    NodeKind.Binding,
                    NodeKind.PropertyDeclaration,
                    NodeKind.ObjectDefinition,
                    NodeKind.ObjectDefinition,
                    NodeKind.ObjectDefinition,
                ],
                children.Select(child => child.Kind));
        }

        [Fact]
        public void UT_11_GetChildren_of_leaf_node_returns_empty_array()
        {
            IdAssignmentNode idAssignmentNode = new() { Id = "root" };

            ImmutableArray<AstNode> children = QmlAstUtils.GetChildren(idAssignmentNode);

            Assert.Empty(children);
        }

        [Fact]
        public void GetChildren_includes_comments_and_object_value_children_in_walker_order()
        {
            CommentNode leadingComment = new() { Text = "// leading", IsBlock = false };
            CommentNode trailingComment = new() { Text = "// trailing", IsBlock = false };
            BindingNode bindingNode = new()
            {
                PropertyName = "font",
                Value = Values.Object("Font", font =>
                {
                    _ = font.Binding("pixelSize", Values.Number(12));
                }),
                LeadingComments = [leadingComment],
                TrailingComment = trailingComment,
            };

            ImmutableArray<AstNode> children = QmlAstUtils.GetChildren(bindingNode);

            Assert.Equal(
                [NodeKind.Comment, NodeKind.ObjectDefinition, NodeKind.Comment],
                children.Select(child => child.Kind));
        }

        [Fact]
        public void UT_12_CountNodes_counts_all_nodes_including_document()
        {
            QmlDocument document = CreateUtilityDocument();

            int count = QmlAstUtils.CountNodes(document);

            Assert.Equal(17, count);
        }

        [Fact]
        public void UT_13_CountNodes_on_minimal_document_returns_document_and_root_object()
        {
            QmlDocument document = AstFixtures.MinimalDocument();

            int count = QmlAstUtils.CountNodes(document);

            Assert.Equal(2, count);
        }

        [Fact]
        public void UT_14_MaxDepth_on_flat_document_returns_root_object_depth()
        {
            QmlDocument document = AstFixtures.MinimalDocument();

            int depth = QmlAstUtils.MaxDepth(document);

            Assert.Equal(1, depth);
        }

        [Fact]
        public void UT_15_MaxDepth_on_nested_document_returns_deepest_object_depth()
        {
            QmlDocument document = CreateUtilityDocument();

            int depth = QmlAstUtils.MaxDepth(document);

            Assert.Equal(3, depth);
        }

        [Fact]
        public void Utils_full_syntax_fixture_exposes_expected_utility_invariants()
        {
            QmlDocument document = AstFixtures.FullSyntaxDocument();

            ImmutableArray<string> ids = QmlAstUtils.CollectIds(document);
            ImmutableArray<string> typeNames = QmlAstUtils.CollectTypeNames(document);
            ImmutableArray<string> modules = QmlAstUtils.CollectImportedModules(document);
            int nodeCount = QmlAstUtils.CountNodes(document);
            int depth = QmlAstUtils.MaxDepth(document);

            Assert.Contains("myRect", ids);
            Assert.Equal(["QtQuick"], modules.ToArray());
            Assert.True(typeNames.Length >= 8);
            Assert.True(nodeCount > 30);
            Assert.True(depth >= 4);
        }

        [Fact]
        public void UT_16_StructuralEqual_returns_true_for_same_tree_and_ignores_span_by_default()
        {
            QmlDocument left = CreateSpannedDocument(
                new SourceSpan(new SourcePosition(1, 1, 0), new SourcePosition(1, 10, 9)));
            QmlDocument right = CreateSpannedDocument(
                new SourceSpan(new SourcePosition(10, 1, 100), new SourcePosition(10, 10, 109)));

            bool equal = QmlAstUtils.StructuralEqual(left, right);

            Assert.True(equal);
        }

        [Fact]
        public void StructuralEqual_compares_spans_when_requested()
        {
            QmlDocument left = CreateSpannedDocument(
                new SourceSpan(new SourcePosition(1, 1, 0), new SourcePosition(1, 10, 9)));
            QmlDocument right = CreateSpannedDocument(
                new SourceSpan(new SourcePosition(10, 1, 100), new SourcePosition(10, 10, 109)));

            bool equal = QmlAstUtils.StructuralEqual(left, right, ignoreSpan: false);

            Assert.False(equal);
        }

        [Fact]
        public void StructuralEqual_compares_attached_comments()
        {
            QmlDocument left = CreateCommentedDocument("// leading");
            QmlDocument right = CreateCommentedDocument("// changed");

            bool equal = QmlAstUtils.StructuralEqual(left, right);

            Assert.False(equal);
        }

        [Fact]
        public void UT_17_StructuralEqual_returns_false_when_binding_value_differs()
        {
            QmlDocument left = new QmlDocumentBuilder()
                .SetRootObject("Item", root =>
                {
                    _ = root.Binding("width", Values.Number(100));
                })
                .Build();
            QmlDocument right = new QmlDocumentBuilder()
                .SetRootObject("Item", root =>
                {
                    _ = root.Binding("width", Values.Number(200));
                })
                .Build();

            bool equal = QmlAstUtils.StructuralEqual(left, right);

            Assert.False(equal);
        }

        [Fact]
        public void Summarize_returns_deterministic_document_summary()
        {
            QmlDocument document = CreateUtilityDocument();

            string summary = QmlAstUtils.Summarize(document);

            Assert.Equal("QmlDocument(0 pragmas, 3 imports, root=Column, id=root, members=6, nodes=17, maxDepth=3)", summary);
        }

        [Fact]
        public void CollectImportedModules_returns_only_module_import_uris()
        {
            QmlDocument document = CreateUtilityDocument();

            ImmutableArray<string> modules = QmlAstUtils.CollectImportedModules(document);

            Assert.Equal(["QtQuick", "QtQuick.Controls"], modules.ToArray());
        }

        [Fact]
        public void GetObjectId_returns_direct_id_or_null()
        {
            QmlDocument document = CreateUtilityDocument();
            ObjectDefinitionNode objectWithoutId = new() { TypeName = "Item" };

            string? rootId = QmlAstUtils.GetObjectId(document.RootObject);
            string? missingId = QmlAstUtils.GetObjectId(objectWithoutId);

            Assert.Equal("root", rootId);
            Assert.Null(missingId);
        }

        [Fact]
        public void GetObjectId_returns_first_direct_id_and_ignores_child_ids()
        {
            ObjectDefinitionNode obj = new()
            {
                TypeName = "Item",
                Members =
                [
                    new ObjectDefinitionNode
                    {
                        TypeName = "Child",
                        Members = [new IdAssignmentNode { Id = "child" }],
                    },
                    new IdAssignmentNode { Id = "root" },
                    new IdAssignmentNode { Id = "duplicate" },
                ],
            };

            string? id = QmlAstUtils.GetObjectId(obj);

            Assert.Equal("root", id);
        }

        private static QmlDocument CreateUtilityDocument()
        {
            return new QmlDocumentBuilder()
                .AddModuleImport("QtQuick", "2.15")
                .AddModuleImport("QtQuick.Controls", "2.15")
                .AddJavaScriptImport("logic.js", "Logic")
                .SetRootObject("Column", root =>
                {
                    _ = root.Id("root")
                        .Binding("width", Values.Number(400))
                        .PropertyDeclaration("title", "string")
                        .Child("Text", text =>
                        {
                            _ = text.Id("label")
                                .Binding("text", Values.String("hello"));
                        })
                        .Child("Text", text =>
                        {
                            _ = text.Id("subtitle")
                                .Binding("text", Values.String("world"));
                        })
                        .Child("Rectangle", rectangle =>
                        {
                            _ = rectangle.Id("box")
                                .Child("MouseArea");
                        });
                })
                .Build();
        }

        private static QmlDocument CreateSpannedDocument(SourceSpan span)
        {
            return new QmlDocumentBuilder()
                .SetRootObject(new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Span = span,
                })
                .Build();
        }

        private static QmlDocument CreateCommentedDocument(string leadingCommentText)
        {
            return new QmlDocumentBuilder()
                .SetRootObject(new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    LeadingComments =
                    [
                        new CommentNode
                        {
                            Text = leadingCommentText,
                            IsBlock = false,
                        },
                    ],
                })
                .Build();
        }
    }
}
