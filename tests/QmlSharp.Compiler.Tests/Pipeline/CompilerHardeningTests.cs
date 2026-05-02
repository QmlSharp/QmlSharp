using System.Text.Json;
using Microsoft.CodeAnalysis;
using QmlSharp.Compiler.Tests.Fixtures;
using QmlSharp.Qml.Emitter;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Compiler.Tests.Pipeline
{
    public sealed class CompilerHardeningTests
    {
        private static readonly IRegistryQuery Registry = RegistryFixture.CreateQtQuickAndControlsRegistry();

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public void CompilerHardening_RepeatedRunsProduceByteStableArtifactSet()
        {
            using TempOutputDirectory firstOutput = new();
            using TempOutputDirectory secondOutput = new();

            ImmutableDictionary<string, string> firstArtifacts = CompileAndWriteArtifacts(firstOutput.Path);
            ImmutableDictionary<string, string> secondArtifacts = CompileAndWriteArtifacts(secondOutput.Path);

            Assert.Equal(firstArtifacts.Keys.Order(StringComparer.Ordinal), secondArtifacts.Keys.Order(StringComparer.Ordinal));
            foreach (string relativePath in firstArtifacts.Keys.Order(StringComparer.Ordinal))
            {
                Assert.Equal(firstArtifacts[relativePath], secondArtifacts[relativePath]);
            }
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public void CompilerHardening_RepeatedRunsProduceStableDiagnosticsOrdering()
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

            string[] firstDiagnostics = CompileDiagnostics(sources);
            string[] secondDiagnostics = CompileDiagnostics(sources);

            Assert.Equal(firstDiagnostics, secondDiagnostics);
            Assert.Equal(firstDiagnostics.Order(StringComparer.Ordinal).ToArray(), firstDiagnostics);
        }

        [Fact]
        [Trait("Category", TestCategories.Integration)]
        public void CompilerHardening_RoslynSyntaxErrorsRecoverWithoutSuccessfulUnits()
        {
            ImmutableArray<(string FileName, string Source)> sources = ImmutableArray.Create(
                ("BrokenView.cs", """
                    using QmlSharp.Core;
                    namespace TestApp;
                    public sealed class BrokenView : View<BrokenViewModel>
                    {
                        public override object Build() => Column(
                    }
                    """),
                ("BrokenViewModel.cs", """
                    using QmlSharp.Core;
                    namespace TestApp;
                    [ViewModel]
                    public sealed class BrokenViewModel
                    {
                        [State] public int Count { get; set; }
                    }
                    """));
            using ProjectContext context = RoslynTestHelper.CreateContext(sources);
            ICompiler compiler = CreateCompiler(context);

            CompilationResult result = compiler.Compile(CompilerTestFixtures.DefaultOptions);

            Assert.False(result.Success);
            Assert.Empty(result.Units);
            Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == DiagnosticCodes.RoslynCompilationFailed);
        }

        private static ImmutableDictionary<string, string> CompileAndWriteArtifacts(string outputDir)
        {
            CompilerOptions options = CompilerTestFixtures.DefaultOptions with
            {
                OutputDir = outputDir,
                SourceMapDir = Path.Join(outputDir, "source-maps"),
            };
            using ProjectContext context = CompilerTestFixtures.CreateMultiViewModelContext();
            ICompiler compiler = CreateCompiler(context);

            CompilationResult compilation = compiler.Compile(options);
            OutputResult output = compiler.WriteOutput(compilation, options);

            Assert.True(compilation.Success);
            Assert.True(output.Success);

            ImmutableArray<string> paths = output.QmlFiles
                .AddRange(output.SchemaFiles)
                .AddRange(output.SourceMapFiles)
                .Add(output.EventBindingsFile!);
            ImmutableDictionary<string, string>.Builder artifacts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            foreach (string path in paths.Order(StringComparer.Ordinal))
            {
                string relativePath = Path.GetRelativePath(outputDir, path).Replace('\\', '/');
                artifacts.Add(relativePath, NormalizeJsonArtifact(relativePath, File.ReadAllText(path)));
            }

            return artifacts.ToImmutable();
        }

        private static string NormalizeJsonArtifact(string relativePath, string text)
        {
            if (!relativePath.EndsWith(".json", StringComparison.Ordinal)
                && !relativePath.EndsWith(".map", StringComparison.Ordinal))
            {
                return text;
            }

            using JsonDocument document = JsonDocument.Parse(text);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        }

        private static string[] CompileDiagnostics(ImmutableArray<(string FileName, string Source)> sources)
        {
            using ProjectContext context = RoslynTestHelper.CreateContext(sources);
            ICompiler compiler = CreateCompiler(context);
            DiagnosticReporter reporter = new();

            return compiler.Compile(CompilerTestFixtures.DefaultOptions)
                .Diagnostics
                .Select(reporter.Format)
                .ToArray();
        }

        private static ICompiler CreateCompiler(ProjectContext context)
        {
            return new QmlSharp.Compiler.QmlCompiler(
                new StaticAnalyzer(context),
                new ViewModelExtractor(),
                static () => new IdAllocator(),
                new DslTransformer(),
                new ImportResolver(),
                new PostProcessor(),
                new QmlEmitter(),
                Registry,
                new SourceMapManager(),
                new EventBindingsBuilder(),
                new CompilerOutputWriter());
        }

        private sealed class StaticAnalyzer : ICSharpAnalyzer
        {
            private readonly ProjectContext projectContext;
            private readonly CSharpAnalyzer inner = new();

            public StaticAnalyzer(ProjectContext context)
            {
                projectContext = context;
            }

            public ProjectContext CreateProjectContext(CompilerOptions options)
            {
                ArgumentNullException.ThrowIfNull(options);
                return projectContext;
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
