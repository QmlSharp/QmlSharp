using System.Text.Json;
using QmlSharp.Host.Diagnostics;
using QmlSharp.Host.Effects;
using QmlSharp.Host.Instances;
using QmlSharp.Host.Tests.StateSynchronization;
using ManagedInstanceRegistry = QmlSharp.Host.Instances.InstanceRegistry;

namespace QmlSharp.Host.Tests.EffectRouting
{
    public sealed class EffectRouterTests
    {
        private static int nextNativeHandle = 122;

        [Fact]
        public void Dispatch_RegisteredEffect_CallsNativeDispatch()
        {
            TestContext context = CreateContext();

            EffectDispatchResult result = context.Router.Dispatch(context.Instance.InstanceId, "showToast", "{\"message\":\"Saved\"}");

            Assert.Equal(EffectDispatchStatus.Dispatched, result.Status);
            SyncCall call = Assert.Single(context.Interop.Calls);
            Assert.Equal("effect", call.Kind);
            Assert.Equal(context.Instance.InstanceId, call.InstanceId);
            Assert.Equal("showToast", call.PropertyName);
            Assert.Equal("{\"message\":\"Saved\"}", call.Value);
            Assert.Equal(1, context.Registry.GetMetrics().TotalEffectsEmitted);
        }

        [Fact]
        public void Dispatch_NullPayload_UsesEmptyObjectJson()
        {
            TestContext context = CreateContext();

            EffectDispatchResult result = context.Router.Dispatch(context.Instance.InstanceId, 11);

            Assert.Equal(EffectDispatchStatus.Dispatched, result.Status);
            SyncCall call = Assert.Single(context.Interop.Calls);
            Assert.Equal("{}", call.Value);
        }

        [Fact]
        public void Broadcast_RegisteredEffect_CallsNativeBroadcast()
        {
            TestContext context = CreateContext();
            ManagedViewModelInstance secondInstance = RegisterReadyInstance(context.Registry, "CounterViewModel");
            context.Router.RegisterEffect(secondInstance.InstanceId, 11, "showToast");

            EffectDispatchResult result = context.Router.Broadcast("CounterViewModel", "showToast", "{\"message\":\"Saved\"}");

            Assert.Equal(EffectDispatchStatus.Broadcast, result.Status);
            SyncCall call = Assert.Single(context.Interop.Calls);
            Assert.Equal("broadcast", call.Kind);
            Assert.Equal("CounterViewModel", call.InstanceId);
            Assert.Equal("showToast", call.PropertyName);
            Assert.Equal("{\"message\":\"Saved\"}", call.Value);
            Assert.Equal(2, context.Registry.GetMetrics().TotalEffectsEmitted);
        }

        [Fact]
        public void Dispatch_FromManagedThread_MarshalsThroughInteropDecisionPoint()
        {
            TestContext context = CreateContext();
            context.Interop.IsOnMainThread = false;

            EffectDispatchResult result = context.Router.Dispatch(context.Instance.InstanceId, "showToast", "{\"message\":\"Saved\"}");

            Assert.Equal(EffectDispatchStatus.Dispatched, result.Status);
            Assert.Equal(1, context.Interop.PostToMainThreadCallCount);
            SyncCall call = Assert.Single(context.Interop.Calls);
            Assert.Equal("effect", call.Kind);
        }

        [Fact]
        public void Dispatch_ComplexPayload_SerializesValidJson()
        {
            TestContext context = CreateContext();

            EffectDispatchResult result = context.Router.Dispatch(
                context.Instance.InstanceId,
                "showToast",
                new ToastPayload("Saved", new[] { "info", "persisted" }));

            Assert.Equal(EffectDispatchStatus.Dispatched, result.Status);
            SyncCall call = Assert.Single(context.Interop.Calls);
            JsonElement payload = Parse(Assert.IsType<string>(call.Value));
            Assert.Equal("Saved", payload.GetProperty("Message").GetString());
            Assert.Equal("info", payload.GetProperty("Tags")[0].GetString());
            Assert.Equal("persisted", payload.GetProperty("Tags")[1].GetString());
        }

        [Fact]
        public void Dispatch_UnknownInstance_ReturnsStructuredError()
        {
            TestContext context = CreateContext();

            EffectDispatchResult result = context.Router.Dispatch(NewInstanceId(), "showToast", "{}");

            Assert.Equal(EffectDispatchStatus.UnknownInstance, result.Status);
            Assert.Empty(context.Interop.Calls);
            RuntimeDiagnostic diagnostic = Assert.Single(context.Diagnostics);
            Assert.Equal(RuntimeDiagnosticSeverity.Warning, diagnostic.Severity);
        }

