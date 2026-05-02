using System.Text.Json;
using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.SourceMaps
{
    public sealed class SourceMapManagerTests
    {
        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void SourceMap_SM01_AddMappingAndBuildProducesSourceMap()
        {
            SourceMapManager manager = new();
            ISourceMapBuilder builder = manager.CreateBuilder("CounterView.cs", "CounterView.qml");

            builder.AddMapping(new SourceMapMapping(10, 4, "CounterView.cs", 5, 3, nodeKind: "binding"));
            SourceMap sourceMap = builder.Build();

            SourceMapMapping mapping = Assert.Single(sourceMap.Mappings);
            Assert.Equal("1.0", sourceMap.SchemaVersion);
            Assert.Equal("CounterView.cs", sourceMap.SourceFilePath);
            Assert.Equal("CounterView.qml", sourceMap.OutputFilePath);
            Assert.Equal(10, mapping.OutputLine);
            Assert.Equal(5, mapping.SourceLine);
            Assert.Equal("binding", mapping.NodeKind);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void SourceMap_SM02_MultipleMappingsAreOrderedByGeneratedPosition()
        {
            SourceMapManager manager = new();
            ISourceMapBuilder builder = manager.CreateBuilder("View.cs", "View.qml");

            builder.AddMapping(new SourceMapMapping(20, 1, "View.cs", 2, 1));
            builder.AddMapping(new SourceMapMapping(10, 20, "View.cs", 7, 2));
            builder.AddMapping(new SourceMapMapping(10, 4, "View.cs", 3, 1));
            SourceMap sourceMap = builder.Build();

            Assert.Equal(new[] { "10:4", "10:20", "20:1" }, sourceMap.Mappings.Select(static mapping => $"{mapping.OutputLine}:{mapping.OutputColumn}").ToArray());
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void SourceMap_SM03_SerializeProducesValidCanonicalJson()
        {
            SourceMapManager manager = new();
            SourceMap sourceMap = BuildSampleSourceMap(manager);

            string json = manager.Serialize(sourceMap);

            using JsonDocument document = JsonDocument.Parse(json);
            Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
            Assert.Contains("\"sourceFilePath\": \"View.cs\"", json, StringComparison.Ordinal);
            Assert.Contains("\"outputFilePath\": \"View.qml\"", json, StringComparison.Ordinal);
            Assert.Contains("\"mappings\"", json, StringComparison.Ordinal);
            Assert.True(json.IndexOf("\"outputLine\"", StringComparison.Ordinal) < json.IndexOf("\"sourceLine\"", StringComparison.Ordinal));
            Assert.EndsWith("\n", json, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void SourceMap_SM04_DeserializeRoundTrips()
        {
            SourceMapManager manager = new();
            SourceMap original = BuildSampleSourceMap(manager);

            string first = manager.Serialize(original);
            SourceMap parsed = manager.Deserialize(first);
            string second = manager.Serialize(parsed);

            Assert.Equal(first, second);
            Assert.Equal(original.SchemaVersion, parsed.SchemaVersion);
            Assert.Equal(original.SourceFilePath, parsed.SourceFilePath);
            Assert.Equal(original.OutputFilePath, parsed.OutputFilePath);
            Assert.Equal(original.Mappings.ToArray(), parsed.Mappings.ToArray());
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void SourceMap_SM05_FindSourceLocationForKnownQmlPosition()
        {
            SourceMapManager manager = new();
            SourceMap sourceMap = BuildSampleSourceMap(manager);

            SourceLocation? location = manager.FindSourceLocation(sourceMap, "View.qml", 10, 4);

            Assert.NotNull(location);
            Assert.Equal("View.cs", location.FilePath);
            Assert.Equal(3, location.Line);
            Assert.Equal(1, location.Column);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void SourceMap_SM06_FindSourceLocationForUnknownQmlPositionReturnsNull()
        {
            SourceMapManager manager = new();
            SourceMap sourceMap = BuildSampleSourceMap(manager);

            SourceLocation? location = manager.FindSourceLocation(sourceMap, "View.qml", 99, 1);
            SourceLocation? wrongFile = manager.FindSourceLocation(sourceMap, "Other.qml", 10, 4);

            Assert.Null(location);
            Assert.Null(wrongFile);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void SourceMap_SM07_FindQmlLocationForKnownSourcePosition()
        {
            SourceMapManager manager = new();
            SourceMap sourceMap = BuildSampleSourceMap(manager);

            QmlLocation? location = manager.FindQmlLocation(sourceMap, "View.cs", 7, 2);

            Assert.NotNull(location);
            Assert.Equal("View.qml", location.FilePath);
            Assert.Equal(10, location.Line);
            Assert.Equal(20, location.Column);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void SourceMap_UsesColumnToDisambiguateMultipleMappingsOnSameGeneratedLine()
        {
            SourceMapManager manager = new();
            SourceMap sourceMap = BuildSampleSourceMap(manager);

            SourceLocation? location = manager.FindSourceLocation(sourceMap, "View.qml", 10, 18);

            Assert.NotNull(location);
            Assert.Equal(7, location.Line);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void SourceMap_UsesColumnToDisambiguateMultipleMappingsOnSameSourceLine()
        {
            SourceMapManager manager = new();
            ISourceMapBuilder builder = manager.CreateBuilder("View.cs", "View.qml");
            builder.AddMapping(new SourceMapMapping(4, 1, "View.cs", 5, 2));
            builder.AddMapping(new SourceMapMapping(9, 1, "View.cs", 5, 12));
            SourceMap sourceMap = builder.Build();

            QmlLocation? location = manager.FindQmlLocation(sourceMap, "View.cs", 5, 10);

            Assert.NotNull(location);
            Assert.Equal(9, location.Line);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void SourceMap_PublicJsonPositionsAreOneBased()
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(() => new SourceMapMapping(0, 1, "View.cs", 1, 1));
            _ = Assert.Throws<ArgumentOutOfRangeException>(() => new SourceMapMapping(1, 0, "View.cs", 1, 1));
            _ = Assert.Throws<ArgumentOutOfRangeException>(() => new SourceMapMapping(1, 1, "View.cs", 0, 1));
            _ = Assert.Throws<ArgumentOutOfRangeException>(() => new QmlLocation("View.qml", 1, 0));
        }

        private static SourceMap BuildSampleSourceMap(SourceMapManager manager)
        {
            ISourceMapBuilder builder = manager.CreateBuilder("View.cs", "View.qml");
            builder.AddMapping(new SourceMapMapping(10, 20, "View.cs", 7, 2, symbol: "Submit", nodeKind: "handler"));
            builder.AddMapping(new SourceMapMapping(10, 4, "View.cs", 3, 1, symbol: "Title", nodeKind: "binding"));
            return builder.Build();
        }
    }
}
