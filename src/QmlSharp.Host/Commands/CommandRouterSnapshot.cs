namespace QmlSharp.Host.Commands
{
    internal sealed record CommandRouterSnapshot(
        IReadOnlyList<CommandRouterSnapshot.Registration> Registrations,
        IReadOnlyList<CommandRouterSnapshot.QueuedCommand> QueuedCommands)
    {
        internal sealed record Registration(
            int CommandId,
            string CommandName,
            Func<CommandInvocation, Task> Handler);

        internal sealed record QueuedCommand(
            int CommandId,
            string CommandName,
            string ArgsJson);
    }
}
