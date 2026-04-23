namespace QmlSharp.Registry.Tests.Helpers
{
    internal static class QtEnvironmentCollection
    {
        public const string Name = "QtEnvironment";
    }

    [CollectionDefinition(QtEnvironmentCollection.Name, DisableParallelization = true)]
    public sealed class QtEnvironmentCollectionDefinition
    {
    }
}
