namespace QmlSharp.Host.Commands
{
    /// <summary>Command invocation received from the native host.</summary>
    public sealed record CommandInvocation(string InstanceId, string CommandName, string ArgsJson);
}
