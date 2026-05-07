using QmlSharp.DevTools;
using QmlSharp.Integration.Tests.Fixtures;

namespace QmlSharp.Integration.Tests.DevTools
{
    public sealed class FullFlowTests
    {
        [Fact]
        [Trait("TestId", "INT-01")]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task FileChangeHotReloadSucceeds_INT_01()
        {
            await using DevFlowHarness harness = DevFlowHarness.Create(DevFlowWatcherKind.Polling);
            await harness.StartAsync();

            harness.Fixture.ApplyChangedView();

            await DevFlowAssertions.WaitUntilAsync(
                () => harness.NativeHost.ReloadedQmlPaths.Count == 1 && harness.Server.Status == DevServerStatus.Running,
                "File change did not produce a successful hot reload.");

            Assert.Equal(DevServerStatus.Running, harness.Server.Status);
            Assert.False(harness.Overlay.IsVisible);
            _ = Assert.Single(harness.Compiler.Requests);
            string reloadedPath = Assert.Single(harness.NativeHost.ReloadedQmlPaths);
            Assert.EndsWith("CounterView.qml", reloadedPath, StringComparison.Ordinal);
            Assert.Equal(1, harness.Server.Stats.HotReloadCount);
        }

        [Fact]
        [Trait("TestId", "INT-02")]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task CompileErrorShowsOverlay_INT_02()
        {
            await using DevFlowHarness harness = DevFlowHarness.Create();
            await harness.StartAsync();

            harness.Fixture.ApplyBrokenViewModel();
            harness.ControlledWatcher.Emit(harness.Fixture.CreateBatch(harness.Fixture.CounterViewModelPath));

            await DevFlowAssertions.WaitUntilAsync(
                () => harness.Server.Status == DevServerStatus.Error,
                "Broken source did not transition the dev server to Error.");

            Assert.True(harness.Overlay.IsVisible);
            OverlayError overlayError = Assert.Single(harness.OverlayHost.ShownErrors);
            Assert.Equal("Compilation Error", overlayError.Title);
            Assert.Contains("QMLSHARP-", overlayError.Message, StringComparison.Ordinal);
            Assert.Equal(1, harness.Server.Stats.ErrorCount);
        }

        [Fact]
        [Trait("TestId", "INT-03")]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task FixingErrorHidesOverlay_INT_03()
        {
            await using DevFlowHarness harness = DevFlowHarness.Create();
            await harness.StartAsync();
            harness.Fixture.ApplyBrokenViewModel();
            harness.ControlledWatcher.Emit(harness.Fixture.CreateBatch(harness.Fixture.CounterViewModelPath));
            await DevFlowAssertions.WaitUntilAsync(
                () => harness.Server.Status == DevServerStatus.Error,
                "Broken source did not show the overlay before the fix.");

            harness.Fixture.ApplyFixedViewModel();
            harness.ControlledWatcher.Emit(harness.Fixture.CreateBatch(harness.Fixture.CounterViewModelPath));

            await DevFlowAssertions.WaitUntilAsync(
                () => harness.Server.Status == DevServerStatus.Running && harness.Server.Stats.HotReloadCount == 1,
                "Fixing the source did not hide the overlay and restore Running state.");

            Assert.False(harness.Overlay.IsVisible);
            Assert.True(harness.OverlayHost.HideCalls >= 1);
            Assert.Equal(2, harness.Compiler.Requests.Count);
            _ = Assert.Single(harness.NativeHost.ReloadedQmlPaths);
        }

        [Fact]
        [Trait("TestId", "INT-04")]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task StateIsPreservedAfterReload_INT_04()
        {
            await using DevFlowHarness harness = DevFlowHarness.Create();
            await harness.StartAsync();

            harness.Fixture.ApplyChangedView();
            harness.ControlledWatcher.Emit(harness.Fixture.CreateBatch(harness.Fixture.CounterViewPath));

            await DevFlowAssertions.WaitUntilAsync(
                () => harness.NativeHost.SyncedStates.Count == 1 && harness.Server.Status == DevServerStatus.Running,
                "Hot reload did not preserve state through the native-host fixture.");

            string syncedInstanceId = Assert.Single(harness.NativeHost.SyncedInstanceIds);
            IReadOnlyDictionary<string, object?> state = Assert.Single(harness.NativeHost.SyncedStates);
            _ = Assert.Single(harness.NativeHost.RestoredSnapshots);
            Assert.Equal("new-counter", syncedInstanceId);
            Assert.Equal(41, state["count"]);
            Assert.Equal(1, harness.Server.Stats.HotReloadCount);
        }

