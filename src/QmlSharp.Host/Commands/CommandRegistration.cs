namespace QmlSharp.Host.Commands
{
    /// <summary>Registration metadata for one managed command handler.</summary>
    public sealed record CommandRegistration(
        string InstanceId,
        int CommandId,
        string CommandName,
        Func<CommandInvocation, Task> Handler);
}
