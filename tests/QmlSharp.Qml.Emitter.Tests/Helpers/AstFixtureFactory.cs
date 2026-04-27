namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    internal static class AstFixtureFactory
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
    }
}
