#pragma warning disable MA0048

using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using QmlSharp.Core;
using QmlSharp.Host.Instances;

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Interactive REPL for C# Roslyn scripting, QML evaluation, and dev-server commands.
    /// </summary>
    public sealed class Repl : IRepl
    {
        private static readonly TimeSpan DefaultEvaluationTimeout = TimeSpan.FromSeconds(10);
        private static readonly JsonSerializerOptions HistoryJsonOptions = new()
        {
            WriteIndented = true,
        };

        private readonly SemaphoreSlim evalGate = new(1, 1);
        private readonly ReplOptions options;
        private readonly IDevToolsNativeHost? nativeHost;
        private readonly IDevServer? devServer;
        private readonly IPerfProfiler? profiler;
        private readonly IDevToolsClock clock;
        private readonly List<string> history = new();
        private ScriptState<object?>? scriptState;
        private bool isRunning;
        private bool disposed;

        /// <summary>
        /// Initializes a standalone C# REPL.
        /// </summary>
        /// <param name="options">Optional REPL configuration.</param>
        public Repl(ReplOptions? options = null)
            : this(options, nativeHost: null, devServer: null, profiler: null)
        {
        }

        /// <summary>
        /// Initializes a REPL with optional native-host, dev-server, and profiler integrations.
        /// </summary>
        /// <param name="options">Optional REPL configuration.</param>
        /// <param name="nativeHost">Native-host facade for QML evaluation and instance inspection.</param>
        /// <param name="devServer">DevServer facade for rebuild and restart commands.</param>
        /// <param name="profiler">Profiler used by built-in perf commands.</param>
        public Repl(
            ReplOptions? options,
            IDevToolsNativeHost? nativeHost,
            IDevServer? devServer,
            IPerfProfiler? profiler)
            : this(options, nativeHost, devServer, profiler, SystemDevToolsClock.Instance)
        {
        }

        internal Repl(
            ReplOptions? options,
            IDevToolsNativeHost? nativeHost,
            IDevServer? devServer,
            IPerfProfiler? profiler,
            IDevToolsClock clock)
        {
            ArgumentNullException.ThrowIfNull(clock);

            this.options = NormalizeOptions(options ?? new ReplOptions());
            this.nativeHost = nativeHost;
            this.devServer = devServer;
            this.profiler = profiler;
            this.clock = clock;
            Mode = this.options.DefaultMode;
        }

        /// <inheritdoc />
        public ReplMode Mode { get; set; }

        /// <inheritdoc />
        public bool IsRunning => isRunning;

        /// <inheritdoc />
        public IReadOnlyList<string> History => history.ToImmutableArray();

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (isRunning)
            {
                return Task.CompletedTask;
            }

            LoadHistory();
            scriptState = null;
            isRunning = true;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync()
        {
            if (!isRunning)
            {
                return Task.CompletedTask;
            }

            SaveHistory();
            scriptState = null;
            isRunning = false;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<ReplResult> EvalAsync(string input, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (!isRunning)
            {
                throw new InvalidOperationException("REPL is not started.");
            }

            ArgumentNullException.ThrowIfNull(input);

            long waitStart = clock.GetTimestamp();
            try
            {
                await evalGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                return Failure(
                    exception.Message,
                    ReplErrorKind.Timeout,
                    waitStart,
                    line: null,
                    column: null);
            }

            try
            {
                ThrowIfDisposed();
                return await EvalCoreAsync(input, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ = evalGate.Release();
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (disposed)
            {
                return;
            }

            await StopAsync().ConfigureAwait(false);
            disposed = true;
            evalGate.Dispose();
        }

        private async Task<ReplResult> EvalCoreAsync(string input, CancellationToken cancellationToken)
        {
            AddHistory(input);
            long startTimestamp = clock.GetTimestamp();
            using IDisposable? span = profiler?.StartSpan("repl_eval", PerfCategory.Repl);
            using CancellationTokenSource linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            TimeSpan evaluationTimeout = options.EvaluationTimeout ?? DefaultEvaluationTimeout;
            linkedCancellation.CancelAfter(evaluationTimeout);

            try
            {
                string trimmedInput = input.Trim();
                if (trimmedInput.StartsWith(':'))
                {
                    return await EvaluateCommandAsync(trimmedInput, startTimestamp, linkedCancellation.Token).ConfigureAwait(false);
                }

                return Mode == ReplMode.CSharp
                    ? await EvaluateCSharpAsync(input, startTimestamp, cancellationToken, evaluationTimeout).ConfigureAwait(false)
                    : await EvaluateQmlAsync(input, startTimestamp, linkedCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                return Failure(
                    exception.Message,
                    ReplErrorKind.Timeout,
                    startTimestamp,
                    line: null,
                    column: null);
            }
            catch (Exception exception) when (!IsCriticalException(exception))
            {
                return Failure(
                    exception.Message,
                    ReplErrorKind.RuntimeError,
                    startTimestamp,
                    line: null,
                    column: null);
            }
        }

        private async Task<ReplResult> EvaluateCSharpAsync(
            string input,
            long startTimestamp,
            CancellationToken cancellationToken,
            TimeSpan evaluationTimeout)
        {
            try
            {
                ScriptState<object?> currentState =
                    await RunCSharpScriptWithTimeoutAsync(input, cancellationToken, evaluationTimeout).ConfigureAwait(false);
                scriptState = currentState;

                object? returnValue = scriptState.ReturnValue;
                string output = FormatValue(returnValue);
                string? returnType = returnValue?.GetType().Name;
                return Success(output, returnType, startTimestamp);
            }
            catch (CompilationErrorException exception)
            {
                Diagnostic? diagnostic = exception.Diagnostics.FirstOrDefault();
                (int? line, int? column) = GetDiagnosticLocation(diagnostic);
                return Failure(
                    FormatCompilationError(exception),
                    ReplErrorKind.CompilationError,
                    startTimestamp,
                    line,
                    column);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (!IsCriticalException(exception))
            {
                return Failure(
                    exception.Message,
                    ReplErrorKind.RuntimeError,
                    startTimestamp,
                    line: null,
                    column: null);
            }
        }

        private async Task<ScriptState<object?>> RunCSharpScriptWithTimeoutAsync(
            string input,
            CancellationToken cancellationToken,
            TimeSpan evaluationTimeout)
        {
            ScriptState<object?>? previousState = scriptState;
            ScriptOptions scriptOptions = CreateScriptOptions();
            Task<ScriptState<object?>> evaluationTask = Task.Run(
                async () =>
                {
                    return previousState is null
                        ? await CSharpScript.RunAsync(
                            input,
                            scriptOptions,
                            cancellationToken: cancellationToken).ConfigureAwait(false)
                        : await previousState.ContinueWithAsync(
                            input,
                            scriptOptions,
                            cancellationToken).ConfigureAwait(false);
                },
                CancellationToken.None);

            Task timeoutTask = Task.Delay(evaluationTimeout, CancellationToken.None);
            Task cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            Task completedTask = await Task.WhenAny(evaluationTask, timeoutTask, cancellationTask).ConfigureAwait(false);
            if (ReferenceEquals(completedTask, evaluationTask))
            {
                return await evaluationTask.ConfigureAwait(false);
            }

            ObserveScriptFault(evaluationTask);
            if (ReferenceEquals(completedTask, cancellationTask))
            {
                throw new OperationCanceledException(cancellationToken);
            }

            throw new OperationCanceledException("C# evaluation timed out.");
        }

        private static void ObserveScriptFault(Task evaluationTask)
        {
            _ = evaluationTask.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private async Task<ReplResult> EvaluateQmlAsync(
            string input,
            long startTimestamp,
            CancellationToken cancellationToken)
        {
            if (nativeHost is null)
            {
                return Failure(
                    "QML evaluation requires a native host.",
                    ReplErrorKind.QmlError,
                    startTimestamp,
                    line: null,
                    column: null);
            }

            try
            {
                string output = await nativeHost.EvaluateQmlAsync(input, cancellationToken).ConfigureAwait(false);
                return Success(output, returnType: null, startTimestamp);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (!IsCriticalException(exception))
            {
                return Failure(
                    exception.Message,
                    ReplErrorKind.QmlError,
                    startTimestamp,
                    line: null,
                    column: null);
            }
        }

        private async Task<ReplResult> EvaluateCommandAsync(
            string input,
            long startTimestamp,
            CancellationToken cancellationToken)
        {
            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string command = parts.Length == 0 ? input : parts[0];

            return command.ToLowerInvariant() switch
            {
                ":mode" => EvaluateModeCommand(parts, startTimestamp),
                ":rebuild" => await EvaluateRebuildCommandAsync(startTimestamp, cancellationToken).ConfigureAwait(false),
                ":restart" => await EvaluateRestartCommandAsync(startTimestamp, cancellationToken).ConfigureAwait(false),
                ":instances" => await EvaluateInstancesCommandAsync(startTimestamp, cancellationToken).ConfigureAwait(false),
                ":perf" => EvaluatePerfCommand(parts, input, startTimestamp),
                ":clear" => Success("\u001b[2J\u001b[H", returnType: null, startTimestamp),
                ":help" => Success(CreateHelpText(), returnType: null, startTimestamp),
                ":history" => Success(CreateHistoryText(), returnType: null, startTimestamp),
                ":quit" => await EvaluateQuitCommandAsync(startTimestamp).ConfigureAwait(false),
                _ => UnsupportedCommand("Unsupported REPL command '" + command + "'.", startTimestamp),
            };
        }

        private ReplResult EvaluateModeCommand(string[] parts, long startTimestamp)
        {
            if (parts.Length != 2)
            {
                return UnsupportedCommand("Usage: :mode csharp|qml", startTimestamp);
            }

            if (parts[1].Equals("csharp", StringComparison.OrdinalIgnoreCase) ||
                parts[1].Equals("cs", StringComparison.OrdinalIgnoreCase))
            {
                Mode = ReplMode.CSharp;
                return Success("Mode switched to CSharp.", returnType: null, startTimestamp);
            }

            if (parts[1].Equals("qml", StringComparison.OrdinalIgnoreCase))
            {
                Mode = ReplMode.Qml;
                return Success("Mode switched to Qml.", returnType: null, startTimestamp);
            }

            return UnsupportedCommand("Unsupported REPL mode '" + parts[1] + "'.", startTimestamp);
        }

        private async Task<ReplResult> EvaluateRebuildCommandAsync(
            long startTimestamp,
            CancellationToken cancellationToken)
        {
            if (devServer is null)
            {
                return UnsupportedCommand(":rebuild requires a DevServer.", startTimestamp);
            }

            HotReloadResult result = await devServer.RebuildAsync(cancellationToken).ConfigureAwait(false);
            if (result.Success)
            {
                string output = "Rebuild succeeded in " + FormatMilliseconds(result.TotalTime) + ".";
                return Success(output, returnType: null, startTimestamp);
            }

            string errorMessage = "Rebuild failed: " + (result.ErrorMessage ?? "unknown error");
            return Failure(
                errorMessage,
                ReplErrorKind.RuntimeError,
                startTimestamp,
                line: null,
                column: null);
        }

        private async Task<ReplResult> EvaluateRestartCommandAsync(
            long startTimestamp,
            CancellationToken cancellationToken)
        {
            if (devServer is null)
            {
                return UnsupportedCommand(":restart requires a DevServer.", startTimestamp);
            }

            await devServer.RestartAsync(cancellationToken).ConfigureAwait(false);
            return Success("Restart requested.", returnType: null, startTimestamp);
        }

        private async Task<ReplResult> EvaluateInstancesCommandAsync(
            long startTimestamp,
            CancellationToken cancellationToken)
        {
            if (nativeHost is null)
            {
                return UnsupportedCommand(":instances requires a native host.", startTimestamp);
            }

            IReadOnlyList<InstanceInfo> instances = await nativeHost.GetInstancesAsync(cancellationToken).ConfigureAwait(false);
            if (instances.Count == 0)
            {
                return Success("No active ViewModel instances.", returnType: null, startTimestamp);
            }

            IEnumerable<string> lines = instances
                .OrderBy(static instance => instance.ClassName, StringComparer.Ordinal)
                .ThenBy(static instance => instance.CompilerSlotKey, StringComparer.Ordinal)
                .ThenBy(static instance => instance.InstanceId, StringComparer.Ordinal)
                .Select(static instance =>
                    instance.ClassName +
                    " [" + instance.State + "] " +
                    instance.InstanceId +
                    " " +
                    instance.CompilerSlotKey);
            return Success(string.Join(Environment.NewLine, lines), returnType: null, startTimestamp);
        }

        private ReplResult EvaluatePerfCommand(string[] parts, string input, long startTimestamp)
        {
            if (profiler is null)
            {
                return UnsupportedCommand(":perf requires a profiler.", startTimestamp);
            }

            if (parts.Length >= 3 && parts[1].Equals("export", StringComparison.OrdinalIgnoreCase))
            {
                string outputPath = input[(input.IndexOf("export", StringComparison.OrdinalIgnoreCase) + "export".Length)..].Trim();
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    return UnsupportedCommand("Usage: :perf export <path>", startTimestamp);
                }

                profiler.ExportChromeTrace(outputPath);
                return Success("Profiler trace exported to " + outputPath + ".", returnType: null, startTimestamp);
            }

            PerfSummary summary = profiler.GetSummary();
            if (summary.TotalSpans == 0)
            {
                return Success("No performance spans recorded.", returnType: null, startTimestamp);
            }

            IEnumerable<string> lines = summary.Categories
                .OrderBy(static pair => pair.Key)
                .Select(static pair =>
                    pair.Key +
                    ": " +
                    pair.Value.Count +
                    " span(s), avg " +
                    FormatMilliseconds(pair.Value.AvgTime));
            return Success(string.Join(Environment.NewLine, lines), returnType: null, startTimestamp);
        }

        private async Task<ReplResult> EvaluateQuitCommandAsync(long startTimestamp)
        {
            await StopAsync().ConfigureAwait(false);
            return Success("REPL stopped.", returnType: null, startTimestamp);
        }

        private void AddHistory(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            history.Add(input);
            TrimHistory();
        }

        private void LoadHistory()
        {
            history.Clear();
            if (string.IsNullOrWhiteSpace(options.HistoryFilePath) ||
                !File.Exists(options.HistoryFilePath))
            {
                return;
            }

            string text = File.ReadAllText(options.HistoryFilePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            IReadOnlyList<string> entries = TryReadJsonHistory(text, out ImmutableArray<string> jsonEntries)
                ? jsonEntries
                : text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            history.AddRange(entries.Where(static entry => !string.IsNullOrWhiteSpace(entry)));
            TrimHistory();
        }

        private void SaveHistory()
        {
            if (string.IsNullOrWhiteSpace(options.HistoryFilePath))
            {
                return;
            }

            string fullPath = Path.GetFullPath(options.HistoryFilePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(history, HistoryJsonOptions);
            File.WriteAllText(fullPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private void TrimHistory()
        {
            int extra = history.Count - options.MaxHistory;
            if (extra > 0)
            {
                history.RemoveRange(0, extra);
            }
        }

        private static bool TryReadJsonHistory(string text, out ImmutableArray<string> entries)
        {
            try
            {
                string[]? parsed = JsonSerializer.Deserialize<string[]>(text);
                entries = parsed?.ToImmutableArray() ?? ImmutableArray<string>.Empty;
                return parsed is not null;
            }
            catch (JsonException)
            {
                entries = ImmutableArray<string>.Empty;
                return false;
            }
        }

        private static ReplOptions NormalizeOptions(ReplOptions options)
        {
            if (!Enum.IsDefined(options.DefaultMode))
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.DefaultMode, "Unknown REPL mode.");
            }

            if (options.MaxHistory <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxHistory, "History size must be greater than zero.");
            }

            if (options.EvaluationTimeout.HasValue && options.EvaluationTimeout.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.EvaluationTimeout, "Evaluation timeout must be positive.");
            }

            return options;
        }

        private static ScriptOptions CreateScriptOptions()
        {
            ImmutableArray<Assembly> references = ImmutableArray.Create(
                typeof(object).Assembly,
                typeof(Console).Assembly,
                typeof(Enumerable).Assembly,
                typeof(Task).Assembly,
                typeof(StateAttribute).Assembly,
                typeof(Repl).Assembly);

            return ScriptOptions.Default
                .AddReferences(references)
                .AddImports(
                    "System",
                    "System.Collections.Generic",
                    "System.Collections.Immutable",
                    "System.Linq",
                    "System.Threading",
                    "System.Threading.Tasks",
                    "QmlSharp.Core",
                    "QmlSharp.DevTools");
        }

        private static (int? Line, int? Column) GetDiagnosticLocation(Diagnostic? diagnostic)
        {
            if (diagnostic is null || !diagnostic.Location.IsInSource)
            {
                return (null, null);
            }

            FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
            return (span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1);
        }

        private static string FormatCompilationError(CompilationErrorException exception)
        {
            ImmutableArray<Diagnostic> diagnostics = exception.Diagnostics.ToImmutableArray();
            if (diagnostics.Length == 0)
            {
                return exception.Message;
            }

            return string.Join(Environment.NewLine, diagnostics.Select(static diagnostic => diagnostic.ToString()));
        }

        private static string FormatValue(object? value)
        {
            return value switch
            {
                null => string.Empty,
                string text => text,
                _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            };
        }

        private static string FormatMilliseconds(TimeSpan elapsed)
        {
            return elapsed.TotalMilliseconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "ms";
        }

        private static string CreateHelpText()
        {
            return string.Join(
                Environment.NewLine,
                ":mode csharp",
                ":mode qml",
                ":rebuild",
                ":restart",
                ":instances",
                ":perf",
                ":perf export <path>",
                ":history",
                ":clear",
                ":help",
                ":quit");
        }

        private string CreateHistoryText()
        {
            if (history.Count == 0)
            {
                return "History is empty.";
            }

            return string.Join(
                Environment.NewLine,
                history.Select(static (entry, index) =>
                    (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + entry));
        }

        private ReplResult Success(string output, string? returnType, long startTimestamp)
        {
            return new ReplResult(
                Success: true,
                output,
                returnType,
                ElapsedOrOneTick(startTimestamp),
                Error: null);
        }

        private ReplResult Failure(
            string message,
            ReplErrorKind kind,
            long startTimestamp,
            int? line,
            int? column)
        {
            return new ReplResult(
                Success: false,
                message,
                ReturnType: null,
                ElapsedOrOneTick(startTimestamp),
                new ReplError(message, kind, line, column));
        }

        private ReplResult UnsupportedCommand(string message, long startTimestamp)
        {
            return Failure(
                message,
                ReplErrorKind.UnsupportedCommand,
                startTimestamp,
                line: null,
                column: null);
        }

        private TimeSpan ElapsedOrOneTick(long startTimestamp)
        {
            TimeSpan elapsed = clock.GetElapsedTime(startTimestamp);
            return elapsed <= TimeSpan.Zero ? TimeSpan.FromTicks(1) : elapsed;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        private static bool IsCriticalException(Exception exception)
        {
            return exception is OutOfMemoryException
                or StackOverflowException
                or AccessViolationException
                or AppDomainUnloadedException
                or BadImageFormatException
                or CannotUnloadAppDomainException
                or InvalidProgramException
                or ThreadAbortException;
        }
    }
}

#pragma warning restore MA0048
