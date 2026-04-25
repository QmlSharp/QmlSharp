#pragma warning disable MA0048

namespace QmlSharp.Qml.Ast.Serialization
{
    /// <summary>
    /// AST to and from JSON serialization contract.
    /// </summary>
    public interface IQmlAstSerializer
    {
        /// <summary>
        /// Serializes a document to compact JSON.
        /// </summary>
        /// <param name="document">Document to serialize.</param>
        /// <returns>JSON text.</returns>
        string ToJson(QmlDocument document);

        /// <summary>
        /// Serializes a document to pretty JSON.
        /// </summary>
        /// <param name="document">Document to serialize.</param>
        /// <returns>Indented JSON text.</returns>
        string ToPrettyJson(QmlDocument document);

        /// <summary>
        /// Deserializes JSON into a document.
        /// </summary>
        /// <param name="json">JSON text.</param>
        /// <returns>Deserialized document.</returns>
        QmlDocument FromJson(string json);

        /// <summary>
        /// Deep-clones a document.
        /// </summary>
        /// <param name="document">Document to clone.</param>
        /// <returns>Cloned document.</returns>
        QmlDocument Clone(QmlDocument document);
    }

    /// <summary>
    /// Exception thrown when AST JSON serialization or deserialization fails.
    /// </summary>
    public sealed class QmlAstSerializationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QmlAstSerializationException"/> class.
        /// </summary>
        /// <param name="message">Failure message.</param>
        public QmlAstSerializationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QmlAstSerializationException"/> class.
        /// </summary>
        /// <param name="message">Failure message.</param>
        /// <param name="inner">Inner exception.</param>
        public QmlAstSerializationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}

#pragma warning restore MA0048
