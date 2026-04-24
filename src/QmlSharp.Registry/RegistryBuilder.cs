using System.Diagnostics.CodeAnalysis;
using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Normalization;
using QmlSharp.Registry.Parsing;
using QmlSharp.Registry.Querying;
using QmlSharp.Registry.Scanning;
using QmlSharp.Registry.Snapshots;

namespace QmlSharp.Registry
{
    /// <summary>
    /// Default orchestrator for the registry build pipeline.
    /// </summary>
    public sealed class RegistryBuilder : IRegistryBuilder
    {
        private readonly IQtTypeScanner qtTypeScanner;
        private readonly IQmltypesParser qmltypesParser;
        private readonly IQmldirParser qmldirParser;
        private readonly IMetatypesParser metatypesParser;
        private readonly ITypeNameMapper typeNameMapper;
        private readonly ITypeNormalizer typeNormalizer;
        private readonly IRegistrySnapshot registrySnapshot;

        public RegistryBuilder()
            : this(
                new QtTypeScanner(),
                new QmltypesParser(),
                new QmldirParser(),
                new MetatypesParser(),
                new TypeNameMapper(),
                new TypeNormalizer(),
                new RegistrySnapshot())
        {
        }

        public RegistryBuilder(
            IQtTypeScanner qtTypeScanner,
            IQmltypesParser qmltypesParser,
            IQmldirParser qmldirParser,
            IMetatypesParser metatypesParser,
            ITypeNameMapper typeNameMapper,
            ITypeNormalizer typeNormalizer,
            IRegistrySnapshot registrySnapshot)
        {
            this.qtTypeScanner = qtTypeScanner ?? throw new ArgumentNullException(nameof(qtTypeScanner));
            this.qmltypesParser = qmltypesParser ?? throw new ArgumentNullException(nameof(qmltypesParser));
            this.qmldirParser = qmldirParser ?? throw new ArgumentNullException(nameof(qmldirParser));
            this.metatypesParser = metatypesParser ?? throw new ArgumentNullException(nameof(metatypesParser));
            this.typeNameMapper = typeNameMapper ?? throw new ArgumentNullException(nameof(typeNameMapper));
            this.typeNormalizer = typeNormalizer ?? throw new ArgumentNullException(nameof(typeNormalizer));
            this.registrySnapshot = registrySnapshot ?? throw new ArgumentNullException(nameof(registrySnapshot));
        }

        public BuildResult Build(BuildConfig config, Action<BuildProgress>? progress = null)
        {
            ArgumentNullException.ThrowIfNull(config);

            int totalSteps = GetBuildStepCount(config);
            return BuildCore(config, progress, stepOffset: 0, totalSteps, ImmutableArray<RegistryDiagnostic>.Empty);
        }

        public BuildResult LoadFromSnapshot(string snapshotPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(snapshotPath);

            return TryLoadSnapshot(snapshotPath, out QmlRegistry? registry, out RegistryDiagnostic? diagnostic)
                ? CreateSuccessResult(registry!, ImmutableArray<RegistryDiagnostic>.Empty, progress: null, currentStep: 0, totalSteps: 0, "Loaded registry snapshot.")
                : CreateFailureResult(
                    diagnostic is null
                        ? ImmutableArray<RegistryDiagnostic>.Empty
                        : [diagnostic],
                    progress: null,
                    currentStep: 0,
                    totalSteps: 0,
                    "Failed to load registry snapshot.");
        }

