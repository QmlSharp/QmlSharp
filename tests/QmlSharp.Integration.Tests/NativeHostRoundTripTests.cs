using System.Text.Json;
using QmlSharp.Host.Commands;
using QmlSharp.Host.Effects;
using QmlSharp.Host.Engine;
using QmlSharp.Host.ErrorOverlay;
using QmlSharp.Host.HotReload;
using QmlSharp.Host.InstanceRegistry;
using QmlSharp.Host.Instances;
using QmlSharp.Integration.Tests.Fixtures;

namespace QmlSharp.Integration.Tests
{
    public sealed class NativeHostRoundTripTests
    {
        [Fact]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        [Trait("Category", TestCategories.RequiresNative)]
        public void EngineStartup_FromFixtureArtifacts_InitializesAndShutsDown_INT_01()
        {
            using NativeRoundTripFixture fixture = NativeRoundTripFixture.Create();
            using QmlSharpEngine engine = fixture.CreateEngine();

            engine.Initialize(fixture.DistDirectory, [], fixture.MainQmlPath);
            string metricsJson = engine.GetNativeMetricsJson();
            engine.Shutdown();

            Assert.False(string.IsNullOrWhiteSpace(metricsJson));
            Assert.False(engine.IsInitialized);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        [Trait("Category", TestCategories.RequiresNative)]
        public void Run_FromQuitFixture_EntersEventLoopAndExitsCleanly_INT_01()
        {
            using NativeRoundTripFixture fixture = NativeRoundTripFixture.Create();
            using QmlSharpEngine engine = fixture.CreateEngine();
            engine.Initialize(fixture.DistDirectory, [], fixture.QuitQmlPath);
            engine.RegisterTypes(fixture.Schemas);
            fixture.QueueApplicationQuit();

            int exitCode = engine.Run(fixture.QuitQmlPath);

            Assert.Equal(0, exitCode);
            engine.Shutdown();
            Assert.False(engine.IsInitialized);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        [Trait("Category", TestCategories.RequiresNative)]
        public void RegisterTypesAndLoadRootQml_FromSchema_CreatesManagedAndNativeInstance_INT_01()
        {
            using NativeRoundTripFixture fixture = NativeRoundTripFixture.Create();
            using QmlSharpEngine engine = CreateInitializedRegisteredEngine(fixture);

            ManagedViewModelInstance instance = LoadMainAndGetSingleInstance(engine, fixture);
            string nativeInfoJson = engine.GetNativeInstanceInfoJson(instance.InstanceId)
                ?? throw new InvalidOperationException("Native instance info should be present.");

            using JsonDocument nativeInfo = JsonDocument.Parse(nativeInfoJson);
            Assert.Equal("RegistrationCounterViewModel", nativeInfo.RootElement.GetProperty("className").GetString());
            Assert.Equal("RegistrationView::__qmlsharp_vm0", nativeInfo.RootElement.GetProperty("compilerSlotKey").GetString());
            Assert.Equal("created", nativeInfo.RootElement.GetProperty("properties").GetProperty("title").GetString());
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        [Trait("Category", TestCategories.RequiresNative)]
        public void SyncState_FromManaged_UpdatesQmlObservedProperty_INT_02()
        {
            using NativeRoundTripFixture fixture = NativeRoundTripFixture.Create();
            using QmlSharpEngine engine = CreateInitializedRegisteredEngine(fixture);
            ManagedViewModelInstance instance = LoadMainAndGetSingleInstance(engine, fixture);

            engine.SyncState(instance.InstanceId, "count", 41);

            using JsonDocument nativeInfo = ReadNativeInstanceInfo(engine, instance.InstanceId);
            JsonElement properties = nativeInfo.RootElement.GetProperty("properties");
            Assert.Equal(41, properties.GetProperty("count").GetInt32());
            Assert.Equal("count:41", properties.GetProperty("title").GetString());
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        [Trait("Category", TestCategories.RequiresNative)]
        public void CommandDispatch_PreReadyQmlCommand_FlushesThroughInstanceReady_INT_03()
        {
            using NativeRoundTripFixture fixture = NativeRoundTripFixture.Create();
            using QmlSharpEngine engine = CreateInitializedRegisteredEngine(fixture);
            ManagedViewModelInstance instance = LoadMainAndGetSingleInstance(engine, fixture);
            TaskCompletionSource<CommandInvocation> commandReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
            engine.RegisterCommandHandler(instance.InstanceId, "commandInt", commandReceived.SetResult);

            Assert.Equal(InstanceState.Pending, instance.State);
            Assert.False(commandReceived.Task.IsCompleted);

            engine.InstanceReady(instance.InstanceId);

            CommandInvocation invocation = WaitFor(commandReceived.Task, "commandInt should flush after instanceReady.");
            Assert.Equal(instance.InstanceId, invocation.InstanceId);
            Assert.Equal("commandInt", invocation.CommandName);
            Assert.Equal("[7]", invocation.ArgsJson);
            Assert.Equal(InstanceState.Active, engine.Instances.FindById(instance.InstanceId)?.State);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        [Trait("Category", TestCategories.RequiresNative)]
        public void DispatchEffect_FromManaged_ReachesQmlSignalHandler_INT_04()
        {
            using NativeRoundTripFixture fixture = NativeRoundTripFixture.Create();
            using QmlSharpEngine engine = CreateInitializedRegisteredEngine(fixture);
            ManagedViewModelInstance instance = LoadMainAndGetSingleInstance(engine, fixture);
            engine.InstanceReady(instance.InstanceId);
            engine.Effects.RegisterEffect(instance.InstanceId, 2001, "showToast");

            EffectDispatchResult result = engine.DispatchEffect(instance.InstanceId, "showToast", new { message = "hello" });

            Assert.True(result.Succeeded, result.Message);
            using JsonDocument nativeInfo = ReadNativeInstanceInfo(engine, instance.InstanceId);
            string? title = nativeInfo.RootElement.GetProperty("properties").GetProperty("title").GetString();
            Assert.Equal("showToast:{\"message\":\"hello\"}", title);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        [Trait("Category", TestCategories.RequiresNative)]
        public async Task HotReload_PreservesStateAcrossReloadedFixture_INT_06()
        {
            using NativeRoundTripFixture fixture = NativeRoundTripFixture.Create();
            using QmlSharpEngine engine = CreateInitializedRegisteredEngine(fixture);
            ManagedViewModelInstance oldInstance = LoadMainAndGetSingleInstance(engine, fixture);
            engine.InstanceReady(oldInstance.InstanceId);
            engine.SyncState(oldInstance.InstanceId, "count", 42);

            HotReloadResult result = await engine.ReloadAsync(fixture.ReloadQmlPath);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(1, result.InstancesMatched);
            ManagedViewModelInstance newInstance = Assert.Single(engine.Instances.GetAll());
            Assert.NotEqual(oldInstance.InstanceId, newInstance.InstanceId);
            using JsonDocument nativeInfo = ReadNativeInstanceInfo(engine, newInstance.InstanceId);
            JsonElement properties = nativeInfo.RootElement.GetProperty("properties");
            Assert.Equal(42, properties.GetProperty("count").GetInt32());
            Assert.Equal("count:42", properties.GetProperty("title").GetString());
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        [Trait("Category", TestCategories.RequiresNative)]
        public void ErrorOverlay_ShowHide_UpdatesNativeMetrics_INT_07()
        {
            using NativeRoundTripFixture fixture = NativeRoundTripFixture.Create();
            using QmlSharpEngine engine = CreateInitializedRegisteredEngine(fixture);
            _ = LoadMainAndGetSingleInstance(engine, fixture);

            engine.ErrorOverlay.Show(new ErrorOverlayPayload(
                "Integration failure",
                new ErrorOverlaySourceLocation("CounterViewModel.cs", 12, 5),
                "INT-07"));
            using JsonDocument visibleMetrics = JsonDocument.Parse(engine.GetNativeMetricsJson());
            Assert.True(engine.ErrorOverlay.IsVisible);
            Assert.True(visibleMetrics.RootElement.GetProperty("errorOverlayVisible").GetBoolean());

            engine.ErrorOverlay.Hide();
            using JsonDocument hiddenMetrics = JsonDocument.Parse(engine.GetNativeMetricsJson());
            Assert.False(engine.ErrorOverlay.IsVisible);
            Assert.False(hiddenMetrics.RootElement.GetProperty("errorOverlayVisible").GetBoolean());
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        [Trait("Category", TestCategories.RequiresNative)]
        public void DevTools_InstanceEnumerationAndMetrics_ReportNativeRuntimeState_INT_05()
        {
            using NativeRoundTripFixture fixture = NativeRoundTripFixture.Create();
            using QmlSharpEngine engine = CreateInitializedRegisteredEngine(fixture);
            ManagedViewModelInstance instance = LoadMainAndGetSingleInstance(engine, fixture);
            engine.InstanceReady(instance.InstanceId);
            engine.SyncState(instance.InstanceId, "count", 9);

            using JsonDocument instances = JsonDocument.Parse(engine.GetNativeAllInstancesJson());
            using JsonDocument metrics = JsonDocument.Parse(engine.GetNativeMetricsJson());

            JsonElement listedInstance = Assert.Single(instances.RootElement.EnumerateArray());
            Assert.Equal(instance.InstanceId, listedInstance.GetProperty("instanceId").GetString());
            Assert.Equal("RegistrationCounterViewModel", listedInstance.GetProperty("className").GetString());
            Assert.Equal(9, listedInstance.GetProperty("properties").GetProperty("count").GetInt32());
            Assert.True(metrics.RootElement.GetProperty("activeInstanceCount").GetInt32() >= 1);
            Assert.True(metrics.RootElement.GetProperty("stateSyncCount").GetDouble() >= 1.0);
        }

        private static QmlSharpEngine CreateInitializedRegisteredEngine(NativeRoundTripFixture fixture)
        {
            QmlSharpEngine engine = fixture.CreateEngine();
            engine.Initialize(fixture.DistDirectory, [], fixture.MainQmlPath);
            engine.RegisterTypes(fixture.Schemas);
            return engine;
        }

        private static ManagedViewModelInstance LoadMainAndGetSingleInstance(
            QmlSharpEngine engine,
            NativeRoundTripFixture fixture)
        {
            engine.LoadRootQml(fixture.MainQmlPath);
            return Assert.Single(engine.Instances.GetAll());
        }

        private static JsonDocument ReadNativeInstanceInfo(QmlSharpEngine engine, string instanceId)
        {
            string nativeInfoJson = engine.GetNativeInstanceInfoJson(instanceId)
                ?? throw new InvalidOperationException("Native instance info should be present.");
            return JsonDocument.Parse(nativeInfoJson);
        }

        private static T WaitFor<T>(Task<T> task, string failureMessage)
        {
            if (!task.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException(failureMessage);
            }

            return task.GetAwaiter().GetResult();
        }
    }
}