        [Fact]
        public void Dispatch_UnknownEffect_ReturnsStructuredError()
        {
            TestContext context = CreateContext();

            EffectDispatchResult result = context.Router.Dispatch(context.Instance.InstanceId, "missing", "{}");

            Assert.Equal(EffectDispatchStatus.UnknownEffect, result.Status);
            Assert.Empty(context.Interop.Calls);
            RuntimeDiagnostic diagnostic = Assert.Single(context.Diagnostics);
            Assert.Contains("missing", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Broadcast_UnregisteredEffect_ReturnsStructuredError()
        {
            TestContext context = CreateContext();

            EffectDispatchResult result = context.Router.Broadcast("CounterViewModel", "missing", "{}");

            Assert.Equal(EffectDispatchStatus.UnknownEffect, result.Status);
            Assert.Empty(context.Interop.Calls);
        }

        [Fact]
        public void Dispatch_InvalidRawJsonPayload_ReturnsStructuredError()
        {
            TestContext context = CreateContext();

            EffectDispatchResult result = context.Router.Dispatch(context.Instance.InstanceId, "showToast", "{not-json");

            Assert.Equal(EffectDispatchStatus.InvalidPayload, result.Status);
            Assert.Empty(context.Interop.Calls);
            RuntimeDiagnostic diagnostic = Assert.Single(context.Diagnostics);
            Assert.Equal(RuntimeDiagnosticSeverity.Error, diagnostic.Severity);
        }

        [Fact]
        public void Dispatch_NativeFailure_ReturnsStructuredError()
        {
            TestContext context = CreateContext();
            context.Interop.NextResultCode = -3;
            context.Interop.LastError = "native instance missing";

            EffectDispatchResult result = context.Router.Dispatch(context.Instance.InstanceId, 11, "{}");

            Assert.Equal(EffectDispatchStatus.NativeFailure, result.Status);
            Assert.Contains("native instance missing", result.Message, StringComparison.Ordinal);
            RuntimeDiagnostic diagnostic = Assert.Single(context.Diagnostics);
            Assert.Equal(RuntimeDiagnosticSeverity.Error, diagnostic.Severity);
        }

        [Fact]
        public void Dispose_ClearsRegistrations()
        {
            TestContext context = CreateContext();

            context.Router.Dispose();
            EffectDispatchResult result = context.Router.Dispatch(context.Instance.InstanceId, 11, "{}");

            Assert.Equal(EffectDispatchStatus.Disposed, result.Status);
            Assert.Empty(context.Interop.Calls);
        }

        [Fact]
        public void ClearInstance_RemovesEffectRegistration()
        {
            TestContext context = CreateContext();

            context.Router.ClearInstance(context.Instance.InstanceId);
            EffectDispatchResult result = context.Router.Dispatch(context.Instance.InstanceId, 11, "{}");

            Assert.Equal(EffectDispatchStatus.UnknownEffect, result.Status);
            Assert.Empty(context.Interop.Calls);
        }

        private static TestContext CreateContext()
        {
            ManagedInstanceRegistry registry = new();
            ManagedViewModelInstance instance = RegisterReadyInstance(registry, "CounterViewModel");
            FakeNativeHostInterop interop = new();
            List<RuntimeDiagnostic> diagnostics = [];
            EffectRouter router = new(registry, interop, diagnostics.Add);
            router.RegisterEffect(instance.InstanceId, 11, "showToast");
            return new TestContext(registry, instance, interop, router, diagnostics);
        }

        private static ManagedViewModelInstance RegisterReadyInstance(ManagedInstanceRegistry registry, string className)
        {
            ManagedViewModelInstance instance = registry.Register(new InstanceRegistration(
                NewInstanceId(),
                className,
                className + "-schema",
                className + "::__qmlsharp_vm0",
                new IntPtr(Interlocked.Increment(ref nextNativeHandle)),
                new IntPtr(Interlocked.Increment(ref nextNativeHandle))));
            Assert.True(registry.MarkReady(instance.InstanceId));
            return instance;
        }

        private static JsonElement Parse(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }

        private static string NewInstanceId()
        {
            return Guid.NewGuid().ToString("D");
        }

        private sealed record ToastPayload(string Message, IReadOnlyList<string> Tags);

        private sealed record TestContext(
            ManagedInstanceRegistry Registry,
            ManagedViewModelInstance Instance,
            FakeNativeHostInterop Interop,
            EffectRouter Router,
            List<RuntimeDiagnostic> Diagnostics);
    }
}
