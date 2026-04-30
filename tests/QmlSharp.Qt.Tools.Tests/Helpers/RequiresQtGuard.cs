namespace QmlSharp.Qt.Tools.Tests.Helpers
{
    internal static class RequiresQtGuard
    {
        public static QtAvailability CheckCurrentEnvironment(params string[] requiredToolNames)
        {
            return Check(
                Environment.GetEnvironmentVariable,
                File.Exists,
                requiredToolNames);
        }

        public static QtAvailability Check(
            Func<string, string?> getEnvironmentVariable,
            Func<string, bool> fileExists,
            params string[] requiredToolNames)
        {
            string[] requiredTools = NormalizeRequiredToolNames(requiredToolNames);
            string? qtDir = NormalizeEnvironmentPath(
                getEnvironmentVariable(QtToolsTestEnvironment.QtDirVariableName));

            if (!string.IsNullOrWhiteSpace(qtDir))
            {
                if (TryFindRequiredToolsInBinDirectory(
                    Path.Join(qtDir, "bin"),
                    fileExists,
                    requiredTools,
                    out string? toolPath))
                {
                    return new QtAvailability(true, qtDir, toolPath, string.Empty);
                }
            }

            string? path = getEnvironmentVariable(QtToolsTestEnvironment.PathVariableName);
            if (!string.IsNullOrWhiteSpace(path))
            {
                foreach (string binDirectory in path
                    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(NormalizeEnvironmentPath)
                    .Where(static value => value is not null)
                    .Select(static value => value!))
                {
                    if (TryFindRequiredToolsInBinDirectory(
                        binDirectory,
                        fileExists,
                        requiredTools,
                        out string? toolPath))
                    {
                        string? rootDir = Directory.GetParent(binDirectory)?.FullName;
                        return new QtAvailability(true, rootDir, toolPath, string.Empty);
                    }
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

        private static string[] NormalizeRequiredToolNames(string[] requiredToolNames)
        {
            string[] toolNames = requiredToolNames
                .Select(static toolName => toolName.Trim())
                .Where(static toolName => toolName.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return toolNames.Length == 0
                ? [QtToolsTestEnvironment.DefaultRequiredToolName]
                : toolNames;
        }

        private static bool TryFindRequiredToolsInBinDirectory(
            string binDirectory,
            Func<string, bool> fileExists,
            string[] requiredToolNames,
            out string? firstToolPath)
        {
            firstToolPath = null;

            foreach (string toolName in requiredToolNames)
            {
                string toolPath = Path.Join(binDirectory, GetExecutableName(toolName));
                if (!fileExists(toolPath))
                {
                    firstToolPath = null;
                    return false;
                }

                firstToolPath ??= toolPath;
            }

            return firstToolPath is not null;
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