        public BuildResult BuildOrLoad(BuildConfig config, Action<BuildProgress>? progress = null)
        {
            ArgumentNullException.ThrowIfNull(config);

            string? snapshotPath = NormalizeOptionalPath(config.SnapshotPath);
            if (!CanAttemptSnapshotLoad(config, snapshotPath))
            {
                int totalSteps = GetBuildStepCount(config);
                return BuildCore(config, progress, stepOffset: 0, totalSteps, ImmutableArray<RegistryDiagnostic>.Empty);
            }

            if (!File.Exists(snapshotPath))
            {
                int totalSteps = GetBuildStepCount(config);
                return BuildCore(config, progress, stepOffset: 0, totalSteps, ImmutableArray<RegistryDiagnostic>.Empty);
            }

            int rebuildTotalSteps = 1 + GetBuildStepCount(config);
            ReportProgress(progress, BuildPhase.LoadingSnapshot, 1, rebuildTotalSteps, snapshotPath);

            if (TryLoadSnapshot(snapshotPath, out QmlRegistry? registry, out RegistryDiagnostic? diagnostic))
            {
                return CreateSuccessResult(registry!, ImmutableArray<RegistryDiagnostic>.Empty, progress, rebuildTotalSteps, rebuildTotalSteps, "Loaded registry snapshot.");
            }

            SnapshotValidity validity = registrySnapshot.CheckValidity(snapshotPath);
            if (!validity.IsValid)
            {
                return BuildCore(
                    config,
                    progress,
                    stepOffset: 1,
                    rebuildTotalSteps,
                    [CreateSnapshotDiagnostic(validity, DiagnosticSeverity.Warning, snapshotPath)]);
            }

            ImmutableArray<RegistryDiagnostic> fallbackDiagnostics = diagnostic is null
                ? ImmutableArray<RegistryDiagnostic>.Empty
                : [PromoteToWarning(diagnostic)];
            return BuildCore(config, progress, stepOffset: 1, rebuildTotalSteps, fallbackDiagnostics);
        }

