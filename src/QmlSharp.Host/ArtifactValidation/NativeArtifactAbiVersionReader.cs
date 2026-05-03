using QmlSharp.Host.Interop;

namespace QmlSharp.Host.ArtifactValidation
{
    internal sealed class NativeArtifactAbiVersionReader : IArtifactAbiVersionReader
    {
        public int ReadAbiVersion(string nativeLibraryPath)
        {
            using NativeHostLibrary library = new(nativeLibraryPath);
            return library.GetAbiVersion();
        }
    }
}
