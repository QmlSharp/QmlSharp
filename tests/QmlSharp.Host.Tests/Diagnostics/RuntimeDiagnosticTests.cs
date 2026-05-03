using QmlSharp.Host.Diagnostics;
using QmlSharp.Host.Tests.Fixtures;

namespace QmlSharp.Host.Tests.Diagnostics
{
    public sealed class RuntimeDiagnosticTests
    {
        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void RuntimeDiagnostic_SourceMapReadyPayload_PreservesRuntimeContext()
        {
            RuntimeDiagnostic diagnostic = new(
                RuntimeDiagnosticSeverity.Error,
                "Binding failed.",
                "file:///dist/qml/Main.qml",
                "dist/qml/Main.qml",
                12,
                8,
                "load",
                "mainViewModel",
                "MainView");

            Assert.Equal(RuntimeDiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal("Binding failed.", diagnostic.Message);
            Assert.Equal("file:///dist/qml/Main.qml", diagnostic.FileUrl);
            Assert.Equal("dist/qml/Main.qml", diagnostic.FilePath);
            Assert.Equal(12, diagnostic.Line);
            Assert.Equal(8, diagnostic.Column);
            Assert.Equal("load", diagnostic.EnginePhase);
            Assert.Equal("mainViewModel", diagnostic.InstanceId);
            Assert.Equal("MainView", diagnostic.ComponentName);
        }
    }
}
