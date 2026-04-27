using System.Collections.Immutable;

#pragma warning disable MA0048

namespace QmlSharp.Qml.Ast.Transforms
{
    /// <summary>
    /// Interface for a single AST transformation.
    /// </summary>
    public interface IQmlAstTransform
    {
        /// <summary>
        /// Transforms a single node.
        /// </summary>
        /// <param name="node">The input node.</param>
        /// <returns>The transformed node, or null to remove it.</returns>
        AstNode? TransformNode(AstNode node);

        /// <summary>
        /// Transforms a binding value.
        /// </summary>
        /// <param name="value">The input value.</param>
        /// <returns>The transformed value.</returns>
        BindingValue TransformValue(BindingValue value) => value;
    }

    /// <summary>
    /// Applies one or more transforms to an AST document.
    /// </summary>
    public sealed class QmlAstTransformer
    {
        private readonly ImmutableArray<IQmlAstTransform> _transforms;

        /// <summary>
        /// Initializes a new instance of the <see cref="QmlAstTransformer"/> class.
        /// </summary>
        /// <param name="transforms">Transforms applied in order.</param>
        public QmlAstTransformer(params IQmlAstTransform[] transforms)
            : this((IEnumerable<IQmlAstTransform>)transforms)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QmlAstTransformer"/> class.
        /// </summary>
        /// <param name="transforms">Transforms applied in order.</param>
        public QmlAstTransformer(IEnumerable<IQmlAstTransform> transforms)
        {
            ArgumentNullException.ThrowIfNull(transforms);
            _transforms = ValidateAndFreezeTransforms(transforms);
        }

        /// <summary>
        /// Applies all configured transforms to <paramref name="document"/>.
        /// </summary>
        /// <param name="document">Input document.</param>
        /// <returns>Transformed document.</returns>
        public QmlDocument Transform(QmlDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            QmlDocument transformedDocument = document;
            foreach (IQmlAstTransform transform in _transforms)
            {
                transformedDocument = TransformDocument(transformedDocument, transform);
            }

            return transformedDocument;
        }

        private static ImmutableArray<IQmlAstTransform> ValidateAndFreezeTransforms(IEnumerable<IQmlAstTransform> transforms)
        {
            ImmutableArray<IQmlAstTransform>.Builder builder = ImmutableArray.CreateBuilder<IQmlAstTransform>();
            foreach (IQmlAstTransform transform in transforms)
            {
                if (transform is null)
                {
                    throw new ArgumentException("Transforms collection cannot contain null entries.", nameof(transforms));
                }

                builder.Add(transform);
            }

            return builder.ToImmutable();
        }

        private static QmlDocument TransformDocument(QmlDocument document, IQmlAstTransform transform)
        {
            AstNode? transformedNode = TransformNode(document, transform, allowDelete: false);
            return transformedNode as QmlDocument
                ?? throw new InvalidOperationException("Document transform must produce a QmlDocument.");
        }

        private static AstNode? TransformNode(AstNode node, IQmlAstTransform transform, bool allowDelete)
        {
            AstNode nodeWithTransformedChildren = TransformNodeChildren(node, transform);
            AstNode? transformedNode = transform.TransformNode(nodeWithTransformedChildren);
            if (transformedNode is null)
            {
                if (allowDelete)
                {
                    return null;
                }

                throw new InvalidOperationException($"Transform '{transform.GetType().Name}' attempted to delete required node '{node.GetType().Name}'.");
            }

            return transformedNode;
        }

        private static AstNode TransformNodeChildren(AstNode node, IQmlAstTransform transform)
        {
            AstNode nodeWithComments = TransformNodeComments(node, transform);

            return nodeWithComments switch
            {
                QmlDocument document => TransformDocumentChildren(document, transform),
                ObjectDefinitionNode objectDefinitionNode => TransformObjectDefinitionChildren(objectDefinitionNode, transform),
                InlineComponentNode inlineComponentNode => TransformInlineComponentChildren(inlineComponentNode, transform),
                PropertyDeclarationNode propertyDeclarationNode => TransformPropertyDeclarationChildren(propertyDeclarationNode, transform),
                BindingNode bindingNode => TransformBindingChildren(bindingNode, transform),
                GroupedBindingNode groupedBindingNode => TransformGroupedBindingChildren(groupedBindingNode, transform),
                AttachedBindingNode attachedBindingNode => TransformAttachedBindingChildren(attachedBindingNode, transform),
                ArrayBindingNode arrayBindingNode => TransformArrayBindingChildren(arrayBindingNode, transform),
                BehaviorOnNode behaviorOnNode => TransformBehaviorOnChildren(behaviorOnNode, transform),
                _ => nodeWithComments,
            };
        }

