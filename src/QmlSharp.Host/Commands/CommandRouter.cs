using System.Globalization;
using System.Text.Json;
using QmlSharp.Host.Diagnostics;
using QmlSharp.Host.Exceptions;
using QmlSharp.Host.InstanceRegistry;
using QmlSharp.Host.Instances;
using ManagedInstanceRegistry = QmlSharp.Host.Instances.InstanceRegistry;

namespace QmlSharp.Host.Commands
{
    /// <summary>Routes native command callbacks to managed ViewModel handlers.</summary>
    public sealed class CommandRouter : IDisposable
    {
        private static readonly TaskScheduler DefaultScheduler = TaskScheduler.Default;

        private readonly ManagedInstanceRegistry registry;
        private readonly Action<RuntimeDiagnostic>? diagnostics;
        private readonly Lock syncRoot = new();
        private readonly Lock dispatchSyncRoot = new();
        private readonly Dictionary<CommandKey, CommandRegistration> registrationsById = [];
        private readonly Dictionary<CommandNameKey, CommandRegistration> registrationsByName = [];
        private readonly Dictionary<string, Queue<QueuedCommand>> queuedCommandsByInstanceId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Task> dispatchChainsByInstanceId = new(StringComparer.Ordinal);
        private bool disposed;

        /// <summary>Initializes a new command router.</summary>
        public CommandRouter(ManagedInstanceRegistry registry, Action<RuntimeDiagnostic>? diagnostics = null)
        {
            ArgumentNullException.ThrowIfNull(registry);
            this.registry = registry;
            this.diagnostics = diagnostics;
        }

        /// <summary>Registers a command handler for a specific instance and command ID.</summary>
        public void RegisterCommandHandler(
            string instanceId,
            int commandId,
            string commandName,
            Action<CommandInvocation> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RegisterCommandHandler(instanceId, commandId, commandName, invocation =>
            {
                handler(invocation);
                return Task.CompletedTask;
            });
        }

        /// <summary>Registers an asynchronous command handler for a specific instance and command ID.</summary>
        public void RegisterCommandHandler(
            string instanceId,
            int commandId,
            string commandName,
            Func<CommandInvocation, Task> handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            ArgumentOutOfRangeException.ThrowIfNegative(commandId);
            ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
            ArgumentNullException.ThrowIfNull(handler);

            if (registry.FindById(instanceId) is null)
            {
                throw new InstanceNotFoundException(instanceId);
            }

            CommandRegistration registration = new(instanceId, commandId, commandName, handler);
            CommandKey idKey = new(instanceId, commandId);
            CommandNameKey nameKey = new(instanceId, commandName);

            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (registrationsById.ContainsKey(idKey) || registrationsByName.ContainsKey(nameKey))
                {
                    throw new CommandRoutingException(
                        instanceId,
                        commandId,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Command '{0}' ({1}) is already registered for instance '{2}'.",
                            commandName,
                            commandId,
                            instanceId));
                }

