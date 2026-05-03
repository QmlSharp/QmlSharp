namespace QmlSharp.Host.ArtifactValidation
{
    /// <summary>Reads the ABI version exposed by a native QmlSharp host library.</summary>
    public interface IArtifactAbiVersionReader
    {
        /// <summary>Reads the ABI version from the native library at <paramref name="nativeLibraryPath" />.</summary>
        int ReadAbiVersion(string nativeLibraryPath);
    }
}
