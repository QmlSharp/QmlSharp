using System.Text.Json;
using QmlSharp.Host.Exceptions;
using QmlSharp.Host.Instances;
using QmlSharp.Host.StateSynchronization;
using ManagedInstanceRegistry = QmlSharp.Host.Instances.InstanceRegistry;

namespace QmlSharp.Host.Tests.StateSynchronization
{
    public sealed class StateSyncTests
    {
        [Fact]
        public void Push_IntValue_CallsSyncStateIntAndUpdatesSnapshot()
        {
            TestContext context = CreateContext();

            context.StateSync.PushInt(context.Instance.InstanceId, "count", 42);

            SyncCall call = Assert.Single(context.Interop.Calls);
            Assert.Equal("int", call.Kind);
            Assert.Equal(context.Instance.InstanceId, call.InstanceId);
            Assert.Equal("count", call.PropertyName);
            Assert.Equal(42, call.Value);
            Assert.Equal(42, context.Instance.CurrentState["count"]);
        }

        [Fact]
        public void Push_DoubleValue_CallsSyncStateDoubleAndUpdatesSnapshot()
        {
            TestContext context = CreateContext();

            context.StateSync.PushDouble(context.Instance.InstanceId, "ratio", 0.75);

            SyncCall call = Assert.Single(context.Interop.Calls);
            Assert.Equal("double", call.Kind);
            Assert.Equal(0.75, Assert.IsType<double>(call.Value), precision: 3);
            Assert.Equal(0.75, Assert.IsType<double>(context.Instance.CurrentState["ratio"]), precision: 3);
        }

        [Fact]
        public void Push_BoolValue_CallsSyncStateBoolAndUpdatesSnapshot()
        {
            TestContext context = CreateContext();

            context.StateSync.PushBool(context.Instance.InstanceId, "enabled", true);

            SyncCall call = Assert.Single(context.Interop.Calls);
            Assert.Equal("bool", call.Kind);
            Assert.True(Assert.IsType<bool>(call.Value));
            Assert.True(Assert.IsType<bool>(context.Instance.CurrentState["enabled"]));
        }

        [Fact]
        public void Push_StringValue_CallsSyncStateStringAndUpdatesSnapshot()
        {
            TestContext context = CreateContext();

            context.StateSync.PushString(context.Instance.InstanceId, "label", "Ready");

            SyncCall call = Assert.Single(context.Interop.Calls);
            Assert.Equal("string", call.Kind);
            Assert.Equal("Ready", call.Value);
            Assert.Equal("Ready", context.Instance.CurrentState["label"]);
        }

        [Fact]
        public void Push_GenericScalarValues_SelectsTypedFastPaths()
        {
            TestContext context = CreateContext();

            context.StateSync.Push(context.Instance.InstanceId, "count", 1);
            context.StateSync.Push(context.Instance.InstanceId, "ratio", 0.5);
            context.StateSync.Push(context.Instance.InstanceId, "enabled", true);
            context.StateSync.Push(context.Instance.InstanceId, "label", "Ready");

            Assert.Collection(
                context.Interop.Calls,
                call => Assert.Equal("int", call.Kind),
                call => Assert.Equal("double", call.Kind),
                call => Assert.Equal("bool", call.Kind),
                call => Assert.Equal("string", call.Kind));
        }

        [Fact]
        public void Push_NullString_CallsSyncStateStringAndStoresNull()
        {
            TestContext context = CreateContext();

            context.StateSync.PushString(context.Instance.InstanceId, "label", null);

            SyncCall call = Assert.Single(context.Interop.Calls);
            Assert.Equal("string", call.Kind);
            Assert.Null(call.Value);
            Assert.Null(context.Instance.CurrentState["label"]);
        }

        [Fact]
        public void Push_ComplexObject_FallsBackToJson()
        {
            TestContext context = CreateContext();

            context.StateSync.Push(context.Instance.InstanceId, "settings", new SettingsPayload("dark", 2));

            SyncCall call = Assert.Single(context.Interop.Calls);
            Assert.Equal("json", call.Kind);
            JsonElement payload = Parse(Assert.IsType<string>(call.Value));
            Assert.Equal("dark", payload.GetProperty("Mode").GetString());
            Assert.Equal(2, payload.GetProperty("Level").GetInt32());
            JsonElement snapshot = Assert.IsType<JsonElement>(context.Instance.CurrentState["settings"]);
            Assert.Equal("dark", snapshot.GetProperty("Mode").GetString());
        }

        [Fact]
        public void PushBatch_MultipleValues_CallsSyncStateBatchAndUpdatesSnapshotOnce()
        {
            TestContext context = CreateContext();
            Dictionary<string, object?> properties = new(StringComparer.Ordinal)
            {
                ["label"] = "Ready",
                ["count"] = 7,
                ["enabled"] = false
            };

            context.StateSync.PushBatch(context.Instance.InstanceId, properties);

            SyncCall call = Assert.Single(context.Interop.Calls);
            Assert.Equal("batch", call.Kind);
            JsonElement payload = Parse(Assert.IsType<string>(call.Value));
            Assert.Equal(7, payload.GetProperty("count").GetInt32());
            Assert.False(payload.GetProperty("enabled").GetBoolean());
            Assert.Equal("Ready", payload.GetProperty("label").GetString());
            Assert.Equal(7, context.Instance.CurrentState["count"]);
            Assert.False(Assert.IsType<bool>(context.Instance.CurrentState["enabled"]));
            Assert.Equal("Ready", context.Instance.CurrentState["label"]);
            Assert.Equal(1, context.Registry.GetMetrics().TotalStateSyncs);
        }

