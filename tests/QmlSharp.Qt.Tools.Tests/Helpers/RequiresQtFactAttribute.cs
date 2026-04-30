namespace QmlSharp.Qt.Tools.Tests.Helpers
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class RequiresQtFactAttribute : FactAttribute
    {
        public RequiresQtFactAttribute(params string[] requiredToolNames)
        {
            QtAvailability availability = RequiresQtGuard.CheckCurrentEnvironment(requiredToolNames);
            if (!availability.IsAvailable)
            {
                Skip = availability.SkipReason;
            }
        }
    }
}
