using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using QmlSharp.Compiler;

namespace QmlSharp.Build
{
    internal sealed class NativeBuildStage : IBuildStage
    {
        private const string FingerprintFileName = ".qmlsharp-native-inputs.sha256";
        private const string NativeTargetName = "qmlsharp_native";
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly ICppCodeGenerator cppCodeGenerator;
        private readonly IViewModelSchemaSerializer schemaSerializer;
        private readonly IPackageResolver packageResolver;
        private readonly ICMakeBuilder? cmakeBuilder;

        public NativeBuildStage()
            : this(new CppCodeGenerator(), new ViewModelSchemaSerializer(), new PackageResolver(), null)
        {
        }

        public NativeBuildStage(
            ICppCodeGenerator cppCodeGenerator,
            IViewModelSchemaSerializer schemaSerializer,
            IPackageResolver packageResolver,
            ICMakeBuilder? cmakeBuilder = null)
        {
            ArgumentNullException.ThrowIfNull(cppCodeGenerator);
            ArgumentNullException.ThrowIfNull(schemaSerializer);
            ArgumentNullException.ThrowIfNull(packageResolver);

            this.cppCodeGenerator = cppCodeGenerator;
            this.schemaSerializer = schemaSerializer;
            this.packageResolver = packageResolver;
            this.cmakeBuilder = cmakeBuilder;
        }

        public BuildPhase Phase => BuildPhase.CppCodeGenAndBuild;

        public async Task<BuildStageResult> ExecuteAsync(
            BuildContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            NativeStagePaths paths = NativeStagePaths.Create(context.OutputDir);
            if (context.LibraryMode || context.DryRun)
            {
                return BuildStageResult.Succeeded();
            }

            if (context.Config.Native.Prebuilt)
            {
                return ValidatePrebuilt(paths.NativeLibraryPath);
            }

            if (!TryLoadSchemas(context, out ImmutableArray<SchemaFile> schemaFiles, out BuildStageResult? loadFailure))
            {
                return loadFailure!;
            }

            if (schemaFiles.IsDefaultOrEmpty)
            {
                return BuildStageResult.Succeeded();
            }

            string abiSourceDir = ResolveAbiSourceDir(context.ProjectDir);
            string fingerprint = ComputeFingerprint(context, paths, schemaFiles, abiSourceDir);
            if (CanSkipIncrementalBuild(context, paths, schemaFiles, fingerprint))
            {
                return BuildStageResult.Succeeded(
                    artifacts: new BuildArtifacts
                    {
                        NativeLibraryPath = paths.NativeLibraryPath,
                    });
            }

            return await GenerateAndBuildAsync(context, paths, schemaFiles, abiSourceDir, fingerprint, cancellationToken)
                .ConfigureAwait(false);
        }

        private bool TryLoadSchemas(
            BuildContext context,
            out ImmutableArray<SchemaFile> schemaFiles,
            out BuildStageResult? failure)
        {
            try
            {
                schemaFiles = LoadSchemas(context);
                failure = null;
                return true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                schemaFiles = ImmutableArray<SchemaFile>.Empty;
                failure = Failed(
                    BuildDiagnosticCode.CppGenerationFailed,
                    context.OutputDir,
                    "C++ generation failed while loading schemas",
                    exception);
                return false;
            }
        }

        private async Task<BuildStageResult> GenerateAndBuildAsync(
            BuildContext context,
            NativeStagePaths paths,
            ImmutableArray<SchemaFile> schemaFiles,
            string abiSourceDir,
            string fingerprint,
            CancellationToken cancellationToken)
        {
            if (!TryGenerate(
                    context,
                    paths,
                    schemaFiles,
                    abiSourceDir,
                    out CppGenerationResult? generationResult,
                    out BuildStageResult? generationFailure))
            {
                return generationFailure!;
            }

            CppGenerationResult generated = generationResult ?? throw new InvalidOperationException("Generation result was not set.");
            ImmutableArray<BuildDiagnostic> generationDiagnostics = NormalizeDiagnostics(generated.Diagnostics);
            if (generationDiagnostics.Any(IsBlockingDiagnostic))
            {
                return new BuildStageResult
                {
                    Success = false,
                    Diagnostics = generationDiagnostics,
                };
            }

            BuildStageResult? writeFailure = await WriteGeneratedFilesAsync(
                    generated,
                    cancellationToken)
                .ConfigureAwait(false);
            if (writeFailure is not null)
            {
                return AppendDiagnostics(writeFailure, generationDiagnostics);
            }

            BuildStageResult? cmakeFailure = await RunCMakeAsync(
                    context,
                    paths,
                    generated,
                    generationDiagnostics,
                    cancellationToken)
                .ConfigureAwait(false);
            if (cmakeFailure is not null)
            {
                return cmakeFailure;
            }

            await WriteFingerprintAsync(paths.FingerprintPath, fingerprint, cancellationToken)
                .ConfigureAwait(false);

            return CreateSuccess(generated, generationDiagnostics, paths.NativeLibraryPath);
        }

