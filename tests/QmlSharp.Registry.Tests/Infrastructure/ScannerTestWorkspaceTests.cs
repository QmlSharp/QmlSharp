using QmlSharp.Registry.Tests.Helpers;

namespace QmlSharp.Registry.Tests.Infrastructure
{
    public sealed class ScannerTestWorkspaceTests
    {
        [Fact]
        public void GetPath_rejects_rooted_relative_paths()
        {
            using ScannerTestWorkspace workspace = new ScannerTestWorkspace();
            string rootedPath = Path.GetFullPath("outside.txt");

            ArgumentException exception = Assert.Throws<ArgumentException>(() => workspace.GetPath(rootedPath));

            Assert.Equal("relativePath", exception.ParamName);
        }
    }
}
