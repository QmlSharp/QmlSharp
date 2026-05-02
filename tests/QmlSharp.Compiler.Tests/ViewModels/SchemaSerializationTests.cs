using System.Text.Json;
using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.ViewModels
{
    public sealed class SchemaSerializationTests
    {
        private readonly ViewModelSchemaSerializer serializer = new();

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void Schema_SerializesCanonicalJsonWithStep0600FieldOrder()
        {
            ViewModelSchema schema = CompilerTestFixtures.CreateCounterSchema();

            string json = serializer.Serialize(schema);

            Assert.Contains("\"schemaVersion\": \"1.0\"", json, StringComparison.Ordinal);
            Assert.True(json.IndexOf("\"schemaVersion\"", StringComparison.Ordinal) < json.IndexOf("\"className\"", StringComparison.Ordinal));
            Assert.True(json.IndexOf("\"className\"", StringComparison.Ordinal) < json.IndexOf("\"moduleName\"", StringComparison.Ordinal));
            Assert.True(json.IndexOf("\"moduleName\"", StringComparison.Ordinal) < json.IndexOf("\"moduleUri\"", StringComparison.Ordinal));
            Assert.True(json.IndexOf("\"compilerSlotKey\"", StringComparison.Ordinal) < json.IndexOf("\"properties\"", StringComparison.Ordinal));
            Assert.DoesNotContain("\"version\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"sourceName\"", json, StringComparison.Ordinal);
            Assert.EndsWith("\n", json, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void Schema_RoundTripsThroughJsonWithStableOutput()
        {
            ViewModelSchema schema = CompilerTestFixtures.CreateTodoSchema();

            string first = serializer.Serialize(schema);
            ViewModelSchema parsed = serializer.Deserialize(first);
            string second = serializer.Serialize(parsed);

            Assert.Equal(first, second);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void Schema_SortsPropertiesCommandsAndEffectsByQmlName()
        {
            ViewModelSchema schema = new(
                "1.0",
                "SortedViewModel",
                "TestApp",
                "QmlSharp.TestApp",
                new QmlVersion(1, 0),
                2,
                "SortedViewModel::__qmlsharp_vm0",
                ImmutableArray.Create(
                    new StateEntry("zeta", "int", "0", false, 2),
                    new StateEntry("alpha", "int", "0", false, 1)),
                ImmutableArray.Create(
                    new CommandEntry("zeta", ImmutableArray<ParameterEntry>.Empty, 4),
                    new CommandEntry("alpha", ImmutableArray<ParameterEntry>.Empty, 3)),
                ImmutableArray.Create(
                    new EffectEntry("zeta", "void", 6, ImmutableArray<ParameterEntry>.Empty),
                    new EffectEntry("alpha", "void", 5, ImmutableArray<ParameterEntry>.Empty)),
                new LifecycleInfo(false, false, false));

            string json = serializer.Serialize(schema);

            Assert.True(json.IndexOf("\"name\": \"alpha\"", StringComparison.Ordinal) < json.IndexOf("\"name\": \"zeta\"", StringComparison.Ordinal));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void Schema_OmitsUnsupportedDefaultValueWhenInitializerIsNotConstant()
        {
            CSharpAnalyzer analyzer = new();
            ViewModelExtractor extractor = new();
            ProjectContext context = analyzer.CreateInMemoryProjectContext(
                CompilerTestFixtures.DefaultOptions,
                RoslynTestHelper.CreateCompilation("""
                    using QmlSharp.Core;
                    namespace TestApp;
                    [ViewModel]
                    public sealed class DynamicDefaultViewModel
                    {
                        [State] public string Title { get; set; } = System.DateTime.UtcNow.ToString();
                    }
                    """),
                ImmutableArray.Create("Source0.cs"));
            ViewModelSchema schema = extractor.Extract(Assert.Single(analyzer.DiscoverViewModels(context)), context, new IdAllocator());

            string json = serializer.Serialize(schema);

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement property = document.RootElement.GetProperty("properties")[0];
            Assert.False(property.TryGetProperty("defaultValue", out JsonElement _));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void Schema_EffectSerializationOmitsInternalPayloadParameters()
        {
            ViewModelSchema schema = CompilerTestFixtures.CreateTodoSchema();

            string json = serializer.Serialize(schema);

            Assert.Contains("\"payloadType\": \"string\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"parameters\"", json.Substring(json.IndexOf("\"effects\"", StringComparison.Ordinal)), StringComparison.Ordinal);
        }
    }
}
