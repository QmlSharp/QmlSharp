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
        string? ErrorMessage);
}
