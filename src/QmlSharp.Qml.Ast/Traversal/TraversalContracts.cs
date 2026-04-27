using System.Collections.Immutable;

#pragma warning disable MA0048

namespace QmlSharp.Qml.Ast.Traversal
{
    /// <summary>
    /// Delegate called before visiting a node's children. Return false to skip its subtree.
    /// </summary>
    /// <param name="node">Current node.</param>
    /// <param name="context">Walker context.</param>
    /// <returns>True to continue into children; false to skip.</returns>
    public delegate bool WalkerEnterDelegate(AstNode node, WalkerContext context);

    /// <summary>
    /// Delegate called after visiting a node's children.
    /// </summary>
    /// <param name="node">Current node.</param>
    /// <param name="context">Walker context.</param>
    public delegate void WalkerLeaveDelegate(AstNode node, WalkerContext context);

    /// <summary>
    /// Traversal context for walker callbacks.
    /// </summary>
    public sealed class WalkerContext
    {
        private readonly AncestorPath? _ancestorPath;
        private ImmutableArray<AstNode>? _materializedPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="WalkerContext"/> class.
        /// </summary>
        /// <param name="path">Ancestor path from root to current parent.</param>
        /// <param name="parent">Immediate parent, or null for root.</param>
        /// <param name="depth">Current depth (root document is depth 0).</param>
        public WalkerContext(ImmutableArray<AstNode> path, AstNode? parent, int depth)
        {
            _materializedPath = path;
            Parent = parent;
            Depth = depth;
        }

        internal WalkerContext(AncestorPath? ancestorPath, AstNode? parent, int depth)
        {
            _ancestorPath = ancestorPath;
            Parent = parent;
            Depth = depth;
        }

        /// <summary>
        /// Gets ancestors from root to the current node's parent.
        /// </summary>
        public ImmutableArray<AstNode> Path
        {
            get
            {
                if (_materializedPath.HasValue)
                {
                    return _materializedPath.GetValueOrDefault();
                }

                ImmutableArray<AstNode> path = _ancestorPath?.ToImmutableArray() ?? ImmutableArray<AstNode>.Empty;
                _materializedPath = path;
                return path;
            }
        }

        /// <summary>
        /// Gets the immediate parent node, or null at root.
        /// </summary>
        public AstNode? Parent { get; }

        /// <summary>
        /// Gets current traversal depth (document root is 0).
        /// </summary>
        public int Depth { get; }

        internal sealed class AncestorPath
        {
            public AncestorPath(AstNode node, AncestorPath? previous)
            {
                Node = node;
                Previous = previous;
                Length = previous?.Length + 1 ?? 1;
            }

            public AstNode Node { get; }

            public AncestorPath? Previous { get; }

            public int Length { get; }

            public ImmutableArray<AstNode> ToImmutableArray()
            {
                ImmutableArray<AstNode>.Builder builder = ImmutableArray.CreateBuilder<AstNode>(Length);
                builder.Count = Length;

                AncestorPath? current = this;
                for (int index = Length - 1; current is not null; index--)
                {
                    builder[index] = current.Node;
                    current = current.Previous;
                }

                return builder.MoveToImmutable();
            }
        }
    }

    /// <summary>
    /// Base visitor contract for AST traversal.
    /// </summary>
    public abstract class QmlAstVisitor
    {
        /// <summary>
        /// Visits a node by dispatching to its concrete node-type method.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal into the node's subtree; otherwise false.</returns>
        public virtual bool Visit(AstNode node)
        {
            ArgumentNullException.ThrowIfNull(node);

            return node switch
            {
                QmlDocument document => VisitDocument(document),
                ImportNode importNode => VisitImport(importNode),
                PragmaNode pragmaNode => VisitPragma(pragmaNode),
                ObjectDefinitionNode objectDefinitionNode => VisitObjectDefinition(objectDefinitionNode),
                InlineComponentNode inlineComponentNode => VisitInlineComponent(inlineComponentNode),
                IdAssignmentNode idAssignmentNode => VisitIdAssignment(idAssignmentNode),
                PropertyDeclarationNode propertyDeclarationNode => VisitPropertyDeclaration(propertyDeclarationNode),
                PropertyAliasNode propertyAliasNode => VisitPropertyAlias(propertyAliasNode),
                BindingNode bindingNode => VisitBinding(bindingNode),
                GroupedBindingNode groupedBindingNode => VisitGroupedBinding(groupedBindingNode),
                AttachedBindingNode attachedBindingNode => VisitAttachedBinding(attachedBindingNode),
                ArrayBindingNode arrayBindingNode => VisitArrayBinding(arrayBindingNode),
                BehaviorOnNode behaviorOnNode => VisitBehaviorOn(behaviorOnNode),
                SignalDeclarationNode signalDeclarationNode => VisitSignalDeclaration(signalDeclarationNode),
                SignalHandlerNode signalHandlerNode => VisitSignalHandler(signalHandlerNode),
                FunctionDeclarationNode functionDeclarationNode => VisitFunctionDeclaration(functionDeclarationNode),
                EnumDeclarationNode enumDeclarationNode => VisitEnumDeclaration(enumDeclarationNode),
                CommentNode commentNode => VisitComment(commentNode),
                _ => throw new ArgumentOutOfRangeException(nameof(node), $"Unsupported AST node type '{node.GetType().FullName}'."),
            };
        }

