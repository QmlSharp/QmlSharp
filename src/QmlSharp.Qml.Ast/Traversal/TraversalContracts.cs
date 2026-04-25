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
        /// <summary>
        /// Initializes a new instance of the <see cref="WalkerContext"/> class.
        /// </summary>
        /// <param name="path">Ancestor path from root to current parent.</param>
        /// <param name="parent">Immediate parent, or null for root.</param>
        /// <param name="depth">Current depth (root document is depth 0).</param>
        public WalkerContext(ImmutableArray<AstNode> path, AstNode? parent, int depth)
        {
            Path = path;
            Parent = parent;
            Depth = depth;
        }

        /// <summary>
        /// Gets ancestors from root to the current node's parent.
        /// </summary>
        public ImmutableArray<AstNode> Path { get; }

        /// <summary>
        /// Gets the immediate parent node, or null at root.
        /// </summary>
        public AstNode? Parent { get; }

        /// <summary>
        /// Gets current traversal depth (document root is 0).
        /// </summary>
        public int Depth { get; }
    }

    /// <summary>
    /// Base visitor contract for AST traversal.
    /// </summary>
    public abstract class QmlAstVisitor
    {
        /// <summary>
        /// Visits a node.
        /// </summary>
        /// <param name="node">Node to visit.</param>
        /// <returns>True to continue traversal; otherwise false.</returns>
        public virtual bool Visit(AstNode node) => true;

        /// <summary>
        /// Accepts a document root for traversal.
        /// </summary>
        /// <param name="document">Document root.</param>
        public abstract void Accept(QmlDocument document);
    }
}

#pragma warning restore MA0048
