namespace QmlSharp.Host.Diagnostics
{
    /// <summary>Source-map-ready diagnostic payload captured from native Qt/QML runtime behavior.</summary>
    public sealed record RuntimeDiagnostic(
        RuntimeDiagnosticSeverity Severity,
        string Message,
        string? FileUrl = null,
        string? FilePath = null,
        int Line = 0,
        int Column = 0,
        string? EnginePhase = null,
        string? InstanceId = null,
        string? ComponentName = null);
}
