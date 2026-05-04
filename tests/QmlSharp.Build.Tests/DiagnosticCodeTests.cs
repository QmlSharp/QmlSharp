using System.Reflection;

namespace QmlSharp.Build.Tests
{
    public sealed class DiagnosticCodeTests
    {
        private static readonly ImmutableArray<string> ExpectedCodes = ImmutableArray.Create(
            "QMLSHARP-B001",
            "QMLSHARP-B002",
            "QMLSHARP-B003",
            "QMLSHARP-B004",
            "QMLSHARP-B010",
            "QMLSHARP-B011",
            "QMLSHARP-B012",
            "QMLSHARP-B020",
            "QMLSHARP-B021",
            "QMLSHARP-B022",
            "QMLSHARP-B023",
            "QMLSHARP-B030",
            "QMLSHARP-B031",
            "QMLSHARP-B040",
            "QMLSHARP-B041",
            "QMLSHARP-B050",
            "QMLSHARP-B051",
            "QMLSHARP-B060",
            "QMLSHARP-B061",
            "QMLSHARP-B070",
            "QMLSHARP-B071",
            "QMLSHARP-B072",
            "QMLSHARP-B090");

        [Fact]
        public void BuildDiagnosticCode_DefinesEveryStep0801Code()
        {
            ImmutableArray<string> actual = GetPublicDiagnosticCodes();

            Assert.Equal(ExpectedCodes.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
        }

        [Fact]
        public void BuildDiagnosticCode_AllCodesAreUnique()
        {
            ImmutableArray<string> actual = GetPublicDiagnosticCodes();

            Assert.Equal(actual.Length, actual.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void BuildDiagnosticCode_AllCodesUseBuildPattern()
        {
            foreach (string code in GetPublicDiagnosticCodes())
            {
                Assert.Matches("^QMLSHARP-B0[0-9]{2}$", code);
            }
        }

        private static ImmutableArray<string> GetPublicDiagnosticCodes()
        {
            return typeof(BuildDiagnosticCode)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(static field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                .Select(static field => (string?)field.GetRawConstantValue())
                .Where(static value => value is not null)
                .Select(static value => value!)
                .ToImmutableArray();
        }
    }
}
