using QmlSharp.Compiler;
using QmlSharp.Host.Engine;
using QmlSharp.Host.Exceptions;
using QmlSharp.Host.InstanceRegistry;
using QmlSharp.Host.Interop;
using QmlSharp.Host.Tests.Fixtures;
using QmlSharp.Host.Tests.StateSynchronization;

namespace QmlSharp.Host.Tests.Engine
{
    public sealed class QmlSharpEngineTests
    {
        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void ExpectedAbiVersion_DefaultFacade_ReturnsLockedVersion()
        {
            QmlSharpEngine engine = new();

            Assert.Equal(NativeHostAbi.SupportedAbiVersion, engine.ExpectedAbiVersion);
        }

        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void LoadRootQml_BeforeInitialize_ThrowsEngineNotInitializedException()
        {
            using QmlSharpEngine engine = CreateEngine(new FakeNativeHostInterop());

            _ = Assert.Throws<EngineNotInitializedException>(() => engine.LoadRootQml("Main.qml"));
        }

        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void Initialize_WithMockInterop_SetsNativeCallbacksAndFacades()
        {
            FakeNativeHostInterop interop = new();
            using QmlSharpEngine engine = CreateEngine(interop);

            engine.Initialize(["qmlsharp-test"]);

            Assert.True(engine.IsInitialized);
            Assert.NotNull(engine.ErrorOverlay);
            Assert.NotNull(interop.InstanceCreatedCallback);
            Assert.NotNull(interop.InstanceDestroyedCallback);
            Assert.NotNull(interop.CommandCallback);
            Assert.Contains(interop.Calls, static call => call.Kind == "engine_init");
            Assert.Contains(interop.Calls, static call => call.Kind == "set_instance_callbacks" && (string?)call.Value == "set");
            Assert.Contains(interop.Calls, static call => call.Kind == "set_command_callback" && (string?)call.Value == "set");
        }

        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void RegisterModule_WithSchema_RegistersNativeTypeAndUpdatesStateMetadata()
        {
            FakeNativeHostInterop interop = new();
            using QmlSharpEngine engine = CreateEngine(interop);
            ViewModelSchema schema = CreateSchema();
            string instanceId = "11111111-1111-4111-8111-111111111111";
            List<string> callbackTypes = [];

            engine.Initialize();
            engine.RegisterModule(
                "QmlSharp.Managed.Tests",
                1,
                0,
                [schema],
                (moduleUri, versionMajor, versionMinor, typeName) =>
                {
                    callbackTypes.Add(typeName);
                    return 123;
                });

            interop.InstanceCreatedCallback?.Invoke(instanceId, schema.ClassName, schema.CompilerSlotKey);
            engine.State.PushString(instanceId, "title", "ready");

            Assert.Contains("ManagedCounterViewModel", callbackTypes);
            Assert.Contains(interop.Calls, static call => call.Kind == "register_module");
            Assert.Contains(interop.Calls, static call => call.Kind == "string" && call.PropertyName == "title");
        }

        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void InstanceReady_ExistingInstance_MarksManagedInstanceAndCallsNativeReady()
        {
            FakeNativeHostInterop interop = new();
            using QmlSharpEngine engine = CreateEngine(interop);
            string instanceId = "22222222-2222-4222-8222-222222222222";

            engine.Initialize();
            interop.InstanceCreatedCallback?.Invoke(instanceId, "ManagedCounterViewModel", "MainView::__qmlsharp_vm0");

            engine.InstanceReady(instanceId);

            Assert.Equal(InstanceState.Active, engine.Instances.FindById(instanceId)?.State);
            Assert.Contains(interop.Calls, static call => call.Kind == "instance_ready");
        }

        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void Dispose_InitializedEngine_ShutsDownNativeOnceAndClearsCallbacks()
        {
            FakeNativeHostInterop interop = new();
            QmlSharpEngine engine = CreateEngine(interop);

            engine.Initialize();
            engine.Dispose();
            engine.Dispose();

            _ = Assert.Single(interop.Calls, static call => call.Kind == "engine_shutdown");
            Assert.Contains(interop.Calls, static call => call.Kind == "set_instance_callbacks" && (string?)call.Value == "clear");
            Assert.Contains(interop.Calls, static call => call.Kind == "set_command_callback" && (string?)call.Value == "clear");
        }

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void Initialize_WithRealNativeLibrary_InitializesAndShutsDown()
        {
            NativeTestLibrary.ConfigureHeadlessQt();
            using QmlSharpEngine engine = new(NativeTestLibrary.Resolve(), static (moduleUri, versionMajor, versionMinor, typeName) => 1);

            engine.Initialize();
            string metricsJson = engine.GetNativeMetricsJson();
            engine.Shutdown();

            Assert.False(string.IsNullOrWhiteSpace(metricsJson));
            Assert.False(engine.IsInitialized);
        }

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void LoadRootQml_MissingFile_ThrowsQmlLoadException()
        {
            NativeTestLibrary.ConfigureHeadlessQt();
            using QmlSharpEngine engine = new(NativeTestLibrary.Resolve(), static (moduleUri, versionMajor, versionMinor, typeName) => 1);
            engine.Initialize();

            QmlLoadException exception = Assert.Throws<QmlLoadException>(() => engine.LoadRootQml("missing-root.qml"));

            Assert.Contains("missing-root.qml", exception.QmlPath, StringComparison.Ordinal);
        }

        private static QmlSharpEngine CreateEngine(FakeNativeHostInterop interop)
        {
            return new QmlSharpEngine(new TestArtifactValidator(), interop);
        }

        private static ViewModelSchema CreateSchema()
        {
            return new ViewModelSchema(
                "1.0",
                "ManagedCounterViewModel",
                "ManagedTests",
                "QmlSharp.Managed.Tests",
                new QmlVersion(1, 0),
                1,
                "MainView::__qmlsharp_vm0",
                [
                    new StateEntry("title", "string", DefaultValue: null, ReadOnly: false, MemberId: 0)
                ],
                [],
                [],
                new LifecycleInfo(OnMounted: false, OnUnmounting: false, HotReload: false));
        }

        private sealed class TestArtifactValidator : QmlSharp.Host.ArtifactValidation.IArtifactValidator
        {
            public QmlSharp.Host.ArtifactValidation.ArtifactValidationResult Validate(string distDirectory)
            {
                return Valid();
            }

            public QmlSharp.Host.ArtifactValidation.ArtifactValidationResult Validate(
                QmlSharp.Host.ArtifactValidation.ArtifactValidationRequest request)
            {
                return Valid();
            }

            public QmlSharp.Host.ArtifactValidation.ArtifactValidationResult ValidateSchemaRegistrationResult(
                string schemaFilePath,
                int nativeResultCode,
                string? nativeError)
            {
                return Valid();
            }

            private static QmlSharp.Host.ArtifactValidation.ArtifactValidationResult Valid()
            {
                return new QmlSharp.Host.ArtifactValidation.ArtifactValidationResult(
                    IsValid: true,
                    Diagnostics: Array.Empty<QmlSharp.Host.ArtifactValidation.ArtifactDiagnostic>());
            }
        }
    }
}
