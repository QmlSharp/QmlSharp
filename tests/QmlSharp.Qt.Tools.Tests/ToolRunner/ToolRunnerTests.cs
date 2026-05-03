using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using QmlSharp.Qt.Tools.Tests.Helpers;
using ProcessToolRunner = QmlSharp.Qt.Tools.ToolRunner;

namespace QmlSharp.Qt.Tools.Tests.ToolRunner
{
    public sealed class ToolRunnerTests
    {
        [Fact]
        [Trait("Category", TestCategories.ToolRunner)]
        public async Task ToolRunner_TR001_RunSuccessfulTool_CapturesExitCodeAndOutput()
        {
            ProcessToolRunner runner = new();
            ProbeInvocation probe = CreateProbeInvocation("inspect", "alpha");

            ToolResult result = await runner.RunAsync(probe.ExecutablePath, probe.Arguments);

            Assert.Equal(0, result.ExitCode);
            Assert.True(result.Success);
            Assert.Contains("ARGC=1", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("ARG0=alpha", result.Stdout, StringComparison.Ordinal);
            Assert.Equal(string.Empty, result.Stderr);
        }

        [Fact]
        [Trait("Category", TestCategories.ToolRunner)]
        public async Task ToolRunner_TR002_MissingExecutable_ThrowsQtToolNotFoundError()
        {
            ProcessToolRunner runner = new();
            string expectedPath = Path.Join(Path.GetTempPath(), "qmlsharp-missing-tool-runner-probe.exe");

            QtToolNotFoundError error = await Assert.ThrowsAsync<QtToolNotFoundError>(
                () => runner.RunAsync(expectedPath, []));

            Assert.Equal("qmlsharp-missing-tool-runner-probe", error.ToolName);
            Assert.Equal(expectedPath, error.ExpectedPath);
        }

        [Fact]
        [Trait("Category", TestCategories.ToolRunner)]
        public async Task ToolRunner_TR003_Timeout_KillsProcessAndPreservesPartialOutput()
        {
            ProcessToolRunner runner = new();
            ProbeInvocation probe = CreateProbeInvocation("sleep", "60000");
            ToolRunnerOptions options = new() { Timeout = TimeSpan.FromSeconds(2) };

            QtToolTimeoutError error = await Assert.ThrowsAsync<QtToolTimeoutError>(
                () => runner.RunAsync(probe.ExecutablePath, probe.Arguments, options));

            Assert.Equal(Path.GetFileNameWithoutExtension(probe.ExecutablePath), error.ToolName);
            Assert.Equal(options.Timeout, error.Timeout);
            Assert.Contains("stdout-before-wait", error.PartialStdout, StringComparison.Ordinal);
            Assert.Contains("stderr-before-wait", error.PartialStderr, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.ToolRunner)]
        public async Task ToolRunner_TR004_StdinWorkingDirectoryAndEnvironment_AreApplied()
        {
            ProcessToolRunner runner = new();
            ProbeInvocation probe = CreateProbeInvocation("inspect", "--read-stdin");
            string workingDirectory = Path.Join(
                Path.GetTempPath(),
                "qmlsharp toolrunner cwd " + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(workingDirectory);
            Dictionary<string, string> environment = new(StringComparer.Ordinal)
            {
                ["TOOLRUNNER_PROBE"] = "env payload",
            };
            ToolRunnerOptions options = new()
            {
                Cwd = workingDirectory,
                Stdin = "stdin payload",
                Env = environment.ToImmutableDictionary(StringComparer.Ordinal),
            };

            try
            {
                ToolResult result = await runner.RunAsync(probe.ExecutablePath, probe.Arguments, options);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("CWD=", result.Stdout, StringComparison.Ordinal);
                Assert.Contains(Path.GetFileName(workingDirectory), result.Stdout, StringComparison.Ordinal);
                Assert.Contains("ENV_TOOLRUNNER_PROBE=env payload", result.Stdout, StringComparison.Ordinal);
                Assert.Contains("STDIN=stdin payload", result.Stdout, StringComparison.Ordinal);
            }
            finally
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }

        [Fact]
        [Trait("Category", TestCategories.ToolRunner)]
        public async Task ToolRunner_TR005_CommandString_RecordsExecutableAndArgumentsForDiagnostics()
        {
            ProcessToolRunner runner = new();
            ProbeInvocation probe = CreateProbeInvocation("inspect", "argument with spaces");

            ToolResult result = await runner.RunAsync(probe.ExecutablePath, probe.Arguments);

            Assert.Contains(probe.ExecutablePath, result.Command, StringComparison.Ordinal);
            Assert.Contains("inspect", result.Command, StringComparison.Ordinal);
            Assert.Contains("argument with spaces", result.Command, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.ToolRunner)]
        public async Task ToolRunner_TR006_DurationMs_IsTracked()
        {
            ProcessToolRunner runner = new();
            ProbeInvocation probe = CreateProbeInvocation("sleep", "300");
            Stopwatch stopwatch = Stopwatch.StartNew();

            ToolResult result = await runner.RunAsync(probe.ExecutablePath, probe.Arguments);
            stopwatch.Stop();

            Assert.Equal(0, result.ExitCode);
            Assert.True(result.DurationMs >= 250);
            Assert.True(result.DurationMs <= stopwatch.ElapsedMilliseconds + 500);
        }

        [Fact]
        [Trait("Category", TestCategories.ToolRunner)]
        public async Task ToolRunner_Cancellation_KillsProcessAndThrowsOperationCanceledException()
        {
            ProcessToolRunner runner = new();
            ProbeInvocation probe = CreateProbeInvocation("sleep", "60000");
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
            Stopwatch stopwatch = Stopwatch.StartNew();

            _ = await Assert.ThrowsAsync<OperationCanceledException>(
                () => runner.RunAsync(
                    probe.ExecutablePath,
                    probe.Arguments,
                    new ToolRunnerOptions { Timeout = TimeSpan.FromSeconds(30) },
                    cts.Token));

            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15));
        }

