namespace QmlSharp.Qml.Ast.Tests.Helpers
{
    internal static class AstFixtures
    {
        public static QmlDocument MinimalDocument(string rootTypeName = "Item")
        {
            _ = rootTypeName;
            return new QmlDocument();
        }

        public static QmlDocument FullSyntaxDocument()
        {
            return FullSyntaxDocumentFactory.Create();
        }
    }
}
