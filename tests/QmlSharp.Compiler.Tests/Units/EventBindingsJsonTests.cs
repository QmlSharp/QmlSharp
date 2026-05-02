using System.Text.Json;
using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.Units
{
    public sealed class EventBindingsJsonTests
    {
        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void EventBindings_SerializesCanonicalJsonManifestWithStableFieldOrder()
        {
            EventBindingsBuilder builder = new();
            EventBindingsIndex index = builder.Build(ImmutableArray.Create(
                CompilerTestFixtures.CreateTodoSchema(),
                CompilerTestFixtures.CreateCounterSchema()));

            string json = builder.Serialize(index);

            Assert.StartsWith("{\n  \"schemaVersion\": \"1.0\",\n  \"commands\": [", json, StringComparison.Ordinal);
            Assert.True(json.IndexOf("\"commands\"", StringComparison.Ordinal) < json.IndexOf("\"effects\"", StringComparison.Ordinal));
            Assert.DoesNotContain("compilerSlotKey", json, StringComparison.Ordinal);
            Assert.DoesNotContain("methodName", json, StringComparison.Ordinal);
            Assert.DoesNotContain("paramTypes", json, StringComparison.Ordinal);
            Assert.DoesNotContain("async", json, StringComparison.Ordinal);
            Assert.DoesNotContain("throttle", json, StringComparison.Ordinal);
            Assert.EndsWith("\n", json, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void EventBindings_JsonUsesQmlSharpCommandAndEffectFields()
        {
            EventBindingsBuilder builder = new();
            EventBindingsIndex index = builder.Build(ImmutableArray.Create(CompilerTestFixtures.CreateTodoSchema()));

            string json = builder.Serialize(index);

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement command = document.RootElement.GetProperty("commands")[0];
            JsonElement effect = document.RootElement.GetProperty("effects")[0];

            Assert.Equal("TodoViewModel", command.GetProperty("viewModelClass").GetString());
            Assert.Equal("addItem", command.GetProperty("commandName").GetString());
            Assert.True(command.TryGetProperty("commandId", out JsonElement _));
            Assert.True(command.TryGetProperty("parameterTypes", out JsonElement _));
            Assert.Equal("showToast", effect.GetProperty("effectName").GetString());
            Assert.Equal("string", effect.GetProperty("payloadType").GetString());
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void EventBindings_SortsEntriesByViewModelNameBindingNameAndId()
        {
            ViewModelSchema zetaSchema = SchemaWithCommand("ZetaViewModel", "ZetaView::__qmlsharp_vm0", "same", 2);
            ViewModelSchema alphaSchema = SchemaWithCommand("AlphaViewModel", "AlphaView::__qmlsharp_vm0", "zeta", 9);
            ViewModelSchema alphaSecondSchema = SchemaWithCommand("AlphaViewModel", "AlphaView::__qmlsharp_vm0", "alpha", 8);

            EventBindingsIndex index = new EventBindingsBuilder().Build(ImmutableArray.Create(zetaSchema, alphaSchema, alphaSecondSchema));

            Assert.Equal(
                new[] { "AlphaViewModel.alpha.8", "AlphaViewModel.zeta.9", "ZetaViewModel.same.2" },
                index.Commands.Select(static command => $"{command.ViewModelClass}.{command.CommandName}.{command.CommandId}").ToArray());
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void EventBindings_RoundTripsSerializedManifestWithoutMachineSpecificFields()
        {
            EventBindingsBuilder builder = new();
            EventBindingsIndex index = builder.Build(ImmutableArray.Create(CompilerTestFixtures.CreateTodoSchema()));

            string first = builder.Serialize(index);
            EventBindingsIndex parsed = builder.Deserialize(first);
            string second = builder.Serialize(parsed);

            Assert.Equal(first, second);
            Assert.Equal(string.Empty, parsed.Commands[0].CompilerSlotKey);
            Assert.Equal(string.Empty, parsed.Effects[0].CompilerSlotKey);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void EventBindings_InternalIndexIncludesCompilerSlotKeyForDiagnostics()
        {
            EventBindingsIndex index = new EventBindingsBuilder().Build(ImmutableArray.Create(CompilerTestFixtures.CreateCounterSchema()));

            CommandBindingEntry command = Assert.Single(index.Commands);

            Assert.Equal("CounterView::__qmlsharp_vm0", command.CompilerSlotKey);
        }

        private static ViewModelSchema SchemaWithCommand(string className, string compilerSlotKey, string commandName, int commandId)
        {
            return new ViewModelSchema(
                "1.0",
                className,
                "TestApp",
                "QmlSharp.TestApp",
                new QmlVersion(1, 0),
                2,
                compilerSlotKey,
                ImmutableArray<StateEntry>.Empty,
                ImmutableArray.Create(new CommandEntry(commandName, ImmutableArray<ParameterEntry>.Empty, commandId)),
                ImmutableArray<EffectEntry>.Empty,
                new LifecycleInfo(false, false, true));
        }
    }
}