        /// <summary>
        /// Visits a <see cref="QmlDocument"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitDocument(QmlDocument node) => true;

        /// <summary>
        /// Visits an <see cref="ImportNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitImport(ImportNode node) => true;

        /// <summary>
        /// Visits a <see cref="PragmaNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitPragma(PragmaNode node) => true;

        /// <summary>
        /// Visits an <see cref="ObjectDefinitionNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitObjectDefinition(ObjectDefinitionNode node) => true;

        /// <summary>
        /// Visits an <see cref="InlineComponentNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitInlineComponent(InlineComponentNode node) => true;

        /// <summary>
        /// Visits an <see cref="IdAssignmentNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitIdAssignment(IdAssignmentNode node) => true;

        /// <summary>
        /// Visits a <see cref="PropertyDeclarationNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitPropertyDeclaration(PropertyDeclarationNode node) => true;

        /// <summary>
        /// Visits a <see cref="PropertyAliasNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitPropertyAlias(PropertyAliasNode node) => true;

        /// <summary>
        /// Visits a <see cref="BindingNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitBinding(BindingNode node) => true;

        /// <summary>
        /// Visits a <see cref="GroupedBindingNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitGroupedBinding(GroupedBindingNode node) => true;

        /// <summary>
        /// Visits an <see cref="AttachedBindingNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitAttachedBinding(AttachedBindingNode node) => true;

        /// <summary>
        /// Visits an <see cref="ArrayBindingNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitArrayBinding(ArrayBindingNode node) => true;

        /// <summary>
        /// Visits a <see cref="BehaviorOnNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitBehaviorOn(BehaviorOnNode node) => true;

        /// <summary>
        /// Visits a <see cref="SignalDeclarationNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitSignalDeclaration(SignalDeclarationNode node) => true;

        /// <summary>
        /// Visits a <see cref="SignalHandlerNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitSignalHandler(SignalHandlerNode node) => true;

        /// <summary>
        /// Visits a <see cref="FunctionDeclarationNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitFunctionDeclaration(FunctionDeclarationNode node) => true;

        /// <summary>
        /// Visits an <see cref="EnumDeclarationNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitEnumDeclaration(EnumDeclarationNode node) => true;

        /// <summary>
        /// Visits a <see cref="CommentNode"/> node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool VisitComment(CommentNode node) => true;

        /// <summary>
        /// Walks the AST rooted at <paramref name="document"/> in deterministic depth-first order.
        /// Returning false from a visit method skips that node's subtree.
        /// </summary>
        /// <param name="document">Document root.</param>
        public virtual void Accept(QmlDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            _ = VisitNode(document);
        }

        private bool VisitNode(AstNode node)
        {
            if (!Visit(node))
            {
                return false;
            }

            VisitLeadingComments(node);
            VisitChildren(node);
            VisitTrailingComment(node);

            return true;
        }

        private void VisitLeadingComments(AstNode node)
        {
            foreach (CommentNode leadingComment in node.LeadingComments)
            {
                _ = VisitNode(leadingComment);
            }
        }

        private void VisitChildren(AstNode node)
        {
            switch (node)
            {
                case QmlDocument document:
                    VisitDocumentChildren(document);
                    break;

                case ObjectDefinitionNode objectDefinitionNode:
                    VisitNodeList(objectDefinitionNode.Members);
                    break;

                case InlineComponentNode inlineComponentNode:
                    _ = VisitNode(inlineComponentNode.Body);
                    break;

                case PropertyDeclarationNode propertyDeclarationNode when propertyDeclarationNode.InitialValue is not null:
                    VisitBindingValue(propertyDeclarationNode.InitialValue);
                    break;

                case BindingNode bindingNode:
                    VisitBindingValue(bindingNode.Value);
                    break;

                case GroupedBindingNode groupedBindingNode:
                    VisitBindingNodeList(groupedBindingNode.Bindings);
                    break;

                case AttachedBindingNode attachedBindingNode:
                    VisitBindingNodeList(attachedBindingNode.Bindings);
                    break;

                case ArrayBindingNode arrayBindingNode:
                    VisitBindingValueList(arrayBindingNode.Elements);
                    break;

                case BehaviorOnNode behaviorOnNode:
                    _ = VisitNode(behaviorOnNode.Animation);
                    break;
            }
        }

