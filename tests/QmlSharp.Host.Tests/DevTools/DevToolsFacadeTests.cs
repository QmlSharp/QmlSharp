using QmlSharp.Host.DevTools;
using QmlSharp.Host.InstanceRegistry;
using QmlSharp.Host.Instances;
using QmlSharp.Host.Metrics;
using ManagedInstanceRegistry = QmlSharp.Host.Instances.InstanceRegistry;

namespace QmlSharp.Host.Tests.DevTools
{
    public sealed class DevToolsFacadeTests
    {
        [Fact]
        public void GetInstanceInfo_KnownInstance_ReturnsRuntimeState()
        {
            ManagedInstanceRegistry registry = new();
            ManagedViewModelInstance instance = RegisterInstance(registry, "CounterViewModel");
            Assert.True(registry.MarkReady(instance.InstanceId));
            Assert.True(registry.UpdatePropertyState(instance.InstanceId, "count", 3));
            DevToolsFacade facade = new(registry);

            InstanceInfo? info = facade.GetInstanceInfo(instance.InstanceId);

            Assert.NotNull(info);
            Assert.Equal(instance.InstanceId, info.InstanceId);
            Assert.Equal("CounterViewModel", info.ClassName);
            Assert.Equal(InstanceState.Active, info.State);
            Assert.Equal(3, info.Properties["count"]);
        }

        [Fact]
        public void ListInstances_MultipleInstances_ReturnsSummaries()
        {
            ManagedInstanceRegistry registry = new();
            ManagedViewModelInstance first = RegisterInstance(registry, "CounterViewModel");
            ManagedViewModelInstance second = RegisterInstance(registry, "TodoViewModel");
            DevToolsFacade facade = new(registry);

            IReadOnlyList<InstanceSummary> summaries = facade.ListInstances();

            Assert.Equal(2, summaries.Count);
            Assert.Contains(summaries, summary => string.Equals(summary.InstanceId, first.InstanceId, StringComparison.Ordinal));
            Assert.Contains(summaries, summary => string.Equals(summary.InstanceId, second.InstanceId, StringComparison.Ordinal));
        }

        [Fact]
        public void GetRuntimeMetrics_RegistryActivity_ReturnsHostMetrics()
        {
            ManagedInstanceRegistry registry = new();
            ManagedViewModelInstance instance = RegisterInstance(registry, "CounterViewModel");
            Assert.True(registry.UpdatePropertyState(instance.InstanceId, "count", 1));
            Assert.True(registry.RecordCommandDispatched(instance.InstanceId));
            registry.RecordHotReload(success: true, TimeSpan.FromMilliseconds(12));
            DevToolsFacade facade = new(registry);

            RuntimeMetrics metrics = facade.GetRuntimeMetrics();

            Assert.Equal(1, metrics.ActiveInstanceCount);
            Assert.Equal(1, metrics.TotalStateSyncs);
            Assert.Equal(1, metrics.TotalCommandsDispatched);
            Assert.Equal(1, metrics.TotalHotReloads);
            Assert.Equal(TimeSpan.FromMilliseconds(12), metrics.LastHotReloadDuration);
        }

        private static ManagedViewModelInstance RegisterInstance(ManagedInstanceRegistry registry, string className)
        {
            return registry.Register(new InstanceRegistration(
                Guid.NewGuid().ToString("D"),
                className,
                className + "-schema",
                className + "::__qmlsharp_vm0",
                new IntPtr(Random.Shared.Next(1, 10_000)),
                new IntPtr(Random.Shared.Next(10_001, 20_000))));
        }
    }
}
