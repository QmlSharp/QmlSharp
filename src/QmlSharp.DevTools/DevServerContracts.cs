#pragma warning disable CA1003, MA0046, MA0048

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Top-level facade for the QmlSharp dev server.
    /// </summary>
    public interface IDevServer : IAsyncDisposable
    {
        /// <summary>Starts the dev server.</summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>Stops the dev server.</summary>
        Task StopAsync();

        /// <summary>Forces an immediate rebuild and hot reload.</summary>
        Task<HotReloadResult> RebuildAsync(CancellationToken cancellationToken = default);

        /// <summary>Performs a full restart.</summary>
        Task RestartAsync(CancellationToken cancellationToken = default);

        /// <summary>Gets the current server status.</summary>
        DevServerStatus Status { get; }

        /// <summary>Gets runtime statistics.</summary>
        ServerStats Stats { get; }

        /// <summary>Fires on every state transition.</summary>
        event Action<DevServerStatusChangedEvent> OnStatusChanged;

        /// <summary>Gets access to the REPL when available.</summary>
        IRepl? Repl { get; }
    }

    /// <summary>Dev server lifecycle states.</summary>
    public enum DevServerStatus
    {
        /// <summary>The server is not running.</summary>
        Idle,

        /// <summary>The server is starting.</summary>
        Starting,

        /// <summary>The server is building.</summary>
        Building,

        /// <summary>The server is running.</summary>
        Running,

        /// <summary>The server is reloading.</summary>
        Reloading,

        /// <summary>The server is in an error state.</summary>
        Error,

        /// <summary>The server is stopping.</summary>
        Stopping,
    }

    /// <summary>Runtime statistics tracked by the dev server.</summary>
    /// <param name="BuildCount">Number of initial builds.</param>
    /// <param name="RebuildCount">Number of incremental rebuilds.</param>
    /// <param name="HotReloadCount">Number of successful hot reloads.</param>
    /// <param name="ErrorCount">Number of errors.</param>
    /// <param name="TotalBuildTime">Total time spent building.</param>
    /// <param name="TotalHotReloadTime">Total time spent hot reloading.</param>
    /// <param name="Uptime">Server uptime.</param>
    public sealed record ServerStats(
        int BuildCount,
        int RebuildCount,
        int HotReloadCount,
        int ErrorCount,
        TimeSpan TotalBuildTime,
        TimeSpan TotalHotReloadTime,
        TimeSpan Uptime);

    /// <summary>Event fired on dev server state transitions.</summary>
    /// <param name="Previous">Previous status.</param>
    /// <param name="Current">Current status.</param>
    /// <param name="Timestamp">Transition timestamp.</param>
    /// <param name="Reason">Optional transition reason.</param>
    public sealed record DevServerStatusChangedEvent(
        DevServerStatus Previous,
        DevServerStatus Current,
        DateTimeOffset Timestamp,
        string? Reason);

    /// <summary>Configuration for the dev server.</summary>
    /// <param name="ProjectRoot">Root project directory.</param>
    /// <param name="WatcherOptions">File watcher options.</param>
    /// <param name="ConsoleOptions">Console options.</param>
    /// <param name="EnableRepl">Whether the REPL is enabled.</param>
    /// <param name="EnableProfiling">Whether profiling is enabled.</param>
    /// <param name="ConfigPath">Optional path to the qmlsharp config file.</param>
    /// <param name="Headless">Whether the dev session should avoid launching a Qt window.</param>
    /// <param name="EntryOverride">Optional command-line entry override that must survive config reloads.</param>
    public sealed record DevServerOptions(
        string ProjectRoot,
        FileWatcherOptions WatcherOptions,
        DevConsoleOptions ConsoleOptions,
        bool EnableRepl = true,
        bool EnableProfiling = true,
        string? ConfigPath = null,
        bool Headless = false,
        string? EntryOverride = null);

#pragma warning restore CA1003, MA0046, MA0048
}