        private static AstNode TransformNodeComments(AstNode node, IQmlAstTransform transform)
        {
            ImmutableArray<CommentNode> leadingComments = TransformNodeList(node.LeadingComments, transform, out bool leadingChanged);
            CommentNode? trailingComment = TransformOptionalNode(node.TrailingComment, transform, out bool trailingChanged);
            if (!leadingChanged && !trailingChanged)
            {
                return node;
            }

            return node with
            {
                LeadingComments = leadingComments,
                TrailingComment = trailingComment,
            };
        }

        private static AstNode TransformDocumentChildren(QmlDocument document, IQmlAstTransform transform)
        {
            ImmutableArray<PragmaNode> pragmas = TransformNodeList(document.Pragmas, transform, out bool pragmasChanged);
            ImmutableArray<ImportNode> imports = TransformNodeList(document.Imports, transform, out bool importsChanged);
            ObjectDefinitionNode rootObject = TransformRequiredNode(document.RootObject, transform);
            if (!pragmasChanged && !importsChanged && ReferenceEquals(rootObject, document.RootObject))
            {
                return document;
            }

            return document with
            {
                Pragmas = pragmas,
                Imports = imports,
                RootObject = rootObject,
            };
        }

        private static AstNode TransformObjectDefinitionChildren(ObjectDefinitionNode objectDefinitionNode, IQmlAstTransform transform)
        {
            ImmutableArray<AstNode> members = TransformNodeList(objectDefinitionNode.Members, transform, out bool membersChanged);
            if (!membersChanged)
            {
                return objectDefinitionNode;
            }

            return objectDefinitionNode with
            {
                Members = members,
            };
        }

        private static AstNode TransformInlineComponentChildren(InlineComponentNode inlineComponentNode, IQmlAstTransform transform)
        {
            ObjectDefinitionNode body = TransformRequiredNode(inlineComponentNode.Body, transform);
            if (ReferenceEquals(body, inlineComponentNode.Body))
            {
                return inlineComponentNode;
            }

            return inlineComponentNode with
            {
                Body = body,
            };
        }

        private static AstNode TransformPropertyDeclarationChildren(PropertyDeclarationNode propertyDeclarationNode, IQmlAstTransform transform)
        {
            if (propertyDeclarationNode.InitialValue is null)
            {
                return propertyDeclarationNode;
            }

            BindingValue initialValue = TransformValue(propertyDeclarationNode.InitialValue, transform);
            if (ReferenceEquals(initialValue, propertyDeclarationNode.InitialValue))
            {
                return propertyDeclarationNode;
            }

            return propertyDeclarationNode with
            {
                InitialValue = initialValue,
            };
        }

        private static AstNode TransformBindingChildren(BindingNode bindingNode, IQmlAstTransform transform)
        {
            BindingValue value = TransformValue(bindingNode.Value, transform);
            if (ReferenceEquals(value, bindingNode.Value))
            {
                return bindingNode;
            }

            return bindingNode with
            {
                Value = value,
            };
        }

        private static AstNode TransformGroupedBindingChildren(GroupedBindingNode groupedBindingNode, IQmlAstTransform transform)
        {
            ImmutableArray<BindingNode> bindings = TransformNodeList(groupedBindingNode.Bindings, transform, out bool bindingsChanged);
            if (!bindingsChanged)
            {
                return groupedBindingNode;
            }

            return groupedBindingNode with
            {
                Bindings = bindings,
            };
        }

        private static AstNode TransformAttachedBindingChildren(AttachedBindingNode attachedBindingNode, IQmlAstTransform transform)
        {
            ImmutableArray<BindingNode> bindings = TransformNodeList(attachedBindingNode.Bindings, transform, out bool bindingsChanged);
            if (!bindingsChanged)
            {
                return attachedBindingNode;
            }

            return attachedBindingNode with
            {
                Bindings = bindings,
            };
        }

        private static AstNode TransformArrayBindingChildren(ArrayBindingNode arrayBindingNode, IQmlAstTransform transform)
        {
            ImmutableArray<BindingValue> elements = TransformValueList(arrayBindingNode.Elements, transform, out bool elementsChanged);
            if (!elementsChanged)
            {
                return arrayBindingNode;
            }

            return arrayBindingNode with
            {
                Elements = elements,
            };
        }

