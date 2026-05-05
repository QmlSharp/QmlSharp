using QmlSharp.Qt.Tools;
using QtDiagnosticSeverity = QmlSharp.Qt.Tools.DiagnosticSeverity;

namespace QmlSharp.Build
{
    internal sealed class QmlValidationBuildStage : IBuildStage
    {
        private readonly IQmlFormat? formatter;
        private readonly IQmlLint? linter;
        private readonly IPackageResolver packageResolver;

        public QmlValidationBuildStage()
        {
            packageResolver = new PackageResolver();
        }

        public QmlValidationBuildStage(
            IQmlFormat formatter,
            IQmlLint linter,
            IPackageResolver packageResolver)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            ArgumentNullException.ThrowIfNull(linter);
            ArgumentNullException.ThrowIfNull(packageResolver);

            this.formatter = formatter;
            this.linter = linter;
            this.packageResolver = packageResolver;
        }

        public BuildPhase Phase => BuildPhase.QmlValidation;

        public async Task<BuildStageResult> ExecuteAsync(
            BuildContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            if (context.DryRun || (!context.Config.Build.Format && !context.Config.Build.Lint))
            {
                return BuildStageResult.Succeeded();
            }

            ImmutableArray<string> qmlFiles = DiscoverQmlFiles(context.OutputDir);
            if (qmlFiles.IsDefaultOrEmpty)
            {
                return BuildStageResult.Succeeded();
            }

            ImmutableArray<BuildDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<BuildDiagnostic>();
            QtToolServices services = await ResolveQtToolServicesAsync(context, cancellationToken)
                .ConfigureAwait(false);
            if (context.Config.Build.Format)
            {
                await RunFormatterAsync(services.Formatter, qmlFiles, diagnostics, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (context.Config.Build.Lint)
            {
                await RunLinterAsync(services.Linter, context, qmlFiles, diagnostics, cancellationToken)
                    .ConfigureAwait(false);
            }

            ImmutableArray<BuildDiagnostic> finalDiagnostics = diagnostics
                .OrderBy(static diagnostic => diagnostic.FilePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
                .ToImmutableArray();

            return new BuildStageResult
            {
                Success = !finalDiagnostics.Any(IsBlockingDiagnostic),
                Diagnostics = finalDiagnostics,
            };
        }

        private async Task<QtToolServices> ResolveQtToolServicesAsync(
            BuildContext context,
            CancellationToken cancellationToken)
        {
            if (formatter is not null && linter is not null)
            {
                return new QtToolServices(formatter, linter);
            }

            QtToolchain toolchain = new();
            _ = await toolchain.DiscoverAsync(
                    new QtToolchainConfig
                    {
                        QtDir = context.QtDir,
                        Cwd = context.ProjectDir,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            return new QtToolServices(
                new QmlFormat(toolchain, new ToolRunner(), new QtDiagnosticParser()),
                new QmlLint(toolchain, new ToolRunner(), new QtDiagnosticParser()));
        }

        private async Task RunFormatterAsync(
            IQmlFormat formatter,
            ImmutableArray<string> qmlFiles,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            CancellationToken cancellationToken)
        {
            ImmutableArray<QmlFormatResult> results = await formatter.FormatBatchAsync(
                    qmlFiles,
                    new QmlFormatOptions
                    {
                        InPlace = true,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            foreach ((QmlFormatResult result, string filePath) in PairResultsWithFiles(results, qmlFiles))
            {
                AddQtDiagnostics(
                    BuildDiagnosticCode.QmlFormatError,
                    BuildPhase.QmlValidation,
                    result.Diagnostics,
                    diagnostics);

                if (!result.ToolResult.Success && !HasErrorDiagnostic(result.Diagnostics))
                {
                    diagnostics.Add(CreateToolFailureDiagnostic(
                        BuildDiagnosticCode.QmlFormatError,
                        "qmlformat failed",
                        result.ToolResult,
                        filePath));
                }
            }
        }

        private async Task RunLinterAsync(
            IQmlLint linter,
            BuildContext context,
            ImmutableArray<string> qmlFiles,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            CancellationToken cancellationToken)
        {
            ImmutableArray<QmlLintResult> results = await linter.LintBatchAsync(
                    qmlFiles,
                    new QmlLintOptions
                    {
                        JsonOutput = true,
                        ImportPaths = ResolveImportPaths(context),
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            foreach ((QmlLintResult result, string filePath) in PairResultsWithFiles(results, qmlFiles))
            {
                AddQtDiagnostics(
                    BuildDiagnosticCode.QmlLintError,
                    BuildPhase.QmlValidation,
                    result.Diagnostics,
                    diagnostics);

                if ((!result.ToolResult.Success || result.ErrorCount > 0) && !HasErrorDiagnostic(result.Diagnostics))
                {
                    diagnostics.Add(CreateToolFailureDiagnostic(
                        BuildDiagnosticCode.QmlLintError,
                        "qmllint failed",
                        result.ToolResult,
                        filePath));
                }
            }
        }

        private ImmutableArray<string> ResolveImportPaths(BuildContext context)
        {
            ImmutableArray<string>.Builder importPaths = ImmutableArray.CreateBuilder<string>();
            AddIfDirectory(importPaths, Path.Join(context.OutputDir, "qml"));
            AddIfDirectory(importPaths, Path.Join(context.QtDir, "qml"));

            try
            {
                ImmutableArray<ResolvedPackage> packages = packageResolver.Resolve(context.ProjectDir);
                foreach (string importPath in packageResolver.CollectImportPaths(packages))
                {
                    AddIfDirectory(importPaths, importPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
            }

            return importPaths
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static ImmutableArray<string> DiscoverQmlFiles(string outputDir)
        {
            string qmlRoot = Path.Join(outputDir, "qml");
            if (!Directory.Exists(qmlRoot))
            {
                return ImmutableArray<string>.Empty;
            }

            return Directory
                .EnumerateFiles(qmlRoot, "*.qml", SearchOption.AllDirectories)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static void AddQtDiagnostics(
            string code,
            BuildPhase phase,
            ImmutableArray<QtDiagnostic> qtDiagnostics,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics)
        {
            foreach (QtDiagnostic diagnostic in qtDiagnostics.IsDefault ? ImmutableArray<QtDiagnostic>.Empty : qtDiagnostics)
            {
                diagnostics.Add(new BuildDiagnostic(
                    code,
                    MapSeverity(diagnostic.Severity),
                    diagnostic.Message,
                    phase,
                    diagnostic.File));
            }
        }

        private static BuildDiagnostic CreateToolFailureDiagnostic(
            string code,
            string prefix,
            ToolResult toolResult,
            string? filePath)
        {
            string output = string.IsNullOrWhiteSpace(toolResult.Stderr)
                ? toolResult.Stdout
                : toolResult.Stderr;
            string detail = string.IsNullOrWhiteSpace(output)
                ? $"{prefix} with exit code {toolResult.ExitCode}."
                : $"{prefix} with exit code {toolResult.ExitCode}: {output.Trim()}";

            return new BuildDiagnostic(
                code,
                BuildDiagnosticSeverity.Error,
                detail,
                BuildPhase.QmlValidation,
                filePath);
        }

        private static bool HasErrorDiagnostic(ImmutableArray<QtDiagnostic> diagnostics)
        {
            return (diagnostics.IsDefault ? ImmutableArray<QtDiagnostic>.Empty : diagnostics)
                .Any(static diagnostic => diagnostic.Severity == QtDiagnosticSeverity.Error);
        }

        private static BuildDiagnosticSeverity MapSeverity(QtDiagnosticSeverity severity)
        {
            return severity switch
            {
                QtDiagnosticSeverity.Info => BuildDiagnosticSeverity.Info,
                QtDiagnosticSeverity.Warning => BuildDiagnosticSeverity.Warning,
                QtDiagnosticSeverity.Error => BuildDiagnosticSeverity.Error,
                QtDiagnosticSeverity.Hint => BuildDiagnosticSeverity.Info,
                QtDiagnosticSeverity.Disabled => BuildDiagnosticSeverity.Info,
                _ => BuildDiagnosticSeverity.Error,
            };
        }

        private static bool IsBlockingDiagnostic(BuildDiagnostic diagnostic)
        {
            return diagnostic.Severity is BuildDiagnosticSeverity.Error or BuildDiagnosticSeverity.Fatal;
        }

        private static void AddIfDirectory(ImmutableArray<string>.Builder importPaths, string path)
        {
            if (Directory.Exists(path))
            {
                importPaths.Add(Path.GetFullPath(path));
            }
        }

        private static ImmutableArray<(T Result, string FilePath)> PairResultsWithFiles<T>(
            ImmutableArray<T> results,
            ImmutableArray<string> qmlFiles)
        {
            ImmutableArray<(T Result, string FilePath)>.Builder pairs =
                ImmutableArray.CreateBuilder<(T Result, string FilePath)>(results.Length);
            for (int index = 0; index < results.Length; index++)
            {
                string filePath = index < qmlFiles.Length ? qmlFiles[index] : string.Empty;
                pairs.Add((results[index], filePath));
            }

            return pairs.ToImmutable();
        }

        private sealed record QtToolServices(IQmlFormat Formatter, IQmlLint Linter);
    }
}
