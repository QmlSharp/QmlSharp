using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class CMakeBuilderTests
    {
        [Fact]
        public async Task ConfigureAsync_WithoutPresetFile_UsesSourceBuildDirsAndQtEnvironment()
        {
            using TempDirectory source = new("qmlsharp-cmake-source");
            using TempDirectory build = new("qmlsharp-cmake-build");
            RecordingProcessRunner processRunner = new(Success("configured", string.Empty));
            CMakeBuilder builder = new(
                new CMakeBuilderOptions
                {
                    SourceDir = source.Path,
                    QtDir = "C:/Qt/6.11.0/msvc2022_64",
                    NativeOutputDir = Path.Join(source.Path, "native"),
                    EnvironmentVariables = ImmutableDictionary<string, string>.Empty.Add("CUSTOM_FLAG", "1"),
                },
                processRunner);

            CMakeStepResult result = await builder.ConfigureAsync(build.Path, "windows-ci");

            ProcessRunRequest request = Assert.Single(processRunner.Requests);
            Assert.True(result.Success);
            Assert.Equal("cmake", request.FileName);
            Assert.Equal(Path.GetFullPath(source.Path), request.WorkingDirectory);
            Assert.Contains("-S", request.Arguments);
            Assert.Contains(Path.GetFullPath(source.Path), request.Arguments);
            Assert.Contains("-B", request.Arguments);
            Assert.Contains(Path.GetFullPath(build.Path), request.Arguments);
            Assert.Contains("-DCMAKE_BUILD_TYPE=RelWithDebInfo", request.Arguments);
            Assert.Contains(request.Arguments, argument =>
                argument.StartsWith("-DCMAKE_PREFIX_PATH=", StringComparison.Ordinal));
            Assert.Equal("1", request.EnvironmentVariables["CUSTOM_FLAG"]);
            Assert.Equal(Path.GetFullPath("C:/Qt/6.11.0/msvc2022_64"), request.EnvironmentVariables["QT_DIR"]);
        }

        [Fact]
        public async Task ConfigureAsync_WithPresetFile_UsesConfiguredPresetAndRequestedBuildDir()
        {
            using TempDirectory source = new("qmlsharp-cmake-preset-source");
            using TempDirectory build = new("qmlsharp-cmake-preset-build");
            await File.WriteAllTextAsync(Path.Join(source.Path, "CMakePresets.json"), "{}");
            RecordingProcessRunner processRunner = new(Success(string.Empty, string.Empty));
            CMakeBuilder builder = new(
                new CMakeBuilderOptions
                {
                    SourceDir = source.Path,
                },
                processRunner);

            _ = await builder.ConfigureAsync(build.Path, "windows-debug");

            ProcessRunRequest request = Assert.Single(processRunner.Requests);
            Assert.Contains("--preset", request.Arguments);
            Assert.Contains("windows-debug", request.Arguments);
            Assert.Contains("-B", request.Arguments);
            Assert.Contains(Path.GetFullPath(build.Path), request.Arguments);
        }

        [Fact]
        public async Task BuildAsync_WithPresetFile_UsesRequestedBuildDir()
        {
            using TempDirectory source = new("qmlsharp-cmake-build-preset-source");
            using TempDirectory build = new("qmlsharp-cmake-build-preset-dir");
            await File.WriteAllTextAsync(Path.Join(source.Path, "CMakePresets.json"), "{}");
            RecordingProcessRunner processRunner = new(Success(string.Empty, string.Empty));
            CMakeBuilder builder = new(
                new CMakeBuilderOptions
                {
                    SourceDir = source.Path,
                    BuildPreset = "windows-debug",
                },
                processRunner);

            _ = await builder.BuildAsync(build.Path);

            ProcessRunRequest request = Assert.Single(processRunner.Requests);
            Assert.Equal(new[] { "--build", Path.GetFullPath(build.Path) }, request.Arguments.AsEnumerable());
        }

        [Fact]
        public async Task BuildAsync_MapsExitCodeAndPreservesStdoutStderr()
        {
            using TempDirectory source = new("qmlsharp-cmake-build-source");
            using TempDirectory build = new("qmlsharp-cmake-build-dir");
            RecordingProcessRunner processRunner = new(new ProcessRunResult(
                "compiler stdout",
                "compiler stderr",
                TimeSpan.FromMilliseconds(25),
                2));
            CMakeBuilder builder = new(
                new CMakeBuilderOptions
                {
                    SourceDir = source.Path,
                },
                processRunner);

            CMakeStepResult result = await builder.BuildAsync(build.Path);

            Assert.False(result.Success);
            Assert.Equal(2, result.ExitCode);
            Assert.Equal("compiler stdout", result.Stdout);
            Assert.Equal("compiler stderr", result.Stderr);
            Assert.True(result.Duration >= TimeSpan.Zero);
        }

        [Fact]
        public void GetOutputLibraryPath_UsesConfiguredNativeOutputDirectory()
        {
            using TempDirectory source = new("qmlsharp-cmake-output-source");
            string nativeOutputDir = Path.Join(source.Path, "dist", "native");
            CMakeBuilder builder = new(new CMakeBuilderOptions
            {
                SourceDir = source.Path,
                NativeOutputDir = nativeOutputDir,
            });

            string outputPath = builder.GetOutputLibraryPath(Path.Join(source.Path, "build"));

            Assert.Equal(
                Path.Join(Path.GetFullPath(nativeOutputDir), NativeLibraryNames.GetFileName("qmlsharp_native")),
                outputPath);
        }

        [Fact]
        public void GetOutputLibraryPath_FindsKnownConfigurationOutputBeforeDefaultPath()
        {
            using TempDirectory source = new("qmlsharp-cmake-output-source");
            string buildDir = Path.Join(source.Path, "build");
            string debugOutput = Path.Join(buildDir, "Debug", NativeLibraryNames.GetFileName("qmlsharp_native"));
            string? debugDirectory = Path.GetDirectoryName(debugOutput);
            Assert.False(string.IsNullOrWhiteSpace(debugDirectory));
            _ = Directory.CreateDirectory(debugDirectory);
            File.WriteAllText(debugOutput, "native");
            CMakeBuilder builder = new(new CMakeBuilderOptions
            {
                SourceDir = source.Path,
            });

            string outputPath = builder.GetOutputLibraryPath(buildDir);

            Assert.Equal(debugOutput, outputPath);
        }

        [Fact]
        public async Task ProcessRunner_CapturesStdoutStderrExitCodeAndEnvironment()
        {
            using TempDirectory work = new("qmlsharp-process-runner");
            ProcessRunner runner = new();
            ProcessRunRequest request = CreateShellRequest(
                "echo qmlsharp-out && echo %QMLSHARP_PROCESS_TEST% && echo qmlsharp-err 1>&2 && exit /b 3",
                "printf 'qmlsharp-out\\n%s\\n' \"$QMLSHARP_PROCESS_TEST\"; printf 'qmlsharp-err\\n' >&2; exit 3",
                work.Path) with
            {
                EnvironmentVariables = ImmutableDictionary<string, string>.Empty.Add("QMLSHARP_PROCESS_TEST", "env-ok"),
            };

            ProcessRunResult result = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(3, result.ExitCode);
            Assert.Contains("qmlsharp-out", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("env-ok", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("qmlsharp-err", result.Stderr, StringComparison.Ordinal);
            Assert.True(result.Duration >= TimeSpan.Zero);
        }

        [Fact]
        public async Task ProcessRunner_TimeoutKillsProcessAndReturnsTimeoutResult()
        {
            using TempDirectory work = new("qmlsharp-process-timeout");
            ProcessRunner runner = new();
            ProcessRunRequest request = CreateShellRequest(
                "ping -n 6 127.0.0.1 >nul",
                "sleep 5",
                work.Path) with
            {
                Timeout = TimeSpan.FromMilliseconds(10),
            };

            ProcessRunResult result = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(-1, result.ExitCode);
            Assert.Contains("Process timed out", result.Stderr, StringComparison.Ordinal);
        }

        [RequiresCMakeQtFact]
        [Trait("Category", BuildTestCategories.Integration)]
        [Trait("Category", BuildTestCategories.RequiresCMake)]
        [Trait("Category", BuildTestCategories.RequiresQt)]
        public async Task RequiresCMakeRequiresQt_ConfigureAndBuildSmoke()
        {
            using TempDirectory source = new("qmlsharp-cmake-real-source");
            using TempDirectory build = new("qmlsharp-cmake-real-build");
            string nativeLibraryName = NativeLibraryNames.GetFileName("qmlsharp_native");
            string cmake = string.Join(
                "\n",
                "cmake_minimum_required(VERSION 3.21 FATAL_ERROR)",
                "project(qmlsharp_cmake_smoke LANGUAGES CXX)",
                "find_package(Qt6 REQUIRED COMPONENTS Core)",
                "add_custom_target(qmlsharp_native ALL",
                $"  COMMAND ${{CMAKE_COMMAND}} -E touch \"${{CMAKE_BINARY_DIR}}/{nativeLibraryName}\"",
                "  VERBATIM)",
                string.Empty);
            await File.WriteAllTextAsync(Path.Join(source.Path, "CMakeLists.txt"), cmake);
            string qtDir = Environment.GetEnvironmentVariable("QT_DIR")!;
            CMakeBuilder builder = new(new CMakeBuilderOptions
            {
                SourceDir = source.Path,
                NativeOutputDir = build.Path,
                QtDir = qtDir,
                Timeout = TimeSpan.FromMinutes(2),
            });

            CMakeStepResult configure = await builder.ConfigureAsync(build.Path, "default");
            CMakeStepResult buildResult = configure.Success
                ? await builder.BuildAsync(build.Path)
                : new CMakeStepResult(false, string.Empty, "configure failed", TimeSpan.Zero, -1);

            Assert.True(configure.Success, configure.Stdout + configure.Stderr);
            Assert.True(buildResult.Success, buildResult.Stdout + buildResult.Stderr);
            Assert.True(File.Exists(builder.GetOutputLibraryPath(build.Path)));
        }

        private static ProcessRunResult Success(string stdout, string stderr)
        {
            return new ProcessRunResult(stdout, stderr, TimeSpan.FromMilliseconds(1), 0);
        }

        private static ProcessRunRequest CreateShellRequest(
            string windowsCommand,
            string unixCommand,
            string workingDirectory)
        {
            return OperatingSystem.IsWindows()
                ? new ProcessRunRequest
                {
                    FileName = "cmd.exe",
                    Arguments = ImmutableArray.Create("/c", windowsCommand),
                    WorkingDirectory = workingDirectory,
                }
                : new ProcessRunRequest
                {
                    FileName = "/bin/sh",
                    Arguments = ImmutableArray.Create("-c", unixCommand),
                    WorkingDirectory = workingDirectory,
                };
        }

        private sealed class RecordingProcessRunner : IProcessRunner
        {
            private readonly Queue<ProcessRunResult> results = new();
            private readonly List<ProcessRunRequest> requests = new();

            public RecordingProcessRunner(params ProcessRunResult[] results)
            {
                foreach (ProcessRunResult result in results)
                {
                    this.results.Enqueue(result);
                }
            }

            public IReadOnlyList<ProcessRunRequest> Requests => requests;

            public Task<ProcessRunResult> RunAsync(
                ProcessRunRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                requests.Add(request);
                ProcessRunResult result = results.Count == 0
                    ? Success(string.Empty, string.Empty)
                    : results.Dequeue();
                return Task.FromResult(result);
            }
        }
    }
}
