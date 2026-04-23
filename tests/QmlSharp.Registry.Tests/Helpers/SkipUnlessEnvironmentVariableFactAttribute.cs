namespace QmlSharp.Registry.Tests.Helpers
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class SkipUnlessEnvironmentVariableFactAttribute : FactAttribute
    {
        public SkipUnlessEnvironmentVariableFactAttribute(string variableName, string reason)
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variableName)))
            {
                Skip = reason;
            }
        }
    }
}
