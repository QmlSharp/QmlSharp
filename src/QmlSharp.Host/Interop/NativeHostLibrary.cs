using System.Runtime.InteropServices;

namespace QmlSharp.Host.Interop
{
    internal sealed class NativeHostLibrary : INativeHostInterop, IDisposable
    {
        private readonly IntPtr libraryHandle;
        private readonly QmlsharpGetAbiVersionDelegate getAbiVersion;
        private readonly QmlsharpGetLastErrorDelegate getLastError;
        private readonly QmlsharpFreeStringDelegate freeString;
        private readonly Dictionary<string, Delegate> loadedDelegates = new(StringComparer.Ordinal);
        private readonly Lock syncRoot = new();
        private readonly int ownerManagedThreadId;
        private bool disposed;

        public NativeHostLibrary(string nativeLibraryPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(nativeLibraryPath);
            ownerManagedThreadId = Environment.CurrentManagedThreadId;

            string fullPath = Path.GetFullPath(nativeLibraryPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("The QmlSharp native host library was not found.", fullPath);
            }

            IntPtr loadedLibraryHandle = NativeLibrary.Load(fullPath);
            try
            {
                libraryHandle = loadedLibraryHandle;
                getAbiVersion = LoadDelegate<QmlsharpGetAbiVersionDelegate>("qmlsharp_get_abi_version");
                getLastError = LoadDelegate<QmlsharpGetLastErrorDelegate>("qmlsharp_get_last_error");
                freeString = LoadDelegate<QmlsharpFreeStringDelegate>("qmlsharp_free_string");
            }
            catch
            {
                NativeLibrary.Free(loadedLibraryHandle);
                throw;
            }
        }

        public bool IsOnMainThread => Environment.CurrentManagedThreadId == ownerManagedThreadId;

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

        public void PostToMainThread(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            ThrowIfDisposed();
            if (!IsOnMainThread)
            {
                throw new InvalidOperationException(
                    "Native host main-thread dispatch is not available yet; call native host operations from the owner thread.");
            }

            callback();
        }

        public int SyncStateString(string instanceId, string propertyName, string? value)
        {
            ThrowIfDisposed();
            return LoadLazyDelegate<QmlsharpSyncStateStringDelegate>("qmlsharp_sync_state_string")(instanceId, propertyName, value);
        }

        public int SyncStateInt(string instanceId, string propertyName, int value)
        {
            ThrowIfDisposed();
            return LoadLazyDelegate<QmlsharpSyncStateIntDelegate>("qmlsharp_sync_state_int")(instanceId, propertyName, value);
        }

        public int SyncStateDouble(string instanceId, string propertyName, double value)
        {
            ThrowIfDisposed();
            return LoadLazyDelegate<QmlsharpSyncStateDoubleDelegate>("qmlsharp_sync_state_double")(instanceId, propertyName, value);
        }

        public int SyncStateBool(string instanceId, string propertyName, bool value)
        {
            ThrowIfDisposed();
            return LoadLazyDelegate<QmlsharpSyncStateBoolDelegate>("qmlsharp_sync_state_bool")(instanceId, propertyName, value ? 1 : 0);
        }

        public int SyncStateJson(string instanceId, string propertyName, string jsonValue)
        {
            ThrowIfDisposed();
            return LoadLazyDelegate<QmlsharpSyncStateJsonDelegate>("qmlsharp_sync_state_json")(instanceId, propertyName, jsonValue);
        }

        public int SyncStateBatch(string instanceId, string propertiesJson)
        {
            ThrowIfDisposed();
            return LoadLazyDelegate<QmlsharpSyncStateBatchDelegate>("qmlsharp_sync_state_batch")(instanceId, propertiesJson);
        }

        public int DispatchEffect(string instanceId, string effectName, string payloadJson)
        {
            ThrowIfDisposed();
            return LoadLazyDelegate<QmlsharpDispatchEffectDelegate>("qmlsharp_dispatch_effect")(instanceId, effectName, payloadJson);
        }

        public int BroadcastEffect(string className, string effectName, string payloadJson)
        {
            ThrowIfDisposed();
            return LoadLazyDelegate<QmlsharpBroadcastEffectDelegate>("qmlsharp_broadcast_effect")(className, effectName, payloadJson);
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

        private TDelegate LoadLazyDelegate<TDelegate>(string exportName)
            where TDelegate : Delegate
        {
            lock (syncRoot)
            {
                if (loadedDelegates.TryGetValue(exportName, out Delegate? existingDelegate))
                {
                    return (TDelegate)existingDelegate;
                }

                TDelegate loadedDelegate = LoadDelegate<TDelegate>(exportName);
                loadedDelegates.Add(exportName, loadedDelegate);
                return loadedDelegate;
            }
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

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QmlsharpSyncStateStringDelegate(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string instanceId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string propertyName,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QmlsharpSyncStateIntDelegate(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string instanceId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string propertyName,
            int value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QmlsharpSyncStateDoubleDelegate(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string instanceId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string propertyName,
            double value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QmlsharpSyncStateBoolDelegate(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string instanceId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string propertyName,
            int value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QmlsharpSyncStateJsonDelegate(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string instanceId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string propertyName,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string jsonValue);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QmlsharpSyncStateBatchDelegate(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string instanceId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string propertiesJson);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QmlsharpDispatchEffectDelegate(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string instanceId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string effectName,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string payloadJson);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QmlsharpBroadcastEffectDelegate(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string className,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string effectName,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string payloadJson);
    }
}
