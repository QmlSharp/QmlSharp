using System.Diagnostics;

namespace QmlSharp.Qt.Tools
{
    /// <summary>Default process-backed implementation of <see cref="IToolRunner"/>.</summary>
    public sealed class ToolRunner : IToolRunner
    {
        /// <inheritdoc />
        public async Task<ToolResult> RunAsync(
            string toolPath,
            ImmutableArray<string> args,
            ToolRunnerOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(toolPath);

            ToolRunnerOptions effectiveOptions = options ?? new ToolRunnerOptions();
            if (effectiveOptions.Timeout <= TimeSpan.Zero && effectiveOptions.Timeout != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    effectiveOptions.Timeout,
                    "ToolRunnerOptions.Timeout must be positive or Timeout.InfiniteTimeSpan.");
            }

            if (!File.Exists(toolPath))
            {
                throw new QtToolNotFoundError(Path.GetFileNameWithoutExtension(toolPath), toolPath);
            }

            using Process process = new() { StartInfo = CreateStartInfo(toolPath, args, effectiveOptions) };
            return await RunProcessAsync(process, toolPath, args, effectiveOptions, ct).ConfigureAwait(false);
        }

        private static async Task<ToolResult> RunProcessAsync(
            Process process,
            string toolPath,
            ImmutableArray<string> args,
            ToolRunnerOptions options,
            CancellationToken ct)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start Qt tool process '{toolPath}'.");
            }

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            Task waitTask = process.WaitForExitAsync();

            try
            {
                await WriteStdinAsync(process, options, ct).ConfigureAwait(false);
                await waitTask.WaitAsync(options.Timeout, ct).ConfigureAwait(false);
                stopwatch.Stop();
                return await CreateResultAsync(process, stdoutTask, stderrTask, stopwatch, toolPath, args)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                CapturedOutput output = await KillAndCaptureAsync(process, stdoutTask, stderrTask, stopwatch)
                    .ConfigureAwait(false);
                throw new QtToolTimeoutError(
                    Path.GetFileNameWithoutExtension(toolPath),
                    options.Timeout,
                    output.Stdout,
                    output.Stderr);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _ = await KillAndCaptureAsync(process, stdoutTask, stderrTask, stopwatch).ConfigureAwait(false);
                throw new OperationCanceledException("Qt tool process was canceled and killed.", null, ct);
            }
        }

        private static ProcessStartInfo CreateStartInfo(
            string toolPath,
            ImmutableArray<string> args,
            ToolRunnerOptions options)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = toolPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = options.Stdin is not null,
                CreateNoWindow = true,
            };

            foreach (string arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            if (!string.IsNullOrWhiteSpace(options.Cwd))
            {
                startInfo.WorkingDirectory = options.Cwd;
            }

            if (options.Env is not null)
            {
                foreach (KeyValuePair<string, string> item in options.Env)
                {
                    startInfo.Environment[item.Key] = item.Value;
                }
            }

            return startInfo;
        }

        private static async Task WriteStdinAsync(
            Process process,
            ToolRunnerOptions options,
            CancellationToken ct)
        {
            if (options.Stdin is null)
            {
                return;
            }

            await process.StandardInput.WriteAsync(options.Stdin).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(ct).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        private static async Task<ToolResult> CreateResultAsync(
            Process process,
            Task<string> stdoutTask,
            Task<string> stderrTask,
            Stopwatch stopwatch,
            string toolPath,
            ImmutableArray<string> args)
        {
            return new ToolResult
            {
                ExitCode = process.ExitCode,
                Stdout = await stdoutTask.ConfigureAwait(false),
                Stderr = await stderrTask.ConfigureAwait(false),
                DurationMs = Math.Max(1, stopwatch.ElapsedMilliseconds),
                Command = FormatCommand(toolPath, args),
            };
        }

        private static async Task<CapturedOutput> KillAndCaptureAsync(
            Process process,
            Task<string> stdoutTask,
            Task<string> stderrTask,
            Stopwatch stopwatch)
        {
            KillProcessTree(process);
            await WaitForKilledProcessAsync(process).ConfigureAwait(false);
            stopwatch.Stop();
            return new CapturedOutput(
                await ReadCapturedOutputAsync(stdoutTask).ConfigureAwait(false),
                await ReadCapturedOutputAsync(stderrTask).ConfigureAwait(false));
        }

        private static async Task WaitForKilledProcessAsync(Process process)
        {
            try
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // The process may already have exited between timeout/cancellation and Kill.
            }
        }

        private static void KillProcessTree(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The process may already have exited between HasExited and Kill.
            }
        }

        private static async Task<string> ReadCapturedOutputAsync(Task<string> outputTask)
        {
            try
            {
                return await outputTask.ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                return string.Empty;
            }
        }

        private static string FormatCommand(string toolPath, ImmutableArray<string> args)
        {
            IEnumerable<string> parts = [toolPath, .. args];
            return string.Join(" ", parts.Select(QuoteCommandPart));
        }

        private static string QuoteCommandPart(string value)
        {
            if (value.Length == 0)
            {
                return "\"\"";
            }

            bool needsQuoting = value.Any(static ch => char.IsWhiteSpace(ch) || ch is '"' or '\'');
            if (!needsQuoting)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }

        private sealed record CapturedOutput(string Stdout, string Stderr);
    }
}