        [Fact]
        public void Push_UnknownInstance_ThrowsStructuredErrorAndDoesNotCallNative()
        {
            TestContext context = CreateContext();
            string missingInstanceId = NewInstanceId();

            InstanceNotFoundException exception = Assert.Throws<InstanceNotFoundException>(() =>
                context.StateSync.PushInt(missingInstanceId, "count", 1));

            Assert.Equal(missingInstanceId, exception.InstanceId);
            Assert.Empty(context.Interop.Calls);
        }

        [Fact]
        public void Push_UnknownPropertyWhenSchemaMetadataAvailable_ThrowsStructuredErrorAndDoesNotCallNative()
        {
            TestContext context = CreateContext();

            PropertyNotFoundException exception = Assert.Throws<PropertyNotFoundException>(() =>
                context.StateSync.PushInt(context.Instance.InstanceId, "missing", 1));

            Assert.Equal(context.Instance.InstanceId, exception.InstanceId);
            Assert.Equal("missing", exception.PropertyName);
            Assert.Empty(context.Interop.Calls);
            Assert.Empty(context.Instance.CurrentState);
        }

        [Fact]
        public void Push_UnknownPropertyWithoutSchemaMetadata_AllowsNativeValidation()
        {
            TestContext context = CreateContext(includeSchemaMetadata: false);

            context.StateSync.PushInt(context.Instance.InstanceId, "nativeOnly", 5);

            SyncCall call = Assert.Single(context.Interop.Calls);
            Assert.Equal("nativeOnly", call.PropertyName);
            Assert.Equal(5, context.Instance.CurrentState["nativeOnly"]);
        }

        [Fact]
        public void Push_NativeFailure_MapsErrorAndDoesNotUpdateSnapshot()
        {
            TestContext context = CreateContext();
            context.Interop.NextResultCode = -7;
            context.Interop.LastError = "native property was not found";

            PropertyNotFoundException exception = Assert.Throws<PropertyNotFoundException>(() =>
                context.StateSync.PushInt(context.Instance.InstanceId, "count", 1));

            Assert.Equal("count", exception.PropertyName);
            Assert.Contains("native property", exception.Message, StringComparison.Ordinal);
            Assert.Empty(context.Instance.CurrentState);
        }

        [Fact]
        public void Push_FromManagedThread_MarshalsThroughInteropDecisionPoint()
        {
            TestContext context = CreateContext();
            context.Interop.IsOnMainThread = false;

            context.StateSync.PushInt(context.Instance.InstanceId, "count", 3);

            Assert.Equal(1, context.Interop.PostToMainThreadCallCount);
            SyncCall call = Assert.Single(context.Interop.Calls);
            Assert.Equal("int", call.Kind);
            Assert.Equal(3, context.Instance.CurrentState["count"]);
        }

        [Fact]
        public async Task ConcurrentSyncCalls_UpdateManagedSnapshotConsistently()
        {
            TestContext context = CreateContext();
            IReadOnlyList<Task> tasks = Enumerable.Range(0, 50)
                .Select(index => Task.Run(() => context.StateSync.PushInt(context.Instance.InstanceId, "count", index)))
                .ToArray();

            await Task.WhenAll(tasks);

            Assert.Equal(50, context.Interop.Calls.Count);
            Assert.True(context.Instance.CurrentState.ContainsKey("count"));
            Assert.Equal(50, context.Registry.GetMetrics().TotalStateSyncs);
        }

        [Fact]
        public void PushJson_InvalidJson_ThrowsBeforeNativeCall()
        {
            TestContext context = CreateContext();

            NativeJsonException exception = Assert.Throws<NativeJsonException>(() =>
                context.StateSync.PushJson(context.Instance.InstanceId, "settings", "{not-json"));

            Assert.Contains("not valid JSON", exception.Message, StringComparison.Ordinal);
            Assert.Empty(context.Interop.Calls);
            Assert.Empty(context.Instance.CurrentState);
        }

        private static TestContext CreateContext(bool includeSchemaMetadata = true)
        {
            ManagedInstanceRegistry registry = new();
            ManagedViewModelInstance instance = registry.Register(new InstanceRegistration(
                NewInstanceId(),
                "CounterViewModel",
                "counter-schema",
                "CounterView::__qmlsharp_vm0",
                new IntPtr(123),
                new IntPtr(456)));
            FakeNativeHostInterop interop = new();
            IReadOnlyList<StateSyncSchemaMetadata>? schemas = includeSchemaMetadata
                ? new[]
                {
                    new StateSyncSchemaMetadata(
                        "counter-schema",
                        new[]
                        {
                            new StateSyncPropertyMetadata("count", StateSyncValueKind.Int32),
                            new StateSyncPropertyMetadata("ratio", StateSyncValueKind.Double),
                            new StateSyncPropertyMetadata("enabled", StateSyncValueKind.Boolean),
                            new StateSyncPropertyMetadata("label", StateSyncValueKind.String),
                            new StateSyncPropertyMetadata("settings", StateSyncValueKind.Json)
                        })
                }
                : null;
            StateSync stateSync = new(registry, interop, schemas);
            return new TestContext(registry, instance, interop, stateSync);
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

        private sealed record SettingsPayload(string Mode, int Level);

        private sealed record TestContext(
            ManagedInstanceRegistry Registry,
            ManagedViewModelInstance Instance,
            FakeNativeHostInterop Interop,
            StateSync StateSync);
    }
}
