using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Serialization;
using QmlSharp.Qml.Ast.Tests.Helpers;
using QmlSharp.Qml.Ast.Transforms;
using QmlSharp.Qml.Ast.Traversal;
using QmlSharp.Qml.Ast.Utilities;

namespace QmlSharp.Qml.Ast.Tests.E2E
{
    // QmlTS parity notes (tests/ast/e2e.test.ts):
    // - built AST passes structural validation -> E2E_01
    // - serialization roundtrip preserves AST -> E2E_02
    // - walker parity adapted from id collection to full node-kind inventory (+ id check) -> E2E_03
    // - transformed AST remains structurally valid -> E2E_04
    // - full-syntax fixture invariant checks -> E2E_05
    [Trait("Category", TestCategories.Integration)]
    public sealed class AstEndToEndTests
    {
        private static readonly QmlAstSerializer Serializer = new();
        private static readonly QmlAstValidator Validator = new();

        [Fact]
        public void E2E_01_Builder_to_validator_passes_for_full_syntax_document()
        {
            QmlDocument document = AstFixtures.FullSyntaxDocument();

            ImmutableArray<AstDiagnostic> diagnostics = Validator.ValidateStructure(document);

            Assert.Empty(diagnostics);
        }

        [Fact]
        public void E2E_02_Builder_to_serializer_roundtrip_preserves_full_syntax_document()
        {
            QmlDocument document = AstFixtures.FullSyntaxDocument();

            QmlDocument roundTripped = Serializer.FromJson(Serializer.ToJson(document));

            Assert.True(QmlAstUtils.StructuralEqual(document, roundTripped));
            Assert.Equal(document.RootObject.Members.Select(member => member.Kind), roundTripped.RootObject.Members.Select(member => member.Kind));
        }

        [Fact]
        public void E2E_03_Builder_to_walker_covers_every_NodeKind_and_matches_id_collection()
        {
            QmlDocument document = AstFixtures.FullSyntaxDocument();
            HashSet<NodeKind> visitedKinds = [];
            List<string> walkedIds = [];

            QmlAstWalker.Walk(
                document,
                enter: (node, ctx) =>
                {
                    _ = ctx;
                    _ = visitedKinds.Add(node.Kind);
                    if (node is IdAssignmentNode idAssignmentNode)
                    {
                        walkedIds.Add(idAssignmentNode.Id);
                    }

                    return true;
                },
                leave: null);

            foreach (NodeKind nodeKind in FullSyntaxDocumentFactory.AllNodeKinds())
            {
                Assert.Contains(nodeKind, visitedKinds);
            }

            ImmutableArray<string> utilityIds = QmlAstUtils.CollectIds(document);
            Assert.Equal(
                utilityIds.OrderBy(static id => id, StringComparer.Ordinal),
                walkedIds.OrderBy(static id => id, StringComparer.Ordinal));
        }

        [Fact]
        public void E2E_04_Builder_to_transform_to_validator_revalidates_successfully()
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
            ImmutableArray<AstDiagnostic> diagnostics = Validator.ValidateStructure(transformed);

            Assert.Empty(diagnostics);
            Assert.Equal(100, Assert.IsType<NumberLiteral>(GetRootBinding(document, "width").Value).Value);
            Assert.Equal(124, Assert.IsType<NumberLiteral>(GetRootBinding(transformed, "width").Value).Value);
        }

