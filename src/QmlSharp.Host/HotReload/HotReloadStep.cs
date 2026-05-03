namespace QmlSharp.Host.HotReload
{
    /// <summary>The managed hot reload protocol phases.</summary>
    public enum HotReloadStep
    {
        /// <summary>Managed state and native runtime snapshot capture.</summary>
        Capture,

        /// <summary>Native QML tree destruction and new QML loading.</summary>
        Reload,

        /// <summary>Managed state hydration into matched new instances.</summary>
        Hydrate,

        /// <summary>Native runtime snapshot restoration.</summary>
        Restore
    }
}
