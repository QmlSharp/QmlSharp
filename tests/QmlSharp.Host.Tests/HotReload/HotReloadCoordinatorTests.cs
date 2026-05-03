using QmlSharp.Host.Commands;
using QmlSharp.Host.Effects;
using QmlSharp.Host.HotReload;
using QmlSharp.Host.InstanceRegistry;
using QmlSharp.Host.Instances;
using QmlSharp.Host.Metrics;
using QmlSharp.Host.StateSynchronization;
using QmlSharp.Host.Tests.StateSynchronization;
using ManagedInstanceRegistry = QmlSharp.Host.Instances.InstanceRegistry;

namespace QmlSharp.Host.Tests.HotReload
{
    public sealed class HotReloadCoordinatorTests
    {
        private static int nextNativeHandle = 30_000;

        [Fact]
        public async Task ReloadAsync_SuccessfulReload_ExecutesFourStepsInOrder()
        {
            TestContext context = CreateContext();
            ManagedViewModelInstance oldInstance = RegisterInstance(context.Registry, "CounterViewModel");
            Assert.True(context.Registry.MarkReady(oldInstance.InstanceId));
            Assert.True(context.Registry.UpdatePropertyState(oldInstance.InstanceId, "count", 42));
            ManagedViewModelInstance? newInstance = null;
            context.Interop.OnReload = () =>
            {
                Assert.True(context.Registry.Unregister(oldInstance.InstanceId));
                newInstance = RegisterInstance(context.Registry, "CounterViewModel");
            };
            List<HotReloadStep> steps = [];
            context.Coordinator.StepStarted += (_, args) => steps.Add(args.Step);

            HotReloadResult result = await context.Coordinator.ReloadAsync(new IntPtr(9), "CounterView.qml");

            Assert.True(result.Success);
            Assert.Equal(1, result.InstancesMatched);
            Assert.Equal(0, result.InstancesOrphaned);
            Assert.Equal(0, result.InstancesNew);
            Assert.Equal([HotReloadStep.Capture, HotReloadStep.Reload, HotReloadStep.Hydrate, HotReloadStep.Restore], steps);
            Assert.NotNull(newInstance);
            Assert.Equal(InstanceState.Active, newInstance.State);
            Assert.Equal(42, newInstance.CurrentState["count"]);
            Assert.Equal(["capture", "reload", "batch", "restore"], context.Interop.Calls.Select(static call => call.Kind).ToArray());
        }