        [Fact]
        [Trait("Category", TestCategories.ToolRunner)]
        public async Task ToolRunner_ArgumentsWithSpacesAndShellMetacharacters_ArePassedAsOneArgument()
        {
            ProcessToolRunner runner = new();
            string suspiciousArgument = "value with spaces && echo \"should-not-run\" ; $HOME";
            ProbeInvocation probe = CreateProbeInvocation("inspect", suspiciousArgument);

            ToolResult result = await runner.RunAsync(probe.ExecutablePath, probe.Arguments);

            string[] stdoutLines = result.Stdout.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(5, stdoutLines.Length);
            Assert.StartsWith("CWD=", stdoutLines[0], StringComparison.Ordinal);
            Assert.Equal("ENV_TOOLRUNNER_PROBE=", stdoutLines[1]);
            Assert.Equal("STDIN=", stdoutLines[2]);
            Assert.Equal("ARGC=1", stdoutLines[3]);
            Assert.Equal($"ARG0={suspiciousArgument}", stdoutLines[4]);
            Assert.Equal(string.Empty, result.Stderr);
        }

        [Fact]
        [Trait("Category", TestCategories.ToolRunner)]
        public async Task ToolRunner_Timeout_KillsEntireProcessTree()
        {
            ProcessToolRunner runner = new();
            string childStartedPath = Path.Join(
                Path.GetTempPath(),
                "qmlsharp-toolrunner-child-started-" + Guid.NewGuid().ToString("N") + ".txt");
            string markerPath = Path.Join(
                Path.GetTempPath(),
                "qmlsharp-toolrunner-child-" + Guid.NewGuid().ToString("N") + ".txt");
            TimeSpan timeout = TimeSpan.FromSeconds(3);
            TimeSpan childDelay = TimeSpan.FromSeconds(4);
            ProbeInvocation probe = CreateProbeInvocation(
                "spawn-child-sleep",
                markerPath,
                ((int)childDelay.TotalMilliseconds).ToString(CultureInfo.InvariantCulture),
                childStartedPath);

            try
            {
                _ = await Assert.ThrowsAsync<QtToolTimeoutError>(() =>
                    runner.RunAsync(
                        probe.ExecutablePath,
                        probe.Arguments,
                        new ToolRunnerOptions { Timeout = timeout }));

                Assert.True(File.Exists(childStartedPath), "The probe did not start its child before timeout.");

                await Task.Delay(childDelay + TimeSpan.FromSeconds(1));

                Assert.False(File.Exists(markerPath));
            }
            finally
            {
                if (File.Exists(childStartedPath))
                {
                    File.Delete(childStartedPath);
                }

                if (File.Exists(markerPath))
                {
                    File.Delete(markerPath);
                }
            }
        }

