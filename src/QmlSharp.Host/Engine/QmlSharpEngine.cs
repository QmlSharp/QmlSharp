using QmlSharp.Host.ArtifactValidation;
using QmlSharp.Host.Interop;

namespace QmlSharp.Host.Engine
{
    /// <summary>Public entry point for the managed native-host facade.</summary>
    public sealed class QmlSharpEngine
    {
        private readonly IArtifactValidator artifactValidator;

        public QmlSharpEngine()
            : this(new ArtifactValidator())
        {
        }

        internal QmlSharpEngine(IArtifactValidator artifactValidator)
        {
            ArgumentNullException.ThrowIfNull(artifactValidator);
            this.artifactValidator = artifactValidator;
        }

        /// <summary>The native ABI version this managed host expects.</summary>
        public int ExpectedAbiVersion => NativeHostAbi.SupportedAbiVersion;

        /// <summary>Validates startup artifacts before native engine initialization.</summary>
        public ArtifactValidationResult ValidateStartupArtifacts(string distDirectory, string? rootQmlFilePath = null)
        {
            return artifactValidator.Validate(new ArtifactValidationRequest(distDirectory, rootQmlFilePath));
        }

        /// <summary>Validates startup artifacts and throws if fatal diagnostics would prevent startup.</summary>
        public ArtifactValidationResult EnsureStartupArtifactsValid(string distDirectory, string? rootQmlFilePath = null)
        {
            ArtifactValidationResult result = ValidateStartupArtifacts(distDirectory, rootQmlFilePath);
            if (!result.IsValid)
            {
                throw new ArtifactValidationException(result);
            }

            return result;
        }
    }
}