        [SuppressMessage("Maintainability", "MA0051:Method is too long", Justification = "The build orchestrator intentionally keeps the pipeline phases and progress reporting in one deterministic method.")]
        private BuildResult BuildCore(
            BuildConfig config,
            Action<BuildProgress>? progress,
            int stepOffset,
            int totalSteps,
            ImmutableArray<RegistryDiagnostic> additionalDiagnostics)
        {
            List<RegistryDiagnostic> diagnostics = additionalDiagnostics.IsDefaultOrEmpty
                ? []
                : [.. additionalDiagnostics];

            ReportProgress(progress, BuildPhase.Scanning, stepOffset + 1, totalSteps, config.QtDir);
            ScanResult scanResult = qtTypeScanner.Scan(new ScannerConfig(config.QtDir, config.ModuleFilter, config.IncludeInternal));
            diagnostics.AddRange(scanResult.Diagnostics);
            if (RegistryDiagnosticCollection.HasErrors(scanResult.Diagnostics))
            {
                return CreateFailureResult(diagnostics.ToImmutableArray(), progress, totalSteps, totalSteps, "Registry build failed during scanning.");
            }

            string qmlRootDirectory = Path.Join(Path.GetFullPath(config.QtDir), "qml");

            ReportProgress(progress, BuildPhase.ParsingQmltypes, stepOffset + 2, totalSteps, BuildFileCountDetail(scanResult.QmltypesPaths.Length, ".qmltypes"));
            ImmutableArray<RawQmltypesFile> qmltypesFiles = scanResult.QmltypesPaths
                .Select(ParseQmltypesFile)
                .Select(result => result.Value!)
                .ToImmutableArray();

            ReportProgress(progress, BuildPhase.ParsingQmldir, stepOffset + 3, totalSteps, BuildFileCountDetail(scanResult.QmldirPaths.Length, "qmldir"));
            ImmutableArray<(string ModuleUri, RawQmldirFile File)> qmldirFiles = scanResult.QmldirPaths
                .Select(path =>
                {
                    ParseResult<RawQmldirFile> result = ParseQmldirFile(path);
                    RawQmldirFile file = result.Value!;
                    string moduleUri = !string.IsNullOrWhiteSpace(file.Module)
                        ? file.Module!
                        : qtTypeScanner.InferModuleUri(path, qmlRootDirectory) ?? string.Empty;
                    return (moduleUri, file);
                })
                .ToImmutableArray();

            ReportProgress(progress, BuildPhase.ParsingMetatypes, stepOffset + 4, totalSteps, BuildFileCountDetail(scanResult.MetatypesPaths.Length, "metatypes"));
            ImmutableArray<RawMetatypesFile> metatypesFiles = scanResult.MetatypesPaths
                .Select(ParseMetatypesFile)
                .Select(result => result.Value!)
                .ToImmutableArray();

            ReportProgress(progress, BuildPhase.Normalizing, stepOffset + 5, totalSteps, "Merging registry sources.");
            NormalizeResult normalizeResult = typeNormalizer.Normalize(qmltypesFiles, qmldirFiles, metatypesFiles, typeNameMapper);
            diagnostics.AddRange(normalizeResult.Diagnostics);

            if (normalizeResult.Registry is null)
            {
                return CreateFailureResult(diagnostics.ToImmutableArray(), progress, totalSteps, totalSteps, "Registry normalization did not produce a registry.");
            }

            QmlRegistry registry = normalizeResult.Registry;
            if (!string.IsNullOrWhiteSpace(config.SnapshotPath))
            {
                ReportProgress(progress, BuildPhase.SavingSnapshot, stepOffset + 6, totalSteps, config.SnapshotPath);
                try
                {
                    registrySnapshot.SaveToFile(registry, config.SnapshotPath!);
                }
                catch (IOException exception)
                {
                    diagnostics.Add(CreateSnapshotSaveFailureDiagnostic(config.SnapshotPath, exception));
                }
                catch (UnauthorizedAccessException exception)
                {
                    diagnostics.Add(CreateSnapshotSaveFailureDiagnostic(config.SnapshotPath, exception));
                }
                catch (NotSupportedException exception)
                {
                    diagnostics.Add(CreateSnapshotSaveFailureDiagnostic(config.SnapshotPath, exception));
                }
                catch (ArgumentException exception)
                {
                    diagnostics.Add(CreateSnapshotSaveFailureDiagnostic(config.SnapshotPath, exception));
                }
                catch (System.Security.SecurityException exception)
                {
                    diagnostics.Add(CreateSnapshotSaveFailureDiagnostic(config.SnapshotPath, exception));
                }
            }

            ImmutableArray<RegistryDiagnostic> finalDiagnostics = diagnostics.ToImmutableArray();
            return CreateSuccessResult(
                registry,
                finalDiagnostics,
                progress,
                totalSteps,
                totalSteps,
                RegistryDiagnosticCollection.HasErrors(finalDiagnostics)
                    ? "Registry build completed with errors."
                    : "Registry build completed.");
        }

