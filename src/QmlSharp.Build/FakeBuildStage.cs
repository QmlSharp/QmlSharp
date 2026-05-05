namespace QmlSharp.Build
{
    internal sealed class FakeBuildStage : IBuildStage
    {
        private FakeBuildStage(BuildPhase phase)
        {
            Phase = phase;
        }

        public BuildPhase Phase { get; }

        public static ImmutableArray<IBuildStage> CreateDefaultStages()
        {
            ImmutableArray<IBuildStage>.Builder builder = ImmutableArray.CreateBuilder<IBuildStage>(8);
            builder.Add(new FakeBuildStage(BuildPhase.ConfigLoading));
            builder.Add(new FakeBuildStage(BuildPhase.CSharpCompilation));
            builder.Add(new ModuleMetadataBuildStage());
            builder.Add(new PackageResolutionBuildStage());
            builder.Add(new ResourceBundlingBuildStage());
            builder.Add(new FakeBuildStage(BuildPhase.QmlValidation));
            builder.Add(new NativeBuildStage());
            builder.Add(new FakeBuildStage(BuildPhase.OutputAssembly));
            return builder.ToImmutable();
        }

        public Task<BuildStageResult> ExecuteAsync(
            BuildContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            BuildStageResult result = Phase switch
            {
                BuildPhase.CSharpCompilation => BuildStageResult.Succeeded(new BuildStatsDelta
                {
                    FilesCompiled = 1,
                    SchemasGenerated = 1,
                }),
                BuildPhase.AssetBundling => BuildStageResult.Succeeded(new BuildStatsDelta
                {
                    AssetsCollected = 1,
                }),
                _ => BuildStageResult.Succeeded(),
            };
            return Task.FromResult(result);
        }
    }
}
