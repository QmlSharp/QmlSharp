using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Fixtures
{
    public sealed class DslTestFixturesTests
    {
        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void CreateMinimalFixture_ProducesExpectedRegistryShape()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();

            Assert.NotNull(registry.FindModule("QtQuick"));
            Assert.NotNull(registry.FindTypeByQualifiedName("QQuickItem"));
            Assert.NotNull(registry.FindTypeByQmlName("QtQuick", "Rectangle"));
            Assert.Contains(registry.GetModuleTypes("QtQuick"), type => type.QmlName == "Text");
            Assert.Contains(registry.GetAllProperties("QQuickRectangle"), property => property.Property.Name == "width");
            Assert.True(registry.InheritsFrom("QQuickRectangle", "QQuickItem"));
        }

        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void FixtureFactories_ProduceNamedScenarioContexts()
        {
            IRegistryQuery p0Registry = DslTestFixtures.CreateP0Fixture();

            Assert.NotNull(DslTestFixtures.CreateQtQuickFixture().FindModule("QtQuick"));
            Assert.NotNull(DslTestFixtures.CreateQtQuickControlsFixture().FindModule("QtQuick.Controls"));
            Assert.NotNull(p0Registry.FindModule("QtQuick.Layouts"));
            Assert.Equal("QObject", p0Registry.FindTypeByQmlName("QtQml", "QtObject")?.QualifiedName);
            Assert.True(p0Registry.InheritsFrom("QQuickItem", "QObject"));
            QmlType? button = p0Registry.FindTypeByQmlName("QtQuick.Controls", "Button");
            Assert.NotNull(button);
            Assert.True(p0Registry.InheritsFrom(button.QualifiedName, "QObject"));
            Assert.NotNull(DslTestFixtures.CreateCircularInheritanceFixture().FindModule("QtQuick.Circular"));
            Assert.NotEmpty(DslTestFixtures.CreateAttachedTypesFixture().GetAttachedTypes());
            Assert.Contains("CounterViewModel", DslTestFixtures.CreateCounterViewModelSchema(), StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void GeneratedOutputTempDirectory_CreatesAndDeletesDirectory()
        {
            string path;
            using (GeneratedOutputTempDirectory directory = DslTestFixtures.CreateGeneratedOutputTempDirectory())
            {
                path = directory.Path;
                Assert.True(Directory.Exists(path));
            }

            Assert.False(Directory.Exists(path));
        }
    }
}
