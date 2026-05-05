#pragma warning disable MA0048

namespace QmlSharp.Build
{
    /// <summary>Manages a development session with watch mode and hot reload hooks.</summary>
    public interface IDevSession : IAsyncDisposable
    {
        /// <summary>Starts the dev session.</summary>
        Task StartAsync(DevCommandOptions options, CancellationToken cancellationToken = default);

        /// <summary>Triggers a rebuild.</summary>
        Task<BuildResult> RebuildAsync();

        /// <summary>Current session state.</summary>
        DevSessionState State { get; }

        /// <summary>Most recent build result observed by the session.</summary>
        BuildResult? LastBuild { get; }

        /// <summary>Registers a callback for build result notifications.</summary>
        void OnBuildComplete(Action<BuildResult> callback);

        /// <summary>Registers a callback for state-change notifications.</summary>
        void OnStateChanged(Action<DevSessionState> callback);
    }

    /// <summary>Minimal host hook used by build-system dev sessions.</summary>
    public interface IDevHostHook : IAsyncDisposable
    {
        /// <summary>Starts the host after a successful initial build.</summary>
        Task StartAsync(DevHostStartRequest request, CancellationToken cancellationToken = default);

        /// <summary>Requests host hot reload after a successful rebuild.</summary>
        Task ReloadAsync(DevHostReloadRequest request, CancellationToken cancellationToken = default);

        /// <summary>Stops the host hook.</summary>
        Task StopAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>Host start request created by the minimal dev session.</summary>
    public sealed record DevHostStartRequest(
        DevCommandOptions Options,
        string ProjectDir,
        string OutputDir,
        string Entry,
        BuildResult BuildResult);

    /// <summary>Host hot reload request created by the minimal dev session.</summary>
    public sealed record DevHostReloadRequest(
        DevCommandOptions Options,
        string ProjectDir,
        string OutputDir,
        string Entry,
        ImmutableArray<string> ChangedFiles,
        BuildResult BuildResult);

    /// <summary>Dev session state.</summary>
    public enum DevSessionState
    {
        /// <summary>Session is starting.</summary>
        Starting,

        /// <summary>Initial build is running.</summary>
        Building,

        /// <summary>Session is running.</summary>
        Running,

        /// <summary>Rebuild is running.</summary>
        Rebuilding,

        /// <summary>Session is in an error state.</summary>
        Error,

        /// <summary>Session has stopped.</summary>
        Stopped,
    }
}

#pragma warning restore MA0048
