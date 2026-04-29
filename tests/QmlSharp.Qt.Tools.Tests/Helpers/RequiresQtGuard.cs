namespace QmlSharp.Qt.Tools.Tests.Helpers
{
    internal static class RequiresQtGuard
    {
        public static QtAvailability CheckCurrentEnvironment()
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            return Check(
                Environment.GetEnvironmentVariable,
                path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries),
                File.Exists);
        }

        public static QtAvailability Check(
            Func<string, string?> getEnvironmentVariable,
            IEnumerable<string> pathEntries,
            Func<string, bool> fileExists)
        {
            string? qtDir = getEnvironmentVariable(QtToolsTestEnvironment.QtDirVariableName);

            if (!string.IsNullOrWhiteSpace(qtDir))
            {
                string toolPath = Path.Join(qtDir, "bin", GetExecutableName("qmlformat"));
                if (fileExists(toolPath))
                {
                    return new QtAvailability(true, qtDir, toolPath, string.Empty);
                }
            }

            foreach (string pathEntry in pathEntries.Where(static entry => !string.IsNullOrWhiteSpace(entry)))
            {
                string toolPath = Path.Join(pathEntry, GetExecutableName("qmlformat"));
                if (fileExists(toolPath))
                {
                    return new QtAvailability(true, qtDir, toolPath, string.Empty);
                }
            }

            return new QtAvailability(
                false,
                qtDir,
                null,
                QtToolsTestEnvironment.QtSdkUnavailableReason);
        }

        private static string GetExecutableName(string toolName)
        {
            if (OperatingSystem.IsWindows())
            {
                return toolName + ".exe";
            }

            return toolName;
        }
    }
}