        [Fact]
        public async Task ReloadAsync_NativeReloadFailure_PreservesStableManagedState()
        {
            TestContext context = CreateContext();
            ManagedViewModelInstance oldInstance = RegisterInstance(context.Registry, "CounterViewModel");
            Assert.True(context.Registry.MarkReady(oldInstance.InstanceId));
            Assert.True(context.Registry.UpdatePropertyState(oldInstance.InstanceId, "count", 7));
            context.Interop.ReloadResultCode = -5;
            context.Interop.LastError = "QML syntax error";

            HotReloadResult result = await context.Coordinator.ReloadAsync(new IntPtr(9), "Broken.qml");

            Assert.False(result.Success);
            Assert.Equal(HotReloadStep.Reload, result.FailedStep);
            Assert.Contains("QML syntax error", result.ErrorMessage, StringComparison.Ordinal);
            Assert.Same(oldInstance, context.Registry.FindById(oldInstance.InstanceId));
            Assert.Equal(7, oldInstance.CurrentState["count"]);
            Assert.DoesNotContain(context.Interop.Calls, static call => string.Equals(call.Kind, "batch", StringComparison.Ordinal));
            Assert.DoesNotContain(context.Interop.Calls, static call => string.Equals(call.Kind, "restore", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ReloadAsync_HydrationFailure_ReportsFailureWithoutRestoringSnapshot()
        {
            TestContext context = CreateContext();
            ManagedViewModelInstance oldInstance = RegisterInstance(context.Registry, "CounterViewModel");
            Assert.True(context.Registry.UpdatePropertyState(oldInstance.InstanceId, "count", 13));
            context.Interop.OnReload = () =>
            {
                Assert.True(context.Registry.Unregister(oldInstance.InstanceId));
                _ = RegisterInstance(context.Registry, "CounterViewModel");
                context.Interop.NextResultCode = -7;
                context.Interop.LastError = "Property 'count' is missing.";
            };

            HotReloadResult result = await context.Coordinator.ReloadAsync(new IntPtr(9), "CounterView.qml");

            Assert.False(result.Success);
            Assert.Equal(HotReloadStep.Hydrate, result.FailedStep);
            Assert.Contains("count", result.ErrorMessage, StringComparison.Ordinal);
            Assert.Equal(["capture", "reload", "batch"], context.Interop.Calls.Select(static call => call.Kind).ToArray());
        }

        [Fact]
        public async Task ReloadAsync_DisposedInstanceDuringReload_ReportsOrphanedSnapshot()
        {
            TestContext context = CreateContext();
            ManagedViewModelInstance oldInstance = RegisterInstance(context.Registry, "CounterViewModel");
            Assert.True(context.Registry.UpdatePropertyState(oldInstance.InstanceId, "count", 5));
            context.Interop.OnReload = () => Assert.True(context.Registry.Unregister(oldInstance.InstanceId));

            HotReloadResult result = await context.Coordinator.ReloadAsync(new IntPtr(9), "CounterView.qml");

            Assert.True(result.Success);
            Assert.Equal(0, result.InstancesMatched);
            Assert.Equal(1, result.InstancesOrphaned);
            Assert.Equal(0, result.InstancesNew);
            Assert.Equal(["capture", "reload", "restore"], context.Interop.Calls.Select(static call => call.Kind).ToArray());
        }

        [Fact]
        public async Task ReloadAsync_ClassRename_DoesNotPreserveStateAcrossCompositeKeyMismatch()
        {
            TestContext context = CreateContext();
            ManagedViewModelInstance oldInstance = RegisterInstance(context.Registry, "CounterViewModel");
            Assert.True(context.Registry.UpdatePropertyState(oldInstance.InstanceId, "count", 42));
            ManagedViewModelInstance? renamedInstance = null;
            context.Interop.OnReload = () =>
            {
                Assert.True(context.Registry.Unregister(oldInstance.InstanceId));
                renamedInstance = RegisterInstance(
                    context.Registry,
                    className: "CounterVM",
                    compilerSlotKey: "CounterView::__qmlsharp_vm0");
            };

            HotReloadResult result = await context.Coordinator.ReloadAsync(new IntPtr(9), "CounterView.qml");

            Assert.True(result.Success);
            Assert.Equal(0, result.InstancesMatched);
            Assert.Equal(1, result.InstancesOrphaned);
            Assert.Equal(1, result.InstancesNew);
            Assert.NotNull(renamedInstance);
            Assert.Empty(renamedInstance.CurrentState);
            Assert.DoesNotContain(context.Interop.Calls, static call => string.Equals(call.Kind, "batch", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ReloadAsync_SuccessAndFailure_UpdateRuntimeMetrics()
        {
            TestContext context = CreateContext();
            ManagedViewModelInstance oldInstance = RegisterInstance(context.Registry, "CounterViewModel");
            context.Interop.OnReload = () =>
            {
                Assert.True(context.Registry.Unregister(oldInstance.InstanceId));
                _ = RegisterInstance(context.Registry, "CounterViewModel");
            };
            HotReloadResult success = await context.Coordinator.ReloadAsync(new IntPtr(9), "CounterView.qml");
            context.Interop.ReloadResultCode = -5;
            context.Interop.LastError = "reload failed";

            HotReloadResult failure = await context.Coordinator.ReloadAsync(new IntPtr(9), "Broken.qml");

            RuntimeMetrics metrics = context.Registry.GetMetrics();
            Assert.True(success.Success);
            Assert.False(failure.Success);
            Assert.Equal(2, metrics.TotalHotReloads);
            Assert.Equal(1, metrics.TotalHotReloadFailures);
            Assert.True(metrics.LastHotReloadDuration > TimeSpan.Zero);
        }

        [Fact]
        public async Task ReloadAsync_PendingInstance_RestoresCommandQueueAndEffectSubscriptions()
        {
            TestContext context = CreateContext(includeRouters: true);
            ManagedViewModelInstance oldInstance = RegisterInstance(context.Registry, "CounterViewModel");
            int commandDispatchCount = 0;
            context.CommandRouter!.RegisterCommandHandler(oldInstance.InstanceId, 1, "increment", _ => commandDispatchCount++);
            context.EffectRouter!.RegisterEffect(oldInstance.InstanceId, 2, "showToast");
            CommandDispatchResult queued = context.CommandRouter.OnCommand(oldInstance.InstanceId, "increment", "[]");
            ManagedViewModelInstance? newInstance = null;
            context.Interop.OnReload = () =>
            {
                Assert.True(context.Registry.Unregister(oldInstance.InstanceId));
                newInstance = RegisterInstance(context.Registry, "CounterViewModel");
            };

            HotReloadResult result = await context.Coordinator.ReloadAsync(new IntPtr(9), "CounterView.qml");

            Assert.True(result.Success);
            Assert.Equal(CommandDispatchStatus.Queued, queued.Status);
            Assert.NotNull(newInstance);
            Assert.Equal(InstanceState.Pending, newInstance.State);
            Assert.Equal(1, context.Registry.GetInfo(newInstance.InstanceId)!.QueuedCommandCount);
            EffectDispatchResult effectResult = context.EffectRouter.Dispatch(newInstance.InstanceId, "showToast", "{}");
            Assert.Equal(EffectDispatchStatus.Dispatched, effectResult.Status);
            Assert.Equal(0, commandDispatchCount);
        }

        private static TestContext CreateContext(bool includeRouters = false)
        {
            ManagedInstanceRegistry registry = new();
            FakeNativeHostInterop interop = new();
            StateSync stateSync = new(registry, interop);
            CommandRouter? commandRouter = includeRouters ? new CommandRouter(registry) : null;
            EffectRouter? effectRouter = includeRouters ? new EffectRouter(registry, interop) : null;
            HotReloadCoordinator coordinator = new(registry, interop, stateSync, commandRouter, effectRouter);
            return new TestContext(registry, interop, stateSync, commandRouter, effectRouter, coordinator);
        }

        private static ManagedViewModelInstance RegisterInstance(
            ManagedInstanceRegistry registry,
            string className,
            string compilerSlotKey = "CounterView::__qmlsharp_vm0")
        {
            return registry.Register(new InstanceRegistration(
                Guid.NewGuid().ToString("D"),
                className,
                className + "-schema",
                compilerSlotKey,
                new IntPtr(Interlocked.Increment(ref nextNativeHandle)),
                new IntPtr(Interlocked.Increment(ref nextNativeHandle))));
        }

        private sealed record TestContext(
            ManagedInstanceRegistry Registry,
            FakeNativeHostInterop Interop,
            StateSync StateSync,
            CommandRouter? CommandRouter,
            EffectRouter? EffectRouter,
            HotReloadCoordinator Coordinator);
    }
}
