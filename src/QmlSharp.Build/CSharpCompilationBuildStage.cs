using System.Globalization;
using QmlSharp.Compiler;
using CompilerDiagnosticSeverity = QmlSharp.Compiler.DiagnosticSeverity;
using CompilerQmlVersion = QmlSharp.Compiler.QmlVersion;

namespace QmlSharp.Build
{
    internal sealed class CSharpCompilationBuildStage : IBuildStage
    {
        private readonly ICompiler compiler;

        public CSharpCompilationBuildStage()
            : this(new QmlCompiler())
        {
        }

        public CSharpCompilationBuildStage(ICompiler compiler)
        {
            ArgumentNullException.ThrowIfNull(compiler);
            this.compiler = compiler;
        }

        public BuildPhase Phase => BuildPhase.CSharpCompilation;

        public Task<BuildStageResult> ExecuteAsync(BuildContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            if (context.DryRun)
            {
                return Task.FromResult(BuildStageResult.Succeeded());
            }

            if (!TryCreateCompilerOptions(context, out CompilerOptions? options, out BuildStageResult? optionsFailure))
            {
                return Task.FromResult(optionsFailure ?? throw new InvalidOperationException("Compiler options failure was not set."));
            }

            CompilerOptions compilerOptions = options ?? throw new InvalidOperationException("Compiler options were not set.");
            return Task.FromResult(CompileAndWrite(compilerOptions));
        }

        private BuildStageResult CompileAndWrite(CompilerOptions options)
        {
            CompilationResult compilationResult;
            try
            {
                compilationResult = compiler.Compile(options);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return BuildStageResult.Failed(CreateDiagnostic(
                    BuildDiagnosticCode.CompilationFailed,
                    $"C# compilation failed: {exception.Message}",
                    options.ProjectPath));
            }

            if (!compilationResult.Success)
            {
                return CreateFailedCompilationResult(compilationResult);
            }

            return WriteCompilerOutput(compilationResult, options);
        }

        private static BuildStageResult CreateFailedCompilationResult(CompilationResult compilationResult)
        {
            return new BuildStageResult
            {
                Success = false,
                Diagnostics = CreateDiagnostics(compilationResult, outputResult: null),
                Stats = new BuildStatsDelta
                {
                    FilesCompiled = compilationResult.Stats.TotalFiles,
                },
            };
        }

        private BuildStageResult WriteCompilerOutput(CompilationResult compilationResult, CompilerOptions options)
        {
            OutputResult outputResult;
            try
            {
                outputResult = compiler.WriteOutput(compilationResult, options);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return BuildStageResult.Failed(CreateDiagnostic(
                    BuildDiagnosticCode.SchemaGenerationFailed,
                    $"Compiler output writing failed: {exception.Message}",
                    options.OutputDir));
            }

            ImmutableArray<BuildDiagnostic> diagnostics = CreateDiagnostics(compilationResult, outputResult);
            bool success = compilationResult.Success &&
                outputResult.Success &&
                !diagnostics.Any(IsBlockingDiagnostic);

            return new BuildStageResult
            {
                Success = success,
                Diagnostics = diagnostics,
                Stats = new BuildStatsDelta
                {
                    FilesCompiled = compilationResult.Stats.TotalFiles,
                    SchemasGenerated = outputResult.SchemaFiles.Length,
                },
                Artifacts = new BuildArtifacts
                {
                    QmlFiles = outputResult.QmlFiles,
                    SchemaFiles = outputResult.SchemaFiles,
                    EventBindingsFile = outputResult.EventBindingsFile,
                    SourceMapFiles = outputResult.SourceMapFiles,
                },
            };
        }

        private static bool TryCreateCompilerOptions(
            BuildContext context,
            out CompilerOptions? options,
            out BuildStageResult? failure)
        {
            options = null;
            failure = null;

            string? projectPath = FindProjectFile(context.ProjectDir);
            if (projectPath is null)
            {
                failure = BuildStageResult.Failed(CreateDiagnostic(
                    BuildDiagnosticCode.CompilationFailed,
                    "C# compilation failed because no .csproj file was found in the project directory.",
                    context.ProjectDir));
                return false;
            }

            options = new CompilerOptions
            {
                ProjectPath = projectPath,
                OutputDir = context.OutputDir,
                SourceMapDir = Path.Join(context.OutputDir, "source-maps"),
                GenerateSourceMaps = context.Config.Build.SourceMaps,
                FormatQml = false,
                LintQml = false,
                ModuleUriPrefix = context.Config.Module.Prefix,
                ModuleVersion = new CompilerQmlVersion(
                    context.Config.Module.Version.Major,
                    context.Config.Module.Version.Minor),
                IncludePatterns = CreateIncludePatterns(context.FileFilter),
                Incremental = context.Config.Build.Incremental && !context.ForceRebuild,
            };
            return true;
        }

