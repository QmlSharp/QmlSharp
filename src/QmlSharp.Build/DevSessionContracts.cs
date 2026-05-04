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

        /// <summary>Registers a callback for build result notifications.</summary>
        void OnBuildComplete(Action<BuildResult> callback);
    }

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
