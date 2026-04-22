namespace QmlSharp.Registry.Tests
{
    public sealed class PlaceholderTests
    {
        [Fact]
        public void Placeholder_test_discovery_proves_the_test_project_runs()
        {
            Assert.Equal("01.00", QmlSharp.Core.Placeholder.StepId);
            Assert.Equal(QmlSharp.Core.Placeholder.StepId, QmlSharp.Registry.Placeholder.StepId);
        }
    }
}
