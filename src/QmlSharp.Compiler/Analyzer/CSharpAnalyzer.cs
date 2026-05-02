using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Roslyn-backed implementation of <see cref="ICSharpAnalyzer"/>.
    /// </summary>
    public sealed class CSharpAnalyzer : ICSharpAnalyzer
    {
        private const string ViewMetadataName = "QmlSharp.Core.View`1";
        private const string ViewModelAttributeMetadataName = "QmlSharp.Core.ViewModelAttribute";
        private const string AnalysisPhase = "Analyze";

        private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        /// <inheritdoc />
        public ProjectContext CreateProjectContext(CompilerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            CompilerOptions normalizedOptions = options.ValidateAndNormalize();
            DiagnosticReporter diagnostics = new();

            if (!File.Exists(normalizedOptions.ProjectPath))
            {
                _ = diagnostics.Report(
                    DiagnosticCodes.ProjectLoadFailed,
                    DiagnosticSeverity.Error,
                    SourceLocation.FileOnly(normalizedOptions.ProjectPath),
                    AnalysisPhase,
                    "Project file does not exist.");

                return new ProjectContext(
                    Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("QmlSharp.ProjectLoadFailed"),
                    ImmutableArray<string>.Empty,
                    normalizedOptions,
                    diagnostics);
            }

            try
            {
                return LoadProjectContext(normalizedOptions, diagnostics);
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or ArgumentException)
            {
                _ = diagnostics.Report(
                    DiagnosticCodes.ProjectLoadFailed,
                    DiagnosticSeverity.Error,
                    SourceLocation.FileOnly(normalizedOptions.ProjectPath),
                    AnalysisPhase,
                    exception.Message);

                return new ProjectContext(
                    Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("QmlSharp.ProjectLoadFailed"),
                    ImmutableArray<string>.Empty,
                    normalizedOptions,
                    diagnostics);
            }
        }

        private static ProjectContext LoadProjectContext(CompilerOptions normalizedOptions, DiagnosticReporter diagnostics)
        {
            EnsureMsBuildRegistered();

            MSBuildWorkspace workspace = MSBuildWorkspace.Create();
            Project project = workspace.OpenProjectAsync(normalizedOptions.ProjectPath).GetAwaiter().GetResult();
            Compilation? compilation = project.GetCompilationAsync().GetAwaiter().GetResult();

            if (compilation is null)
            {
                return CreateNullCompilationProjectContext(project.Name, normalizedOptions, diagnostics, workspace);
            }

            ImmutableArray<string> sourceFiles = CollectSourceFiles(
                compilation,
                Path.GetDirectoryName(Path.GetFullPath(normalizedOptions.ProjectPath)) ?? Directory.GetCurrentDirectory(),
                normalizedOptions);

            ReportCompilationErrors(compilation, diagnostics, sourceFiles);

            return new ProjectContext(compilation, sourceFiles, normalizedOptions, diagnostics, workspace);
        }

        private static ProjectContext CreateNullCompilationProjectContext(
            string projectName,
            CompilerOptions normalizedOptions,
            DiagnosticReporter diagnostics,
            MSBuildWorkspace workspace)
        {
            _ = diagnostics.Report(
                DiagnosticCodes.ProjectLoadFailed,
                DiagnosticSeverity.Error,
                SourceLocation.FileOnly(normalizedOptions.ProjectPath),
                AnalysisPhase,
                "Roslyn returned no compilation for the project.");

            return new ProjectContext(
                Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(projectName),
                ImmutableArray<string>.Empty,
                normalizedOptions,
                diagnostics,
                workspace);
        }

        /// <summary>
        /// Creates a project context from an in-memory Roslyn compilation for fast unit tests.
        /// </summary>
        /// <param name="options">Compiler options to attach to the context.</param>
        /// <param name="compilation">The in-memory Roslyn compilation.</param>
        /// <param name="sourceFiles">Optional source file list. Defaults to compilation syntax tree paths.</param>
        /// <param name="diagnostics">Optional diagnostic reporter.</param>
        /// <returns>A project context backed by the supplied compilation.</returns>
        public ProjectContext CreateInMemoryProjectContext(
            CompilerOptions options,
            Compilation compilation,
            ImmutableArray<string> sourceFiles = default,
            IDiagnosticReporter? diagnostics = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(compilation);

            CompilerOptions normalizedOptions = options.ValidateAndNormalize();
            IDiagnosticReporter reporter = diagnostics ?? new DiagnosticReporter();
            ImmutableArray<string> files = sourceFiles.IsDefault
                ? compilation.SyntaxTrees.Select(static tree => tree.FilePath).ToImmutableArray()
                : sourceFiles;

            ReportCompilationErrors(compilation, reporter, files);
            return new ProjectContext(compilation, files, normalizedOptions, reporter);
        }

        /// <inheritdoc />
        public ImmutableArray<DiscoveredView> DiscoverViews(ProjectContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            INamedTypeSymbol? viewBaseSymbol = context.Compilation.GetTypeByMetadataName(ViewMetadataName);
            if (viewBaseSymbol is null)
            {
                return ImmutableArray<DiscoveredView>.Empty;
            }

            ImmutableArray<INamedTypeSymbol> classSymbols = DiscoverClassSymbols(context);
            ImmutableArray<DiscoveredView>.Builder views = ImmutableArray.CreateBuilder<DiscoveredView>();
            HashSet<ISymbol> seen = new(SymbolEqualityComparer.Default);

            foreach (INamedTypeSymbol classSymbol in classSymbols)
            {
                if (!seen.Add(classSymbol))
                {
                    continue;
                }

                INamedTypeSymbol? viewModelSymbol = FindViewModelTypeArgument(classSymbol, viewBaseSymbol);
                if (viewModelSymbol is null)
                {
                    continue;
                }

                views.Add(new DiscoveredView(
                    classSymbol.Name,
                    GetSourceFilePath(classSymbol),
                    viewModelSymbol.Name,
                    classSymbol,
                    viewModelSymbol));
            }

            return views
                .OrderBy(static view => view.FilePath, PathComparer)
                .ThenBy(static view => view.ClassName, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        /// <inheritdoc />
        public ImmutableArray<DiscoveredViewModel> DiscoverViewModels(ProjectContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            ImmutableArray<INamedTypeSymbol> classSymbols = DiscoverClassSymbols(context);
            ImmutableArray<DiscoveredViewModel>.Builder viewModels = ImmutableArray.CreateBuilder<DiscoveredViewModel>();
            HashSet<ISymbol> seen = new(SymbolEqualityComparer.Default);

            foreach (INamedTypeSymbol classSymbol in classSymbols)
            {
                if (!seen.Add(classSymbol) || !HasAttribute(classSymbol, ViewModelAttributeMetadataName))
                {
                    continue;
                }

                viewModels.Add(new DiscoveredViewModel(
                    classSymbol.Name,
                    GetSourceFilePath(classSymbol),
                    classSymbol));
            }

            return viewModels
                .OrderBy(static viewModel => viewModel.FilePath, PathComparer)
                .ThenBy(static viewModel => viewModel.ClassName, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        /// <inheritdoc />
        public ImmutableArray<DiscoveredImport> DiscoverImports(ProjectContext context, string filePath)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            SyntaxTree syntaxTree = GetSyntaxTree(context, filePath);
            if (!HasValidSyntax(syntaxTree))
            {
                return ImmutableArray<DiscoveredImport>.Empty;
            }

            SyntaxNode root = syntaxTree.GetRoot();
            ImmutableArray<DiscoveredImport>.Builder imports = ImmutableArray.CreateBuilder<DiscoveredImport>();

            foreach (UsingDirectiveSyntax usingDirective in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            {
                if (usingDirective.Name is null)
                {
                    continue;
                }

                string namespaceName = usingDirective.Name.ToString();
                if (!IsDslNamespace(namespaceName))
                {
                    continue;
                }

                FileLinePositionSpan lineSpan = usingDirective.GetLocation().GetLineSpan();
                imports.Add(new DiscoveredImport(
                    namespaceName,
                    syntaxTree.FilePath,
                    lineSpan.StartLinePosition.Line + 1));
            }

            return imports
                .OrderBy(static import => import.LineNumber)
                .ThenBy(static import => import.CSharpNamespace, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        /// <inheritdoc />
        public SemanticModel GetSemanticModel(ProjectContext context, string filePath)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            SyntaxTree syntaxTree = GetSyntaxTree(context, filePath);
            return context.Compilation.GetSemanticModel(syntaxTree);
        }

        private static void EnsureMsBuildRegistered()
        {
            if (!MSBuildLocator.IsRegistered)
            {
                _ = MSBuildLocator.RegisterDefaults();
            }
        }

        private static ImmutableArray<string> CollectSourceFiles(
            Compilation compilation,
            string projectDirectory,
            CompilerOptions options)
        {
            ImmutableArray<string>.Builder files = ImmutableArray.CreateBuilder<string>();
            foreach (SyntaxTree syntaxTree in compilation.SyntaxTrees)
            {
                string filePath = syntaxTree.FilePath;
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    continue;
                }

                string relativePath = GetRelativePath(projectDirectory, filePath);
                if (MatchesAny(options.IncludePatterns, relativePath) && !MatchesAny(options.ExcludePatterns, relativePath))
                {
                    files.Add(filePath);
                }
            }

            return files
                .Distinct(PathComparer)
                .Order(PathComparer)
                .ToImmutableArray();
        }

        private static ImmutableArray<INamedTypeSymbol> DiscoverClassSymbols(ProjectContext context)
        {
            ImmutableArray<INamedTypeSymbol>.Builder symbols = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

            foreach (SyntaxTree syntaxTree in GetAnalyzableSyntaxTrees(context))
            {
                SemanticModel semanticModel = context.Compilation.GetSemanticModel(syntaxTree);
                SyntaxNode root = syntaxTree.GetRoot();

                foreach (ClassDeclarationSyntax classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    if (semanticModel.GetDeclaredSymbol(classDeclaration) is INamedTypeSymbol symbol)
                    {
                        symbols.Add(symbol);
                    }
                }
            }

            return symbols.ToImmutable();
        }

        private static IEnumerable<SyntaxTree> GetAnalyzableSyntaxTrees(ProjectContext context)
        {
            foreach (SyntaxTree syntaxTree in context.Compilation.SyntaxTrees)
            {
                if (IsSourceFileAnalyzable(context, syntaxTree))
                {
                    yield return syntaxTree;
                }
            }
        }

        private static bool IsSourceFileAnalyzable(ProjectContext context, SyntaxTree syntaxTree)
        {
            if (!ContainsSourceFile(context, syntaxTree.FilePath))
            {
                return false;
            }

            if (!HasValidSyntax(syntaxTree))
            {
                return false;
            }

            return !context.Compilation.GetDiagnostics()
                .Any(diagnostic =>
                    diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error
                    && diagnostic.Location.SourceTree is not null
                    && PathsEqual(diagnostic.Location.SourceTree.FilePath, syntaxTree.FilePath));
        }

        private static bool HasValidSyntax(SyntaxTree syntaxTree)
        {
            return !syntaxTree.GetDiagnostics().Any(static diagnostic =>
                diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        }

        private static bool ContainsSourceFile(ProjectContext context, string filePath)
        {
            return context.SourceFiles.Any(sourceFile => PathsEqual(sourceFile, filePath));
        }

        private static SyntaxTree GetSyntaxTree(ProjectContext context, string filePath)
        {
            SyntaxTree? syntaxTree = context.Compilation.SyntaxTrees
                .FirstOrDefault(tree => PathsEqual(tree.FilePath, filePath) && ContainsSourceFile(context, tree.FilePath));

            if (syntaxTree is null)
            {
                throw new FileNotFoundException("The requested source file is not part of the project context.", filePath);
            }

            return syntaxTree;
        }

        private static INamedTypeSymbol? FindViewModelTypeArgument(
            INamedTypeSymbol classSymbol,
            INamedTypeSymbol viewBaseSymbol)
        {
            for (INamedTypeSymbol? current = classSymbol.BaseType; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, viewBaseSymbol)
                    && current.TypeArguments.Length == 1
                    && current.TypeArguments[0] is INamedTypeSymbol viewModelSymbol)
                {
                    return viewModelSymbol;
                }
            }

            return null;
        }

        private static bool HasAttribute(ISymbol symbol, string metadataName)
        {
            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                if (StringComparer.Ordinal.Equals(attribute.AttributeClass?.ToDisplayString(), metadataName))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetSourceFilePath(INamedTypeSymbol symbol)
        {
            Location? location = symbol.Locations
                .Where(static candidate => candidate.IsInSource)
                .OrderBy(static candidate => candidate.SourceTree?.FilePath ?? string.Empty, PathComparer)
                .ThenBy(static candidate => candidate.GetLineSpan().StartLinePosition.Line)
                .ThenBy(static candidate => candidate.GetLineSpan().StartLinePosition.Character)
                .FirstOrDefault();

            return location?.SourceTree?.FilePath ?? string.Empty;
        }

        private static bool IsDslNamespace(string namespaceName)
        {
            return StringComparer.Ordinal.Equals(namespaceName, "QmlSharp.QtQml")
                || namespaceName.StartsWith("QmlSharp.QtQuick", StringComparison.Ordinal);
        }

        private static void ReportCompilationErrors(
            Compilation compilation,
            IDiagnosticReporter reporter,
            ImmutableArray<string> sourceFiles)
        {
            ImmutableArray<Diagnostic> diagnostics = compilation.GetDiagnostics()
                .Where(diagnostic =>
                    diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error
                    && IsDiagnosticInSourceSet(diagnostic, sourceFiles))
                .OrderBy(static diagnostic => diagnostic.Location.SourceTree?.FilePath ?? string.Empty, PathComparer)
                .ThenBy(static diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line)
                .ThenBy(static diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Character)
                .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
                .ToImmutableArray();

            foreach (Diagnostic diagnostic in diagnostics)
            {
                reporter.Report(new CompilerDiagnostic(
                    DiagnosticCodes.RoslynCompilationFailed,
                    DiagnosticSeverity.Error,
                    diagnostic.GetMessage(),
                    ToSourceLocation(diagnostic.Location),
                    AnalysisPhase));
            }
        }

        private static bool IsDiagnosticInSourceSet(Diagnostic diagnostic, ImmutableArray<string> sourceFiles)
        {
            SyntaxTree? sourceTree = diagnostic.Location.SourceTree;
            if (sourceTree is null)
            {
                return true;
            }

            return sourceFiles.Any(sourceFile => PathsEqual(sourceFile, sourceTree.FilePath));
        }

        private static SourceLocation? ToSourceLocation(Location location)
        {
            if (!location.IsInSource)
            {
                return null;
            }

            FileLinePositionSpan lineSpan = location.GetLineSpan();
            return SourceLocation.Partial(
                lineSpan.Path,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1);
        }

        private static string GetRelativePath(string projectDirectory, string filePath)
        {
            if (Path.IsPathRooted(filePath))
            {
                return Path.GetRelativePath(projectDirectory, filePath);
            }

            return filePath;
        }

        private static bool MatchesAny(ImmutableArray<string> patterns, string path)
        {
            if (patterns.IsDefaultOrEmpty)
            {
                return false;
            }

            string normalizedPath = NormalizePath(path);
            foreach (string pattern in patterns)
            {
                if (GlobMatches(pattern, normalizedPath))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool GlobMatches(string pattern, string normalizedPath)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return false;
            }

            string normalizedPattern = NormalizePath(pattern);
            string[] patternSegments = normalizedPattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string[] pathSegments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return MatchSegments(patternSegments, 0, pathSegments, 0);
        }

        private static bool MatchSegments(
            string[] patternSegments,
            int patternIndex,
            string[] pathSegments,
            int pathIndex)
        {
            if (patternIndex == patternSegments.Length)
            {
                return pathIndex == pathSegments.Length;
            }

            if (StringComparer.Ordinal.Equals(patternSegments[patternIndex], "**"))
            {
                for (int index = pathIndex; index <= pathSegments.Length; index++)
                {
                    if (MatchSegments(patternSegments, patternIndex + 1, pathSegments, index))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (pathIndex >= pathSegments.Length)
            {
                return false;
            }

            return SegmentMatches(patternSegments[patternIndex], pathSegments[pathIndex])
                && MatchSegments(patternSegments, patternIndex + 1, pathSegments, pathIndex + 1);
        }

        private static bool SegmentMatches(string patternSegment, string pathSegment)
        {
            return SegmentMatches(patternSegment, 0, pathSegment, 0);
        }

        private static bool SegmentMatches(
            string patternSegment,
            int patternIndex,
            string pathSegment,
            int pathIndex)
        {
            if (patternIndex == patternSegment.Length)
            {
                return pathIndex == pathSegment.Length;
            }

            if (patternSegment[patternIndex] == '*')
            {
                for (int index = pathIndex; index <= pathSegment.Length; index++)
                {
                    if (SegmentMatches(patternSegment, patternIndex + 1, pathSegment, index))
                    {
                        return true;
                    }
                }

                return false;
            }

            return pathIndex < pathSegment.Length
                && patternSegment[patternIndex] == pathSegment[pathIndex]
                && SegmentMatches(patternSegment, patternIndex + 1, pathSegment, pathIndex + 1);
        }

        private static bool PathsEqual(string left, string right)
        {
            return PathComparer.Equals(NormalizePath(left), NormalizePath(right));
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
