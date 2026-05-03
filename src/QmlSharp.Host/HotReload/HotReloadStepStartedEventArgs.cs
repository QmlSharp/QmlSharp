namespace QmlSharp.Host.HotReload
{
    /// <summary>Event data for an observed hot reload protocol phase.</summary>
    public sealed class HotReloadStepStartedEventArgs : EventArgs
    {
        public HotReloadStepStartedEventArgs(HotReloadStep step)
        {
            Step = step;
        }

        /// <summary>The phase that is starting.</summary>
        public HotReloadStep Step { get; }
    }
}
