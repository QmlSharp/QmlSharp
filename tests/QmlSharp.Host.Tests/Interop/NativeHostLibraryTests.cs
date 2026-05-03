using QmlSharp.Host.Interop;
using QmlSharp.Host.Tests.Fixtures;

namespace QmlSharp.Host.Tests.Interop
{
    public sealed class NativeHostLibraryTests
    {
        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void GetAbiVersion_BuiltNativeLibrary_ReturnsLockedVersion()
        {
            using NativeHostLibrary library = new(NativeTestLibrary.Resolve());

            Assert.Equal(NativeHostAbi.SupportedAbiVersion, library.GetAbiVersion());
        }

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void FreeString_NullPointer_DoesNotThrow()
        {
            using NativeHostLibrary library = new(NativeTestLibrary.Resolve());

            Exception? exception = Record.Exception(() => library.FreeString(IntPtr.Zero));

            Assert.Null(exception);
        }

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void GetLastError_WhenNoNativeError_ReturnsNull()
        {
            using NativeHostLibrary library = new(NativeTestLibrary.Resolve());

            Assert.Null(library.GetLastError());
        }
    }
}
