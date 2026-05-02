namespace QmlSharp.Compiler
{
    /// <summary>
    /// Configuration for the QmlSharp compiler pipeline.
    /// </summary>
    public sealed record CompilerOptions
    {
        /// <summary>Gets the default compiler source include glob patterns.</summary>
        public static ImmutableArray<string> DefaultIncludePatterns { get; } = ImmutableArray.Create("**/*.cs");

        /// <summary>Gets the default compiler source exclude glob patterns.</summary>
        public static ImmutableArray<string> DefaultExcludePatterns { get; } =
            ImmutableArray.Create("**/obj/**", "**/bin/**", "**/*Tests*/**");

        /// <summary>Gets the MSBuild project file path to compile.</summary>
        public required string ProjectPath { get; init; }

        /// <summary>Gets the output directory for generated compiler artifacts.</summary>
        public required string OutputDir { get; init; }

        /// <summary>Gets the source-map output directory.</summary>
        public string? SourceMapDir { get; init; }

        /// <summary>Gets a value indicating whether source maps should be generated.</summary>
        public bool GenerateSourceMaps { get; init; } = true;

        /// <summary>Gets a value indicating whether generated QML should be formatted.</summary>
        public bool FormatQml { get; init; }

        /// <summary>Gets a value indicating whether generated QML should be linted.</summary>
        public bool LintQml { get; init; }

        /// <summary>Gets the QML module version for generated registrations.</summary>
        public QmlVersion ModuleVersion { get; init; } = new(1, 0);

        /// <summary>Gets the authoritative QML module URI prefix.</summary>
        public required string ModuleUriPrefix { get; init; }

        /// <summary>Gets source include glob patterns.</summary>
        public ImmutableArray<string> IncludePatterns { get; init; } = DefaultIncludePatterns;

        /// <summary>Gets source exclude glob patterns.</summary>
        public ImmutableArray<string> ExcludePatterns { get; init; } = DefaultExcludePatterns;

        /// <summary>Gets the maximum diagnostic severity that still allows compilation to proceed.</summary>
        public DiagnosticSeverity MaxAllowedSeverity { get; init; } = DiagnosticSeverity.Warning;

        /// <summary>Gets a value indicating whether incremental compilation is enabled.</summary>
        public bool Incremental { get; init; } = true;

        /// <summary>Gets the incremental compiler cache directory.</summary>
        public string? CacheDir { get; init; }

        /// <summary>Gets additional Roslyn analyzer assembly paths.</summary>
        public ImmutableArray<string> AdditionalAnalyzers { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Validates required options and returns a normalized copy with computed defaults.
        /// </summary>
        /// <returns>A validated copy of the current options.</returns>
        public CompilerOptions ValidateAndNormalize()
        {
            ValidateRequired(ProjectPath, nameof(ProjectPath));
            ValidateRequired(OutputDir, nameof(OutputDir));
            ValidateRequired(ModuleUriPrefix, nameof(ModuleUriPrefix));

            ValidateModuleVersion(ModuleVersion);
            ValidateMaxAllowedSeverity(MaxAllowedSeverity);

            ImmutableArray<string> includePatterns = IncludePatterns.IsDefaultOrEmpty
                ? DefaultIncludePatterns
                : IncludePatterns;
            ImmutableArray<string> excludePatterns = MergeExcludePatterns(ExcludePatterns);
            string sourceMapDir = string.IsNullOrWhiteSpace(SourceMapDir)
                ? CombineWithRelativeChild(OutputDir, "source-maps")
                : SourceMapDir;
            string cacheDir = string.IsNullOrWhiteSpace(CacheDir)
                ? CombineWithRelativeChild(OutputDir, ".compiler-cache")
                : CacheDir;
            ImmutableArray<string> additionalAnalyzers = AdditionalAnalyzers.IsDefault
                ? ImmutableArray<string>.Empty
                : AdditionalAnalyzers;

            return this with
            {
                SourceMapDir = sourceMapDir,
                CacheDir = cacheDir,
                IncludePatterns = includePatterns,
                ExcludePatterns = excludePatterns,
                AdditionalAnalyzers = additionalAnalyzers,
            };
        }

        /// <summary>
        /// Gets a value indicating whether a diagnostic severity can be reported without aborting compilation.
        /// </summary>
        /// <param name="severity">The severity to evaluate.</param>
        /// <returns><see langword="true"/> when compilation can continue for the severity.</returns>
        public bool Allows(DiagnosticSeverity severity)
        {
            return !ShouldStopOn(severity);
        }

        /// <summary>
        /// Gets a value indicating whether a diagnostic severity must stop compilation.
        /// </summary>
        /// <param name="severity">The severity to evaluate.</param>
        /// <returns><see langword="true"/> when the severity is fatal or exceeds <see cref="MaxAllowedSeverity"/>.</returns>
        public bool ShouldStopOn(DiagnosticSeverity severity)
        {
            if (!Enum.IsDefined(severity))
            {
                throw new ArgumentException("Severity must be a defined diagnostic severity.", nameof(severity));
            }

            return severity == DiagnosticSeverity.Fatal || severity > MaxAllowedSeverity;
        }

        private static void ValidateRequired(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{parameterName} is required.", parameterName);
            }
        }

        private static string CombineWithRelativeChild(string baseDir, string childSegment)
        {
            ValidateRequired(baseDir, nameof(baseDir));
            ValidateRequired(childSegment, nameof(childSegment));

            if (Path.IsPathRooted(childSegment))
            {
                throw new ArgumentException("Child path segment must be relative.", nameof(childSegment));
            }

            return Path.Join(baseDir, childSegment);
        }

        private static void ValidateModuleVersion(QmlVersion moduleVersion)
        {
            if (moduleVersion.Major < 0)
            {
                throw new ArgumentException("ModuleVersion.Major must be greater than or equal to 0.", nameof(moduleVersion));
            }

            if (moduleVersion.Minor < 0)
            {
                throw new ArgumentException("ModuleVersion.Minor must be greater than or equal to 0.", nameof(moduleVersion));
            }
        }

        private static void ValidateMaxAllowedSeverity(DiagnosticSeverity maxAllowedSeverity)
        {
            if (!Enum.IsDefined(maxAllowedSeverity))
            {
                throw new ArgumentException("MaxAllowedSeverity must be a defined diagnostic severity.", nameof(maxAllowedSeverity));
            }
        }

        private static ImmutableArray<string> MergeExcludePatterns(ImmutableArray<string> userPatterns)
        {
            if (userPatterns.IsDefaultOrEmpty)
            {
                return DefaultExcludePatterns;
            }

            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
            HashSet<string> seen = new(StringComparer.Ordinal);

            foreach (string pattern in DefaultExcludePatterns)
            {
                AddPattern(pattern, seen, builder);
            }

            foreach (string pattern in userPatterns)
            {
                AddPattern(pattern, seen, builder);
            }

            return builder.ToImmutable();
        }

        private static void AddPattern(
            string pattern,
            HashSet<string> seen,
            ImmutableArray<string>.Builder builder)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return;
            }

            if (seen.Add(pattern))
            {
                builder.Add(pattern);
            }
        }
    }
}
