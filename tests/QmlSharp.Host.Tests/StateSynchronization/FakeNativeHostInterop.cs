using QmlSharp.Host.Interop;

namespace QmlSharp.Host.Tests.StateSynchronization
{
    internal sealed class FakeNativeHostInterop : INativeHostInterop
    {
        private readonly Lock syncRoot = new();
        private readonly List<SyncCall> calls = [];
        private readonly List<OverlayCall> overlayCalls = [];

        public bool IsOnMainThread { get; set; } = true;

        public int PostToMainThreadCallCount { get; private set; }

        public int NextResultCode { get; set; }

        public int ReloadResultCode { get; set; }

        public IntPtr EngineHandle { get; set; } = new(1);

        public int EngineExecResultCode { get; set; }

        public int RegisterResultCode { get; set; }

        public string? SnapshotJson { get; set; } = "{\"window\":{}}";

        public string? LastError { get; set; }

        public Action? OnReload { get; set; }

        public Exception? HideErrorException { get; set; }

        public IReadOnlyList<SyncCall> Calls
        {
            get
            {
                lock (syncRoot)
                {
                    return calls.ToArray();
                }
            }
        }

        public IReadOnlyList<OverlayCall> OverlayCalls
        {
            get
            {
                lock (syncRoot)
                {
                    return overlayCalls.ToArray();
                }
            }
        }

        public int GetAbiVersion()
        {
            return NativeHostAbi.SupportedAbiVersion;
        }

        public IntPtr EngineInit(IReadOnlyList<string> arguments)
        {
            AddCall(new SyncCall("engine_init", arguments.Count.ToString(), PropertyName: null, string.Join("|", arguments)));
            return EngineHandle;
        }

        public void EngineShutdown(IntPtr engineHandle)
        {
            AddCall(new SyncCall("engine_shutdown", engineHandle.ToString(), PropertyName: null, Value: null));
        }

        public int EngineExec(IntPtr engineHandle)
        {
            AddCall(new SyncCall("engine_exec", engineHandle.ToString(), PropertyName: null, Value: null));
            return EngineExecResultCode;
        }

        public string? GetLastError()
        {
            return LastError;
        }

        public void FreeString(IntPtr pointer)
        {
        }

        public void PostToMainThread(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            lock (syncRoot)
            {
                PostToMainThreadCallCount++;
            }

            callback();
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
            AddCall(new SyncCall("register_type", moduleUri, typeName, schemaId));
            if (RegisterResultCode == 0)
            {
                _ = registerCallback(moduleUri, versionMajor, versionMinor, typeName);
            }

            return RegisterResultCode;
        }

        public int RegisterModule(
            IntPtr engineHandle,
            string moduleUri,
            int versionMajor,
            int versionMinor,
            IReadOnlyList<QmlSharpTypeRegistrationEntry> entries)
        {
            AddCall(new SyncCall("register_module", moduleUri, versionMajor.ToString(), entries.Count));
            if (RegisterResultCode == 0)
            {
                foreach (QmlSharpTypeRegistrationEntry entry in entries)
                {
                    _ = entry.RegisterCallback(moduleUri, versionMajor, versionMinor, entry.TypeName);
                }
            }

            return RegisterResultCode;
        }

        public Action<string, string, string>? InstanceCreatedCallback { get; private set; }

        public Action<string>? InstanceDestroyedCallback { get; private set; }

        public Action<string, string, string>? CommandCallback { get; private set; }

        public void SetInstanceCallbacks(
            Action<string, string, string>? onCreated,
            Action<string>? onDestroyed)
        {
            InstanceCreatedCallback = onCreated;
            InstanceDestroyedCallback = onDestroyed;
            AddCall(new SyncCall("set_instance_callbacks", "callbacks", PropertyName: null, onCreated is null ? "clear" : "set"));
        }

        public void InstanceReady(string instanceId)
        {
            AddCall(new SyncCall("instance_ready", instanceId, PropertyName: null, Value: null));
        }