        private static AstNode TransformBehaviorOnChildren(BehaviorOnNode behaviorOnNode, IQmlAstTransform transform)
        {
            ObjectDefinitionNode animation = TransformRequiredNode(behaviorOnNode.Animation, transform);
            if (ReferenceEquals(animation, behaviorOnNode.Animation))
            {
                return behaviorOnNode;
            }

            return behaviorOnNode with
            {
                Animation = animation,
            };
        }

        private static BindingValue TransformValue(BindingValue value, IQmlAstTransform transform)
        {
            BindingValue valueWithTransformedChildren = TransformValueChildren(value, transform);
            BindingValue transformedValue = transform.TransformValue(valueWithTransformedChildren)
                ?? throw new InvalidOperationException($"Transform '{transform.GetType().Name}' returned null from TransformValue.");
            return transformedValue;
        }

        private static BindingValue TransformValueChildren(BindingValue value, IQmlAstTransform transform)
        {
            return value switch
            {
                ObjectValue objectValue => TransformObjectValueChildren(objectValue, transform),
                ArrayValue arrayValue => TransformArrayValueChildren(arrayValue, transform),
                _ => value,
            };
        }

        private static BindingValue TransformObjectValueChildren(ObjectValue objectValue, IQmlAstTransform transform)
        {
            ObjectDefinitionNode transformedObject = TransformRequiredNode(objectValue.Object, transform);
            if (ReferenceEquals(transformedObject, objectValue.Object))
            {
                return objectValue;
            }

            return objectValue with
            {
                Object = transformedObject,
            };
        }

        private static BindingValue TransformArrayValueChildren(ArrayValue arrayValue, IQmlAstTransform transform)
        {
            ImmutableArray<BindingValue> elements = TransformValueList(arrayValue.Elements, transform, out bool elementsChanged);
            if (!elementsChanged)
            {
                return arrayValue;
            }

            return arrayValue with
            {
                Elements = elements,
            };
        }

        private static ImmutableArray<TNode> TransformNodeList<TNode>(ImmutableArray<TNode> nodes, IQmlAstTransform transform, out bool changed)
            where TNode : AstNode
        {
            changed = false;
            ImmutableArray<TNode>.Builder builder = ImmutableArray.CreateBuilder<TNode>(nodes.Length);
            foreach (TNode node in nodes)
            {
                AstNode? transformedNode = TransformNode(node, transform, allowDelete: true);
                if (transformedNode is null)
                {
                    changed = true;
                    continue;
                }

                if (transformedNode is not TNode typedNode)
                {
                    throw new InvalidOperationException($"Transform '{transform.GetType().Name}' produced '{transformedNode.GetType().Name}' where '{typeof(TNode).Name}' is required.");
                }

                if (!ReferenceEquals(typedNode, node))
                {
                    changed = true;
                }

                builder.Add(typedNode);
            }

            if (!changed && builder.Count == nodes.Length)
            {
                return nodes;
            }

            return builder.ToImmutable();
        }

        private static TNode? TransformOptionalNode<TNode>(TNode? node, IQmlAstTransform transform, out bool changed)
            where TNode : AstNode
        {
            changed = false;
            if (node is null)
            {
                return null;
            }

            AstNode? transformedNode = TransformNode(node, transform, allowDelete: true);
            if (transformedNode is null)
            {
                changed = true;
                return null;
            }

            if (transformedNode is not TNode typedNode)
            {
                throw new InvalidOperationException($"Transform '{transform.GetType().Name}' produced '{transformedNode.GetType().Name}' where '{typeof(TNode).Name}' is required.");
            }

            changed = !ReferenceEquals(typedNode, node);
            return typedNode;
        }

        private static TNode TransformRequiredNode<TNode>(TNode node, IQmlAstTransform transform)
            where TNode : AstNode
        {
            AstNode? transformedNode = TransformNode(node, transform, allowDelete: false);
            return transformedNode as TNode
                ?? throw new InvalidOperationException($"Transform '{transform.GetType().Name}' produced '{transformedNode?.GetType().Name}' where '{typeof(TNode).Name}' is required.");
        }

        private static ImmutableArray<BindingValue> TransformValueList(ImmutableArray<BindingValue> values, IQmlAstTransform transform, out bool changed)
        {
            changed = false;
            ImmutableArray<BindingValue>.Builder builder = ImmutableArray.CreateBuilder<BindingValue>(values.Length);
            foreach (BindingValue value in values)
            {
                BindingValue transformedValue = TransformValue(value, transform);
                if (!ReferenceEquals(transformedValue, value))
                {
                    changed = true;
                }

                builder.Add(transformedValue);
            }

            if (!changed)
            {
                return values;
            }

            return builder.ToImmutable();
        }
    }
}

#pragma warning restore MA0048
