#pragma warning disable MA0048

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Analyzes C# source files with Roslyn and discovers compiler inputs.
    /// </summary>
    public interface ICSharpAnalyzer
    {
        /// <summary>Creates project-level analysis context from compiler options.</summary>
        ProjectContext CreateProjectContext(CompilerOptions options);

        /// <summary>Discovers View classes deriving from <c>View&lt;TViewModel&gt;</c>.</summary>
        ImmutableArray<DiscoveredView> DiscoverViews(ProjectContext context);

        /// <summary>Discovers classes marked with <c>[ViewModel]</c>.</summary>
        ImmutableArray<DiscoveredViewModel> DiscoverViewModels(ProjectContext context);

        /// <summary>Discovers DSL namespace imports from a source file.</summary>
        ImmutableArray<DiscoveredImport> DiscoverImports(ProjectContext context, string filePath);

        /// <summary>Gets the Roslyn semantic model for a source file.</summary>
        Microsoft.CodeAnalysis.SemanticModel GetSemanticModel(ProjectContext context, string filePath);
    }

    /// <summary>
    /// Project-level Roslyn analysis state shared across compiler stages.
    /// </summary>
    public sealed class ProjectContext : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ProjectContext"/> class.</summary>
        public ProjectContext(
            Microsoft.CodeAnalysis.Compilation compilation,
            ImmutableArray<string> sourceFiles,
            CompilerOptions options,
            IDiagnosticReporter diagnostics,
            IDisposable? ownedResource = null)
        {
            ArgumentNullException.ThrowIfNull(compilation);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(diagnostics);

            Compilation = compilation;
            SourceFiles = sourceFiles;
            Options = options;
            Diagnostics = diagnostics;
            OwnedResource = ownedResource;
        }

        /// <summary>Gets the Roslyn compilation.</summary>
        public Microsoft.CodeAnalysis.Compilation Compilation { get; }

        /// <summary>Gets source files participating in the compiler run.</summary>
        public ImmutableArray<string> SourceFiles { get; }

        /// <summary>Gets the compiler options that created this context.</summary>
        public CompilerOptions Options { get; }

        /// <summary>Gets the shared diagnostic reporter.</summary>
        public IDiagnosticReporter Diagnostics { get; }

        private IDisposable? OwnedResource { get; set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            OwnedResource?.Dispose();
            OwnedResource = null;
        }
    }

    /// <summary>A discovered View class and its ViewModel binding.</summary>
    public sealed record DiscoveredView(
        string ClassName,
        string FilePath,
        string ViewModelTypeName,
        Microsoft.CodeAnalysis.INamedTypeSymbol TypeSymbol,
        Microsoft.CodeAnalysis.INamedTypeSymbol ViewModelSymbol);

    /// <summary>A discovered ViewModel class.</summary>
    public sealed record DiscoveredViewModel(
        string ClassName,
        string FilePath,
        Microsoft.CodeAnalysis.INamedTypeSymbol TypeSymbol);

    /// <summary>A discovered C# using directive that may map to a QML import.</summary>
    public sealed record DiscoveredImport(
        string CSharpNamespace,
        string FilePath,
        int LineNumber);

    /// <summary>A source file discovered for compiler analysis.</summary>
    public sealed record DiscoveredSourceFile(
        string FilePath,
        string RelativePath,
        bool IsGenerated,
        ImmutableArray<DiscoveredImport> Imports);
}

#pragma warning restore MA0048
