using System.Globalization;
using System.Text;
using System.Text.Json;

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Records development-time performance spans and exports Chrome Trace JSON.
    /// </summary>
    public sealed class PerfProfiler : IPerfProfiler
    {
        private const int ChromeTracePid = 1;
        private const int ChromeTraceTid = 1;
        private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1_000;

        private static readonly JsonWriterOptions ChromeTraceWriterOptions = new()
        {
            Indented = true,
        };

        private readonly Lock gate = new();
        private readonly List<PerfRecord> records = new();
        private readonly IDevToolsClock clock;

        /// <summary>
        /// Initializes a new performance profiler.
        /// </summary>
        /// <param name="isEnabled">Whether span recording should be enabled.</param>
        public PerfProfiler(bool isEnabled = true)
            : this(SystemDevToolsClock.Instance, isEnabled)
        {
        }

        internal PerfProfiler(IDevToolsClock clock, bool isEnabled = true)
        {
            ArgumentNullException.ThrowIfNull(clock);

            this.clock = clock;
            IsEnabled = isEnabled;
        }

        /// <inheritdoc />
        public bool IsEnabled { get; }

        /// <inheritdoc />
        public IPerfSpan StartSpan(string name, PerfCategory category)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (!Enum.IsDefined(category))
            {
                throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown performance category.");
            }

            if (!IsEnabled)
            {
                return NoOpPerfSpan.Instance;
            }

            return new ActivePerfSpan(this, name, category, clock.UtcNow, clock.GetTimestamp());
        }

        IDisposable IPerfProfiler.StartSpan(string name, PerfCategory category)
        {
            return StartSpan(name, category);
        }

        /// <inheritdoc />
        public IReadOnlyList<PerfRecord> GetRecords()
        {
            lock (gate)
            {
                return records.ToImmutableArray();
            }
        }

        /// <inheritdoc />
        public PerfSummary GetSummary()
        {
            ImmutableArray<PerfRecord> snapshot;
            lock (gate)
            {
                snapshot = records.ToImmutableArray();
            }

            ImmutableDictionary<PerfCategory, CategoryStats>.Builder categoryBuilder =
                ImmutableDictionary.CreateBuilder<PerfCategory, CategoryStats>();

            foreach (IGrouping<PerfCategory, PerfRecord> group in snapshot
                .GroupBy(static record => record.Category)
                .OrderBy(static group => group.Key))
            {
                ImmutableArray<TimeSpan> durations = group
                    .Select(static record => record.Duration)
                    .ToImmutableArray();
                TimeSpan total = SumDurations(durations);
                categoryBuilder[group.Key] = new CategoryStats(
                    durations.Length,
                    total,
                    durations.Min(),
                    durations.Max(),
                    TimeSpan.FromTicks(total.Ticks / durations.Length));
            }

            return new PerfSummary(
                categoryBuilder.ToImmutable(),
                snapshot.Length,
                SumDurations(snapshot.Select(static record => record.Duration)));
        }

        /// <inheritdoc />
        public void Clear()
        {
            lock (gate)
            {
                records.Clear();
            }
        }

        /// <inheritdoc />
        public void ExportChromeTrace(string outputPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

            string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            ImmutableArray<PerfRecord> snapshot;
            lock (gate)
            {
                snapshot = records.ToImmutableArray();
            }

            string json = CreateChromeTraceJson(snapshot);
            File.WriteAllText(outputPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static TimeSpan SumDurations(IEnumerable<TimeSpan> durations)
        {
            long totalTicks = durations.Sum(static duration => duration.Ticks);
            return TimeSpan.FromTicks(totalTicks);
        }

        private static string CreateChromeTraceJson(ImmutableArray<PerfRecord> snapshot)
        {
            using MemoryStream stream = new();
            using (Utf8JsonWriter writer = new(stream, ChromeTraceWriterOptions))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("traceEvents");
                writer.WriteStartArray();

                foreach (PerfRecord record in snapshot)
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", record.Name);
                    writer.WriteString("cat", record.Category.ToString());
                    writer.WriteString("ph", "X");
                    writer.WriteNumber("ts", ToUnixTimeMicroseconds(record.StartTime));
                    writer.WriteNumber("dur", ToDurationMicroseconds(record.Duration));
                    writer.WriteNumber("pid", ChromeTracePid);
                    writer.WriteNumber("tid", ChromeTraceTid);
                    writer.WritePropertyName("args");
                    WriteMetadataObject(writer, record.Metadata);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static long ToUnixTimeMicroseconds(DateTimeOffset timestamp)
        {
            return (timestamp.ToUniversalTime().Ticks - DateTimeOffset.UnixEpoch.Ticks) / TicksPerMicrosecond;
        }

        private static long ToDurationMicroseconds(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                return 0;
            }

            return Math.Max(1, duration.Ticks / TicksPerMicrosecond);
        }

        private static void WriteMetadataObject(
            Utf8JsonWriter writer,
            IReadOnlyDictionary<string, string>? metadata)
        {
            writer.WriteStartObject();

            if (metadata is not null)
            {
                foreach (KeyValuePair<string, string> entry in metadata.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(entry.Key);
                    writer.WriteStringValue(entry.Value);
                }
            }

            writer.WriteEndObject();
        }

        private void Record(PerfRecord record)
        {
            lock (gate)
            {
                records.Add(record);
            }
        }

        private sealed class ActivePerfSpan : IPerfSpan
        {
            private readonly Lock spanGate = new();
            private readonly PerfProfiler owner;
            private readonly string name;
            private readonly PerfCategory category;
            private readonly DateTimeOffset startTime;
            private readonly long startTimestamp;
            private SortedDictionary<string, string>? metadata;
            private bool ended;

            public ActivePerfSpan(
                PerfProfiler owner,
                string name,
                PerfCategory category,
                DateTimeOffset startTime,
                long startTimestamp)
            {
                this.owner = owner;
                this.name = name;
                this.category = category;
                this.startTime = startTime;
                this.startTimestamp = startTimestamp;
            }

            public void AddMetadata(string key, object? value)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(key);

                lock (spanGate)
                {
                    if (ended)
                    {
                        return;
                    }

                    metadata ??= new SortedDictionary<string, string>(StringComparer.Ordinal);
                    metadata[key] = NormalizeMetadataValue(value);
                }
            }

            public void Dispose()
            {
                IReadOnlyDictionary<string, string>? metadataSnapshot;

                lock (spanGate)
                {
                    if (ended)
                    {
                        return;
                    }

                    ended = true;
                    metadataSnapshot = metadata is null || metadata.Count == 0
                        ? null
                        : metadata.ToImmutableDictionary(
                            static pair => pair.Key,
                            static pair => pair.Value,
                            StringComparer.Ordinal);
                }

                TimeSpan duration = owner.clock.GetElapsedTime(startTimestamp);
                if (duration <= TimeSpan.Zero)
                {
                    duration = TimeSpan.FromTicks(1);
                }

                owner.Record(new PerfRecord(
                    name,
                    category,
                    startTime,
                    duration,
                    metadataSnapshot));
            }

            private static string NormalizeMetadataValue(object? value)
            {
                return value switch
                {
                    null => string.Empty,
                    string text => text,
                    DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
                    DateTime dateTime => dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    TimeSpan timeSpan => timeSpan.TotalMilliseconds.ToString(CultureInfo.InvariantCulture),
                    _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
                };
            }
        }

        private sealed class NoOpPerfSpan : IPerfSpan
        {
            public static NoOpPerfSpan Instance { get; } = new();

            public void AddMetadata(string key, object? value)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(key);
            }

            public void Dispose()
            {
            }
        }
    }
}
