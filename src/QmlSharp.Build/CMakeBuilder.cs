using System.Diagnostics;
using System.Globalization;

#pragma warning disable MA0048

namespace QmlSharp.Build
{
    /// <summary>Options used by <see cref="CMakeBuilder" /> when invoking CMake.</summary>
    public sealed record CMakeBuilderOptions
    {
        /// <summary>CMake executable name or absolute path.</summary>
        public string CMakeExecutable { get; init; } = "cmake";

        /// <summary>Directory containing the generated native CMake project.</summary>
        public string SourceDir { get; init; } = Directory.GetCurrentDirectory();

        /// <summary>Directory where the native library is expected after CMake build.</summary>
        public string? NativeOutputDir { get; init; }

        /// <summary>Qt SDK directory used for CMake package discovery.</summary>
        public string? QtDir { get; init; }

        /// <summary>Optional CMake build preset to use for the build step.</summary>
        public string? BuildPreset { get; init; }

        /// <summary>Maximum time allowed for each CMake process.</summary>
        public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

        /// <summary>Additional environment variables passed to CMake.</summary>
        public ImmutableDictionary<string, string> EnvironmentVariables { get; init; } =
            ImmutableDictionary<string, string>.Empty;
    }

    /// <summary>Default CMake CLI implementation of <see cref="ICMakeBuilder" />.</summary>
    public sealed class CMakeBuilder : ICMakeBuilder
    {
        private readonly CMakeBuilderOptions options;
        private readonly IProcessRunner processRunner;

        /// <summary>Create a CMake builder with default options.</summary>
        public CMakeBuilder()
            : this(new CMakeBuilderOptions())
        {
        }

        /// <summary>Create a CMake builder with explicit options.</summary>
        public CMakeBuilder(CMakeBuilderOptions options)
            : this(options, new ProcessRunner())
        {
        }

        internal CMakeBuilder(CMakeBuilderOptions options, IProcessRunner processRunner)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(processRunner);

            this.options = options;
            this.processRunner = processRunner;
        }

        /// <inheritdoc />
        public async Task<CMakeStepResult> ConfigureAsync(
            string buildDir,
            string preset,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(buildDir);

            string sourceDir = Path.GetFullPath(options.SourceDir);
            string normalizedBuildDir = Path.GetFullPath(buildDir);
            _ = Directory.CreateDirectory(normalizedBuildDir);

            ImmutableArray<string> arguments = CreateConfigureArguments(
                sourceDir,
                normalizedBuildDir,
                preset);
            ProcessRunResult result = await processRunner.RunAsync(
                    CreateRequest(arguments, sourceDir),
                    cancellationToken)
                .ConfigureAwait(false);
            return ToCMakeStepResult(result);
        }

        /// <inheritdoc />
        public async Task<CMakeStepResult> BuildAsync(
            string buildDir,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(buildDir);

            string sourceDir = Path.GetFullPath(options.SourceDir);
            string normalizedBuildDir = Path.GetFullPath(buildDir);
            ImmutableArray<string> arguments = CreateBuildArguments(normalizedBuildDir);
            ProcessRunResult result = await processRunner.RunAsync(
                    CreateRequest(arguments, sourceDir),
                    cancellationToken)
                .ConfigureAwait(false);
            return ToCMakeStepResult(result);
        }

        /// <inheritdoc />
        public string GetOutputLibraryPath(string buildDir)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(buildDir);

            string libraryName = NativeLibraryNames.GetFileName("qmlsharp_native");
            string outputDir = string.IsNullOrWhiteSpace(options.NativeOutputDir)
                ? Path.GetFullPath(buildDir)
                : Path.GetFullPath(options.NativeOutputDir);
            string expectedPath = Path.Join(outputDir, libraryName);
            if (File.Exists(expectedPath))
            {
                return expectedPath;
            }

            foreach (string configuration in NativeLibraryNames.KnownConfigurations)
            {
                string configuredPath = Path.Join(Path.GetFullPath(buildDir), configuration, libraryName);
                if (File.Exists(configuredPath))
                {
                    return configuredPath;
                }
            }

