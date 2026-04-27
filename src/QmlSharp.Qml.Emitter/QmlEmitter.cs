using QmlSharp.Qml.Ast;

namespace QmlSharp.Qml.Emitter
{
    /// <summary>
    /// Default QML emitter implementation.
    /// </summary>
    public sealed class QmlEmitter : IQmlEmitter
    {
        /// <inheritdoc/>
        public string Emit(QmlDocument document, EmitOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(document);

            throw new NotSupportedException("QML document emission is implemented in later 03-qml-emitter steps.");
        }

        /// <inheritdoc/>
        public string EmitFragment(AstNode node, FragmentEmitOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(node);

            throw new NotSupportedException("QML fragment emission is implemented in later 03-qml-emitter steps.");
        }

        /// <inheritdoc/>
        public EmitResult EmitWithSourceMap(QmlDocument document, EmitOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(document);

            throw new NotSupportedException("QML source-map emission is implemented in later 03-qml-emitter steps.");
        }
    }
}
