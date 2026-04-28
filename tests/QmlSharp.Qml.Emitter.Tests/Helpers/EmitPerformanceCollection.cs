namespace QmlSharp.Qml.Emitter.Tests.Helpers
{
    internal static class EmitPerformanceCollection
    {
        public const string Name = "EmitPerformance";
    }

    [CollectionDefinition(EmitPerformanceCollection.Name, DisableParallelization = true)]
    public sealed class EmitPerformanceCollectionDefinition
    {
    }
}