        private void VisitDocumentChildren(QmlDocument document)
        {
            foreach (PragmaNode pragmaNode in document.Pragmas)
            {
                _ = VisitNode(pragmaNode);
            }

            foreach (ImportNode importNode in document.Imports)
            {
                _ = VisitNode(importNode);
            }

            _ = VisitNode(document.RootObject);
        }

        private void VisitNodeList(ImmutableArray<AstNode> nodes)
        {
            foreach (AstNode childNode in nodes)
            {
                _ = VisitNode(childNode);
            }
        }

        private void VisitBindingNodeList(ImmutableArray<BindingNode> bindings)
        {
            foreach (BindingNode bindingNode in bindings)
            {
                _ = VisitNode(bindingNode);
            }
        }

        private void VisitBindingValueList(ImmutableArray<BindingValue> values)
        {
            foreach (BindingValue value in values)
            {
                VisitBindingValue(value);
            }
        }

        private void VisitTrailingComment(AstNode node)
        {
            if (node.TrailingComment is not null)
            {
                _ = VisitNode(node.TrailingComment);
            }
        }

        private void VisitBindingValue(BindingValue value)
        {
            switch (value)
            {
                case ObjectValue objectValue:
                    _ = VisitNode(objectValue.Object);
                    break;

                case ArrayValue arrayValue:
                    foreach (BindingValue item in arrayValue.Elements)
                    {
                        VisitBindingValue(item);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Generic AST walker with enter/leave callbacks and subtree skipping support.
    /// </summary>
    public static class QmlAstWalker
    {
        /// <summary>
        /// Walks a document root in deterministic depth-first order.
        /// </summary>
        /// <param name="document">Document root.</param>
        /// <param name="enter">Enter callback invoked in pre-order.</param>
        /// <param name="leave">Leave callback invoked in post-order for non-skipped nodes.</param>
        public static void Walk(QmlDocument document, WalkerEnterDelegate? enter, WalkerLeaveDelegate? leave)
        {
            Walk((AstNode)document, enter, leave);
        }

        /// <summary>
        /// Walks an AST node in deterministic depth-first order.
        /// </summary>
        /// <param name="node">Traversal root node.</param>
        /// <param name="enter">Enter callback invoked in pre-order.</param>
        /// <param name="leave">Leave callback invoked in post-order for non-skipped nodes.</param>
        public static void Walk(AstNode node, WalkerEnterDelegate? enter, WalkerLeaveDelegate? leave)
        {
            ArgumentNullException.ThrowIfNull(node);
            WalkNode(node, parent: null, path: null, depth: 0, enter, leave);
        }

        private static void WalkNode(
            AstNode node,
            AstNode? parent,
            WalkerContext.AncestorPath? path,
            int depth,
            WalkerEnterDelegate? enter,
            WalkerLeaveDelegate? leave)
        {
            WalkerContext context = new(path, parent, depth);
            bool shouldContinue = enter?.Invoke(node, context) ?? true;
            if (!shouldContinue)
            {
                return;
            }

            WalkerContext.AncestorPath childPath = new(node, path);
            WalkChildren(node, childPath, depth + 1, enter, leave);

            leave?.Invoke(node, context);
        }

        private static void WalkChildren(
            AstNode node,
            WalkerContext.AncestorPath path,
            int depth,
            WalkerEnterDelegate? enter,
            WalkerLeaveDelegate? leave)
        {
            WalkLeadingComments(node, path, depth, enter, leave);
            WalkNodeChildren(node, path, depth, enter, leave);
            WalkTrailingComment(node, path, depth, enter, leave);
        }

        private static void WalkLeadingComments(
            AstNode node,
            WalkerContext.AncestorPath path,
            int depth,
            WalkerEnterDelegate? enter,
            WalkerLeaveDelegate? leave)
        {
            foreach (CommentNode leadingComment in node.LeadingComments)
            {
                WalkNode(leadingComment, node, path, depth, enter, leave);
            }
        }

        private static void WalkNodeChildren(
            AstNode node,
            WalkerContext.AncestorPath path,
            int depth,
            WalkerEnterDelegate? enter,
            WalkerLeaveDelegate? leave)
        {
            switch (node)
            {
                case QmlDocument document:
                    WalkDocumentChildren(document, path, depth, enter, leave);
                    break;

                case ObjectDefinitionNode objectDefinitionNode:
                    WalkNodeList(objectDefinitionNode.Members, objectDefinitionNode, path, depth, enter, leave);
                    break;

                case InlineComponentNode inlineComponentNode:
                    WalkNode(inlineComponentNode.Body, inlineComponentNode, path, depth, enter, leave);
                    break;

                case PropertyDeclarationNode propertyDeclarationNode when propertyDeclarationNode.InitialValue is not null:
                    WalkBindingValue(propertyDeclarationNode.InitialValue, propertyDeclarationNode, path, depth, enter, leave);
                    break;

                case BindingNode bindingNode:
                    WalkBindingValue(bindingNode.Value, bindingNode, path, depth, enter, leave);
                    break;

                case GroupedBindingNode groupedBindingNode:
                    WalkBindingNodeList(groupedBindingNode.Bindings, groupedBindingNode, path, depth, enter, leave);
                    break;

                case AttachedBindingNode attachedBindingNode:
                    WalkBindingNodeList(attachedBindingNode.Bindings, attachedBindingNode, path, depth, enter, leave);
                    break;

                case ArrayBindingNode arrayBindingNode:
                    WalkBindingValueList(arrayBindingNode.Elements, arrayBindingNode, path, depth, enter, leave);
                    break;

                case BehaviorOnNode behaviorOnNode:
                    WalkNode(behaviorOnNode.Animation, behaviorOnNode, path, depth, enter, leave);
                    break;
            }
        }

        private static void WalkDocumentChildren(
            QmlDocument document,
            WalkerContext.AncestorPath path,
            int depth,
            WalkerEnterDelegate? enter,
            WalkerLeaveDelegate? leave)
        {
            foreach (PragmaNode pragmaNode in document.Pragmas)
            {
                WalkNode(pragmaNode, document, path, depth, enter, leave);
            }

            foreach (ImportNode importNode in document.Imports)
            {
                WalkNode(importNode, document, path, depth, enter, leave);
            }

            WalkNode(document.RootObject, document, path, depth, enter, leave);
        }

        private static void WalkNodeList(
            ImmutableArray<AstNode> nodes,
            AstNode parent,
            WalkerContext.AncestorPath path,
            int depth,
            WalkerEnterDelegate? enter,
            WalkerLeaveDelegate? leave)
        {
            foreach (AstNode childNode in nodes)
            {
                WalkNode(childNode, parent, path, depth, enter, leave);
            }
        }

        private static void WalkBindingNodeList(
            ImmutableArray<BindingNode> bindings,
            AstNode parent,
            WalkerContext.AncestorPath path,
            int depth,
            WalkerEnterDelegate? enter,
            WalkerLeaveDelegate? leave)
        {
            foreach (BindingNode bindingNode in bindings)
            {
                WalkNode(bindingNode, parent, path, depth, enter, leave);
            }
        }

        private static void WalkBindingValueList(
            ImmutableArray<BindingValue> values,
            AstNode parent,
            WalkerContext.AncestorPath path,
            int depth,
            WalkerEnterDelegate? enter,
            WalkerLeaveDelegate? leave)
        {
            foreach (BindingValue value in values)
            {
                WalkBindingValue(value, parent, path, depth, enter, leave);
            }
        }

        private static void WalkTrailingComment(
            AstNode node,
            WalkerContext.AncestorPath path,
            int depth,
            WalkerEnterDelegate? enter,
            WalkerLeaveDelegate? leave)
        {
            if (node.TrailingComment is not null)
            {
                WalkNode(node.TrailingComment, node, path, depth, enter, leave);
            }
        }

        private static void WalkBindingValue(
            BindingValue value,
            AstNode parent,
            WalkerContext.AncestorPath path,
            int depth,
            WalkerEnterDelegate? enter,
            WalkerLeaveDelegate? leave)
        {
            switch (value)
            {
                case ObjectValue objectValue:
                    WalkNode(objectValue.Object, parent, path, depth, enter, leave);
                    break;

                case ArrayValue arrayValue:
                    foreach (BindingValue element in arrayValue.Elements)
                    {
                        WalkBindingValue(element, parent, path, depth, enter, leave);
                    }

                    break;
            }
        }
    }
}

#pragma warning restore MA0048
