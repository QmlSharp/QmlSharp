namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class RequiresQtFactAttribute : FactAttribute
    {
        public RequiresQtFactAttribute()
        {
            string? qtDir = Environment.GetEnvironmentVariable("QT_DIR");
            if (string.IsNullOrWhiteSpace(qtDir) || !HasRequiredQtTool(qtDir))
            {
                Skip = "Qt SDK not available (set QT_DIR to a Qt 6.11 SDK root containing bin/qmlformat).";
            }
        }

        private static bool HasRequiredQtTool(string qtDir)
        {
            string toolName = OperatingSystem.IsWindows() ? "qmlformat.exe" : "qmlformat";
            string toolPath = Path.Join(qtDir.Trim(), "bin", toolName);
            return File.Exists(toolPath);
        }
    }
}
