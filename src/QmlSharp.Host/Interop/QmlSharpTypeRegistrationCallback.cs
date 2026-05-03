namespace QmlSharp.Host.Interop
{
    /// <summary>Managed callback used by native type registration to invoke generated QObject registration code.</summary>
    public delegate int QmlSharpTypeRegistrationCallback(
        string moduleUri,
        int versionMajor,
        int versionMinor,
        string typeName);
}
