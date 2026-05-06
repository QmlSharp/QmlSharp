using System.Diagnostics;
using System.Globalization;
using QmlSharp.Compiler;

#pragma warning disable CA1303, MA0048

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Direct ANSI-formatted developer console output.
    /// </summary>
    public sealed class DevConsole : IDevConsole
    {
        private const string Escape = "\u001b[";
        private const string Reset = Escape + "0m";
        private const string Bold = Escape + "1m";
        private const string Cyan = Escape + "36m";
        private const string Green = Escape + "32m";
        private const string Yellow = Escape + "33m";
        private const string Red = Escape + "31m";
        private const string CheckMark = "\u2705";
        private const string CrossMark = "\u274c";
        private const string WarningMark = "\u26a0";

        private readonly DevConsoleOptions options;
        private readonly IConsoleWriter writer;
        private readonly IDevToolsClock clock;
        private readonly ISourceMapLookup sourceMapLookup;

        /// <summary>
        /// Initializes a developer console using the configured output writer or Console.Out.
        /// </summary>
        /// <param name="options">Console options.</param>
        public DevConsole(DevConsoleOptions options)
            : this(options, writer: null, clock: null, sourceMapLookup: null)
        {
        }

        /// <summary>
        /// Initializes a developer console with deterministic timing and optional source maps.
        /// </summary>
        /// <param name="options">Console options.</param>
        /// <param name="clock">Clock used for timestamps.</param>
        /// <param name="sourceMapLookup">Source-map lookup used for diagnostic formatting.</param>
        public DevConsole(
            DevConsoleOptions options,
            IDevToolsClock? clock,
            ISourceMapLookup? sourceMapLookup = null)
            : this(options, writer: null, clock, sourceMapLookup)
        {
        }

        /// <summary>
        /// Initializes a developer console with an injectable writer.
        /// </summary>
        /// <param name="options">Console options.</param>
        /// <param name="writer">Console writer used for all output.</param>
        /// <param name="clock">Clock used for timestamps.</param>
        /// <param name="sourceMapLookup">Source-map lookup used for diagnostic formatting.</param>
        public DevConsole(
            DevConsoleOptions options,
            IConsoleWriter? writer,
            IDevToolsClock? clock = null,
            ISourceMapLookup? sourceMapLookup = null)
        {
            ArgumentNullException.ThrowIfNull(options);

            this.options = options;
            this.writer = writer ?? new TextWriterConsoleWriter(options.Output ?? Console.Out);
            this.clock = clock ?? SystemDevToolsClock.Instance;
            this.sourceMapLookup = sourceMapLookup ?? SourceMapLookup.Empty;
        }

        /// <inheritdoc />
        public void Banner(string version, DevServerOptions serverOptions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(version);
            ArgumentNullException.ThrowIfNull(serverOptions);

            Write(LogLevel.Info, Colorize($"QmlSharp {version}", Bold + Cyan));
            Write(LogLevel.Info, $"{Prefix("server")} Project: {serverOptions.ProjectRoot}");
            Write(LogLevel.Info, $"{Prefix("watch")} {FormatPathList(serverOptions.WatcherOptions.WatchPaths, maxItems: 3)}");
        }

        /// <inheritdoc />
        public void WatchStarted(int fileCount, IReadOnlyList<string> paths)
        {
            ArgumentNullException.ThrowIfNull(paths);

            Write(LogLevel.Info, $"{Prefix("watch")} Watching {fileCount.ToString(CultureInfo.InvariantCulture)} file(s) in {FormatPathList(paths, maxItems: 3)}");
        }

        /// <inheritdoc />
        public void FileChanged(FileChangeBatch batch)
        {
            ArgumentNullException.ThrowIfNull(batch);

            ImmutableArray<string> paths = batch.Changes
                .OrderBy(static change => change.FilePath, StringComparer.Ordinal)
                .Select(static change => change.FilePath)
                .ToImmutableArray();
            Write(LogLevel.Info, $"{Prefix("watch")} File change: {FormatPathList(paths, maxItems: 3)}");
        }

        /// <inheritdoc />
        public void BuildStart(int fileCount)
        {
            Write(LogLevel.Info, $"{Prefix("build")} Compiling {fileCount.ToString(CultureInfo.InvariantCulture)} file(s)...");
        }

        /// <inheritdoc />
        public void BuildSuccess(TimeSpan elapsed, int fileCount)
        {
            Write(
                LogLevel.Info,
                $"{Prefix("build")} {Colorize(CheckMark, Green)} Compiled in {FormatMilliseconds(elapsed)} ({fileCount.ToString(CultureInfo.InvariantCulture)} file(s))");
        }

        /// <inheritdoc />
        public void BuildError(IReadOnlyList<CompilerDiagnostic> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            int errorCount = errors.Count(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Fatal);
            int warningCount = errors.Count(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);
            int diagnosticCount = errors.Count;
            Write(
                LogLevel.Error,
                $"{Prefix("build")} {Colorize(CrossMark, Red)} Build failed: {errorCount.ToString(CultureInfo.InvariantCulture)} error(s), {warningCount.ToString(CultureInfo.InvariantCulture)} warning(s), {diagnosticCount.ToString(CultureInfo.InvariantCulture)} diagnostic(s)");

            foreach (CompilerDiagnostic diagnostic in errors)
            {
                Write(LogLevel.Error, FormatDiagnostic(diagnostic));
            }
        }

        /// <inheritdoc />
        public void HotReloadSuccess(HotReloadResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            Write(
                LogLevel.Info,
                $"{Prefix("reload")} {Colorize(CheckMark, Green)} Hot reload in {FormatMilliseconds(result.TotalTime)} ({result.InstancesMatched.ToString(CultureInfo.InvariantCulture)} matched, {result.InstancesOrphaned.ToString(CultureInfo.InvariantCulture)} orphaned, {result.InstancesNew.ToString(CultureInfo.InvariantCulture)} new)");
        }

        /// <inheritdoc />
        public void HotReloadError(string message)
        {
            string output = string.IsNullOrWhiteSpace(message) ? "Hot reload failed." : message;
            Write(LogLevel.Error, $"{Prefix("reload")} {Colorize(CrossMark, Red)} Hot reload failed: {output}");
        }

        /// <inheritdoc />
        public void RestartRequired(string reason)
        {
            string output = string.IsNullOrWhiteSpace(reason) ? "Schema change detected." : reason;
            Write(LogLevel.Warn, $"{Prefix("reload")} {Colorize(WarningMark, Yellow)} Restart required: {output}");
        }

        /// <inheritdoc />
        public void ServerStopped()
        {
            Write(LogLevel.Info, $"{Prefix("server")} Stopped.");
        }

        /// <inheritdoc />
        public void Info(string message)
        {
            Write(LogLevel.Info, $"{Prefix("qmlsharp")} {message}");
        }

        /// <inheritdoc />
        public void Warn(string message)
        {
            Write(LogLevel.Warn, $"{Prefix("warn")} {Colorize(WarningMark, Yellow)} {message}");
        }

        /// <inheritdoc />
        public void Error(string message)
        {
            Write(LogLevel.Error, $"{Prefix("error")} {Colorize(CrossMark, Red)} {message}");
        }

        private void Write(LogLevel level, string message)
        {
            if (!ShouldWrite(level))
            {
                return;
            }

            string normalized = message.Replace("\r\n", "\n", StringComparison.Ordinal);
            foreach (string line in normalized.Split('\n'))
            {
                writer.WriteLine(ApplyTimestamp(line));
            }
        }

        private bool ShouldWrite(LogLevel level)
        {
            return options.Level != LogLevel.Silent && level >= options.Level;
        }

        private string ApplyTimestamp(string message)
        {
            if (!options.ShowTimestamps)
            {
                return message;
            }

            return clock.UtcNow.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " " + message;
        }

        private string FormatDiagnostic(CompilerDiagnostic diagnostic)
        {
            ArgumentNullException.ThrowIfNull(diagnostic);

            DiagnosticLocation diagnosticLocation = ResolveDiagnosticLocation(diagnostic.Location);
            string severity = FormatSeverity(diagnostic.Severity);
            string location = diagnosticLocation.DisplayLocation is null
                ? "unknown location"
                : FormatLocation(diagnosticLocation.DisplayLocation);
            string generatedLocation = diagnosticLocation.GeneratedLocation is not null
                ? " (generated " + FormatLocation(diagnosticLocation.GeneratedLocation) + ")"
                : string.Empty;
            string diagnosticPrefix = Colorize(severity, GetSeverityColor(diagnostic.Severity));

            return $"  {diagnosticPrefix} {diagnostic.Code}: {diagnostic.Message} at {location}{generatedLocation}";
        }

        private DiagnosticLocation ResolveDiagnosticLocation(SourceLocation? location)
        {
            if (location is null)
            {
                return new DiagnosticLocation(null, null);
            }

            SourceLocation? mappedLocation = sourceMapLookup.FindSourceLocation(location);
            return mappedLocation is null
                ? new DiagnosticLocation(location, null)
                : new DiagnosticLocation(mappedLocation, location);
        }

        private static string FormatLocation(SourceLocation location)
        {
            if (location.FilePath is not null && location.Line.HasValue && location.Column.HasValue)
            {
                int line = location.Line.Value;
                int column = location.Column.Value;
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{location.FilePath}:{line}:{column}");
            }

            if (location.FilePath is not null)
            {
                return location.FilePath;
            }

            if (location.Line.HasValue && location.Column.HasValue)
            {
                int line = location.Line.Value;
                int column = location.Column.Value;
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"line {line}:{column}");
            }

            return "unknown location";
        }

        private static string FormatSeverity(DiagnosticSeverity severity)
        {
            return severity switch
            {
                DiagnosticSeverity.Info => "info",
                DiagnosticSeverity.Warning => "warning",
                DiagnosticSeverity.Error => "error",
                DiagnosticSeverity.Fatal => "fatal",
                _ => severity.ToString().ToLowerInvariant(),
            };
        }

        private static string GetSeverityColor(DiagnosticSeverity severity)
        {
            return severity switch
            {
                DiagnosticSeverity.Info => Cyan,
                DiagnosticSeverity.Warning => Yellow,
                DiagnosticSeverity.Error or DiagnosticSeverity.Fatal => Red,
                _ => Reset,
            };
        }

        private string Prefix(string scope)
        {
            return Colorize($"[{scope}]", Cyan);
        }

        private string Colorize(string text, string ansiCode)
        {
            return options.Color ? ansiCode + text + Reset : text;
        }

        private static string FormatMilliseconds(TimeSpan elapsed)
        {
            long milliseconds = (long)Math.Round(elapsed.TotalMilliseconds, MidpointRounding.AwayFromZero);
            return milliseconds.ToString(CultureInfo.InvariantCulture) + "ms";
        }

        private static string FormatPathList(IReadOnlyList<string> paths, int maxItems)
        {
            if (paths.Count == 0)
            {
                return "(none)";
            }

            ImmutableArray<string> sortedPaths = paths
                .OrderBy(static path => path, StringComparer.Ordinal)
                .Take(maxItems)
                .ToImmutableArray();
            string suffix = paths.Count > maxItems
                ? " +" + (paths.Count - maxItems).ToString(CultureInfo.InvariantCulture) + " more"
                : string.Empty;

            return string.Join(", ", sortedPaths) + suffix;
        }

        private sealed record DiagnosticLocation(SourceLocation? DisplayLocation, SourceLocation? GeneratedLocation);

        private sealed class TextWriterConsoleWriter : IConsoleWriter
        {
            private readonly TextWriter textWriter;

            public TextWriterConsoleWriter(TextWriter textWriter)
            {
                ArgumentNullException.ThrowIfNull(textWriter);

                this.textWriter = textWriter;
            }

            public void Write(string text)
            {
                textWriter.Write(text);
            }

            public void WriteLine(string text)
            {
                textWriter.WriteLine(text);
            }
        }

        private sealed class SystemDevToolsClock : IDevToolsClock
        {
            public static SystemDevToolsClock Instance { get; } = new();

            public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

            public long GetTimestamp()
            {
                return Stopwatch.GetTimestamp();
            }

            public TimeSpan GetElapsedTime(long startTimestamp)
            {
                return Stopwatch.GetElapsedTime(startTimestamp);
            }
        }
    }
}

#pragma warning restore CA1303, MA0048
