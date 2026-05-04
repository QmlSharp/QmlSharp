using System.Collections.Concurrent;
using System.Globalization;
using QmlSharp.Host.Commands;
using QmlSharp.Host.Diagnostics;
using QmlSharp.Host.Instances;
using QmlSharp.Host.Tests.Fixtures;
using ManagedInstanceRegistry = QmlSharp.Host.Instances.InstanceRegistry;

namespace QmlSharp.Host.Tests.CommandRouting
{
    public sealed class CommandRouterTests
    {
        [Fact]
        public async Task OnCommand_RegisteredHandler_DispatchesInvocation()
        {
            TestContext context = CreateContext();
            _ = context.Router.MarkReady(context.Instance.InstanceId);
            TaskCompletionSource<CommandInvocation> handled = NewCompletionSource<CommandInvocation>();
            context.Router.RegisterCommandHandler(context.Instance.InstanceId, 7, "increment", handled.SetResult);

            CommandDispatchResult result = context.Router.OnCommand(context.Instance.InstanceId, "increment", "[1,\"two\",true]");

            Assert.Equal(CommandDispatchStatus.Dispatched, result.Status);
            CommandInvocation invocation = await WaitFor(handled.Task);
            Assert.Equal(context.Instance.InstanceId, invocation.InstanceId);
            Assert.Equal(7, invocation.CommandId);
            Assert.Equal("increment", invocation.CommandName);
            Assert.Equal("[1,\"two\",true]", invocation.ArgsJson);
            Assert.Equal(1, context.Registry.GetMetrics().TotalCommandsDispatched);
        }

