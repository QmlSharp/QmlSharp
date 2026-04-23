namespace QmlSharp.Registry.Tests.Helpers
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class SkipUnlessEnvironmentVariableTheoryAttribute : TheoryAttribute
    {
        public SkipUnlessEnvironmentVariableTheoryAttribute(string variableName, string reason)
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variableName)))
            {
                Skip = reason;
            }
        }
    }
}
