using QmlSharp.Registry.Tests.Helpers;

namespace QmlSharp.Registry.Tests.Infrastructure
{
    [Collection(QtEnvironmentCollection.Name)]
    public sealed class SkipUnlessEnvironmentVariableAttributeTests
    {
        [Fact]
        public void SkipUnlessEnvironmentVariableFact_sets_skip_when_variable_is_missing()
        {
            const string variableName = RegistryTestEnvironment.QtDirVariableName;
            const string reason = RegistryTestEnvironment.QtSdkUnavailableReason;

            string? original = Environment.GetEnvironmentVariable(variableName);
            Environment.SetEnvironmentVariable(variableName, null);

            try
            {
                SkipUnlessEnvironmentVariableFactAttribute attribute = new SkipUnlessEnvironmentVariableFactAttribute(variableName, reason);
                Assert.Equal(reason, attribute.Skip);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variableName, original);
            }
        }

        [Fact]
        public void SkipUnlessEnvironmentVariableTheory_leaves_tests_enabled_when_variable_is_present()
        {
            const string variableName = RegistryTestEnvironment.QtDirVariableName;

            string? original = Environment.GetEnvironmentVariable(variableName);
            Environment.SetEnvironmentVariable(variableName, @"C:\Qt\6.8.0\msvc2022_64");

            try
            {
                SkipUnlessEnvironmentVariableTheoryAttribute attribute = new SkipUnlessEnvironmentVariableTheoryAttribute(variableName, "unused");
                Assert.Null(attribute.Skip);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variableName, original);
            }
        }
    }
}
