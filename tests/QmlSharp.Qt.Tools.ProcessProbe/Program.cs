using System.Globalization;

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

                default:
                    Console.Error.WriteLine($"unknown command: {command}");
                    return 65;
            }
        }

        private static async Task InspectAsync(string[] args)
        {
            string stdin = await Console.In.ReadToEndAsync().ConfigureAwait(false);
            Console.WriteLine($"CWD={Environment.CurrentDirectory}");
            Console.WriteLine($"ENV_TOOLRUNNER_PROBE={Environment.GetEnvironmentVariable("TOOLRUNNER_PROBE")}");
            Console.WriteLine($"STDIN={stdin}");
            Console.WriteLine($"ARGC={args.Length}");

            for (int index = 0; index < args.Length; index++)
            {
                Console.WriteLine($"ARG{index}={args[index]}");
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
    }
}
