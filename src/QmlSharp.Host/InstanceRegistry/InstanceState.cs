namespace QmlSharp.Host.Instances
{
    /// <summary>Lifecycle state for a managed ViewModel instance.</summary>
    public enum InstanceState
    {
        /// <summary>The native instance exists, but managed hydration is not complete.</summary>
        Pending,

        /// <summary>The instance is ready for state, command, and effect routing.</summary>
        Active,

        /// <summary>The native instance has been destroyed.</summary>
        Destroyed
    }
}
