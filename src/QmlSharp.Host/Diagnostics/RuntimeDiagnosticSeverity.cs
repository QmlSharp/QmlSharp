namespace QmlSharp.Host.Diagnostics
{
    /// <summary>Severity for raw runtime diagnostics emitted by the native host.</summary>
    public enum RuntimeDiagnosticSeverity
    {
        /// <summary>Informational runtime event.</summary>
        Info,

        /// <summary>Recoverable runtime warning.</summary>
        Warning,

        /// <summary>Runtime error that may prevent startup, reload, or rendering.</summary>
        Error
    }
}
