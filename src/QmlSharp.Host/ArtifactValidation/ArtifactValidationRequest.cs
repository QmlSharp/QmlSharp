namespace QmlSharp.Host.ArtifactValidation
{
    /// <summary>Inputs for startup artifact validation.</summary>
    public sealed record ArtifactValidationRequest(string DistDirectory, string? RootQmlFilePath = null);
}
