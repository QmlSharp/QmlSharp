namespace QmlSharp.Compiler
{
    /// <summary>
    /// Serializes ViewModel schemas to and from the canonical runtime contract JSON shape.
    /// </summary>
    public interface IViewModelSchemaSerializer
    {
        /// <summary>Serializes a schema to stable UTF-8/LF JSON text.</summary>
        string Serialize(ViewModelSchema schema);

        /// <summary>Parses a schema JSON document.</summary>
        ViewModelSchema Deserialize(string json);
    }
}