        [Fact]
        [Trait("Category", TestCategories.ToolRunner)]
        public async Task ToolRunner_OutputCapture_IsBoundedAndMarksTruncation()
        {
            ProcessToolRunner runner = new();
            ProbeInvocation probe = CreateProbeInvocation("large-output", "4096");

            ToolResult result = await runner.RunAsync(
                probe.ExecutablePath,
                probe.Arguments,
                new ToolRunnerOptions { MaxCapturedOutputChars = 128 });

            Assert.Equal(0, result.ExitCode);
            Assert.True(result.Stdout.Length <= 128);
            Assert.True(result.Stderr.Length <= 128);
            Assert.Contains("output truncated", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("output truncated", result.Stderr, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.ToolRunner)]
        public async Task ToolRunner_OutputCapture_AtExactLimit_IsNotMarkedAsTruncated()
        {
            ProcessToolRunner runner = new();
            ProbeInvocation probe = CreateProbeInvocation("large-output", "128");

            ToolResult result = await runner.RunAsync(
                probe.ExecutablePath,
                probe.Arguments,
                new ToolRunnerOptions { MaxCapturedOutputChars = 128 });

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(new string('o', 128), result.Stdout);
            Assert.Equal(new string('e', 128), result.Stderr);
            Assert.DoesNotContain("output truncated", result.Stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("output truncated", result.Stderr, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.ToolRunner)]
        public async Task ToolRunner_InvalidOutputCaptureLimit_Throws()
        {
            ProcessToolRunner runner = new();
            ProbeInvocation probe = CreateProbeInvocation("inspect");

            ArgumentOutOfRangeException error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                runner.RunAsync(
                    probe.ExecutablePath,
                    probe.Arguments,
                    new ToolRunnerOptions { MaxCapturedOutputChars = 0 }));

            Assert.Contains("MaxCapturedOutputChars", error.Message, StringComparison.Ordinal);
        }

        private static ProbeInvocation CreateProbeInvocation(params string[] probeArguments)
        {
            string configuration = GetCurrentBuildConfiguration();
            string outputDirectory = Path.Join(
                GetRepositoryRoot(),
                "tests",
                "QmlSharp.Qt.Tools.ProcessProbe",
                "bin",
                configuration,
                "net10.0");

            string executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "QmlSharp.Qt.Tools.ProcessProbe.exe"
                : "QmlSharp.Qt.Tools.ProcessProbe";
            string executablePath = Path.Join(outputDirectory, executableName);
            if (File.Exists(executablePath))
            {
                return new ProbeInvocation(executablePath, [.. probeArguments]);
            }

            string dllPath = Path.Join(outputDirectory, "QmlSharp.Qt.Tools.ProcessProbe.dll");
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException("The ToolRunner process probe has not been built.", dllPath);
            }

            string dotnetPath = ResolveDotnetHostPath();
            return new ProbeInvocation(dotnetPath, [dllPath, .. probeArguments]);
        }

        private static string GetRepositoryRoot()
        {
            DirectoryInfo directory = new(AppContext.BaseDirectory);
            for (int depth = 0; depth < 5; depth++)
            {
                directory = directory.Parent
                    ?? throw new DirectoryNotFoundException("Could not locate repository root from test output.");
            }

            return directory.FullName;
        }

        private static string GetCurrentBuildConfiguration()
        {
            DirectoryInfo baseDirectory = new(AppContext.BaseDirectory);
            return baseDirectory.Parent?.Name
                ?? throw new DirectoryNotFoundException("Could not locate build configuration from test output.");
        }

        private static string ResolveDotnetHostPath()
        {
            string executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
            string? path = Environment.GetEnvironmentVariable("PATH");
            if (path is null)
            {
                throw new FileNotFoundException("PATH is not set, so dotnet could not be resolved.");
            }

            string? candidate = path
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(directory => Path.Join(directory, executableName))
                .Where(File.Exists)
                .FirstOrDefault();
            if (candidate is not null)
            {
                return candidate;
            }

            throw new FileNotFoundException("Could not resolve dotnet from PATH.");
        }

        private sealed record ProbeInvocation(
            string ExecutablePath,
            ImmutableArray<string> Arguments);
    }
}
