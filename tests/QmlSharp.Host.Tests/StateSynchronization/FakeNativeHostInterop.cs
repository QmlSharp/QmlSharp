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