            return expectedPath;
        }

        private ImmutableArray<string> CreateConfigureArguments(
            string sourceDir,
            string buildDir,
            string preset)
        {
            string normalizedPreset = string.IsNullOrWhiteSpace(preset) ? "default" : preset.Trim();
            if (HasCMakePresets(sourceDir))
            {
                return ImmutableArray.Create("--preset", normalizedPreset);
            }

            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
            builder.Add("-S");
            builder.Add(sourceDir);
            builder.Add("-B");
            builder.Add(buildDir);
            AddCacheVariable(builder, "CMAKE_BUILD_TYPE", InferBuildType(normalizedPreset));
            if (!string.IsNullOrWhiteSpace(options.QtDir))
            {
                AddCacheVariable(builder, "CMAKE_PREFIX_PATH", Path.GetFullPath(options.QtDir));
            }

            if (!string.IsNullOrWhiteSpace(options.NativeOutputDir))
            {
                string nativeOutputDir = Path.GetFullPath(options.NativeOutputDir);
                AddCacheVariable(builder, "CMAKE_LIBRARY_OUTPUT_DIRECTORY", nativeOutputDir);
                AddCacheVariable(builder, "CMAKE_RUNTIME_OUTPUT_DIRECTORY", nativeOutputDir);
                AddCacheVariable(builder, "CMAKE_ARCHIVE_OUTPUT_DIRECTORY", Path.Join(buildDir, "lib"));
            }

            return builder.ToImmutable();
        }

        private ImmutableArray<string> CreateBuildArguments(string buildDir)
        {
            string sourceDir = Path.GetFullPath(options.SourceDir);
            string? buildPreset = string.IsNullOrWhiteSpace(options.BuildPreset)
                ? null
                : options.BuildPreset.Trim();
            if (buildPreset is not null && HasCMakePresets(sourceDir))
            {
                return ImmutableArray.Create("--build", "--preset", buildPreset);
            }

            return ImmutableArray.Create("--build", buildDir);
        }

        private ProcessRunRequest CreateRequest(ImmutableArray<string> arguments, string workingDirectory)
        {
            ImmutableDictionary<string, string>.Builder environment =
                ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in options.EnvironmentVariables)
            {
                environment[pair.Key] = pair.Value;
            }

            if (!string.IsNullOrWhiteSpace(options.QtDir))
            {
                string qtDir = Path.GetFullPath(options.QtDir);
                environment["QT_DIR"] = qtDir;
                environment["CMAKE_PREFIX_PATH"] = qtDir;
            }

            return new ProcessRunRequest
            {
                FileName = options.CMakeExecutable,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                EnvironmentVariables = environment.ToImmutable(),
                Timeout = options.Timeout,
            };
        }

        private static void AddCacheVariable(
            ImmutableArray<string>.Builder builder,
            string name,
            string value)
        {
            builder.Add(string.Format(CultureInfo.InvariantCulture, "-D{0}={1}", name, value));
        }

        private static string InferBuildType(string preset)
        {
            if (preset.Contains("release", StringComparison.OrdinalIgnoreCase))
            {
                return "Release";
            }

            if (preset.Contains("ci", StringComparison.OrdinalIgnoreCase))
            {
                return "RelWithDebInfo";
            }

            return "Debug";
        }

        private static bool HasCMakePresets(string sourceDir)
        {
            return File.Exists(Path.Join(sourceDir, "CMakePresets.json")) ||
                File.Exists(Path.Join(sourceDir, "CMakeUserPresets.json"));
        }

        private static CMakeStepResult ToCMakeStepResult(ProcessRunResult result)
        {
            return new CMakeStepResult(
                result.ExitCode == 0,
                result.Stdout,
                result.Stderr,
                result.Duration,
                result.ExitCode);
        }
    }

    internal sealed record ProcessRunRequest
    {
        public required string FileName { get; init; }

        public required ImmutableArray<string> Arguments { get; init; }

        public required string WorkingDirectory { get; init; }

        public ImmutableDictionary<string, string> EnvironmentVariables { get; init; } =
            ImmutableDictionary<string, string>.Empty;

        public TimeSpan Timeout { get; init; }
    }

    internal sealed record ProcessRunResult(
        string Stdout,
        string Stderr,
        TimeSpan Duration,
        int ExitCode);

    internal interface IProcessRunner
    {
        Task<ProcessRunResult> RunAsync(
            ProcessRunRequest request,
            CancellationToken cancellationToken);
    }

    internal sealed class ProcessRunner : IProcessRunner
    {
        public async Task<ProcessRunResult> RunAsync(
            ProcessRunRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            Stopwatch stopwatch = Stopwatch.StartNew();
            using Process process = CreateProcess(request);
            bool started = process.Start();
            if (!started)
            {
                throw new InvalidOperationException("The process could not be started.");
            }

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            using CancellationTokenSource timeout = CreateTimeoutSource(request.Timeout);
            using CancellationTokenSource linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

            try
            {
                await process.WaitForExitAsync(linkedCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                KillProcess(process);
                string stdout = await ReadCompletedOutputAsync(stdoutTask).ConfigureAwait(false);
                string stderr = await ReadCompletedOutputAsync(stderrTask).ConfigureAwait(false);
                stopwatch.Stop();
                string timeoutMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    "Process timed out after {0}.",
                    request.Timeout);
                string combinedStderr = string.IsNullOrWhiteSpace(stderr)
                    ? timeoutMessage
                    : stderr + Environment.NewLine + timeoutMessage;
                return new ProcessRunResult(stdout, combinedStderr, stopwatch.Elapsed, -1);
            }
            catch (OperationCanceledException)
            {
                KillProcess(process);
                throw;
            }

            string finalStdout = await stdoutTask.ConfigureAwait(false);
            string finalStderr = await stderrTask.ConfigureAwait(false);
            stopwatch.Stop();
            return new ProcessRunResult(finalStdout, finalStderr, stopwatch.Elapsed, process.ExitCode);
        }

        private static Process CreateProcess(ProcessRunRequest request)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = request.FileName,
                WorkingDirectory = request.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (string argument in request.Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            foreach (KeyValuePair<string, string> pair in request.EnvironmentVariables)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }

            return new Process
            {
                StartInfo = startInfo,
            };
        }

        private static CancellationTokenSource CreateTimeoutSource(TimeSpan timeout)
        {
            return timeout > TimeSpan.Zero
                ? new CancellationTokenSource(timeout)
                : new CancellationTokenSource();
        }

        private static void KillProcess(Process process)
        {
            if (process.HasExited)
            {
                return;
            }

            process.Kill(entireProcessTree: true);
        }

        private static async Task<string> ReadCompletedOutputAsync(Task<string> outputTask)
        {
            try
            {
                return await outputTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return string.Empty;
            }
        }
    }

    internal static class NativeLibraryNames
    {
        public static ImmutableArray<string> KnownConfigurations { get; } =
            ImmutableArray.CreateRange(new[] { "Debug", "Release", "RelWithDebInfo", "MinSizeRel" });

        public static string GetFileName(string targetName)
        {
            if (OperatingSystem.IsWindows())
            {
                return targetName + ".dll";
            }

            if (OperatingSystem.IsMacOS())
            {
                return "lib" + targetName + ".dylib";
            }

            return "lib" + targetName + ".so";
        }
    }
}

#pragma warning restore MA0048