        private static ImmutableArray<string> CreateIncludePatterns(string? fileFilter)
        {
            if (string.IsNullOrWhiteSpace(fileFilter))
            {
                return CompilerOptions.DefaultIncludePatterns;
            }

            return ImmutableArray.Create(fileFilter.Trim());
        }

        private static string? FindProjectFile(string projectDir)
        {
            if (!Directory.Exists(projectDir))
            {
                return null;
            }

            return Directory
                .EnumerateFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static ImmutableArray<BuildDiagnostic> CreateDiagnostics(
            CompilationResult compilationResult,
            OutputResult? outputResult)
        {
            ImmutableArray<BuildDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<BuildDiagnostic>();
            foreach (CompilerDiagnostic diagnostic in compilationResult.Diagnostics)
            {
                diagnostics.Add(MapCompilerDiagnostic(diagnostic));
            }

            foreach (CompilerDiagnostic diagnostic in outputResult?.Diagnostics ?? ImmutableArray<CompilerDiagnostic>.Empty)
            {
                diagnostics.Add(MapCompilerDiagnostic(diagnostic));
            }

            if (compilationResult.Success &&
                outputResult is not null &&
                outputResult.Success &&
                outputResult.SchemaFiles.IsDefaultOrEmpty)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.NoViewModelsFound,
                    "C# compilation produced no ViewModel schemas.",
                    null));
            }

            return diagnostics
                .OrderBy(static diagnostic => diagnostic.FilePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Phase)
                .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static BuildDiagnostic MapCompilerDiagnostic(CompilerDiagnostic diagnostic)
        {
            string code = IsOutputGenerationDiagnostic(diagnostic.Code)
                ? BuildDiagnosticCode.SchemaGenerationFailed
                : BuildDiagnosticCode.CompilationFailed;
            string phase = string.IsNullOrWhiteSpace(diagnostic.Phase)
                ? "compiler"
                : diagnostic.Phase;
            string message = string.Format(
                CultureInfo.InvariantCulture,
                "{0}: {1} {2}",
                diagnostic.Code,
                phase,
                diagnostic.Message);

            return new BuildDiagnostic(
                code,
                MapSeverity(diagnostic.Severity),
                message,
                BuildPhase.CSharpCompilation,
                diagnostic.Location?.FilePath);
        }

        private static bool IsOutputGenerationDiagnostic(string code)
        {
            return string.Equals(code, DiagnosticCodes.OutputWriteFailed, StringComparison.Ordinal) ||
                string.Equals(code, DiagnosticCodes.SchemaSerializationFailed, StringComparison.Ordinal) ||
                string.Equals(code, DiagnosticCodes.SourceMapWriteFailed, StringComparison.Ordinal);
        }

        private static BuildDiagnosticSeverity MapSeverity(CompilerDiagnosticSeverity severity)
        {
            return severity switch
            {
                CompilerDiagnosticSeverity.Info => BuildDiagnosticSeverity.Info,
                CompilerDiagnosticSeverity.Warning => BuildDiagnosticSeverity.Warning,
                CompilerDiagnosticSeverity.Error => BuildDiagnosticSeverity.Error,
                CompilerDiagnosticSeverity.Fatal => BuildDiagnosticSeverity.Fatal,
                _ => BuildDiagnosticSeverity.Error,
            };
        }

        private static BuildDiagnostic CreateDiagnostic(string code, string message, string? filePath)
        {
            return new BuildDiagnostic(
                code,
                BuildDiagnosticSeverity.Error,
                message,
                BuildPhase.CSharpCompilation,
                filePath);
        }

        private static bool IsBlockingDiagnostic(BuildDiagnostic diagnostic)
        {
            return diagnostic.Severity is BuildDiagnosticSeverity.Error or BuildDiagnosticSeverity.Fatal;
        }
    }
}
