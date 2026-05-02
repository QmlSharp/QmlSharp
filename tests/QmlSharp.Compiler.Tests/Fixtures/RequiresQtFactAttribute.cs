namespace QmlSharp.Compiler.Tests.Fixtures
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class RequiresQtFactAttribute : FactAttribute
    {
        public RequiresQtFactAttribute(params string[] requiredToolNames)
        {
            string? qtDir = Environment.GetEnvironmentVariable("QT_DIR");
            if (string.IsNullOrWhiteSpace(qtDir))
            {
                Skip = "QT_DIR is not set.";
                return;
            }

            string binDir = Path.Join(qtDir, "bin");
            foreach (string toolName in requiredToolNames)
            {
                string executable = OperatingSystem.IsWindows() ? toolName + ".exe" : toolName;
                string toolPath = Path.Join(binDir, executable);
                if (!File.Exists(toolPath))
                {
                    Skip = $"{toolName} was not found under QT_DIR.";
                    return;
                }
            }
        }
    }
}
