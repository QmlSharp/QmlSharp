using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.Options
{
    public sealed class CompilerContractSmokeTests
    {
        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void CompilerOptions_Defaults_MatchStep0601Contract()
        {
            CompilerOptions options = CompilerTestFixtures.DefaultOptions;

            Assert.True(options.GenerateSourceMaps);
            Assert.False(options.FormatQml);
            Assert.False(options.LintQml);
            Assert.True(options.Incremental);
            Assert.Equal(new QmlVersion(1, 0), options.ModuleVersion);
            Assert.Equal(["**/*.cs"], options.IncludePatterns.ToArray());
            Assert.Contains("**/obj/**", options.ExcludePatterns);
            Assert.Contains("**/bin/**", options.ExcludePatterns);
            Assert.Equal(DiagnosticSeverity.Warning, options.MaxAllowedSeverity);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void SchemaRecords_UseCanonicalStep0600ArtifactNames()
        {
            ViewModelSchema schema = CompilerTestFixtures.CreateCounterSchema();

            Assert.Equal("1.0", schema.SchemaVersion);
            Assert.Equal("TestApp", schema.ModuleName);
            Assert.Equal("QmlSharp.TestApp", schema.ModuleUri);
            Assert.Equal(2, schema.Version);
            Assert.Equal("CounterView::__qmlsharp_vm0", schema.CompilerSlotKey);
            Assert.Collection(
                schema.Properties,
                state =>
                {
                    Assert.Equal("count", state.Name);
                    Assert.Equal("int", state.Type);
                    Assert.Equal("0", state.DefaultValue);
                    Assert.False(state.ReadOnly);
                });
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void ArtifactRecords_RepresentEventBindingsAndSourceMaps()
        {
            EventBindingsIndex bindings = new(
                "1.0",
                ImmutableArray.Create(new CommandBindingEntry("CounterViewModel", "increment", 123, ImmutableArray<string>.Empty)),
                ImmutableArray.Create(new EffectBindingEntry("TodoViewModel", "showToast", 456, "string")));

            SourceMap sourceMap = new(
                "1.0",
                "CounterView.cs",
                "CounterView.qml",
                ImmutableArray.Create(new SourceMapMapping(1, 1, "CounterView.cs", 10, 5)));

            Assert.Equal("increment", bindings.Commands.Single().CommandName);
            Assert.Empty(bindings.Commands.Single().ParameterTypes);
            Assert.Equal("showToast", bindings.Effects.Single().EffectName);
            Assert.Equal("string", bindings.Effects.Single().PayloadType);
            Assert.Equal(1, sourceMap.Mappings.Single().OutputLine);
            Assert.Equal(1, sourceMap.Mappings.Single().OutputColumn);
        }
    }
}
