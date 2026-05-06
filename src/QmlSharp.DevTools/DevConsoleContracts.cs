#pragma warning disable MA0048

using QmlSharp.Compiler;

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Formatted terminal output for the dev server.
    /// </summary>
    public interface IDevConsole
    {
        /// <summary>Prints the startup banner with version and config info.</summary>
        void Banner(string version, DevServerOptions options);

        /// <summary>Prints watch startup information.</summary>
        void WatchStarted(int fileCount, IReadOnlyList<string> paths);

        /// <summary>Prints file change notification with file paths.</summary>
        void FileChanged(FileChangeBatch batch);

        /// <summary>Prints build start information.</summary>
        void BuildStart(int fileCount);

        /// <summary>Prints build success information.</summary>
        void BuildSuccess(TimeSpan elapsed, int fileCount);

        /// <summary>Prints build errors with diagnostic details.</summary>
        void BuildError(IReadOnlyList<CompilerDiagnostic> errors);

        /// <summary>Prints successful hot reload information.</summary>
        void HotReloadSuccess(HotReloadResult result);

        /// <summary>Prints hot reload failure information.</summary>
        void HotReloadError(string message);

        /// <summary>Prints a restart-required warning.</summary>
        void RestartRequired(string reason);

        /// <summary>Prints server stop information.</summary>
        void ServerStopped();

        /// <summary>Prints an informational message.</summary>
        void Info(string message);

        /// <summary>Prints a warning message.</summary>
        void Warn(string message);

        /// <summary>Prints an error message.</summary>
        void Error(string message);
    }

    /// <summary>Configuration for developer console output.</summary>
    /// <param name="Level">Minimum log level.</param>
    /// <param name="Color">Whether ANSI color output is enabled.</param>
    /// <param name="ShowTimestamps">Whether each line includes a timestamp.</param>
    /// <param name="Output">Output writer. Null means console output.</param>
    public sealed record DevConsoleOptions(
        LogLevel Level = LogLevel.Info,
        bool Color = true,
        bool ShowTimestamps = true,
        TextWriter? Output = null);

    /// <summary>Console log level filter.</summary>
    public enum LogLevel
    {
        /// <summary>Debug messages.</summary>
        Debug,

        /// <summary>Informational messages.</summary>
        Info,

        /// <summary>Warning messages.</summary>
        Warn,

        /// <summary>Error messages.</summary>
        Error,

        /// <summary>No messages.</summary>
        Silent,
    }

#pragma warning restore MA0048
}
