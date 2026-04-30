using System.Runtime.InteropServices;

namespace QmlSharp.Core
{
    /// <summary>QML 4x4 matrix value.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct Matrix4x4(
        double M11,
        double M12,
        double M13,
        double M14,
        double M21,
        double M22,
        double M23,
        double M24,
        double M31,
        double M32,
        double M33,
        double M34,
        double M41,
        double M42,
        double M43,
        double M44);
}