        private bool TryGenerate(
            BuildContext context,
            NativeStagePaths paths,
            ImmutableArray<SchemaFile> schemaFiles,
            string abiSourceDir,
            out CppGenerationResult? result,
            out BuildStageResult? failure)
        {
            try
            {
                result = cppCodeGenerator.Generate(
                    schemaFiles.Select(static schema => schema.Schema).ToImmutableArray(),
                    CreateGenerationOptions(context, abiSourceDir));
                failure = null;
                return true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                result = null;
                failure = Failed(BuildDiagnosticCode.CppGenerationFailed, paths.GeneratedDir, "C++ generation failed", exception);
                return false;
            }
        }

        private static CppGenerationOptions CreateGenerationOptions(BuildContext context, string abiSourceDir)
        {
            return new CppGenerationOptions
            {
                OutputDir = context.OutputDir,
                QtDir = context.QtDir,
                ProjectName = NativeTargetName,
                CmakePreset = context.Config.Native.CmakePreset,
                AbiSourceDir = abiSourceDir,
            };
        }

        private static BuildStageResult CreateSuccess(
            CppGenerationResult generationResult,
            ImmutableArray<BuildDiagnostic> generationDiagnostics,
            string nativeLibraryPath)
        {
            return new BuildStageResult
            {
                Success = true,
                Diagnostics = generationDiagnostics,
                Stats = new BuildStatsDelta
                {
                    CppFilesGenerated = generationResult.Files.Count,
                    NativeLibBuilt = true,
                },
                Artifacts = new BuildArtifacts
                {
                    NativeLibraryPath = nativeLibraryPath,
                },
            };
        }

        private async Task<BuildStageResult?> RunCMakeAsync(
            BuildContext context,
            NativeStagePaths paths,
            CppGenerationResult generationResult,
            ImmutableArray<BuildDiagnostic> generationDiagnostics,
            CancellationToken cancellationToken)
        {
            ICMakeBuilder builder = cmakeBuilder ?? CreateCMakeBuilder(context, paths);
            CMakeStepResult configureResult = await builder
                .ConfigureAsync(paths.BuildDir, context.Config.Native.CmakePreset, cancellationToken)
                .ConfigureAwait(false);
            if (!configureResult.Success)
            {
                BuildDiagnostic diagnostic = CreateCMakeDiagnostic(
                    BuildDiagnosticCode.CMakeConfigureFailed,
                    "CMake configure failed",
                    configureResult,
                    generationResult.CMakeListsPath);
                return new BuildStageResult
                {
                    Success = false,
                    Diagnostics = generationDiagnostics.Add(diagnostic),
                };
            }

            CMakeStepResult buildResult = await builder
                .BuildAsync(paths.BuildDir, cancellationToken)
                .ConfigureAwait(false);
            if (!buildResult.Success)
            {
                BuildDiagnostic diagnostic = CreateCMakeDiagnostic(
                    BuildDiagnosticCode.CMakeBuildFailed,
                    "CMake build failed",
                    buildResult,
                    generationResult.CMakeListsPath);
                return new BuildStageResult
                {
                    Success = false,
                    Diagnostics = generationDiagnostics.Add(diagnostic),
                };
            }

            string builtLibraryPath = builder.GetOutputLibraryPath(paths.BuildDir);
            return CopyNativeLibraryIfNeeded(
                builtLibraryPath,
                paths.NativeLibraryPath,
                generationDiagnostics);
        }

