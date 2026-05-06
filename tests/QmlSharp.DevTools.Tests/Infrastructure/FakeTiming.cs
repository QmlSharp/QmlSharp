namespace QmlSharp.DevTools.Tests.Infrastructure
{
    public sealed class ManualDevToolsClock : IDevToolsClock
    {
        private long timestamp;

        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;

        public long GetTimestamp()
        {
            return timestamp++;
        }

        public TimeSpan GetElapsedTime(long startTimestamp)
        {
            long elapsedTicks = Math.Max(0, timestamp - startTimestamp);
            return TimeSpan.FromTicks(elapsedTicks);
        }
    }

    public sealed class FakeDevToolsTimer : IDevToolsTimer
    {
        public event Action Tick = static () => { };

        public TimeSpan DueTime { get; private set; }

        public TimeSpan Period { get; private set; }

        public bool Disposed { get; private set; }

        public void Change(TimeSpan dueTime, TimeSpan period)
        {
            DueTime = dueTime;
            Period = period;
        }

        public void Fire()
        {
            Tick();
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    public sealed class FakeDevToolsTimerFactory : IDevToolsTimerFactory
    {
        private readonly List<FakeDevToolsTimer> timers = new();

        public IReadOnlyList<FakeDevToolsTimer> Timers => timers;

        public IDevToolsTimer CreateTimer(Action callback)
        {
            FakeDevToolsTimer timer = new();
            timer.Tick += callback;
            timers.Add(timer);
            return timer;
        }
    }

    public static class PerformanceTimingHelper
    {
        public static PerfRecord CreateRecord(
            string name = "compile",
            PerfCategory category = PerfCategory.Compile,
            TimeSpan? duration = null)
        {
            return new PerfRecord(
                name,
                category,
                DateTimeOffset.UnixEpoch,
                duration ?? TimeSpan.FromMilliseconds(1),
                Metadata: null);
        }
    }
}
