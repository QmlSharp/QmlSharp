#pragma warning disable CA1003, MA0046, MA0048

using QmlSharp.Compiler;

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Coordinates the hot reload protocol after the compiler emits new artifacts.
    /// </summary>
    public interface IHotReloadOrchestrator
    {
        /// <summary>Executes the full hot reload sequence.</summary>
        /// <param name="result">Compilation result from the compiler module.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the reload.</param>
        /// <returns>Detailed result with per-phase timing.</returns>
        Task<HotReloadResult> ReloadAsync(
            CompilationResult result,
            CancellationToken cancellationToken = default);

        /// <summary>Fires before the hot reload sequence begins.</summary>
        event Action<HotReloadStartingEvent> OnBefore;

        /// <summary>Fires after the hot reload sequence completes.</summary>
        event Action<HotReloadCompletedEvent> OnAfter;
    }

    /// <summary>Result of a complete hot reload operation.</summary>
    /// <param name="Success">Whether all hot reload steps succeeded.</param>
    /// <param name="InstancesMatched">Number of instances matched by class name and compiler slot key.</param>
    /// <param name="InstancesOrphaned">Old instances with no matching new instance.</param>
    /// <param name="InstancesNew">New instances with no matching old instance.</param>
    /// <param name="Phases">Per-phase timing breakdown.</param>
    /// <param name="TotalTime">Total wall-clock time.</param>
    /// <param name="ErrorMessage">Error message if the reload failed.</param>
    /// <param name="FailedStep">The failed step, if any.</param>
    public sealed record HotReloadResult(
        bool Success,
        int InstancesMatched,
        int InstancesOrphaned,
        int InstancesNew,
        HotReloadPhases Phases,
        TimeSpan TotalTime,
        string? ErrorMessage,
        HotReloadStep? FailedStep);

    /// <summary>Per-phase timing for the hot reload protocol.</summary>
    /// <param name="CaptureTime">Snapshot capture duration.</param>
    /// <param name="NukeLoadTime">QML reload duration.</param>
    /// <param name="HydrateTime">State hydration duration.</param>
    /// <param name="RestoreTime">Native restore duration.</param>
    public sealed record HotReloadPhases(
        TimeSpan CaptureTime,
        TimeSpan NukeLoadTime,
        TimeSpan HydrateTime,
        TimeSpan RestoreTime);

    /// <summary>The steps of the hot reload protocol.</summary>
    public enum HotReloadStep
    {
        /// <summary>Capture native and managed state before reload.</summary>
        Capture,

        /// <summary>Reload the generated QML.</summary>
        NukeLoad,

        /// <summary>Hydrate newly-created instances.</summary>
        Hydrate,

        /// <summary>Restore native window and view state.</summary>
        Restore,
    }

    /// <summary>Event fired before hot reload begins.</summary>
    /// <param name="OldInstanceCount">Number of old instances captured before reload.</param>
    /// <param name="Timestamp">Event timestamp.</param>
    public sealed record HotReloadStartingEvent(
        int OldInstanceCount,
        DateTimeOffset Timestamp);

    /// <summary>Event fired after hot reload completes.</summary>
    /// <param name="Result">The completed hot reload result.</param>
    /// <param name="Timestamp">Event timestamp.</param>
    public sealed record HotReloadCompletedEvent(
        HotReloadResult Result,
        DateTimeOffset Timestamp);

#pragma warning restore CA1003, MA0046, MA0048
}
