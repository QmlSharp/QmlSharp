using QmlSharp.Host.Instances;
using QmlSharp.Host.Metrics;

namespace QmlSharp.Host.Tests.Instances
{
    public sealed class InstanceRegistryTests
    {
        [Fact]
        public void OnInstanceCreated_ValidRegistration_AddsEntry()
        {
            InstanceRegistry registry = new();
            InstanceRegistration registration = CreateRegistration();

            ManagedViewModelInstance instance = registry.Register(registration);

            Assert.Same(instance, registry.FindById(registration.InstanceId));
            Assert.Equal(registration.InstanceId, instance.InstanceId);
            Assert.Equal(registration.ClassName, instance.ClassName);
            Assert.Equal(registration.SchemaId, instance.SchemaId);
            Assert.Equal(registration.CompilerSlotKey, instance.CompilerSlotKey);
            Assert.Equal(registration.NativeHandle, instance.NativeHandle);
            Assert.Equal(registration.RootObjectHandle, instance.RootObjectHandle);
        }

        [Fact]
        public void OnInstanceCreated_ValidRegistration_SetsStateToPending()
        {
            InstanceRegistry registry = new();

            ManagedViewModelInstance instance = registry.Register(CreateRegistration());

            Assert.Equal(InstanceState.Pending, instance.State);
        }

        [Fact]
        public void MarkReady_KnownInstance_TransitionsToActive()
        {
            InstanceRegistry registry = new();
            ManagedViewModelInstance instance = registry.Register(CreateRegistration());

            bool changed = registry.MarkReady(instance.InstanceId);

            Assert.True(changed);
            Assert.Equal(InstanceState.Active, registry.FindById(instance.InstanceId)?.State);
        }

        [Fact]
        public void MarkReady_UnknownInstanceId_NoException()
        {
            InstanceRegistry registry = new();

            bool changed = registry.MarkReady(NewInstanceId());

            Assert.False(changed);
        }

        [Fact]
        public void OnInstanceDestroyed_KnownInstance_RemovesEntry()
        {
            InstanceRegistry registry = new();
            ManagedViewModelInstance instance = registry.Register(CreateRegistration());

            bool removed = registry.Unregister(instance.InstanceId);

            Assert.True(removed);
            Assert.Null(registry.FindById(instance.InstanceId));
            Assert.Null(registry.FindByNativeHandle(instance.NativeHandle));
            RuntimeMetrics metrics = registry.GetMetrics();
            Assert.Equal(0, metrics.ActiveInstanceCount);
            Assert.Equal(1, metrics.DestroyedInstanceCount);
            Assert.Equal(1, metrics.TotalInstancesDestroyed);
            Assert.True(instance.DisposedAt.HasValue);
            Assert.Equal(InstanceState.Destroyed, instance.State);
        }

        [Fact]
        public void FindBySlotKey_KnownClassAndSlot_ReturnsMatchingInstance()
        {
            InstanceRegistry registry = new();
            ManagedViewModelInstance instance = registry.Register(CreateRegistration(
                className: "CounterViewModel",
                compilerSlotKey: "CounterView::__qmlsharp_vm0"));

            ManagedViewModelInstance? found = registry.FindBySlotKey("CounterViewModel", "CounterView::__qmlsharp_vm0");

            Assert.Same(instance, found);
        }

        [Fact]
        public void FindBySlotKey_UnknownClassAndSlot_ReturnsNull()
        {
            InstanceRegistry registry = new();
            _ = registry.Register(CreateRegistration());

            ManagedViewModelInstance? found = registry.FindBySlotKey("MissingViewModel", "Missing::__qmlsharp_vm0");

            Assert.Null(found);
        }

        [Fact]
        public void GetAll_MultipleActiveInstances_ReturnsAllActiveInstances()
        {
            InstanceRegistry registry = new();
            ManagedViewModelInstance first = registry.Register(CreateRegistration());
            ManagedViewModelInstance second = registry.Register(CreateRegistration());
            ManagedViewModelInstance third = registry.Register(CreateRegistration());
            _ = registry.Unregister(second.InstanceId);

            IReadOnlyList<ManagedViewModelInstance> all = registry.GetAll();

            Assert.Equal(2, all.Count);
            Assert.Contains(first, all);
            Assert.DoesNotContain(second, all);
            Assert.Contains(third, all);
        }

        [Fact]
        public void FindByClassName_MultipleInstances_ReturnsMatchingInstances()
        {
            InstanceRegistry registry = new();
            ManagedViewModelInstance first = registry.Register(CreateRegistration(className: "CounterViewModel"));
            ManagedViewModelInstance second = registry.Register(CreateRegistration(className: "CounterViewModel"));
            _ = registry.Register(CreateRegistration(className: "TodoViewModel"));

            IReadOnlyList<ManagedViewModelInstance> matches = registry.FindByClassName("CounterViewModel");

            Assert.Equal(2, matches.Count);
            Assert.Contains(first, matches);
            Assert.Contains(second, matches);
        }

