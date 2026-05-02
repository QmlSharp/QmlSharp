using QmlSharp.Compiler.Tests.Fixtures;
using QmlSharp.Qml.Ast;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Compiler.Tests.PostProcessing
{
    public sealed class FixtureSmokeTests
    {
        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void AstFixture_ProvidesPostProcessingRoot()
        {
            QmlDocument document = CompilerTestFixtures.CreateCounterAstFixture();

            Assert.Equal("Column", document.RootObject.TypeName);
            Assert.NotEmpty(document.RootObject.Members);
        }

        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void RegistryFixture_ProvidesQtQuickAndControlsTypes()
        {
            IRegistryQuery registry = RegistryFixture.CreateQtQuickAndControlsRegistry();

            Assert.NotNull(registry.FindTypeByQmlName("QtQuick", "Rectangle"));
            Assert.NotNull(registry.FindTypeByQmlName("QtQuick.Controls", "Button"));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void RegistryFixture_InheritsFromMatchesProductionQuerySemantics()
        {
            IRegistryQuery registry = RegistryFixture.CreateQtQuickAndControlsRegistry();

            Assert.True(registry.InheritsFrom("QtQuick.Rectangle", "QtQuick.Item"));
            Assert.False(registry.InheritsFrom("QtQuick.Rectangle", "QtQuick.Rectangle"));
            Assert.False(registry.InheritsFrom("QtQuick.Item", "QtQuick.Rectangle"));
        }
    }
}
