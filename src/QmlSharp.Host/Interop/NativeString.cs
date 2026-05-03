using System.Runtime.InteropServices;

namespace QmlSharp.Host.Interop
{
    internal static class NativeString
    {
        internal static string? ReadAndFree(IntPtr pointer, INativeHostInterop interop)
        {
            ArgumentNullException.ThrowIfNull(interop);

            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUTF8(pointer);
            }
            finally
            {
                interop.FreeString(pointer);
            }
        }
    }
}