        [Fact]
        [Trait("TestId", "INT-05")]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task SchemaStructuralChangeTriggersRestart_INT_05()
        {
            await using DevFlowHarness harness = DevFlowHarness.Create();
            await harness.StartAsync();

            harness.Fixture.ApplySchemaChangeViewModel();
            harness.ControlledWatcher.Emit(harness.Fixture.CreateBatch(harness.Fixture.CounterViewModelPath));

            await DevFlowAssertions.WaitUntilAsync(
                () => harness.BuildPipeline.Requests.Count >= 2 && harness.Server.Status == DevServerStatus.Running,
                "Schema structural change did not trigger a restart.");

            Assert.Empty(harness.NativeHost.ReloadedQmlPaths);
            Assert.Equal(2, harness.Server.Stats.BuildCount);
            Assert.Equal(1, harness.Server.Stats.RebuildCount);
            Assert.Equal(2, harness.ControlledWatcher.StartCalls);
            Assert.Equal(1, harness.ControlledWatcher.StopCalls);
        }

        [Fact]
        [Trait("TestId", "INT-06")]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task RapidChangesDebounceIntoOneReload_INT_06()
        {
            await using DevFlowHarness harness = DevFlowHarness.Create(DevFlowWatcherKind.Polling);
            await harness.StartAsync();

            harness.Fixture.AppendRapidViewChange(1);
            harness.Fixture.AppendRapidViewChange(2);
            harness.Fixture.AppendRapidViewChange(3);

            await DevFlowAssertions.WaitUntilAsync(
                () => harness.NativeHost.ReloadedQmlPaths.Count == 1 &&
                    harness.Compiler.ActiveCompiles == 0 &&
                    harness.Server.Status == DevServerStatus.Running,
                "Rapid changes did not debounce into one completed reload.");
            await Task.Delay(500);

            _ = Assert.Single(harness.Compiler.Requests);
            _ = Assert.Single(harness.NativeHost.ReloadedQmlPaths);
            Assert.Equal(1, harness.Server.Stats.HotReloadCount);
        }

        [Fact]
        [Trait("TestId", "INT-07")]
        [Trait("Category", TestCategories.Integration)]
        [Trait("Category", TestCategories.RequiresQt)]
        public async Task ConcurrentChangesAreQueued_INT_07()
        {
            await using DevFlowHarness harness = DevFlowHarness.Create();
            TaskCompletionSource firstCompileStarted = NewSignal();
            TaskCompletionSource releaseFirstCompile = NewSignal();
            int incrementalCompileCalls = 0;
            harness.Compiler.BeforeCompileAsync = async (request, cancellationToken) =>
            {
                if (request.Changes is null)
                {
                    return;
                }

                int call = Interlocked.Increment(ref incrementalCompileCalls);
                if (call == 1)
                {
                    firstCompileStarted.SetResult();
                    await releaseFirstCompile.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
            };
            await harness.StartAsync();

            harness.Fixture.AppendConcurrentViewChange("first-concurrent-change");
            harness.ControlledWatcher.Emit(harness.Fixture.CreateBatch(harness.Fixture.CounterViewPath));
            await firstCompileStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            harness.Fixture.AppendConcurrentViewChange("second-concurrent-change");
            harness.ControlledWatcher.Emit(harness.Fixture.CreateBatch(harness.Fixture.CounterViewPath));

            _ = Assert.Single(harness.Compiler.Requests);
            releaseFirstCompile.SetResult();
            await DevFlowAssertions.WaitUntilAsync(
                () => harness.Compiler.Requests.Count == 2 && harness.Server.Status == DevServerStatus.Running,
                "Concurrent file changes were not queued after the active reload.");

            Assert.Equal(2, harness.Server.Stats.RebuildCount);
            Assert.Equal(2, harness.Server.Stats.HotReloadCount);
            DevFlowCompileRequest secondRequest = harness.Compiler.Requests[1];
            FileChange secondChange = Assert.Single(secondRequest.Changes?.Changes ?? []);
            Assert.Equal(Path.GetFullPath(harness.Fixture.CounterViewPath), secondChange.FilePath);
        }

        private static TaskCompletionSource NewSignal()
        {
            return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
