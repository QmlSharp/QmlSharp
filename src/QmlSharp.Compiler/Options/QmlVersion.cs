namespace QmlSharp.Compiler
{
    /// <summary>
    /// QML module version pair.
    /// </summary>
    /// <param name="Major">The major version.</param>
    /// <param name="Minor">The minor version.</param>
    public sealed record QmlVersion(int Major, int Minor);
}
