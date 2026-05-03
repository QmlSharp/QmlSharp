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

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void MainThreadDispatch_FromWorkerThreadFailsFast()
        {
            using NativeHostLibrary library = new(NativeTestLibrary.Resolve());
            Exception? capturedException = null;

            Assert.True(library.IsOnMainThread);
            Thread worker = new(() =>
            {
                capturedException = Record.Exception(() => library.PostToMainThread(static () => { }));
            });
            worker.Start();
            worker.Join();

            InvalidOperationException exception = Assert.IsType<InvalidOperationException>(capturedException);
            Assert.Contains("main-thread dispatch is not available", exception.Message, StringComparison.Ordinal);
        }
    }
}
