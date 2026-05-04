using System.Diagnostics;

namespace QmlSharp.Build
{
    /// <summary>Deterministic fake-stage implementation of the build pipeline orchestrator.</summary>
    public sealed class BuildPipeline : IBuildPipeline
    {
        private static readonly ImmutableArray<BuildPhase> CanonicalPhases =
            ImmutableArray.Create(
                BuildPhase.ConfigLoading,
                BuildPhase.CSharpCompilation,
                BuildPhase.ModuleMetadata,
                BuildPhase.DependencyResolution,
                BuildPhase.AssetBundling,
                BuildPhase.QmlValidation,
                BuildPhase.CppCodeGenAndBuild,
                BuildPhase.OutputAssembly);

        private readonly ImmutableDictionary<BuildPhase, IBuildStage> _stages;
        private readonly Lock _progressLock = new();
        private ImmutableArray<Action<BuildProgress>> _progressCallbacks =
            ImmutableArray<Action<BuildProgress>>.Empty;

        /// <summary>Create a pipeline with deterministic fake stages.</summary>
        public BuildPipeline()
            : this(FakeBuildStage.CreateDefaultStages())
        {
        }

        internal BuildPipeline(ImmutableArray<IBuildStage> stages)
        {
            if (stages.IsDefaultOrEmpty)
            {
                throw new ArgumentException("At least one build stage is required.", nameof(stages));
            }

            ImmutableDictionary<BuildPhase, IBuildStage>.Builder builder =
                ImmutableDictionary.CreateBuilder<BuildPhase, IBuildStage>();
            foreach (IBuildStage stage in stages)
            {
                if (!CanonicalPhases.Contains(stage.Phase))
                {
                    throw new ArgumentException($"Unknown build phase '{stage.Phase}'.", nameof(stages));
                }

                builder[stage.Phase] = stage;
            }

            _stages = builder.ToImmutable();
        }

        /// <inheritdoc />
        public Task<BuildResult> BuildAsync(BuildContext context, CancellationToken cancellationToken = default)
        {
            return RunAsync(context, CanonicalPhases, cancellationToken);
        }

        /// <inheritdoc />
        public Task<BuildResult> BuildPhasesAsync(
            BuildContext context,
            ImmutableArray<BuildPhase> phases,
            CancellationToken cancellationToken = default)
        {
            ImmutableArray<BuildPhase> orderedPhases = OrderRequestedPhases(phases);
            return RunAsync(context, orderedPhases, cancellationToken);
        }

        /// <inheritdoc />
        public void OnProgress(Action<BuildProgress> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);

            lock (_progressLock)
            {
                _progressCallbacks = _progressCallbacks.Add(callback);
            }
        }

        private async Task<BuildResult> RunAsync(
            BuildContext context,
            ImmutableArray<BuildPhase> phases,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);

            Stopwatch totalStopwatch = Stopwatch.StartNew();
            ImmutableArray<PhaseResult>.Builder phaseResults =
                ImmutableArray.CreateBuilder<PhaseResult>(phases.Length);
            ImmutableArray<BuildDiagnostic>.Builder diagnostics =
                ImmutableArray.CreateBuilder<BuildDiagnostic>();
            Dictionary<BuildPhase, PhaseResult> resultsByPhase = new();
            BuildStatsBuilder statsBuilder = new();
            ImmutableHashSet<BuildPhase> requestedPhases = phases.ToImmutableHashSet();

            for (int index = 0; index < phases.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                BuildPhase phase = phases[index];
                EmitProgress(new BuildProgress(
                    phase,
                    $"Build Stage {(int)phase}: {phase}",
                    index + 1,
                    phases.Length));

                PhaseResult phaseResult = await ExecuteOrSkipPhaseAsync(
                    phase,
                    requestedPhases,
                    resultsByPhase,
                    context,
                    statsBuilder,
                    cancellationToken).ConfigureAwait(false);

                phaseResults.Add(phaseResult);
                resultsByPhase[phase] = phaseResult;
                diagnostics.AddRange(phaseResult.Diagnostics);
            }

