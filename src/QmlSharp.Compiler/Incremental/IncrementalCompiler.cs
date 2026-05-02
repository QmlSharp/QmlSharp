using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Default file-hash incremental compiler with deterministic cache persistence.
    /// </summary>
    public sealed class IncrementalCompiler : IIncrementalCompiler
    {
        private const string CacheVersion = "1.0";
        private const string CacheFileName = "incremental-cache.json";

        private static readonly bool PathsAreCaseInsensitive = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

        private static readonly StringComparer PathComparer = PathsAreCaseInsensitive
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        private static readonly JsonWriterOptions WriterOptions = new()
        {
            Indented = true,
        };

        private readonly ICompiler compiler;
        private readonly ICSharpAnalyzer analyzer;
        private readonly IViewModelExtractor viewModelExtractor;
        private readonly Func<IIdAllocator> idAllocatorFactory;
        private readonly IEventBindingsBuilder eventBindingsBuilder;
        private readonly IViewModelSchemaSerializer schemaSerializer;
        private readonly ISourceMapManager sourceMapManager;
        private readonly DependencyGraph dependencyGraph = new();
        private readonly Dictionary<string, CachedUnit> cachedUnitsByViewFile = new(PathComparer);
        private readonly HashSet<string> explicitlyInvalidated = new(PathComparer);

        private string? optionsFingerprint;
        private string? moduleFingerprint;
        private string? sourceMapFingerprint;

        /// <summary>
        /// Initializes a new instance of the <see cref="IncrementalCompiler"/> class.
        /// </summary>
        public IncrementalCompiler(ICompiler compiler)
            : this(
                compiler,
                new CSharpAnalyzer(),
                new ViewModelExtractor(),
                static () => new IdAllocator(),
                new EventBindingsBuilder(),
                new ViewModelSchemaSerializer(),
                new SourceMapManager())
        {
        }

        /// <summary>
        /// Initializes a new instance with explicit stage dependencies for tests.
        /// </summary>
        public IncrementalCompiler(
            ICompiler compiler,
            ICSharpAnalyzer analyzer,
            IViewModelExtractor viewModelExtractor,
            Func<IIdAllocator> idAllocatorFactory,
            IEventBindingsBuilder eventBindingsBuilder,
            IViewModelSchemaSerializer schemaSerializer,
            ISourceMapManager sourceMapManager)
        {
            this.compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            this.analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            this.viewModelExtractor = viewModelExtractor ?? throw new ArgumentNullException(nameof(viewModelExtractor));
            this.idAllocatorFactory = idAllocatorFactory ?? throw new ArgumentNullException(nameof(idAllocatorFactory));
            this.eventBindingsBuilder = eventBindingsBuilder ?? throw new ArgumentNullException(nameof(eventBindingsBuilder));
            this.schemaSerializer = schemaSerializer ?? throw new ArgumentNullException(nameof(schemaSerializer));
            this.sourceMapManager = sourceMapManager ?? throw new ArgumentNullException(nameof(sourceMapManager));
        }

        /// <inheritdoc />
        public CompilationResult CompileIncremental(ProjectContext context, CompilerOptions options)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(options);

            Stopwatch stopwatch = Stopwatch.StartNew();
            CompilerOptions normalizedOptions = options.ValidateAndNormalize();
            ProjectContext effectiveContext = CreateEffectiveContext(context, normalizedOptions);
            ImmutableArray<DiscoveredView> views = analyzer.DiscoverViews(effectiveContext);
            ImmutableArray<DiscoveredViewModel> viewModels = analyzer.DiscoverViewModels(effectiveContext);
            UpdateDependencies(views);

            DirtyState dirtyState = BuildDirtyState(effectiveContext, normalizedOptions, views, viewModels, updateFingerprints: true);
            UpdateContentHashes(dirtyState.CurrentContentHashes);
            ImmutableHashSet<string> dirtyViewKeys = dirtyState.DirtyViewFiles
                .Select(static filePath => NormalizePathKey(filePath))
                .ToImmutableHashSet(PathComparer);

            ImmutableArray<CompilationUnit>.Builder units = ImmutableArray.CreateBuilder<CompilationUnit>();
            foreach (DiscoveredView view in views.OrderBy(static discoveredView => discoveredView.FilePath, PathComparer)
                .ThenBy(static discoveredView => discoveredView.ClassName, StringComparer.Ordinal))
            {
                string viewKey = NormalizePathKey(view.FilePath);
                if (dirtyState.ForceFullCompile
                    || dirtyViewKeys.Contains(viewKey)
                    || !cachedUnitsByViewFile.TryGetValue(viewKey, out CachedUnit? cachedUnit))
                {
                    CompilationUnit freshUnit = compiler.CompileFile(view.FilePath, effectiveContext, normalizedOptions);
                    units.Add(freshUnit);
                    CacheCompiledUnit(freshUnit);
                }
                else
                {
                    units.Add(cachedUnit.ToCompilationUnit());
                }
            }

            explicitlyInvalidated.Clear();

            EventBindingsIndex eventBindings = eventBindingsBuilder.Build(units
                .Where(static unit => unit.Schema is not null)
                .Select(static unit => unit.Schema!)
                .ToImmutableArray());

            stopwatch.Stop();
            return CompilationResult.FromUnits(
                units.ToImmutable(),
                effectiveContext.Diagnostics.GetDiagnostics(),
                eventBindings,
                stopwatch.ElapsedMilliseconds);
        }

        /// <inheritdoc />
        public ImmutableArray<string> GetDirtyFiles(ProjectContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            CompilerOptions normalizedOptions = context.Options.ValidateAndNormalize();
            ImmutableArray<DiscoveredView> views = analyzer.DiscoverViews(context);
            ImmutableArray<DiscoveredViewModel> viewModels = analyzer.DiscoverViewModels(context);
            UpdateDependencies(views);

            return BuildDirtyState(context, normalizedOptions, views, viewModels, updateFingerprints: false).DirtyFiles;
        }

        /// <inheritdoc />
        public DependencyGraph GetDependencyGraph()
        {
            return dependencyGraph;
        }

        /// <inheritdoc />
        public void Invalidate(ImmutableArray<string> filePaths)
        {
            if (filePaths.IsDefault)
            {
                return;
            }

            foreach (string key in filePaths
                .Where(static filePath => !string.IsNullOrWhiteSpace(filePath))
                .Select(static filePath => NormalizePathKey(filePath)))
            {
                _ = explicitlyInvalidated.Add(key);
                foreach (string viewModelClassName in dependencyGraph.GetDependenciesOf(key))
                {
                    foreach (string dependent in dependencyGraph.GetDependentsOf(viewModelClassName))
                    {
                        _ = explicitlyInvalidated.Add(NormalizePathKey(dependent));
                    }
                }
            }
        }

        /// <inheritdoc />
        public void ClearCache()
        {
            cachedUnitsByViewFile.Clear();
            explicitlyInvalidated.Clear();
            dependencyGraph.Clear();
            optionsFingerprint = null;
            moduleFingerprint = null;
            sourceMapFingerprint = null;
        }

        /// <inheritdoc />
        public void SaveCache(string cacheDir)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cacheDir);

            Directory.CreateDirectory(cacheDir);
            string cachePath = Path.Join(cacheDir, CacheFileName);
            string json = SerializeCache();
            File.WriteAllText(cachePath, json, Encoding.UTF8);
        }

        /// <inheritdoc />
        public void LoadCache(string cacheDir)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cacheDir);

            string cachePath = Path.Join(cacheDir, CacheFileName);
            if (!File.Exists(cachePath))
            {
                ClearCache();
                return;
            }

            try
            {
                LoadCacheJson(File.ReadAllText(cachePath, Encoding.UTF8));
            }
            catch (JsonException)
            {
                ClearCache();
            }
            catch (ArgumentException)
            {
                ClearCache();
            }
            catch (InvalidOperationException)
            {
                ClearCache();
            }
            catch (FormatException)
            {
                ClearCache();
            }
        }

        private DirtyState BuildDirtyState(
            ProjectContext context,
            CompilerOptions options,
            ImmutableArray<DiscoveredView> views,
            ImmutableArray<DiscoveredViewModel> viewModels,
            bool updateFingerprints)
        {
            string currentOptionsFingerprint = ComputeOptionsFingerprint(options);
            string currentModuleFingerprint = ComputeModuleFingerprint(options);
            string currentSourceMapFingerprint = ComputeSourceMapFingerprint(options);
            bool optionChanged = optionsFingerprint is not null && !StringComparer.Ordinal.Equals(optionsFingerprint, currentOptionsFingerprint);
            bool moduleChanged = moduleFingerprint is not null && !StringComparer.Ordinal.Equals(moduleFingerprint, currentModuleFingerprint);
            bool sourceMapChanged = sourceMapFingerprint is not null && !StringComparer.Ordinal.Equals(sourceMapFingerprint, currentSourceMapFingerprint);
            bool forceFullCompile = cachedUnitsByViewFile.Count == 0 || optionChanged || moduleChanged || sourceMapChanged || !options.Incremental;

            ImmutableDictionary<string, string> currentHashes = ComputeCurrentContentHashes(context);
            ImmutableArray<string>.Builder dirtyFiles = ImmutableArray.CreateBuilder<string>();
            ImmutableArray<string>.Builder dirtyViewFiles = ImmutableArray.CreateBuilder<string>();

            AddDirtyContentFiles(context, currentHashes, dirtyFiles, forceFullCompile);
            AddDirtyViews(views, dirtyFiles, dirtyViewFiles, forceFullCompile);
            AddExplicitlyInvalidatedViewModelDependents(viewModels, dirtyFiles, dirtyViewFiles);

            if (!forceFullCompile)
            {
                AddDependentsForChangedViewModels(context, viewModels, dirtyFiles, dirtyViewFiles);
            }

            AddExplicitlyInvalidatedViews(context, views, dirtyViewFiles);

            if (updateFingerprints)
            {
                optionsFingerprint = currentOptionsFingerprint;
                moduleFingerprint = currentModuleFingerprint;
                sourceMapFingerprint = currentSourceMapFingerprint;
            }

            return new DirtyState(
                NormalizeDirtyFiles(dirtyFiles.ToImmutable()),
                NormalizeDirtyFiles(dirtyViewFiles.ToImmutable()),
                currentHashes,
                forceFullCompile);
        }

        private static ProjectContext CreateEffectiveContext(ProjectContext context, CompilerOptions options)
        {
            if (context.Options == options)
            {
                return context;
            }

            return new ProjectContext(
                context.Compilation,
                context.SourceFiles,
                options,
                context.Diagnostics);
        }

        private void AddExplicitlyInvalidatedViewModelDependents(
            ImmutableArray<DiscoveredViewModel> viewModels,
            ImmutableArray<string>.Builder dirtyFiles,
            ImmutableArray<string>.Builder dirtyViewFiles)
        {
            foreach (DiscoveredViewModel viewModel in viewModels.OrderBy(static viewModel => viewModel.FilePath, PathComparer))
            {
                if (!explicitlyInvalidated.Contains(NormalizePathKey(viewModel.FilePath)))
                {
                    continue;
                }

                foreach (string dependent in dependencyGraph.GetDependentsOf(viewModel.ClassName))
                {
                    AddIfMissing(dirtyFiles, dependent);
                    AddIfMissing(dirtyViewFiles, dependent);
                }
            }
        }

        private void AddDirtyContentFiles(
            ProjectContext context,
            ImmutableDictionary<string, string> currentHashes,
            ImmutableArray<string>.Builder dirtyFiles,
            bool forceFullCompile)
        {
            foreach (string sourceFile in SortPaths(context.SourceFiles))
            {
                string key = NormalizePathKey(sourceFile);
                if (forceFullCompile
                    || explicitlyInvalidated.Contains(key)
                    || !currentHashes.TryGetValue(key, out string? currentHash)
                    || !StringComparer.Ordinal.Equals(dependencyGraph.GetContentHash(key), currentHash))
                {
                    dirtyFiles.Add(sourceFile);
                }
            }
        }

        private static void AddDirtyViews(
            ImmutableArray<DiscoveredView> views,
            ImmutableArray<string>.Builder dirtyFiles,
            ImmutableArray<string>.Builder dirtyViewFiles,
            bool forceFullCompile)
        {
            foreach (DiscoveredView view in views
                .OrderBy(static view => view.FilePath, PathComparer)
                .Where(view => forceFullCompile || dirtyFiles.Any(filePath => PathsEqual(filePath, view.FilePath))))
            {
                dirtyViewFiles.Add(view.FilePath);
            }
        }

        private void AddExplicitlyInvalidatedViews(
            ProjectContext context,
            ImmutableArray<DiscoveredView> views,
            ImmutableArray<string>.Builder dirtyViewFiles)
        {
            ImmutableHashSet<string> allSourceKeys = context.SourceFiles
                .Select(static filePath => NormalizePathKey(filePath))
                .ToImmutableHashSet(PathComparer);

            foreach (string invalidated in explicitlyInvalidated.Order(PathComparer))
            {
                if (!allSourceKeys.Contains(invalidated))
                {
                    continue;
                }

                foreach (DiscoveredView view in views.Where(view => PathsEqual(view.FilePath, invalidated)))
                {
                    AddIfMissing(dirtyViewFiles, view.FilePath);
                }
            }
        }

        private void AddDependentsForChangedViewModels(
            ProjectContext context,
            ImmutableArray<DiscoveredViewModel> viewModels,
            ImmutableArray<string>.Builder dirtyFiles,
            ImmutableArray<string>.Builder dirtyViewFiles)
        {
            foreach (DiscoveredViewModel viewModel in viewModels.OrderBy(static viewModel => viewModel.FilePath, PathComparer))
            {
                if (!dirtyFiles.Any(filePath => PathsEqual(filePath, viewModel.FilePath)))
                {
                    continue;
                }

                ImmutableArray<string> dependents = dependencyGraph.GetDependentsOf(viewModel.ClassName);
                if (dependents.IsDefaultOrEmpty)
                {
                    continue;
                }

                bool schemaChanged = HasSchemaChanged(context, viewModel, dependents);
                if (!schemaChanged)
                {
                    continue;
                }

                foreach (string dependent in dependents)
                {
                    AddIfMissing(dirtyFiles, dependent);
                    AddIfMissing(dirtyViewFiles, dependent);
                }
            }
        }

        private bool HasSchemaChanged(ProjectContext context, DiscoveredViewModel viewModel, ImmutableArray<string> dependents)
        {
            foreach (string dependent in dependents.Order(PathComparer))
            {
                if (!cachedUnitsByViewFile.TryGetValue(NormalizePathKey(dependent), out CachedUnit? cachedUnit))
                {
                    return true;
                }

                DiscoveredView? view = analyzer.DiscoverViews(context)
                    .FirstOrDefault(candidate => PathsEqual(candidate.FilePath, dependent)
                        && StringComparer.Ordinal.Equals(candidate.ViewModelTypeName, viewModel.ClassName));
                if (view is null)
                {
                    return true;
                }

                ViewModelSchema schema = viewModelExtractor.Extract(view, context, CreateIdAllocator());
                string nextSchemaHash = HashText(schemaSerializer.Serialize(schema));
                if (!StringComparer.Ordinal.Equals(nextSchemaHash, cachedUnit.SchemaHash))
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateDependencies(ImmutableArray<DiscoveredView> views)
        {
            dependencyGraph.ClearDependencies();
            foreach (DiscoveredView view in views.OrderBy(static view => view.FilePath, PathComparer)
                .ThenBy(static view => view.ViewModelTypeName, StringComparer.Ordinal))
            {
                dependencyGraph.AddDependency(NormalizePathKey(view.FilePath), view.ViewModelTypeName);
            }
        }

        private void UpdateContentHashes(ImmutableDictionary<string, string> currentHashes)
        {
            foreach (KeyValuePair<string, string> pair in currentHashes.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                dependencyGraph.SetContentHash(pair.Key, pair.Value);
            }
        }

        private void CacheCompiledUnit(CompilationUnit unit)
        {
            string key = NormalizePathKey(unit.SourceFilePath);
            string? contentHash = dependencyGraph.GetContentHash(key);
            if (contentHash is null)
            {
                contentHash = HashText(unit.SourceFilePath);
            }

            string? schemaJson = unit.Schema is null ? null : schemaSerializer.Serialize(unit.Schema);
            string? sourceMapJson = unit.SourceMap is null ? null : sourceMapManager.Serialize(unit.SourceMap);
            string schemaHash = schemaJson is null ? string.Empty : HashText(schemaJson);

            cachedUnitsByViewFile[key] = new CachedUnit(
                unit.SourceFilePath,
                unit.ViewClassName,
                unit.ViewModelClassName,
                unit.QmlText,
                schemaJson,
                sourceMapJson,
                NormalizeDiagnostics(unit.Diagnostics),
                schemaHash,
                contentHash);
        }

        private ImmutableDictionary<string, string> ComputeCurrentContentHashes(ProjectContext context)
        {
            ImmutableDictionary<string, string>.Builder hashes = ImmutableDictionary.CreateBuilder<string, string>(PathComparer);
            foreach (string filePath in SortPaths(context.SourceFiles))
            {
                hashes[NormalizePathKey(filePath)] = ComputeContentHash(context, filePath);
            }

            return hashes.ToImmutable();
        }

        private string ComputeContentHash(ProjectContext context, string filePath)
        {
            SyntaxTree? syntaxTree = context.Compilation.SyntaxTrees.FirstOrDefault(tree => PathsEqual(tree.FilePath, filePath));
            if (syntaxTree is not null)
            {
                return HashText(syntaxTree.GetText().ToString());
            }

            if (File.Exists(filePath))
            {
                return HashBytes(File.ReadAllBytes(filePath));
            }

            return HashText(string.Empty);
        }

        private string SerializeCache()
        {
            using MemoryStream stream = new();
            using (Utf8JsonWriter writer = new(stream, WriterOptions))
            {
                writer.WriteStartObject();
                writer.WriteString("cacheVersion", CacheVersion);
                writer.WriteString("optionsFingerprint", optionsFingerprint);
                writer.WriteString("moduleFingerprint", moduleFingerprint);
                writer.WriteString("sourceMapFingerprint", sourceMapFingerprint);
                WriteFileHashes(writer);
                WriteDependencies(writer);
                WriteUnits(writer);
                writer.WriteEndObject();
            }

            return NormalizeJsonText(Encoding.UTF8.GetString(stream.ToArray()));
        }

        private void WriteFileHashes(Utf8JsonWriter writer)
        {
            writer.WritePropertyName("files");
            writer.WriteStartArray();
            foreach (KeyValuePair<string, string> pair in dependencyGraph.GetContentHashes())
            {
                writer.WriteStartObject();
                writer.WriteString("path", pair.Key);
                writer.WriteString("contentHash", pair.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private void WriteDependencies(Utf8JsonWriter writer)
        {
            writer.WritePropertyName("dependencies");
            writer.WriteStartArray();
            foreach (string viewFile in dependencyGraph.GetViewFiles())
            {
                writer.WriteStartObject();
                writer.WriteString("viewFilePath", viewFile);
                writer.WritePropertyName("viewModelClassNames");
                writer.WriteStartArray();
                foreach (string dependency in dependencyGraph.GetDependenciesOf(viewFile).Order(StringComparer.Ordinal))
                {
                    writer.WriteStringValue(dependency);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private void WriteUnits(Utf8JsonWriter writer)
        {
            writer.WritePropertyName("units");
            writer.WriteStartArray();
            foreach (CachedUnit unit in cachedUnitsByViewFile.Values.OrderBy(static unit => unit.SourceFilePath, PathComparer)
                .ThenBy(static unit => unit.ViewClassName, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("sourceFilePath", unit.SourceFilePath);
                writer.WriteString("viewClassName", unit.ViewClassName);
                writer.WriteString("viewModelClassName", unit.ViewModelClassName);
                writer.WriteString("contentHash", unit.ContentHash);
                writer.WriteString("schemaHash", unit.SchemaHash);
                if (unit.QmlText is not null)
                {
                    writer.WriteString("qmlText", unit.QmlText);
                }

                if (unit.SchemaJson is not null)
                {
                    writer.WriteString("schemaJson", unit.SchemaJson);
                }

                if (unit.SourceMapJson is not null)
                {
                    writer.WriteString("sourceMapJson", unit.SourceMapJson);
                }

                WriteDiagnostics(writer, unit.Diagnostics);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteDiagnostics(Utf8JsonWriter writer, ImmutableArray<CompilerDiagnostic> diagnostics)
        {
            writer.WritePropertyName("diagnostics");
            writer.WriteStartArray();
            foreach (CompilerDiagnostic diagnostic in NormalizeDiagnostics(diagnostics))
            {
                writer.WriteStartObject();
                writer.WriteString("code", diagnostic.Code);
                writer.WriteString("severity", diagnostic.Severity.ToString());
                writer.WriteString("message", diagnostic.Message);
                if (diagnostic.Phase is not null)
                {
                    writer.WriteString("phase", diagnostic.Phase);
                }

                if (diagnostic.Location is not null)
                {
                    writer.WritePropertyName("location");
                    writer.WriteStartObject();
                    if (diagnostic.Location.FilePath is not null)
                    {
                        writer.WriteString("filePath", diagnostic.Location.FilePath);
                    }

                    if (diagnostic.Location.Line is not null)
                    {
                        writer.WriteNumber("line", diagnostic.Location.Line.Value);
                    }

                    if (diagnostic.Location.Column is not null)
                    {
                        writer.WriteNumber("column", diagnostic.Location.Column.Value);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private void LoadCacheJson(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string version = ReadRequiredString(root, "cacheVersion");
            if (!StringComparer.Ordinal.Equals(version, CacheVersion))
            {
                ClearCache();
                return;
            }

            cachedUnitsByViewFile.Clear();
            explicitlyInvalidated.Clear();
            dependencyGraph.Clear();

            optionsFingerprint = ReadOptionalString(root, "optionsFingerprint");
            moduleFingerprint = ReadOptionalString(root, "moduleFingerprint");
            sourceMapFingerprint = ReadOptionalString(root, "sourceMapFingerprint");
            ReadFileHashes(ReadRequiredProperty(root, "files"));
            ReadDependencies(ReadRequiredProperty(root, "dependencies"));
            ReadUnits(ReadRequiredProperty(root, "units"));
        }

        private void ReadFileHashes(JsonElement files)
        {
            foreach (JsonElement file in files.EnumerateArray())
            {
                dependencyGraph.SetContentHash(
                    ReadRequiredString(file, "path"),
                    ReadRequiredString(file, "contentHash"));
            }
        }

        private void ReadDependencies(JsonElement dependencies)
        {
            foreach (JsonElement dependency in dependencies.EnumerateArray())
            {
                string viewFilePath = ReadRequiredString(dependency, "viewFilePath");
                foreach (string? value in ReadRequiredProperty(dependency, "viewModelClassNames")
                    .EnumerateArray()
                    .Select(static viewModelClassName => viewModelClassName.GetString()))
                {
                    if (value is null)
                    {
                        throw new JsonException("Dependency class names must be strings.");
                    }

                    dependencyGraph.AddDependency(viewFilePath, value);
                }
            }
        }

        private void ReadUnits(JsonElement units)
        {
            foreach (JsonElement unit in units.EnumerateArray())
            {
                CachedUnit cachedUnit = new(
                    ReadRequiredString(unit, "sourceFilePath"),
                    ReadRequiredString(unit, "viewClassName"),
                    ReadRequiredString(unit, "viewModelClassName"),
                    ReadOptionalString(unit, "qmlText"),
                    ReadOptionalString(unit, "schemaJson"),
                    ReadOptionalString(unit, "sourceMapJson"),
                    ReadDiagnostics(ReadRequiredProperty(unit, "diagnostics")),
                    ReadRequiredString(unit, "schemaHash"),
                    ReadRequiredString(unit, "contentHash"));

                ValidateCachedUnit(cachedUnit);
                cachedUnitsByViewFile[NormalizePathKey(cachedUnit.SourceFilePath)] = cachedUnit;
            }
        }

        private void ValidateCachedUnit(CachedUnit cachedUnit)
        {
            if (cachedUnit.SchemaJson is not null)
            {
                _ = schemaSerializer.Deserialize(cachedUnit.SchemaJson);
            }

            if (cachedUnit.SourceMapJson is not null)
            {
                _ = sourceMapManager.Deserialize(cachedUnit.SourceMapJson);
            }
        }

        private IIdAllocator CreateIdAllocator()
        {
            IIdAllocator idAllocator = idAllocatorFactory();
            return idAllocator ?? throw new InvalidOperationException("The ID allocator factory returned null.");
        }

        private static void AddIfMissing(ImmutableArray<string>.Builder builder, string filePath)
        {
            if (!builder.Any(existing => PathsEqual(existing, filePath)))
            {
                builder.Add(filePath);
            }
        }

        private static ImmutableArray<string> NormalizeDirtyFiles(ImmutableArray<string> files)
        {
            return files
                .Where(static filePath => !string.IsNullOrWhiteSpace(filePath))
                .Distinct(PathComparer)
                .Order(PathComparer)
                .ToImmutableArray();
        }

        private static ImmutableArray<CompilerDiagnostic> NormalizeDiagnostics(ImmutableArray<CompilerDiagnostic> diagnostics)
        {
            return (diagnostics.IsDefault ? ImmutableArray<CompilerDiagnostic>.Empty : diagnostics)
                .OrderBy(static diagnostic => diagnostic.Location?.FilePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Location?.Line ?? 0)
                .ThenBy(static diagnostic => diagnostic.Location?.Column ?? 0)
                .ThenBy(static diagnostic => diagnostic.Severity)
                .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Phase ?? string.Empty, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static IEnumerable<string> SortPaths(ImmutableArray<string> paths)
        {
            return (paths.IsDefault ? ImmutableArray<string>.Empty : paths)
                .Order(PathComparer);
        }

        private static string ComputeOptionsFingerprint(CompilerOptions options)
        {
            return HashText(string.Join(
                "\n",
                "schema=1",
                $"incremental={options.Incremental.ToString(CultureInfo.InvariantCulture)}",
                $"generateSourceMaps={options.GenerateSourceMaps.ToString(CultureInfo.InvariantCulture)}",
                $"sourceMapDir={options.SourceMapDir}",
                $"formatQml={options.FormatQml.ToString(CultureInfo.InvariantCulture)}",
                $"lintQml={options.LintQml.ToString(CultureInfo.InvariantCulture)}",
                $"moduleUri={options.ModuleUriPrefix}",
                $"moduleVersion={options.ModuleVersion.Major}.{options.ModuleVersion.Minor}",
                $"maxSeverity={(int)options.MaxAllowedSeverity}",
                $"include={JoinPatterns(options.IncludePatterns)}",
                $"exclude={JoinPatterns(options.ExcludePatterns)}",
                $"analyzers={JoinPatterns(options.AdditionalAnalyzers)}"));
        }

        private static string ComputeModuleFingerprint(CompilerOptions options)
        {
            return HashText($"{options.ModuleUriPrefix}\n{options.ModuleVersion.Major}.{options.ModuleVersion.Minor}");
        }

        private static string ComputeSourceMapFingerprint(CompilerOptions options)
        {
            return HashText($"{options.GenerateSourceMaps}\n{options.SourceMapDir}");
        }

        private static string JoinPatterns(ImmutableArray<string> patterns)
        {
            return string.Join("\u001F", (patterns.IsDefault ? ImmutableArray<string>.Empty : patterns).Order(StringComparer.Ordinal));
        }

        private static string HashText(string text)
        {
            return HashBytes(Encoding.UTF8.GetBytes(text));
        }

        private static string HashBytes(byte[] bytes)
        {
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static bool PathsEqual(string left, string right)
        {
            return PathComparer.Equals(NormalizePathKey(left), NormalizePathKey(right));
        }

        private static string NormalizePathKey(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string ReadRequiredString(JsonElement element, string propertyName)
        {
            string? value = ReadRequiredProperty(element, propertyName).GetString();
            if (value is null)
            {
                throw new JsonException($"Property '{propertyName}' must be a string.");
            }

            return value;
        }

        private static string? ReadOptionalString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement value) ? value.GetString() : null;
        }

        private static JsonElement ReadRequiredProperty(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value))
            {
                throw new JsonException($"Property '{propertyName}' is required.");
            }

            return value;
        }

        private static ImmutableArray<CompilerDiagnostic> ReadDiagnostics(JsonElement diagnostics)
        {
            ImmutableArray<CompilerDiagnostic>.Builder builder = ImmutableArray.CreateBuilder<CompilerDiagnostic>();
            foreach (JsonElement diagnostic in diagnostics.EnumerateArray())
            {
                builder.Add(new CompilerDiagnostic(
                    ReadRequiredString(diagnostic, "code"),
                    ReadSeverity(diagnostic),
                    ReadRequiredString(diagnostic, "message"),
                    ReadLocation(diagnostic),
                    ReadOptionalString(diagnostic, "phase")));
            }

            return NormalizeDiagnostics(builder.ToImmutable());
        }

        private static DiagnosticSeverity ReadSeverity(JsonElement diagnostic)
        {
            string severity = ReadRequiredString(diagnostic, "severity");
            if (!Enum.TryParse(severity, ignoreCase: false, out DiagnosticSeverity value))
            {
                throw new JsonException($"Diagnostic severity '{severity}' is invalid.");
            }

            return value;
        }

        private static SourceLocation? ReadLocation(JsonElement diagnostic)
        {
            if (!diagnostic.TryGetProperty("location", out JsonElement location))
            {
                return null;
            }

            string? filePath = ReadOptionalString(location, "filePath");
            int? line = ReadOptionalInt32(location, "line");
            int? column = ReadOptionalInt32(location, "column");
            return SourceLocation.Partial(filePath, line, column);
        }

        private static int? ReadOptionalInt32(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value))
            {
                return null;
            }

            return value.GetInt32();
        }

        private static string NormalizeJsonText(string json)
        {
            string normalized = json.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            return normalized.EndsWith("\n", StringComparison.Ordinal) ? normalized : normalized + "\n";
        }

        private sealed record DirtyState(
            ImmutableArray<string> DirtyFiles,
            ImmutableArray<string> DirtyViewFiles,
            ImmutableDictionary<string, string> CurrentContentHashes,
            bool ForceFullCompile);

        private sealed record CachedUnit(
            string SourceFilePath,
            string ViewClassName,
            string ViewModelClassName,
            string? QmlText,
            string? SchemaJson,
            string? SourceMapJson,
            ImmutableArray<CompilerDiagnostic> Diagnostics,
            string SchemaHash,
            string ContentHash)
        {
            public CompilationUnit ToCompilationUnit()
            {
                ViewModelSchema? schema = SchemaJson is null ? null : new ViewModelSchemaSerializer().Deserialize(SchemaJson);
                SourceMap? sourceMap = SourceMapJson is null ? null : new SourceMapManager().Deserialize(SourceMapJson);
                return new CompilationUnit
                {
                    SourceFilePath = SourceFilePath,
                    ViewClassName = ViewClassName,
                    ViewModelClassName = ViewModelClassName,
                    QmlText = QmlText,
                    Schema = schema,
                    SourceMap = sourceMap,
                    Diagnostics = Diagnostics,
                    Stats = new CompilationUnitStats
                    {
                        QmlBytes = QmlText is null ? 0 : Encoding.UTF8.GetByteCount(QmlText),
                    },
                };
            }
        }
    }
}
