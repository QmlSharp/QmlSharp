using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Tests.Helpers;
using QmlSharp.Registry.Tests.Scanning;

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

        [Fact]
        public void Diagnostic_code_inventory_uses_actual_tests_for_registry_scanner_missing_file_diagnostics()
        {
            Dictionary<string, string?> coveringTestsByCode = DiagnosticCodeInventory.Entries
                .ToDictionary(entry => entry.Code, entry => entry.CoveringTestId, StringComparer.Ordinal);

            Assert.Equal(nameof(QtTypeScannerTests.Scan_returns_REG002_when_no_qmltypes_are_found), coveringTestsByCode[DiagnosticCodes.NoQmltypesFound]);
            Assert.Equal(nameof(QtTypeScannerTests.Scan_returns_REG003_when_no_qmldir_files_are_found), coveringTestsByCode[DiagnosticCodes.NoQmldirFound]);
            Assert.Equal(nameof(QtTypeScannerTests.Scan_returns_REG004_when_no_metatypes_are_found), coveringTestsByCode[DiagnosticCodes.NoMetatypesFound]);
        }

        [Fact]
        public void Scenario_prefixed_scanner_tests_use_unique_ids()
        {
            string[] duplicateScenarioIds = typeof(QtTypeScannerTests).Assembly
                .GetTypes()
                .Where(type => type.Namespace == "QmlSharp.Registry.Tests.Scanning")
                .SelectMany(type => type.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly))
                .Where(IsTestMethod)
                .Select(method => GetScenarioId(method.Name))
                .Where(static scenarioId => scenarioId is not null)
                .Cast<string>()
                .GroupBy(scenarioId => scenarioId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(scenarioId => scenarioId, StringComparer.Ordinal)
                .ToArray();

            Assert.Empty(duplicateScenarioIds);
        }

        private static string[] GetDiagnosticCodes()
        {
            return typeof(DiagnosticCodes)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()!)
                .ToArray();
        }

        private static bool IsTestMethod(System.Reflection.MethodInfo method)
        {
            return method.GetCustomAttributes(inherit: true)
                .Any(attribute => attribute is FactAttribute);
        }

        private static string? GetScenarioId(string methodName)
        {
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
                methodName,
                @"^(SCN_[^_]+)",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
