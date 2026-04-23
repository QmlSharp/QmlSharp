#pragma warning disable MA0048

namespace QmlSharp.Registry.Diagnostics
{
    /// <summary>Severity level for registry diagnostics.</summary>
    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>
    /// A diagnostic message produced during scanning, parsing, or normalization.
    /// Diagnostics are collected rather than thrown — the pipeline continues
    /// and reports all issues at the end.
    /// </summary>
    public sealed record RegistryDiagnostic(
        DiagnosticSeverity Severity,
        string Code,
        string Message,
        string? FilePath,
        int? Line,
        int? Column);

    /// <summary>Diagnostic code constants.</summary>
    public static class DiagnosticCodes
    {
        public const string InvalidQtDir = "REG001";
        public const string NoQmltypesFound = "REG002";
        public const string NoQmldirFound = "REG003";
        public const string NoMetatypesFound = "REG004";

        public const string QmltypesSyntaxError = "REG010";
        public const string QmltypesUnexpectedToken = "REG011";
        public const string QmldirSyntaxError = "REG020";
        public const string QmldirUnknownDirective = "REG021";
        public const string MetatypesJsonError = "REG030";
        public const string MetatypesMissingField = "REG031";

        public const string TypeConflict = "REG040";
        public const string UnresolvedPrototype = "REG041";
        public const string UnresolvedAttachedType = "REG042";
        public const string CircularInheritance = "REG043";
        public const string DuplicateExport = "REG044";

        public const string SnapshotVersionMismatch = "REG050";
        public const string SnapshotCorrupt = "REG051";
    }
}

#pragma warning restore MA0048
