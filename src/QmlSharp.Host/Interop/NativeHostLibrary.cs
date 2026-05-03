using System.Diagnostics;
using System.Runtime.InteropServices;
using QmlSharp.Host.Exceptions;

namespace QmlSharp.Host.Interop
{
    internal sealed class NativeHostLibrary : INativeHostInterop, IDisposable
    {
        private readonly IntPtr libraryHandle;
        private readonly QmlsharpGetAbiVersionDelegate getAbiVersion;
        private readonly QmlsharpGetLastErrorDelegate getLastError;
        private readonly QmlsharpFreeStringDelegate freeString;
        private readonly QmlsharpMainThreadCallbackDelegate postCallbackThunk;
        private readonly QmlsharpInstanceCreatedDelegate instanceCreatedThunk;
        private readonly QmlsharpInstanceDestroyedDelegate instanceDestroyedThunk;
        private readonly QmlsharpCommandDelegate commandThunk;
        private readonly Dictionary<string, Delegate> loadedDelegates = new(StringComparer.Ordinal);
        private readonly Lock syncRoot = new();
        private Action<string, string, string>? instanceCreatedCallback;
        private Action<string>? instanceDestroyedCallback;
        private Action<string, string, string>? commandCallback;
        private int ownerManagedThreadId;
        private bool disposed;

        public NativeHostLibrary(string nativeLibraryPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(nativeLibraryPath);
            ownerManagedThreadId = Environment.CurrentManagedThreadId;
            postCallbackThunk = InvokePostedCallback;
            instanceCreatedThunk = InvokeInstanceCreated;
            instanceDestroyedThunk = InvokeInstanceDestroyed;
            commandThunk = InvokeCommand;

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

        public IntPtr EngineInit(IReadOnlyList<string> arguments)
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ThrowIfDisposed();

            IntPtr argv = IntPtr.Zero;
            IntPtr[] argumentPointers = new IntPtr[arguments.Count];
            try
            {
                for (int index = 0; index < arguments.Count; ++index)
                {
                    string? argument = arguments[index];
                    ArgumentNullException.ThrowIfNull(argument, nameof(arguments));
                    argumentPointers[index] = Marshal.StringToCoTaskMemUTF8(argument);
                }

                if (argumentPointers.Length > 0)
                {
                    argv = Marshal.AllocHGlobal(IntPtr.Size * argumentPointers.Length);
                    for (int index = 0; index < argumentPointers.Length; ++index)
                    {
                        Marshal.WriteIntPtr(argv, index * IntPtr.Size, argumentPointers[index]);
                    }
                }

                IntPtr engineHandle = LoadLazyDelegate<QmlsharpEngineInitDelegate>("qmlsharp_engine_init")(
                    argumentPointers.Length,
                    argv);
                if (engineHandle != IntPtr.Zero)
                {
                    ownerManagedThreadId = Environment.CurrentManagedThreadId;
                }

                return engineHandle;
            }
            finally
            {
                if (argv != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(argv);
                }

                foreach (IntPtr argumentPointer in argumentPointers.Where(static pointer => pointer != IntPtr.Zero))
                {
                    Marshal.FreeCoTaskMem(argumentPointer);
                }
            }
        }

        public void EngineShutdown(IntPtr engineHandle)
        {
            ThrowIfDisposed();
            LoadLazyDelegate<QmlsharpEngineShutdownDelegate>("qmlsharp_engine_shutdown")(engineHandle);
        }

        public int EngineExec(IntPtr engineHandle)
        {
            ThrowIfDisposed();
            return LoadLazyDelegate<QmlsharpEngineExecDelegate>("qmlsharp_engine_exec")(engineHandle);
        }

        public string? GetLastError()
        {
            ThrowIfDisposed();
            return NativeString.ReadAndFree(getLastError(), this);
        }

        internal IntPtr GetLastErrorPointerForTesting()
        {
            ThrowIfDisposed();
            return getLastError();
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

            GCHandle callbackHandle = GCHandle.Alloc(callback);
            try
            {
                LoadLazyDelegate<QmlsharpPostToMainThreadDelegate>("qmlsharp_post_to_main_thread")(
                    postCallbackThunk,
                    GCHandle.ToIntPtr(callbackHandle));

                string? nativeError = GetLastError();
                if (!string.IsNullOrWhiteSpace(nativeError))
                {
                    callbackHandle.Free();
                    throw new NativeHostException(-1, nativeError);
                }
            }
            catch
            {
                if (callbackHandle.IsAllocated)
                {
                    callbackHandle.Free();
                }

                throw;
            }
        }

