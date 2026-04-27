using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Tests.Helpers;
using QmlSharp.Qml.Ast.Transforms;

namespace QmlSharp.Qml.Ast.Tests.Transforms
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class TransformTests
    {
        [Fact]
        public void TR_01_Identity_transform_returns_structurally_equal_document()
        {
            QmlDocument document = CreateTransformDocument();
            QmlAstTransformer transformer = new(new DelegateTransform());

            QmlDocument result = transformer.Transform(document);

            Assert.Equal(document, result);
        }

        [Fact]
        public void TR_02_Transform_modifies_binding_value_number_to_string()
        {
            QmlDocument document = CreateTransformDocument();
            QmlAstTransformer transformer = new(
                new DelegateTransform(valueTransform: value =>
                {
                    if (value is NumberLiteral numberLiteral)
                    {
                        return new StringLiteral(numberLiteral.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }

                    return value;
                }));

            QmlDocument result = transformer.Transform(document);
            BindingNode widthBinding = GetRootBinding(result, "width");

            StringLiteral widthValue = Assert.IsType<StringLiteral>(widthBinding.Value);
            Assert.Equal("400", widthValue.Value);
        }

        [Fact]
        public void TR_03_Transform_deletes_node_when_TransformNode_returns_null()
        {
            QmlDocument document = CreateTransformDocument();
            QmlAstTransformer transformer = new(
                new DelegateTransform(nodeTransform: node =>
                {
                    if (node is BindingNode bindingNode && bindingNode.PropertyName == "color")
                    {
                        return null;
                    }

                    return node;
                }));

            QmlDocument result = transformer.Transform(document);

            Assert.Collection(
                result.RootObject.Members,
                member => _ = Assert.IsType<IdAssignmentNode>(member),
                member =>
                {
                    BindingNode bindingNode = Assert.IsType<BindingNode>(member);
                    Assert.Equal("width", bindingNode.PropertyName);
                },
                member =>
                {
                    ObjectDefinitionNode childObject = Assert.IsType<ObjectDefinitionNode>(member);
                    Assert.Equal("Text", childObject.TypeName);
                });
        }

        [Fact]
        public void TR_04_Transform_replaces_import_module_uri()
        {
            QmlDocument document = CreateTransformDocument();
            QmlAstTransformer transformer = new(
                new DelegateTransform(nodeTransform: node =>
                {
                    if (node is ImportNode importNode && importNode.ModuleUri == "QtQuick")
                    {
                        return importNode with { ModuleUri = "QtQuick.Window" };
                    }

                    return node;
                }));

            QmlDocument result = transformer.Transform(document);

            Assert.Equal("QtQuick.Window", result.Imports[0].ModuleUri);
            Assert.Equal("QtQuick.Controls", result.Imports[1].ModuleUri);
            Assert.Equal(document.RootObject.TypeName, result.RootObject.TypeName);
        }

        [Fact]
        public void TR_05_Original_document_is_not_mutated_after_transform()
        {
            QmlDocument document = CreateTransformDocument();
            QmlAstTransformer transformer = new(
                new DelegateTransform(nodeTransform: node =>
                {
                    if (node is ImportNode importNode && importNode.ModuleUri == "QtQuick.Controls")
                    {
                        return null;
                    }

                    if (node is BindingNode bindingNode && bindingNode.PropertyName == "width")
                    {
                        return bindingNode with { Value = Values.Number(999) };
                    }

                    return node;
                }));

            _ = transformer.Transform(document);

            Assert.Equal(2, document.Imports.Length);
            Assert.Equal("QtQuick", document.Imports[0].ModuleUri);
            Assert.Equal("QtQuick.Controls", document.Imports[1].ModuleUri);
            NumberLiteral widthValue = Assert.IsType<NumberLiteral>(GetRootBinding(document, "width").Value);
            Assert.Equal(400, widthValue.Value);
        }

        [Fact]
        public void TR_06_Chained_transforms_compose_in_order_and_modify_same_subtree()
        {
            QmlDocument document = CreateTransformDocument();
            IQmlAstTransform incrementWidth = new DelegateTransform(nodeTransform: node =>
            {
                if (node is BindingNode bindingNode
                    && bindingNode.PropertyName == "width"
                    && bindingNode.Value is NumberLiteral widthLiteral)
                {
                    return bindingNode with { Value = new NumberLiteral(widthLiteral.Value + 1) };
                }

                return node;
            });
            IQmlAstTransform stringifyWidth = new DelegateTransform(nodeTransform: node =>
            {
                if (node is BindingNode bindingNode
                    && bindingNode.PropertyName == "width"
                    && bindingNode.Value is NumberLiteral widthLiteral)
                {
                    return bindingNode with { Value = new StringLiteral($"size-{widthLiteral.Value}") };
                }

                return node;
            });
            QmlAstTransformer transformer = new(incrementWidth, stringifyWidth);

            QmlDocument result = transformer.Transform(document);
            BindingNode widthBinding = GetRootBinding(result, "width");

            StringLiteral widthValue = Assert.IsType<StringLiteral>(widthBinding.Value);
            Assert.Equal("size-401", widthValue.Value);
        }

        [Fact]
        public void TR_07_Transform_on_deeply_nested_tree_rewrites_all_levels()
        {
            QmlDocument document = CreateDeepDocument();
            QmlAstTransformer transformer = new(
                new DelegateTransform(nodeTransform: node =>
                {
                    if (node is ObjectDefinitionNode objectDefinitionNode)
                    {
                        return objectDefinitionNode with { TypeName = $"{objectDefinitionNode.TypeName}Renamed" };
                    }

                    return node;
                }));

            QmlDocument result = transformer.Transform(document);

            Assert.Equal("ARenamed", result.RootObject.TypeName);
            ObjectDefinitionNode bNode = Assert.IsType<ObjectDefinitionNode>(result.RootObject.Members[0]);
            Assert.Equal("BRenamed", bNode.TypeName);
            ObjectDefinitionNode cNode = Assert.IsType<ObjectDefinitionNode>(bNode.Members[0]);
            Assert.Equal("CRenamed", cNode.TypeName);
            ObjectDefinitionNode dNode = Assert.IsType<ObjectDefinitionNode>(cNode.Members[0]);
            Assert.Equal("DRenamed", dNode.TypeName);
            ObjectDefinitionNode eNode = Assert.IsType<ObjectDefinitionNode>(dNode.Members[0]);
            Assert.Equal("ERenamed", eNode.TypeName);
            ObjectDefinitionNode fNode = Assert.IsType<ObjectDefinitionNode>(eNode.Members[0]);
            Assert.Equal("FRenamed", fNode.TypeName);
        }

        [Fact]
        public void TR_08_TransformValue_rewrites_script_expression_but_leaves_number_literal()
        {
            QmlDocument document = CreateExpressionDocument();
            QmlAstTransformer transformer = new(
                new DelegateTransform(valueTransform: value =>
                {
                    if (value is ScriptExpression scriptExpression)
                    {
                        return scriptExpression with { Code = scriptExpression.Code.Replace("parent.opacity", "Math.max(parent.opacity, 0.0)", StringComparison.Ordinal) };
                    }

                    return value;
                }));

            QmlDocument result = transformer.Transform(document);

            NumberLiteral widthValue = Assert.IsType<NumberLiteral>(GetRootBinding(result, "width").Value);
            ScriptExpression opacityValue = Assert.IsType<ScriptExpression>(GetRootBinding(result, "opacity").Value);
            Assert.Equal(400, widthValue.Value);
            Assert.Equal("Math.max(parent.opacity, 0.0) * 0.5", opacityValue.Code);
        }

        [Fact]
        public void Transform_full_syntax_fixture_can_be_rewritten_and_revalidated()
        {
            QmlDocument document = AstFixtures.FullSyntaxDocument();
            QmlAstTransformer transformer = new(new DelegateTransform(nodeTransform: node =>
            {
                if (node is BindingNode bindingNode
                    && bindingNode.PropertyName == "width"
                    && bindingNode.Value is NumberLiteral width)
                {
                    return bindingNode with
                    {
                        Value = new NumberLiteral(width.Value + 24),
                    };
                }

                return node;
            }));

            QmlDocument transformed = transformer.Transform(document);
            QmlAstValidator validator = new();

            Assert.Empty(validator.ValidateStructure(transformed));
            Assert.Equal(100, Assert.IsType<NumberLiteral>(GetRootBinding(document, "width").Value).Value);
            Assert.Equal(124, Assert.IsType<NumberLiteral>(GetRootBinding(transformed, "width").Value).Value);
        }

        [Fact]
        public void Transform_can_delete_import_nodes_by_returning_null()
        {
            QmlDocument document = CreateTransformDocument();
            QmlAstTransformer transformer = new(
                new DelegateTransform(nodeTransform: node =>
                {
                    if (node is ImportNode importNode && importNode.ModuleUri == "QtQuick.Controls")
                    {
                        return null;
                    }

                    return node;
                }));

            QmlDocument result = transformer.Transform(document);

            _ = Assert.Single(result.Imports);
            Assert.Equal("QtQuick", result.Imports[0].ModuleUri);
        }

        [Fact]
        public void Transform_can_delete_comment_nodes_from_members_and_comment_fields()
        {
            CommentNode leadingComment = new() { Text = "// leading", IsBlock = false };
            CommentNode trailingComment = new() { Text = "// trailing", IsBlock = false };
            BindingNode bindingNode = new()
            {
                PropertyName = "width",
                Value = Values.Number(100),
                LeadingComments = [leadingComment],
                TrailingComment = trailingComment,
            };
            QmlDocument document = new()
            {
                Imports =
                [
                    new ImportNode
                    {
                        ImportKind = ImportKind.Module,
                        ModuleUri = "QtQuick",
                    },
                ],
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members = [bindingNode, new CommentNode { Text = "// member", IsBlock = false }],
                },
            };
            QmlAstTransformer transformer = new(
                new DelegateTransform(nodeTransform: node =>
                {
                    if (node is CommentNode)
                    {
                        return null;
                    }

                    return node;
                }));

            QmlDocument result = transformer.Transform(document);

            BindingNode transformedBinding = Assert.IsType<BindingNode>(result.RootObject.Members[0]);
            Assert.Empty(transformedBinding.LeadingComments);
            Assert.Null(transformedBinding.TrailingComment);
            _ = Assert.Single(result.RootObject.Members);
        }

        [Fact]
        public void Transform_updates_object_values_nested_inside_binding_values()
        {
            QmlDocument document = CreateNestedObjectValueDocument();
            QmlAstTransformer transformer = new(
                new DelegateTransform(nodeTransform: node =>
                {
                    if (node is ObjectDefinitionNode objectDefinitionNode && objectDefinitionNode.TypeName == "PaletteItem")
                    {
                        return objectDefinitionNode with { TypeName = "PaletteEntry" };
                    }

                    if (node is ObjectDefinitionNode nestedObjectDefinitionNode && nestedObjectDefinitionNode.TypeName == "NestedItem")
                    {
                        return nestedObjectDefinitionNode with { TypeName = "NestedEntry" };
                    }

                    return node;
                }));

            QmlDocument result = transformer.Transform(document);
            BindingNode paletteBinding = GetRootBinding(result, "palette");
            ArrayValue topLevelArray = Assert.IsType<ArrayValue>(paletteBinding.Value);
            ObjectValue firstEntry = Assert.IsType<ObjectValue>(topLevelArray.Elements[0]);
            ArrayValue nestedArray = Assert.IsType<ArrayValue>(topLevelArray.Elements[1]);
            ObjectValue nestedEntry = Assert.IsType<ObjectValue>(nestedArray.Elements[0]);

            Assert.Equal("PaletteEntry", firstEntry.Object.TypeName);
            Assert.Equal("NestedEntry", nestedEntry.Object.TypeName);
        }

        [Fact]
        public void Transform_processes_trailing_comments_after_structural_children()
        {
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    LeadingComments = [new CommentNode { Text = "// leading", IsBlock = false }],
                    Members =
                    [
                        new BindingNode
                        {
                            PropertyName = "width",
                            Value = Values.Number(100),
                        },
                    ],
                    TrailingComment = new CommentNode { Text = "// trailing", IsBlock = false },
                },
            };
            List<string> visited = [];
            QmlAstTransformer transformer = new(
                new DelegateTransform(nodeTransform: node =>
                {
                    switch (node)
                    {
                        case CommentNode commentNode:
                            visited.Add(commentNode.Text);
                            break;

                        case BindingNode bindingNode:
                            visited.Add($"binding:{bindingNode.PropertyName}");
                            break;

                        case ObjectDefinitionNode objectDefinitionNode:
                            visited.Add($"object:{objectDefinitionNode.TypeName}");
                            break;
                    }

                    return node;
                }));

            _ = transformer.Transform(document);

            Assert.Equal(
                ["// leading", "binding:width", "// trailing", "object:Item"],
                visited);
        }

        private static QmlDocument CreateTransformDocument()
        {
            return new QmlDocumentBuilder()
                .AddModuleImport("QtQuick", "2.15")
                .AddModuleImport("QtQuick.Controls", "2.15")
                .SetRootObject("Rectangle", root =>
                {
                    _ = root.Id("root")
                        .Binding("width", Values.Number(400))
                        .Binding("color", Values.String("red"))
                        .Child("Text", text =>
                        {
                            _ = text.Binding("text", Values.String("hello"));
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
                                _ = d.Child("E", e =>
                                {
                                    _ = e.Child("F", f =>
                                    {
                                        _ = f.Binding("depth", Values.Number(6));
                                    });
                                });
                            });
                        });
                    });
                })
                .Build();
        }

        private static QmlDocument CreateExpressionDocument()
        {
            return new QmlDocumentBuilder()
                .SetRootObject("Item", root =>
                {
                    _ = root.Binding("width", Values.Number(400))
                        .Binding("opacity", Values.Expression("parent.opacity * 0.5"));
                })
                .Build();
        }

        private static QmlDocument CreateNestedObjectValueDocument()
        {
            return new QmlDocumentBuilder()
                .SetRootObject("Item", root =>
                {
                    _ = root.Binding("palette", Values.Array(
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

        private static BindingNode GetRootBinding(QmlDocument document, string propertyName)
        {
            BindingNode? bindingNode = document.RootObject.Members
                .OfType<BindingNode>()
                .Where(member => member.PropertyName == propertyName)
                .FirstOrDefault();

            return bindingNode
                ?? throw new InvalidOperationException($"Binding '{propertyName}' not found on root object.");
        }

        private sealed class DelegateTransform : IQmlAstTransform
        {
            private readonly Func<AstNode, AstNode?> _nodeTransform;
            private readonly Func<BindingValue, BindingValue> _valueTransform;

            public DelegateTransform(
                Func<AstNode, AstNode?>? nodeTransform = null,
                Func<BindingValue, BindingValue>? valueTransform = null)
            {
                _nodeTransform = nodeTransform ?? IdentityNode;
                _valueTransform = valueTransform ?? IdentityValue;
            }

            public AstNode? TransformNode(AstNode node)
            {
                return _nodeTransform(node);
            }

            public BindingValue TransformValue(BindingValue value)
            {
                return _valueTransform(value);
            }

            private static AstNode IdentityNode(AstNode node)
            {
                return node;
            }

            private static BindingValue IdentityValue(BindingValue value)
            {
                return value;
            }
        }
    }
}
