using QmlSharp.Qml.Ast.Tests.Helpers;

namespace QmlSharp.Qml.Ast.Tests.Infrastructure
{
    public sealed class TestHarnessSmokeTests
    {
        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public void ASTS_01_Test_project_executes_under_dotnet_test()
        {
            QmlDocument document = AstFixtures.MinimalDocument();

            Assert.Equal(NodeKind.Document, document.Kind);
        }

        [Fact]
        [Trait("Category", TestCategories.Parity)]
        public void ASTS_02_Parity_category_marker_is_available()
        {
            string category = TestCategories.Parity;

            Assert.Equal("Parity", category);
        }
    }
}
