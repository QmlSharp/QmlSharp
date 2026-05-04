namespace QmlSharp.Build
{
    internal sealed class ResourceBundlingBuildStage : IBuildStage
    {
        private readonly IResourceBundler? _resourceBundler;

        public ResourceBundlingBuildStage()
        {
        }

        public ResourceBundlingBuildStage(IResourceBundler resourceBundler)
        {
            ArgumentNullException.ThrowIfNull(resourceBundler);

            _resourceBundler = resourceBundler;
        }

        public BuildPhase Phase => BuildPhase.AssetBundling;

        public Task<BuildStageResult> ExecuteAsync(BuildContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            IResourceBundler bundler = _resourceBundler ?? new ResourceBundler(context.ProjectDir);
            ResourceCollectionResult collection = Collect(bundler, context.Config);
            ResourceBundleResult bundle = context.DryRun
                ? new ResourceBundleResult(0, 0, ImmutableArray<string>.Empty, null)
                : bundler.Bundle(collection.Resources, context.OutputDir);
            ImmutableArray<BuildDiagnostic> diagnostics = collection.Diagnostics
                .AddRange(bundle.Diagnostics)
                .ToImmutableArray();
            BuildArtifacts artifacts = new()
            {
                AssetFiles = bundle.OutputPaths
                    .Where(path => !string.Equals(path, bundle.QrcPath, StringComparison.Ordinal))
                    .ToImmutableArray(),
                QrcFile = bundle.QrcPath,
            };

            return Task.FromResult(new BuildStageResult
            {
                Success = !diagnostics.Any(IsBlockingDiagnostic),
                Diagnostics = diagnostics,
                Stats = new BuildStatsDelta
                {
                    AssetsCollected = collection.Resources.Length,
                },
                Artifacts = artifacts,
            });
        }

        private static ResourceCollectionResult Collect(IResourceBundler bundler, QmlSharpConfig config)
        {
            if (bundler is ResourceBundler resourceBundler)
            {
                return resourceBundler.CollectWithDiagnostics(config);
            }

            return new ResourceCollectionResult(
                bundler.Collect(config),
                ImmutableArray<BuildDiagnostic>.Empty);
        }

        private static bool IsBlockingDiagnostic(BuildDiagnostic diagnostic)
        {
            return diagnostic.Severity is BuildDiagnosticSeverity.Error or BuildDiagnosticSeverity.Fatal;
        }
    }
}
