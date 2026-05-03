namespace QmlSharp.Host.ErrorOverlay
{
    /// <summary>Optional source location displayed by the native error overlay.</summary>
    public sealed record ErrorOverlaySourceLocation(string? FilePath, int? Line, int? Column);
}
