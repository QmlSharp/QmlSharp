using System.Diagnostics;
using Microsoft.CodeAnalysis;
using QmlSharp.Qml.Emitter;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Default top-level compiler pipeline implementation.
    /// </summary>
    public sealed class QmlCompiler : ICompiler
    {
        private const string AnalyzePhase = "Analyze";
        private const string EmitPhase = "EmittingQml";
        private const string GeneralPhase = "CompilerPipeline";
        private const string DefaultSnapshotRelativePath = "data/qt-registry-snapshots/qt-6.11.0-registry.snapshot.bin";

        private readonly ICSharpAnalyzer analyzer;
        private readonly IViewModelExtractor viewModelExtractor;
        private readonly IIdAllocator idAllocator;
        private readonly IDslTransformer dslTransformer;
        private readonly IImportResolver importResolver;
        private readonly IPostProcessor postProcessor;
        private readonly IQmlEmitter qmlEmitter;
        private readonly IRegistryQuery registry;
        private readonly ISourceMapManager sourceMapManager;
        private readonly IEventBindingsBuilder eventBindingsBuilder;
        private readonly CompilerOutputWriter outputWriter;
        private readonly List<Action<CompilationProgress>> progressCallbacks = [];

        /// <summary>
        /// Initializes a new compiler pipeline using the checked-in Qt 6.11 registry snapshot when available.
        /// </summary>
        public QmlCompiler()
            : this(LoadDefaultRegistryQuery())
        {
        }

        /// <summary>
        /// Initializes a new compiler pipeline with an explicit QML registry query service.
        /// </summary>
        /// <param name="registry">The QML registry query service used for DSL validation.</param>
        public QmlCompiler(IRegistryQuery registry)
            : this(
                new CSharpAnalyzer(),
                new ViewModelExtractor(),
                new IdAllocator(),
                new DslTransformer(),
                new ImportResolver(),
                new PostProcessor(),
                new QmlEmitter(),
                registry,
                new SourceMapManager(),
                new EventBindingsBuilder(),
                new CompilerOutputWriter())
        {
        }

        /// <summary>
        /// Initializes a new compiler pipeline with explicit stage dependencies.
        /// </summary>
        public QmlCompiler(
            ICSharpAnalyzer analyzer,
            IViewModelExtractor viewModelExtractor,
            IIdAllocator idAllocator,
            IDslTransformer dslTransformer,
            IImportResolver importResolver,
            IPostProcessor postProcessor,
            IQmlEmitter qmlEmitter,
            IRegistryQuery registry,
            ISourceMapManager sourceMapManager,
            IEventBindingsBuilder eventBindingsBuilder,
            CompilerOutputWriter outputWriter)
        {
            this.analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            this.viewModelExtractor = viewModelExtractor ?? throw new ArgumentNullException(nameof(viewModelExtractor));
            this.idAllocator = idAllocator ?? throw new ArgumentNullException(nameof(idAllocator));
            this.dslTransformer = dslTransformer ?? throw new ArgumentNullException(nameof(dslTransformer));
            this.importResolver = importResolver ?? throw new ArgumentNullException(nameof(importResolver));
            this.postProcessor = postProcessor ?? throw new ArgumentNullException(nameof(postProcessor));
            this.qmlEmitter = qmlEmitter ?? throw new ArgumentNullException(nameof(qmlEmitter));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.sourceMapManager = sourceMapManager ?? throw new ArgumentNullException(nameof(sourceMapManager));
            this.eventBindingsBuilder = eventBindingsBuilder ?? throw new ArgumentNullException(nameof(eventBindingsBuilder));
            this.outputWriter = outputWriter ?? throw new ArgumentNullException(nameof(outputWriter));
        }

        /// <inheritdoc />
        public CompilationResult Compile(CompilerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            Stopwatch stopwatch = Stopwatch.StartNew();
            CompilerOptions normalizedOptions = options.ValidateAndNormalize();

            Report(CompilationPhase.LoadingProject, 0, 0, normalizedOptions.ProjectPath);
            using ProjectContext context = analyzer.CreateProjectContext(normalizedOptions);

            Report(CompilationPhase.Analyzing, 0, context.SourceFiles.Length, "Discovering Views and ViewModels.");
            ImmutableArray<DiscoveredView> views = analyzer.DiscoverViews(context);
            ImmutableArray<DiscoveredViewModel> viewModels = analyzer.DiscoverViewModels(context);

            Report(CompilationPhase.ExtractingViewModels, 0, views.Length, "Pairing Views with ViewModels.");
            ImmutableDictionary<string, DiscoveredViewModel> viewModelsByMetadataName = CreateViewModelLookup(viewModels);

            ImmutableArray<CompilationUnit>.Builder units = ImmutableArray.CreateBuilder<CompilationUnit>();
            int currentFile = 0;
            foreach (DiscoveredView view in views.OrderBy(static discoveredView => discoveredView.FilePath, StringComparer.Ordinal)
                .ThenBy(static discoveredView => discoveredView.ClassName, StringComparer.Ordinal))
            {
                currentFile++;
                Report(CompilationPhase.TransformingDsl, currentFile, views.Length, view.FilePath);
                units.Add(CompileView(view, context, normalizedOptions, viewModelsByMetadataName));
            }

            EventBindingsIndex eventBindings = eventBindingsBuilder.Build(units
                .Where(static unit => unit.Schema is not null)
                .Select(static unit => unit.Schema!)
                .ToImmutableArray());
            ImmutableArray<CompilerDiagnostic> projectDiagnostics = GetProjectDiagnosticsNotOwnedByUnits(context, units.ToImmutable());

            stopwatch.Stop();
            CompilationResult result = CompilationResult.FromUnits(
                units.ToImmutable(),
                projectDiagnostics,
                eventBindings,
                stopwatch.ElapsedMilliseconds);

            Report(CompilationPhase.Done, views.Length, views.Length, result.Success ? "Compilation completed." : "Compilation completed with diagnostics.");
            return result;
        }

        /// <inheritdoc />
        public CompilationUnit CompileFile(string filePath, ProjectContext context, CompilerOptions options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(options);

            CompilerOptions normalizedOptions = options.ValidateAndNormalize();
            ImmutableArray<DiscoveredView> views = analyzer.DiscoverViews(context);
            DiscoveredView? view = views
                .Where(candidate => PathsEqual(candidate.FilePath, filePath))
                .OrderBy(static candidate => candidate.ClassName, StringComparer.Ordinal)
                .FirstOrDefault();

            if (view is null)
            {
                CompilerDiagnostic diagnostic = new(
                    DiagnosticCodes.ViewMissingBuildMethod,
                    DiagnosticSeverity.Error,
                    DiagnosticMessageCatalog.FormatMessage(DiagnosticCodes.ViewMissingBuildMethod, $"No View class was found in '{filePath}'."),
                    SourceLocation.FileOnly(filePath),
                    AnalyzePhase);
                return CreateFailedUnit(filePath, Path.GetFileNameWithoutExtension(filePath), string.Empty, [diagnostic], elapsedMilliseconds: 0);
            }

            ImmutableDictionary<string, DiscoveredViewModel> viewModelsByMetadataName = CreateViewModelLookup(analyzer.DiscoverViewModels(context));
            return CompileView(view, context, normalizedOptions, viewModelsByMetadataName);
        }

        /// <inheritdoc />
        public OutputResult WriteOutput(CompilationResult result, CompilerOptions options)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(options);

            Report(CompilationPhase.WritingArtifacts, 0, result.Units.Length, options.OutputDir);
            OutputResult outputResult = outputWriter.WriteOutput(result, options);
            Report(CompilationPhase.Done, result.Units.Length, result.Units.Length, outputResult.Success ? "Output writing completed." : "Output writing completed with diagnostics.");
            return outputResult;
        }

        /// <inheritdoc />
        public void OnProgress(Action<CompilationProgress> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            progressCallbacks.Add(callback);
        }

        private CompilationUnit CompileView(
            DiscoveredView view,
            ProjectContext context,
            CompilerOptions options,
            ImmutableDictionary<string, DiscoveredViewModel> viewModelsByMetadataName)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<CompilerDiagnostic>();

            try
            {
                if (!TryGetBoundViewModel(view, viewModelsByMetadataName, diagnostics, out DiscoveredViewModel? boundViewModel)
                    || boundViewModel is null)
                {
                    stopwatch.Stop();
                    return CreateFailedUnit(view.FilePath, view.ClassName, view.ViewModelTypeName, diagnostics.ToImmutable(), stopwatch.ElapsedMilliseconds);
                }

                ViewCompilationArtifacts artifacts = BuildViewArtifacts(view, context, options, diagnostics);

                if (HasErrors(diagnostics))
                {
                    stopwatch.Stop();
                    return CreateUnit(
                        view.FilePath,
                        view.ClassName,
                        view.ViewModelTypeName,
                        qmlText: null,
                        artifacts.Schema,
                        artifacts.PostProcessResult.Document,
                        sourceMap: null,
                        diagnostics.ToImmutable(),
                        stopwatch.ElapsedMilliseconds);
                }

                Report(CompilationPhase.EmittingQml, 0, 0, view.FilePath);
                string qmlText = EmitQml(artifacts.PostProcessResult.Document, view.FilePath, diagnostics);
                SourceMap sourceMap = CreateSourceMap(view, artifacts.TransformResult.SourceMappings);

                stopwatch.Stop();
                return CreateUnit(
                    view.FilePath,
                    view.ClassName,
                    view.ViewModelTypeName,
                    qmlText,
                    artifacts.Schema,
                    artifacts.PostProcessResult.Document,
                    sourceMap,
                    diagnostics.ToImmutable(),
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or NotSupportedException)
            {
                diagnostics.Add(new CompilerDiagnostic(
                    DiagnosticCodes.InternalError,
                    DiagnosticSeverity.Fatal,
                    DiagnosticMessageCatalog.FormatMessage(DiagnosticCodes.InternalError, exception.Message),
                    SourceLocation.FileOnly(view.FilePath),
                    GeneralPhase));
                stopwatch.Stop();
                return CreateFailedUnit(view.FilePath, view.ClassName, view.ViewModelTypeName, diagnostics.ToImmutable(), stopwatch.ElapsedMilliseconds);
            }
        }

        private ViewCompilationArtifacts BuildViewArtifacts(
            DiscoveredView view,
            ProjectContext context,
            CompilerOptions options,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            ViewModelSchema schema = ExtractSchema(view, context, diagnostics);
            DslTransformResult transformResult = dslTransformer.Transform(view, context, registry);
            diagnostics.AddRange(transformResult.Diagnostics);

            ImmutableArray<DiscoveredImport> discoveredImports = analyzer.DiscoverImports(context, view.FilePath);
            ImmutableArray<ResolvedImport> resolvedImports = importResolver.Resolve(discoveredImports, options);

            Report(CompilationPhase.PostProcessing, 0, 0, view.FilePath);
            PostProcessResult postProcessResult = postProcessor.Process(
                transformResult.Document,
                view,
                schema,
                resolvedImports,
                options);
            diagnostics.AddRange(postProcessResult.Diagnostics);

            return new ViewCompilationArtifacts(schema, transformResult, postProcessResult);
        }

        private ViewModelSchema ExtractSchema(
            DiscoveredView view,
            ProjectContext context,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            ImmutableArray<CompilerDiagnostic> beforeExtractionDiagnostics = context.Diagnostics.GetDiagnostics();
            ViewModelSchema schema = viewModelExtractor.Extract(view, context, idAllocator);
            diagnostics.AddRange(GetNewDiagnostics(beforeExtractionDiagnostics, context.Diagnostics.GetDiagnostics()));
            return schema;
        }

        private string EmitQml(
            QmlSharp.Qml.Ast.QmlDocument document,
            string sourceFilePath,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            try
            {
                return NormalizeText(qmlEmitter.Emit(document, CreateEmitOptions()));
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or NotSupportedException)
            {
                diagnostics.Add(new CompilerDiagnostic(
                    DiagnosticCodes.EmitFailed,
                    DiagnosticSeverity.Error,
                    DiagnosticMessageCatalog.FormatMessage(DiagnosticCodes.EmitFailed, exception.Message),
                    SourceLocation.FileOnly(sourceFilePath),
                    EmitPhase));
                return string.Empty;
            }
        }

        private SourceMap CreateSourceMap(DiscoveredView view, ImmutableArray<SourceMapping> mappings)
        {
            ISourceMapBuilder builder = sourceMapManager.CreateBuilder(view.FilePath, $"{view.ClassName}.qml");
            foreach (SourceMapping mapping in (mappings.IsDefault ? ImmutableArray<SourceMapping>.Empty : mappings)
                .OrderBy(static mapping => mapping.Output.Line ?? 1)
                .ThenBy(static mapping => mapping.Output.Column ?? 1)
                .ThenBy(static mapping => mapping.Source.FilePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static mapping => mapping.Source.Line ?? 1)
                .ThenBy(static mapping => mapping.Source.Column ?? 1)
                .ThenBy(static mapping => mapping.Symbol, StringComparer.Ordinal)
                .ThenBy(static mapping => mapping.NodeKind, StringComparer.Ordinal))
            {
                if (mapping.Source.FilePath is null || mapping.Source.Line is null || mapping.Source.Column is null)
                {
                    continue;
                }

                builder.AddMapping(new SourceMapMapping(
                    mapping.Output.Line ?? 1,
                    mapping.Output.Column ?? 1,
                    mapping.Source.FilePath,
                    mapping.Source.Line.Value,
                    mapping.Source.Column.Value,
                    mapping.Symbol,
                    mapping.NodeKind));
            }

            return builder.Build();
        }

        private static EmitOptions CreateEmitOptions()
        {
            return new EmitOptions
            {
                Newline = NewlineStyle.Lf,
                SortImports = true,
                Normalize = false,
                TrailingNewline = true,
                InsertBlankLinesBetweenSections = true,
                InsertBlankLinesBetweenObjects = true,
                InsertBlankLinesBetweenFunctions = true,
                SingleLineEmptyObjects = true,
            };
        }

        private static ImmutableDictionary<string, DiscoveredViewModel> CreateViewModelLookup(ImmutableArray<DiscoveredViewModel> viewModels)
        {
            ImmutableDictionary<string, DiscoveredViewModel>.Builder builder =
                ImmutableDictionary.CreateBuilder<string, DiscoveredViewModel>(StringComparer.Ordinal);

            foreach (DiscoveredViewModel viewModel in viewModels
                .OrderBy(static model => model.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
                .ThenBy(static model => model.FilePath, StringComparer.Ordinal))
            {
                string metadataName = viewModel.TypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (!builder.ContainsKey(metadataName))
                {
                    builder.Add(metadataName, viewModel);
                }
            }

            return builder.ToImmutable();
        }

        private static bool TryGetBoundViewModel(
            DiscoveredView view,
            ImmutableDictionary<string, DiscoveredViewModel> viewModelsByMetadataName,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            out DiscoveredViewModel? viewModel)
        {
            string metadataName = view.ViewModelSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (viewModelsByMetadataName.TryGetValue(metadataName, out DiscoveredViewModel? discoveredViewModel))
            {
                viewModel = discoveredViewModel;
                return true;
            }

            diagnostics.Add(new CompilerDiagnostic(
                DiagnosticCodes.ViewModelMissingAttribute,
                DiagnosticSeverity.Error,
                DiagnosticMessageCatalog.FormatMessage(
                    DiagnosticCodes.ViewModelMissingAttribute,
                    $"View '{view.ClassName}' is bound to '{view.ViewModelTypeName}', but the ViewModel was not discovered with [ViewModel]."),
                SourceLocation.FileOnly(view.FilePath),
                AnalyzePhase));
            viewModel = null;
            return false;
        }

        private static ImmutableArray<CompilerDiagnostic> GetProjectDiagnosticsNotOwnedByUnits(
            ProjectContext context,
            ImmutableArray<CompilationUnit> units)
        {
            ImmutableHashSet<CompilerDiagnostic> unitDiagnostics = units
                .SelectMany(static unit => unit.Diagnostics.IsDefault ? ImmutableArray<CompilerDiagnostic>.Empty : unit.Diagnostics)
                .ToImmutableHashSet();

            return context.Diagnostics.GetDiagnostics()
                .Where(diagnostic => !unitDiagnostics.Contains(diagnostic))
                .ToImmutableArray();
        }

        private static ImmutableArray<CompilerDiagnostic> GetNewDiagnostics(
            ImmutableArray<CompilerDiagnostic> before,
            ImmutableArray<CompilerDiagnostic> after)
        {
            ImmutableHashSet<CompilerDiagnostic> existing = before.IsDefault
                ? ImmutableHashSet<CompilerDiagnostic>.Empty
                : before.ToImmutableHashSet();

            return (after.IsDefault ? ImmutableArray<CompilerDiagnostic>.Empty : after)
                .Where(diagnostic => !existing.Contains(diagnostic))
                .ToImmutableArray();
        }

        private static bool HasErrors(ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            return diagnostics.Any(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Fatal);
        }

        private static CompilationUnit CreateFailedUnit(
            string sourceFilePath,
            string viewClassName,
            string viewModelClassName,
            ImmutableArray<CompilerDiagnostic> diagnostics,
            long elapsedMilliseconds)
        {
            return CreateUnit(
                sourceFilePath,
                viewClassName,
                viewModelClassName,
                qmlText: null,
                schema: null,
                document: null,
                sourceMap: null,
                diagnostics,
                elapsedMilliseconds);
        }

        private static CompilationUnit CreateUnit(
            string sourceFilePath,
            string viewClassName,
            string viewModelClassName,
            string? qmlText,
            ViewModelSchema? schema,
            QmlSharp.Qml.Ast.QmlDocument? document,
            SourceMap? sourceMap,
            ImmutableArray<CompilerDiagnostic> diagnostics,
            long elapsedMilliseconds)
        {
            return new CompilationUnit
            {
                SourceFilePath = sourceFilePath,
                ViewClassName = viewClassName,
                ViewModelClassName = viewModelClassName,
                QmlText = string.IsNullOrEmpty(qmlText) ? null : qmlText,
                Schema = schema,
                Document = document,
                SourceMap = sourceMap,
                Diagnostics = diagnostics.IsDefault
                    ? ImmutableArray<CompilerDiagnostic>.Empty
                    : diagnostics.OrderBy(static diagnostic => diagnostic.Location?.FilePath ?? string.Empty, StringComparer.Ordinal)
                        .ThenBy(static diagnostic => diagnostic.Location?.Line ?? 0)
                        .ThenBy(static diagnostic => diagnostic.Location?.Column ?? 0)
                        .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                        .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
                        .ToImmutableArray(),
                Stats = new CompilationUnitStats
                {
                    ElapsedMilliseconds = elapsedMilliseconds,
                    QmlBytes = string.IsNullOrEmpty(qmlText) ? 0 : System.Text.Encoding.UTF8.GetByteCount(qmlText),
                },
            };
        }

        private void Report(CompilationPhase phase, int currentFile, int totalFiles, string? detail)
        {
            if (progressCallbacks.Count == 0)
            {
                return;
            }

            CompilationProgress progress = new(phase, currentFile, totalFiles, detail);
            foreach (Action<CompilationProgress> callback in progressCallbacks)
            {
                callback(progress);
            }
        }

        private static IRegistryQuery LoadDefaultRegistryQuery()
        {
            string snapshotPath = FindDefaultSnapshotPath();
            BuildResult result = new RegistryBuilder().LoadFromSnapshot(snapshotPath);
            if (result.Query is null)
            {
                string message = result.Diagnostics.IsDefaultOrEmpty
                    ? $"Failed to load checked-in registry snapshot '{snapshotPath}'."
                    : string.Join(" ", result.Diagnostics.Select(static diagnostic => diagnostic.Message));
                throw new InvalidOperationException(message);
            }

            return result.Query;
        }

        private static string FindDefaultSnapshotPath()
        {
            string currentDirectory = AppContext.BaseDirectory;
            for (int index = 0; index < 8; index++)
            {
                string candidate = Path.GetFullPath(Path.Join(currentDirectory, DefaultSnapshotRelativePath));
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                DirectoryInfo? parent = Directory.GetParent(currentDirectory);
                if (parent is null)
                {
                    break;
                }

                currentDirectory = parent.FullName;
            }

            string workingTreeCandidate = Path.GetFullPath(DefaultSnapshotRelativePath);
            if (File.Exists(workingTreeCandidate))
            {
                return workingTreeCandidate;
            }

            throw new FileNotFoundException("The checked-in Qt 6.11 registry snapshot was not found.", DefaultSnapshotRelativePath);
        }

        private static bool PathsEqual(string left, string right)
        {
            StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return string.Equals(NormalizePath(left), NormalizePath(right), comparison);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string NormalizeText(string text)
        {
            string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            return normalized.EndsWith("\n", StringComparison.Ordinal) ? normalized : normalized + "\n";
        }

        private sealed record ViewCompilationArtifacts(
            ViewModelSchema Schema,
            DslTransformResult TransformResult,
            PostProcessResult PostProcessResult);
    }
}
