using QmlSharp.Host.ArtifactValidation;
using QmlSharp.Host.Interop;

namespace QmlSharp.Host.Tests.ArtifactValidation
{
    internal sealed class FakeAbiVersionReader : IArtifactAbiVersionReader
    {
        private readonly int version;

        public FakeAbiVersionReader(int? version = null)
        {
            this.version = version ?? NativeHostAbi.SupportedAbiVersion;
        }

        public int Calls { get; private set; }

        public string? LastPath { get; private set; }

        public int ReadAbiVersion(string nativeLibraryPath)
        {
            Calls++;
            LastPath = nativeLibraryPath;
            return version;
        }
    }
}
