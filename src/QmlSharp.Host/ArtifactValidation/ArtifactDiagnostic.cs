namespace QmlSharp.Host.ArtifactValidation
{
    /// <summary>A single startup artifact validation diagnostic.</summary>
    public sealed record ArtifactDiagnostic(
        ArtifactDiagnosticSeverity Severity,
        string Code,
        string Message,
        string? FilePath = null);
}
