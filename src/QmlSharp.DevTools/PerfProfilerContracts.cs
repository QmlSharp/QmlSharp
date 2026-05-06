#pragma warning disable MA0048

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Records performance spans for compilation, hot reload, file watching, and other dev operations.
    /// </summary>
    public interface IPerfProfiler
    {
        /// <summary>Starts a new timing span.</summary>
        IPerfSpan StartSpan(string name, PerfCategory category);

        /// <summary>Returns all recorded spans.</summary>
        IReadOnlyList<PerfRecord> GetRecords();

        /// <summary>Returns an aggregate summary grouped by category.</summary>
        PerfSummary GetSummary();

        /// <summary>Clears all recorded spans.</summary>
        void Clear();

        /// <summary>Exports all spans as Chrome Trace JSON format.</summary>
        void ExportChromeTrace(string outputPath);

        /// <summary>Gets a value indicating whether profiling is enabled.</summary>
        bool IsEnabled { get; }
    }

    /// <summary>
    /// Represents an active performance span.
    /// </summary>
    public interface IPerfSpan : IDisposable
    {
        /// <summary>Adds metadata to the span before it is disposed.</summary>
        void AddMetadata(string key, object? value);
    }

    /// <summary>A single recorded performance span.</summary>
    /// <param name="Name">Span name.</param>
    /// <param name="Category">Category for grouping.</param>
    /// <param name="StartTime">Span start time.</param>
    /// <param name="Duration">Span duration.</param>
    /// <param name="Metadata">Optional span metadata.</param>
    public sealed record PerfRecord(
        string Name,
        PerfCategory Category,
        DateTimeOffset StartTime,
        TimeSpan Duration,
        IReadOnlyDictionary<string, object?>? Metadata);

    /// <summary>Categories for performance spans.</summary>
    public enum PerfCategory
    {
        /// <summary>Compiler work.</summary>
        Compile,

        /// <summary>Hot reload orchestration.</summary>
        HotReload,

        /// <summary>State capture work.</summary>
        Capture,

        /// <summary>State restore work.</summary>
        Restore,

        /// <summary>File watching work.</summary>
        FileWatch,

        /// <summary>QML linting work.</summary>
        QmlLint,

        /// <summary>QML formatting work.</summary>
        QmlFormat,

        /// <summary>REPL work.</summary>
        Repl,

        /// <summary>Build pipeline work.</summary>
        Build,
    }

    /// <summary>Aggregate performance summary.</summary>
    /// <param name="Categories">Per-category aggregates.</param>
    /// <param name="TotalSpans">Total spans recorded.</param>
    /// <param name="TotalTime">Total wall-clock time across all spans.</param>
    public sealed record PerfSummary(
        IReadOnlyDictionary<PerfCategory, CategoryStats> Categories,
        int TotalSpans,
        TimeSpan TotalTime);

    /// <summary>Aggregate stats for a single category.</summary>
    /// <param name="Count">Number of spans.</param>
    /// <param name="TotalTime">Total span time.</param>
    /// <param name="MinTime">Minimum span time.</param>
    /// <param name="MaxTime">Maximum span time.</param>
    /// <param name="AvgTime">Average span time.</param>
    public sealed record CategoryStats(
        int Count,
        TimeSpan TotalTime,
        TimeSpan MinTime,
        TimeSpan MaxTime,
        TimeSpan AvgTime);

#pragma warning restore MA0048
}
