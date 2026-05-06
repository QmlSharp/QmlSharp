#pragma warning disable MA0048

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Interactive REPL supporting C# and QML evaluation modes.
    /// </summary>
    public interface IRepl : IAsyncDisposable
    {
        /// <summary>Starts the REPL session.</summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>Stops the REPL session.</summary>
        Task StopAsync();

        /// <summary>Evaluates an expression or statement in the current mode.</summary>
        Task<ReplResult> EvalAsync(string input, CancellationToken cancellationToken = default);

        /// <summary>Gets or sets the current evaluation mode.</summary>
        ReplMode Mode { get; set; }

        /// <summary>Gets a value indicating whether the REPL is running.</summary>
        bool IsRunning { get; }

        /// <summary>Gets command history.</summary>
        IReadOnlyList<string> History { get; }
    }

    /// <summary>REPL evaluation modes.</summary>
    public enum ReplMode
    {
        /// <summary>C# expressions via Roslyn scripting.</summary>
        CSharp,

        /// <summary>QML expressions evaluated on the Qt engine.</summary>
        Qml,
    }

    /// <summary>Result of a REPL evaluation.</summary>
    /// <param name="Success">Whether evaluation succeeded.</param>
    /// <param name="Output">Formatted output string.</param>
    /// <param name="ReturnType">The return type of the evaluated expression in C# mode.</param>
    /// <param name="Elapsed">Evaluation duration.</param>
    /// <param name="Error">Error details if evaluation failed.</param>
    public sealed record ReplResult(
        bool Success,
        string Output,
        string? ReturnType,
        TimeSpan Elapsed,
        ReplError? Error);

    /// <summary>REPL evaluation error.</summary>
    /// <param name="Message">Error message.</param>
    /// <param name="Kind">Error kind.</param>
    /// <param name="Line">One-based line number within the input.</param>
    /// <param name="Column">One-based column number within the input.</param>
    public sealed record ReplError(
        string Message,
        ReplErrorKind Kind,
        int? Line,
        int? Column);

    /// <summary>Categories of REPL errors.</summary>
    public enum ReplErrorKind
    {
        /// <summary>C# syntax or semantic error from Roslyn.</summary>
        CompilationError,

        /// <summary>Runtime exception during evaluation.</summary>
        RuntimeError,

        /// <summary>QML evaluation error.</summary>
        QmlError,

        /// <summary>Evaluation timed out.</summary>
        Timeout,

        /// <summary>Built-in command is not supported by the current REPL context.</summary>
        UnsupportedCommand,
    }

    /// <summary>Configuration for a REPL session.</summary>
    /// <param name="DefaultMode">Initial evaluation mode.</param>
    /// <param name="MaxHistory">Maximum number of history entries retained.</param>
    /// <param name="HistoryFilePath">Optional history persistence file.</param>
    /// <param name="EvaluationTimeout">Maximum duration for one evaluation.</param>
    public sealed record ReplOptions(
        ReplMode DefaultMode = ReplMode.CSharp,
        int MaxHistory = 100,
        string? HistoryFilePath = null,
        TimeSpan? EvaluationTimeout = null);

#pragma warning restore MA0048
}
