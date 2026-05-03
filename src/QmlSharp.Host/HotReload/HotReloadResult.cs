namespace QmlSharp.Host.HotReload
{
    /// <summary>Timing and match summary for a native-host hot reload attempt.</summary>
    public sealed record HotReloadResult(
        bool Success,
        int InstancesMatched,
        int InstancesOrphaned,
        TimeSpan CaptureTime,
        TimeSpan ReloadTime,
        TimeSpan HydrateTime,
        TimeSpan RestoreTime,
        string? ErrorMessage)
    {
        /// <summary>New instances that did not match a previous snapshot.</summary>
        public int InstancesNew { get; init; }

        /// <summary>Protocol step that failed, when the reload did not complete.</summary>
        public HotReloadStep? FailedStep { get; init; }

        /// <summary>Native snapshot JSON returned during the capture step.</summary>
        public string? NativeSnapshotJson { get; init; }

        /// <summary>Total measured wall-clock time for the reload attempt.</summary>
        public TimeSpan TotalTime => CaptureTime + ReloadTime + HydrateTime + RestoreTime;
    }
}