        [Fact]
        public void CaptureInstanceSnapshots_InstancesWithState_IncludesCurrentPropertyValues()
        {
            InstanceRegistry registry = new();
            ManagedViewModelInstance instance = registry.Register(CreateRegistration());
            _ = registry.UpdatePropertyState(instance.InstanceId, "count", 42);
            _ = registry.UpdatePropertyState(instance.InstanceId, "label", "Ready");

            IReadOnlyList<InstanceSnapshot> snapshots = registry.CaptureInstanceSnapshots();

            InstanceSnapshot snapshot = Assert.Single(snapshots);
            Assert.Equal(instance.InstanceId, snapshot.InstanceId);
            Assert.Equal(instance.SchemaId, snapshot.SchemaId);
            Assert.Equal(42, snapshot.State["count"]);
            Assert.Equal("Ready", snapshot.State["label"]);
        }

        [Fact]
        public void Register_DuplicateInstanceId_Throws()
        {
            InstanceRegistry registry = new();
            InstanceRegistration registration = CreateRegistration();
            _ = registry.Register(registration);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            {
                _ = registry.Register(registration);
            });

            Assert.Contains(registration.InstanceId, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Register_DuplicateNativeHandle_Throws()
        {
            InstanceRegistry registry = new();
            InstanceRegistration first = CreateRegistration(nativeHandle: new IntPtr(1234));
            InstanceRegistration second = CreateRegistration(nativeHandle: new IntPtr(1234));
            _ = registry.Register(first);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            {
                _ = registry.Register(second);
            });

            Assert.Contains("Native handle", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Register_NonUuidV4InstanceId_Throws()
        {
            InstanceRegistry registry = new();
            InstanceRegistration registration = CreateRegistration(instanceId: Guid.NewGuid().ToString("N"));

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = registry.Register(registration);
            });

            Assert.Equal("instanceId", exception.ParamName);
        }

        [Fact]
        public void FindByNativeHandle_KnownHandle_ReturnsMatchingInstance()
        {
            InstanceRegistry registry = new();
            InstanceRegistration registration = CreateRegistration(nativeHandle: new IntPtr(5678));
            ManagedViewModelInstance instance = registry.Register(registration);

            ManagedViewModelInstance? found = registry.FindByNativeHandle(registration.NativeHandle);

            Assert.Same(instance, found);
        }

        [Fact]
        public void FindById_UnknownInstance_ReturnsNull()
        {
            InstanceRegistry registry = new();

            ManagedViewModelInstance? found = registry.FindById(NewInstanceId());

            Assert.Null(found);
        }

        [Fact]
        public void UpdatePropertyState_UnknownInstance_ReturnsFalse()
        {
            InstanceRegistry registry = new();

            bool changed = registry.UpdatePropertyState(NewInstanceId(), "count", 1);

            Assert.False(changed);
        }

        [Fact]
        public void QueuedCommandCount_UpdatesInstanceAndMetrics()
        {
            InstanceRegistry registry = new();
            ManagedViewModelInstance instance = registry.Register(CreateRegistration());

            _ = registry.IncrementQueuedCommandCount(instance.InstanceId);
            _ = registry.IncrementQueuedCommandCount(instance.InstanceId);
            _ = registry.DecrementQueuedCommandCount(instance.InstanceId);

            InstanceInfo info = registry.GetInfo(instance.InstanceId)!;
            RuntimeMetrics metrics = registry.GetMetrics();
            Assert.Equal(1, info.QueuedCommandCount);
            Assert.Equal(1, metrics.QueuedCommandCount);
        }

        [Fact]
        public void Metrics_RegistryActivity_ReturnsStableSnapshot()
        {
            InstanceRegistry registry = new();
            ManagedViewModelInstance first = registry.Register(CreateRegistration());
            ManagedViewModelInstance second = registry.Register(CreateRegistration());
            _ = registry.MarkReady(first.InstanceId);
            _ = registry.UpdatePropertyState(first.InstanceId, "count", 1);
            _ = registry.UpdatePropertyState(first.InstanceId, "count", 2);
            _ = registry.RecordCommandDispatched(first.InstanceId);
            _ = registry.RecordEffectEmitted(first.InstanceId);
            _ = registry.SetQueuedCommandCount(first.InstanceId, 3);
            _ = registry.Unregister(second.InstanceId);

            RuntimeMetrics metrics = registry.GetMetrics();

            Assert.Equal(1, metrics.ActiveInstanceCount);
            Assert.Equal(2, metrics.TotalInstancesCreated);
            Assert.Equal(1, metrics.TotalInstancesDestroyed);
            Assert.Equal(2, metrics.TotalStateSyncs);
            Assert.Equal(1, metrics.TotalCommandsDispatched);
            Assert.Equal(1, metrics.TotalEffectsEmitted);
            Assert.Equal(1, metrics.DestroyedInstanceCount);
            Assert.Equal(3, metrics.QueuedCommandCount);
            Assert.True(metrics.Uptime >= TimeSpan.Zero);
        }