            totalStopwatch.Stop();
            BuildStats stats = statsBuilder.ToBuildStats(totalStopwatch.Elapsed);
            ImmutableArray<PhaseResult> finalPhaseResults = phaseResults.ToImmutable();
            ImmutableArray<BuildDiagnostic> finalDiagnostics = diagnostics.ToImmutable();
            bool success = finalPhaseResults.All(static result => result.Success) &&
                !finalDiagnostics.Any(IsBlockingDiagnostic);

            return new BuildResult
            {
                Success = success,
                PhaseResults = finalPhaseResults,
                Diagnostics = finalDiagnostics,
                Stats = stats,
                Artifacts = statsBuilder.ToBuildArtifacts(),
            };
        }

        private async Task<PhaseResult> ExecuteOrSkipPhaseAsync(
            BuildPhase phase,
            ImmutableHashSet<BuildPhase> requestedPhases,
            IReadOnlyDictionary<BuildPhase, PhaseResult> resultsByPhase,
            BuildContext context,
            BuildStatsBuilder statsBuilder,
            CancellationToken cancellationToken)
        {
            BuildDiagnostic? skipDiagnostic = CreateSkipDiagnosticIfDependencyFailed(
                phase,
                requestedPhases,
                resultsByPhase);
            if (skipDiagnostic is not null)
            {
                return new PhaseResult(
                    phase,
                    false,
                    TimeSpan.Zero,
                    ImmutableArray.Create(skipDiagnostic));
            }

            return await ExecutePhaseAsync(phase, context, statsBuilder, cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task<PhaseResult> ExecutePhaseAsync(
            BuildPhase phase,
            BuildContext context,
            BuildStatsBuilder statsBuilder,
            CancellationToken cancellationToken)
        {
            if (!_stages.TryGetValue(phase, out IBuildStage? stage))
            {
                BuildDiagnostic diagnostic = new(
                    BuildDiagnosticCode.InternalError,
                    BuildDiagnosticSeverity.Error,
                    $"Build Stage {(int)phase} ({phase}) has no registered fake stage implementation.",
                    phase,
                    null);
                return new PhaseResult(phase, false, TimeSpan.Zero, ImmutableArray.Create(diagnostic));
            }

            Stopwatch phaseStopwatch = Stopwatch.StartNew();
            try
            {
                BuildStageResult stageResult = await stage.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                phaseStopwatch.Stop();

                ImmutableArray<BuildDiagnostic> diagnostics = NormalizeDiagnostics(stageResult.Diagnostics);
                bool success = stageResult.Success && !diagnostics.Any(IsBlockingDiagnostic);
                if (success)
                {
                    statsBuilder.Add(stageResult.Stats);
                    statsBuilder.AddArtifacts(stageResult.Artifacts);
                }

                return new PhaseResult(phase, success, phaseStopwatch.Elapsed, diagnostics);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (BuildStageException ex)
            {
                phaseStopwatch.Stop();
                BuildDiagnostic diagnostic = new(
                    BuildDiagnosticCode.InternalError,
                    BuildDiagnosticSeverity.Error,
                    $"Build Stage {(int)phase} ({phase}) failed unexpectedly: {ex.Message}",
                    phase,
                    null);
                return new PhaseResult(phase, false, phaseStopwatch.Elapsed, ImmutableArray.Create(diagnostic));
            }
        }

        private static ImmutableArray<BuildPhase> OrderRequestedPhases(ImmutableArray<BuildPhase> phases)
        {
            if (phases.IsDefaultOrEmpty)
            {
                return ImmutableArray<BuildPhase>.Empty;
            }

            ImmutableHashSet<BuildPhase> requested = phases.ToImmutableHashSet();
            foreach (BuildPhase phase in requested.Where(phase => !CanonicalPhases.Contains(phase)))
            {
                throw new ArgumentException($"Unknown build phase '{phase}'.", nameof(phases));
            }

            ImmutableArray<BuildPhase>.Builder ordered = ImmutableArray.CreateBuilder<BuildPhase>(requested.Count);
            foreach (BuildPhase phase in CanonicalPhases.Where(requested.Contains))
            {
                ordered.Add(phase);
            }

            return ordered.ToImmutable();
        }

        private static BuildDiagnostic? CreateSkipDiagnosticIfDependencyFailed(
            BuildPhase phase,
            ImmutableHashSet<BuildPhase> requestedPhases,
            IReadOnlyDictionary<BuildPhase, PhaseResult> resultsByPhase)
        {
            foreach (BuildPhase dependency in GetDependencies(phase))
            {
                if (!requestedPhases.Contains(dependency))
                {
                    continue;
                }

                if (resultsByPhase.TryGetValue(dependency, out PhaseResult? dependencyResult) &&
                    !dependencyResult.Success)
                {
                    return new BuildDiagnostic(
                        BuildDiagnosticCode.InternalError,
                        BuildDiagnosticSeverity.Error,
                        $"Build Stage {(int)phase} ({phase}) was skipped because Build Stage {(int)dependency} ({dependency}) failed.",
                        phase,
                        null);
                }
            }

            return null;
        }

        private static ImmutableArray<BuildPhase> GetDependencies(BuildPhase phase)
        {
            return phase switch
            {
                BuildPhase.ConfigLoading => ImmutableArray<BuildPhase>.Empty,
                BuildPhase.CSharpCompilation => ImmutableArray.Create(BuildPhase.ConfigLoading),
                BuildPhase.ModuleMetadata => ImmutableArray.Create(
                    BuildPhase.ConfigLoading,
                    BuildPhase.CSharpCompilation),
                BuildPhase.DependencyResolution => ImmutableArray.Create(BuildPhase.ConfigLoading),
                BuildPhase.AssetBundling => ImmutableArray.Create(BuildPhase.ConfigLoading),
                BuildPhase.QmlValidation => ImmutableArray.Create(
                    BuildPhase.ConfigLoading,
                    BuildPhase.CSharpCompilation),
                BuildPhase.CppCodeGenAndBuild => ImmutableArray.Create(
                    BuildPhase.ConfigLoading,
                    BuildPhase.CSharpCompilation),
                BuildPhase.OutputAssembly => ImmutableArray.Create(
                    BuildPhase.ConfigLoading,
                    BuildPhase.CSharpCompilation,
                    BuildPhase.CppCodeGenAndBuild),
                _ => ImmutableArray<BuildPhase>.Empty,
            };
        }

        private static ImmutableArray<BuildDiagnostic> NormalizeDiagnostics(
            ImmutableArray<BuildDiagnostic> diagnostics)
        {
            return diagnostics.IsDefault ? ImmutableArray<BuildDiagnostic>.Empty : diagnostics;
        }

        private static bool IsBlockingDiagnostic(BuildDiagnostic diagnostic)
        {
            return diagnostic.Severity is BuildDiagnosticSeverity.Error or BuildDiagnosticSeverity.Fatal;
        }

        private void EmitProgress(BuildProgress progress)
        {
            ImmutableArray<Action<BuildProgress>> callbacks;
            lock (_progressLock)
            {
                callbacks = _progressCallbacks;
            }

            foreach (Action<BuildProgress> callback in callbacks)
            {
                callback(progress);
            }
        }

        private sealed class BuildStatsBuilder
        {
            private int _filesCompiled;
            private int _schemasGenerated;
            private int _cppFilesGenerated;
            private int _assetsCollected;
            private bool _nativeLibBuilt;
            private ImmutableArray<string> _qmlFiles = ImmutableArray<string>.Empty;
            private ImmutableArray<string> _schemaFiles = ImmutableArray<string>.Empty;
            private ImmutableArray<string> _sourceMapFiles = ImmutableArray<string>.Empty;
            private ImmutableArray<string> _moduleMetadataFiles = ImmutableArray<string>.Empty;
            private ImmutableArray<string> _qmlImportPaths = ImmutableArray<string>.Empty;
            private ImmutableArray<string> _thirdPartySchemaFiles = ImmutableArray<string>.Empty;
            private ImmutableArray<string> _packagePaths = ImmutableArray<string>.Empty;
            private ImmutableArray<string> _assetFiles = ImmutableArray<string>.Empty;
            private string? _eventBindingsFile;
            private string? _qrcFile;
            private string? _nativeLibraryPath;
            private string? _assemblyPath;

            public void Add(BuildStatsDelta delta)
            {
                ArgumentNullException.ThrowIfNull(delta);

                _filesCompiled += delta.FilesCompiled;
                _schemasGenerated += delta.SchemasGenerated;
                _cppFilesGenerated += delta.CppFilesGenerated;
                _assetsCollected += delta.AssetsCollected;
                _nativeLibBuilt |= delta.NativeLibBuilt;
            }

            public void AddArtifacts(BuildArtifacts artifacts)
            {
                ArgumentNullException.ThrowIfNull(artifacts);

                _qmlFiles = AddDistinct(_qmlFiles, artifacts.QmlFiles);
                _schemaFiles = AddDistinct(_schemaFiles, artifacts.SchemaFiles);
                _sourceMapFiles = AddDistinct(_sourceMapFiles, artifacts.SourceMapFiles);
                _moduleMetadataFiles = AddDistinct(_moduleMetadataFiles, artifacts.ModuleMetadataFiles);
                _qmlImportPaths = AddDistinct(_qmlImportPaths, artifacts.QmlImportPaths);
                _thirdPartySchemaFiles = AddDistinct(_thirdPartySchemaFiles, artifacts.ThirdPartySchemaFiles);
                _packagePaths = AddDistinct(_packagePaths, artifacts.PackagePaths);
                _assetFiles = AddDistinct(_assetFiles, artifacts.AssetFiles);
                _eventBindingsFile ??= artifacts.EventBindingsFile;
                _qrcFile ??= artifacts.QrcFile;
                _nativeLibraryPath ??= artifacts.NativeLibraryPath;
                _assemblyPath ??= artifacts.AssemblyPath;
            }

            public BuildStats ToBuildStats(TimeSpan totalDuration)
            {
                return new BuildStats(
                    totalDuration,
                    _filesCompiled,
                    _schemasGenerated,
                    _cppFilesGenerated,
                    _assetsCollected,
                    _nativeLibBuilt);
            }

            public BuildArtifacts ToBuildArtifacts()
            {
                return new BuildArtifacts
                {
                    QmlFiles = _qmlFiles,
                    SchemaFiles = _schemaFiles,
                    EventBindingsFile = _eventBindingsFile,
                    SourceMapFiles = _sourceMapFiles,
                    ModuleMetadataFiles = _moduleMetadataFiles,
                    QmlImportPaths = _qmlImportPaths,
                    ThirdPartySchemaFiles = _thirdPartySchemaFiles,
                    PackagePaths = _packagePaths,
                    AssetFiles = _assetFiles,
                    QrcFile = _qrcFile,
                    NativeLibraryPath = _nativeLibraryPath,
                    AssemblyPath = _assemblyPath,
                };
            }

            private static ImmutableArray<string> AddDistinct(
                ImmutableArray<string> existing,
                ImmutableArray<string> additions)
            {
                if (additions.IsDefaultOrEmpty)
                {
                    return existing;
                }

                return existing
                    .AddRange(additions)
                    .Where(static item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static item => item, StringComparer.Ordinal)
                    .ToImmutableArray();
            }
        }
    }

}
