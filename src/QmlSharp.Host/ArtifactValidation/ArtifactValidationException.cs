namespace QmlSharp.Host.ArtifactValidation
{
    /// <summary>Thrown when fatal artifact validation diagnostics prevent startup.</summary>
    public sealed class ArtifactValidationException : Exception
    {
        public ArtifactValidationException(ArtifactValidationResult result)
            : base("QmlSharp startup artifact validation failed.")
        {
            Result = result;
        }

        /// <summary>The validation result that blocked startup.</summary>
        public ArtifactValidationResult Result { get; }
    }
}
