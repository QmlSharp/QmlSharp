namespace QmlSharp.Host.ArtifactValidation
{
    /// <summary>Severity for startup artifact validation diagnostics.</summary>
    public enum ArtifactDiagnosticSeverity
    {
        /// <summary>The host may continue.</summary>
        Info,

        /// <summary>The host may continue, but the artifact set is incomplete or suspicious.</summary>
        Warning,

        /// <summary>The host must not start.</summary>
        Error
    }
}
