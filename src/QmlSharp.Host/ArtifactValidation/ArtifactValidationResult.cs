namespace QmlSharp.Host.ArtifactValidation
{
    /// <summary>Result of validating the host startup artifact set.</summary>
    public sealed record ArtifactValidationResult(bool IsValid, IReadOnlyList<ArtifactDiagnostic> Diagnostics);
}
