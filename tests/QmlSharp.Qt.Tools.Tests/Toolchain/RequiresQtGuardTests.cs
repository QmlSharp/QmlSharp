using QmlSharp.Qt.Tools.Tests.Helpers;

namespace QmlSharp.Qt.Tools.Tests.Toolchain
{
    [Collection(QtEnvironmentCollection.Name)]
    [Trait("Category", TestCategories.Smoke)]
    public sealed class RequiresQtGuardTests
    {
        [Fact]
        public void RequiresQtGuard_WhenQtIsUnavailable_ReturnsClearSkipReason()
        {
            QtAvailability availability = RequiresQtGuard.Check(
                static _ => null,
                static _ => false);

            Assert.False(availability.IsAvailable);
            Assert.Null(availability.QtDir);
            Assert.Null(availability.ToolPath);
            Assert.Contains("QT_DIR", availability.SkipReason, StringComparison.Ordinal);
        }

        [Fact]
        public void RequiresQtGuard_WhenQtDirIsWhitespace_ReturnsUnavailableWithNormalizedQtDir()
        {
            QtAvailability availability = RequiresQtGuard.Check(
                static name => name == QtToolsTestEnvironment.QtDirVariableName ? "   " : null,
                static _ => true);

            Assert.False(availability.IsAvailable);
            Assert.Null(availability.QtDir);
            Assert.Null(availability.ToolPath);
        }

        [Fact]
        public void RequiresQtGuard_WhenQtDirHasPaddingAndToolExists_UsesTrimmedQtDir()
        {
            const string qtRoot = "qt-root";
            QtAvailability availability = RequiresQtGuard.Check(
                static name => name == QtToolsTestEnvironment.QtDirVariableName
                    ? $"  {qtRoot}  "
                    : null,
                static _ => true);

            Assert.True(availability.IsAvailable);
            Assert.Equal(qtRoot, availability.QtDir);
            Assert.Contains(qtRoot, availability.ToolPath, StringComparison.Ordinal);
        }

        [Fact]
        public void RequiresQtFact_SkipsCleanlyWhenQtDirAndPathAreUnavailable()
        {
            string? originalQtDir = Environment.GetEnvironmentVariable(QtToolsTestEnvironment.QtDirVariableName);
            Environment.SetEnvironmentVariable(QtToolsTestEnvironment.QtDirVariableName, null);

            try
            {
                RequiresQtFactAttribute attribute = new();

                Assert.Equal(QtToolsTestEnvironment.QtSdkUnavailableReason, attribute.Skip);
            }
            finally
            {
                Environment.SetEnvironmentVariable(QtToolsTestEnvironment.QtDirVariableName, originalQtDir);
            }
        }
    }
}
