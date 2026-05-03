using System.Text.Json;
using QmlSharp.Host.Exceptions;
using QmlSharp.Host.Interop;
using QmlSharp.Host.Tests.Fixtures;

namespace QmlSharp.Host.Tests.Interop
{
    public sealed class NativeHostLibraryTests
    {
        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void GetAbiVersion_BuiltNativeLibrary_ReturnsLockedVersion()
        {
            using NativeHostLibrary library = CreateLibrary();

            Assert.Equal(NativeHostAbi.SupportedAbiVersion, library.GetAbiVersion());
        }

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void EngineInit_EmptyArguments_ReturnsNonZeroHandle_PIN_01()
        {
            using NativeHostLibrary library = CreateLibrary();
            IntPtr engine = library.EngineInit(Array.Empty<string>());
            try
            {
                Assert.NotEqual(IntPtr.Zero, engine);
            }
            finally
            {
                library.EngineShutdown(engine);
            }
        }

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void RegisterType_UnicodeModuleUri_MarshalsUtf8_PIN_02()
        {
            using NativeHostLibrary library = CreateLibrary();
            IntPtr engine = InitializeEngine(library);
            string? capturedModuleUri = null;
            string? capturedTypeName = null;
            try
            {
                int resultCode = library.RegisterType(
                    engine,
                    "QmlSharp.Native.Über",
                    1,
                    2,
                    "Utf8Counter",
                    "schema-snow-雪",
                    "Utf8View::__qmlsharp_vm0",
                    (moduleUri, versionMajor, versionMinor, typeName) =>
                    {
                        capturedModuleUri = moduleUri;
                        capturedTypeName = typeName;
                        return 42;
                    });

                Assert.Equal(0, resultCode);
                Assert.Equal("QmlSharp.Native.Über", capturedModuleUri);
                Assert.Equal("Utf8Counter", capturedTypeName);
            }
            finally
            {
                library.EngineShutdown(engine);
            }
        }

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void SyncStateString_UnicodeUnknownInstance_PreservesUtf8InNativeError_PIN_03()
        {
            using NativeHostLibrary library = CreateLibrary();

            int resultCode = library.SyncStateString("missing-雪", "title", "hello");
            string? error = library.GetLastError();

            Assert.Equal(-3, resultCode);
            Assert.Contains("missing-雪", error, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void SyncStateInt_Int32BoundaryValue_ReturnsDeterministicInstanceError_PIN_04()
        {
            using NativeHostLibrary library = CreateLibrary();

            int resultCode = library.SyncStateInt("missing-int-instance", "count", int.MinValue);
            string? error = library.GetLastError();

            Assert.Equal(-3, resultCode);
            Assert.Contains("missing-int-instance", error, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void CaptureSnapshot_InitializedEngine_ReturnsJsonAndFreesNativeString_PIN_05()
        {
            using NativeHostLibrary library = CreateLibrary();
            IntPtr engine = InitializeEngine(library);
            try
            {
                string? snapshot = library.CaptureSnapshot(engine);

                Assert.False(string.IsNullOrWhiteSpace(snapshot));
                using JsonDocument document = JsonDocument.Parse(snapshot);
                Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
                Assert.True(document.RootElement.TryGetProperty("instances", out JsonElement instances));
                Assert.Equal(JsonValueKind.Array, instances.ValueKind);
            }
            finally
            {
                library.EngineShutdown(engine);
            }
        }

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void GetLastError_AfterInvalidHandleFailure_ReturnsNativeMessage_PIN_06()
        {
            using NativeHostLibrary library = CreateLibrary();

            int resultCode = library.ReloadQml(IntPtr.Zero, "missing.qml");
            string? error = library.GetLastError();

            Assert.Equal(-2, resultCode);
            Assert.Contains("non-null engine handle", error, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void FreeString_ValidNativePointer_DoesNotThrow_PIN_07()
        {
            using NativeHostLibrary library = CreateLibrary();
            _ = library.ReloadQml(IntPtr.Zero, "missing.qml");
            IntPtr errorPointer = library.GetLastErrorPointerForTesting();

            Exception? exception = Record.Exception(() => library.FreeString(errorPointer));

            Assert.NotEqual(IntPtr.Zero, errorPointer);
            Assert.Null(exception);
        }

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void FreeString_NullPointer_DoesNotThrow_PIN_08()
        {
            using NativeHostLibrary library = CreateLibrary();

            Exception? exception = Record.Exception(() => library.FreeString(IntPtr.Zero));

            Assert.Null(exception);
        }

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void PostToMainThread_ManagedDelegateSurvivesUntilNativeCallback()
        {
            using NativeHostLibrary library = CreateLibrary();
            IntPtr engine = InitializeEngine(library);
            bool called = false;

            library.PostToMainThread(() =>
            {
                called = true;
                library.EngineShutdown(engine);
            });
            GC.Collect();
            GC.WaitForPendingFinalizers();

            int exitCode = library.EngineExec(engine);

            Assert.Equal(0, exitCode);
            Assert.True(called);
        }

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void RegisterModule_InvalidTypeName_ReturnsInvalidArgumentAndLastError()
        {
            using NativeHostLibrary library = CreateLibrary();
            IntPtr engine = InitializeEngine(library);
            try
            {
                QmlSharpTypeRegistrationEntry[] entries =
                [
                    new(
                        "invalidType",
                        "schema-invalid",
                        "InvalidView::__qmlsharp_vm0",
                        static (moduleUri, versionMajor, versionMinor, typeName) => 1)
                ];

                int resultCode = library.RegisterModule(engine, "QmlSharp.Native.InvalidArguments", 1, 0, entries);
                string? error = library.GetLastError();

                Assert.Equal(-2, resultCode);
                Assert.Contains("uppercase", error, StringComparison.Ordinal);
            }
            finally
            {
                library.EngineShutdown(engine);
            }
        }

        [Fact]
        [Trait("Category", TestCategories.RequiresNative)]
        public void PostToMainThread_BeforeEngineInit_ThrowsManagedNativeHostException()
        {
            using NativeHostLibrary library = CreateLibrary();

            NativeHostException exception = Assert.Throws<NativeHostException>(() => library.PostToMainThread(static () => { }));

            Assert.Contains("initialized Qt application", exception.Message, StringComparison.Ordinal);
        }

        private static NativeHostLibrary CreateLibrary()
        {
            NativeTestLibrary.ConfigureHeadlessQt();
            return new NativeHostLibrary(NativeTestLibrary.Resolve());
        }

        private static IntPtr InitializeEngine(NativeHostLibrary library)
        {
            IntPtr engine = library.EngineInit(Array.Empty<string>());
            Assert.NotEqual(IntPtr.Zero, engine);
            return engine;
        }
    }
}
