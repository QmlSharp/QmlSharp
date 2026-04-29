namespace QmlSharp.Qt.Tools.Tests.Helpers
{
    internal sealed record QtAvailability(
        bool IsAvailable,
        string? QtDir,
        string? ToolPath,
        string SkipReason);
}
