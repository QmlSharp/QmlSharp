using System.Globalization;

namespace QmlSharp.DevTools.Tests
{
    public sealed class DevConsoleTests
    {
        [Fact]
        [Trait("TestId", "DCO-01")]
        public void Banner_PrintsVersionAndConfig()
        {
            ConsoleHarness harness = ConsoleHarness.Create();
            DevServerOptions options = CreateServerOptions(projectRoot: "C:/repo/CounterApp");

            harness.Console.Banner("0.1.0-test", options);

            string output = harness.Output();
            Assert.Contains("QmlSharp 0.1.0-test", output, StringComparison.Ordinal);
            Assert.Contains("C:/repo/CounterApp", output, StringComparison.Ordinal);
            Assert.Contains("src", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DCO-02")]
        public void BuildStart_PrintsFileCount()
        {
            ConsoleHarness harness = ConsoleHarness.Create();

            harness.Console.BuildStart(3);

            string output = harness.Output();
            Assert.Contains("[build]", output, StringComparison.Ordinal);
            Assert.Contains("3 file(s)", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DCO-03")]
        public void BuildSuccess_PrintsElapsedAndCheckmark()
        {
            ConsoleHarness harness = ConsoleHarness.Create();

            harness.Console.BuildSuccess(TimeSpan.FromMilliseconds(125), 4);

            string output = harness.Output();
            Assert.Contains("\u2705", output, StringComparison.Ordinal);
            Assert.Contains("125ms", output, StringComparison.Ordinal);
            Assert.Contains("4 file(s)", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DCO-04")]
        public void BuildError_PrintsErrorCountAndDetails()
        {
            ConsoleHarness harness = ConsoleHarness.Create();
            CompilerDiagnostic first = CreateDiagnostic(
                DiagnosticSeverity.Error,
                "Unexpected token",
                new SourceLocation("src/App.cs", 10, 5));
            CompilerDiagnostic second = CreateDiagnostic(
                DiagnosticSeverity.Warning,
                "Unused binding",
                new SourceLocation("src/Main.cs", 3, 1));

            harness.Console.BuildError(new[] { first, second });

            string output = harness.Output();
            Assert.Contains("\u274c", output, StringComparison.Ordinal);
            Assert.Contains("1 error(s)", output, StringComparison.Ordinal);
            Assert.Contains("1 warning(s)", output, StringComparison.Ordinal);
            Assert.Contains("src/App.cs:10:5", output, StringComparison.Ordinal);
            Assert.Contains("Unexpected token", output, StringComparison.Ordinal);
            Assert.Contains("src/Main.cs:3:1", output, StringComparison.Ordinal);
            Assert.Contains("Unused binding", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DCO-05")]
        public void HotReloadSuccess_PrintsMatchedCount()
        {
            ConsoleHarness harness = ConsoleHarness.Create();
            HotReloadResult result = new(
                Success: true,
                InstancesMatched: 2,
                InstancesOrphaned: 1,
                InstancesNew: 0,
                new HotReloadPhases(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
                TotalTime: TimeSpan.FromMilliseconds(37),
                ErrorMessage: null,
                FailedStep: null);

            harness.Console.HotReloadSuccess(result);

            string output = harness.Output();
            Assert.Contains("\u2705", output, StringComparison.Ordinal);
            Assert.Contains("37ms", output, StringComparison.Ordinal);
            Assert.Contains("2 matched", output, StringComparison.Ordinal);
            Assert.Contains("1 orphaned", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DCO-06")]
        public void HotReloadError_PrintsErrorMessage()
        {
            ConsoleHarness harness = ConsoleHarness.Create();

            harness.Console.HotReloadError("QML parse error");

            string output = harness.Output();
            Assert.Contains("\u274c", output, StringComparison.Ordinal);
            Assert.Contains("QML parse error", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DCO-07")]
        public void RestartRequired_PrintsWarning()
        {
            ConsoleHarness harness = ConsoleHarness.Create();

            harness.Console.RestartRequired("state property added");

            string output = harness.Output();
            Assert.Contains("\u26a0", output, StringComparison.Ordinal);
            Assert.Contains("Restart required", output, StringComparison.Ordinal);
            Assert.Contains("state property added", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DCO-08")]
        public void LogLevel_WarnOnly_SuppressesInfo()
        {
            ConsoleHarness harness = ConsoleHarness.Create(level: LogLevel.Warn);

            harness.Console.Info("info message");
            harness.Console.Warn("warn message");
            harness.Console.Error("error message");

            string output = harness.Output();
            Assert.DoesNotContain("info message", output, StringComparison.Ordinal);
            Assert.Contains("warn message", output, StringComparison.Ordinal);
            Assert.Contains("error message", output, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DCO-09")]
        public void Color_False_NoAnsiCodes()
        {
            ConsoleHarness harness = ConsoleHarness.Create(color: false);
            CompilerDiagnostic diagnostic = CreateDiagnostic(
                DiagnosticSeverity.Error,
                "Unexpected token",
                new SourceLocation("src/App.cs", 10, 5));

            harness.Console.Info("hello");
            harness.Console.BuildSuccess(TimeSpan.FromMilliseconds(1), 1);
            harness.Console.BuildError(new[] { diagnostic });

            Assert.DoesNotContain("\u001b[", harness.Output(), StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DCO-10")]
        public void ShowTimestamps_True_EachLineHasTimestamp()
        {
            ConsoleHarness harness = ConsoleHarness.Create(showTimestamps: true);
            CompilerDiagnostic diagnostic = CreateDiagnostic(
                DiagnosticSeverity.Error,
                "Unexpected token",
                new SourceLocation("src/App.cs", 10, 5));

            harness.Console.BuildError(new[] { diagnostic });

            string[] lines = harness.Lines();
            Assert.NotEmpty(lines);
            Assert.All(lines, line => Assert.StartsWith("14:15:16 ", line, StringComparison.Ordinal));
        }

        [Fact]
        public void Color_True_IncludesAnsiCodes()
        {
            ConsoleHarness harness = ConsoleHarness.Create(color: true);

            harness.Console.Info("hello");

            Assert.Contains("\u001b[", harness.Output(), StringComparison.Ordinal);
        }

        [Fact]
        public void FileChanged_LongFileList_TruncatesAfterThreeEntries()
        {
            ConsoleHarness harness = ConsoleHarness.Create();
            DateTimeOffset now = DateTimeOffset.Parse(
                "2026-05-06T14:15:16Z",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal);
            FileChangeBatch batch = new(
                ImmutableArray.Create(
                    new FileChange("a.cs", FileChangeKind.Modified, now),
                    new FileChange("b.cs", FileChangeKind.Modified, now),
                    new FileChange("c.cs", FileChangeKind.Modified, now),
                    new FileChange("d.cs", FileChangeKind.Modified, now),
                    new FileChange("e.cs", FileChangeKind.Modified, now)),
                now,
                now);

            harness.Console.FileChanged(batch);

            string output = harness.Output();
            Assert.Contains("a.cs", output, StringComparison.Ordinal);
            Assert.Contains("c.cs", output, StringComparison.Ordinal);
            Assert.Contains("+2 more", output, StringComparison.Ordinal);
            Assert.DoesNotContain("d.cs", output, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildError_DiagnosticWithoutPosition_OmitsFakeZeroZero()
        {
            ConsoleHarness harness = ConsoleHarness.Create();
            CompilerDiagnostic diagnostic = CreateDiagnostic(
                DiagnosticSeverity.Error,
                "Unexpected token",
                SourceLocation.FileOnly("src/App.cs"));

            harness.Console.BuildError(new[] { diagnostic });

            string output = harness.Output();
            Assert.Contains("src/App.cs", output, StringComparison.Ordinal);
            Assert.DoesNotContain(":0:0", output, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildError_SourceMapAvailable_PrintsOriginalCSharpLocation()
        {
            SourceMap sourceMap = new(
                "1.0",
                "src/CounterView.cs",
                "CounterView.qml",
                ImmutableArray.Create(new SourceMapMapping(
                    outputLine: 15,
                    outputColumn: 10,
                    sourceFilePath: "src/CounterView.cs",
                    sourceLine: 42,
                    sourceColumn: 8)));
            ConsoleHarness harness = ConsoleHarness.Create(sourceMapLookup: new SourceMapLookup(new[] { sourceMap }));
            CompilerDiagnostic diagnostic = CreateDiagnostic(
                DiagnosticSeverity.Error,
                "Binding target not found",
                new SourceLocation("dist/qml/CounterView.qml", 15, 12));

            harness.Console.BuildError(new[] { diagnostic });

            string output = harness.Output();
            Assert.Contains("src/CounterView.cs:42:8", output, StringComparison.Ordinal);
            Assert.Contains("generated dist/qml/CounterView.qml:15:12", output, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildError_SourceMapMissing_FallsBackToGeneratedQmlLocation()
        {
            ConsoleHarness harness = ConsoleHarness.Create();
            CompilerDiagnostic diagnostic = CreateDiagnostic(
                DiagnosticSeverity.Error,
                "Binding target not found",
                new SourceLocation("dist/qml/CounterView.qml", 15, 12));

            harness.Console.BuildError(new[] { diagnostic });

            string output = harness.Output();
            Assert.Contains("dist/qml/CounterView.qml:15:12", output, StringComparison.Ordinal);
            Assert.DoesNotContain("generated dist/qml/CounterView.qml", output, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildError_MultipleDiagnostics_PrintsAllDiagnostics()
        {
            ConsoleHarness harness = ConsoleHarness.Create();
            CompilerDiagnostic primary = CreateDiagnostic(
                DiagnosticSeverity.Error,
                "Primary error",
                new SourceLocation("src/App.cs", 1, 2));
            CompilerDiagnostic additional = CreateDiagnostic(
                DiagnosticSeverity.Info,
                "Additional context",
                SourceLocation.FileOnly("src/App.cs"));

            harness.Console.BuildError(new[] { primary, additional });

            string output = harness.Output();
            Assert.Contains("Primary error", output, StringComparison.Ordinal);
            Assert.Contains("Additional context", output, StringComparison.Ordinal);
        }

        [Fact]
        public void Constructor_InjectableConsoleWriter_IsUsed()
        {
            ConsoleCapture capture = new();
            ManualDevToolsClock clock = CreateClock();
            DevConsole console = new(
                new DevConsoleOptions(Level: LogLevel.Debug, Color: false, ShowTimestamps: false),
                capture,
                clock);

            console.Info("captured");

            Assert.Contains("captured", capture.GetOutput(), StringComparison.Ordinal);
        }

        private static CompilerDiagnostic CreateDiagnostic(
            DiagnosticSeverity severity,
            string message,
            SourceLocation? location)
        {
            return new CompilerDiagnostic(
                DiagnosticCodes.InvalidStateAttribute,
                severity,
                message,
                location);
        }

        private static DevServerOptions CreateServerOptions(string projectRoot = "C:/repo")
        {
            FileWatcherOptions watcherOptions = new(ImmutableArray.Create("src", "views"));
            return new DevServerOptions(projectRoot, watcherOptions, new DevConsoleOptions());
        }

        private static ManualDevToolsClock CreateClock()
        {
            return new ManualDevToolsClock
            {
                UtcNow = DateTimeOffset.Parse(
                    "2026-05-06T14:15:16Z",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal),
            };
        }

        private sealed class ConsoleHarness
        {
            private readonly StringWriter writer;

            private ConsoleHarness(StringWriter writer, DevConsole console)
            {
                this.writer = writer;
                Console = console;
            }

            public DevConsole Console { get; }

            public static ConsoleHarness Create(
                LogLevel level = LogLevel.Debug,
                bool color = false,
                bool showTimestamps = false,
                ISourceMapLookup? sourceMapLookup = null)
            {
                StringWriter writer = new(CultureInfo.InvariantCulture);
                ManualDevToolsClock clock = CreateClock();
                DevConsole console = new(
                    new DevConsoleOptions(level, color, showTimestamps, writer),
                    clock,
                    sourceMapLookup);
                return new ConsoleHarness(writer, console);
            }

            public string Output()
            {
                return writer.ToString();
            }

            public string[] Lines()
            {
                return writer.ToString()
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            }
        }
    }
}
