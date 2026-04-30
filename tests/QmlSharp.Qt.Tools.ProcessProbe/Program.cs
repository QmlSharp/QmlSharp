using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace QmlSharp.Qt.Tools.ProcessProbe
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("missing command");
                return 64;
            }

            string command = args[0];
            string[] commandArgs = args.Skip(1).ToArray();
            switch (command)
            {
                case "inspect":
                    await InspectAsync(commandArgs).ConfigureAwait(false);
                    return 0;

                case "sleep":
                    return await SleepAsync(commandArgs).ConfigureAwait(false);

                case "exit":
                    return Exit(commandArgs);

                case "spawn-child-sleep":
                    return await SpawnChildSleepAsync(commandArgs).ConfigureAwait(false);

                case "delayed-write":
                    return await DelayedWriteAsync(commandArgs).ConfigureAwait(false);

                case "large-output":
                    return LargeOutput(commandArgs);

                default:
                    Console.Error.WriteLine($"unknown command: {command}");
                    return 65;
            }
        }

        private static async Task InspectAsync(string[] args)
        {
            bool readStdin = args.Contains("--read-stdin", StringComparer.Ordinal);
            string[] inspectedArgs = args
                .Where(static arg => !string.Equals(arg, "--read-stdin", StringComparison.Ordinal))
                .ToArray();
            string stdin = readStdin
                ? await Console.In.ReadToEndAsync().ConfigureAwait(false)
                : string.Empty;
            Console.WriteLine($"CWD={Environment.CurrentDirectory}");
            Console.WriteLine($"ENV_TOOLRUNNER_PROBE={Environment.GetEnvironmentVariable("TOOLRUNNER_PROBE")}");
            Console.WriteLine($"STDIN={stdin}");
            Console.WriteLine($"ARGC={inspectedArgs.Length}");

            for (int index = 0; index < inspectedArgs.Length; index++)
            {
                Console.WriteLine($"ARG{index}={inspectedArgs[index]}");
            }
        }

        private static async Task<int> SleepAsync(string[] args)
        {
            int milliseconds = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 60_000;
            Console.WriteLine("stdout-before-wait");
            Console.Error.WriteLine("stderr-before-wait");
            await Console.Out.FlushAsync().ConfigureAwait(false);
            await Console.Error.FlushAsync().ConfigureAwait(false);
            await Task.Delay(milliseconds).ConfigureAwait(false);
            return 0;
        }

        private static int Exit(string[] args)
        {
            int exitCode = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 0;
            Console.WriteLine("stdout-exit");
            Console.Error.WriteLine("stderr-exit");
            return exitCode;
        }

        private static async Task<int> SpawnChildSleepAsync(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("spawn-child-sleep requires marker path and delay milliseconds");
                return 64;
            }

            string markerPath = args[0];
            string delayMilliseconds = args[1];
            using Process child = Process.Start(CreateChildStartInfo("delayed-write", markerPath, delayMilliseconds))
                ?? throw new InvalidOperationException("Failed to start child process.");
            Console.WriteLine("stdout-before-wait");
            Console.Error.WriteLine("stderr-before-wait");
            Console.WriteLine($"child-pid={child.Id}");
            await Console.Out.FlushAsync().ConfigureAwait(false);
            await Console.Error.FlushAsync().ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);
            return 0;
        }

        private static async Task<int> DelayedWriteAsync(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("delayed-write requires marker path and delay milliseconds");
                return 64;
            }

            string markerPath = args[0];
            int milliseconds = int.Parse(args[1], CultureInfo.InvariantCulture);
            await Task.Delay(milliseconds).ConfigureAwait(false);
            await File.WriteAllTextAsync(markerPath, "child survived").ConfigureAwait(false);
            return 0;
        }

        private static int LargeOutput(string[] args)
        {
            int length = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 100_000;
            Console.Write(new string('o', length));
            Console.Error.Write(new string('e', length));
            return 0;
        }

        private static ProcessStartInfo CreateChildStartInfo(params string[] args)
        {
            string processPath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Process path is unavailable.");
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            ProcessStartInfo startInfo = new()
            {
                FileName = processPath,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.ArgumentList.Add(assemblyPath);
            }

            foreach (string arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            return startInfo;
        }
    }
}
