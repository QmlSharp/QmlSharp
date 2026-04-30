using System.Runtime.InteropServices;

namespace QmlSharp.Core
{
    /// <summary>QML four-dimensional vector.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct Vector4(double X, double Y, double Z, double W);
}
