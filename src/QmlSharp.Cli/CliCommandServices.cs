using System.Collections.Immutable;
using QmlSharp.Build;
using QmlSharp.DevTools;

#pragma warning disable MA0048

namespace QmlSharp.Cli
{
    /// <summary>Service bundle used by QmlSharp CLI command registration.</summary>
    public sealed record CliCommandServices
    {
        /// <summary>Configuration loader used by build and dev commands.</summary>
        public required IConfigLoader ConfigLoader { get; init; }

        /// <summary>Build pipeline used by the build command.</summary>
        public required IBuildPipeline BuildPipeline { get; init; }

        /// <summary>Dev server factory used by the dev command.</summary>
        public required ICliDevServerFactory DevServerFactory { get; init; }

        /// <summary>Doctor service used by the doctor command.</summary>
        public required IDoctor Doctor { get; init; }

        /// <summary>Optional factory for project-directory-aware doctor services.</summary>
        public Func<string, IDoctor>? DoctorFactory { get; init; }

        /// <summary>Init service used by the init command.</summary>
        public required IInitService InitService { get; init; }

        /// <summary>Clean service used by the clean command.</summary>
        public required ICleanService CleanService { get; init; }

        /// <summary>Creates default command services for the current build-system implementation wave.</summary>
        public static CliCommandServices CreateDefault()
        {
            ConfigLoader configLoader = new();
            BuildPipeline buildPipeline = new();

            return new CliCommandServices
            {
                ConfigLoader = configLoader,
                BuildPipeline = buildPipeline,
                DevServerFactory = new DefaultCliDevServerFactory(buildPipeline),
                Doctor = new Doctor(),
                DoctorFactory = static projectDir => new Doctor(projectDir),
                InitService = new InitService(),
                CleanService = new CleanService(),
            };
        }

        /// <summary>Creates a doctor for a parsed project directory.</summary>
        public IDoctor CreateDoctor(string projectDir)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectDir);
            return DoctorFactory is null ? Doctor : DoctorFactory(projectDir);
        }
    }

    internal sealed class DefaultCliDevServerFactory : ICliDevServerFactory
    {
        private readonly IBuildPipeline buildPipeline;

        public DefaultCliDevServerFactory(IBuildPipeline buildPipeline)
        {
            ArgumentNullException.ThrowIfNull(buildPipeline);

            this.buildPipeline = buildPipeline;
        }

        public IDevServer Create(DevServerCreationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            IDevToolsBuildPipeline devToolsBuildPipeline = new BuildPipelineAdapter(buildPipeline);
            return new DevServer(
                context.ServerOptions,
                context.BuildContext,
                new FileWatcher(context.ServerOptions.WatcherOptions),
                devToolsBuildPipeline,
                new DevConsole(context.ServerOptions.ConsoleOptions),
                new ErrorOverlay(NoOpErrorOverlayNativeHost.Instance),
                new PerfProfiler(context.ServerOptions.EnableProfiling));
        }
    }

    internal sealed class BuildPipelineAdapter : IDevToolsBuildPipeline
    {
        private readonly IBuildPipeline buildPipeline;

        public BuildPipelineAdapter(IBuildPipeline buildPipeline)
        {
            ArgumentNullException.ThrowIfNull(buildPipeline);

            this.buildPipeline = buildPipeline;
        }

        public Task<BuildResult> BuildAsync(
            BuildContext context,
            CancellationToken cancellationToken = default)
        {
            return buildPipeline.BuildAsync(context, cancellationToken);
        }

        public Task<BuildResult> BuildPhasesAsync(
            BuildContext context,
            ImmutableArray<BuildPhase> phases,
            CancellationToken cancellationToken = default)
        {
            return buildPipeline.BuildPhasesAsync(context, phases, cancellationToken);
        }

        public void OnProgress(Action<BuildProgress> callback)
        {
            buildPipeline.OnProgress(callback);
        }
    }

    internal sealed class NoOpErrorOverlayNativeHost : IErrorOverlayNativeHost
    {
        public static NoOpErrorOverlayNativeHost Instance { get; } = new();

        public void ShowError(string title, string message, string? filePath, int line, int column)
        {
        }

        public void HideError()
        {
        }
    }
}

#pragma warning restore MA0048
