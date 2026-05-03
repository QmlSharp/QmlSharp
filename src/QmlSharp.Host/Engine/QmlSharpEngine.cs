using QmlSharp.Host.Interop;

namespace QmlSharp.Host.Engine
{
    /// <summary>Public entry point for the managed native-host facade.</summary>
    public sealed class QmlSharpEngine
    {
        /// <summary>The native ABI version this managed host expects.</summary>
        public int ExpectedAbiVersion => NativeHostAbi.SupportedAbiVersion;
    }
}
