using QmlSharp.Host.Engine;
using QmlSharp.Host.Interop;
using QmlSharp.Host.Tests.Fixtures;

namespace QmlSharp.Host.Tests.Engine
{
    public sealed class QmlSharpEngineTests
    {
        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void ExpectedAbiVersion_DefaultFacade_ReturnsLockedVersion()
        {
            QmlSharpEngine engine = new();

            Assert.Equal(NativeHostAbi.SupportedAbiVersion, engine.ExpectedAbiVersion);
        }
    }
}
