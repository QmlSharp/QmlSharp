namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class RequiresQtFactAttribute : FactAttribute
    {
        public RequiresQtFactAttribute(params string[] requiredToolNames)
        {
            string[] requiredTools = NormalizeRequiredToolNames(requiredToolNames);
            if (!HasRequiredQtTools(requiredTools))
            {
                Skip = "Qt SDK not available (set QT_DIR to a Qt 6.11 SDK root containing the required tools, or put the Qt bin directory on PATH; checked QT_DIR and PATH fallback).";
            }
        }

        private static bool HasRequiredQtTools(string[] requiredToolNames)
        {
            string? qtDir = Environment.GetEnvironmentVariable("QT_DIR");
            if (!string.IsNullOrWhiteSpace(qtDir)
                && HasRequiredQtToolsInBinDirectory(Path.Join(qtDir.Trim(), "bin"), requiredToolNames))
            {
                return true;
            }

            string? path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            return path
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(binDirectory => HasRequiredQtToolsInBinDirectory(binDirectory, requiredToolNames));
        }

        private static bool HasRequiredQtToolsInBinDirectory(string binDirectory, string[] requiredToolNames)
        {
            return requiredToolNames.All(toolName => File.Exists(Path.Join(binDirectory, GetExecutableName(toolName))));
        }

        private static string[] NormalizeRequiredToolNames(string[] requiredToolNames)
        {
            string[] toolNames = requiredToolNames
                .Select(static toolName => toolName.Trim())
                .Where(static toolName => toolName.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return toolNames.Length == 0
                ? ["qmlformat"]
                : toolNames;
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
