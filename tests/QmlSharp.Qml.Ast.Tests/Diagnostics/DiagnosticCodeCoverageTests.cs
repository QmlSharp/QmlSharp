using QmlSharp.Qml.Ast.Tests.Helpers;

namespace QmlSharp.Qml.Ast.Tests.Diagnostics
{
    [Trait("Category", TestCategories.Coverage)]
    public sealed class DiagnosticCodeCoverageTests
    {
        [Fact]
        public void ASTD_01_DiagnosticCode_inventory_matches_expected_values()
        {
            DiagnosticCode[] expected =
            [
                DiagnosticCode.E001_DuplicateId,
                DiagnosticCode.E002_InvalidIdFormat,
                DiagnosticCode.E003_DuplicatePropertyName,
                DiagnosticCode.E004_DuplicateSignalName,
                DiagnosticCode.E005_InvalidHandlerNameFormat,
                DiagnosticCode.E006_ConflictingPropertyModifiers,
                DiagnosticCode.E007_InvalidImport,
                DiagnosticCode.E008_DuplicateEnumName,
                DiagnosticCode.E009_InvalidInlineComponentName,
                DiagnosticCode.E010_ExcessiveNestingDepth,
                DiagnosticCode.E100_UnknownType,
                DiagnosticCode.E101_UnknownProperty,
                DiagnosticCode.E102_UnknownSignal,
                DiagnosticCode.E103_UnknownAttachedType,
                DiagnosticCode.E104_RequiredPropertyNotSet,
                DiagnosticCode.E105_ReadonlyPropertyBound,
                DiagnosticCode.E106_InvalidEnumReference,
                DiagnosticCode.E107_UnknownModule,
                DiagnosticCode.W001_UnusedImport,
            ];

            Assert.Equal(expected, Enum.GetValues<DiagnosticCode>());
        }

        [Fact]
        public void ASTD_02_DiagnosticCode_inventory_covers_structural_semantic_and_warning_buckets()
        {
            DiagnosticCode[] allCodes = Enum.GetValues<DiagnosticCode>();

            DiagnosticCode[] structural = allCodes
                .Where(code => code.ToString().StartsWith("E0", StringComparison.Ordinal))
                .ToArray();
            DiagnosticCode[] semantic = allCodes
                .Where(code => code.ToString().StartsWith("E1", StringComparison.Ordinal))
                .ToArray();
            DiagnosticCode[] warning = allCodes
                .Where(code => code.ToString().StartsWith("W", StringComparison.Ordinal))
                .ToArray();

            Assert.Equal(10, structural.Length);
            Assert.Equal(8, semantic.Length);
            DiagnosticCode warningCode = Assert.Single(warning);
            Assert.Equal(DiagnosticCode.W001_UnusedImport, warningCode);
        }
    }
}
