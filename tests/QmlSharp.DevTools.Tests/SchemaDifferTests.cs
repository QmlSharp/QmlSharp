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

        [Fact]
        [Trait("TestId", "DSV-11")]
        public void Compare_PreviousNullOrEmptyPreviousCompilation_ReturnsNoStructuralChanges()
        {
            SchemaDiffer differ = new();
            CompilationResult current = DevToolsTestFixtures.CompilationResultWithSchema();
            CompilationResult previousWithoutSchemas = DevToolsTestFixtures.SuccessfulCompilationResult();

            SchemaDiffResult firstRun = differ.Compare(previous: null, current);
            SchemaDiffResult previousHadNoSchemas = differ.Compare(previousWithoutSchemas, current);

            Assert.False(firstRun.HasStructuralChanges);
            Assert.False(previousHadNoSchemas.HasStructuralChanges);
            Assert.Empty(firstRun.Reasons);
            Assert.Empty(previousHadNoSchemas.AffectedViewModels);
        }

        [Fact]
        [Trait("TestId", "DSV-11")]
        public void Compare_ViewModelAddedOrRemoved_IsStructuralAndSorted()
        {
            SchemaDiffer differ = new();
            ViewModelSchema counter = DevToolsTestFixtures.ViewModelSchema(className: "CounterViewModel");
            ViewModelSchema todo = DevToolsTestFixtures.ViewModelSchema(
                className: "TodoViewModel",
                compilerSlotKey: "TodoView::__qmlsharp_vm0");

            SchemaDiffResult added = differ.Compare([counter], [counter, todo]);
            SchemaDiffResult removed = differ.Compare([counter, todo], [counter]);

            Assert.True(added.HasStructuralChanges);
            Assert.Equal(["TodoViewModel"], added.AffectedViewModels);
            Assert.Contains("ViewModel added: TodoViewModel", added.RestartReason, StringComparison.Ordinal);
            Assert.True(removed.HasStructuralChanges);
            Assert.Equal(["TodoViewModel"], removed.AffectedViewModels);
            Assert.Contains("ViewModel removed: TodoViewModel", removed.RestartReason, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DSV-11")]
        public void Compare_ScalarSchemaFieldsChanged_ReportStructuralReasons()
        {
            SchemaDiffer differ = new();
            ViewModelSchema previous = DevToolsTestFixtures.ViewModelSchema();
            ViewModelSchema current = previous with
            {
                SchemaVersion = "2.1",
                ClassName = "RenamedCounterViewModel",
                ModuleName = "RenamedModule",
                ModuleUri = "Test.Renamed",
                ModuleVersion = new QmlSharp.Compiler.QmlVersion(2, 0),
            };

            SchemaDiffResult result = differ.Compare([previous], [current]);

            Assert.True(result.HasStructuralChanges);
            Assert.Contains("schema version changed", result.RestartReason, StringComparison.Ordinal);
            Assert.Contains("class name changed", result.RestartReason, StringComparison.Ordinal);
            Assert.Contains("module name changed", result.RestartReason, StringComparison.Ordinal);
            Assert.Contains("module URI changed", result.RestartReason, StringComparison.Ordinal);
            Assert.Contains("module version changed", result.RestartReason, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DSV-11")]
        public void Compare_MemberShapeChanges_ReportTypeWritabilityPayloadAndParameterReasons()
        {
            SchemaDiffer differ = new();
            ViewModelSchema previous = DevToolsTestFixtures.ViewModelSchema(
                properties: ImmutableArray.Create(DevToolsTestFixtures.State("count", "int", readOnly: false)),
                commands: ImmutableArray.Create(DevToolsTestFixtures.Command("setCount", "value")),
                effects: ImmutableArray.Create(DevToolsTestFixtures.Effect("countChanged", "int")));
            ViewModelSchema current = DevToolsTestFixtures.ViewModelSchema(
                properties: ImmutableArray.Create(DevToolsTestFixtures.State("count", "string", readOnly: true)),
                commands: ImmutableArray.Create(DevToolsTestFixtures.Command("setCount", "value", "source")),
                effects: ImmutableArray.Create(new EffectEntry(
                    "countChanged",
                    "string",
                    EffectId: 99,
                    ImmutableArray.Create(new ParameterEntry("payload", "string"), new ParameterEntry("source", "string")))));

            SchemaDiffResult result = differ.Compare([previous], [current]);

            Assert.True(result.HasStructuralChanges);
            Assert.Contains("state property type changed: count", result.RestartReason, StringComparison.Ordinal);
            Assert.Contains("state property writability changed: count", result.RestartReason, StringComparison.Ordinal);
            Assert.Contains("command parameters changed: setCount", result.RestartReason, StringComparison.Ordinal);
            Assert.Contains("effect payload changed: countChanged", result.RestartReason, StringComparison.Ordinal);
            Assert.Contains("effect parameters changed: countChanged", result.RestartReason, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("TestId", "DSV-11")]
        public void Compare_DefaultAndExplicitEmptyParameterLists_AreEquivalent()
        {
            SchemaDiffer differ = new();
            ViewModelSchema previous = DevToolsTestFixtures.ViewModelSchema(
                commands: ImmutableArray.Create(new CommandEntry("reset", default, CommandId: 1)));
            ViewModelSchema current = DevToolsTestFixtures.ViewModelSchema(
                commands: ImmutableArray.Create(new CommandEntry("reset", ImmutableArray<ParameterEntry>.Empty, CommandId: 2)));

            SchemaDiffResult result = differ.Compare([previous], [current]);

            Assert.False(result.HasStructuralChanges);
        }
    }
}
