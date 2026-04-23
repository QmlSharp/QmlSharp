using QmlSharp.Registry.Diagnostics;

namespace QmlSharp.Registry.Tests.Helpers
{
    internal static class DiagnosticCodeInventory
    {
        public static ImmutableArray<DiagnosticCodeCoverageEntry> Entries { get; } =
        [
            new(DiagnosticCodes.InvalidQtDir, DiagnosticCoverageStatus.Pending, null, "Planned for scanner and builder invalid path coverage."),
            new(DiagnosticCodes.NoQmltypesFound, DiagnosticCoverageStatus.Pending, null, "Planned for scanner discovery coverage."),
            new(DiagnosticCodes.NoQmldirFound, DiagnosticCoverageStatus.Pending, null, "Planned for scanner discovery coverage."),
            new(DiagnosticCodes.NoMetatypesFound, DiagnosticCoverageStatus.Pending, null, "Planned for scanner discovery coverage."),
            new(DiagnosticCodes.QmltypesSyntaxError, DiagnosticCoverageStatus.Pending, null, "Planned for qmltypes parser syntax coverage."),
            new(DiagnosticCodes.QmltypesUnexpectedToken, DiagnosticCoverageStatus.Pending, null, "Planned for qmltypes parser token coverage."),
            new(DiagnosticCodes.QmldirSyntaxError, DiagnosticCoverageStatus.Pending, null, "Planned for qmldir parser syntax coverage."),
            new(DiagnosticCodes.QmldirUnknownDirective, DiagnosticCoverageStatus.Pending, null, "Planned for qmldir parser directive coverage."),
            new(DiagnosticCodes.MetatypesJsonError, DiagnosticCoverageStatus.Pending, null, "Planned for metatypes parser JSON coverage."),
            new(DiagnosticCodes.MetatypesMissingField, DiagnosticCoverageStatus.Pending, null, "Planned for metatypes parser missing field coverage."),
            new(DiagnosticCodes.TypeConflict, DiagnosticCoverageStatus.Pending, null, "Planned for normalizer conflict coverage."),
            new(DiagnosticCodes.UnresolvedPrototype, DiagnosticCoverageStatus.Pending, null, "Planned for normalizer inheritance coverage."),
            new(DiagnosticCodes.UnresolvedAttachedType, DiagnosticCoverageStatus.Pending, null, "Planned for normalizer attached type coverage."),
            new(DiagnosticCodes.CircularInheritance, DiagnosticCoverageStatus.Pending, null, "Planned for normalizer cycle coverage."),
            new(DiagnosticCodes.DuplicateExport, DiagnosticCoverageStatus.Pending, null, "Planned for normalizer export conflict coverage."),
            new(DiagnosticCodes.SnapshotVersionMismatch, DiagnosticCoverageStatus.Pending, null, "Planned for snapshot compatibility coverage."),
            new(DiagnosticCodes.SnapshotCorrupt, DiagnosticCoverageStatus.Pending, null, "Planned for snapshot corruption coverage."),
        ];
    }

    internal sealed record DiagnosticCodeCoverageEntry(
        string Code,
        DiagnosticCoverageStatus Status,
        string? CoveringTestId,
        string Notes);

    internal enum DiagnosticCoverageStatus
    {
        Pending,
        Covered,
    }
}
