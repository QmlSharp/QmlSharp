namespace QmlSharp.Host.Interop
{
    /// <summary>Schema-derived metadata and generated callback for one native QML type registration.</summary>
    public sealed record QmlSharpTypeRegistrationEntry(
        string TypeName,
        string SchemaId,
        string CompilerSlotKey,
        QmlSharpTypeRegistrationCallback RegisterCallback);
}
