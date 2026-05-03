using QmlSharp.Host.Diagnostics;

namespace QmlSharp.Host.Commands
{
    /// <summary>Structured result for command dispatch attempts.</summary>
    public sealed record CommandDispatchResult(
        CommandDispatchStatus Status,
        string? Message = null,
        RuntimeDiagnostic? Diagnostic = null)
    {
        /// <summary>Returns true when the command was accepted for immediate or queued dispatch.</summary>
        public bool Accepted => Status is CommandDispatchStatus.Dispatched or CommandDispatchStatus.Queued;

        /// <summary>Creates a successful immediate-dispatch result.</summary>
        public static CommandDispatchResult Dispatched()
        {
            return new CommandDispatchResult(CommandDispatchStatus.Dispatched);
        }

        /// <summary>Creates a successful queued-dispatch result.</summary>
        public static CommandDispatchResult Queued()
        {
            return new CommandDispatchResult(CommandDispatchStatus.Queued);
        }
    }
}
