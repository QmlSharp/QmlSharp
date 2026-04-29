namespace QmlSharp.Qt.Tools.Tests.Helpers
{
    internal static class RequiresQtGuard
    {
        public static QtAvailability CheckCurrentEnvironment()
        {
            return Check(
                Environment.GetEnvironmentVariable,
                File.Exists);
        }

        public static QtAvailability Check(
            Func<string, string?> getEnvironmentVariable,
            Func<string, bool> fileExists)
        {
            string? qtDir = NormalizeEnvironmentPath(
                getEnvironmentVariable(QtToolsTestEnvironment.QtDirVariableName));

            if (!string.IsNullOrWhiteSpace(qtDir))
            {
                string toolPath = Path.Join(qtDir, "bin", GetExecutableName("qmlformat"));
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

        private static string? NormalizeEnvironmentPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            return path.Trim();
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