        public void SetCommandCallback(Action<string, string, string>? callback)
        {
            CommandCallback = callback;
            AddCall(new SyncCall("set_command_callback", "callback", PropertyName: null, callback is null ? "clear" : "set"));
        }

        public int SyncStateString(string instanceId, string propertyName, string? value)
        {
            AddCall(new SyncCall("string", instanceId, propertyName, value));
            return NextResultCode;
        }

        public int SyncStateInt(string instanceId, string propertyName, int value)
        {
            AddCall(new SyncCall("int", instanceId, propertyName, value));
            return NextResultCode;
        }

        public int SyncStateDouble(string instanceId, string propertyName, double value)
        {
            AddCall(new SyncCall("double", instanceId, propertyName, value));
            return NextResultCode;
        }

        public int SyncStateBool(string instanceId, string propertyName, bool value)
        {
            AddCall(new SyncCall("bool", instanceId, propertyName, value));
            return NextResultCode;
        }

        public int SyncStateJson(string instanceId, string propertyName, string jsonValue)
        {
            AddCall(new SyncCall("json", instanceId, propertyName, jsonValue));
            return NextResultCode;
        }

        public int SyncStateBatch(string instanceId, string propertiesJson)
        {
            AddCall(new SyncCall("batch", instanceId, PropertyName: null, propertiesJson));
            return NextResultCode;
        }

        public int DispatchEffect(string instanceId, string effectName, string payloadJson)
        {
            AddCall(new SyncCall("effect", instanceId, effectName, payloadJson));
            return NextResultCode;
        }

        public int BroadcastEffect(string className, string effectName, string payloadJson)
        {
            AddCall(new SyncCall("broadcast", className, effectName, payloadJson));
            return NextResultCode;
        }

        public string? CaptureSnapshot(IntPtr engineHandle)
        {
            AddCall(new SyncCall("capture", engineHandle.ToString(), PropertyName: null, SnapshotJson));
            return SnapshotJson;
        }

        public int ReloadQml(IntPtr engineHandle, string qmlSourcePath)
        {
            AddCall(new SyncCall("reload", engineHandle.ToString(), "qmlSourcePath", qmlSourcePath));
            if (ReloadResultCode == 0)
            {
                OnReload?.Invoke();
            }

            return ReloadResultCode;
        }

        public void RestoreSnapshot(IntPtr engineHandle, string snapshotJson)
        {
            AddCall(new SyncCall("restore", engineHandle.ToString(), PropertyName: null, snapshotJson));
        }

        public void ShowError(
            IntPtr engineHandle,
            string title,
            string message,
            string? filePath,
            int line,
            int column)
        {
            lock (syncRoot)
            {
                overlayCalls.Add(new OverlayCall(engineHandle, title, message, filePath, line, column, IsShow: true));
            }
        }

        public void HideError(IntPtr engineHandle)
        {
            if (HideErrorException is not null)
            {
                throw HideErrorException;
            }

            lock (syncRoot)
            {
                overlayCalls.Add(new OverlayCall(engineHandle, Title: null, Message: null, FilePath: null, Line: 0, Column: 0, IsShow: false));
            }
        }

        public string? GetNativeInstanceInfo(string instanceId)
        {
            AddCall(new SyncCall("native_instance_info", instanceId, PropertyName: null, Value: null));
            return "{\"instanceId\":\"" + instanceId + "\"}";
        }

        public string? GetNativeAllInstances()
        {
            AddCall(new SyncCall("native_all_instances", "all", PropertyName: null, Value: null));
            return "[]";
        }

        public string? GetNativeMetrics()
        {
            AddCall(new SyncCall("native_metrics", "metrics", PropertyName: null, Value: null));
            return "{}";
        }

        private void AddCall(SyncCall call)
        {
            lock (syncRoot)
            {
                calls.Add(call);
            }
        }
    }

    internal sealed record OverlayCall(
        IntPtr EngineHandle,
        string? Title,
        string? Message,
        string? FilePath,
        int Line,
        int Column,
        bool IsShow);
}
