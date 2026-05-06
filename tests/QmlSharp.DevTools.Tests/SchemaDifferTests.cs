namespace QmlSharp.DevTools.Tests
{
    public sealed class SchemaDifferTests
    {
        [Fact]
        [Trait("TestId", "DSV-11")]
        public void Compare_StatePropertyAddedOrRemoved_IsStructural()
        {
            SchemaDiffer differ = new();
            ViewModelSchema previous = DevToolsTestFixtures.ViewModelSchema(
                properties: ImmutableArray.Create(DevToolsTestFixtures.State("count", "int")));
            ViewModelSchema current = DevToolsTestFixtures.ViewModelSchema(
                properties: ImmutableArray.Create(
                    DevToolsTestFixtures.State("count", "int"),
                    DevToolsTestFixtures.State("title", "string")));

            SchemaDiffResult added = differ.Compare([previous], [current]);
            SchemaDiffResult removed = differ.Compare([current], [previous]);

            Assert.True(added.HasStructuralChanges);
            Assert.Contains("state property added: title", added.RestartReason, StringComparison.Ordinal);
            Assert.True(removed.HasStructuralChanges);
            Assert.Contains("state property removed: title", removed.RestartReason, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DSV-11")]
        public void Compare_CommandAddedOrRemoved_IsStructural()
        {
            SchemaDiffer differ = new();
            ViewModelSchema previous = DevToolsTestFixtures.ViewModelSchema(
                commands: ImmutableArray.Create(DevToolsTestFixtures.Command("increment")));
            ViewModelSchema current = DevToolsTestFixtures.ViewModelSchema(
                commands: ImmutableArray.Create(
                    DevToolsTestFixtures.Command("increment"),
                    DevToolsTestFixtures.Command("reset")));

            SchemaDiffResult result = differ.Compare([previous], [current]);

            Assert.True(result.HasStructuralChanges);
            Assert.Contains("command added: reset", result.RestartReason, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DSV-11")]
        public void Compare_EffectAddedOrRemoved_IsStructural()
        {
            SchemaDiffer differ = new();
            ViewModelSchema previous = DevToolsTestFixtures.ViewModelSchema(
                effects: ImmutableArray.Create(DevToolsTestFixtures.Effect("countChanged", "int")));
            ViewModelSchema current = DevToolsTestFixtures.ViewModelSchema(
                effects: ImmutableArray.Create(
                    DevToolsTestFixtures.Effect("countChanged", "int"),
                    DevToolsTestFixtures.Effect("toast", "string")));

            SchemaDiffResult result = differ.Compare([previous], [current]);

            Assert.True(result.HasStructuralChanges);
            Assert.Contains("effect added: toast", result.RestartReason, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DSV-11")]
        public void Compare_ImplementationOnlyChange_IsIgnored()
        {
            SchemaDiffer differ = new();
            ViewModelSchema previous = DevToolsTestFixtures.ViewModelSchema(
                properties: ImmutableArray.Create(new StateEntry(
                    "count",
                    "int",
                    DefaultValue: "1",
                    ReadOnly: false,
                    MemberId: 1)));
            ViewModelSchema current = previous with
            {
                Properties = ImmutableArray.Create(new StateEntry(
                    "count",
                    "int",
                    DefaultValue: "2",
                    ReadOnly: false,
                    MemberId: 99)),
            };

            SchemaDiffResult result = differ.Compare([previous], [current]);

            Assert.False(result.HasStructuralChanges);
            Assert.Empty(result.AffectedViewModels);
            Assert.Empty(result.Reasons);
        }
    }
}
