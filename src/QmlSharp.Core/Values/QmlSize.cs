using System.Runtime.InteropServices;

namespace QmlSharp.Core
{
    /// <summary>QML two-dimensional size.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct QmlSize(double Width, double Height);
}
