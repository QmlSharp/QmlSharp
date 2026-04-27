using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Serialization;
using QmlSharp.Qml.Ast.Tests.Helpers;
using QmlSharp.Qml.Ast.Transforms;
using QmlSharp.Qml.Ast.Utilities;

namespace QmlSharp.Qml.Ast.Tests.Closure
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class AstClosureCoverageTests
    {
        private static readonly QmlAstSerializer Serializer = new();

        [Fact]
        public void Serializer_rejects_empty_non_document_and_malformed_discriminators()
        {
            _ = Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson("   "));
            Assert.Contains(
                "root must have kind 'Document'",
                Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson("{\"kind\":\"Comment\",\"text\":\"// note\"}")).Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "Expected AST node JSON object",
                Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson("[]")).Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "discriminator 'kind' must be a string",
                Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson("{\"kind\":1}")).Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "discriminator 'kind' cannot be empty",
                Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson("{\"kind\":\"\"}")).Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Serializer_rejects_required_null_missing_and_wrong_optional_node_shapes()
        {
            Assert.Contains(
                "Required property 'rootObject' is missing",
                Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson("{\"kind\":\"Document\"}")).Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "Required property 'rootObject' cannot be null",
                Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson("{\"kind\":\"Document\",\"rootObject\":null}")).Message,
                StringComparison.Ordinal);

            string wrongTrailingCommentJson = "{\"kind\":\"Document\",\"rootObject\":{\"kind\":\"ObjectDefinition\",\"typeName\":\"Item\",\"trailingComment\":{\"kind\":\"Binding\",\"propertyName\":\"width\",\"value\":{\"kind\":\"NumberLiteral\",\"value\":1}}}}";

            Assert.Contains(
                "Property 'trailingComment' must be a CommentNode AST node.",
                Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson(wrongTrailingCommentJson)).Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Serializer_exception_inner_constructor_preserves_inner_exception()
        {
            InvalidOperationException inner = new("inner failure");

            QmlAstSerializationException exception = new("outer failure", inner);

            Assert.Equal("outer failure", exception.Message);
            Assert.Same(inner, exception.InnerException);
        }

        [Fact]
        public void Transformer_rejects_null_entries_wrong_typed_replacements_and_required_deletions()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddModuleImport("QtQuick")
                .SetRootObject("Item", root =>
                {
                    _ = root.Binding("width", Values.Number(100));
                })
                .Build();

            _ = Assert.Throws<ArgumentException>(() => new QmlAstTransformer([new IdentityTransform(), null!]));
            Assert.Contains(
                "must produce a QmlDocument",
                Assert.Throws<InvalidOperationException>(() => new QmlAstTransformer(new ReplaceDocumentWithCommentTransform()).Transform(document)).Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "attempted to delete required node 'ObjectDefinitionNode'",
                Assert.Throws<InvalidOperationException>(() => new QmlAstTransformer(new DeleteObjectsTransform()).Transform(document)).Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "where 'ImportNode' is required",
                Assert.Throws<InvalidOperationException>(() => new QmlAstTransformer(new ReplaceImportsWithCommentTransform()).Transform(document)).Message,
                StringComparison.Ordinal);
            Assert.Contains(
                "returned null from TransformValue",
                Assert.Throws<InvalidOperationException>(() => new QmlAstTransformer(new NullValueTransform()).Transform(document)).Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Transformer_rejects_wrong_optional_comment_replacements()
        {
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    TrailingComment = new CommentNode { Text = "// trailing" },
                },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new QmlAstTransformer(new ReplaceCommentsWithBindingTransform()).Transform(document));

            Assert.Contains("where 'CommentNode' is required", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Semantic_validation_exercises_grouped_array_behavior_function_signal_and_value_paths()
        {
            QmlDocument document = new()
            {
                Imports =
                [
                    new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtQuick" },
                    new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtQuick.Controls", Qualifier = "Controls" },
                ],
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members =
                    [
                        new PropertyDeclarationNode
                        {
                            Name = "localEnum",
                            TypeName = "MissingPropertyType",
                            InitialValue = Values.Enum("Image", "MissingEnum"),
                        },
                        new GroupedBindingNode
                        {
                            GroupName = "missingGroup",
                            Bindings = [new BindingNode { PropertyName = "width", Value = Values.Number(1) }],
                        },
                        new ArrayBindingNode
                        {
                            PropertyName = "missingArrayProperty",
                            Elements =
                            [
                                Values.Object(new ObjectDefinitionNode { TypeName = "MissingArrayObject" }),
                                Values.Array(Values.Enum("MissingArrayEnum", "Value")),
                            ],
                        },
                        new BehaviorOnNode
                        {
                            PropertyName = "missingBehaviorProperty",
                            Animation = new ObjectDefinitionNode { TypeName = "Rectangle" },
                        },
                        new AttachedBindingNode
                        {
                            AttachedTypeName = "Layout",
                            Bindings = [new BindingNode { PropertyName = "missingAttachedProperty", Value = Values.Boolean(true) }],
                        },
                        new SignalDeclarationNode
                        {
                            Name = "submitted",
                            Parameters = [new ParameterDeclaration("payload", "MissingSignalType")],
                        },
                        new FunctionDeclarationNode
                        {
                            Name = "compute",
                            Parameters = [new ParameterDeclaration("input", "MissingFunctionParameterType")],
                            ReturnType = "MissingFunctionReturnType",
                            Body = "return input;",
                        },
                        new InlineComponentNode
                        {
                            Name = "InlineThing",
                            Body = new ObjectDefinitionNode { TypeName = "MissingInlineType" },
                        },
                        new ObjectDefinitionNode { TypeName = "Controls.Button" },
                    ],
                },
            };

            ImmutableArray<AstDiagnostic> diagnostics = new QmlAstValidator().ValidateSemantic(document, new TestTypeChecker());

            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E100_UnknownType && diagnostic.Message.Contains("MissingPropertyType", StringComparison.Ordinal));
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E100_UnknownType && diagnostic.Message.Contains("MissingSignalType", StringComparison.Ordinal));
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E100_UnknownType && diagnostic.Message.Contains("MissingFunctionParameterType", StringComparison.Ordinal));
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E100_UnknownType && diagnostic.Message.Contains("MissingFunctionReturnType", StringComparison.Ordinal));
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E100_UnknownType && diagnostic.Message.Contains("MissingInlineType", StringComparison.Ordinal));
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E100_UnknownType && diagnostic.Message.Contains("MissingArrayObject", StringComparison.Ordinal));
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E101_UnknownProperty && diagnostic.Message.Contains("missingGroup", StringComparison.Ordinal));
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E101_UnknownProperty && diagnostic.Message.Contains("missingArrayProperty", StringComparison.Ordinal));
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E101_UnknownProperty && diagnostic.Message.Contains("missingBehaviorProperty", StringComparison.Ordinal));
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E101_UnknownProperty && diagnostic.Message.Contains("missingAttachedProperty", StringComparison.Ordinal));
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E106_InvalidEnumReference && diagnostic.Message.Contains("Image.MissingEnum", StringComparison.Ordinal));
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E106_InvalidEnumReference && diagnostic.Message.Contains("MissingArrayEnum.Value", StringComparison.Ordinal));
            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Message.Contains("Controls.Button", StringComparison.Ordinal));
        }

        [Fact]
        public void Semantic_validation_accepts_local_alias_changed_handler_and_flags_readonly_behavior_target()
        {
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Image",
                    Members =
                    [
                        new PropertyAliasNode { Name = "localAlias", Target = "child.text" },
                        new SignalHandlerNode
                        {
                            HandlerName = "onLocalAliasChanged",
                            Form = SignalHandlerForm.Expression,
                            Code = "console.log(localAlias)",
                        },
                        new BehaviorOnNode
                        {
                            PropertyName = "sourceSize",
                            Animation = new ObjectDefinitionNode { TypeName = "Rectangle" },
                        },
                    ],
                },
            };

            ImmutableArray<AstDiagnostic> diagnostics = new QmlAstValidator().ValidateSemantic(document, new TestTypeChecker());

            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E102_UnknownSignal);
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E105_ReadonlyPropertyBound);
        }

        [Fact]
        public void Structural_validation_rejects_invalid_enum_import_and_identifier_edge_cases()
        {
            QmlDocument document = new()
            {
                Imports = [(ImportNode)new() { ImportKind = (ImportKind)999 }],
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members =
                    [
                        new IdAssignmentNode { Id = string.Empty },
                        new SignalHandlerNode
                        {
                            HandlerName = "onBad-Name",
                            Form = SignalHandlerForm.Expression,
                            Code = "noop()",
                        },
                        new InlineComponentNode
                        {
                            Name = "Bad-Name",
                            Body = new ObjectDefinitionNode { TypeName = "Rectangle" },
                        },
                    ],
                },
            };

            ImmutableArray<AstDiagnostic> diagnostics = new QmlAstValidator().ValidateStructure(document);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E007_InvalidImport);
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E002_InvalidIdFormat);
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E005_InvalidHandlerNameFormat);
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E009_InvalidInlineComponentName);
        }

        [Fact]
        public void Utilities_cover_child_extraction_depth_and_structural_mismatch_edges()
        {
            CommentNode leadingComment = new() { Text = "// leading" };
            CommentNode trailingComment = new() { Text = "// trailing" };
            QmlDocument document = new()
            {
                Pragmas = [new PragmaNode { Name = PragmaName.Singleton }],
                Imports = [new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtQuick" }],
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members =
                    [
                        new PropertyDeclarationNode
                        {
                            Name = "childProp",
                            TypeName = "var",
                            InitialValue = Values.Object(new ObjectDefinitionNode { TypeName = "Text" }),
                        },
                        new GroupedBindingNode
                        {
                            GroupName = "font",
                            Bindings = [new BindingNode { PropertyName = "pixelSize", Value = Values.Number(12) }],
                        },
                        new AttachedBindingNode
                        {
                            AttachedTypeName = "Layout",
                            Bindings = [new BindingNode { PropertyName = "fillWidth", Value = Values.Boolean(true) }],
                        },
                        new ArrayBindingNode
                        {
                            PropertyName = "states",
                            Elements = [Values.Array(Values.Object(new ObjectDefinitionNode { TypeName = "State" }))],
                        },
                        new BehaviorOnNode
                        {
                            PropertyName = "opacity",
                            Animation = new ObjectDefinitionNode { TypeName = "NumberAnimation" },
                        },
                        new BindingNode
                        {
                            PropertyName = "decorated",
                            Value = Values.Number(1),
                            LeadingComments = [leadingComment],
                            TrailingComment = trailingComment,
                        },
                    ],
                },
            };

            Assert.Equal([NodeKind.Pragma, NodeKind.Import, NodeKind.ObjectDefinition], QmlAstUtils.GetChildren(document).Select(static node => node.Kind));
            Assert.Equal([NodeKind.ObjectDefinition], QmlAstUtils.GetChildren((InlineComponentNode)new() { Name = "Badge", Body = new ObjectDefinitionNode { TypeName = "Rectangle" } }).Select(static node => node.Kind));
            Assert.Equal([NodeKind.ObjectDefinition], QmlAstUtils.GetChildren((PropertyDeclarationNode)document.RootObject.Members[0]).Select(static node => node.Kind));
            Assert.Equal([NodeKind.Binding], QmlAstUtils.GetChildren((GroupedBindingNode)document.RootObject.Members[1]).Select(static node => node.Kind));
            Assert.Equal([NodeKind.Binding], QmlAstUtils.GetChildren((AttachedBindingNode)document.RootObject.Members[2]).Select(static node => node.Kind));
            Assert.Equal([NodeKind.ObjectDefinition], QmlAstUtils.GetChildren((ArrayBindingNode)document.RootObject.Members[3]).Select(static node => node.Kind));
            Assert.Equal([NodeKind.ObjectDefinition], QmlAstUtils.GetChildren((BehaviorOnNode)document.RootObject.Members[4]).Select(static node => node.Kind));
            Assert.Equal([NodeKind.Comment, NodeKind.Comment], QmlAstUtils.GetChildren(document.RootObject.Members[5]).Select(static node => node.Kind));
            Assert.True(QmlAstUtils.MaxDepth(document) >= 2);

            QmlDocument differentType = document with { RootObject = document.RootObject with { TypeName = "Rectangle" } };
            QmlDocument differentComment = document with
            {
                RootObject = document.RootObject with
                {
                    Members = document.RootObject.Members.SetItem(
                        5,
                        document.RootObject.Members[5] with { LeadingComments = [new CommentNode { Text = "// other" }] }),
                },
            };

            Assert.False(QmlAstUtils.StructuralEqual(document.RootObject, document.Imports[0]));
            Assert.False(QmlAstUtils.StructuralEqual(document, differentType));
            Assert.False(QmlAstUtils.StructuralEqual(document, differentComment));
        }

        private sealed class IdentityTransform : IQmlAstTransform
        {
            public AstNode? TransformNode(AstNode node) => node;
        }

        private sealed class ReplaceDocumentWithCommentTransform : IQmlAstTransform
        {
            public AstNode? TransformNode(AstNode node)
            {
                return node is QmlDocument
                    ? new CommentNode { Text = "// wrong root" }
                    : node;
            }
        }

        private sealed class DeleteObjectsTransform : IQmlAstTransform
        {
            public AstNode? TransformNode(AstNode node)
            {
                return node is ObjectDefinitionNode ? null : node;
            }
        }

        private sealed class ReplaceImportsWithCommentTransform : IQmlAstTransform
        {
            public AstNode? TransformNode(AstNode node)
            {
                return node is ImportNode
                    ? new CommentNode { Text = "// wrong import" }
                    : node;
            }
        }

        private sealed class ReplaceCommentsWithBindingTransform : IQmlAstTransform
        {
            public AstNode? TransformNode(AstNode node)
            {
                return node is CommentNode
                    ? new BindingNode { PropertyName = "width", Value = Values.Number(1) }
                    : node;
            }
        }

        private sealed class NullValueTransform : IQmlAstTransform
        {
            public AstNode? TransformNode(AstNode node) => node;

            public BindingValue TransformValue(BindingValue value) => null!;
        }
    }
}
