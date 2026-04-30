namespace QmlSharp.Qt.Tools
{
    /// <summary>Default cumulative Qt quality gate implementation.</summary>
    public sealed class QualityGate : IQualityGate
    {
        private const string StringInputFileName = "<string>";

        private readonly IQmlFormat _qmlFormat;
        private readonly IQmlLint _qmlLint;
        private readonly IQmlCachegen _qmlCachegen;
        private readonly IQmlRunner _qmlRunner;

        /// <summary>Create a quality gate backed by the default Qt wrappers.</summary>
        public QualityGate()
        {
            QtToolchain toolchain = new();
            ToolRunner toolRunner = new();
            QtDiagnosticParser diagnosticParser = new();

            _qmlFormat = new QmlFormat(toolchain, toolRunner, diagnosticParser);
            _qmlLint = new QmlLint(toolchain, toolRunner, diagnosticParser);
            _qmlCachegen = new QmlCachegen(toolchain, toolRunner, diagnosticParser);
            _qmlRunner = new QmlRunner(toolchain, toolRunner, diagnosticParser);
        }

        /// <summary>Create a quality gate using explicit tool wrappers.</summary>
        public QualityGate(
            IQmlFormat qmlFormat,
            IQmlLint qmlLint,
            IQmlCachegen qmlCachegen,
            IQmlRunner qmlRunner)
        {
            _qmlFormat = qmlFormat ?? throw new ArgumentNullException(nameof(qmlFormat));
            _qmlLint = qmlLint ?? throw new ArgumentNullException(nameof(qmlLint));
            _qmlCachegen = qmlCachegen ?? throw new ArgumentNullException(nameof(qmlCachegen));
            _qmlRunner = qmlRunner ?? throw new ArgumentNullException(nameof(qmlRunner));
        }

        /// <inheritdoc />
        public async Task<QualityGateResult> RunAsync(
            string filePath,
            QualityGateLevel level,
            QualityGateOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ValidateLevel(level);

            string normalizedFilePath = Path.GetFullPath(filePath);
            QualityGateOptions effectiveOptions = options ?? new QualityGateOptions();
            ImmutableArray<QualityGateLevel> levels = GetLevels(level);
            ImmutableArray<QtDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<QtDiagnostic>();
            ImmutableDictionary<QualityGateLevel, ImmutableArray<QtDiagnostic>>.Builder levelDiagnostics =
                ImmutableDictionary.CreateBuilder<QualityGateLevel, ImmutableArray<QtDiagnostic>>();
            ImmutableDictionary<QualityGateLevel, long>.Builder levelDurations =
                ImmutableDictionary.CreateBuilder<QualityGateLevel, long>();

            bool passed = true;
            QualityGateLevel? failedAtLevel = null;
            QualityGateLevel completedLevel = levels[0];

            foreach (QualityGateLevel currentLevel in levels)
            {
                ct.ThrowIfCancellationRequested();

                StageResult stage = await RunLevelAsync(normalizedFilePath, currentLevel, effectiveOptions, ct)
                    .ConfigureAwait(false);
                completedLevel = currentLevel;
                levelDurations[currentLevel] = stage.DurationMs;
                levelDiagnostics[currentLevel] = stage.Diagnostics;
                diagnostics.AddRange(stage.Diagnostics);
                effectiveOptions.OnProgress?.Invoke(new QualityGateProgress
                {
                    Level = currentLevel,
                    Passed = stage.Passed,
                    DurationMs = stage.DurationMs,
                    DiagnosticCount = stage.Diagnostics.Length,
                });

                if (!stage.Passed)
                {
                    passed = false;
                    failedAtLevel ??= currentLevel;
                    if (effectiveOptions.EarlyStop)
                    {
                        break;
                    }
                }
            }

            ImmutableDictionary<QualityGateLevel, long> immutableDurations = levelDurations.ToImmutable();
            return new QualityGateResult
            {
                Passed = passed,
                CompletedLevel = completedLevel,
                FailedAtLevel = failedAtLevel,
                Diagnostics = diagnostics.ToImmutable(),
                LevelDiagnostics = levelDiagnostics.ToImmutable(),
                LevelDurationMs = immutableDurations,
                TotalDurationMs = SumDurations(immutableDurations),
            };
        }

        /// <inheritdoc />
        public async Task<QualityGateResult> RunStringAsync(
            string qmlSource,
            QualityGateLevel level,
            QualityGateOptions? options = null,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(qmlSource);
            ValidateLevel(level);

            string tempFile = Path.Join(
                Path.GetTempPath(),
                "qmlsharp-qualitygate-" + Guid.NewGuid().ToString("N") + ".qml");

            try
            {
                await File.WriteAllTextAsync(tempFile, qmlSource, ct).ConfigureAwait(false);
                QualityGateResult result = await RunAsync(tempFile, level, options, ct).ConfigureAwait(false);
                return RewriteInputFile(result, tempFile, StringInputFileName);
            }
            finally
            {
                TryDeleteFile(tempFile);
            }
        }

        /// <inheritdoc />
        public async Task<QualityGateResult> RunBatchAsync(
            ImmutableArray<string> filePaths,
            QualityGateLevel level,
            QualityGateOptions? options = null,
            CancellationToken ct = default)
        {
            ValidateLevel(level);
            if (filePaths.IsDefaultOrEmpty)
            {
                return new QualityGateResult
                {
                    Passed = true,
                    CompletedLevel = level,
                    TotalDurationMs = 0,
                };
            }

            QualityGateOptions effectiveOptions = options ?? new QualityGateOptions();
            BatchAccumulator accumulator = new(level, filePaths.Length);

            foreach (string filePath in filePaths)
            {
                string normalizedFilePath = NormalizeBatchFilePath(filePath, nameof(filePaths));
                QualityGateResult fileResult = await RunAsync(normalizedFilePath, level, effectiveOptions, ct)
                    .ConfigureAwait(false);
                accumulator.Add(normalizedFilePath, fileResult);
            }

            return accumulator.ToResult();
        }

        private async Task<StageResult> RunLevelAsync(
            string filePath,
            QualityGateLevel level,
            QualityGateOptions options,
            CancellationToken ct)
        {
            return level switch
            {
                QualityGateLevel.Syntax => await RunSyntaxAsync(filePath, ct).ConfigureAwait(false),
                QualityGateLevel.Lint => await RunLintAsync(filePath, options, ct).ConfigureAwait(false),
                QualityGateLevel.Compile => await RunCompileAsync(filePath, options, ct).ConfigureAwait(false),
                QualityGateLevel.Full => await RunFullAsync(filePath, ct).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown quality gate level."),
            };
        }

        private async Task<StageResult> RunSyntaxAsync(string filePath, CancellationToken ct)
        {
            QmlFormatResult result = await _qmlFormat.FormatFileAsync(filePath, ct: ct).ConfigureAwait(false);
            return new StageResult(result.Success, result.Diagnostics, NormalizeDuration(result.ToolResult.DurationMs));
        }

        private async Task<StageResult> RunLintAsync(
            string filePath,
            QualityGateOptions options,
            CancellationToken ct)
        {
            QmlLintResult result = await _qmlLint
                .LintFileAsync(filePath, new QmlLintOptions { ImportPaths = options.ImportPaths }, ct)
                .ConfigureAwait(false);
            return new StageResult(result.Success, result.Diagnostics, NormalizeDuration(result.ToolResult.DurationMs));
        }

        private async Task<StageResult> RunCompileAsync(
            string filePath,
            QualityGateOptions options,
            CancellationToken ct)
        {
            string outputDirectory = Path.Join(
                Path.GetTempPath(),
                "qmlsharp-qualitygate-cachegen-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(outputDirectory);

            try
            {
                QmlCachegenResult result = await _qmlCachegen
                    .CompileFileAsync(
                        filePath,
                        new QmlCachegenOptions
                        {
                            ImportPaths = options.ImportPaths,
                            OutputDir = outputDirectory,
                        },
                        ct)
                    .ConfigureAwait(false);
                return new StageResult(result.Success, result.Diagnostics, NormalizeDuration(result.ToolResult.DurationMs));
            }
            finally
            {
                TryDeleteDirectory(outputDirectory);
            }
        }

        private async Task<StageResult> RunFullAsync(string filePath, CancellationToken ct)
        {
            QmlRunResult result = await _qmlRunner.RunFileAsync(filePath, ct: ct).ConfigureAwait(false);
            return new StageResult(result.Passed, result.RuntimeErrors, NormalizeDuration(result.ToolResult.DurationMs));
        }

        private static ImmutableArray<QualityGateLevel> GetLevels(QualityGateLevel level)
        {
            return level switch
            {
                QualityGateLevel.Syntax => [QualityGateLevel.Syntax],
                QualityGateLevel.Lint => [QualityGateLevel.Syntax, QualityGateLevel.Lint],
                QualityGateLevel.Compile => [QualityGateLevel.Syntax, QualityGateLevel.Lint, QualityGateLevel.Compile],
                QualityGateLevel.Full =>
                [
                    QualityGateLevel.Syntax,
                    QualityGateLevel.Lint,
                    QualityGateLevel.Compile,
                    QualityGateLevel.Full,
                ],
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown quality gate level."),
            };
        }

        private static QualityGateResult RewriteInputFile(
            QualityGateResult result,
            string fromPath,
            string toPath)
        {
            ImmutableArray<QtDiagnostic> diagnostics = RewriteDiagnostics(result.Diagnostics, fromPath, toPath);
            ImmutableDictionary<QualityGateLevel, ImmutableArray<QtDiagnostic>> levelDiagnostics = result
                .LevelDiagnostics
                .ToImmutableDictionary(
                    static pair => pair.Key,
                    pair => RewriteDiagnostics(pair.Value, fromPath, toPath));

            return result with
            {
                Diagnostics = diagnostics,
                LevelDiagnostics = levelDiagnostics,
            };
        }

        private static ImmutableArray<QtDiagnostic> RewriteDiagnostics(
            ImmutableArray<QtDiagnostic> diagnostics,
            string fromPath,
            string toPath)
        {
            if (diagnostics.IsDefaultOrEmpty)
            {
                return [];
            }

            return diagnostics
                .Select(diagnostic => IsSamePath(diagnostic.File, fromPath)
                    ? diagnostic with { File = toPath }
                    : diagnostic)
                .ToImmutableArray();
        }

        private static void AddLevelDiagnostics(
            Dictionary<QualityGateLevel, ImmutableArray<QtDiagnostic>.Builder> target,
            ImmutableDictionary<QualityGateLevel, ImmutableArray<QtDiagnostic>> source)
        {
            foreach (KeyValuePair<QualityGateLevel, ImmutableArray<QtDiagnostic>> pair in source.OrderBy(static pair => pair.Key))
            {
                if (!target.TryGetValue(pair.Key, out ImmutableArray<QtDiagnostic>.Builder? builder))
                {
                    builder = ImmutableArray.CreateBuilder<QtDiagnostic>();
                    target[pair.Key] = builder;
                }

                builder.AddRange(pair.Value);
            }
        }

        private static ImmutableDictionary<QualityGateLevel, ImmutableArray<QtDiagnostic>> FreezeLevelDiagnostics(
            Dictionary<QualityGateLevel, ImmutableArray<QtDiagnostic>.Builder> diagnostics)
        {
            ImmutableDictionary<QualityGateLevel, ImmutableArray<QtDiagnostic>>.Builder builder =
                ImmutableDictionary.CreateBuilder<QualityGateLevel, ImmutableArray<QtDiagnostic>>();
            foreach (KeyValuePair<QualityGateLevel, ImmutableArray<QtDiagnostic>.Builder> pair in diagnostics.OrderBy(static pair => pair.Key))
            {
                builder[pair.Key] = pair.Value.ToImmutable();
            }

            return builder.ToImmutable();
        }

        private static void AddLevelDurations(
            ImmutableDictionary<QualityGateLevel, long>.Builder target,
            ImmutableDictionary<QualityGateLevel, long> source)
        {
            foreach (KeyValuePair<QualityGateLevel, long> pair in source)
            {
                target[pair.Key] = target.TryGetValue(pair.Key, out long current)
                    ? current + pair.Value
                    : pair.Value;
            }
        }

        private static string NormalizeBatchFilePath(string filePath, string paramName)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or whitespace.", paramName);
            }

            return Path.GetFullPath(filePath);
        }

        private static long SumDurations(ImmutableDictionary<QualityGateLevel, long> durations)
        {
            return durations.Values.Sum();
        }

        private static long NormalizeDuration(long durationMs)
        {
            return Math.Max(1, durationMs);
        }

        private static void ValidateLevel(QualityGateLevel level)
        {
            if (!Enum.IsDefined(level))
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown quality gate level.");
            }
        }

        private static bool IsSamePath(string? left, string right)
        {
            if (left is null)
            {
                return false;
            }

            return GetPathComparer().Equals(NormalizePathKey(left), NormalizePathKey(right));
        }

        private static string NormalizePathKey(string path)
        {
            string candidate = path.Replace('/', Path.DirectorySeparatorChar);
            try
            {
                candidate = Path.GetFullPath(candidate);
            }
            catch (ArgumentException)
            {
                // Best-effort normalization for diagnostics emitted by external tools.
            }
            catch (NotSupportedException)
            {
                // Best-effort normalization for diagnostics emitted by external tools.
            }
            catch (PathTooLongException)
            {
                // Best-effort normalization for diagnostics emitted by external tools.
            }

            return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? candidate.ToUpperInvariant()
                : candidate;
        }

        private static StringComparer GetPathComparer()
        {
            return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup for temporary string-input files.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup for temporary string-input files.
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup for transient qmlcachegen output.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup for transient qmlcachegen output.
            }
        }

        private readonly record struct StageResult(
            bool Passed,
            ImmutableArray<QtDiagnostic> Diagnostics,
            long DurationMs);

        private sealed class BatchAccumulator
        {
            private readonly QualityGateLevel _requestedLevel;
            private readonly ImmutableArray<QualityGateFileResult>.Builder _fileResults;
            private readonly ImmutableArray<QtDiagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<QtDiagnostic>();
            private readonly Dictionary<QualityGateLevel, ImmutableArray<QtDiagnostic>.Builder> _levelDiagnostics = [];
            private readonly ImmutableDictionary<QualityGateLevel, long>.Builder _levelDurations =
                ImmutableDictionary.CreateBuilder<QualityGateLevel, long>();

            private bool _passed = true;
            private QualityGateLevel? _failedAtLevel;
            private QualityGateLevel _completedLevel;

            public BatchAccumulator(QualityGateLevel requestedLevel, int fileCount)
            {
                _requestedLevel = requestedLevel;
                _completedLevel = requestedLevel;
                _fileResults = ImmutableArray.CreateBuilder<QualityGateFileResult>(fileCount);
            }

            public void Add(string filePath, QualityGateResult result)
            {
                _fileResults.Add(CreateFileResult(filePath, result));
                _diagnostics.AddRange(result.Diagnostics);
                AddLevelDiagnostics(_levelDiagnostics, result.LevelDiagnostics);
                AddLevelDurations(_levelDurations, result.LevelDurationMs);
                TrackFailure(result);
            }

            public QualityGateResult ToResult()
            {
                ImmutableDictionary<QualityGateLevel, long> immutableDurations = _levelDurations.ToImmutable();
                return new QualityGateResult
                {
                    Passed = _passed,
                    CompletedLevel = _passed ? _requestedLevel : _completedLevel,
                    FailedAtLevel = _failedAtLevel,
                    Diagnostics = _diagnostics.ToImmutable(),
                    LevelDiagnostics = FreezeLevelDiagnostics(_levelDiagnostics),
                    LevelDurationMs = immutableDurations,
                    TotalDurationMs = SumDurations(immutableDurations),
                    FileResults = _fileResults.MoveToImmutable(),
                };
            }

            private static QualityGateFileResult CreateFileResult(string filePath, QualityGateResult result)
            {
                return new QualityGateFileResult
                {
                    FilePath = filePath,
                    Passed = result.Passed,
                    CompletedLevel = result.CompletedLevel,
                    FailedAtLevel = result.FailedAtLevel,
                    Diagnostics = result.Diagnostics,
                    LevelDurationMs = result.LevelDurationMs,
                    TotalDurationMs = result.TotalDurationMs,
                };
            }

            private void TrackFailure(QualityGateResult result)
            {
                if (result.Passed)
                {
                    return;
                }

                _passed = false;
                if (_failedAtLevel is null)
                {
                    _failedAtLevel = result.FailedAtLevel;
                    _completedLevel = result.CompletedLevel;
                }
            }
        }
    }
}
