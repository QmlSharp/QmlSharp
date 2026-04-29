namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class RequiresQtFactAttribute : FactAttribute
    {
        public RequiresQtFactAttribute()
        {
            string? qtDir = Environment.GetEnvironmentVariable("QT_DIR");
            if (string.IsNullOrWhiteSpace(qtDir))
            {
                Skip = "Qt SDK not available (set QT_DIR to a Qt 6.11 SDK root).";
            }
        }
    }
}
