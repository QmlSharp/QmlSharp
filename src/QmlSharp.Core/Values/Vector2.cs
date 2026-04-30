using System.Runtime.InteropServices;

namespace QmlSharp.Core
{
    /// <summary>QML two-dimensional vector.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct Vector2(double X, double Y);
}
