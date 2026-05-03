using QmlSharp.Host.ErrorOverlay;
using QmlSharp.Host.Tests.StateSynchronization;

namespace QmlSharp.Host.Tests.ErrorOverlay
{
    public sealed class ErrorOverlayControllerTests
    {
        [Fact]
        public void Show_StructuredPayload_MapsToNativeOverlayContract()
        {
            FakeNativeHostInterop interop = new();
            ErrorOverlayController overlay = new(interop, new IntPtr(77));
            ErrorOverlayPayload payload = new(
                "Property 'Countt' was not found.",
                new ErrorOverlaySourceLocation("CounterViewModel.cs", 12, 8),
                "QMLSHARP-1001",
                ErrorOverlaySeverity.Error);

            overlay.Show(payload);

            Assert.True(overlay.IsVisible);
            OverlayCall call = Assert.Single(interop.OverlayCalls);
            Assert.True(call.IsShow);
            Assert.Equal(new IntPtr(77), call.EngineHandle);
            Assert.Equal("Error QMLSHARP-1001", call.Title);
            Assert.Equal("Property 'Countt' was not found.", call.Message);
            Assert.Equal("CounterViewModel.cs", call.FilePath);
            Assert.Equal(12, call.Line);
            Assert.Equal(8, call.Column);
        }

        [Fact]
        public void Hide_WhenVisible_CallsNativeHideAndUpdatesVisibility()
        {
            FakeNativeHostInterop interop = new();
            ErrorOverlayController overlay = new(interop, new IntPtr(77));
            overlay.Show(new ErrorOverlayPayload("Broken", Severity: ErrorOverlaySeverity.Warning));

            overlay.Hide();

            Assert.False(overlay.IsVisible);
            Assert.Equal(2, interop.OverlayCalls.Count);
            OverlayCall hideCall = interop.OverlayCalls[1];
            Assert.False(hideCall.IsShow);
            Assert.Equal(new IntPtr(77), hideCall.EngineHandle);
        }

        [Fact]
        public void Hide_WhenNotVisible_IsNoOp()
        {
            FakeNativeHostInterop interop = new();
            ErrorOverlayController overlay = new(interop, new IntPtr(77));

            overlay.Hide();

            Assert.False(overlay.IsVisible);
            Assert.Empty(interop.OverlayCalls);
        }

        [Fact]
        public void Show_FromManagedThread_MarshalsThroughInteropDecisionPoint()
        {
            FakeNativeHostInterop interop = new()
            {
                IsOnMainThread = false
            };
            ErrorOverlayController overlay = new(interop, new IntPtr(77));

            overlay.Show(new ErrorOverlayPayload("Broken", Severity: ErrorOverlaySeverity.Warning));

            Assert.True(overlay.IsVisible);
            Assert.Equal(1, interop.PostToMainThreadCallCount);
            OverlayCall call = Assert.Single(interop.OverlayCalls);
            Assert.Equal("Warning", call.Title);
        }
    }
}
