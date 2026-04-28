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
            _ = ResolvedEmitOptions.From(options);

            throw new NotSupportedException("QML document emission is implemented in later 03-qml-emitter steps.");
        }

        /// <inheritdoc/>
        public string EmitFragment(AstNode node, FragmentEmitOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(node);
            _ = ResolveFragmentOptions(options);

            throw new NotSupportedException("QML fragment emission is implemented in later 03-qml-emitter steps.");
        }

        /// <inheritdoc/>
        public EmitResult EmitWithSourceMap(QmlDocument document, EmitOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(document);
            _ = ResolvedEmitOptions.From(options);

            throw new NotSupportedException("QML source-map emission is implemented in later 03-qml-emitter steps.");
        }

        private static ResolvedEmitOptions ResolveFragmentOptions(FragmentEmitOptions? options)
        {
            if (options?.IndentLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.IndentLevel, "Fragment indentation level cannot be negative.");
            }

            return ResolvedEmitOptions.From(options?.Options);
        }
    }
}
