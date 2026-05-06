using System.Text.Json;

namespace QmlSharp.DevTools.Tests
{
    public sealed class PerfProfilerTests
    {
        [Fact]
        [Trait("TestId", "PRF-01")]
        public void StartSpan_Dispose_RecordsSpan()
        {
            PerfProfiler profiler = CreateProfiler(out ManualDevToolsClock clock);

            using (profiler.StartSpan("compile-view", PerfCategory.Compile))
            {
                clock.Advance(TimeSpan.FromMilliseconds(5));
            }

            PerfRecord record = Assert.Single(profiler.GetRecords());
            Assert.Equal("compile-view", record.Name);
            Assert.Equal(PerfCategory.Compile, record.Category);
            Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(1), record.StartTime);
            Assert.True(record.Duration > TimeSpan.Zero);
        }

        [Fact]
        [Trait("TestId", "PRF-02")]
        public void GetRecords_MultipleSpans_AllRecorded()
        {
            PerfProfiler profiler = CreateProfiler(out ManualDevToolsClock clock);

            for (int index = 0; index < 5; index++)
            {
                using (profiler.StartSpan("span-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), PerfCategory.Compile))
                {
                    clock.Advance(TimeSpan.FromMilliseconds(1));
                }
            }

            IReadOnlyList<PerfRecord> records = profiler.GetRecords();
            Assert.Equal(5, records.Count);
            Assert.Equal("span-0", records[0].Name);
            Assert.Equal("span-4", records[4].Name);
        }

        [Fact]
        [Trait("TestId", "PRF-03")]
        public void GetRecords_Empty_ReturnsEmptyList()
        {
            PerfProfiler profiler = CreateProfiler(out _);

            Assert.Empty(profiler.GetRecords());
            Assert.Equal(0, profiler.GetSummary().TotalSpans);
        }

        [Fact]
        [Trait("TestId", "PRF-04")]
        public void GetSummary_GroupedByCategory_CorrectCounts()
        {
            PerfProfiler profiler = CreateProfiler(out ManualDevToolsClock clock);

            RecordSpan(profiler, clock, "compile-1", PerfCategory.Compile, TimeSpan.FromMilliseconds(1));
            RecordSpan(profiler, clock, "compile-2", PerfCategory.Compile, TimeSpan.FromMilliseconds(1));
            RecordSpan(profiler, clock, "compile-3", PerfCategory.Compile, TimeSpan.FromMilliseconds(1));
            RecordSpan(profiler, clock, "reload-1", PerfCategory.HotReload, TimeSpan.FromMilliseconds(1));
            RecordSpan(profiler, clock, "reload-2", PerfCategory.HotReload, TimeSpan.FromMilliseconds(1));

            PerfSummary summary = profiler.GetSummary();

            Assert.Equal(5, summary.TotalSpans);
            Assert.Equal(3, summary.Categories[PerfCategory.Compile].Count);
            Assert.Equal(2, summary.Categories[PerfCategory.HotReload].Count);
        }

        [Fact]
        [Trait("TestId", "PRF-05")]
        public void GetSummary_CategoryStats_MinMaxAvg()
        {
            PerfProfiler profiler = CreateProfiler(out ManualDevToolsClock clock);

            RecordSpan(profiler, clock, "compile-10", PerfCategory.Compile, TimeSpan.FromMilliseconds(10));
            RecordSpan(profiler, clock, "compile-20", PerfCategory.Compile, TimeSpan.FromMilliseconds(20));
            RecordSpan(profiler, clock, "compile-30", PerfCategory.Compile, TimeSpan.FromMilliseconds(30));

            CategoryStats stats = profiler.GetSummary().Categories[PerfCategory.Compile];

            Assert.Equal(TimeSpan.FromMilliseconds(10), stats.MinTime);
            Assert.Equal(TimeSpan.FromMilliseconds(30), stats.MaxTime);
            Assert.Equal(TimeSpan.FromMilliseconds(20), stats.AvgTime);
            Assert.Equal(TimeSpan.FromMilliseconds(60), stats.TotalTime);
        }

        [Fact]
        [Trait("TestId", "PRF-06")]
        public void Clear_RemovesAllSpans()
        {
            PerfProfiler profiler = CreateProfiler(out ManualDevToolsClock clock);
            RecordSpan(profiler, clock, "compile", PerfCategory.Compile, TimeSpan.FromMilliseconds(1));
            RecordSpan(profiler, clock, "reload", PerfCategory.HotReload, TimeSpan.FromMilliseconds(1));

            profiler.Clear();

            Assert.Empty(profiler.GetRecords());
            Assert.Equal(0, profiler.GetSummary().TotalSpans);
        }

        [Fact]
        [Trait("TestId", "PRF-07")]
        public void ExportChromeTrace_ValidJson()
        {
            PerfProfiler profiler = CreateProfiler(out ManualDevToolsClock clock);
            RecordSpan(profiler, clock, "compile", PerfCategory.Compile, TimeSpan.FromMilliseconds(3));

            using JsonDocument document = JsonDocument.Parse(ExportTrace(profiler));

            JsonElement traceEvents = document.RootElement.GetProperty("traceEvents");
            Assert.Equal(JsonValueKind.Array, traceEvents.ValueKind);
            Assert.Equal(1, traceEvents.GetArrayLength());
        }

        [Fact]
        [Trait("TestId", "PRF-08")]
        public void ExportChromeTrace_SpanFields_Correct()
        {
            PerfProfiler profiler = CreateProfiler(out ManualDevToolsClock clock);
            RecordSpan(profiler, clock, "rebuild", PerfCategory.Compile, TimeSpan.FromMilliseconds(7));

            using JsonDocument document = JsonDocument.Parse(ExportTrace(profiler));
            JsonElement traceEvent = document.RootElement.GetProperty("traceEvents")[0];

            Assert.Equal("rebuild", traceEvent.GetProperty("name").GetString());
            Assert.Equal("Compile", traceEvent.GetProperty("cat").GetString());
            Assert.Equal("X", traceEvent.GetProperty("ph").GetString());
            Assert.True(traceEvent.GetProperty("ts").GetInt64() > 0);
            Assert.Equal(7_000, traceEvent.GetProperty("dur").GetInt64());
            Assert.Equal(1, traceEvent.GetProperty("pid").GetInt32());
            Assert.Equal(1, traceEvent.GetProperty("tid").GetInt32());
            Assert.Equal(JsonValueKind.Object, traceEvent.GetProperty("args").ValueKind);
        }

        [Fact]
        [Trait("TestId", "PRF-09")]
        public void ExportChromeTrace_Metadata_InArgs()
        {
            PerfProfiler profiler = CreateProfiler(out ManualDevToolsClock clock);

            using (IPerfSpan span = profiler.StartSpan("hot-reload", PerfCategory.HotReload))
            {
                span.AddMetadata("changedFiles", 2);
                span.AddMetadata("output", "CounterView.qml");
                span.AddMetadata("success", true);
                clock.Advance(TimeSpan.FromMilliseconds(4));
            }

            PerfRecord record = Assert.Single(profiler.GetRecords());
            Assert.NotNull(record.Metadata);
            Assert.Equal("2", record.Metadata["changedFiles"]);
            Assert.Equal("CounterView.qml", record.Metadata["output"]);
            Assert.Equal("True", record.Metadata["success"]);

            using JsonDocument document = JsonDocument.Parse(ExportTrace(profiler));
            JsonElement args = document.RootElement.GetProperty("traceEvents")[0].GetProperty("args");
            Assert.Equal("2", args.GetProperty("changedFiles").GetString());
            Assert.Equal("CounterView.qml", args.GetProperty("output").GetString());
            Assert.Equal("True", args.GetProperty("success").GetString());
        }

        [Fact]
        [Trait("TestId", "PRF-10")]
        public void IsEnabled_False_StartSpanNoOp()
        {
            CountingClock clock = new();
            PerfProfiler profiler = new(clock, isEnabled: false);

            IPerfSpan span = profiler.StartSpan("disabled", PerfCategory.Compile);
            span.AddMetadata("ignored", "true");
            span.Dispose();

            Assert.False(profiler.IsEnabled);
            Assert.Empty(profiler.GetRecords());
            Assert.Equal(0, clock.TimestampCalls);
            Assert.Equal(0, clock.ElapsedCalls);
        }

        [Fact]
        public void StartSpan_NotDisposed_DoesNotRecord()
        {
            PerfProfiler profiler = CreateProfiler(out _);

            _ = profiler.StartSpan("never-ended", PerfCategory.Compile);

            Assert.Empty(profiler.GetRecords());
        }

        [Fact]
        public void Dispose_CalledTwice_RecordsOnce()
        {
            PerfProfiler profiler = CreateProfiler(out ManualDevToolsClock clock);
            IPerfSpan span = profiler.StartSpan("idempotent", PerfCategory.Compile);
            clock.Advance(TimeSpan.FromMilliseconds(1));

            span.Dispose();
            span.Dispose();

            _ = Assert.Single(profiler.GetRecords());
        }

        [Fact]
        public void AddMetadata_AfterDispose_DoesNotMutateRecord()
        {
            PerfProfiler profiler = CreateProfiler(out ManualDevToolsClock clock);
            IPerfSpan span = profiler.StartSpan("immutable", PerfCategory.Compile);

            span.AddMetadata("success", true);
            clock.Advance(TimeSpan.FromMilliseconds(1));
            span.Dispose();
            span.AddMetadata("late", "ignored");

            PerfRecord record = Assert.Single(profiler.GetRecords());
            Assert.NotNull(record.Metadata);
            Assert.Equal("True", record.Metadata["success"]);
            Assert.False(record.Metadata.ContainsKey("late"));
        }

        [Fact]
        public void InterfaceStartSpan_ReturnsDisposableForSourceCompatibility()
        {
            PerfProfiler profiler = CreateProfiler(out ManualDevToolsClock clock);
            IPerfProfiler contract = profiler;

            using (contract.StartSpan("compile", PerfCategory.Compile))
            {
                clock.Advance(TimeSpan.FromMilliseconds(1));
            }

            _ = Assert.Single(profiler.GetRecords());
        }

        private static PerfProfiler CreateProfiler(out ManualDevToolsClock clock)
        {
            clock = new ManualDevToolsClock
            {
                UtcNow = DateTimeOffset.UnixEpoch.AddSeconds(1),
            };
            return new PerfProfiler(clock);
        }

        private static void RecordSpan(
            PerfProfiler profiler,
            ManualDevToolsClock clock,
            string name,
            PerfCategory category,
            TimeSpan duration)
        {
            using IPerfSpan span = profiler.StartSpan(name, category);
            clock.Advance(duration);
        }

        private static string ExportTrace(PerfProfiler profiler)
        {
            string fileName = "qmlsharp-perf-" + Path.GetRandomFileName() + ".json";
            string outputPath = Path.GetFullPath(Path.Join(Path.GetTempPath(), fileName));
            try
            {
                profiler.ExportChromeTrace(outputPath);
                return File.ReadAllText(outputPath);
            }
            finally
            {
                File.Delete(outputPath);
            }
        }

        private sealed class CountingClock : IDevToolsClock
        {
            public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;

            public int TimestampCalls { get; private set; }

            public int ElapsedCalls { get; private set; }

            public long GetTimestamp()
            {
                TimestampCalls++;
                return 0;
            }

            public TimeSpan GetElapsedTime(long startTimestamp)
            {
                ElapsedCalls++;
                return TimeSpan.Zero;
            }
        }
    }
}
