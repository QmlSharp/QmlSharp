using QmlSharp.Host.Interop;

namespace QmlSharp.Host.Tests.StateSynchronization
{
    internal sealed class FakeNativeHostInterop : INativeHostInterop
    {
        private readonly Lock syncRoot = new();
        private readonly List<SyncCall> calls = [];

        public bool IsOnMainThread { get; set; } = true;

        public int PostToMainThreadCallCount { get; private set; }

        public int NextResultCode { get; set; }

        public string? LastError { get; set; }

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

        private void AddCall(SyncCall call)
        {
            lock (syncRoot)
            {
                calls.Add(call);
            }
        }
    }
}