        public int RegisterType(
            IntPtr engineHandle,
            string moduleUri,
            int versionMajor,
            int versionMinor,
            string typeName,
            string schemaId,
            string compilerSlotKey,
            QmlSharpTypeRegistrationCallback registerCallback)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleUri);
            ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
            ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
            ArgumentException.ThrowIfNullOrWhiteSpace(compilerSlotKey);
            ArgumentNullException.ThrowIfNull(registerCallback);
            ThrowIfDisposed();

            QmlsharpTypeRegistrationCallbackDelegate nativeCallback = CreateTypeRegistrationCallback(registerCallback);
            return LoadLazyDelegate<QmlsharpRegisterTypeDelegate>("qmlsharp_register_type")(
                engineHandle,
                moduleUri,
                versionMajor,
                versionMinor,
                typeName,
                schemaId,
                compilerSlotKey,
                nativeCallback);
        }

        public int RegisterModule(
            IntPtr engineHandle,
            string moduleUri,
            int versionMajor,
            int versionMinor,
            IReadOnlyList<QmlSharpTypeRegistrationEntry> entries)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleUri);
            ArgumentNullException.ThrowIfNull(entries);
            ThrowIfDisposed();

            if (entries.Count == 0)
            {
                return InvokeRegisterModule(engineHandle, moduleUri, versionMajor, versionMinor, IntPtr.Zero, 0);
            }

            using NativeModuleRegistrationBuffer buffer = CreateModuleRegistrationBuffer(entries);
            return InvokeRegisterModule(engineHandle, moduleUri, versionMajor, versionMinor, buffer.Pointer, entries.Count);
        }

        public void SetInstanceCallbacks(
            Action<string, string, string>? onCreated,
            Action<string>? onDestroyed)
        {
            ThrowIfDisposed();
            instanceCreatedCallback = onCreated;
            instanceDestroyedCallback = onDestroyed;
            LoadLazyDelegate<QmlsharpSetInstanceCallbacksDelegate>("qmlsharp_set_instance_callbacks")(
                onCreated is null ? null : instanceCreatedThunk,
                onDestroyed is null ? null : instanceDestroyedThunk);
        }

        public void InstanceReady(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            ThrowIfDisposed();
            LoadLazyDelegate<QmlsharpInstanceReadyDelegate>("qmlsharp_instance_ready")(instanceId);
        }

        public void SetCommandCallback(Action<string, string, string>? callback)
        {
            ThrowIfDisposed();
            commandCallback = callback;
            LoadLazyDelegate<QmlsharpSetCommandCallbackDelegate>("qmlsharp_set_command_callback")(
                callback is null ? null : commandThunk);
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

        public string? CaptureSnapshot(IntPtr engineHandle)
        {
            ThrowIfDisposed();
            return NativeString.ReadAndFree(
                LoadLazyDelegate<QmlsharpCaptureSnapshotDelegate>("qmlsharp_capture_snapshot")(engineHandle),
                this);
        }

        public int ReloadQml(IntPtr engineHandle, string qmlSourcePath)
        {
            ThrowIfDisposed();
            return LoadLazyDelegate<QmlsharpReloadQmlDelegate>("qmlsharp_reload_qml")(engineHandle, qmlSourcePath);
        }

        public void RestoreSnapshot(IntPtr engineHandle, string snapshotJson)
        {
            ThrowIfDisposed();
            LoadLazyDelegate<QmlsharpRestoreSnapshotDelegate>("qmlsharp_restore_snapshot")(engineHandle, snapshotJson);
        }

        public void ShowError(
            IntPtr engineHandle,
            string title,
            string message,
            string? filePath,
            int line,
            int column)
        {
            ThrowIfDisposed();
            LoadLazyDelegate<QmlsharpShowErrorDelegate>("qmlsharp_show_error")(engineHandle, title, message, filePath, line, column);
        }

        public void HideError(IntPtr engineHandle)
        {
            ThrowIfDisposed();
            LoadLazyDelegate<QmlsharpHideErrorDelegate>("qmlsharp_hide_error")(engineHandle);
        }

        public string? GetNativeInstanceInfo(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            ThrowIfDisposed();
            return NativeString.ReadAndFree(
                LoadLazyDelegate<QmlsharpGetInstanceInfoDelegate>("qmlsharp_get_instance_info")(instanceId),
                this);
        }

        public string? GetNativeAllInstances()
        {
            ThrowIfDisposed();
            return NativeString.ReadAndFree(
                LoadLazyDelegate<QmlsharpGetAllInstancesDelegate>("qmlsharp_get_all_instances")(),
                this);
        }

        public string? GetNativeMetrics()
        {
            ThrowIfDisposed();
            return NativeString.ReadAndFree(
                LoadLazyDelegate<QmlsharpGetMetricsDelegate>("qmlsharp_get_metrics")(),
                this);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                try
                {
                    if (loadedDelegates.ContainsKey("qmlsharp_set_command_callback"))
                    {
                        SetCommandCallback(null);
                    }

                    if (loadedDelegates.ContainsKey("qmlsharp_set_instance_callbacks"))
                    {
                        SetInstanceCallbacks(null, null);
                    }
                }
                catch (Exception exception) when (!IsCriticalException(exception))
                {
                    Trace.TraceError("QmlSharp native host dispose callback cleanup failed: {0}", exception);
                }

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

        private void InvokeInstanceCreated(IntPtr instanceId, IntPtr className, IntPtr compilerSlotKey)
        {
            try
            {
                instanceCreatedCallback?.Invoke(
                    ReadCallbackString(instanceId),
                    ReadCallbackString(className),
                    ReadCallbackString(compilerSlotKey));
            }
            catch (Exception exception) when (!IsCriticalException(exception))
            {
                ReportNonCriticalCallbackException(nameof(InvokeInstanceCreated), exception);
            }
        }

        private void InvokeInstanceDestroyed(IntPtr instanceId)
        {
            try
            {
                instanceDestroyedCallback?.Invoke(ReadCallbackString(instanceId));
            }
            catch (Exception exception) when (!IsCriticalException(exception))
            {
                ReportNonCriticalCallbackException(nameof(InvokeInstanceDestroyed), exception);
            }
        }

        private void InvokeCommand(IntPtr instanceId, IntPtr commandName, IntPtr argsJson)
        {
            try
            {
                commandCallback?.Invoke(
                    ReadCallbackString(instanceId),
                    ReadCallbackString(commandName),
                    ReadCallbackString(argsJson));
            }
            catch (Exception exception) when (!IsCriticalException(exception))
            {
                ReportNonCriticalCallbackException(nameof(InvokeCommand), exception);
            }
        }

        private static void InvokePostedCallback(IntPtr userData)
        {
            GCHandle callbackHandle = GCHandle.FromIntPtr(userData);
            try
            {
                if (callbackHandle.Target is Action callback)
                {
                    callback();
                }
            }
            finally
            {
                callbackHandle.Free();
            }
        }

        private static QmlsharpTypeRegistrationCallbackDelegate CreateTypeRegistrationCallback(
            QmlSharpTypeRegistrationCallback callback)
        {
            return (moduleUri, versionMajor, versionMinor, typeName) =>
            {
                try
                {
                    return callback(
                        ReadCallbackString(moduleUri),
                        versionMajor,
                        versionMinor,
                        ReadCallbackString(typeName));
                }
                catch (Exception exception) when (!IsCriticalException(exception))
                {
                    return -1;
                }
            };
        }

        private int InvokeRegisterModule(
            IntPtr engineHandle,
            string moduleUri,
            int versionMajor,
            int versionMinor,
            IntPtr entries,
            int entryCount)
        {
            return LoadLazyDelegate<QmlsharpRegisterModuleDelegate>("qmlsharp_register_module")(
                engineHandle,
                moduleUri,
                versionMajor,
                versionMinor,
                entries,
                entryCount);
        }

        private static NativeModuleRegistrationBuffer CreateModuleRegistrationBuffer(
            IReadOnlyList<QmlSharpTypeRegistrationEntry> entries)
        {
            NativeModuleRegistrationBuffer buffer = new(entries.Count);
            int entrySize = Marshal.SizeOf<QmlsharpTypeRegistrationEntryNative>();
            for (int index = 0; index < entries.Count; ++index)
            {
                QmlSharpTypeRegistrationEntry entry = entries[index];
                ValidateRegistrationEntry(entry);
                QmlsharpTypeRegistrationCallbackDelegate nativeCallback = CreateTypeRegistrationCallback(entry.RegisterCallback);
                buffer.NativeCallbacks.Add(nativeCallback);

                IntPtr typeName = buffer.AddString(entry.TypeName);
                IntPtr schemaId = buffer.AddString(entry.SchemaId);
                IntPtr compilerSlotKey = buffer.AddString(entry.CompilerSlotKey);
                QmlsharpTypeRegistrationEntryNative nativeEntry = new(
                    typeName,
                    schemaId,
                    compilerSlotKey,
                    Marshal.GetFunctionPointerForDelegate(nativeCallback));
                Marshal.StructureToPtr(nativeEntry, IntPtr.Add(buffer.Pointer, entrySize * index), fDeleteOld: false);
            }

            return buffer;
        }

        private static void ValidateRegistrationEntry(QmlSharpTypeRegistrationEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (string.IsNullOrWhiteSpace(entry.TypeName))
            {
                throw new ArgumentException("A native registration entry requires a non-empty type name.", nameof(entry));
            }

            if (string.IsNullOrWhiteSpace(entry.SchemaId))
            {
                throw new ArgumentException("A native registration entry requires a non-empty schema ID.", nameof(entry));
            }

            if (string.IsNullOrWhiteSpace(entry.CompilerSlotKey))
            {
                throw new ArgumentException("A native registration entry requires a non-empty compiler slot key.", nameof(entry));
            }

            if (entry.RegisterCallback is null)
            {
                throw new ArgumentException("A native registration entry requires a generated registration callback.", nameof(entry));
            }
        }

        private static string ReadCallbackString(IntPtr pointer)
        {
            return Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
        }

        private static void ReportNonCriticalCallbackException(string callbackName, Exception exception)
        {
            Trace.TraceError("QmlSharp native host callback '{0}' failed: {1}", callbackName, exception);
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        private static bool IsCriticalException(Exception exception)
        {
            return exception is OutOfMemoryException
                or StackOverflowException
                or AccessViolationException
                or AppDomainUnloadedException
                or BadImageFormatException
                or CannotUnloadAppDomainException
                or InvalidProgramException
                or ThreadAbortException;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct QmlsharpTypeRegistrationEntryNative(
            IntPtr typeName,
            IntPtr schemaId,
            IntPtr compilerSlotKey,
            IntPtr registerCallback)
        {
            private readonly IntPtr typeName = typeName;
            private readonly IntPtr schemaId = schemaId;
            private readonly IntPtr compilerSlotKey = compilerSlotKey;
            private readonly IntPtr registerCallback = registerCallback;
        }

        private sealed class NativeModuleRegistrationBuffer : IDisposable
        {
            private readonly List<IntPtr> stringPointers = [];

            public NativeModuleRegistrationBuffer(int entryCount)
            {
                Pointer = Marshal.AllocHGlobal(Marshal.SizeOf<QmlsharpTypeRegistrationEntryNative>() * entryCount);
            }

            public IntPtr Pointer { get; }

            public List<QmlsharpTypeRegistrationCallbackDelegate> NativeCallbacks { get; } = [];

            public IntPtr AddString(string value)
            {
                IntPtr pointer = Marshal.StringToCoTaskMemUTF8(value);
                stringPointers.Add(pointer);
                return pointer;
            }

            public void Dispose()
            {
                Marshal.FreeHGlobal(Pointer);
                foreach (IntPtr stringPointer in stringPointers)
                {
                    Marshal.FreeCoTaskMem(stringPointer);
                }

                GC.KeepAlive(NativeCallbacks);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QmlsharpGetAbiVersionDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr QmlsharpEngineInitDelegate(int argc, IntPtr argv);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void QmlsharpEngineShutdownDelegate(IntPtr engineHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QmlsharpEngineExecDelegate(IntPtr engineHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void QmlsharpMainThreadCallbackDelegate(IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void QmlsharpPostToMainThreadDelegate(
            QmlsharpMainThreadCallbackDelegate callback,
            IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QmlsharpTypeRegistrationCallbackDelegate(
            IntPtr moduleUri,
            int versionMajor,
            int versionMinor,
            IntPtr typeName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QmlsharpRegisterTypeDelegate(
            IntPtr engineHandle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string moduleUri,
            int versionMajor,
            int versionMinor,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string typeName,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string schemaId,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string compilerSlotKey,
            QmlsharpTypeRegistrationCallbackDelegate registerCallback);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QmlsharpRegisterModuleDelegate(
            IntPtr engineHandle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string moduleUri,
            int versionMajor,
            int versionMinor,
            IntPtr entries,
            int entryCount);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void QmlsharpInstanceCreatedDelegate(
            IntPtr instanceId,
            IntPtr className,
            IntPtr compilerSlotKey);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void QmlsharpInstanceDestroyedDelegate(IntPtr instanceId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void QmlsharpSetInstanceCallbacksDelegate(
            QmlsharpInstanceCreatedDelegate? onCreated,
            QmlsharpInstanceDestroyedDelegate? onDestroyed);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void QmlsharpInstanceReadyDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string instanceId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void QmlsharpCommandDelegate(
            IntPtr instanceId,
            IntPtr commandName,
            IntPtr argsJson);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void QmlsharpSetCommandCallbackDelegate(QmlsharpCommandDelegate? callback);

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

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr QmlsharpCaptureSnapshotDelegate(IntPtr engineHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QmlsharpReloadQmlDelegate(
            IntPtr engineHandle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string qmlSourcePath);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void QmlsharpRestoreSnapshotDelegate(
            IntPtr engineHandle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string snapshotJson);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void QmlsharpShowErrorDelegate(
            IntPtr engineHandle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string title,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string message,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? filePath,
            int line,
            int column);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void QmlsharpHideErrorDelegate(IntPtr engineHandle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr QmlsharpGetInstanceInfoDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string instanceId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr QmlsharpGetAllInstancesDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr QmlsharpGetMetricsDelegate();
    }
}
