using QmlSharp.Registry.Diagnostics;

namespace QmlSharp.Registry.Tests.Helpers
{
    internal static class DiagnosticCodeInventory
    {
        public static ImmutableArray<DiagnosticCodeCoverageEntry> Entries { get; } =
        [
            new(DiagnosticCodes.InvalidQtDir, DiagnosticCoverageStatus.Covered, "SCN-02", "Covered by scanner invalid path and missing qml directory tests."),
            new(DiagnosticCodes.NoQmltypesFound, DiagnosticCoverageStatus.Covered, "Scan_returns_REG002_when_no_qmltypes_are_found", "Covered by the dedicated missing-qmltypes diagnostic test."),
            new(DiagnosticCodes.NoQmldirFound, DiagnosticCoverageStatus.Covered, "Scan_returns_REG003_when_no_qmldir_files_are_found", "Covered by the dedicated missing-qmldir diagnostic test."),
            new(DiagnosticCodes.NoMetatypesFound, DiagnosticCoverageStatus.Covered, "Scan_returns_REG004_when_no_metatypes_are_found", "Covered by the dedicated missing-metatypes diagnostic test."),
            new(DiagnosticCodes.QmltypesSyntaxError, DiagnosticCoverageStatus.Covered, "QTP_30_Invalid_syntax_produces_diagnostic", "Covered by qmltypes parser syntax, tokenizer, and recovery diagnostics tests."),
            new(DiagnosticCodes.QmltypesUnexpectedToken, DiagnosticCoverageStatus.Covered, "Parse_reports_REG011_for_unexpected_tokens_when_context_is_known", "Covered by qmltypes parser and tokenizer unexpected-token recovery tests."),
            new(DiagnosticCodes.QmldirSyntaxError, DiagnosticCoverageStatus.Covered, "Parse_reports_REG020_for_syntax_errors_with_source_coordinates", "Covered by qmldir parser malformed-directive diagnostics with source path, line, and column assertions."),
            new(DiagnosticCodes.QmldirUnknownDirective, DiagnosticCoverageStatus.Covered, "QDP-20", "Covered by qmldir parser unknown-directive warning coverage with source path, line, and column assertions."),
            new(DiagnosticCodes.MetatypesJsonError, DiagnosticCoverageStatus.Covered, "MTP_20_Invalid_JSON_produces_REG030_with_source_location", "Covered by metatypes parser invalid JSON coverage with source path and JSON location assertions."),
            new(DiagnosticCodes.MetatypesMissingField, DiagnosticCoverageStatus.Covered, "Parse_missing_required_fields_produces_REG031_and_continues_with_partial_results", "Covered by metatypes parser required-field shape diagnostics with partial parse recovery assertions."),
            new(DiagnosticCodes.TypeConflict, DiagnosticCoverageStatus.Covered, "NRM_06_Qmltypes_primary_merge_keeps_qmltypes_values_when_metatypes_conflict", "Covered by the qmltypes-primary merge precedence test."),
            new(DiagnosticCodes.UnresolvedPrototype, DiagnosticCoverageStatus.Covered, "NRM_13_Unresolved_prototype_produces_REG041_warning", "Covered by unresolved prototype normalization coverage."),
            new(DiagnosticCodes.UnresolvedAttachedType, DiagnosticCoverageStatus.Covered, "NRM_04_Normalize_single_metatypes_source_only_creates_fallback_types", "Covered by metatypes fallback normalization coverage for unresolved attached types."),
            new(DiagnosticCodes.CircularInheritance, DiagnosticCoverageStatus.Covered, "NRM_11_Circular_inheritance_is_detected", "Covered by circular inheritance normalization coverage."),
            new(DiagnosticCodes.DuplicateExport, DiagnosticCoverageStatus.Covered, "NRM_14_Duplicate_exports_produce_REG044_warning", "Covered by duplicate export normalization coverage."),
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
