namespace QmlSharp.Registry.Tests.Helpers
{
    internal static class RegistryQueryPerformanceCollection
    {
        public const string Name = "RegistryQueryPerformance";
    }

    [CollectionDefinition(RegistryQueryPerformanceCollection.Name, DisableParallelization = true)]
    public sealed class RegistryQueryPerformanceCollectionDefinition
    {
    }
}
