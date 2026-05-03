using QmlSharp.Host.InstanceRegistry;
using QmlSharp.Host.Metrics;

namespace QmlSharp.Host.Instances
{
    /// <summary>Thread-safe source of truth for managed ViewModel instance routing state.</summary>
    public sealed class InstanceRegistry : IDisposable
    {
        private readonly Lock syncRoot = new();
        private readonly DateTimeOffset startedAt;
        private readonly Dictionary<string, ManagedViewModelInstance> instancesById = new(StringComparer.Ordinal);
        private readonly Dictionary<IntPtr, string> instanceIdsByNativeHandle = [];
        private readonly Dictionary<string, List<string>> instanceIdsByClassName = new(StringComparer.Ordinal);
        private readonly Dictionary<SlotKey, List<string>> instanceIdsBySlotKey = [];
        private readonly List<ManagedViewModelInstance> destroyedInstances = [];
        private bool disposed;
        private long totalInstancesCreated;
        private long totalInstancesDestroyed;
        private long totalStateSyncs;
        private long totalCommandsDispatched;
        private long totalEffectsEmitted;
        private long totalHotReloads;
        private long totalHotReloadFailures;
        private TimeSpan lastHotReloadDuration;

        /// <summary>Initializes a new instance registry.</summary>
        public InstanceRegistry()
            : this(DateTimeOffset.UtcNow)
        {
        }

        internal InstanceRegistry(DateTimeOffset startedAt)
        {
            this.startedAt = startedAt;
        }

        /// <summary>Registers a managed instance from native lifecycle metadata.</summary>
        public ManagedViewModelInstance Register(InstanceRegistration registration)
        {
            ArgumentNullException.ThrowIfNull(registration);
            ValidateRegistration(registration);

            lock (syncRoot)
            {
                ThrowIfDisposed();
                if (instancesById.ContainsKey(registration.InstanceId))
                {
                    throw new InvalidOperationException($"Instance '{registration.InstanceId}' is already registered.");
                }

                if (registration.NativeHandle != IntPtr.Zero && instanceIdsByNativeHandle.ContainsKey(registration.NativeHandle))
                {
                    throw new InvalidOperationException($"Native handle '{registration.NativeHandle}' is already registered.");
                }

                ManagedViewModelInstance instance = new(registration, DateTimeOffset.UtcNow);
                instancesById.Add(instance.InstanceId, instance);
                AddClassIndex(instance);
                AddSlotIndex(instance);
                if (instance.NativeHandle != IntPtr.Zero)
                {
                    instanceIdsByNativeHandle.Add(instance.NativeHandle, instance.InstanceId);
                }

                checked
                {
                    totalInstancesCreated++;
                }

                return instance;
            }
        }

        /// <summary>Registers a managed instance from native lifecycle metadata.</summary>
        public ManagedViewModelInstance Register(
            string instanceId,
            string className,
            string schemaId,
            string compilerSlotKey,
            IntPtr nativeHandle,
            IntPtr rootObjectHandle)
        {
            return Register(new InstanceRegistration(
                instanceId,
                className,
                schemaId,
                compilerSlotKey,
                nativeHandle,
                rootObjectHandle));
        }

        /// <summary>Unregisters an instance by <paramref name="instanceId"/>.</summary>
        public bool Unregister(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

            lock (syncRoot)
            {
                if (disposed)
                {
                    return false;
                }

                if (!instancesById.Remove(instanceId, out ManagedViewModelInstance? instance))
                {
                    return false;
                }

                RemoveClassIndex(instance);
                RemoveSlotIndex(instance);
                if (instance.NativeHandle != IntPtr.Zero)
                {
                    _ = instanceIdsByNativeHandle.Remove(instance.NativeHandle);
                }

                if (instance.MarkDestroyed(DateTimeOffset.UtcNow))
                {
                    checked
                    {
                        totalInstancesDestroyed++;
                    }
                }

                destroyedInstances.Add(instance);
                return true;
            }
        }

