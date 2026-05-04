using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class BuildPipelineTests
    {
        [Fact]
        public async Task BP01_FullPipeline_ProducesAllEightPhaseResults()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(BP01_FullPipeline_ProducesAllEightPhaseResults));
            string iconPath = Path.Join(project.Path, "assets", "icon.png");
            await File.WriteAllTextAsync(iconPath, "icon");
            BuildPipeline pipeline = new();
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);

            BuildResult result = await pipeline.BuildAsync(context);

            Assert.True(result.Success);
            Assert.Equal(GetCanonicalPhases(), result.PhaseResults.Select(static phase => phase.Phase));
            Assert.All(result.PhaseResults, static phase => Assert.True(phase.Success));
            Assert.True(result.Stats.FilesCompiled > 0);
            Assert.True(result.Stats.SchemasGenerated > 0);
            Assert.True(result.Stats.CppFilesGenerated > 0);
            Assert.True(result.Stats.AssetsCollected > 0);
            Assert.Contains(Path.Join(context.OutputDir, "assets", "icon.png"), result.Artifacts.AssetFiles);
            Assert.True(result.Stats.NativeLibBuilt);
            Assert.True(result.Stats.TotalDuration >= TimeSpan.Zero);
        }

        [Fact]
        public async Task BP02_ConfigLoadingFailure_SkipsDependentStages()
        {
            RecordingBuildStage configStage = CreateFailedStage(
                BuildPhase.ConfigLoading,
                BuildDiagnosticCode.ConfigValidationError);
            ImmutableArray<RecordingBuildStage> stages = CreateRecordingStages(configStage);
            BuildPipeline pipeline = CreatePipeline(stages);

            BuildResult result = await pipeline.BuildAsync(BuildTestFixtures.CreateDefaultContext());

            Assert.False(result.Success);
            Assert.Equal(8, result.PhaseResults.Length);
            Assert.False(result.PhaseResults[0].Success);
            Assert.All(result.PhaseResults.Skip(1), static phase => Assert.False(phase.Success));
            Assert.Equal(1, stages.Single(stage => stage.Phase == BuildPhase.ConfigLoading).CallCount);
            Assert.Equal(0, stages.Single(stage => stage.Phase == BuildPhase.DependencyResolution).CallCount);
            Assert.Equal(0, stages.Single(stage => stage.Phase == BuildPhase.AssetBundling).CallCount);
            Assert.Contains(result.Diagnostics, static diagnostic =>
                diagnostic.Message.Contains("was skipped", StringComparison.Ordinal));
        }

        [Fact]
        public async Task BP03_CSharpCompilationFailure_StillRunsIndependentStagesFourAndFive()
        {
            RecordingBuildStage compilationStage = CreateFailedStage(
                BuildPhase.CSharpCompilation,
                BuildDiagnosticCode.CompilationFailed);
            ImmutableArray<RecordingBuildStage> stages = CreateRecordingStages(compilationStage);
            BuildPipeline pipeline = CreatePipeline(stages);

            BuildResult result = await pipeline.BuildAsync(BuildTestFixtures.CreateDefaultContext());

            Assert.False(result.Success);
            Assert.False(result.PhaseResults.Single(static phase => phase.Phase == BuildPhase.CSharpCompilation).Success);
            Assert.False(result.PhaseResults.Single(static phase => phase.Phase == BuildPhase.ModuleMetadata).Success);
            Assert.True(result.PhaseResults.Single(static phase => phase.Phase == BuildPhase.DependencyResolution).Success);
            Assert.True(result.PhaseResults.Single(static phase => phase.Phase == BuildPhase.AssetBundling).Success);
            Assert.False(result.PhaseResults.Single(static phase => phase.Phase == BuildPhase.QmlValidation).Success);
            Assert.Equal(1, stages.Single(stage => stage.Phase == BuildPhase.DependencyResolution).CallCount);
            Assert.Equal(1, stages.Single(stage => stage.Phase == BuildPhase.AssetBundling).CallCount);
        }

        [Fact]
        public async Task BP04_IncrementalNoSchemaChange_CanSkipStageSevenAtOrchestrationLevel()
        {
            RecordingBuildStage cppStage = new(
                BuildPhase.CppCodeGenAndBuild,
                BuildStageResult.Succeeded());
            ImmutableArray<RecordingBuildStage> stages = CreateRecordingStages(cppStage);
            BuildPipeline pipeline = CreatePipeline(stages);

            BuildResult result = await pipeline.BuildAsync(BuildTestFixtures.CreateDefaultContext());

            Assert.True(result.Success);
            Assert.True(result.PhaseResults.Single(static phase => phase.Phase == BuildPhase.CppCodeGenAndBuild).Success);
            Assert.Equal(1, cppStage.CallCount);
            Assert.Equal(0, result.Stats.CppFilesGenerated);
            Assert.False(result.Stats.NativeLibBuilt);
        }

        [Fact]
        public async Task BP05_SchemaChangeDetected_RunsStageSevenFakeCodegen()
        {
            BuildPipeline pipeline = new();

            BuildResult result = await pipeline.BuildAsync(BuildTestFixtures.CreateDefaultContext());

            Assert.True(result.Success);
            Assert.True(result.PhaseResults.Single(static phase => phase.Phase == BuildPhase.CppCodeGenAndBuild).Success);
            Assert.True(result.Stats.CppFilesGenerated > 0);
            Assert.True(result.Stats.NativeLibBuilt);
        }

        [Fact]
        public async Task BP06_ProgressCallbacks_FireForAllStagesInCanonicalOrder()
        {
            BuildPipeline pipeline = new();
            List<BuildProgress> firstListenerEvents = new();
            List<BuildProgress> secondListenerEvents = new();
            pipeline.OnProgress(firstListenerEvents.Add);
            pipeline.OnProgress(secondListenerEvents.Add);

            BuildResult result = await pipeline.BuildAsync(BuildTestFixtures.CreateDefaultContext());

            Assert.True(result.Success);
            Assert.Equal(GetCanonicalPhases(), firstListenerEvents.Select(static progress => progress.Phase));
            Assert.Equal(GetCanonicalPhases(), secondListenerEvents.Select(static progress => progress.Phase));
            Assert.Equal(Enumerable.Range(1, 8), firstListenerEvents.Select(static progress => progress.CurrentStep));
            Assert.All(firstListenerEvents, static progress => Assert.Equal(8, progress.TotalSteps));
        }

        [Fact]
        public async Task BP07_BuildPhasesAsync_RunsOnlyRequestedStagesInCanonicalOrder()
        {
            ImmutableArray<RecordingBuildStage> stages = CreateRecordingStages();
            BuildPipeline pipeline = CreatePipeline(stages);

            BuildResult result = await pipeline.BuildPhasesAsync(
                BuildTestFixtures.CreateDefaultContext(),
                ImmutableArray.Create(BuildPhase.ModuleMetadata, BuildPhase.CSharpCompilation));

            Assert.True(result.Success);
            Assert.Equal(
                ImmutableArray.Create(BuildPhase.CSharpCompilation, BuildPhase.ModuleMetadata),
                result.PhaseResults.Select(static phase => phase.Phase));
            Assert.Equal(1, stages.Single(stage => stage.Phase == BuildPhase.CSharpCompilation).CallCount);
            Assert.Equal(1, stages.Single(stage => stage.Phase == BuildPhase.ModuleMetadata).CallCount);
            Assert.Equal(0, stages.Single(stage => stage.Phase == BuildPhase.ConfigLoading).CallCount);
            Assert.Equal(0, stages.Single(stage => stage.Phase == BuildPhase.DependencyResolution).CallCount);
        }

        [Fact]
        public async Task BP08_LibraryMode_SkipsStageSevenNativeStats()
        {
            BuildPipeline pipeline = new();
            BuildContext context = BuildTestFixtures.CreateDefaultContext() with
            {
                LibraryMode = true,
            };

            BuildResult result = await pipeline.BuildAsync(context);

            Assert.True(result.Success);
            Assert.True(result.PhaseResults.Single(static phase => phase.Phase == BuildPhase.CppCodeGenAndBuild).Success);
            Assert.Equal(0, result.Stats.CppFilesGenerated);
            Assert.False(result.Stats.NativeLibBuilt);
        }

        [Fact]
        public async Task EH05_PhaseFailure_DoesNotCrashAndKeepsAllPhaseResults()
        {
            RecordingBuildStage cppStage = CreateFailedStage(
                BuildPhase.CppCodeGenAndBuild,
                BuildDiagnosticCode.CppGenerationFailed);
            ImmutableArray<RecordingBuildStage> stages = CreateRecordingStages(cppStage);
            BuildPipeline pipeline = CreatePipeline(stages);

            BuildResult result = await pipeline.BuildAsync(BuildTestFixtures.CreateDefaultContext());

            Assert.False(result.Success);
            Assert.Equal(8, result.PhaseResults.Length);
            Assert.False(result.PhaseResults.Single(static phase => phase.Phase == BuildPhase.CppCodeGenAndBuild).Success);
            Assert.False(result.PhaseResults.Single(static phase => phase.Phase == BuildPhase.OutputAssembly).Success);
            Assert.Contains(result.Diagnostics, static diagnostic =>
                diagnostic.Code == BuildDiagnosticCode.CppGenerationFailed);
        }

        [Fact]
        public async Task EH05_StageException_BecomesFailedPhaseAndSkipsDependents()
        {
            RecordingBuildStage cppStage = CreateThrowingStage(
                BuildPhase.CppCodeGenAndBuild,
                new BuildStageException("native build process failed"));
            ImmutableArray<RecordingBuildStage> stages = CreateRecordingStages(cppStage);
            BuildPipeline pipeline = CreatePipeline(stages);

            BuildResult result = await pipeline.BuildAsync(BuildTestFixtures.CreateDefaultContext());

            Assert.False(result.Success);
            Assert.Equal(8, result.PhaseResults.Length);
            Assert.False(result.PhaseResults.Single(static phase => phase.Phase == BuildPhase.CppCodeGenAndBuild).Success);
            Assert.False(result.PhaseResults.Single(static phase => phase.Phase == BuildPhase.OutputAssembly).Success);
            Assert.Contains(result.Diagnostics, static diagnostic =>
                diagnostic.Code == BuildDiagnosticCode.InternalError &&
                diagnostic.Phase == BuildPhase.CppCodeGenAndBuild &&
                diagnostic.Message.Contains("native build process failed", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, static diagnostic =>
                diagnostic.Phase == BuildPhase.OutputAssembly &&
                diagnostic.Message.Contains("was skipped", StringComparison.Ordinal));
        }

        [Fact]
        public async Task UnexpectedStageException_BubblesToCaller()
        {
            RecordingBuildStage cppStage = CreateThrowingStage(
                BuildPhase.CppCodeGenAndBuild,
                new InvalidOperationException("unexpected implementation bug"));
            ImmutableArray<RecordingBuildStage> stages = CreateRecordingStages(cppStage);
            BuildPipeline pipeline = CreatePipeline(stages);

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await pipeline.BuildAsync(BuildTestFixtures.CreateDefaultContext()));

            Assert.Contains("unexpected implementation bug", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task BuildAsync_Cancellation_ThrowsOperationCanceledException()
        {
            BuildPipeline pipeline = new();
            using CancellationTokenSource cancellation = new();
            await cancellation.CancelAsync();

            _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await pipeline.BuildAsync(BuildTestFixtures.CreateDefaultContext(), cancellation.Token));
        }

        [Fact]
        public async Task BuildPhasesAsync_UnknownPhase_ThrowsArgumentException()
        {
            BuildPipeline pipeline = new();

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await pipeline.BuildPhasesAsync(
                    BuildTestFixtures.CreateDefaultContext(),
                    ImmutableArray.Create((BuildPhase)999)));

            Assert.Contains("Unknown build phase", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task BuildPipeline_ContextFlags_AreVisibleToFakeStages()
        {
            ImmutableArray<RecordingBuildStage> stages = CreateRecordingStages();
            BuildPipeline pipeline = CreatePipeline(stages);
            BuildContext context = BuildTestFixtures.CreateDefaultContext() with
            {
                ForceRebuild = true,
                LibraryMode = true,
                DryRun = true,
                FileFilter = "Counter*.cs",
            };

            BuildResult result = await pipeline.BuildPhasesAsync(
                context,
                ImmutableArray.Create(BuildPhase.CSharpCompilation));

            RecordingBuildStage compilationStage =
                stages.Single(stage => stage.Phase == BuildPhase.CSharpCompilation);
            Assert.True(result.Success);
            Assert.NotNull(compilationStage.LastContext);
            Assert.True(compilationStage.LastContext.ForceRebuild);
            Assert.True(compilationStage.LastContext.LibraryMode);
            Assert.True(compilationStage.LastContext.DryRun);
            Assert.Equal("Counter*.cs", compilationStage.LastContext.FileFilter);
        }

        private static BuildPipeline CreatePipeline(ImmutableArray<RecordingBuildStage> stages)
        {
            ImmutableArray<IBuildStage> buildStages = stages
                .Select(static stage => (IBuildStage)stage)
                .ToImmutableArray();
            return new BuildPipeline(buildStages);
        }

        private static ImmutableArray<RecordingBuildStage> CreateRecordingStages(
            params RecordingBuildStage[] overrides)
        {
            Dictionary<BuildPhase, RecordingBuildStage> overrideMap = overrides.ToDictionary(
                static stage => stage.Phase,
                static stage => stage);
            ImmutableArray<RecordingBuildStage>.Builder builder =
                ImmutableArray.CreateBuilder<RecordingBuildStage>(8);
            foreach (BuildPhase phase in GetCanonicalPhases())
            {
                if (overrideMap.TryGetValue(phase, out RecordingBuildStage? stage))
                {
                    builder.Add(stage);
                }
                else
                {
                    builder.Add(new RecordingBuildStage(phase, CreateDefaultStageResult(phase)));
                }
            }

            return builder.ToImmutable();
        }

        private static RecordingBuildStage CreateFailedStage(BuildPhase phase, string code)
        {
            BuildDiagnostic diagnostic = new(
                code,
                BuildDiagnosticSeverity.Error,
                $"{phase} failed in fake stage.",
                phase,
                null);
            return new RecordingBuildStage(phase, BuildStageResult.Failed(diagnostic));
        }

        private static RecordingBuildStage CreateThrowingStage(BuildPhase phase, Exception exception)
        {
            return RecordingBuildStage.Throwing(phase, exception);
        }

        private static BuildStageResult CreateDefaultStageResult(BuildPhase phase)
        {
            return phase switch
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
                BuildPhase.CppCodeGenAndBuild => BuildStageResult.Succeeded(new BuildStatsDelta
                {
                    CppFilesGenerated = 2,
                    NativeLibBuilt = true,
                }),
                _ => BuildStageResult.Succeeded(),
            };
        }

        private static ImmutableArray<BuildPhase> GetCanonicalPhases()
        {
            return ImmutableArray.Create(
                BuildPhase.ConfigLoading,
                BuildPhase.CSharpCompilation,
                BuildPhase.ModuleMetadata,
                BuildPhase.DependencyResolution,
                BuildPhase.AssetBundling,
                BuildPhase.QmlValidation,
                BuildPhase.CppCodeGenAndBuild,
                BuildPhase.OutputAssembly);
        }

        private sealed class RecordingBuildStage : IBuildStage
        {
            private readonly Func<BuildContext, CancellationToken, Task<BuildStageResult>> _execute;

            public RecordingBuildStage(BuildPhase phase, BuildStageResult result)
                : this(phase, (context, cancellationToken) => Task.FromResult(result))
            {
            }

            public static RecordingBuildStage Throwing(BuildPhase phase, Exception exception)
            {
                return new RecordingBuildStage(phase, (context, cancellationToken) => throw exception);
            }

            private RecordingBuildStage(
                BuildPhase phase,
                Func<BuildContext, CancellationToken, Task<BuildStageResult>> execute)
            {
                Phase = phase;
                _execute = execute;
            }

            public BuildPhase Phase { get; }

            public int CallCount { get; private set; }

            public BuildContext? LastContext { get; private set; }

            public Task<BuildStageResult> ExecuteAsync(BuildContext context, CancellationToken cancellationToken)
            {
                CallCount++;
                LastContext = context;
                return _execute(context, cancellationToken);
            }
        }
    }
}
