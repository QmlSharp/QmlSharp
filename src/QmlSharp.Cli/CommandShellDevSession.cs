using System.Collections.Immutable;
using QmlSharp.Build;

namespace QmlSharp.Cli
{
    /// <summary>Mock shell dev session used until Step 08.13 implements real watch behavior.</summary>
    public sealed class CommandShellDevSession : IDevSession
    {
        private Action<BuildResult>? _buildComplete;

        /// <inheritdoc />
        public DevSessionState State { get; private set; } = DevSessionState.Stopped;

        /// <inheritdoc />
        public Task StartAsync(DevCommandOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            cancellationToken.ThrowIfCancellationRequested();

            State = DevSessionState.Running;
            _buildComplete?.Invoke(CreateSuccessfulBuildResult());
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<BuildResult> RebuildAsync()
        {
            BuildResult result = CreateSuccessfulBuildResult();
            _buildComplete?.Invoke(result);
            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public void OnBuildComplete(Action<BuildResult> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _buildComplete += callback;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            State = DevSessionState.Stopped;
            return ValueTask.CompletedTask;
        }

        private static BuildResult CreateSuccessfulBuildResult()
        {
            return new BuildResult
            {
                Success = true,
                PhaseResults = ImmutableArray<PhaseResult>.Empty,
                Diagnostics = ImmutableArray<BuildDiagnostic>.Empty,
                Stats = new BuildStats(TimeSpan.Zero, 0, 0, 0, 0, false),
            };
        }
    }
}
