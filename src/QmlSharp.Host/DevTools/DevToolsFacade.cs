using QmlSharp.Host.InstanceRegistry;
using QmlSharp.Host.Instances;
using QmlSharp.Host.Metrics;
using ManagedInstanceRegistry = QmlSharp.Host.Instances.InstanceRegistry;

namespace QmlSharp.Host.DevTools
{
    /// <summary>Host-side inspection facade consumed by later dev-tools transport layers.</summary>
    public sealed class DevToolsFacade
    {
        private readonly ManagedInstanceRegistry registry;

        /// <summary>Initializes a new facade over the managed runtime registry.</summary>
        public DevToolsFacade(ManagedInstanceRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);
            this.registry = registry;
        }

        /// <summary>Gets detailed information for one active instance.</summary>
        public InstanceInfo? GetInstanceInfo(string instanceId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
            return registry.GetInfo(instanceId);
        }

        /// <summary>Lists all active instances as lightweight summaries.</summary>
        public IReadOnlyList<InstanceSummary> ListInstances()
        {
            return registry.GetSummaries();
        }

        /// <summary>Returns the current host runtime metrics snapshot.</summary>
        public RuntimeMetrics GetRuntimeMetrics()
        {
            return registry.GetMetrics();
        }
    }
}
