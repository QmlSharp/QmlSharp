using System.Runtime.InteropServices;

namespace QmlSharp.Core
{
    /// <summary>QML three-dimensional vector.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct Vector3(double X, double Y, double Z);
}