        private ImmutableArray<SchemaFile> LoadSchemas(BuildContext context)
        {
            ImmutableArray<string> schemaPaths = DiscoverProjectSchemaFiles(context.OutputDir)
                .AddRange(DiscoverPackageSchemaFiles(context.ProjectDir))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();

            ImmutableArray<SchemaFile>.Builder builder =
                ImmutableArray.CreateBuilder<SchemaFile>(schemaPaths.Length);
            foreach (string schemaPath in schemaPaths)
            {
                string json = File.ReadAllText(schemaPath, Utf8NoBom);
                ViewModelSchema schema = schemaSerializer.Deserialize(json);
                builder.Add(new SchemaFile(schemaPath, schema));
            }

            return builder.ToImmutable();
        }

        private ImmutableArray<string> DiscoverPackageSchemaFiles(string projectDir)
        {
            try
            {
                ImmutableArray<ResolvedPackage> packages = packageResolver.Resolve(projectDir);
                return packageResolver.CollectSchemas(packages);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                return ImmutableArray<string>.Empty;
            }
        }

        private static ImmutableArray<string> DiscoverProjectSchemaFiles(string outputDir)
        {
            string schemaRoot = Path.Join(outputDir, "schemas");
            if (!Directory.Exists(schemaRoot))
            {
                return ImmutableArray<string>.Empty;
            }

            return Directory
                .EnumerateFiles(schemaRoot, "*.schema.json", SearchOption.AllDirectories)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static async Task<BuildStageResult?> WriteGeneratedFilesAsync(
            CppGenerationResult generationResult,
            CancellationToken cancellationToken)
        {
            foreach (KeyValuePair<string, string> file in generationResult.Files.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                try
                {
                    string? directory = Path.GetDirectoryName(file.Key);
                    if (string.IsNullOrWhiteSpace(directory))
                    {
                        throw new InvalidOperationException("Generated file path must include a directory.");
                    }

                    _ = Directory.CreateDirectory(directory);
                    await File.WriteAllTextAsync(file.Key, file.Value, Utf8NoBom, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    return Failed(
                        BuildDiagnosticCode.CppGenerationFailed,
                        file.Key,
                        "C++ generation failed while writing generated files",
                        exception);
                }
            }

            return null;
        }

        private static BuildStageResult ValidatePrebuilt(string nativeLibraryPath)
        {
            if (File.Exists(nativeLibraryPath) && new FileInfo(nativeLibraryPath).Length > 0)
            {
                return BuildStageResult.Succeeded(
                    artifacts: new BuildArtifacts
                    {
                        NativeLibraryPath = nativeLibraryPath,
                    });
            }

            BuildDiagnostic diagnostic = new(
                BuildDiagnosticCode.CMakeBuildFailed,
                BuildDiagnosticSeverity.Error,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Prebuilt native mode requires an existing native library at '{0}'. Run a full build with native.prebuilt=false first.",
                    nativeLibraryPath),
                BuildPhase.CppCodeGenAndBuild,
                nativeLibraryPath);
            return BuildStageResult.Failed(diagnostic);
        }

        private static bool CanSkipIncrementalBuild(
            BuildContext context,
            NativeStagePaths paths,
            ImmutableArray<SchemaFile> schemaFiles,
            string fingerprint)
        {
            if (context.ForceRebuild || !context.Config.Build.Incremental)
            {
                return false;
            }

            if (!File.Exists(paths.FingerprintPath) || !File.Exists(paths.NativeLibraryPath))
            {
                return false;
            }

            string existingFingerprint = File.ReadAllText(paths.FingerprintPath, Utf8NoBom).Trim();
            return string.Equals(existingFingerprint, fingerprint, StringComparison.Ordinal) &&
                ExpectedGeneratedFilesExist(paths, schemaFiles);
        }

        private static bool ExpectedGeneratedFilesExist(
            NativeStagePaths paths,
            ImmutableArray<SchemaFile> schemaFiles)
        {
            if (!File.Exists(Path.Join(paths.GeneratedDir, "CMakeLists.txt")) ||
                !File.Exists(Path.Join(paths.GeneratedDir, "type_registration.cpp")) ||
                !File.Exists(paths.NativeLibraryPath))
            {
                return false;
            }

            foreach (SchemaFile schemaFile in schemaFiles)
            {
                string className = schemaFile.Schema.ClassName;
                if (!File.Exists(Path.Join(paths.GeneratedDir, className + ".h")) ||
                    !File.Exists(Path.Join(paths.GeneratedDir, className + ".cpp")))
                {
                    return false;
                }
            }

            return true;
        }

        private static string ComputeFingerprint(
            BuildContext context,
            NativeStagePaths paths,
            ImmutableArray<SchemaFile> schemaFiles,
            string abiSourceDir)
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            AppendHash(hash, "qmlsharp-native-stage-v1");
            AppendHash(hash, context.QtDir);
            AppendHash(hash, context.Config.Native.CmakePreset);
            AppendHash(hash, paths.GeneratedDir);
            foreach (SchemaFile schemaFile in schemaFiles.OrderBy(static file => file.Path, StringComparer.Ordinal))
            {
                AppendHash(hash, Path.GetFullPath(schemaFile.Path));
                AppendHash(hash, File.ReadAllText(schemaFile.Path, Utf8NoBom));
            }

            foreach (string nativeContractFile in EnumerateNativeContractFiles(abiSourceDir))
            {
                AppendHash(hash, Path.GetFullPath(nativeContractFile));
                AppendHash(hash, File.ReadAllText(nativeContractFile, Utf8NoBom));
            }

            byte[] digest = hash.GetHashAndReset();
            return Convert.ToHexString(digest);
        }

