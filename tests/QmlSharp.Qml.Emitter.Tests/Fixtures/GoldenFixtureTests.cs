using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Fixtures
{
    public sealed class GoldenFixtureTests
    {
        public static TheoryData<string> GoldenFileNames()
        {
            TheoryData<string> data = new();
            foreach (string fileName in GoldenFixtureBuilder.ExpectedFileNames)
            {
                data.Add(fileName);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(GoldenFileNames))]
        [Trait("Category", TestCategories.Golden)]
        public void Golden_GeneratedOutput_MatchesCommittedFile(string fileName)
        {
            GoldenFixture fixture = GoldenFixtureBuilder.Build(fileName);
            IQmlEmitter emitter = new QmlEmitter();

            string actual = emitter.Emit(fixture.Document, fixture.Options);
            string expected = GoldenFileLoader.Load(fileName);

            Assert.Equal(expected, actual);
            Assert.False(string.IsNullOrWhiteSpace(fixture.ParityNote));
        }

        [Fact]
        [Trait("Category", TestCategories.Golden)]
        public void Golden_CommittedFileSet_MatchesExpectedFixtureSet()
        {
            string goldenDirectory = Path.Join(AppContext.BaseDirectory, "Fixtures", "Golden");
            string[] committedFiles = Directory
                .EnumerateFiles(goldenDirectory, "*.qml", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Order(StringComparer.Ordinal)
                .ToArray();
            string[] expectedFiles = GoldenFixtureBuilder.ExpectedFileNames
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expectedFiles, committedFiles);
        }

        [Theory]
        [MemberData(nameof(GoldenFileNames))]
        [Trait("Category", TestCategories.Golden)]
        public void Golden_CommittedFiles_UseLfWithoutBomAndEndWithFinalNewline(string fileName)
        {
            byte[] bytes = GoldenFileLoader.LoadBytes(fileName);

            Assert.NotEmpty(bytes);
            Assert.DoesNotContain((byte)'\r', bytes);
            Assert.Equal((byte)'\n', bytes[^1]);
            Assert.False(HasUtf8Bom(bytes));
        }

        [Theory]
        [MemberData(nameof(GoldenFileNames))]
        [Trait("Category", TestCategories.Golden)]
        public void Golden_GeneratedOutput_UsesCommittedLfPolicy(string fileName)
        {
            GoldenFixture fixture = GoldenFixtureBuilder.Build(fileName);
            IQmlEmitter emitter = new QmlEmitter();

            string actual = emitter.Emit(fixture.Document, fixture.Options);

            LineEndingAssert.ContainsOnlyLf(actual);
            Assert.EndsWith("\n", actual, StringComparison.Ordinal);
        }

        private static bool HasUtf8Bom(byte[] bytes)
        {
            return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        }
    }
}