        /// <summary>Finds an active instance by its UUID instance identifier.</summary>
        public ManagedViewModelInstance? FindById(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

            lock (syncRoot)
            {
                if (disposed)
                {
                    return null;
                }

                return instancesById.TryGetValue(instanceId, out ManagedViewModelInstance? instance)
                    ? instance
                    : null;
            }
        }

        /// <summary>Finds an active instance by its opaque native handle.</summary>
        public ManagedViewModelInstance? FindByNativeHandle(IntPtr nativeHandle)
        {
            if (nativeHandle == IntPtr.Zero)
            {
                return null;
            }

            lock (syncRoot)
            {
                if (disposed || !instanceIdsByNativeHandle.TryGetValue(nativeHandle, out string? instanceId))
                {
                    return null;
                }

                return instancesById.TryGetValue(instanceId, out ManagedViewModelInstance? instance)
                    ? instance
                    : null;
            }
        }

        /// <summary>Finds the oldest active instance with the given class and compiler slot key.</summary>
        public ManagedViewModelInstance? FindBySlotKey(string className, string compilerSlotKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(className);
            ArgumentException.ThrowIfNullOrWhiteSpace(compilerSlotKey);

            lock (syncRoot)
            {
                if (disposed)
                {
                    return null;
                }

                SlotKey slotKey = new(className, compilerSlotKey);
                if (!instanceIdsBySlotKey.TryGetValue(slotKey, out List<string>? ids))
                {
                    return null;
                }

                return ids
                    .Select(instanceId => instancesById.TryGetValue(instanceId, out ManagedViewModelInstance? instance)
                        ? instance
                        : null)
                    .Where(static instance => instance is not null)
                    .Cast<ManagedViewModelInstance>()
                    .FirstOrDefault();
            }
        }

        /// <summary>Finds all active instances for a ViewModel class.</summary>
        public IReadOnlyList<ManagedViewModelInstance> FindByClassName(string className)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(className);

            lock (syncRoot)
            {
                if (disposed || !instanceIdsByClassName.TryGetValue(className, out List<string>? ids))
                {
                    return [];
                }

                return ids
                    .Select(instanceId => instancesById.TryGetValue(instanceId, out ManagedViewModelInstance? instance)
                        ? instance
                        : null)
                    .Where(static instance => instance is not null)
                    .Cast<ManagedViewModelInstance>()
                    .ToArray();
            }
        }

