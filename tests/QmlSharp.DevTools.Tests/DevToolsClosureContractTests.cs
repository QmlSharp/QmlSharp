namespace QmlSharp.DevTools.Tests
{
    public sealed class DevToolsClosureContractTests
    {
        private static readonly System.Text.RegularExpressions.Regex TestIdRegex = new(
            "Trait\\(\"TestId\",\\s*\"(?<id>[^\"]+)\"\\)",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        [Fact]
        public void TestSpecIds_AllFunctionalAndPerformanceIds_AreMappedToTests()
        {
            ImmutableHashSet<string> expectedFunctionalIds = CreateExpectedFunctionalIds();
            ImmutableHashSet<string> expectedPerformanceIds = CreateExpectedPerformanceIds();
            ImmutableHashSet<string> expectedIds = expectedFunctionalIds
                .Union(expectedPerformanceIds);
            ImmutableHashSet<string> mappedIds = ReadMappedTestIds()
                .Select(static row => row.Id)
                .ToImmutableHashSet(StringComparer.Ordinal);

            ImmutableArray<string> missingIds = expectedIds
                .Except(mappedIds)
                .OrderBy(static id => id, StringComparer.Ordinal)
                .ToImmutableArray();

            Assert.True(
                missingIds.IsEmpty,
                "Missing dev-tools TestId mappings: " + string.Join(", ", missingIds));
            Assert.Equal(92, expectedFunctionalIds.Count);
            Assert.Equal(12, expectedPerformanceIds.Count);
        }

        [Fact]
        public void PerformanceBenchmarks_AllPbmIdsUsePerformanceCategory()
        {
            string repositoryRoot = DevToolsTestFixtures.FindRepositoryRoot();
            ImmutableArray<MappedTestId> mappedIds = ReadMappedTestIds();
            ImmutableHashSet<string> expectedPerformanceIds = CreateExpectedPerformanceIds();

            foreach (MappedTestId mappedId in mappedIds.Where(row => expectedPerformanceIds.Contains(row.Id)))
            {
                string relativePath = Path.GetRelativePath(repositoryRoot, mappedId.FilePath).Replace('\\', '/');
                string fileText = File.ReadAllText(mappedId.FilePath);

                Assert.StartsWith("tests/QmlSharp.DevTools.Tests/Benchmarks/", relativePath, StringComparison.Ordinal);
                Assert.Contains(
                    "Trait(\"Category\", DevToolsTestCategories.Performance)",
                    fileText,
                    StringComparison.Ordinal);
            }
        }

        [Fact]
        public void IntegrationTraceabilityIds_AreEnvironmentGatedInIntegrationProject()
        {
            string repositoryRoot = DevToolsTestFixtures.FindRepositoryRoot();
            ImmutableArray<MappedTestId> mappedIds = ReadMappedTestIds();

            foreach (MappedTestId mappedId in mappedIds.Where(static row => row.Id.StartsWith("INT-", StringComparison.Ordinal)))
            {
                string relativePath = Path.GetRelativePath(repositoryRoot, mappedId.FilePath).Replace('\\', '/');
                string fileText = File.ReadAllText(mappedId.FilePath);

                Assert.StartsWith("tests/QmlSharp.Integration.Tests/DevTools/", relativePath, StringComparison.Ordinal);
                Assert.Contains("Trait(\"Category\", TestCategories.Integration)", fileText, StringComparison.Ordinal);
                Assert.Contains("Trait(\"Category\", TestCategories.RequiresQt)", fileText, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void DevToolsProductCode_DoesNotWriteToolchainContractArtifacts()
        {
            string repositoryRoot = DevToolsTestFixtures.FindRepositoryRoot();
            string devToolsRoot = Path.Join(repositoryRoot, "src", "QmlSharp.DevTools");
            string[] contractArtifactNames =
            [
                ".schema.json",
                ".qml.map",
                "event-bindings.json",
                "manifest.json",
                "qmldir",
                ".qmltypes",
            ];

            foreach (string sourceText in Directory
                .EnumerateFiles(devToolsRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText))
            {
                bool writesFile = sourceText.Contains("WriteAllText", StringComparison.Ordinal) ||
                    sourceText.Contains("WriteAllBytes", StringComparison.Ordinal) ||
                    sourceText.Contains("FileStream", StringComparison.Ordinal) ||
                    sourceText.Contains("Delete(", StringComparison.Ordinal);

                if (!writesFile)
                {
                    continue;
                }

                foreach (string artifactName in contractArtifactNames)
                {
                    Assert.DoesNotContain(artifactName, sourceText, StringComparison.Ordinal);
                }
            }
        }

        private static ImmutableHashSet<string> CreateExpectedFunctionalIds()
        {
            ImmutableHashSet<string>.Builder builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
            AddRange(builder, "FWA", 12);
            AddRange(builder, "HRO", 14);
            AddRange(builder, "EOV", 8);
            AddRange(builder, "DCO", 10);
            AddRange(builder, "DSV", 16);
            AddRange(builder, "RPL", 12);
            AddRange(builder, "PRF", 10);
            AddRange(builder, "INT", 10);
            return builder.ToImmutable();
        }

        private static ImmutableHashSet<string> CreateExpectedPerformanceIds()
        {
            ImmutableHashSet<string>.Builder builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
            AddRange(builder, "PBM", 12);
            return builder.ToImmutable();
        }

        private static void AddRange(ImmutableHashSet<string>.Builder builder, string prefix, int count)
        {
            for (int index = 1; index <= count; index++)
            {
                _ = builder.Add(prefix + "-" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        private static ImmutableArray<MappedTestId> ReadMappedTestIds()
        {
            ImmutableArray<MappedTestId>.Builder builder = ImmutableArray.CreateBuilder<MappedTestId>();

            foreach (string sourceFile in EnumerateTraceabilitySourceFiles())
            {
                string sourceText = File.ReadAllText(sourceFile);
                foreach (System.Text.RegularExpressions.Match match in TestIdRegex.Matches(sourceText))
                {
                    builder.Add(new MappedTestId(match.Groups["id"].Value, sourceFile));
                }
            }

            return builder.ToImmutable();
        }

        private static IEnumerable<string> EnumerateTraceabilitySourceFiles()
        {
            string repositoryRoot = DevToolsTestFixtures.FindRepositoryRoot();
            string devToolsTestsRoot = Path.Join(repositoryRoot, "tests", "QmlSharp.DevTools.Tests");
            string integrationDevToolsRoot = Path.Join(repositoryRoot, "tests", "QmlSharp.Integration.Tests", "DevTools");

            foreach (string sourceFile in Directory.EnumerateFiles(devToolsTestsRoot, "*.cs", SearchOption.AllDirectories))
            {
                yield return sourceFile;
            }

            foreach (string sourceFile in Directory.EnumerateFiles(integrationDevToolsRoot, "*.cs", SearchOption.AllDirectories))
            {
                yield return sourceFile;
            }
        }

        private sealed record MappedTestId(string Id, string FilePath);
    }
}
