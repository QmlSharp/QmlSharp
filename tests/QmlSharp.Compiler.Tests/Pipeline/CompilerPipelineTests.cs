using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QmlSharp.Compiler.Tests.Fixtures;
using QmlSharp.Qml.Emitter;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Compiler.Tests.Pipeline
{
    public sealed class CompilerPipelineTests
    {
        private static readonly IRegistryQuery Registry = RegistryFixture.CreateQtQuickAndControlsRegistry();

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public void CompilerPipeline_CP01_CompileSingleViewAndViewModel_ProducesSuccessfulUnit()
        {
            using ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            ICompiler compiler = CreateCompiler(context);

            CompilationResult result = compiler.Compile(CompilerTestFixtures.DefaultOptions);

            CompilationUnit unit = Assert.Single(result.Units);
            Assert.True(result.Success);
            Assert.Equal("CounterView", unit.ViewClassName);
            Assert.Equal("CounterViewModel", unit.ViewModelClassName);
            Assert.NotNull(unit.Schema);
            Assert.Contains("CounterViewModel", unit.QmlText, StringComparison.Ordinal);
            Assert.Equal(1, result.Stats.SuccessfulFiles);
            Assert.Equal(0, result.Stats.FailedFiles);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public void CompilerPipeline_CP02_CompileThreeViews_ProducesThreeSuccessfulUnits()
        {
            using ProjectContext context = CompilerTestFixtures.CreateNFileContext(3);
            ICompiler compiler = CreateCompiler(context);

            CompilationResult result = compiler.Compile(CompilerTestFixtures.CreateNFileOptions(3));

            Assert.True(result.Success);
            Assert.Equal(3, result.Units.Length);
            Assert.All(result.Units, static unit => Assert.True(unit.Success));
            Assert.Equal(3, result.Stats.SuccessfulFiles);
            Assert.Equal(3, result.EventBindings.Commands.Length);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public void CompilerPipeline_CP03_WriteOutputCreatesAllArtifactTypes()
        {
            using ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            using TempOutputDirectory temp = new();
            CompilerOptions options = CompilerTestFixtures.DefaultOptions with
            {
                OutputDir = temp.Path,
                SourceMapDir = Path.Join(temp.Path, "source-maps"),
            };
            ICompiler compiler = CreateCompiler(context);
            CompilationResult compilation = compiler.Compile(options);

            OutputResult output = compiler.WriteOutput(compilation, options);

            Assert.True(output.Success);
            Assert.Equal(new[] { Path.Join(temp.Path, "qml", "QmlSharp", "TestApp", "CounterView.qml") }, output.QmlFiles);
            Assert.Equal(new[] { Path.Join(temp.Path, "schemas", "CounterViewModel.schema.json") }, output.SchemaFiles);
            Assert.Equal(Path.Join(temp.Path, "event-bindings.json"), output.EventBindingsFile);
            Assert.Equal(new[] { Path.Join(temp.Path, "source-maps", "CounterView.qml.map") }, output.SourceMapFiles);
            Assert.All(output.QmlFiles.AddRange(output.SchemaFiles).AddRange(output.SourceMapFiles), static path => Assert.True(File.Exists(path)));
            Assert.True(File.Exists(output.EventBindingsFile));
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public void CompilerPipeline_CP04_SchemaJsonMatchesCanonicalFieldNames()
        {
            using ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            ICompiler compiler = CreateCompiler(context);
            CompilationResult result = compiler.Compile(CompilerTestFixtures.DefaultOptions);
            ViewModelSchema schema = Assert.Single(result.Units).Schema!;

            string json = new ViewModelSchemaSerializer().Serialize(schema);

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            Assert.Equal(
                new[]
                {
                    "schemaVersion",
                    "className",
                    "moduleName",
                    "moduleUri",
                    "moduleVersion",
                    "compilerSlotKey",
                    "properties",
                    "commands",
                    "effects",
                    "lifecycle",
                },
                root.EnumerateObject().Select(static property => property.Name));
            Assert.False(root.TryGetProperty("states", out _));
            Assert.False(root.TryGetProperty("version", out _));
            JsonElement state = root.GetProperty("properties")[0];
            Assert.True(state.TryGetProperty("readOnly", out _));
            Assert.False(state.TryGetProperty("readonly", out _));
            Assert.False(state.TryGetProperty("qmlName", out _));
            Assert.False(root.GetProperty("commands")[0].TryGetProperty("async", out _));
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public void CompilerPipeline_CP05_EventBindingsAggregateAllCommandsAndEffects()
        {
            using ProjectContext context = CompilerTestFixtures.CreateMultiViewModelContext();
            ICompiler compiler = CreateCompiler(context);

            CompilationResult result = compiler.Compile(CompilerTestFixtures.DefaultOptions);

            Assert.True(result.Success);
            Assert.Equal(
                new[] { "CounterViewModel.decrement", "CounterViewModel.increment", "TodoViewModel.addItem", "TodoViewModel.removeItem" },
                result.EventBindings.Commands.Select(static command => $"{command.ViewModelClass}.{command.CommandName}"));
            EffectBindingEntry effect = Assert.Single(result.EventBindings.Effects);
            Assert.Equal("TodoViewModel", effect.ViewModelClass);
            Assert.Equal("showToast", effect.EffectName);
            Assert.Equal("string", effect.PayloadType);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public void CompilerPipeline_CP06_ProgressCallbackFiresForAllCompilationPhases()
        {
            using ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            using TempOutputDirectory temp = new();
            CompilerOptions options = CompilerTestFixtures.DefaultOptions with
            {
                OutputDir = temp.Path,
                SourceMapDir = Path.Join(temp.Path, "source-maps"),
            };
            List<CompilationPhase> phases = [];
            ICompiler compiler = CreateCompiler(context);
            compiler.OnProgress(progress => phases.Add(progress.Phase));

            CompilationResult result = compiler.Compile(options);
            _ = compiler.WriteOutput(result, options);

            foreach (CompilationPhase phase in Enum.GetValues<CompilationPhase>())
            {
                Assert.Contains(phase, phases);
            }
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public void CompilerPipeline_CP07_FileErrorsDoNotBlockOtherFiles()
        {
            ImmutableArray<(string FileName, string Source)> sources = CompilerSourceFixtures.MultiFileProject()
                .Add(("BrokenViewModel.cs", "namespace TestApp; public sealed class BrokenViewModel { }"))
                .Add(("BrokenView.cs", """
                    using QmlSharp.Core;
                    using QmlSharp.Dsl;
                    namespace TestApp;
                    public sealed class BrokenView : View<BrokenViewModel>
                    {
                        public override object Build() => Column();
                        private static IObjectBuilder Column() => throw new System.NotImplementedException();
                    }
                    """));
            CSharpCompilation compilation = RoslynTestHelper.CreateCompilation(sources);
            using ProjectContext context = new(compilation, sources.Select(static source => source.FileName).ToImmutableArray(), CompilerTestFixtures.DefaultOptions, new DiagnosticReporter());
            ICompiler compiler = CreateCompiler(context);

            CompilationResult result = compiler.Compile(CompilerTestFixtures.DefaultOptions);

            Assert.False(result.Success);
            Assert.Equal(3, result.Units.Length);
            Assert.Equal(2, result.Units.Count(static unit => unit.Success));
            Assert.Contains(result.Units, static unit => unit.ViewClassName == "BrokenView" && !unit.Success);
            Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == DiagnosticCodes.ViewModelMissingAttribute);
            Assert.Contains(result.Units, static unit => unit.ViewClassName == "CounterView" && unit.Success);
            Assert.Contains(result.Units, static unit => unit.ViewClassName == "TodoView" && unit.Success);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public void CompilerPipeline_CP08_CompileFileCompilesSingleView()
        {
            using ProjectContext context = CompilerTestFixtures.CreateMultiViewModelContext();
            ICompiler compiler = CreateCompiler(context);

            CompilationUnit unit = compiler.CompileFile("TodoView.cs", context, CompilerTestFixtures.DefaultOptions);

            Assert.True(unit.Success);
            Assert.Equal("TodoView", unit.ViewClassName);
            Assert.Equal("TodoViewModel", unit.ViewModelClassName);
            Assert.Contains("TodoViewModel", unit.QmlText, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public void CompilerPipeline_GeneratedQmlContainsV2ShapeOnly()
        {
            using ProjectContext context = CompilerTestFixtures.CreateCounterContext();
            ICompiler compiler = CreateCompiler(context);

            CompilationUnit unit = Assert.Single(compiler.Compile(CompilerTestFixtures.DefaultOptions).Units);

            Assert.Contains("CounterViewModel {", unit.QmlText, StringComparison.Ordinal);
            Assert.Contains("id: __qmlsharp_vm0", unit.QmlText, StringComparison.Ordinal);
            Assert.Contains("__qmlsharp_vm0.increment()", unit.QmlText, StringComparison.Ordinal);
            Assert.DoesNotContain("__qmlts", unit.QmlText, StringComparison.Ordinal);
            Assert.DoesNotContain("context", unit.QmlText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("commandId", unit.QmlText, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Golden)]
        public void Golden_CPG1_CounterViewQml_MatchesCommittedGolden()
        {
            CompilationUnit unit = CompileGoldenProject().Units.Single(static unit => unit.ViewClassName == "CounterView");

            Assert.Equal(ReadGolden("CounterView.qml"), unit.QmlText);
        }

        [Fact]
        [Trait("Category", TestCategories.Golden)]
        public void Golden_CPG2_CounterViewModelSchema_MatchesCommittedGolden()
        {
            CompilationUnit unit = CompileGoldenProject().Units.Single(static unit => unit.ViewModelClassName == "CounterViewModel");
            string json = new ViewModelSchemaSerializer().Serialize(unit.Schema!);

            Assert.Equal(ReadGolden("CounterViewModel.schema.json"), json);
        }

        [Fact]
        [Trait("Category", TestCategories.Golden)]
        public void Golden_CPG3_TodoViewQml_MatchesCommittedGolden()
        {
            CompilationUnit unit = CompileGoldenProject().Units.Single(static unit => unit.ViewClassName == "TodoView");

            Assert.Equal(ReadGolden("TodoView.qml"), unit.QmlText);
        }

        [Fact]
        [Trait("Category", TestCategories.Golden)]
        public void Golden_CPG4_EventBindings_MatchesCommittedGolden()
        {
            CompilationResult result = CompileGoldenProject();
            string json = new EventBindingsBuilder().Serialize(result.EventBindings);

            Assert.Equal(ReadGolden("event-bindings.json"), json);
        }

        private static CompilationResult CompileGoldenProject()
        {
            using ProjectContext context = CompilerTestFixtures.CreateMultiViewModelContext();
            ICompiler compiler = CreateCompiler(context);
            CompilationResult result = compiler.Compile(CompilerTestFixtures.DefaultOptions);
            Assert.True(result.Success);
            return result;
        }

        private static ICompiler CreateCompiler(ProjectContext context)
        {
            return new QmlSharp.Compiler.QmlCompiler(
                new StaticAnalyzer(context),
                new ViewModelExtractor(),
                new IdAllocator(),
                new DslTransformer(),
                new ImportResolver(),
                new PostProcessor(),
                new QmlEmitter(),
                Registry,
                new SourceMapManager(),
                new EventBindingsBuilder(),
                new CompilerOutputWriter());
        }

        private static string ReadGolden(string fileName)
        {
            string path = Path.Join(AppContext.BaseDirectory, "testdata", "golden", fileName);
            return File.ReadAllText(path);
        }

        private sealed class StaticAnalyzer : ICSharpAnalyzer
        {
            private readonly ProjectContext context;
            private readonly CSharpAnalyzer inner = new();

            public StaticAnalyzer(ProjectContext context)
            {
                this.context = context;
            }

            public ProjectContext CreateProjectContext(CompilerOptions options)
            {
                return context;
            }

            public ImmutableArray<DiscoveredView> DiscoverViews(ProjectContext context)
            {
                return inner.DiscoverViews(context);
            }

            public ImmutableArray<DiscoveredViewModel> DiscoverViewModels(ProjectContext context)
            {
                return inner.DiscoverViewModels(context);
            }

            public ImmutableArray<DiscoveredImport> DiscoverImports(ProjectContext context, string filePath)
            {
                return inner.DiscoverImports(context, filePath);
            }

            public SemanticModel GetSemanticModel(ProjectContext context, string filePath)
            {
                return inner.GetSemanticModel(context, filePath);
            }
        }
    }
}
