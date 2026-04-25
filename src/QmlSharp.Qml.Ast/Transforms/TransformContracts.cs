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
    /// Contract placeholder for the immutable transform pipeline.
    /// </summary>
    public sealed class QmlAstTransformer
    {
    }
}

#pragma warning restore MA0048
