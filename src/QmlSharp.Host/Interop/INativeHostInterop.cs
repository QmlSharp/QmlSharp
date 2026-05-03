namespace QmlSharp.Host.Interop
{
    internal interface INativeHostInterop
    {
        int GetAbiVersion();

        string? GetLastError();

        void FreeString(IntPtr pointer);
    }
}
