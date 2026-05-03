using System.Collections.ObjectModel;
using QmlSharp.Host.InstanceRegistry;

namespace QmlSharp.Host.Instances
{
    /// <summary>Represents a managed ViewModel instance tracked by the host registry.</summary>
    public sealed class ManagedViewModelInstance
    {
        private readonly Lock syncRoot = new();
        private IReadOnlyDictionary<string, object?> currentState;

        internal ManagedViewModelInstance(InstanceRegistration registration, DateTimeOffset createdAt)
        {
            InstanceId = registration.InstanceId;
            ClassName = registration.ClassName;
            SchemaId = registration.SchemaId;
            CompilerSlotKey = registration.CompilerSlotKey;
            NativeHandle = registration.NativeHandle;
            RootObjectHandle = registration.RootObjectHandle;
            CreatedAt = createdAt;
            State = InstanceState.Pending;
            currentState = EmptyState();
        }

        /// <summary>UUID v4 assigned by the native QObject constructor.</summary>
        public string InstanceId { get; }

        /// <summary>ViewModel class name, for example <c>CounterViewModel</c>.</summary>
        public string ClassName { get; }

        /// <summary>Stable schema identifier associated with this instance.</summary>
        public string SchemaId { get; }

        /// <summary>Compiler-assigned key used for hot reload matching.</summary>
        public string CompilerSlotKey { get; }

        /// <summary>Opaque native object handle. The managed host never owns this pointer.</summary>
        public IntPtr NativeHandle { get; }

        /// <summary>Opaque root QML object handle. The managed host never exposes a Qt type.</summary>
        public IntPtr RootObjectHandle { get; }

        /// <summary>When the instance was created.</summary>
        public DateTimeOffset CreatedAt { get; }

        /// <summary>When the instance was destroyed or disposed.</summary>
        public DateTimeOffset? DisposedAt { get; private set; }

        /// <summary>Current ready-gate state.</summary>
        public InstanceState State { get; private set; }

        /// <summary>Number of commands currently queued for this instance.</summary>
        public int QueuedCommandCount { get; private set; }

        /// <summary>Total commands dispatched for this instance.</summary>
        public long CommandsDispatched { get; private set; }

        /// <summary>Total effects emitted for this instance.</summary>
        public long EffectsEmitted { get; private set; }

        /// <summary>Current property values for diagnostics and hot reload snapshots.</summary>
        public IReadOnlyDictionary<string, object?> CurrentState
        {
            get
            {
                lock (syncRoot)
                {
                    return currentState;
                }
            }
        }

        internal InstanceInfo ToInfo()
        {
            lock (syncRoot)
            {
                return new InstanceInfo(
                    InstanceId,
                    ClassName,
                    SchemaId,
                    CompilerSlotKey,
                    State,
                    currentState,
                    QueuedCommandCount,
                    CommandsDispatched,
                    EffectsEmitted,
                    CreatedAt,
                    DisposedAt);
            }
        }

        internal InstanceSnapshot ToSnapshot()
        {
            lock (syncRoot)
            {
                return new InstanceSnapshot(
                    InstanceId,
                    ClassName,
                    SchemaId,
                    CompilerSlotKey,
                    currentState,
                    CreatedAt,
                    DisposedAt);
            }
        }

        internal InstanceSummary ToSummary()
        {
            lock (syncRoot)
            {
                return new InstanceSummary(InstanceId, ClassName, State)
                {
                    SchemaId = SchemaId,
                    CompilerSlotKey = CompilerSlotKey,
                    QueuedCommandCount = QueuedCommandCount
                };
            }
        }

        internal bool MarkReady()
        {
            lock (syncRoot)
            {
                if (State == InstanceState.Destroyed)
                {
                    return false;
                }

                if (State == InstanceState.Active)
                {
                    return false;
                }

                State = InstanceState.Active;
                return true;
            }
        }

        internal bool MarkDestroyed(DateTimeOffset disposedAt)
        {
            lock (syncRoot)
            {
                if (State == InstanceState.Destroyed)
                {
                    return false;
                }

                State = InstanceState.Destroyed;
                DisposedAt = disposedAt;
                QueuedCommandCount = 0;
                return true;
            }
        }

        internal void UpdatePropertyState(string propertyName, object? value)
        {
            lock (syncRoot)
            {
                Dictionary<string, object?> nextState = new(currentState, StringComparer.Ordinal)
                {
                    [propertyName] = value
                };
                currentState = ToReadOnly(nextState);
            }
        }

        internal void ReplacePropertyState(IReadOnlyDictionary<string, object?> state)
        {
            lock (syncRoot)
            {
                currentState = ToReadOnly(state);
            }
        }

        internal void SetQueuedCommandCount(int count)
        {
            lock (syncRoot)
            {
                QueuedCommandCount = count;
            }
        }

        internal void IncrementQueuedCommandCount()
        {
            lock (syncRoot)
            {
                checked
                {
                    QueuedCommandCount++;
                }
            }
        }

        internal void DecrementQueuedCommandCount()
        {
            lock (syncRoot)
            {
                if (QueuedCommandCount > 0)
                {
                    QueuedCommandCount--;
                }
            }
        }

        internal void RecordCommandDispatched()
        {
            lock (syncRoot)
            {
                checked
                {
                    CommandsDispatched++;
                }
            }
        }

        internal void RecordEffectEmitted()
        {
            lock (syncRoot)
            {
                checked
                {
                    EffectsEmitted++;
                }
            }
        }

        private static IReadOnlyDictionary<string, object?> EmptyState()
        {
            return ToReadOnly(new Dictionary<string, object?>(StringComparer.Ordinal));
        }

        private static IReadOnlyDictionary<string, object?> ToReadOnly(IReadOnlyDictionary<string, object?> state)
        {
            Dictionary<string, object?> copy = new(state, StringComparer.Ordinal);
            return new ReadOnlyDictionary<string, object?>(copy);
        }
    }
}
