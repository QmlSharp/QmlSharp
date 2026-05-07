using QmlSharp.DevTools;
using QmlSharp.Integration.Tests.Fixtures;

namespace QmlSharp.Integration.Tests.DevTools
{
    public sealed class LifecycleTests
    {
        [Fact]
        [Trait("TestId", "INT-09")]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task StartStopLifecycleHasNoLeaks_INT_09()
        {
            DevFlowHarness harness = DevFlowHarness.Create();
            ControlledFileWatcher watcher = harness.ControlledWatcher;
            try
            {
                await harness.StartAsync();
                Assert.Equal(FileWatcherStatus.Running, watcher.Status);
                Assert.Equal(1, watcher.StartCalls);

                await harness.Server.StopAsync();

                Assert.Equal(DevServerStatus.Idle, harness.Server.Status);
                Assert.Equal(FileWatcherStatus.Idle, watcher.Status);
                Assert.Equal(1, watcher.StopCalls);
                Assert.False(harness.Overlay.IsVisible);
                Assert.Equal(0, harness.Compiler.ActiveCompiles);
                Assert.Equal(0, harness.NativeHost.ActiveOperations);

                await harness.DisposeAsync();

                Assert.Equal(FileWatcherStatus.Disposed, watcher.Status);
                Assert.Equal(1, watcher.DisposeCalls);
                Assert.Equal(0, harness.Compiler.ActiveCompiles);
                Assert.Equal(0, harness.NativeHost.ActiveOperations);
            }
            finally
            {
                await harness.DisposeAsync();
            }
        }
    }
}
