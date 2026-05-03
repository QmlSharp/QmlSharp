namespace QmlSharp.Host.Interop
{
    internal interface INativeHostInterop
    {
        bool IsOnMainThread { get; }

        int GetAbiVersion();

        string? GetLastError();

        void FreeString(IntPtr pointer);

        void PostToMainThread(Action callback);

        int SyncStateString(string instanceId, string propertyName, string? value);

        int SyncStateInt(string instanceId, string propertyName, int value);

        int SyncStateDouble(string instanceId, string propertyName, double value);

        int SyncStateBool(string instanceId, string propertyName, bool value);

        int SyncStateJson(string instanceId, string propertyName, string jsonValue);

        int SyncStateBatch(string instanceId, string propertiesJson);
    }
}