        [Fact]
        public void E2E_05_Full_syntax_document_satisfies_fixture_and_coverage_invariants()
        {
            QmlDocument document = AstFixtures.FullSyntaxDocument();

            Assert.Empty(Validator.ValidateStructure(document));
            Assert.True(QmlAstUtils.CountNodes(document) > 30);
            Assert.True(QmlAstUtils.MaxDepth(document) >= 4);

            ImmutableArray<PragmaName> pragmaNames = [.. document.Pragmas.Select(static pragmaNode => pragmaNode.Name)];
            Assert.Equal(FullSyntaxDocumentFactory.AllPragmas().ToArray(), pragmaNames.ToArray());

            ImmutableArray<ImportKind> importKinds = [.. document.Imports.Select(static importNode => importNode.ImportKind)];
            Assert.Equal(FullSyntaxDocumentFactory.AllImportKinds().ToArray(), importKinds.ToArray());

            Assert.Contains(document.RootObject.Members, static member => member is PropertyAliasNode);
            Assert.Contains(
                document.RootObject.Members.OfType<PropertyDeclarationNode>(),
                static declaration => declaration.IsDefault && declaration.IsRequired && !declaration.IsReadonly);
            Assert.Contains(
                document.RootObject.Members.OfType<PropertyDeclarationNode>(),
                static declaration => declaration.IsDefault && !declaration.IsRequired && !declaration.IsReadonly);
            Assert.Contains(
                document.RootObject.Members.OfType<PropertyDeclarationNode>(),
                static declaration => !declaration.IsDefault && declaration.IsRequired && !declaration.IsReadonly);
            Assert.Contains(
                document.RootObject.Members.OfType<PropertyDeclarationNode>(),
                static declaration => !declaration.IsDefault && !declaration.IsRequired && declaration.IsReadonly);

            Assert.Contains(document.RootObject.Members, static member => member is GroupedBindingNode);
            Assert.Contains(document.RootObject.Members, static member => member is AttachedBindingNode);
            Assert.Contains(document.RootObject.Members, static member => member is ArrayBindingNode);
            Assert.Contains(document.RootObject.Members, static member => member is BehaviorOnNode);
            Assert.Contains(document.RootObject.Members, static member => member is SignalDeclarationNode);
            Assert.Contains(document.RootObject.Members, static member => member is FunctionDeclarationNode);
            Assert.Contains(document.RootObject.Members, static member => member is EnumDeclarationNode);
            Assert.Contains(document.RootObject.Members, static member => member is InlineComponentNode);
            Assert.Contains(document.RootObject.Members, static member => member is CommentNode);

            ImmutableArray<SignalHandlerForm> signalHandlerForms =
            [
                .. document.RootObject.Members
                    .OfType<SignalHandlerNode>()
                    .Select(static handler => handler.Form)
                    .Distinct(),
            ];
            Assert.Equal(FullSyntaxDocumentFactory.AllSignalHandlerForms().ToArray(), signalHandlerForms.ToArray());

            HashSet<NodeKind> visitedNodeKinds = [];
            QmlAstWalker.Walk(
                document,
                enter: (node, ctx) =>
                {
                    _ = ctx;
                    _ = visitedNodeKinds.Add(node.Kind);
                    return true;
                },
                leave: null);

            foreach (NodeKind nodeKind in FullSyntaxDocumentFactory.AllNodeKinds())
            {
                Assert.Contains(nodeKind, visitedNodeKinds);
            }

            ImmutableArray<BindingValueKind> bindingValueKinds = CollectBindingValueKinds(document);
            foreach (BindingValueKind kind in FullSyntaxDocumentFactory.AllBindingValueKinds())
            {
                Assert.Contains(kind, bindingValueKinds);
            }

            DiagnosticCode[] expectedDiagnosticCodes =
            [
                DiagnosticCode.E001_DuplicateId,
                DiagnosticCode.E002_InvalidIdFormat,
                DiagnosticCode.E003_DuplicatePropertyName,
                DiagnosticCode.E004_DuplicateSignalName,
                DiagnosticCode.E005_InvalidHandlerNameFormat,
                DiagnosticCode.E006_ConflictingPropertyModifiers,
                DiagnosticCode.E007_InvalidImport,
                DiagnosticCode.E008_DuplicateEnumName,
                DiagnosticCode.E009_InvalidInlineComponentName,
                DiagnosticCode.E010_ExcessiveNestingDepth,
                DiagnosticCode.E100_UnknownType,
                DiagnosticCode.E101_UnknownProperty,
                DiagnosticCode.E102_UnknownSignal,
                DiagnosticCode.E103_UnknownAttachedType,
                DiagnosticCode.E104_RequiredPropertyNotSet,
                DiagnosticCode.E105_ReadonlyPropertyBound,
                DiagnosticCode.E106_InvalidEnumReference,
                DiagnosticCode.E107_UnknownModule,
                DiagnosticCode.W001_UnusedImport,
            ];
            Assert.Equal(expectedDiagnosticCodes, Enum.GetValues<DiagnosticCode>());

            QmlDocument roundTripped = Serializer.FromJson(Serializer.ToJson(document));
            Assert.True(QmlAstUtils.StructuralEqual(document, roundTripped));
        }

        [Fact]
        public void E2E_Optional_Semantic_validation_with_TestTypeChecker_passes_without_registry_dependency()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddModuleImport("QtQuick", "6.11")
                .SetRootObject("Rectangle", root =>
                {
                    _ = root.Binding("width", Values.Number(100))
                        .Binding("color", Values.String("red"))
                        .SignalHandler("onWidthChanged", SignalHandlerForm.Expression, "console.log(width)")
                        .AttachedBinding("Layout", layout => _ = layout.Binding("fillWidth", Values.Boolean(true)))
                        .Child("Text", text => _ = text.Binding("text", Values.String("Hello")));
                })
                .Build();

            ImmutableArray<AstDiagnostic> structuralDiagnostics = Validator.ValidateStructure(document);
            ImmutableArray<AstDiagnostic> semanticDiagnostics = Validator.ValidateSemantic(document, new TestTypeChecker());

            Assert.Empty(structuralDiagnostics);
            Assert.Empty(semanticDiagnostics);
        }

        private static ImmutableArray<BindingValueKind> CollectBindingValueKinds(QmlDocument document)
        {
            ImmutableArray<BindingValueKind>.Builder kinds = ImmutableArray.CreateBuilder<BindingValueKind>();
            QmlAstWalker.Walk(
                document,
                enter: (node, _) =>
                {
                    AddBindingValues(node, kinds);
                    return true;
                },
                leave: null);

            return kinds.ToImmutable();
        }

        private static void AddBindingValues(AstNode node, ImmutableArray<BindingValueKind>.Builder kinds)
        {
            switch (node)
            {
                case PropertyDeclarationNode propertyDeclarationNode when propertyDeclarationNode.InitialValue is not null:
                    AddBindingValue(propertyDeclarationNode.InitialValue, kinds);
                    break;

                case BindingNode bindingNode:
                    AddBindingValue(bindingNode.Value, kinds);
                    break;

                case ArrayBindingNode arrayBindingNode:
                    foreach (BindingValue element in arrayBindingNode.Elements)
                    {
                        AddBindingValue(element, kinds);
                    }

                    break;
            }
        }

        private static void AddBindingValue(BindingValue value, ImmutableArray<BindingValueKind>.Builder kinds)
        {
            kinds.Add(value.Kind);
            switch (value)
            {
                case ObjectValue objectValue:
                    QmlAstWalker.Walk(
                        objectValue.Object,
                        enter: (node, _) =>
                        {
                            AddBindingValues(node, kinds);
                            return true;
                        },
                        leave: null);
                    break;

                case ArrayValue arrayValue:
                    foreach (BindingValue element in arrayValue.Elements)
                    {
                        AddBindingValue(element, kinds);
                    }

                    break;
            }
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
