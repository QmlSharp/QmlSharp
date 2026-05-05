namespace QmlSharp.Build
{
    internal sealed record BuildStageResult
    {
        public bool Success { get; init; } = true;

        public ImmutableArray<BuildDiagnostic> Diagnostics { get; init; } =
            ImmutableArray<BuildDiagnostic>.Empty;

        public BuildStatsDelta Stats { get; init; } = BuildStatsDelta.Empty;

        public BuildArtifacts Artifacts { get; init; } = new();

        public ProductManifest? Manifest { get; init; }

        public static BuildStageResult Succeeded(
            BuildStatsDelta? stats = null,
            BuildArtifacts? artifacts = null,
            ProductManifest? manifest = null)
        {
            return new BuildStageResult
            {
                Success = true,
                Stats = stats ?? BuildStatsDelta.Empty,
                Artifacts = artifacts ?? new BuildArtifacts(),
                Manifest = manifest,
            };
        }

        public static BuildStageResult Failed(BuildDiagnostic diagnostic)
        {
            ArgumentNullException.ThrowIfNull(diagnostic);

            return new BuildStageResult
            {
                Success = false,
                Diagnostics = ImmutableArray.Create(diagnostic),
            };
        }
    }
}
