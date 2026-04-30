using System.Runtime.InteropServices;

namespace QmlSharp.Core
{
    /// <summary>QML rectangle value.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct QmlRect(double X, double Y, double Width, double Height);
}