        [Fact]
        public void OnCommand_UnknownInstance_ReturnsStructuredError()
        {
            TestContext context = CreateContext();

            CommandDispatchResult result = context.Router.OnCommand(NewInstanceId(), "increment", "[]");

            Assert.Equal(CommandDispatchStatus.UnknownInstance, result.Status);
            RuntimeDiagnostic diagnostic = Assert.Single(context.Diagnostics);
            Assert.Equal(RuntimeDiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Equal("command-routing", diagnostic.EnginePhase);
        }

        [Fact]
        public async Task RegisterCommandHandler_SpecificCommand_RoutesOnlyMatchingCommand()
        {
            TestContext context = CreateContext();
            _ = context.Router.MarkReady(context.Instance.InstanceId);
            TaskCompletionSource<CommandInvocation> increment = NewCompletionSource<CommandInvocation>();
            TaskCompletionSource<CommandInvocation> reset = NewCompletionSource<CommandInvocation>();
            context.Router.RegisterCommandHandler(context.Instance.InstanceId, 1, "increment", increment.SetResult);
            context.Router.RegisterCommandHandler(context.Instance.InstanceId, 2, "reset", reset.SetResult);

            CommandDispatchResult result = context.Router.OnCommand(context.Instance.InstanceId, 1, "[42]");

            Assert.Equal(CommandDispatchStatus.Dispatched, result.Status);
            CommandInvocation invocation = await WaitFor(increment.Task);
            Assert.Equal("increment", invocation.CommandName);
            Assert.False(reset.Task.IsCompleted);
        }

        [Fact]
        public async Task RegisterCommandHandler_ByName_AllowsMultipleCommandNamesForSameInstance()
        {
            TestContext context = CreateContext();
            _ = context.Router.MarkReady(context.Instance.InstanceId);
            TaskCompletionSource<CommandInvocation> increment = NewCompletionSource<CommandInvocation>();
            TaskCompletionSource<CommandInvocation> reset = NewCompletionSource<CommandInvocation>();
            context.Router.RegisterCommandHandler(context.Instance.InstanceId, "increment", increment.SetResult);
            context.Router.RegisterCommandHandler(context.Instance.InstanceId, "reset", reset.SetResult);

            CommandDispatchResult incrementResult = context.Router.OnCommand(context.Instance.InstanceId, "increment", "[1]");
            CommandDispatchResult resetResult = context.Router.OnCommand(context.Instance.InstanceId, "reset", "[]");

            Assert.Equal(CommandDispatchStatus.Dispatched, incrementResult.Status);
            Assert.Equal(CommandDispatchStatus.Dispatched, resetResult.Status);
            CommandInvocation incrementInvocation = await WaitFor(increment.Task);
            CommandInvocation resetInvocation = await WaitFor(reset.Task);
            Assert.Equal("increment", incrementInvocation.CommandName);
            Assert.Equal("reset", resetInvocation.CommandName);
            Assert.Equal(0, incrementInvocation.CommandId);
            Assert.Equal(0, resetInvocation.CommandId);
        }

        [Fact]
        public async Task RestoreForHotReload_NameOnlyHandlers_DoNotBecomeNumericHandlers()
        {
            TestContext context = CreateContext();
            TaskCompletionSource<CommandInvocation> numeric = NewCompletionSource<CommandInvocation>();
            TaskCompletionSource<CommandInvocation> nameOnly = NewCompletionSource<CommandInvocation>();
            context.Router.RegisterCommandHandler(context.Instance.InstanceId, 0, "increment", numeric.SetResult);
            context.Router.RegisterCommandHandler(context.Instance.InstanceId, "reset", nameOnly.SetResult);
            CommandRouterSnapshot snapshot = context.Router.CaptureForHotReload(context.Instance.InstanceId);
            string restoredInstanceId = RegisterSecondInstance(context.Registry).InstanceId;

            context.Router.RestoreForHotReload(restoredInstanceId, snapshot, restoreQueuedCommands: false);
            _ = context.Router.MarkReady(restoredInstanceId);
            CommandDispatchResult numericResult = context.Router.OnCommand(restoredInstanceId, 0, "[1]");
            CommandDispatchResult nameResult = context.Router.OnCommand(restoredInstanceId, "reset", "[]");

            Assert.Equal(CommandDispatchStatus.Dispatched, numericResult.Status);
            Assert.Equal(CommandDispatchStatus.Dispatched, nameResult.Status);
            CommandInvocation numericInvocation = await WaitFor(numeric.Task);
            CommandInvocation nameOnlyInvocation = await WaitFor(nameOnly.Task);
            Assert.Equal("increment", numericInvocation.CommandName);
            Assert.Equal("reset", nameOnlyInvocation.CommandName);
        }

        [Fact]
        public async Task OnCommand_PendingInstance_QueuesUntilReady()
        {
            TestContext context = CreateContext();
            TaskCompletionSource<CommandInvocation> handled = NewCompletionSource<CommandInvocation>();
            context.Router.RegisterCommandHandler(context.Instance.InstanceId, 1, "increment", handled.SetResult);

            CommandDispatchResult result = context.Router.OnCommand(context.Instance.InstanceId, "increment", "[]");

            Assert.Equal(CommandDispatchStatus.Queued, result.Status);
            Assert.False(handled.Task.IsCompleted);
            Assert.Equal(1, context.Registry.GetMetrics().QueuedCommandCount);

            Assert.True(context.Router.MarkReady(context.Instance.InstanceId));
            CommandInvocation invocation = await WaitFor(handled.Task);

            Assert.Equal("increment", invocation.CommandName);
            Assert.Equal(0, context.Registry.GetMetrics().QueuedCommandCount);
            Assert.Equal(1, context.Registry.GetMetrics().TotalCommandsDispatched);
        }

        [Fact]
        public async Task MarkReady_MultipleQueuedCommands_FlushesFifoExactlyOnce()
        {
            TestContext context = CreateContext();
            ConcurrentQueue<string> received = new();
            TaskCompletionSource<int> handled = NewCompletionSource<int>();
            int count = 0;
            context.Router.RegisterCommandHandler(context.Instance.InstanceId, 1, "increment", invocation =>
            {
                received.Enqueue(invocation.ArgsJson);
                if (Interlocked.Increment(ref count) == 2)
                {
                    handled.SetResult(count);
                }
            });

            Assert.Equal(CommandDispatchStatus.Queued, context.Router.OnCommand(context.Instance.InstanceId, 1, "[1]").Status);
            Assert.Equal(CommandDispatchStatus.Queued, context.Router.OnCommand(context.Instance.InstanceId, 1, "[2]").Status);

            Assert.True(context.Router.MarkReady(context.Instance.InstanceId));
            Assert.False(context.Router.MarkReady(context.Instance.InstanceId));
            _ = await WaitFor(handled.Task);

            Assert.Equal(new[] { "[1]", "[2]" }, received.ToArray());
            Assert.Equal(0, context.Registry.GetMetrics().QueuedCommandCount);
            Assert.Equal(2, context.Registry.GetMetrics().TotalCommandsDispatched);
        }

        [Fact]
        public void OnCommand_UnregisteredCommand_ReturnsStructuredError()
        {
            TestContext context = CreateContext();
            _ = context.Router.MarkReady(context.Instance.InstanceId);

            CommandDispatchResult result = context.Router.OnCommand(context.Instance.InstanceId, 99, "[]");

            Assert.Equal(CommandDispatchStatus.UnknownCommand, result.Status);
            Assert.False(result.Accepted);
            RuntimeDiagnostic diagnostic = Assert.Single(context.Diagnostics);
            Assert.Contains("99", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void OnCommand_DestroyedInstance_ReturnsStructuredUnknownInstance()
        {
            TestContext context = CreateContext();
            string instanceId = context.Instance.InstanceId;
            context.Router.RegisterCommandHandler(instanceId, 1, "increment", static _ => { });
            context.Router.ClearInstance(instanceId);
            Assert.True(context.Registry.Unregister(instanceId));

            CommandDispatchResult result = context.Router.OnCommand(instanceId, "increment", "[]");

            Assert.Equal(CommandDispatchStatus.UnknownInstance, result.Status);
        }

        [Fact]
        public async Task OnCommand_HandlerThrows_ReportsDiagnostic()
        {
            TestContext context = CreateContext();
            _ = context.Router.MarkReady(context.Instance.InstanceId);
            TaskCompletionSource<RuntimeDiagnostic> diagnosticSource = NewCompletionSource<RuntimeDiagnostic>();
            TestContext observedContext = context with
            {
                Router = new CommandRouter(context.Registry, diagnostic =>
                {
                    context.Diagnostics.Add(diagnostic);
                    diagnosticSource.SetResult(diagnostic);
                })
            };
            observedContext.Router.RegisterCommandHandler(observedContext.Instance.InstanceId, 1, "explode", static _ =>
            {
                throw new InvalidOperationException("handler failed");
            });

            CommandDispatchResult result = observedContext.Router.OnCommand(observedContext.Instance.InstanceId, 1, "[]");

            Assert.Equal(CommandDispatchStatus.Dispatched, result.Status);
            RuntimeDiagnostic diagnostic = await WaitFor(diagnosticSource.Task);
            Assert.Equal(RuntimeDiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Contains("handler failed", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void OnCommand_InvalidJson_ReturnsStructuredError()
        {
            TestContext context = CreateContext();

            CommandDispatchResult result = context.Router.OnCommand(context.Instance.InstanceId, "increment", "{not-json");

            Assert.Equal(CommandDispatchStatus.InvalidPayload, result.Status);
            RuntimeDiagnostic diagnostic = Assert.Single(context.Diagnostics);
            Assert.Equal(RuntimeDiagnosticSeverity.Error, diagnostic.Severity);
        }

        [Fact]
        public void Dispose_ClearsRegistrationsAndQueuedCommands()
        {
            TestContext context = CreateContext();
            context.Router.RegisterCommandHandler(context.Instance.InstanceId, 1, "increment", static _ => { });
            Assert.Equal(CommandDispatchStatus.Queued, context.Router.OnCommand(context.Instance.InstanceId, 1, "[]").Status);

            context.Router.Dispose();
            CommandDispatchResult result = context.Router.OnCommand(context.Instance.InstanceId, 1, "[]");

            Assert.Equal(CommandDispatchStatus.Disposed, result.Status);
            Assert.Equal(0, context.Registry.GetMetrics().QueuedCommandCount);
        }

        [Fact]
        public async Task ConcurrentDispatch_ActiveInstance_RemainsConsistent()
        {
            TestContext context = CreateContext();
            _ = context.Router.MarkReady(context.Instance.InstanceId);
            const int DispatchCount = 50;
            TaskCompletionSource<int> completed = NewCompletionSource<int>();
            int count = 0;
            context.Router.RegisterCommandHandler(context.Instance.InstanceId, 1, "increment", _ =>
            {
                int current = Interlocked.Increment(ref count);
                if (current == DispatchCount)
                {
                    completed.SetResult(current);
                }
            });

            IReadOnlyList<Task> tasks = Enumerable.Range(0, DispatchCount)
                .Select(index => Task.Run(() => context.Router.OnCommand(context.Instance.InstanceId, 1, "[" + index.ToString(CultureInfo.InvariantCulture) + "]")))
                .ToArray();
            await Task.WhenAll(tasks);
            _ = await WaitFor(completed.Task);

            Assert.Equal(DispatchCount, context.Registry.GetMetrics().TotalCommandsDispatched);
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public async Task OnCommand_HandlerReentersRouter_DispatchesNestedCommandWithoutDeadlock()
        {
            TestContext context = CreateContext();
            _ = context.Router.MarkReady(context.Instance.InstanceId);
            TaskCompletionSource<CommandDispatchResult> nestedResult = NewCompletionSource<CommandDispatchResult>();
            TaskCompletionSource<CommandInvocation> innerHandled = NewCompletionSource<CommandInvocation>();
            context.Router.RegisterCommandHandler(context.Instance.InstanceId, 1, "inner", innerHandled.SetResult);
            context.Router.RegisterCommandHandler(context.Instance.InstanceId, 2, "outer", _ =>
            {
                CommandDispatchResult result = context.Router.OnCommand(context.Instance.InstanceId, "inner", "[]");
                nestedResult.SetResult(result);
            });

            CommandDispatchResult outerResult = context.Router.OnCommand(context.Instance.InstanceId, "outer", "[]");

            Assert.Equal(CommandDispatchStatus.Dispatched, outerResult.Status);
            Assert.Equal(CommandDispatchStatus.Dispatched, (await WaitFor(nestedResult.Task)).Status);
            CommandInvocation invocation = await WaitFor(innerHandled.Task);
            Assert.Equal("inner", invocation.CommandName);
        }

        [Fact]
        [Trait("Category", TestCategories.Performance)]
        public async Task Dispose_DuringInFlightHandler_AllowsHandlerToDrainAndRejectsNewCommands()
        {
            TestContext context = CreateContext();
            _ = context.Router.MarkReady(context.Instance.InstanceId);
            TaskCompletionSource<bool> entered = NewCompletionSource<bool>();
            TaskCompletionSource<bool> release = NewCompletionSource<bool>();
            TaskCompletionSource<bool> completed = NewCompletionSource<bool>();
            context.Router.RegisterCommandHandler(context.Instance.InstanceId, 1, "slow", async invocation =>
            {
                entered.SetResult(true);
                bool released = await release.Task.ConfigureAwait(false);
                Assert.True(released);
                Assert.Equal("slow", invocation.CommandName);
                completed.SetResult(true);
            });

            CommandDispatchResult result = context.Router.OnCommand(context.Instance.InstanceId, "slow", "[]");
            _ = await WaitFor(entered.Task);

            context.Router.Dispose();
            release.SetResult(true);
            _ = await WaitFor(completed.Task);
            CommandDispatchResult afterDispose = context.Router.OnCommand(context.Instance.InstanceId, "slow", "[]");

            Assert.Equal(CommandDispatchStatus.Dispatched, result.Status);
            Assert.Equal(CommandDispatchStatus.Disposed, afterDispose.Status);
        }

        private static TestContext CreateContext()
        {
            ManagedInstanceRegistry registry = new();
            ManagedViewModelInstance instance = registry.Register(new InstanceRegistration(
                NewInstanceId(),
                "CounterViewModel",
                "counter-schema",
                "CounterView::__qmlsharp_vm0",
                new IntPtr(123),
                new IntPtr(456)));
            List<RuntimeDiagnostic> diagnostics = [];
            CommandRouter router = new(registry, diagnostics.Add);
            return new TestContext(registry, instance, router, diagnostics);
        }

        private static ManagedViewModelInstance RegisterSecondInstance(ManagedInstanceRegistry registry)
        {
            return registry.Register(new InstanceRegistration(
                NewInstanceId(),
                "CounterViewModel",
                "counter-schema",
                "CounterView::__qmlsharp_vm1",
                new IntPtr(789),
                new IntPtr(987)));
        }

        private static TaskCompletionSource<T> NewCompletionSource<T>()
        {
            return new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static async Task<T> WaitFor<T>(Task<T> task)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
            if (completed != task)
            {
                throw new TimeoutException("Timed out waiting for command routing test callback.");
            }

            return await task;
        }

        private static async Task WaitFor(Task task)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
            if (completed != task)
            {
                throw new TimeoutException("Timed out waiting for command routing test callback.");
            }

            await task;
        }

        private static string NewInstanceId()
        {
            return Guid.NewGuid().ToString("D");
        }

        private sealed record TestContext(
            ManagedInstanceRegistry Registry,
            ManagedViewModelInstance Instance,
            CommandRouter Router,
            List<RuntimeDiagnostic> Diagnostics);
    }
}
