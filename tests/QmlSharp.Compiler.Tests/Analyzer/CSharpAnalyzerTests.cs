using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.Analyzer
{
    public sealed class CSharpAnalyzerTests
    {
        private readonly CSharpAnalyzer analyzer = new();

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_CA01_CreateInMemoryProjectContextWithValidSource_ReturnsContextWithoutErrors()
        {
            ProjectContext context = CreateContext(("CounterViewModel.cs", CompilerSourceFixtures.CounterViewModelSource));

            Assert.NotNull(context);
            Assert.Empty(context.Diagnostics.GetDiagnostics(DiagnosticSeverity.Error));
            Assert.Equal("CounterViewModel.cs", Assert.Single(context.SourceFiles));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_CA02_DiscoverViewsSingleView_ReturnsViewWithViewModelBinding()
        {
            ProjectContext context = CreateContext(
                ("CounterViewModel.cs", CompilerSourceFixtures.CounterViewModelSource),
                ("CounterView.cs", CompilerSourceFixtures.CounterViewSource));

            DiscoveredView view = Assert.Single(analyzer.DiscoverViews(context));

            Assert.Equal("CounterView", view.ClassName);
            Assert.Equal("CounterViewModel", view.ViewModelTypeName);
            Assert.Equal("CounterViewModel", view.ViewModelSymbol.Name);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_CA03_DiscoverViewsMultipleViews_ReturnsAllViews()
        {
            ProjectContext context = CreateContext(CompilerSourceFixtures.MultiFileProject());

            ImmutableArray<DiscoveredView> views = analyzer.DiscoverViews(context);

            Assert.Equal(["CounterView", "TodoView"], views.Select(static view => view.ClassName).Order(StringComparer.Ordinal));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_CA04_DiscoverViewsClassNotDerivingFromView_IsIgnored()
        {
            const string plainClass = """
                namespace TestApp;

                public sealed class PlainService
                {
                }
                """;
            ProjectContext context = CreateContext(("PlainService.cs", plainClass));

            ImmutableArray<DiscoveredView> views = analyzer.DiscoverViews(context);

            Assert.Empty(views);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_CA05_DiscoverViewModelsSingleAttribute_ReturnsViewModel()
        {
            ProjectContext context = CreateContext(("CounterViewModel.cs", CompilerSourceFixtures.CounterViewModelSource));

            DiscoveredViewModel viewModel = Assert.Single(analyzer.DiscoverViewModels(context));

            Assert.Equal("CounterViewModel", viewModel.ClassName);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_CA06_DiscoverViewModelsMultipleAttributes_ReturnsAllViewModels()
        {
            ProjectContext context = CreateContext(CompilerSourceFixtures.MultiFileProject());

            ImmutableArray<DiscoveredViewModel> viewModels = analyzer.DiscoverViewModels(context);

            Assert.Equal(["CounterViewModel", "TodoViewModel"], viewModels.Select(static model => model.ClassName).Order(StringComparer.Ordinal));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_CA07_DiscoverViewModelsClassWithoutAttribute_IsIgnored()
        {
            const string plainClass = """
                namespace TestApp;

                public sealed class PlainViewModel
                {
                }
                """;
            ProjectContext context = CreateContext(("PlainViewModel.cs", plainClass));

            ImmutableArray<DiscoveredViewModel> viewModels = analyzer.DiscoverViewModels(context);

            Assert.Empty(viewModels);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_CA08_DiscoverImportsQtQuickUsing_ReturnsDslNamespace()
        {
            const string source = """
                using QmlSharp.QtQuick;

                namespace TestApp;
                """;
            ProjectContext context = CreateContext(("Imports.cs", source));

            DiscoveredImport import = Assert.Single(analyzer.DiscoverImports(context, "Imports.cs"));

            Assert.Equal("QmlSharp.QtQuick", import.CSharpNamespace);
            Assert.Equal(1, import.LineNumber);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_CA09_DiscoverImportsMultipleDslUsings_ReturnsAllDslNamespaces()
        {
            const string source = """
                using QmlSharp.QtQuick;
                using QmlSharp.QtQuick.Controls;
                using QmlSharp.QtQml;

                namespace TestApp;
                """;
            ProjectContext context = CreateContext(("Imports.cs", source));

            ImmutableArray<DiscoveredImport> imports = analyzer.DiscoverImports(context, "Imports.cs");

            Assert.Equal(
                ["QmlSharp.QtQuick", "QmlSharp.QtQuick.Controls", "QmlSharp.QtQml"],
                imports.Select(static import => import.CSharpNamespace));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_CA10_DiscoverImportsNonDslUsing_IsIgnored()
        {
            const string source = """
                using System.Linq;
                using QmlSharp.Core;
                using QmlSharp.Dsl;

                namespace TestApp;
                """;
            ProjectContext context = CreateContext(("Imports.cs", source));

            ImmutableArray<DiscoveredImport> imports = analyzer.DiscoverImports(context, "Imports.cs");

            Assert.Empty(imports);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_CA11_GetSemanticModelValidFilePath_ReturnsModel()
        {
            ProjectContext context = CreateContext(("CounterViewModel.cs", CompilerSourceFixtures.CounterViewModelSource));

            SemanticModel semanticModel = analyzer.GetSemanticModel(context, "CounterViewModel.cs");

            Assert.NotNull(semanticModel);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_CA12_GetSemanticModelUnknownFilePath_ThrowsFileNotFoundException()
        {
            ProjectContext context = CreateContext(("CounterViewModel.cs", CompilerSourceFixtures.CounterViewModelSource));

            _ = Assert.Throws<FileNotFoundException>(() => analyzer.GetSemanticModel(context, "Missing.cs"));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_CA13_CompilationWithMissingReference_ReportsRoslynCompilationDiagnostic()
        {
            const string source = """
                using QmlSharp.Core;

                namespace TestApp;

                [ViewModel]
                public sealed class BrokenViewModel
                {
                    public MissingType Value { get; set; } = null!;
                }
                """;

            ProjectContext context = CreateContext(("BrokenViewModel.cs", source));

            Assert.Contains(context.Diagnostics.GetDiagnostics(), IsRoslynCompilationDiagnostic);
            Assert.Empty(analyzer.DiscoverViewModels(context));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_CA14_CompilationWithSyntaxErrors_ReportsRoslynCompilationDiagnosticAndSkipsFile()
        {
            const string source = """
                using QmlSharp.Core;

                namespace TestApp;

                [ViewModel]
                public sealed class BrokenViewModel
                {
                    [State] public int Count { get; set;
                }
                """;

            ProjectContext context = CreateContext(("BrokenViewModel.cs", source));

            Assert.Contains(context.Diagnostics.GetDiagnostics(), IsRoslynCompilationDiagnostic);
            Assert.Empty(analyzer.DiscoverViewModels(context));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_DiscoverViewsNestedNamespace_ReturnsNestedView()
        {
            const string source = """
                using QmlSharp.Core;

                namespace TestApp.Features.Counter
                {
                    [ViewModel]
                    public sealed class CounterViewModel
                    {
                    }

                    public sealed class CounterView : View<CounterViewModel>
                    {
                        public override object Build() => new object();
                    }
                }
                """;
            ProjectContext context = CreateContext(("Counter.cs", source));

            DiscoveredView view = Assert.Single(analyzer.DiscoverViews(context));
            DiscoveredViewModel viewModel = Assert.Single(analyzer.DiscoverViewModels(context));

            Assert.Equal("CounterView", view.ClassName);
            Assert.Equal("CounterViewModel", viewModel.ClassName);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_DiscoverViewModelsPartialClass_ReturnsSingleSymbol()
        {
            const string partOne = """
                using QmlSharp.Core;

                namespace TestApp;

                [ViewModel]
                public partial class CounterViewModel
                {
                }
                """;
            const string partTwo = """
                namespace TestApp;

                public partial class CounterViewModel
                {
                    [State] public int Count { get; set; }
                }
                """;
            ProjectContext context = CreateContext(("CounterViewModel.A.cs", partOne), ("CounterViewModel.B.cs", partTwo));

            ImmutableArray<DiscoveredViewModel> viewModels = analyzer.DiscoverViewModels(context);

            DiscoveredViewModel viewModel = Assert.Single(viewModels);
            Assert.Equal("CounterViewModel", viewModel.ClassName);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_DiscoverViewsIndirectGenericBase_ReturnsViewBinding()
        {
            const string source = """
                using QmlSharp.Core;

                namespace TestApp;

                [ViewModel]
                public sealed class CounterViewModel
                {
                }

                public abstract class AppView<TViewModel> : View<TViewModel>
                    where TViewModel : class
                {
                }

                public sealed class CounterView : AppView<CounterViewModel>
                {
                    public override object Build() => new object();
                }
                """;
            ProjectContext context = CreateContext(("Counter.cs", source));

            DiscoveredView view = Assert.Single(analyzer.DiscoverViews(context));

            Assert.Equal("CounterView", view.ClassName);
            Assert.Equal("CounterViewModel", view.ViewModelTypeName);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CSharpAnalyzer_CreateProjectContextWithProject_RespectsIncludeAndExcludePatterns()
        {
            using TempOutputDirectory temp = new();
            string projectPath = CreateProjectFile(temp.Path);
            WriteSource(temp.Path, "Views", "IncludedViewModel.cs", CompilerSourceFixtures.CounterViewModelSource);
            WriteSource(temp.Path, "Views", "IncludedView.cs", CompilerSourceFixtures.CounterViewSource);
            WriteSource(temp.Path, "Generated", "ExcludedViewModel.cs", CompilerSourceFixtures.TodoViewModelSource);

            CompilerOptions options = CompilerTestFixtures.DefaultOptions with
            {
                ProjectPath = projectPath,
                OutputDir = Path.Join(temp.Path, "dist"),
                IncludePatterns = ImmutableArray.Create("Views/**/*.cs", "Generated/**/*.cs"),
                ExcludePatterns = ImmutableArray.Create("Generated/**/*.cs"),
            };

            using ProjectContext context = analyzer.CreateProjectContext(options);

            Assert.Empty(context.Diagnostics.GetDiagnostics(DiagnosticSeverity.Error));
            Assert.Equal(2, context.SourceFiles.Length);
            Assert.All(context.SourceFiles, filePath => Assert.Contains($"{Path.DirectorySeparatorChar}Views{Path.DirectorySeparatorChar}", filePath));
        }

        private static bool IsRoslynCompilationDiagnostic(CompilerDiagnostic diagnostic)
        {
            return diagnostic.Code == DiagnosticCodes.RoslynCompilationFailed
                && diagnostic.Severity == DiagnosticSeverity.Error;
        }

        private ProjectContext CreateContext(params (string FileName, string Source)[] sources)
        {
            return CreateContext(sources.ToImmutableArray());
        }

        private ProjectContext CreateContext(ImmutableArray<(string FileName, string Source)> sources)
        {
            CSharpCompilation compilation = RoslynTestHelper.CreateCompilation(sources);
            ImmutableArray<string> sourceFiles = sources.Select(static source => source.FileName).ToImmutableArray();
            return analyzer.CreateInMemoryProjectContext(CompilerTestFixtures.DefaultOptions, compilation, sourceFiles);
        }

        private static string CreateProjectFile(string directory)
        {
            string projectPath = Path.Join(directory, "AnalyzerFixture.csproj");
            string repositoryRoot = FindRepositoryRoot();
            string coreProject = Path.Join(repositoryRoot, "src", "QmlSharp.Core", "QmlSharp.Core.csproj");
            string dslProject = Path.Join(repositoryRoot, "src", "QmlSharp.Dsl", "QmlSharp.Dsl.csproj");
            string project = $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="{coreProject}" />
                    <ProjectReference Include="{dslProject}" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(projectPath, project);
            return projectPath;
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Join(directory.FullName, "QmlSharp.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not find repository root from the test assembly directory.");
        }

        private static void WriteSource(string root, string folder, string fileName, string source)
        {
            string directory = Path.Join(root, folder);
            _ = Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Join(directory, fileName), source);
        }
    }
}