        /// <summary>Returns all active instances.</summary>
        public IReadOnlyList<ManagedViewModelInstance> GetAll()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return [];
                }

                return [.. instancesById.Values];
            }
        }

        /// <summary>Returns active instance summaries for diagnostics.</summary>
        public IReadOnlyList<InstanceSummary> GetSummaries()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return [];
                }

                return instancesById.Values.Select(static instance => instance.ToSummary()).ToArray();
            }
        }

        /// <summary>Returns detailed information for one active instance.</summary>
        public InstanceInfo? GetInfo(string instanceId)
        {
            ManagedViewModelInstance? instance = FindById(instanceId);
            return instance?.ToInfo();
        }

        /// <summary>Transitions an instance from pending to active.</summary>
        public bool MarkReady(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

            lock (syncRoot)
            {
                if (disposed || !instancesById.TryGetValue(instanceId, out ManagedViewModelInstance? instance))
                {
                    return false;
                }

                return instance.MarkReady();
            }
        }

        /// <summary>Updates one property in the managed state snapshot.</summary>
        public bool UpdatePropertyState(string instanceId, string propertyName, object? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

            lock (syncRoot)
            {
                if (disposed || !instancesById.TryGetValue(instanceId, out ManagedViewModelInstance? instance))
                {
                    return false;
                }

                instance.UpdatePropertyState(propertyName, value);
                checked
                {
                    totalStateSyncs++;
                }

                return true;
            }
        }

        /// <summary>Updates several properties in the managed state snapshot as one sync operation.</summary>
        public bool UpdatePropertyStates(string instanceId, IReadOnlyDictionary<string, object?> state)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            ArgumentNullException.ThrowIfNull(state);

            lock (syncRoot)
            {
                if (disposed || !instancesById.TryGetValue(instanceId, out ManagedViewModelInstance? instance))
                {
                    return false;
                }

                instance.UpdatePropertyStates(state);
                checked
                {
                    totalStateSyncs++;
                }

                return true;
            }
        }

        /// <summary>Replaces the managed state snapshot for one instance.</summary>
        public bool ReplacePropertyState(string instanceId, IReadOnlyDictionary<string, object?> state)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            ArgumentNullException.ThrowIfNull(state);

            lock (syncRoot)
            {
                if (disposed || !instancesById.TryGetValue(instanceId, out ManagedViewModelInstance? instance))
                {
                    return false;
                }

                instance.ReplacePropertyState(state);
                checked
                {
                    totalStateSyncs++;
                }

                return true;
            }
        }

        /// <summary>Captures snapshots for all active instances.</summary>
        public IReadOnlyList<InstanceSnapshot> CaptureInstanceSnapshots()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return [];
                }

                return instancesById.Values.Select(static instance => instance.ToSnapshot()).ToArray();
            }
        }

        /// <summary>Sets the queued command count for an active instance.</summary>
        public bool SetQueuedCommandCount(string instanceId, int count)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            lock (syncRoot)
            {
                if (disposed || !instancesById.TryGetValue(instanceId, out ManagedViewModelInstance? instance))
                {
                    return false;
                }

                instance.SetQueuedCommandCount(count);
                return true;
            }
        }

        /// <summary>Increments the queued command count for an active instance.</summary>
        public bool IncrementQueuedCommandCount(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

            lock (syncRoot)
            {
                if (disposed || !instancesById.TryGetValue(instanceId, out ManagedViewModelInstance? instance))
                {
                    return false;
                }

                instance.IncrementQueuedCommandCount();
                return true;
            }
        }

        /// <summary>Decrements the queued command count for an active instance.</summary>
        public bool DecrementQueuedCommandCount(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

            lock (syncRoot)
            {
                if (disposed || !instancesById.TryGetValue(instanceId, out ManagedViewModelInstance? instance))
                {
                    return false;
                }

                instance.DecrementQueuedCommandCount();
                return true;
            }
        }

        /// <summary>Records one command dispatch for metrics.</summary>
        public bool RecordCommandDispatched(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

            lock (syncRoot)
            {
                if (disposed || !instancesById.TryGetValue(instanceId, out ManagedViewModelInstance? instance))
                {
                    return false;
                }

                instance.RecordCommandDispatched();
                checked
                {
                    totalCommandsDispatched++;
                }

                return true;
            }
        }

        /// <summary>Records one effect emission for metrics.</summary>
        public bool RecordEffectEmitted(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

            lock (syncRoot)
            {
                if (disposed || !instancesById.TryGetValue(instanceId, out ManagedViewModelInstance? instance))
                {
                    return false;
                }

                instance.RecordEffectEmitted();
                checked
                {
                    totalEffectsEmitted++;
                }

                return true;
            }
        }

        /// <summary>Returns a stable runtime metrics snapshot.</summary>
        public RuntimeMetrics GetMetrics()
        {
            lock (syncRoot)
            {
                int queuedCommandCount = 0;
                foreach (ManagedViewModelInstance instance in instancesById.Values)
                {
                    queuedCommandCount += instance.QueuedCommandCount;
                }

                return new RuntimeMetrics(
                    instancesById.Count,
                    totalInstancesCreated,
                    totalInstancesDestroyed,
                    totalStateSyncs,
                    totalCommandsDispatched,
                    totalEffectsEmitted,
                    DateTimeOffset.UtcNow - startedAt)
                {
                    DestroyedInstanceCount = destroyedInstances.Count,
                    QueuedCommandCount = queuedCommandCount,
                    TotalHotReloads = totalHotReloads,
                    TotalHotReloadFailures = totalHotReloadFailures,
                    LastHotReloadDuration = lastHotReloadDuration
                };
            }
        }

        /// <summary>Records a coordinated hot reload attempt for runtime diagnostics.</summary>
        public void RecordHotReload(bool success, TimeSpan duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                checked
                {
                    totalHotReloads++;
                    if (!success)
                    {
                        totalHotReloadFailures++;
                    }
                }

                lastHotReloadDuration = duration;
            }
        }

        /// <summary>Disposes the registry and marks active instances destroyed. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                DateTimeOffset disposedAt = DateTimeOffset.UtcNow;
                foreach (ManagedViewModelInstance instance in instancesById.Values.Where(instance => instance.MarkDestroyed(disposedAt)))
                {
                    checked
                    {
                        totalInstancesDestroyed++;
                    }

                    destroyedInstances.Add(instance);
                }

                instancesById.Clear();
                instanceIdsByNativeHandle.Clear();
                instanceIdsByClassName.Clear();
                instanceIdsBySlotKey.Clear();
                disposed = true;
            }
        }

        private static void ValidateRegistration(InstanceRegistration registration)
        {
            ValidateUuidV4(registration.InstanceId);
            if (string.IsNullOrWhiteSpace(registration.ClassName))
            {
                throw new ArgumentException("Class name is required.", nameof(registration));
            }

            if (string.IsNullOrWhiteSpace(registration.SchemaId))
            {
                throw new ArgumentException("Schema ID is required.", nameof(registration));
            }

            if (string.IsNullOrWhiteSpace(registration.CompilerSlotKey))
            {
                throw new ArgumentException("Compiler slot key is required.", nameof(registration));
            }
        }

        private static void ValidateUuidV4(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            if (instanceId.Length != 36 ||
                instanceId[8] != '-' ||
                instanceId[13] != '-' ||
                instanceId[18] != '-' ||
                instanceId[23] != '-' ||
                instanceId[14] != '4' ||
                !"89abAB".Contains(instanceId[19], StringComparison.Ordinal) ||
                !Guid.TryParseExact(instanceId, "D", out _))
            {
                throw new ArgumentException("Instance ID must be a UUID v4 string in canonical D format.", nameof(instanceId));
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(InstanceRegistry));
            }
        }

        private void AddClassIndex(ManagedViewModelInstance instance)
        {
            if (!instanceIdsByClassName.TryGetValue(instance.ClassName, out List<string>? ids))
            {
                ids = [];
                instanceIdsByClassName.Add(instance.ClassName, ids);
            }

            ids.Add(instance.InstanceId);
        }

        private void RemoveClassIndex(ManagedViewModelInstance instance)
        {
            if (!instanceIdsByClassName.TryGetValue(instance.ClassName, out List<string>? ids))
            {
                return;
            }

            _ = ids.Remove(instance.InstanceId);
            if (ids.Count == 0)
            {
                _ = instanceIdsByClassName.Remove(instance.ClassName);
            }
        }

        private void AddSlotIndex(ManagedViewModelInstance instance)
        {
            SlotKey slotKey = new(instance.ClassName, instance.CompilerSlotKey);
            if (!instanceIdsBySlotKey.TryGetValue(slotKey, out List<string>? ids))
            {
                ids = [];
                instanceIdsBySlotKey.Add(slotKey, ids);
            }

            ids.Add(instance.InstanceId);
        }

        private void RemoveSlotIndex(ManagedViewModelInstance instance)
        {
            SlotKey slotKey = new(instance.ClassName, instance.CompilerSlotKey);
            if (!instanceIdsBySlotKey.TryGetValue(slotKey, out List<string>? ids))
            {
                return;
            }

            _ = ids.Remove(instance.InstanceId);
            if (ids.Count == 0)
            {
                _ = instanceIdsBySlotKey.Remove(slotKey);
            }
        }

        private readonly record struct SlotKey(string ClassName, string CompilerSlotKey);
    }
}