        private ParseResult<RawQmltypesFile> ParseQmltypesFile(string filePath)
        {
            try
            {
                ParseResult<RawQmltypesFile> result = qmltypesParser.Parse(filePath);
                if (result.Value is not null)
                {
                    return result;
                }

                RegistryDiagnostic diagnostic = CreateStageFailureDiagnostic(
                    DiagnosticCodes.QmltypesSyntaxError,
                    "The qmltypes parser returned no file content.",
                    filePath);
                return new ParseResult<RawQmltypesFile>(CreateEmptyQmltypesFile(filePath, [diagnostic]), [diagnostic]);
            }
            catch (IOException exception)
            {
                return CreateQmltypesParseFailure(filePath, exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                return CreateQmltypesParseFailure(filePath, exception);
            }
            catch (NotSupportedException exception)
            {
                return CreateQmltypesParseFailure(filePath, exception);
            }
            catch (ArgumentException exception)
            {
                return CreateQmltypesParseFailure(filePath, exception);
            }
            catch (FormatException exception)
            {
                return CreateQmltypesParseFailure(filePath, exception);
            }
            catch (InvalidOperationException exception)
            {
                return CreateQmltypesParseFailure(filePath, exception);
            }
        }

        private ParseResult<RawQmldirFile> ParseQmldirFile(string filePath)
        {
            try
            {
                ParseResult<RawQmldirFile> result = qmldirParser.Parse(filePath);
                if (result.Value is not null)
                {
                    return result;
                }

                RegistryDiagnostic diagnostic = CreateStageFailureDiagnostic(
                    DiagnosticCodes.QmldirSyntaxError,
                    "The qmldir parser returned no file content.",
                    filePath);
                return new ParseResult<RawQmldirFile>(CreateEmptyQmldirFile(filePath, [diagnostic]), [diagnostic]);
            }
            catch (IOException exception)
            {
                return CreateQmldirParseFailure(filePath, exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                return CreateQmldirParseFailure(filePath, exception);
            }
            catch (NotSupportedException exception)
            {
                return CreateQmldirParseFailure(filePath, exception);
            }
            catch (ArgumentException exception)
            {
                return CreateQmldirParseFailure(filePath, exception);
            }
            catch (FormatException exception)
            {
                return CreateQmldirParseFailure(filePath, exception);
            }
            catch (InvalidOperationException exception)
            {
                return CreateQmldirParseFailure(filePath, exception);
            }
        }

        private ParseResult<RawMetatypesFile> ParseMetatypesFile(string filePath)
        {
            try
            {
                ParseResult<RawMetatypesFile> result = metatypesParser.Parse(filePath);
                if (result.Value is not null)
                {
                    return result;
                }

                RegistryDiagnostic diagnostic = CreateStageFailureDiagnostic(
                    DiagnosticCodes.MetatypesJsonError,
                    "The metatypes parser returned no file content.",
                    filePath);
                return new ParseResult<RawMetatypesFile>(CreateEmptyMetatypesFile(filePath, [diagnostic]), [diagnostic]);
            }
            catch (IOException exception)
            {
                return CreateMetatypesParseFailure(filePath, exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                return CreateMetatypesParseFailure(filePath, exception);
            }
            catch (NotSupportedException exception)
            {
                return CreateMetatypesParseFailure(filePath, exception);
            }
            catch (ArgumentException exception)
            {
                return CreateMetatypesParseFailure(filePath, exception);
            }
            catch (System.Text.Json.JsonException exception)
            {
                return CreateMetatypesParseFailure(filePath, exception);
            }
            catch (FormatException exception)
            {
                return CreateMetatypesParseFailure(filePath, exception);
            }
            catch (InvalidOperationException exception)
            {
                return CreateMetatypesParseFailure(filePath, exception);
            }
        }

        private bool TryLoadSnapshot(string snapshotPath, out QmlRegistry? registry, out RegistryDiagnostic? diagnostic)
        {
            registry = null;
            diagnostic = null;

            try
            {
                registry = registrySnapshot.LoadFromFile(snapshotPath);
                return true;
            }
            catch (InvalidDataException exception)
            {
                diagnostic = CreateSnapshotLoadFailureDiagnostic(snapshotPath, exception);
                return false;
            }
            catch (IOException exception)
            {
                diagnostic = CreateSnapshotLoadFailureDiagnostic(snapshotPath, exception);
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                diagnostic = CreateSnapshotLoadFailureDiagnostic(snapshotPath, exception);
                return false;
            }
            catch (NotSupportedException exception)
            {
                diagnostic = CreateSnapshotLoadFailureDiagnostic(snapshotPath, exception);
                return false;
            }
            catch (ArgumentException exception)
            {
                diagnostic = CreateSnapshotLoadFailureDiagnostic(snapshotPath, exception);
                return false;
            }
        }

        private static BuildResult CreateSuccessResult(
            QmlRegistry registry,
            ImmutableArray<RegistryDiagnostic> diagnostics,
            Action<BuildProgress>? progress,
            int currentStep,
            int totalSteps,
            string detail)
        {
            TypeRegistryAdapter typeRegistry = new(registry);
            RegistryQuery query = new(registry);

            ReportProgress(progress, BuildPhase.Complete, currentStep, totalSteps, detail);
            return new BuildResult(typeRegistry, query, diagnostics);
        }

        private static BuildResult CreateFailureResult(
            ImmutableArray<RegistryDiagnostic> diagnostics,
            Action<BuildProgress>? progress,
            int currentStep,
            int totalSteps,
            string detail)
        {
            ReportProgress(progress, BuildPhase.Complete, currentStep, totalSteps, detail);
            return new BuildResult(TypeRegistry: null, Query: null, diagnostics);
        }

        private static RegistryDiagnostic CreateStageFailureDiagnostic(string code, string message, string? filePath)
        {
            return new RegistryDiagnostic(DiagnosticSeverity.Error, code, message, filePath, null, null);
        }

        private static RegistryDiagnostic CreateExceptionDiagnostic(
            DiagnosticSeverity severity,
            string code,
            string message,
            string? filePath,
            Exception exception)
        {
            return new RegistryDiagnostic(
                severity,
                code,
                $"{message} {exception.Message}",
                filePath,
                null,
                null);
        }

        private static RegistryDiagnostic CreateSnapshotSaveFailureDiagnostic(string? snapshotPath, Exception exception)
        {
            return CreateExceptionDiagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCodes.SnapshotCorrupt,
                $"Failed to save registry snapshot to '{snapshotPath}'.",
                snapshotPath,
                exception);
        }

        private static RegistryDiagnostic CreateSnapshotLoadFailureDiagnostic(string snapshotPath, Exception exception)
        {
            string code = exception is NotSupportedException
                && exception.Message.Contains(DiagnosticCodes.SnapshotVersionMismatch, StringComparison.Ordinal)
                    ? DiagnosticCodes.SnapshotVersionMismatch
                    : DiagnosticCodes.SnapshotCorrupt;

            return CreateExceptionDiagnostic(
                DiagnosticSeverity.Error,
                code,
                $"Failed to load registry snapshot from '{snapshotPath}'.",
                snapshotPath,
                exception);
        }

        private static ParseResult<RawQmltypesFile> CreateQmltypesParseFailure(string filePath, Exception exception)
        {
            RegistryDiagnostic diagnostic = CreateExceptionDiagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCodes.QmltypesSyntaxError,
                $"Failed to parse qmltypes file '{filePath}'.",
                filePath,
                exception);
            return new ParseResult<RawQmltypesFile>(CreateEmptyQmltypesFile(filePath, [diagnostic]), [diagnostic]);
        }

        private static ParseResult<RawQmldirFile> CreateQmldirParseFailure(string filePath, Exception exception)
        {
            RegistryDiagnostic diagnostic = CreateExceptionDiagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCodes.QmldirSyntaxError,
                $"Failed to parse qmldir file '{filePath}'.",
                filePath,
                exception);
            return new ParseResult<RawQmldirFile>(CreateEmptyQmldirFile(filePath, [diagnostic]), [diagnostic]);
        }

