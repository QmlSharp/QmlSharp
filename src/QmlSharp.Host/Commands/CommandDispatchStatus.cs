namespace QmlSharp.Host.Commands
{
    /// <summary>Structured result status for command dispatch attempts.</summary>
    public enum CommandDispatchStatus
    {
        /// <summary>The command was accepted and scheduled for dispatch.</summary>
        Dispatched,

        /// <summary>The command was accepted and queued until instance readiness.</summary>
        Queued,

        /// <summary>The instance was not registered.</summary>
        UnknownInstance,

        /// <summary>The command identifier or name was not registered for the instance.</summary>
        UnknownCommand,

        /// <summary>The command payload was not a valid JSON argument array.</summary>
        InvalidPayload,

        /// <summary>The instance was destroyed before command dispatch.</summary>
        DestroyedInstance,

        /// <summary>The router was disposed.</summary>
        Disposed
    }
}
