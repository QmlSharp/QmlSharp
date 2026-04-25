using QmlSharp.Qml.Ast.Tests.Helpers;

namespace QmlSharp.Qml.Ast.Tests.Contracts
{
    [Trait("Category", TestCategories.Coverage)]
    public sealed class EnumInventoryTests
    {
        [Fact]
        public void ASTI_01_All_public_enum_values_are_unique()
        {
            AssertEnumValuesAreUnique<NodeKind>();
            AssertEnumValuesAreUnique<BindingValueKind>();
            AssertEnumValuesAreUnique<PragmaName>();
            AssertEnumValuesAreUnique<ImportKind>();
            AssertEnumValuesAreUnique<SignalHandlerForm>();
            AssertEnumValuesAreUnique<DiagnosticSeverity>();
            AssertEnumValuesAreUnique<DiagnosticCode>();
        }

        [Fact]
        public void ASTI_02_Enum_inventory_counts_match_expected_contract()
        {
            Assert.Equal(18, Enum.GetValues<NodeKind>().Length);
            Assert.Equal(9, Enum.GetValues<BindingValueKind>().Length);
            Assert.Equal(8, Enum.GetValues<PragmaName>().Length);
            Assert.Equal(3, Enum.GetValues<ImportKind>().Length);
            Assert.Equal(3, Enum.GetValues<SignalHandlerForm>().Length);
            Assert.Equal(3, Enum.GetValues<DiagnosticSeverity>().Length);
            Assert.Equal(19, Enum.GetValues<DiagnosticCode>().Length);
        }

        private static void AssertEnumValuesAreUnique<TEnum>()
            where TEnum : struct, Enum
        {
            long[] numericValues = Enum.GetValues<TEnum>()
                .Select(value => Convert.ToInt64(value))
                .ToArray();

            Assert.Equal(numericValues.Length, numericValues.Distinct().Count());
        }
    }
}
