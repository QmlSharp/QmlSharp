namespace QmlSharp.Host.Tests.StateSynchronization
{
    internal sealed record SyncCall(string Kind, string InstanceId, string? PropertyName, object? Value);
}
