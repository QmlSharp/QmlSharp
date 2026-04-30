using System.Runtime.InteropServices;

namespace QmlSharp.Core
{
    /// <summary>QML quaternion value.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct Quaternion(double Scalar, double X, double Y, double Z);
}