        private static ParseResult<RawMetatypesFile> CreateMetatypesParseFailure(string filePath, Exception exception)
        {
            RegistryDiagnostic diagnostic = CreateExceptionDiagnostic(
                DiagnosticSeverity.Error,
                DiagnosticCodes.MetatypesJsonError,
                $"Failed to parse metatypes file '{filePath}'.",
                filePath,
                exception);
            return new ParseResult<RawMetatypesFile>(CreateEmptyMetatypesFile(filePath, [diagnostic]), [diagnostic]);
        }

        private static RegistryDiagnostic CreateSnapshotDiagnostic(
            SnapshotValidity validity,
            DiagnosticSeverity severity,
            string snapshotPath)
        {
            string code = validity.ErrorMessage?.Contains(DiagnosticCodes.SnapshotVersionMismatch, StringComparison.Ordinal) == true
                ? DiagnosticCodes.SnapshotVersionMismatch
                : DiagnosticCodes.SnapshotCorrupt;
            string message = StripDiagnosticCodePrefix(validity.ErrorMessage, code) ?? "Snapshot is invalid.";

            return new RegistryDiagnostic(severity, code, message, snapshotPath, null, null);
        }

        private static RegistryDiagnostic PromoteToWarning(RegistryDiagnostic diagnostic)
        {
            return diagnostic with { Severity = DiagnosticSeverity.Warning };
        }

