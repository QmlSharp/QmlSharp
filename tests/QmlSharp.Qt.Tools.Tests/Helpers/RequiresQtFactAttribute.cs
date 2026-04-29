namespace QmlSharp.Qt.Tools.Tests.Helpers
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class RequiresQtFactAttribute : FactAttribute
    {
        public RequiresQtFactAttribute()
        {
            QtAvailability availability = RequiresQtGuard.CheckCurrentEnvironment();
            if (!availability.IsAvailable)
            {
                Skip = availability.SkipReason;
            }
        }
    }
}
