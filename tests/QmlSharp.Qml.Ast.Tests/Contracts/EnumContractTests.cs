using QmlSharp.Qml.Ast.Tests.Helpers;

namespace QmlSharp.Qml.Ast.Tests.Contracts
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class EnumContractTests
    {
        [Fact]
        public void ASTC_01_NodeKind_contract_matches_api_design()
        {
            NodeKind[] expected =
            [
                NodeKind.Document,
                NodeKind.Import,
                NodeKind.Pragma,
                NodeKind.ObjectDefinition,
                NodeKind.InlineComponent,
                NodeKind.PropertyDeclaration,
                NodeKind.PropertyAlias,
                NodeKind.Binding,
                NodeKind.GroupedBinding,
                NodeKind.AttachedBinding,
                NodeKind.ArrayBinding,
                NodeKind.BehaviorOn,
                NodeKind.SignalDeclaration,
                NodeKind.SignalHandler,
                NodeKind.FunctionDeclaration,
                NodeKind.EnumDeclaration,
                NodeKind.IdAssignment,
                NodeKind.Comment,
            ];

            Assert.Equal(expected, Enum.GetValues<NodeKind>());
        }

        [Fact]
        public void ASTC_02_BindingValueKind_contract_matches_api_design()
        {
            BindingValueKind[] expected =
            [
                BindingValueKind.NumberLiteral,
                BindingValueKind.StringLiteral,
                BindingValueKind.BooleanLiteral,
                BindingValueKind.NullLiteral,
                BindingValueKind.EnumReference,
                BindingValueKind.ScriptExpression,
                BindingValueKind.ScriptBlock,
                BindingValueKind.ObjectValue,
                BindingValueKind.ArrayValue,
            ];

            Assert.Equal(expected, Enum.GetValues<BindingValueKind>());
        }

        [Fact]
        public void ASTC_03_PragmaName_contract_matches_api_design()
        {
            PragmaName[] expected =
            [
                PragmaName.Singleton,
                PragmaName.ComponentBehavior,
                PragmaName.ListPropertyAssignBehavior,
                PragmaName.FunctionSignatureBehavior,
                PragmaName.NativeMethodBehavior,
                PragmaName.ValueTypeBehavior,
                PragmaName.NativeTextRendering,
                PragmaName.Translator,
            ];

            Assert.Equal(expected, Enum.GetValues<PragmaName>());
        }

        [Fact]
        public void ASTC_04_ImportKind_contract_matches_api_design()
        {
            ImportKind[] expected =
            [
                ImportKind.Module,
                ImportKind.Directory,
                ImportKind.JavaScript,
            ];

            Assert.Equal(expected, Enum.GetValues<ImportKind>());
        }

        [Fact]
        public void ASTC_05_SignalHandlerForm_contract_matches_api_design()
        {
            SignalHandlerForm[] expected =
            [
                SignalHandlerForm.Expression,
                SignalHandlerForm.Block,
                SignalHandlerForm.Arrow,
            ];

            Assert.Equal(expected, Enum.GetValues<SignalHandlerForm>());
        }

        [Fact]
        public void ASTC_06_DiagnosticSeverity_contract_matches_api_design()
        {
            DiagnosticSeverity[] expected =
            [
                DiagnosticSeverity.Error,
                DiagnosticSeverity.Warning,
                DiagnosticSeverity.Info,
            ];

            Assert.Equal(expected, Enum.GetValues<DiagnosticSeverity>());
        }

        [Fact]
        public void ASTC_07_DiagnosticCode_contract_matches_api_design()
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
    }
}