        [Fact]
        public void Dispose_CalledTwice_IsIdempotent()
        {
            InstanceRegistry registry = new();
            ManagedViewModelInstance instance = registry.Register(CreateRegistration());

            registry.Dispose();
            registry.Dispose();

            Assert.Null(registry.FindById(instance.InstanceId));
            RuntimeMetrics metrics = registry.GetMetrics();
            Assert.Equal(0, metrics.ActiveInstanceCount);
            Assert.Equal(1, metrics.TotalInstancesDestroyed);
            Assert.Equal(1, metrics.DestroyedInstanceCount);
            Assert.Equal(InstanceState.Destroyed, instance.State);
        }

        [Fact]
        public async Task ConcurrentReadWriteBehavior_RegistryOperations_RemainConsistent()
        {
            InstanceRegistry registry = new();
            IReadOnlyList<InstanceRegistration> registrations = Enumerable.Range(0, 100)
                .Select(static index => CreateRegistration(
                    className: index % 2 == 0 ? "CounterViewModel" : "TodoViewModel",
                    nativeHandle: new IntPtr(10_000 + index)))
                .ToArray();

            Task writer = Task.Run(() =>
            {
                foreach (InstanceRegistration registration in registrations)
                {
                    ManagedViewModelInstance instance = registry.Register(registration);
                    _ = registry.UpdatePropertyState(instance.InstanceId, "index", registration.NativeHandle.ToInt64());
                    _ = registry.MarkReady(instance.InstanceId);
                }
            });

            Task reader = Task.Run(() =>
            {
                for (int index = 0; index < 1_000; index++)
                {
                    _ = registry.GetAll();
                    _ = registry.CaptureInstanceSnapshots();
                    _ = registry.GetMetrics();
                }
            });

            await Task.WhenAll(writer, reader);

            Assert.Equal(100, registry.GetAll().Count);
            Assert.Equal(50, registry.FindByClassName("CounterViewModel").Count);
            Assert.Equal(100, registry.CaptureInstanceSnapshots().Count);
            Assert.Equal(100, registry.GetMetrics().ActiveInstanceCount);
        }

        [Fact]
        public void Stress_OneHundredRegisteredInstances_AllRemainRoutable()
        {
            InstanceRegistry registry = new();
            List<ManagedViewModelInstance> instances = [];

            for (int index = 0; index < 100; index++)
            {
                ManagedViewModelInstance instance = registry.Register(CreateRegistration(
                    className: "CounterViewModel",
                    compilerSlotKey: $"CounterView::__qmlsharp_vm{index}",
                    nativeHandle: new IntPtr(20_000 + index)));
                _ = registry.UpdatePropertyState(instance.InstanceId, "index", index);
                instances.Add(instance);
            }

            foreach (ManagedViewModelInstance instance in instances)
            {
                Assert.Same(instance, registry.FindById(instance.InstanceId));
                Assert.Same(instance, registry.FindByNativeHandle(instance.NativeHandle));
                Assert.Same(instance, registry.FindBySlotKey(instance.ClassName, instance.CompilerSlotKey));
            }

            Assert.Equal(100, registry.FindByClassName("CounterViewModel").Count);
            Assert.Equal(100, registry.GetMetrics().ActiveInstanceCount);
        }

        private static InstanceRegistration CreateRegistration(
            string? instanceId = null,
            string className = "CounterViewModel",
            string schemaId = "counter-schema",
            string compilerSlotKey = "CounterView::__qmlsharp_vm0",
            IntPtr nativeHandle = default,
            IntPtr rootObjectHandle = default)
        {
            return new InstanceRegistration(
                instanceId ?? NewInstanceId(),
                className,
                schemaId,
                compilerSlotKey,
                nativeHandle == default ? new IntPtr(Random.Shared.Next(1, 1_000_000)) : nativeHandle,
                rootObjectHandle == default ? new IntPtr(Random.Shared.Next(1_000_001, 2_000_000)) : rootObjectHandle);
        }

        private static string NewInstanceId()
        {
            return Guid.NewGuid().ToString("D");
        }
    }
}
