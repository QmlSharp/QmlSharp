namespace QmlSharp.DevTools.Tests
{
    public sealed class ReplTests
    {
        [Fact]
        [Trait("TestId", "RPL-01")]
        public async Task EvalAsync_CSharp_SimpleExpression_ReturnsValue()
        {
            await using Repl repl = new();
            await repl.StartAsync();

            ReplResult result = await repl.EvalAsync("1 + 2");

            Assert.True(result.Success);
            Assert.Equal("3", result.Output);
            Assert.Equal("Int32", result.ReturnType);
            Assert.Null(result.Error);
            Assert.True(result.Elapsed > TimeSpan.Zero);
        }

        [Fact]
        [Trait("TestId", "RPL-02")]
        public async Task EvalAsync_CSharp_VariablePersistence_AcrossEvals()
        {
            await using Repl repl = new();
            await repl.StartAsync();

            ReplResult declaration = await repl.EvalAsync("int x = 10;");
            ReplResult result = await repl.EvalAsync("x * 2");

            Assert.True(declaration.Success);
            Assert.True(result.Success);
            Assert.Equal("20", result.Output);
        }

        [Fact]
        [Trait("TestId", "RPL-03")]
        public async Task EvalAsync_CSharp_SyntaxError_ReturnsCompilationError()
        {
            await using Repl repl = new();
            await repl.StartAsync();

            ReplResult result = await repl.EvalAsync("int x =");

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Equal(ReplErrorKind.CompilationError, result.Error.Kind);
            Assert.Contains("CS", result.Output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "RPL-04")]
        public async Task EvalAsync_CSharp_RuntimeException_ReturnsRuntimeError()
        {
            await using Repl repl = new();
            await repl.StartAsync();

            ReplResult result = await repl.EvalAsync("int.Parse(\"boom\")");

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Equal(ReplErrorKind.RuntimeError, result.Error.Kind);
            Assert.Contains("boom", result.Output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "RPL-05")]
        public async Task EvalAsync_CSharp_AsyncExpression_Awaited()
        {
            await using Repl repl = new();
            await repl.StartAsync();

            ReplResult result = await repl.EvalAsync("await Task.FromResult(42)");

            Assert.True(result.Success);
            Assert.Equal("42", result.Output);
            Assert.Equal("Int32", result.ReturnType);
        }

        [Fact]
        [Trait("TestId", "RPL-06")]
        [Trait("Category", DevToolsTestCategories.QmlRepl)]
        public async Task EvalAsync_Qml_SimpleExpression_ReturnsValue()
        {
            FakeNativeHost nativeHost = new()
            {
                QmlEvaluationResult = "4",
            };
            await using Repl repl = new(
                new ReplOptions(DefaultMode: ReplMode.Qml),
                nativeHost,
                devServer: null,
                profiler: null);
            await repl.StartAsync();

            ReplResult result = await repl.EvalAsync("2 + 2");

            Assert.True(result.Success);
            Assert.Equal("4", result.Output);
            Assert.Null(result.ReturnType);
            Assert.Equal("2 + 2", Assert.Single(nativeHost.EvaluatedInputs));
        }

        [Fact]
        [Trait("TestId", "RPL-07")]
        [Trait("Category", DevToolsTestCategories.QmlRepl)]
        public async Task EvalAsync_Qml_InvalidExpression_ReturnsQmlError()
        {
            FakeNativeHost nativeHost = new()
            {
                QmlEvaluationException = new InvalidOperationException("QML syntax error: unexpected token"),
            };
            await using Repl repl = new(
                new ReplOptions(DefaultMode: ReplMode.Qml),
                nativeHost,
                devServer: null,
                profiler: null);
            await repl.StartAsync();

            ReplResult result = await repl.EvalAsync("invalid { qml }}}");

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Equal(ReplErrorKind.QmlError, result.Error.Kind);
            Assert.Contains("QML syntax error", result.Output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "RPL-08")]
        public async Task Mode_Switch_CSharpToQml_Works()
        {
            FakeNativeHost nativeHost = new()
            {
                QmlEvaluationResult = "qml-value",
            };
            await using Repl repl = new(
                new ReplOptions(DefaultMode: ReplMode.CSharp),
                nativeHost,
                devServer: null,
                profiler: null);
            await repl.StartAsync();

            ReplResult modeResult = await repl.EvalAsync(":mode qml");
            ReplResult evalResult = await repl.EvalAsync("Qt.application.active");

            Assert.True(modeResult.Success);
            Assert.Equal(ReplMode.Qml, repl.Mode);
            Assert.True(evalResult.Success);
            Assert.Equal("qml-value", evalResult.Output);
            Assert.Equal("Qt.application.active", Assert.Single(nativeHost.EvaluatedInputs));
        }

        [Fact]
        [Trait("TestId", "RPL-09")]
        public async Task History_AfterEvals_ContainsInputs()
        {
            await using Repl repl = new(new ReplOptions(MaxHistory: 10));
            await repl.StartAsync();

            _ = await repl.EvalAsync("1 + 1");
            _ = await repl.EvalAsync("2 + 2");
            _ = await repl.EvalAsync(":help");

            Assert.Equal(
                new[] { "1 + 1", "2 + 2", ":help" },
                repl.History);
        }

        [Fact]
        [Trait("TestId", "RPL-10")]
        [Trait("Category", DevToolsTestCategories.QmlRepl)]
        public async Task EvalAsync_Cancellation_ReturnsTimeout()
        {
            FakeNativeHost nativeHost = new()
            {
                QmlEvaluationDelay = TimeSpan.FromSeconds(5),
            };
            ReplOptions options = new(
                DefaultMode: ReplMode.Qml,
                EvaluationTimeout: TimeSpan.FromMilliseconds(10));
            await using Repl repl = new(options, nativeHost, devServer: null, profiler: null);
            await repl.StartAsync();

            ReplResult result = await repl.EvalAsync("slow()");

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Equal(ReplErrorKind.Timeout, result.Error.Kind);
        }

        [Fact]
        [Trait("TestId", "RPL-11")]
        public async Task BuiltinCommand_Rebuild_TriggersDevServer()
        {
            FakeDevServer devServer = new();
            await using Repl repl = new(
                new ReplOptions(),
                nativeHost: null,
                devServer,
                profiler: null);
            await repl.StartAsync();

            ReplResult result = await repl.EvalAsync(":rebuild");

            Assert.True(result.Success);
            Assert.Equal(1, devServer.RebuildCalls);
            Assert.Contains("Rebuild succeeded", result.Output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "RPL-12")]
        public async Task BuiltinCommand_Instances_ListsActive()
        {
            FakeNativeHost nativeHost = new()
            {
                Instances = ImmutableArray.Create(
                    DevToolsTestFixtures.InstanceInfo() with
                    {
                        InstanceId = "instance-42",
                        ClassName = "CounterViewModel",
                        CompilerSlotKey = "CounterView::__qmlsharp_vm0",
                    }),
            };
            await using Repl repl = new(
                new ReplOptions(),
                nativeHost,
                devServer: null,
                profiler: null);
            await repl.StartAsync();

            ReplResult result = await repl.EvalAsync(":instances");

            Assert.True(result.Success);
            Assert.Contains("CounterViewModel", result.Output, StringComparison.Ordinal);
            Assert.Contains("instance-42", result.Output, StringComparison.Ordinal);
            Assert.Contains("CounterView::__qmlsharp_vm0", result.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task EvalAsync_CSharp_DefaultReferencesAndImports_AreAvailable()
        {
            await using Repl repl = new();
            await repl.StartAsync();

            ReplResult linqResult = await repl.EvalAsync("Enumerable.Range(1, 3).Sum()");
            ReplResult coreResult = await repl.EvalAsync("typeof(StateAttribute).Name");

            Assert.True(linqResult.Success);
            Assert.Equal("6", linqResult.Output);
            Assert.True(coreResult.Success);
            Assert.Equal("StateAttribute", coreResult.Output);
        }

        [Fact]
        public async Task BuiltinCommand_Unsupported_ReturnsUnsupportedCommandError()
        {
            await using Repl repl = new();
            await repl.StartAsync();

            ReplResult result = await repl.EvalAsync(":unknown");

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Equal(ReplErrorKind.UnsupportedCommand, result.Error.Kind);
        }

        [Fact]
        public async Task BuiltinCommand_HelpAndHistory_ReturnOutput()
        {
            await using Repl repl = new();
            await repl.StartAsync();

            ReplResult help = await repl.EvalAsync(":help");
            ReplResult history = await repl.EvalAsync(":history");

            Assert.True(help.Success);
            Assert.Contains(":rebuild", help.Output, StringComparison.Ordinal);
            Assert.True(history.Success);
            Assert.Contains(":help", history.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task History_MaxHistory_DropsOldestEntries()
        {
            await using Repl repl = new(new ReplOptions(MaxHistory: 3));
            await repl.StartAsync();

            _ = await repl.EvalAsync("1");
            _ = await repl.EvalAsync("2");
            _ = await repl.EvalAsync("3");
            _ = await repl.EvalAsync("4");

            Assert.Equal(new[] { "2", "3", "4" }, repl.History);
        }

        [Fact]
        public async Task HistoryFile_StopAndStart_PersistsEntries()
        {
            string historyFile = Path.Join(Path.GetTempPath(), "qmlsharp-repl-" + Path.GetRandomFileName() + ".json");
            try
            {
                await using (Repl first = new(new ReplOptions(MaxHistory: 10, HistoryFilePath: historyFile)))
                {
                    await first.StartAsync();
                    _ = await first.EvalAsync("1 + 1");
                    _ = await first.EvalAsync("2 + 2");
                    await first.StopAsync();
                }

                await using Repl second = new(new ReplOptions(MaxHistory: 10, HistoryFilePath: historyFile));
                await second.StartAsync();

                Assert.Equal(new[] { "1 + 1", "2 + 2" }, second.History);
            }
            finally
            {
                File.Delete(historyFile);
            }
        }

        [Fact]
        public async Task StartStopLifecycle_IsIdempotentAndResetsRunningState()
        {
            await using Repl repl = new();

            _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await repl.EvalAsync("1"));
            await repl.StartAsync();
            await repl.StartAsync();
            Assert.True(repl.IsRunning);

            await repl.StopAsync();
            await repl.StopAsync();

            Assert.False(repl.IsRunning);
            _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await repl.EvalAsync("1"));
        }
    }
}
