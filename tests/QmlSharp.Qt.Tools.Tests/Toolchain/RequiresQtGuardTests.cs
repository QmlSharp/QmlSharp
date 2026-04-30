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
            Assert.Contains("PATH fallback", availability.SkipReason, StringComparison.Ordinal);
            Assert.DoesNotContain("QMLSHARP_QT_DIR", availability.SkipReason, StringComparison.Ordinal);
        }

        [Fact]
        public void RequiresQtPolicy_UsesStableTraitNameAndCategoryValue()
        {
            Assert.Equal("Category", QtToolsTestEnvironment.RequiresQtTraitName);
            Assert.Equal("RequiresQt", TestCategories.RequiresQt);
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
        public void RequiresQtGuard_WhenQtDirIsMissingAndPathHasQtBin_UsesPathFallback()
        {
            string binDirectory = Path.Join(Path.GetTempPath(), "qmlsharp-qt-bin");
            string toolPath = Path.Join(binDirectory, GetExecutableName("qmlformat"));
            QtAvailability availability = RequiresQtGuard.Check(
                static name => name == QtToolsTestEnvironment.PathVariableName
                    ? Path.Join(Path.GetTempPath(), "qmlsharp-qt-bin")
                    : null,
                path => path == toolPath);

            Assert.True(availability.IsAvailable);
            Assert.Equal(Directory.GetParent(binDirectory)?.FullName, availability.QtDir);
            Assert.Equal(toolPath, availability.ToolPath);
        }

        [Fact]
        public void RequiresQtGuard_WhenRequiredToolIsMissingFromPath_FallsThroughToSkipReason()
        {
            string binDirectory = Path.Join(Path.GetTempPath(), "qmlsharp-partial-qt-bin");
            string qmlformatPath = Path.Join(binDirectory, GetExecutableName("qmlformat"));
            QtAvailability availability = RequiresQtGuard.Check(
                static name => name == QtToolsTestEnvironment.PathVariableName
                    ? Path.Join(Path.GetTempPath(), "qmlsharp-partial-qt-bin")
                    : null,
                path => path == qmlformatPath,
                "qmlformat",
                "qmllint");

            Assert.False(availability.IsAvailable);
            Assert.Equal(QtToolsTestEnvironment.QtSdkUnavailableReason, availability.SkipReason);
        }

        [Fact]
        public void RequiresQtFact_SkipsCleanlyWhenQtDirAndPathAreUnavailable()
        {
            string? originalQtDir = Environment.GetEnvironmentVariable(QtToolsTestEnvironment.QtDirVariableName);
            string? originalPath = Environment.GetEnvironmentVariable(QtToolsTestEnvironment.PathVariableName);
            Environment.SetEnvironmentVariable(QtToolsTestEnvironment.QtDirVariableName, null);
            Environment.SetEnvironmentVariable(QtToolsTestEnvironment.PathVariableName, null);

            try
            {
                RequiresQtFactAttribute attribute = new();

                Assert.Equal(QtToolsTestEnvironment.QtSdkUnavailableReason, attribute.Skip);
            }
            finally
            {
                Environment.SetEnvironmentVariable(QtToolsTestEnvironment.QtDirVariableName, originalQtDir);
                Environment.SetEnvironmentVariable(QtToolsTestEnvironment.PathVariableName, originalPath);
            }
        }

        private static string GetExecutableName(string toolName)
        {
            if (OperatingSystem.IsWindows())
            {
                return toolName + ".exe";
            }

            return toolName;
        }
    }
}
