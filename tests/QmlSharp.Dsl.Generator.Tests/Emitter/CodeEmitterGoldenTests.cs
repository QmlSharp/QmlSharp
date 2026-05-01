using QmlSharp.Dsl.Generator.Tests.Fixtures;

namespace QmlSharp.Dsl.Generator.Tests.Emitter
{
    public sealed class CodeEmitterGoldenTests
    {
        [Fact]
        [Trait("Category", "Golden")]
        public void EmitTypeFile_Rectangle_MatchesGoldenFile()
        {
            AssertMatchesGolden("Rectangle.cs", DslTestFixtures.CreateGeneratedRectangleMetadata());
        }

        [Fact]
        [Trait("Category", "Golden")]
        public void EmitTypeFile_Text_MatchesGoldenFile()
        {
            AssertMatchesGolden("Text.cs", DslTestFixtures.CreateGeneratedTextMetadata());
        }

        [Fact]
        [Trait("Category", "Golden")]
        public void EmitTypeFile_Button_MatchesGoldenFile()
        {
            AssertMatchesGolden("Button.cs", DslTestFixtures.CreateGeneratedButtonMetadata());
        }

        private static void AssertMatchesGolden(string fileName, GeneratedTypeCode metadata)
        {
            CodeEmitter emitter = new();
            string actual = emitter.EmitTypeFile(metadata, DslTestFixtures.DefaultEmitOptions);
            string expected = File.ReadAllText(GetGoldenPath(fileName));

            Assert.Equal(expected, actual);
        }

        private static string GetGoldenPath(string fileName)
        {
            string safeFileName = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                throw new ArgumentException("Golden file name must include a file name.", nameof(fileName));
            }

            return Path.Join(AppContext.BaseDirectory, "testdata", "golden", safeFileName);
        }
    }
}
