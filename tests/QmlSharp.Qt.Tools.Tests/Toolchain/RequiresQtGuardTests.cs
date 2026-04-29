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
                [],
                static _ => false);

            Assert.False(availability.IsAvailable);
            Assert.Null(availability.QtDir);
            Assert.Null(availability.ToolPath);
            Assert.Contains("QT_DIR", availability.SkipReason, StringComparison.Ordinal);
            Assert.Contains("PATH", availability.SkipReason, StringComparison.Ordinal);
        }

        [Fact]
        public void RequiresQtGuard_WhenQtDirIsWhitespaceAndToolIsOnPath_ReturnsNormalizedQtDir()
        {
            string pathEntry = Path.Join("qt-root", "bin");
            QtAvailability availability = RequiresQtGuard.Check(
                static name => name == QtToolsTestEnvironment.QtDirVariableName ? "   " : null,
                [pathEntry],
                static _ => true);

            Assert.True(availability.IsAvailable);
            Assert.Null(availability.QtDir);
            Assert.NotNull(availability.ToolPath);
        }

        [Fact]
        public void RequiresQtGuard_WhenQtDirHasPaddingAndToolExists_UsesTrimmedQtDir()
        {
            const string qtRoot = "qt-root";
            QtAvailability availability = RequiresQtGuard.Check(
                static name => name == QtToolsTestEnvironment.QtDirVariableName
                    ? $"  {qtRoot}  "
                    : null,
                [],
                static _ => true);

            Assert.True(availability.IsAvailable);
            Assert.Equal(qtRoot, availability.QtDir);
            Assert.Contains(qtRoot, availability.ToolPath, StringComparison.Ordinal);
        }

        [Fact]
        public void RequiresQtFact_SkipsCleanlyWhenQtDirAndPathAreUnavailable()
        {
            string? originalQtDir = Environment.GetEnvironmentVariable(QtToolsTestEnvironment.QtDirVariableName);
            string? originalPath = Environment.GetEnvironmentVariable("PATH");
            Environment.SetEnvironmentVariable(QtToolsTestEnvironment.QtDirVariableName, null);
            Environment.SetEnvironmentVariable("PATH", string.Empty);

            try
            {
                RequiresQtFactAttribute attribute = new();

                Assert.Equal(QtToolsTestEnvironment.QtSdkUnavailableReason, attribute.Skip);
            }
            finally
            {
                Environment.SetEnvironmentVariable(QtToolsTestEnvironment.QtDirVariableName, originalQtDir);
                Environment.SetEnvironmentVariable("PATH", originalPath);
            }
        }
    }
}