        private static IEnumerable<string> EnumerateNativeContractFiles(string abiSourceDir)
        {
            if (!Directory.Exists(abiSourceDir))
            {
                yield break;
            }

            foreach (string root in new[] { "include", "src" }.Select(relativeRoot => Path.Join(abiSourceDir, relativeRoot)))
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (string file in Directory
                    .EnumerateFiles(root, "qmlsharp_*.*", SearchOption.AllDirectories)
                    .Where(IsNativeContractFile)
                    .OrderBy(static path => path, StringComparer.Ordinal))
                {
                    yield return file;
                }
            }
        }

        private static bool IsNativeContractFile(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".h", StringComparison.Ordinal) ||
                string.Equals(extension, ".cpp", StringComparison.Ordinal);
        }

        private static void AppendHash(IncrementalHash hash, string value)
        {
            byte[] bytes = Utf8NoBom.GetBytes(value);
            hash.AppendData(bytes);
            hash.AppendData([0]);
        }

        private static BuildStageResult? CopyNativeLibraryIfNeeded(
            string builtLibraryPath,
            string nativeLibraryPath,
            ImmutableArray<BuildDiagnostic> priorDiagnostics)
        {
            if (!File.Exists(builtLibraryPath))
            {
                BuildDiagnostic diagnostic = new(
                BuildDiagnosticCode.CMakeBuildFailed,
                BuildDiagnosticSeverity.Error,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "CMake build completed but the expected native library was not found at '{0}'.",
                        builtLibraryPath),
                    BuildPhase.CppCodeGenAndBuild,
                    builtLibraryPath);
                return new BuildStageResult
                {
                    Success = false,
                    Diagnostics = priorDiagnostics.Add(diagnostic),
                };
            }

            string normalizedBuiltPath = Path.GetFullPath(builtLibraryPath);
            string normalizedNativePath = Path.GetFullPath(nativeLibraryPath);
            if (string.Equals(normalizedBuiltPath, normalizedNativePath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                string? nativeDirectory = Path.GetDirectoryName(normalizedNativePath);
                if (string.IsNullOrWhiteSpace(nativeDirectory))
                {
                    throw new InvalidOperationException("Native library output path must include a directory.");
                }

                _ = Directory.CreateDirectory(nativeDirectory);
                File.Copy(normalizedBuiltPath, normalizedNativePath, overwrite: true);
                return null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                BuildDiagnostic diagnostic = new(
                BuildDiagnosticCode.CMakeBuildFailed,
                BuildDiagnosticSeverity.Error,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Native library copy failed from '{0}' to '{1}': {2}",
                        normalizedBuiltPath,
                        normalizedNativePath,
                        exception.Message),
                    BuildPhase.CppCodeGenAndBuild,
                    normalizedBuiltPath);
                return new BuildStageResult
                {
                    Success = false,
                    Diagnostics = priorDiagnostics.Add(diagnostic),
                };
            }
        }

        private static async Task WriteFingerprintAsync(
            string fingerprintPath,
            string fingerprint,
            CancellationToken cancellationToken)
        {
            string? directory = Path.GetDirectoryName(fingerprintPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fingerprintPath, fingerprint + "\n", Utf8NoBom, cancellationToken)
                .ConfigureAwait(false);
        }

        private static ICMakeBuilder CreateCMakeBuilder(BuildContext context, NativeStagePaths paths)
        {
            return new CMakeBuilder(new CMakeBuilderOptions
            {
                SourceDir = paths.GeneratedDir,
                NativeOutputDir = paths.NativeDir,
                QtDir = context.QtDir,
                BuildPreset = context.Config.Native.CmakePreset,
            });
        }

        private static string ResolveAbiSourceDir(string projectDir)
        {
            string? projectCandidate = FindNativeSourceDir(projectDir);
            if (projectCandidate is not null)
            {
                return projectCandidate;
            }

            string? assemblyCandidate = FindNativeSourceDir(AppContext.BaseDirectory);
            return assemblyCandidate ?? Path.Join(projectDir, "native");
        }

        private static string? FindNativeSourceDir(string startDirectory)
        {
            DirectoryInfo? current = new(Path.GetFullPath(startDirectory));
            while (current is not null)
            {
                string nativeDir = Path.Join(current.FullName, "native");
                string abiHeader = Path.Join(nativeDir, "include", "qmlsharp", "qmlsharp_abi.h");
                if (File.Exists(abiHeader))
                {
                    return nativeDir;
                }

                current = current.Parent;
            }

            return null;
        }

        private static BuildStageResult AppendDiagnostics(
            BuildStageResult result,
            ImmutableArray<BuildDiagnostic> priorDiagnostics)
        {
            return result with
            {
                Diagnostics = priorDiagnostics.AddRange(result.Diagnostics),
            };
        }

        private static ImmutableArray<BuildDiagnostic> NormalizeDiagnostics(
            ImmutableArray<BuildDiagnostic> diagnostics)
        {
            return diagnostics.IsDefault ? ImmutableArray<BuildDiagnostic>.Empty : diagnostics;
        }

        private static bool IsBlockingDiagnostic(BuildDiagnostic diagnostic)
        {
            return diagnostic.Severity is BuildDiagnosticSeverity.Error or BuildDiagnosticSeverity.Fatal;
        }

        private static BuildDiagnostic CreateCMakeDiagnostic(
            string code,
            string prefix,
            CMakeStepResult result,
            string filePath)
        {
            string detail = string.Format(
                CultureInfo.InvariantCulture,
                "{0} with exit code {1}.{2}{3}{4}{5}",
                prefix,
                result.ExitCode,
                Environment.NewLine,
                FormatOutput("stdout", result.Stdout),
                Environment.NewLine,
                FormatOutput("stderr", result.Stderr));
            return new BuildDiagnostic(
                code,
                BuildDiagnosticSeverity.Error,
                detail,
                BuildPhase.CppCodeGenAndBuild,
                filePath);
        }

        private static string FormatOutput(string streamName, string output)
        {
            return string.IsNullOrWhiteSpace(output)
                ? streamName + ": <empty>"
                : streamName + ": " + output.Trim();
        }

        private static BuildStageResult Failed(
            string code,
            string filePath,
            string messagePrefix,
            Exception exception)
        {
            BuildDiagnostic diagnostic = new(
                code,
                BuildDiagnosticSeverity.Error,
                string.Format(CultureInfo.InvariantCulture, "{0}: {1}", messagePrefix, exception.Message),
                BuildPhase.CppCodeGenAndBuild,
                filePath);
            return BuildStageResult.Failed(diagnostic);
        }

        private sealed record SchemaFile(string Path, ViewModelSchema Schema);

        private sealed record NativeStagePaths(
            string NativeDir,
            string GeneratedDir,
            string BuildDir,
            string NativeLibraryPath,
            string FingerprintPath)
        {
            public static NativeStagePaths Create(string outputDir)
            {
                string nativeDir = Path.Join(outputDir, "native");
                string generatedDir = Path.Join(nativeDir, "generated");
                string buildDir = Path.Join(nativeDir, "build");
                return new NativeStagePaths(
                    nativeDir,
                    generatedDir,
                    buildDir,
                    Path.Join(nativeDir, NativeLibraryNames.GetFileName(NativeTargetName)),
                    Path.Join(generatedDir, FingerprintFileName));
            }
        }
    }
}
