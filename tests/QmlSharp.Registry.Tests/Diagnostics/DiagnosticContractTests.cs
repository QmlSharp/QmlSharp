using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Tests.Helpers;

namespace QmlSharp.Registry.Tests.Diagnostics
{
    public sealed class DiagnosticContractTests
    {
        [Fact]
        public void Diagnostic_codes_are_unique()
        {
            string[] codes = GetDiagnosticCodes();

            Assert.NotEmpty(codes);
            Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void Diagnostic_code_inventory_tracks_every_public_code()
        {
            string[] codes = GetDiagnosticCodes().OrderBy(code => code).ToArray();
            string[] inventoryCodes = DiagnosticCodeInventory.Entries
                .Select(entry => entry.Code)
                .OrderBy(code => code)
                .ToArray();

            Assert.Equal(codes, inventoryCodes);
        }

        private static string[] GetDiagnosticCodes()
        {
            return typeof(DiagnosticCodes)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()!)
                .ToArray();
        }
    }
}
