namespace QmlSharp.Dsl
{
    internal static class NameConventions
    {
        public static string ToQmlPropertyName(string generatedMethodName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(generatedMethodName);
            return char.ToLowerInvariant(generatedMethodName[0]) + generatedMethodName[1..];
        }

        public static string ToQmlSignalHandlerName(string generatedMethodName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(generatedMethodName);
            if (generatedMethodName.Length <= 2 || !generatedMethodName.StartsWith("On", StringComparison.Ordinal))
            {
                return ToQmlPropertyName(generatedMethodName);
            }

            return "on" + generatedMethodName[2..];
        }
    }
}
