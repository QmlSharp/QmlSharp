namespace QmlSharp.Build
{
    internal sealed class PackageResolutionBuildStage : IBuildStage
    {
        private readonly IPackageResolver _packageResolver;

        public PackageResolutionBuildStage()
            : this(new PackageResolver())
        {
        }

        public PackageResolutionBuildStage(IPackageResolver packageResolver)
        {
            ArgumentNullException.ThrowIfNull(packageResolver);

            _packageResolver = packageResolver;
        }

        public BuildPhase Phase => BuildPhase.DependencyResolution;

        public Task<BuildStageResult> ExecuteAsync(BuildContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                PackageResolutionResult result = Resolve(context.ProjectDir);
                ImmutableArray<string> importPaths = _packageResolver.CollectImportPaths(result.Packages);
                ImmutableArray<string> schemaFiles = _packageResolver.CollectSchemas(result.Packages);
                BuildArtifacts artifacts = new()
                {
                    QmlImportPaths = importPaths,
                    ThirdPartySchemaFiles = schemaFiles,
                    PackagePaths = result.Packages
                        .Select(static package => package.PackagePath)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(static path => path, StringComparer.Ordinal)
                        .ToImmutableArray(),
                };

                return Task.FromResult(new BuildStageResult
                {
                    Success = !result.Diagnostics.Any(IsBlockingDiagnostic),
                    Diagnostics = result.Diagnostics,
                    Artifacts = artifacts,
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException exception)
            {
                return Failed(context.ProjectDir, exception);
            }
            catch (IOException exception)
            {
                return Failed(context.ProjectDir, exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                return Failed(context.ProjectDir, exception);
            }
            catch (ArgumentException exception)
            {
                return Failed(context.ProjectDir, exception);
            }
        }

        private PackageResolutionResult Resolve(string projectDir)
        {
            if (_packageResolver is PackageResolver resolver)
            {
                return resolver.ResolveWithDiagnostics(projectDir);
            }

            return new PackageResolutionResult(
                _packageResolver.Resolve(projectDir),
                ImmutableArray<BuildDiagnostic>.Empty);
        }

        private static bool IsBlockingDiagnostic(BuildDiagnostic diagnostic)
        {
            return diagnostic.Severity is BuildDiagnosticSeverity.Error or BuildDiagnosticSeverity.Fatal;
        }

        private static Task<BuildStageResult> Failed(string projectDir, Exception exception)
        {
            BuildDiagnostic diagnostic = new(
                BuildDiagnosticCode.PackageResolutionFailed,
                BuildDiagnosticSeverity.Error,
                $"Package resolution failed: {exception.Message}",
                BuildPhase.DependencyResolution,
                projectDir);
            return Task.FromResult(BuildStageResult.Failed(diagnostic));
        }
    }
}
