namespace QmlSharp.Host.ArtifactValidation
{
    /// <summary>Validates compiler and runtime artifacts before native engine startup.</summary>
    public interface IArtifactValidator
    {
        /// <summary>Validates artifacts under a canonical <c>dist/</c> directory.</summary>
        ArtifactValidationResult Validate(string distDirectory);

        /// <summary>Validates artifacts using the supplied startup request.</summary>
        ArtifactValidationResult Validate(ArtifactValidationRequest request);

        /// <summary>Converts a native schema-registration result into startup validation diagnostics.</summary>
        ArtifactValidationResult ValidateSchemaRegistrationResult(string schemaFilePath, int nativeResultCode, string? nativeError);
    }
}
