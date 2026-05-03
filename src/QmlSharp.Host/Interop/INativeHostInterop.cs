namespace QmlSharp.Host.Interop
{
    internal interface INativeHostInterop
    {
        bool IsOnMainThread { get; }

        int GetAbiVersion();

        IntPtr EngineInit(IReadOnlyList<string> arguments);

        void EngineShutdown(IntPtr engineHandle);

        int EngineExec(IntPtr engineHandle);

        string? GetLastError();

        void FreeString(IntPtr pointer);

        void PostToMainThread(Action callback);

        int RegisterType(
            IntPtr engineHandle,
            string moduleUri,
            int versionMajor,
            int versionMinor,
            string typeName,
            string schemaId,
            string compilerSlotKey,
            QmlSharpTypeRegistrationCallback registerCallback);

        int RegisterModule(
            IntPtr engineHandle,
            string moduleUri,
            int versionMajor,
            int versionMinor,
            IReadOnlyList<QmlSharpTypeRegistrationEntry> entries);

        void SetInstanceCallbacks(
            Action<string, string, string>? onCreated,
            Action<string>? onDestroyed);

        void InstanceReady(string instanceId);

        void SetCommandCallback(Action<string, string, string>? callback);

        int SyncStateString(string instanceId, string propertyName, string? value);

        int SyncStateInt(string instanceId, string propertyName, int value);

        int SyncStateDouble(string instanceId, string propertyName, double value);

        int SyncStateBool(string instanceId, string propertyName, bool value);

        int SyncStateJson(string instanceId, string propertyName, string jsonValue);

        int SyncStateBatch(string instanceId, string propertiesJson);

        int DispatchEffect(string instanceId, string effectName, string payloadJson);

        int BroadcastEffect(string className, string effectName, string payloadJson);

        string? CaptureSnapshot(IntPtr engineHandle);

        int ReloadQml(IntPtr engineHandle, string qmlSourcePath);

        void RestoreSnapshot(IntPtr engineHandle, string snapshotJson);

        void ShowError(
            IntPtr engineHandle,
            string title,
            string message,
            string? filePath,
            int line,
            int column);

        void HideError(IntPtr engineHandle);

        string? GetNativeInstanceInfo(string instanceId);

        string? GetNativeAllInstances();

        string? GetNativeMetrics();
    }
}