                registrationsById.Add(idKey, registration);
                registrationsByName.Add(nameKey, registration);
            }
        }

        /// <summary>Registers a command handler by instance and command name.</summary>
        public void RegisterCommandHandler(string instanceId, string commandName, Action<CommandInvocation> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RegisterCommandHandlerByName(instanceId, commandName, invocation =>
            {
                handler(invocation);
                return Task.CompletedTask;
            });
        }

        /// <summary>Receives a command callback from native code and schedules managed dispatch.</summary>
        public CommandDispatchResult OnCommand(string instanceId, string commandName, string argsJson)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
            ArgumentException.ThrowIfNullOrWhiteSpace(argsJson);

            if (!IsJsonArray(argsJson, out string? payloadError))
            {
                RuntimeDiagnostic diagnostic = CreateDiagnostic(
                    RuntimeDiagnosticSeverity.Error,
                    instanceId,
                    payloadError ?? "Command payload must be a JSON array.");
                Report(diagnostic);
                return new CommandDispatchResult(CommandDispatchStatus.InvalidPayload, diagnostic.Message, diagnostic);
            }

            ManagedViewModelInstance? instance = registry.FindById(instanceId);
            if (instance is null)
            {
                RuntimeDiagnostic diagnostic = CreateDiagnostic(
                    RuntimeDiagnosticSeverity.Warning,
                    instanceId,
                    string.Format(CultureInfo.InvariantCulture, "Command '{0}' targeted an unknown instance.", commandName));
                Report(diagnostic);
                return new CommandDispatchResult(CommandDispatchStatus.UnknownInstance, diagnostic.Message, diagnostic);
            }

            lock (syncRoot)
            {
                if (disposed)
                {
                    return new CommandDispatchResult(CommandDispatchStatus.Disposed, "Command router is disposed.");
                }

                if (!registrationsByName.TryGetValue(new CommandNameKey(instanceId, commandName), out CommandRegistration? registration))
                {
                    RuntimeDiagnostic diagnostic = CreateDiagnostic(
                        RuntimeDiagnosticSeverity.Warning,
                        instanceId,
                        string.Format(CultureInfo.InvariantCulture, "Command '{0}' is not registered for instance '{1}'.", commandName, instanceId));
                    Report(diagnostic);
                    return new CommandDispatchResult(CommandDispatchStatus.UnknownCommand, diagnostic.Message, diagnostic);
                }

                return DispatchOrQueue(instance, new CommandInvocation(instanceId, commandName, argsJson)
                {
                    CommandId = registration.CommandId
                }, registration);
            }
        }

        /// <summary>Receives a command callback with a numeric command ID and schedules managed dispatch.</summary>
        public CommandDispatchResult OnCommand(string instanceId, int commandId, string argsJson)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            ArgumentOutOfRangeException.ThrowIfNegative(commandId);
            ArgumentException.ThrowIfNullOrWhiteSpace(argsJson);

            if (!IsJsonArray(argsJson, out string? payloadError))
            {
                RuntimeDiagnostic diagnostic = CreateDiagnostic(
                    RuntimeDiagnosticSeverity.Error,
                    instanceId,
                    payloadError ?? "Command payload must be a JSON array.");
                Report(diagnostic);
                return new CommandDispatchResult(CommandDispatchStatus.InvalidPayload, diagnostic.Message, diagnostic);
            }

            ManagedViewModelInstance? instance = registry.FindById(instanceId);
            if (instance is null)
            {
                RuntimeDiagnostic diagnostic = CreateDiagnostic(
                    RuntimeDiagnosticSeverity.Warning,
                    instanceId,
                    string.Format(CultureInfo.InvariantCulture, "Command ID '{0}' targeted an unknown instance.", commandId));
                Report(diagnostic);
                return new CommandDispatchResult(CommandDispatchStatus.UnknownInstance, diagnostic.Message, diagnostic);
            }

            lock (syncRoot)
            {
                if (disposed)
                {
                    return new CommandDispatchResult(CommandDispatchStatus.Disposed, "Command router is disposed.");
                }

                if (!registrationsById.TryGetValue(new CommandKey(instanceId, commandId), out CommandRegistration? registration))
                {
                    RuntimeDiagnostic diagnostic = CreateDiagnostic(
                        RuntimeDiagnosticSeverity.Warning,
                        instanceId,
                        string.Format(CultureInfo.InvariantCulture, "Command ID '{0}' is not registered for instance '{1}'.", commandId, instanceId));
                    Report(diagnostic);
                    return new CommandDispatchResult(CommandDispatchStatus.UnknownCommand, diagnostic.Message, diagnostic);
                }

                return DispatchOrQueue(instance, new CommandInvocation(instanceId, registration.CommandName, argsJson)
                {
                    CommandId = commandId
                }, registration);
            }
        }

        /// <summary>Opens the ready gate and flushes queued commands exactly once.</summary>
        public bool MarkReady(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            Queue<QueuedCommand>? queuedCommands = null;
            bool transitioned = registry.MarkReady(instanceId);

            lock (syncRoot)
            {
                if (disposed)
                {
                    return false;
                }

                if (transitioned && queuedCommandsByInstanceId.Remove(instanceId, out Queue<QueuedCommand>? commands))
                {
                    queuedCommands = commands;
                    _ = registry.SetQueuedCommandCount(instanceId, 0);
                }
            }

            if (queuedCommands is null)
            {
                return transitioned;
            }

            foreach (QueuedCommand queuedCommand in queuedCommands)
            {
                ScheduleDispatch(queuedCommand.Invocation, queuedCommand.Registration);
            }

            return transitioned;
        }

        /// <summary>Clears command registrations and queued commands for one instance.</summary>
        public void ClearInstance(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                RemoveRegistrations(instanceId);
                _ = queuedCommandsByInstanceId.Remove(instanceId);
                _ = registry.SetQueuedCommandCount(instanceId, 0);
            }
        }

        /// <summary>Clears all command registrations and queued commands.</summary>
        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                foreach (string instanceId in queuedCommandsByInstanceId.Keys)
                {
                    _ = registry.SetQueuedCommandCount(instanceId, 0);
                }

                registrationsById.Clear();
                registrationsByName.Clear();
                queuedCommandsByInstanceId.Clear();
                lock (dispatchSyncRoot)
                {
                    dispatchChainsByInstanceId.Clear();
                }

                disposed = true;
            }
        }

        private CommandDispatchResult DispatchOrQueue(
            ManagedViewModelInstance instance,
            CommandInvocation invocation,
            CommandRegistration registration)
        {
            if (instance.State == InstanceState.Destroyed)
            {
                return new CommandDispatchResult(CommandDispatchStatus.DestroyedInstance, "Command targeted a destroyed instance.");
            }

            if (instance.State == InstanceState.Pending)
            {
                if (!queuedCommandsByInstanceId.TryGetValue(instance.InstanceId, out Queue<QueuedCommand>? queue))
                {
                    queue = new Queue<QueuedCommand>();
                    queuedCommandsByInstanceId.Add(instance.InstanceId, queue);
                }

                queue.Enqueue(new QueuedCommand(invocation, registration));
                _ = registry.IncrementQueuedCommandCount(instance.InstanceId);
                return CommandDispatchResult.Queued();
            }

            ScheduleDispatch(invocation, registration);
            return CommandDispatchResult.Dispatched();
        }

        private void ScheduleDispatch(CommandInvocation invocation, CommandRegistration registration)
        {
            lock (dispatchSyncRoot)
            {
                if (!dispatchChainsByInstanceId.TryGetValue(invocation.InstanceId, out Task? existingTask) || existingTask.IsCompleted)
                {
                    existingTask = Task.CompletedTask;
                }

                dispatchChainsByInstanceId[invocation.InstanceId] = existingTask.ContinueWith(
                    static (completedTask, state) =>
                    {
                        completedTask.GetAwaiter().GetResult();
                        DispatchState dispatchState = (DispatchState)state!;
                        return dispatchState.Router.InvokeHandlerAsync(dispatchState.Invocation, dispatchState.Registration);
                    },
                    new DispatchState(this, invocation, registration),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    DefaultScheduler).Unwrap();
            }
        }

        private async Task InvokeHandlerAsync(CommandInvocation invocation, CommandRegistration registration)
        {
            try
            {
                await Task.Factory.StartNew(
                    static state =>
                    {
                        DispatchState dispatchState = (DispatchState)state!;
                        return dispatchState.Registration.Handler(dispatchState.Invocation);
                    },
                    new DispatchState(this, invocation, registration),
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    DefaultScheduler).Unwrap().ConfigureAwait(false);
                _ = registry.RecordCommandDispatched(invocation.InstanceId);
            }
            catch (Exception exception) when (!IsCriticalException(exception))
            {
                RuntimeDiagnostic diagnostic = CreateDiagnostic(
                    RuntimeDiagnosticSeverity.Error,
                    invocation.InstanceId,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Command '{0}' handler failed: {1}",
                        invocation.CommandName,
                        exception.Message));
                Report(diagnostic);
            }
        }

        private void RegisterCommandHandlerByName(
            string instanceId,
            string commandName,
            Func<CommandInvocation, Task> handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
            ArgumentNullException.ThrowIfNull(handler);

            if (registry.FindById(instanceId) is null)
            {
                throw new InstanceNotFoundException(instanceId);
            }

            CommandRegistration registration = new(instanceId, 0, commandName, handler);
            CommandNameKey nameKey = new(instanceId, commandName);

            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (registrationsByName.ContainsKey(nameKey))
                {
                    throw new CommandRoutingException(
                        instanceId,
                        commandId: 0,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Command '{0}' is already registered for instance '{1}'.",
                            commandName,
                            instanceId));
                }

                registrationsByName.Add(nameKey, registration);
            }
        }

        private sealed record DispatchState(
            CommandRouter Router,
            CommandInvocation Invocation,
            CommandRegistration Registration);

        private static bool IsJsonArray(string argsJson, out string? error)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(argsJson);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    error = "Command payload must be a JSON array.";
                    return false;
                }

                error = null;
                return true;
            }
            catch (JsonException exception)
            {
                error = "Command payload is not valid JSON: " + exception.Message;
                return false;
            }
        }

        private void RemoveRegistrations(string instanceId)
        {
            foreach (CommandKey key in registrationsById.Keys
                         .Where(key => string.Equals(key.InstanceId, instanceId, StringComparison.Ordinal))
                         .ToArray())
            {
                _ = registrationsById.Remove(key);
            }

            foreach (CommandNameKey key in registrationsByName.Keys
                         .Where(key => string.Equals(key.InstanceId, instanceId, StringComparison.Ordinal))
                         .ToArray())
            {
                _ = registrationsByName.Remove(key);
            }
        }

        private RuntimeDiagnostic CreateDiagnostic(RuntimeDiagnosticSeverity severity, string instanceId, string message)
        {
            return new RuntimeDiagnostic(
                severity,
                message,
                EnginePhase: "command-routing",
                InstanceId: instanceId);
        }

        private void Report(RuntimeDiagnostic diagnostic)
        {
            diagnostics?.Invoke(diagnostic);
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        private static bool IsCriticalException(Exception exception)
        {
            return exception is OutOfMemoryException
                or StackOverflowException
                or AccessViolationException
                or AppDomainUnloadedException
                or BadImageFormatException
                or CannotUnloadAppDomainException
                or InvalidProgramException
                or ThreadAbortException;
        }

        private readonly record struct CommandKey(string InstanceId, int CommandId);

        private readonly record struct CommandNameKey(string InstanceId, string CommandName);

        private sealed record QueuedCommand(CommandInvocation Invocation, CommandRegistration Registration);
    }
}
