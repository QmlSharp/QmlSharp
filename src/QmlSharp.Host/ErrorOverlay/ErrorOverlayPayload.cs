namespace QmlSharp.Host.ErrorOverlay
{
    /// <summary>Structured diagnostic payload sent to the native error overlay hook.</summary>
    public sealed record ErrorOverlayPayload(
        string Message,
        ErrorOverlaySourceLocation? SourceLocation = null,
        string? DiagnosticCode = null,
        ErrorOverlaySeverity Severity = ErrorOverlaySeverity.Error);
}