        private static string? StripDiagnosticCodePrefix(string? message, string code)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return message;
            }

            string prefix = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{code}: ");
            return message.StartsWith(prefix, StringComparison.Ordinal)
                ? message[prefix.Length..]
                : message;
        }

        private static string BuildFileCountDetail(int fileCount, string label)
        {
            string suffix = fileCount == 1 ? string.Empty : "s";
            return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{fileCount} {label} file{suffix}");
        }

        private static int GetBuildStepCount(BuildConfig config)
        {
            return string.IsNullOrWhiteSpace(config.SnapshotPath) ? 6 : 7;
        }

        private static string? NormalizeOptionalPath(string? path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }

        private static bool CanAttemptSnapshotLoad(BuildConfig config, string? snapshotPath)
        {
            if (config.ForceRebuild || snapshotPath is null)
            {
                return false;
            }

            // Filtered builds require a fresh scan because snapshot metadata does not yet
            // encode per-build module filters.
            return (!config.ModuleFilter.HasValue || config.ModuleFilter.Value.IsDefaultOrEmpty)
                && !config.IncludeInternal;
        }

        private static RawQmltypesFile CreateEmptyQmltypesFile(string sourcePath, ImmutableArray<RegistryDiagnostic> diagnostics)
        {
            return new RawQmltypesFile(sourcePath, ImmutableArray<RawQmltypesComponent>.Empty, diagnostics);
        }

        private static RawQmldirFile CreateEmptyQmldirFile(string sourcePath, ImmutableArray<RegistryDiagnostic> diagnostics)
        {
            return new RawQmldirFile(
                SourcePath: sourcePath,
                Module: null,
                Plugins: ImmutableArray<RawQmldirPlugin>.Empty,
                Classname: null,
                Imports: ImmutableArray<RawQmldirImport>.Empty,
                Depends: ImmutableArray<RawQmldirImport>.Empty,
                TypeEntries: ImmutableArray<RawQmldirTypeEntry>.Empty,
                Designersupported: ImmutableArray<string>.Empty,
                Typeinfo: null,
                Diagnostics: diagnostics);
        }

        private static RawMetatypesFile CreateEmptyMetatypesFile(string sourcePath, ImmutableArray<RegistryDiagnostic> diagnostics)
        {
            return new RawMetatypesFile(sourcePath, ImmutableArray<RawMetatypesEntry>.Empty, diagnostics);
        }

        private static void ReportProgress(
            Action<BuildProgress>? progress,
            BuildPhase phase,
            int currentStep,
            int totalSteps,
            string? detail)
        {
            progress?.Invoke(new BuildProgress(phase, currentStep, totalSteps, detail));
        }

        private sealed class TypeRegistryAdapter : ITypeRegistry
        {
            private readonly QmlRegistry registry;

            public TypeRegistryAdapter(QmlRegistry registry)
            {
                this.registry = registry;
            }

            public int FormatVersion => registry.FormatVersion;

            public IReadOnlyList<QmlModule> Modules => registry.Modules;

            public QmlRegistry Registry => registry;

            public string QtVersion => registry.QtVersion;

            public IReadOnlyList<QmlType> Types => registry.GetLookupIndexes().AllTypes;
        }
    }
}
