namespace QmlSharp.Qml.Ast.Tests.Helpers
{
    internal static class AstFixtures
    {
        public static QmlDocument MinimalDocument(string rootTypeName = "Item")
        {
            return new QmlDocument
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = rootTypeName,
                },
            };
        }

        public static QmlDocument FullSyntaxDocument()
        {
            return FullSyntaxDocumentFactory.Create();
        }
    }
}
