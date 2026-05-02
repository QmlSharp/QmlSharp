using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.Analyzer
{
    public sealed class RoslynHelperSmokeTests
    {
        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void CreateCompilation_MinimalViewModelAndView_CompilesWithoutErrors()
        {
            CSharpCompilation compilation = RoslynTestHelper.CreateCompilation(
                CompilerSourceFixtures.CounterViewModelSource,
                CompilerSourceFixtures.CounterViewSource);

            ImmutableArray<Diagnostic> diagnostics = compilation.GetDiagnostics();
            ImmutableArray<Diagnostic> errors = diagnostics
                .Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .ToImmutableArray();

            Assert.Empty(errors);
        }

        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void CreateWithModel_ReturnsSemanticModelForSingleSource()
        {
            (CSharpCompilation compilation, SemanticModel model) =
                RoslynTestHelper.CreateWithModel(CompilerSourceFixtures.CounterViewModelSource);

            _ = Assert.Single(compilation.SyntaxTrees);
            Assert.True(model is not null);
        }
    }
}
