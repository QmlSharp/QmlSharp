using System.Runtime.InteropServices;

namespace QmlSharp.Host.Interop
{
    internal sealed class NativeHostLibrary : INativeHostInterop, IDisposable
    {
        private readonly IntPtr libraryHandle;
        private readonly QmlsharpGetAbiVersionDelegate getAbiVersion;
        private readonly QmlsharpGetLastErrorDelegate getLastError;
        private readonly QmlsharpFreeStringDelegate freeString;
        private bool disposed;

        public NativeHostLibrary(string nativeLibraryPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(nativeLibraryPath);

            string fullPath = Path.GetFullPath(nativeLibraryPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("The QmlSharp native host library was not found.", fullPath);
            }

            libraryHandle = NativeLibrary.Load(fullPath);
            getAbiVersion = LoadDelegate<QmlsharpGetAbiVersionDelegate>("qmlsharp_get_abi_version");
            getLastError = LoadDelegate<QmlsharpGetLastErrorDelegate>("qmlsharp_get_last_error");
            freeString = LoadDelegate<QmlsharpFreeStringDelegate>("qmlsharp_free_string");
        }

        public int GetAbiVersion()
        {
            ThrowIfDisposed();
            return getAbiVersion();
        }

        public string? GetLastError()
        {
            ThrowIfDisposed();
            return NativeString.ReadAndFree(getLastError(), this);
        }

        public void FreeString(IntPtr pointer)
        {
            ThrowIfDisposed();
            freeString(pointer);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                NativeLibrary.Free(libraryHandle);
                disposed = true;
            }

            GC.SuppressFinalize(this);
        }

        private TDelegate LoadDelegate<TDelegate>(string exportName)
            where TDelegate : Delegate
        {
            IntPtr exportPointer = NativeLibrary.GetExport(libraryHandle, exportName);
            return Marshal.GetDelegateForFunctionPointer<TDelegate>(exportPointer);
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QmlsharpGetAbiVersionDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr QmlsharpGetLastErrorDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void QmlsharpFreeStringDelegate(IntPtr pointer);
    }
}
