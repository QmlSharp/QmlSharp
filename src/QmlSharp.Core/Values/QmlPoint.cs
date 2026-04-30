using System.Runtime.InteropServices;

namespace QmlSharp.Core
{
    /// <summary>QML two-dimensional point.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct QmlPoint(double X, double Y);
}
