namespace QmlSharp.Qt.Tools.Tests.Helpers
{
    internal static class QtEnvironmentCollection
    {
        public const string Name = "QtToolsEnvironment";
    }

    [CollectionDefinition(QtEnvironmentCollection.Name, DisableParallelization = true)]
    public sealed class QtEnvironmentCollectionDefinition
    {
    }
}
