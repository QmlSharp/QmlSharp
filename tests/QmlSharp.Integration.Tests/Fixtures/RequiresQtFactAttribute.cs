namespace QmlSharp.Integration.Tests.Fixtures
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class RequiresQtFactAttribute : FactAttribute
    {
        public RequiresQtFactAttribute()
        {
            string? qtDir = Environment.GetEnvironmentVariable("QT_DIR");
            if (string.IsNullOrWhiteSpace(qtDir))
            {
                Skip = "QT_DIR is not set.";
                return;
            }

            if (!Directory.Exists(Path.GetFullPath(qtDir)))
            {
                Skip = "QT_DIR does not exist.";
            }
        }
    }
}
