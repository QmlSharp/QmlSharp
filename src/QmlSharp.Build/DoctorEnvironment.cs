using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QmlSharp.Build
{
    internal sealed class DoctorEnvironment : IDoctorEnvironment
    {
        public string CurrentDirectory => Directory.GetCurrentDirectory();

        public PlatformTarget CurrentPlatform
        {
            get
            {
                if (OperatingSystem.IsWindows())
                {
                    return PlatformTarget.WindowsX64;
                }

                if (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    return PlatformTarget.MacOsArm64;
                }

                if (OperatingSystem.IsMacOS())
                {
                    return PlatformTarget.MacOsX64;
                }

                return PlatformTarget.LinuxX64;
            }
        }

        public string? GetEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            if (!Directory.Exists(path))
            {
                return [];
            }

            return Directory.EnumerateFiles(path, searchPattern, searchOption);
        }

        public Stream OpenRead(string path)
        {
            return File.OpenRead(path);
        }

        public async Task<DoctorProcessResult> RunAsync(
            string executablePath,
            ImmutableArray<string> arguments,
            string? workingDirectory,
            CancellationToken cancellationToken)
        {
            ProcessStartInfo startInfo = new(executablePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = new()
            {
                StartInfo = startInfo,
            };
            try
            {
                if (!process.Start())
                {
                    return new DoctorProcessResult(false, -1, string.Empty, "Process could not be started.");
                }

                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                string stdout = await stdoutTask.ConfigureAwait(false);
                string stderr = await stderrTask.ConfigureAwait(false);
                return new DoctorProcessResult(true, process.ExitCode, stdout, stderr);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                KillProcess(process);
                return new DoctorProcessResult(false, -1, string.Empty, "Process timed out.");
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                return new DoctorProcessResult(false, -1, string.Empty, ex.Message);
            }
        }

        private static void KillProcess(Process process)
        {
            if (process.HasExited)
            {
                return;
            }

            process.Kill(entireProcessTree: true);
        }
    }
}
