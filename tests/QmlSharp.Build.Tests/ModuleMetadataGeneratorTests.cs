using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class ModuleMetadataGeneratorTests
    {
        [Fact]
        public void MM01_QmltypesFileName_DerivesFromLowercaseModuleUri()
        {
            string fileName = ModuleMetadataPaths.GetQmltypesFileName("Company.App.ViewModels");

            Assert.Equal("company_app_viewmodels.qmltypes", fileName);
        }

        [Fact]
        public void MM02_ModuleDirectory_DerivesFromModuleUriUnderQmlRoot()
        {
            string directory = ModuleMetadataPaths.GetModuleDirectory("dist", "Company.App.ViewModels");

            Assert.Equal(Path.Combine("dist", "qml", "Company", "App", "ViewModels"), directory);
        }

        [Fact]
        public void QD01_QmldirGenerator_EmitsCanonicalMetadataInDeterministicOrder()
        {
            QmldirGenerator generator = new();
            ImmutableArray<ViewModelSchema> schemas = ImmutableArray.Create(
                CreateTodoSchema(),
                BuildTestFixtures.CreateCounterSchema());
            ImmutableArray<string> qmlFiles = ImmutableArray.Create("TodoView.qml", "CounterView.qml");

            string content = generator.Generate(
                "QmlSharp.MyApp",
                new QmlSharp.Build.QmlVersion(1, 0),
                schemas,
                qmlFiles);

            Assert.Equal(
                GetExpectedQmldir(),
                content);
            Assert.DoesNotContain("\r", content, StringComparison.Ordinal);
        }

        [Fact]
        public void QD02_QmldirGenerator_FallsBackToSchemaViewNames()
        {
            QmldirGenerator generator = new();

            string content = generator.Generate(
                "QmlSharp.MyApp",
                new QmlSharp.Build.QmlVersion(2, 3),
                ImmutableArray.Create(BuildTestFixtures.CreateCounterSchema()));

            Assert.Contains("CounterView 2.3 CounterView.qml\n", content, StringComparison.Ordinal);
        }

        [Fact]
        public void QT01_QmltypesGenerator_EmitsSchemasMembersAndVariantTypesDeterministically()
        {
            QmltypesGenerator generator = new();
            ViewModelSchema counterSchema = BuildTestFixtures.CreateCounterSchema() with
            {
                Properties = ImmutableArray.Create(
                    new StateEntry("title", "string", null, true, 1),
                    new StateEntry("count", "int", "0", false, 2)),
                Commands = ImmutableArray.Create(
                    new CommandEntry(
                        "setCount",
                        ImmutableArray.Create(new ParameterEntry("value", "int")),
                        3)),
            };
            ViewModelSchema todoSchema = CreateTodoSchema();

            string first = generator.Generate(
                "QmlSharp.MyApp",
                new QmlSharp.Build.QmlVersion(1, 0),
                ImmutableArray.Create(todoSchema, counterSchema));
            string second = generator.Generate(
                "QmlSharp.MyApp",
                new QmlSharp.Build.QmlVersion(1, 0),
                ImmutableArray.Create(counterSchema, todoSchema));

            Assert.Equal(first, second);
            Assert.Contains("Component {\n        name: \"CounterViewModel\"", first, StringComparison.Ordinal);
            Assert.Contains("Component {\n        name: \"TodoViewModel\"", first, StringComparison.Ordinal);
            Assert.True(
                first.IndexOf("CounterViewModel", StringComparison.Ordinal) <
                first.IndexOf("TodoViewModel", StringComparison.Ordinal));
            Assert.Contains("Property { name: \"count\"; type: \"int\" }", first, StringComparison.Ordinal);
            Assert.Contains("Property { name: \"title\"; type: \"string\"; isReadonly: true }", first, StringComparison.Ordinal);
            Assert.Contains("Property { name: \"items\"; type: \"var\" }", first, StringComparison.Ordinal);
            Assert.Contains("Property { name: \"payload\"; type: \"var\"; isReadonly: true }", first, StringComparison.Ordinal);
            Assert.Contains("Property { name: \"mystery\"; type: \"var\" }", first, StringComparison.Ordinal);
            Assert.Contains("Property { name: \"ratio\"; type: \"real\" }", first, StringComparison.Ordinal);
            Assert.Contains("Method {\n            name: \"addItem\"", first, StringComparison.Ordinal);
            Assert.Contains("Parameter { name: \"title\"; type: \"string\" }", first, StringComparison.Ordinal);
            Assert.Contains("Signal {\n            name: \"effectDispatched\"", first, StringComparison.Ordinal);
            Assert.Contains("Parameter { name: \"payloadJson\"; type: \"string\" }", first, StringComparison.Ordinal);
            Assert.DoesNotContain("\r", first, StringComparison.Ordinal);
        }

        [Fact]
        public async Task MM03_ModuleMetadataStage_WritesQmldirAndQmltypesFromSchemaFixtures()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(MM03_ModuleMetadataStage_WritesQmldirAndQmltypesFromSchemaFixtures));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            WriteSchemaFixture(context, BuildTestFixtures.CreateCounterSchema());
            WriteSchemaFixture(context, CreateTodoSchema());
            string moduleDirectory = ModuleMetadataPaths.GetModuleDirectory(context.OutputDir, "QmlSharp.MyApp");
            _ = Directory.CreateDirectory(moduleDirectory);
            await File.WriteAllTextAsync(Path.Combine(moduleDirectory, "TodoView.qml"), "Item {}\n");
            await File.WriteAllTextAsync(Path.Combine(moduleDirectory, "CounterView.qml"), "Item {}\n");
            ModuleMetadataBuildStage stage = new();

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            string qmldir = await File.ReadAllTextAsync(Path.Combine(moduleDirectory, "qmldir"));
            string qmltypes = await File.ReadAllTextAsync(Path.Combine(moduleDirectory, "qmlsharp_myapp.qmltypes"));
            Assert.Equal(
                GetExpectedQmldir(),
                qmldir);
            Assert.Contains("name: \"CounterViewModel\"", qmltypes, StringComparison.Ordinal);
            Assert.Contains("name: \"TodoViewModel\"", qmltypes, StringComparison.Ordinal);
        }

        [Fact]
        public async Task MM04_ModuleMetadataStage_ReportsB030WhenQmldirGenerationFails()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(MM04_ModuleMetadataStage_ReportsB030WhenQmldirGenerationFails));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            WriteSchemaFixture(context, BuildTestFixtures.CreateCounterSchema());
            ModuleMetadataBuildStage stage = new(
                new ThrowingQmldirGenerator(),
                new QmltypesGenerator(),
                new ViewModelSchemaSerializer());

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCode.QmldirGenerationFailed, diagnostic.Code);
            Assert.Equal(BuildPhase.ModuleMetadata, diagnostic.Phase);
        }

        [Fact]
        public async Task MM05_ModuleMetadataStage_ReportsB031WhenQmltypesGenerationFails()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject(nameof(MM05_ModuleMetadataStage_ReportsB031WhenQmltypesGenerationFails));
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            WriteSchemaFixture(context, BuildTestFixtures.CreateCounterSchema());
            ModuleMetadataBuildStage stage = new(
                new QmldirGenerator(),
                new ThrowingQmltypesGenerator(),
                new ViewModelSchemaSerializer());

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCode.QmltypesGenerationFailed, diagnostic.Code);
            Assert.Equal(BuildPhase.ModuleMetadata, diagnostic.Phase);
        }

        private static void WriteSchemaFixture(BuildContext context, ViewModelSchema schema)
        {
            string schemaDirectory = Path.Combine(context.OutputDir, "schemas");
            _ = Directory.CreateDirectory(schemaDirectory);
            ViewModelSchemaSerializer serializer = new();
            string path = Path.Combine(schemaDirectory, $"{schema.ClassName}.schema.json");
            File.WriteAllText(path, serializer.Serialize(schema));
        }

        private static ViewModelSchema CreateTodoSchema()
        {
            return BuildTestFixtures.CreateCounterSchema() with
            {
                ClassName = "TodoViewModel",
                CompilerSlotKey = "TodoView::__qmlsharp_vm0",
                Properties = ImmutableArray.Create(
                    new StateEntry("items", "list<string>", null, false, 11),
                    new StateEntry("mystery", "UnsupportedThing", null, false, 12),
                    new StateEntry("payload", "json", null, true, 13),
                    new StateEntry("ratio", "double", null, false, 14)),
                Commands = ImmutableArray.Create(
                    new CommandEntry(
                        "addItem",
                        ImmutableArray.Create(
                            new ParameterEntry("title", "string"),
                            new ParameterEntry("priority", "int")),
                        15)),
                Effects = ImmutableArray.Create(
                    new EffectEntry(
                        "toast",
                        "json",
                        16,
                        ImmutableArray.Create(new ParameterEntry("payload", "json")))),
            };
        }

        private static string GetExpectedQmldir()
        {
            return
                "module QmlSharp.MyApp\n" +
                "typeinfo qmlsharp_myapp.qmltypes\n" +
                "CounterView 1.0 CounterView.qml\n" +
                "TodoView 1.0 TodoView.qml\n";
        }

        private sealed class ThrowingQmldirGenerator : IQmldirGenerator
        {
            public string Generate(
                string moduleUri,
                QmlSharp.Build.QmlVersion version,
                ImmutableArray<ViewModelSchema> schemas)
            {
                throw new InvalidOperationException("qmldir failed");
            }
        }

        private sealed class ThrowingQmltypesGenerator : IQmltypesGenerator
        {
            public string Generate(
                string moduleUri,
                QmlSharp.Build.QmlVersion version,
                ImmutableArray<ViewModelSchema> schemas)
            {
                throw new InvalidOperationException("qmltypes failed");
            }
        }
    }
}
