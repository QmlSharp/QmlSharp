using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QmlSharp.Build
{
    /// <summary>Filesystem implementation of the QmlSharp product layout contract.</summary>
    public sealed class ProductLayout : IProductLayout
    {
        private const string ManifestFileName = "manifest.json";
        private const string EventBindingsFileName = "event-bindings.json";
        private const string ModuleManifestFileName = "qmlsharp.module.json";
        private const string NativeTargetName = "qmlsharp_native";
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        /// <inheritdoc />
        public void CreateDirectoryStructure(string outputDir)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputDir);

            CreateApplicationDirectories(Path.GetFullPath(outputDir), includeGeneratedNative: true);
        }

        /// <inheritdoc />
        public ProductAssemblyResult Assemble(BuildContext context, BuildArtifacts artifacts)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(artifacts);

            if (context.DryRun)
            {
                return new ProductAssemblyResult(true, 0, 0, ImmutableArray<string>.Empty);
            }

            return context.LibraryMode
                ? AssembleLibrary(context, artifacts)
                : AssembleApplication(context, artifacts);
        }

        /// <inheritdoc />
        public string GenerateManifest(BuildResult result, BuildContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            string outputRoot = GetApplicationRoot(context);
            ProductManifest manifest = CreateProductManifest(outputRoot, result.Artifacts, context);
            return SerializeManifest(manifest);
        }

        /// <inheritdoc />
        public ImmutableArray<BuildDiagnostic> ValidateOutput(string outputDir)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputDir);

            string root = Path.GetFullPath(outputDir);
            if (File.Exists(Path.Join(root, ModuleManifestFileName)) &&
                !File.Exists(Path.Join(root, ManifestFileName)))
            {
                return ValidateLibraryOutput(root);
            }

            return ValidateApplicationOutput(root);
        }

        internal static string GetProductRoot(BuildContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.LibraryMode ? GetLibraryRoot(context) : GetApplicationRoot(context);
        }

        private static ProductAssemblyResult AssembleApplication(BuildContext context, BuildArtifacts artifacts)
        {
            string outputRoot = GetApplicationRoot(context);
            bool includeGeneratedNative = IsDevelopmentMode(context.Config.Build.Mode);
            CreateApplicationDirectories(outputRoot, includeGeneratedNative);

            ImmutableArray<BuildDiagnostic>.Builder diagnostics =
                ImmutableArray.CreateBuilder<BuildDiagnostic>();
            ImmutableArray<string>.Builder outputFiles = ImmutableArray.CreateBuilder<string>();
            int filesCopied = 0;
            long totalBytes = 0;

            CopyApplicationContractArtifacts(
                context,
                artifacts,
                outputRoot,
                diagnostics,
                outputFiles,
                ref filesCopied,
                ref totalBytes);
            CopyApplicationRuntimeArtifacts(
                context,
                artifacts,
                outputRoot,
                includeGeneratedNative,
                diagnostics,
                outputFiles,
                ref filesCopied,
                ref totalBytes);
            ProductManifest? manifest = WriteApplicationManifestIfPossible(
                context,
                artifacts,
                outputRoot,
                diagnostics,
                outputFiles,
                ref totalBytes);

            return CreateAssemblyResult(
                outputRoot,
                filesCopied,
                totalBytes,
                outputFiles,
                diagnostics,
                manifest);
        }

        private static ProductAssemblyResult AssembleLibrary(BuildContext context, BuildArtifacts artifacts)
        {
            string outputRoot = GetLibraryRoot(context);
            CreateLibraryDirectories(outputRoot);

            ImmutableArray<BuildDiagnostic>.Builder diagnostics =
                ImmutableArray.CreateBuilder<BuildDiagnostic>();
            ImmutableArray<string>.Builder outputFiles = ImmutableArray.CreateBuilder<string>();
            int filesCopied = 0;
            long totalBytes = 0;

            CopyLibraryArtifacts(
                context,
                artifacts,
                outputRoot,
                diagnostics,
                outputFiles,
                ref filesCopied,
                ref totalBytes);
            WriteModuleManifestIfPossible(context, outputRoot, diagnostics, outputFiles, ref totalBytes);

            return CreateAssemblyResult(
                outputRoot,
                filesCopied,
                totalBytes,
                outputFiles,
                diagnostics,
                manifest: null);
        }

        private static void CopyApplicationContractArtifacts(
            BuildContext context,
            BuildArtifacts artifacts,
            string outputRoot,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            ImmutableArray<string>.Builder outputFiles,
            ref int filesCopied,
            ref long totalBytes)
        {
            CopyAll(
                artifacts.QmlFiles,
                outputRoot,
                "qml",
                path => GetQmlRelativePath(context, path),
                diagnostics,
                outputFiles,
                ref filesCopied,
                ref totalBytes);
            CopyAll(
                artifacts.ModuleMetadataFiles,
                outputRoot,
                "qml",
                path => GetQmlRelativePath(context, path),
                diagnostics,
                outputFiles,
                ref filesCopied,
                ref totalBytes);
            CopyAll(
                artifacts.SchemaFiles,
                outputRoot,
                "schemas",
                GetSchemaRelativePath,
                diagnostics,
                outputFiles,
                ref filesCopied,
                ref totalBytes);
            CopyEventBindings(artifacts.EventBindingsFile, outputRoot, diagnostics, outputFiles, ref filesCopied, ref totalBytes);
        }

        private static void CopyApplicationRuntimeArtifacts(
            BuildContext context,
            BuildArtifacts artifacts,
            string outputRoot,
            bool includeGeneratedNative,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            ImmutableArray<string>.Builder outputFiles,
            ref int filesCopied,
            ref long totalBytes)
        {
            CopyAll(
                artifacts.SourceMapFiles,
                outputRoot,
                "source-maps",
                GetSourceMapRelativePath,
                diagnostics,
                outputFiles,
                ref filesCopied,
                ref totalBytes);
            CopyAll(
                artifacts.AssetFiles,
                outputRoot,
                "assets",
                GetAssetRelativePath,
                diagnostics,
                outputFiles,
                ref filesCopied,
                ref totalBytes);
            CopyNativeLibrary(artifacts.NativeLibraryPath, outputRoot, diagnostics, outputFiles, ref filesCopied, ref totalBytes);
            CopyManagedOutput(artifacts.AssemblyPath, outputRoot, diagnostics, outputFiles, ref filesCopied, ref totalBytes);
            ApplyGeneratedNativePolicy(context, artifacts, outputRoot, includeGeneratedNative, diagnostics, outputFiles, ref filesCopied, ref totalBytes);
        }

        private static void CopyLibraryArtifacts(
            BuildContext context,
            BuildArtifacts artifacts,
            string outputRoot,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            ImmutableArray<string>.Builder outputFiles,
            ref int filesCopied,
            ref long totalBytes)
        {
            CopyAll(
                artifacts.QmlFiles,
                outputRoot,
                "qml",
                path => GetQmlRelativePath(context, path),
                diagnostics,
                outputFiles,
                ref filesCopied,
                ref totalBytes);
            CopyAll(
                artifacts.ModuleMetadataFiles,
                outputRoot,
                "qml",
                path => GetQmlRelativePath(context, path),
                diagnostics,
                outputFiles,
                ref filesCopied,
                ref totalBytes);
            CopyAll(
                artifacts.SchemaFiles,
                outputRoot,
                "schemas",
                GetSchemaRelativePath,
                diagnostics,
                outputFiles,
                ref filesCopied,
                ref totalBytes);
        }

        private static ProductManifest? WriteApplicationManifestIfPossible(
            BuildContext context,
            BuildArtifacts artifacts,
            string outputRoot,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            ImmutableArray<string>.Builder outputFiles,
            ref long totalBytes)
        {
            if (diagnostics.Any(IsBlockingDiagnostic))
            {
                return null;
            }

            try
            {
                ProductManifest manifest = CreateProductManifest(outputRoot, artifacts, context);
                string manifestPath = Path.Join(outputRoot, ManifestFileName);
                File.WriteAllText(manifestPath, SerializeManifest(manifest), Utf8NoBom);
                AddOutputFile(manifestPath, outputFiles, ref totalBytes);
                return manifest;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.ManifestWriteFailed,
                    $"Manifest write failed: {exception.Message}",
                    Path.Join(outputRoot, ManifestFileName)));
                return null;
            }
        }

        private static void WriteModuleManifestIfPossible(
            BuildContext context,
            string outputRoot,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            ImmutableArray<string>.Builder outputFiles,
            ref long totalBytes)
        {
            if (diagnostics.Any(IsBlockingDiagnostic))
            {
                return;
            }

            try
            {
                string moduleManifestPath = Path.Join(outputRoot, ModuleManifestFileName);
                File.WriteAllText(moduleManifestPath, SerializeModuleManifest(context, outputRoot), Utf8NoBom);
                AddOutputFile(moduleManifestPath, outputFiles, ref totalBytes);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.ManifestWriteFailed,
                    $"Module manifest write failed: {exception.Message}",
                    Path.Join(outputRoot, ModuleManifestFileName)));
            }
        }

        private static ProductAssemblyResult CreateAssemblyResult(
            string outputRoot,
            int filesCopied,
            long totalBytes,
            ImmutableArray<string>.Builder outputFiles,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            ProductManifest? manifest)
        {
            diagnostics.AddRange(ValidateOutputInternal(outputRoot));
            ImmutableArray<BuildDiagnostic> finalDiagnostics = diagnostics
                .Distinct()
                .OrderBy(static diagnostic => diagnostic.Phase)
                .ThenBy(static diagnostic => diagnostic.FilePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
                .ToImmutableArray();
            bool success = !finalDiagnostics.Any(IsBlockingDiagnostic);
            return new ProductAssemblyResult(
                success,
                filesCopied,
                totalBytes,
                outputFiles
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static path => path, StringComparer.Ordinal)
                    .ToImmutableArray())
            {
                Diagnostics = finalDiagnostics,
                Manifest = manifest,
            };
        }

        private static void CreateApplicationDirectories(string outputRoot, bool includeGeneratedNative)
        {
            _ = Directory.CreateDirectory(outputRoot);
            _ = Directory.CreateDirectory(Path.Join(outputRoot, "qml"));
            _ = Directory.CreateDirectory(Path.Join(outputRoot, "schemas"));
            _ = Directory.CreateDirectory(Path.Join(outputRoot, "native"));
            if (includeGeneratedNative)
            {
                _ = Directory.CreateDirectory(Path.Join(outputRoot, "native", "generated"));
            }

            _ = Directory.CreateDirectory(Path.Join(outputRoot, "managed"));
            _ = Directory.CreateDirectory(Path.Join(outputRoot, "source-maps"));
            _ = Directory.CreateDirectory(Path.Join(outputRoot, "assets"));
        }

        private static void CreateLibraryDirectories(string outputRoot)
        {
            _ = Directory.CreateDirectory(outputRoot);
            _ = Directory.CreateDirectory(Path.Join(outputRoot, "qml"));
            _ = Directory.CreateDirectory(Path.Join(outputRoot, "schemas"));
        }

        private static void CopyAll(
            ImmutableArray<string> sourceFiles,
            string outputRoot,
            string destinationRoot,
            Func<string, string> getRelativePath,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            ImmutableArray<string>.Builder outputFiles,
            ref int filesCopied,
            ref long totalBytes)
        {
            foreach (string sourcePath in NormalizePaths(sourceFiles))
            {
                if (!File.Exists(sourcePath))
                {
                    diagnostics.Add(CreateDiagnostic(
                        BuildDiagnosticCode.OutputAssemblyFailed,
                        $"Output assembly failed because source artifact '{sourcePath}' does not exist.",
                        sourcePath));
                    continue;
                }

                string relativePath = getRelativePath(sourcePath);
                string destinationPath = Path.Join(outputRoot, destinationRoot, relativePath);
                CopyOne(sourcePath, destinationPath, diagnostics, outputFiles, ref filesCopied, ref totalBytes);
            }
        }

        private static void CopyEventBindings(
            string? eventBindingsFile,
            string outputRoot,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            ImmutableArray<string>.Builder outputFiles,
            ref int filesCopied,
            ref long totalBytes)
        {
            if (string.IsNullOrWhiteSpace(eventBindingsFile) || !File.Exists(eventBindingsFile))
            {
                return;
            }

            CopyOne(
                eventBindingsFile,
                Path.Join(outputRoot, EventBindingsFileName),
                diagnostics,
                outputFiles,
                ref filesCopied,
                ref totalBytes);
        }

        private static void CopyNativeLibrary(
            string? nativeLibraryPath,
            string outputRoot,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            ImmutableArray<string>.Builder outputFiles,
            ref int filesCopied,
            ref long totalBytes)
        {
            if (string.IsNullOrWhiteSpace(nativeLibraryPath) || !File.Exists(nativeLibraryPath))
            {
                return;
            }

            CopyOne(
                nativeLibraryPath,
                Path.Join(outputRoot, "native", Path.GetFileName(nativeLibraryPath)),
                diagnostics,
                outputFiles,
                ref filesCopied,
                ref totalBytes);
        }

        private static void CopyOne(
            string sourcePath,
            string destinationPath,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            ImmutableArray<string>.Builder outputFiles,
            ref int filesCopied,
            ref long totalBytes)
        {
            try
            {
                string normalizedSource = Path.GetFullPath(sourcePath);
                string normalizedDestination = Path.GetFullPath(destinationPath);
                if (!File.Exists(normalizedSource))
                {
                    diagnostics.Add(CreateDiagnostic(
                        BuildDiagnosticCode.OutputAssemblyFailed,
                        $"Output assembly failed because source artifact '{normalizedSource}' does not exist.",
                        normalizedSource));
                    return;
                }

                string? directory = Path.GetDirectoryName(normalizedDestination);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    _ = Directory.CreateDirectory(directory);
                }

                if (!IsSamePath(normalizedSource, normalizedDestination))
                {
                    File.Copy(normalizedSource, normalizedDestination, overwrite: true);
                    filesCopied++;
                }

                AddOutputFile(normalizedDestination, outputFiles, ref totalBytes);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.OutputAssemblyFailed,
                    $"Output assembly failed while copying '{sourcePath}' to '{destinationPath}': {exception.Message}",
                    sourcePath));
            }
        }

        private static void CopyManagedOutput(
            string? assemblyPath,
            string outputRoot,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            ImmutableArray<string>.Builder outputFiles,
            ref int filesCopied,
            ref long totalBytes)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
            {
                return;
            }

            string assemblyDirectory = Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!;
            foreach (string sourcePath in Directory
                .EnumerateFiles(assemblyDirectory, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => path, StringComparer.Ordinal))
            {
                string destinationPath = Path.Join(outputRoot, "managed", Path.GetFileName(sourcePath));
                CopyOne(sourcePath, destinationPath, diagnostics, outputFiles, ref filesCopied, ref totalBytes);
            }
        }

        private static void ApplyGeneratedNativePolicy(
            BuildContext context,
            BuildArtifacts artifacts,
            string outputRoot,
            bool includeGeneratedNative,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            ImmutableArray<string>.Builder outputFiles,
            ref int filesCopied,
            ref long totalBytes)
        {
            string destinationGeneratedDir = Path.Join(outputRoot, "native", "generated");
            if (!includeGeneratedNative)
            {
                try
                {
                    if (Directory.Exists(destinationGeneratedDir))
                    {
                        Directory.Delete(destinationGeneratedDir, recursive: true);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    diagnostics.Add(CreateDiagnostic(
                        BuildDiagnosticCode.OutputAssemblyFailed,
                        $"Output assembly failed while removing development native output: {exception.Message}",
                        destinationGeneratedDir));
                }

                return;
            }

            string sourceGeneratedDir = ResolveGeneratedNativeSourceDir(context, artifacts);
            if (!Directory.Exists(sourceGeneratedDir))
            {
                return;
            }

            foreach (string sourcePath in Directory
                .EnumerateFiles(sourceGeneratedDir, "*", SearchOption.AllDirectories)
                .OrderBy(static path => path, StringComparer.Ordinal))
            {
                string relativePath = Path.GetRelativePath(sourceGeneratedDir, sourcePath);
                string destinationPath = Path.Join(destinationGeneratedDir, relativePath);
                CopyOne(sourcePath, destinationPath, diagnostics, outputFiles, ref filesCopied, ref totalBytes);
            }
        }

        private static string ResolveGeneratedNativeSourceDir(BuildContext context, BuildArtifacts artifacts)
        {
            if (!string.IsNullOrWhiteSpace(artifacts.NativeLibraryPath))
            {
                string nativeDir = Path.GetDirectoryName(Path.GetFullPath(artifacts.NativeLibraryPath))!;
                string generatedDir = Path.Join(nativeDir, "generated");
                if (Directory.Exists(generatedDir))
                {
                    return generatedDir;
                }
            }

            return Path.Join(GetApplicationRoot(context), "native", "generated");
        }

        private static ProductManifest CreateProductManifest(
            string outputRoot,
            BuildArtifacts artifacts,
            BuildContext context)
        {
            SchemaMetadata schemaMetadata = ReadSchemaMetadata(outputRoot);
            ImmutableArray<string> modules = schemaMetadata.Modules.IsDefaultOrEmpty
                ? ImmutableArray.Create(context.Config.Module.Prefix)
                : schemaMetadata.Modules;

            return new ProductManifest(
                context.Config.Name ?? context.Config.Module.Prefix,
                context.Config.Version,
                context.Config.Build.Mode,
                DateTimeOffset.UtcNow,
                ResolveQtVersion(context.QtDir),
                RuntimeInformation.FrameworkDescription,
                modules,
                schemaMetadata.ViewModels,
                ComputeFileHashes(outputRoot),
                ResolveNativeLibRelativePath(outputRoot, artifacts),
                ResolveManagedAssemblyRelativePath(outputRoot, artifacts, context));
        }

        private static string SerializeManifest(ProductManifest manifest)
        {
            SortedDictionary<string, string> sortedHashes = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> hash in manifest.FileHashes.OrderBy(static hash => hash.Key, StringComparer.Ordinal))
            {
                sortedHashes[hash.Key] = hash.Value;
            }

            ProductManifestJson json = new(
                manifest.ProjectName,
                manifest.Version,
                manifest.BuildMode,
                manifest.BuildTimestamp,
                manifest.QtVersion,
                manifest.DotNetVersion,
                manifest.QmlModules,
                manifest.ViewModels,
                sortedHashes,
                manifest.NativeLib,
                manifest.ManagedAssembly);
            return NormalizeJson(JsonSerializer.Serialize(json, JsonOptions));
        }

        private static string SerializeModuleManifest(BuildContext context, string outputRoot)
        {
            PackageManifest manifest = new(
                context.Config.Name ?? context.Config.Module.Prefix,
                context.Config.Module.Prefix,
                context.Config.Module.Version,
                ImmutableArray.Create("qml"),
                Directory
                    .EnumerateFiles(Path.Join(outputRoot, "schemas"), "*.schema.json", SearchOption.AllDirectories)
                    .Select(path => ToPortablePath(Path.GetRelativePath(outputRoot, path)))
                    .DefaultIfEmpty("schemas")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static path => path, StringComparer.Ordinal)
                    .ToImmutableArray());

            return NormalizeJson(JsonSerializer.Serialize(manifest, JsonOptions));
        }

        private static ImmutableDictionary<string, string> ComputeFileHashes(string outputRoot)
        {
            if (!Directory.Exists(outputRoot))
            {
                return ImmutableDictionary<string, string>.Empty;
            }

            ImmutableDictionary<string, string>.Builder builder =
                ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            foreach (string filePath in Directory
                .EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
                .OrderBy(static path => path, StringComparer.Ordinal))
            {
                string relativePath = ToPortablePath(Path.GetRelativePath(outputRoot, filePath));
                if (StringComparer.Ordinal.Equals(relativePath, ManifestFileName))
                {
                    continue;
                }

                using FileStream stream = File.OpenRead(filePath);
                builder[relativePath] = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            }

            return builder.ToImmutable();
        }

        private static SchemaMetadata ReadSchemaMetadata(string outputRoot)
        {
            string schemasRoot = Path.Join(outputRoot, "schemas");
            if (!Directory.Exists(schemasRoot))
            {
                return new SchemaMetadata(ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
            }

            ImmutableArray<string>.Builder modules = ImmutableArray.CreateBuilder<string>();
            ImmutableArray<string>.Builder viewModels = ImmutableArray.CreateBuilder<string>();
            foreach (string schemaPath in Directory
                .EnumerateFiles(schemasRoot, "*.schema.json", SearchOption.AllDirectories)
                .OrderBy(static path => path, StringComparer.Ordinal))
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(schemaPath));
                    JsonElement root = document.RootElement;
                    if (TryReadString(root, "moduleUri", out string? moduleUri))
                    {
                        modules.Add(moduleUri!);
                    }

                    if (TryReadString(root, "className", out string? className))
                    {
                        viewModels.Add(className!);
                    }
                }
                catch (JsonException)
                {
                    string fileName = Path.GetFileName(schemaPath);
                    viewModels.Add(fileName.EndsWith(".schema.json", StringComparison.Ordinal)
                        ? fileName[..^".schema.json".Length]
                        : Path.GetFileNameWithoutExtension(schemaPath));
                }
            }

            return new SchemaMetadata(
                modules
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static value => value, StringComparer.Ordinal)
                    .ToImmutableArray(),
                viewModels
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static value => value, StringComparer.Ordinal)
                    .ToImmutableArray());
        }

        private static BuildArtifacts DiscoverOutputArtifacts(string outputRoot)
        {
            string qmlRoot = Path.Join(outputRoot, "qml");
            string schemasRoot = Path.Join(outputRoot, "schemas");
            string sourceMapsRoot = Path.Join(outputRoot, "source-maps");
            string assetsRoot = Path.Join(outputRoot, "assets");
            string managedRoot = Path.Join(outputRoot, "managed");
            string nativePath = Path.Join(outputRoot, "native", NativeLibraryNames.GetFileName(NativeTargetName));

            return new BuildArtifacts
            {
                QmlFiles = EnumerateFiles(qmlRoot, "*.qml"),
                SchemaFiles = EnumerateFiles(schemasRoot, "*.schema.json"),
                EventBindingsFile = File.Exists(Path.Join(outputRoot, EventBindingsFileName))
                    ? Path.Join(outputRoot, EventBindingsFileName)
                    : null,
                SourceMapFiles = EnumerateFiles(sourceMapsRoot, "*.qml.map"),
                ModuleMetadataFiles = EnumerateModuleMetadataFiles(qmlRoot),
                AssetFiles = EnumerateFiles(assetsRoot, "*"),
                NativeLibraryPath = File.Exists(nativePath) ? nativePath : null,
                AssemblyPath = Directory.Exists(managedRoot)
                    ? Directory
                        .EnumerateFiles(managedRoot, "*.dll", SearchOption.TopDirectoryOnly)
                        .OrderBy(static path => path, StringComparer.Ordinal)
                        .FirstOrDefault()
                    : null,
            };
        }

        private static ImmutableArray<string> EnumerateModuleMetadataFiles(string qmlRoot)
        {
            if (!Directory.Exists(qmlRoot))
            {
                return ImmutableArray<string>.Empty;
            }

            return Directory
                .EnumerateFiles(qmlRoot, "*", SearchOption.AllDirectories)
                .Where(static path =>
                    StringComparer.Ordinal.Equals(Path.GetFileName(path), "qmldir") ||
                    path.EndsWith(".qmltypes", StringComparison.Ordinal))
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static ImmutableArray<BuildDiagnostic> ValidateOutputInternal(string outputRoot)
        {
            if (File.Exists(Path.Join(outputRoot, ModuleManifestFileName)) &&
                !File.Exists(Path.Join(outputRoot, ManifestFileName)))
            {
                return ValidateLibraryOutput(outputRoot);
            }

            return ValidateApplicationOutput(outputRoot);
        }

        private static ImmutableArray<BuildDiagnostic> ValidateApplicationOutput(string outputRoot)
        {
            ImmutableArray<BuildDiagnostic>.Builder diagnostics =
                ImmutableArray.CreateBuilder<BuildDiagnostic>();
            AddMissingDirectoryDiagnostics(outputRoot, diagnostics, "qml", "schemas", "native", "managed", "source-maps", "assets");

            string manifestPath = Path.Join(outputRoot, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.OutputAssemblyFailed,
                    "Output assembly failed because manifest.json is missing.",
                    manifestPath));
            }

            if (!HasAnyFile(Path.Join(outputRoot, "qml"), "*.qml"))
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.OutputAssemblyFailed,
                    "Output assembly failed because no QML files were found.",
                    Path.Join(outputRoot, "qml")));
            }

            if (!HasAnyFile(Path.Join(outputRoot, "schemas"), "*.schema.json"))
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.OutputAssemblyFailed,
                    "Output assembly failed because no schema files were found.",
                    Path.Join(outputRoot, "schemas")));
            }

            string nativePath = Path.Join(outputRoot, "native", NativeLibraryNames.GetFileName(NativeTargetName));
            if (!File.Exists(nativePath))
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.OutputValidationFailed,
                    "Output validation failed because the native library is missing.",
                    nativePath));
            }

            if (!HasAnyFile(Path.Join(outputRoot, "managed"), "*.dll"))
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.OutputAssemblyFailed,
                    "Output assembly failed because no managed assembly was found.",
                    Path.Join(outputRoot, "managed")));
            }

            return diagnostics.ToImmutable();
        }

        private static ImmutableArray<BuildDiagnostic> ValidateLibraryOutput(string outputRoot)
        {
            ImmutableArray<BuildDiagnostic>.Builder diagnostics =
                ImmutableArray.CreateBuilder<BuildDiagnostic>();
            AddMissingDirectoryDiagnostics(outputRoot, diagnostics, "qml", "schemas");

            if (!File.Exists(Path.Join(outputRoot, ModuleManifestFileName)))
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.OutputAssemblyFailed,
                    "Output assembly failed because qmlsharp.module.json is missing.",
                    Path.Join(outputRoot, ModuleManifestFileName)));
            }

            if (File.Exists(Path.Join(outputRoot, ManifestFileName)))
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.OutputAssemblyFailed,
                    "Library output must not contain application manifest.json.",
                    Path.Join(outputRoot, ManifestFileName)));
            }

            if (File.Exists(Path.Join(outputRoot, EventBindingsFileName)))
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.OutputAssemblyFailed,
                    "Library output must not contain application event-bindings.json.",
                    Path.Join(outputRoot, EventBindingsFileName)));
            }

            if (!HasAnyFile(Path.Join(outputRoot, "qml"), "*"))
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.OutputAssemblyFailed,
                    "Output assembly failed because no library QML files were found.",
                    Path.Join(outputRoot, "qml")));
            }

            if (!HasAnyFile(Path.Join(outputRoot, "schemas"), "*.schema.json"))
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.OutputAssemblyFailed,
                    "Output assembly failed because no library schema files were found.",
                    Path.Join(outputRoot, "schemas")));
            }

            if (HasAnyFile(Path.Join(outputRoot, "native"), "*") ||
                HasAnyFile(Path.Join(outputRoot, "managed"), "*"))
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.OutputAssemblyFailed,
                    "Library output must not contain native or managed application output.",
                    outputRoot));
            }

            return diagnostics.ToImmutable();
        }

        private static void AddMissingDirectoryDiagnostics(
            string outputRoot,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            params string[] relativeDirectories)
        {
            foreach (string relativeDirectory in relativeDirectories)
            {
                string directory = Path.Join(outputRoot, relativeDirectory);
                if (!Directory.Exists(directory))
                {
                    diagnostics.Add(CreateDiagnostic(
                        BuildDiagnosticCode.OutputAssemblyFailed,
                        $"Output assembly failed because '{ToPortablePath(relativeDirectory)}' is missing.",
                        directory));
                }
            }
        }

        private static string ResolveNativeLibRelativePath(string outputRoot, BuildArtifacts artifacts)
        {
            string discoveredPath = File.Exists(Path.Join(outputRoot, "native", NativeLibraryNames.GetFileName(NativeTargetName)))
                ? Path.Join(outputRoot, "native", NativeLibraryNames.GetFileName(NativeTargetName))
                : Path.Join(outputRoot, "native", Path.GetFileName(artifacts.NativeLibraryPath ?? NativeLibraryNames.GetFileName(NativeTargetName)));

            return ToPortablePath(Path.GetRelativePath(outputRoot, discoveredPath));
        }

        private static string ResolveManagedAssemblyRelativePath(
            string outputRoot,
            BuildArtifacts artifacts,
            BuildContext context)
        {
            string managedRoot = Path.Join(outputRoot, "managed");
            string projectAssemblyPath = Path.Join(managedRoot, ProjectName(context) + ".dll");
            string? artifactAssemblyPath = ResolveArtifactManagedAssemblyPath(managedRoot, artifacts.AssemblyPath);
            string fallback = Path.Join(managedRoot, Path.GetFileName(artifacts.AssemblyPath ?? projectAssemblyPath));
            string managedAssembly = artifactAssemblyPath ??
                (File.Exists(projectAssemblyPath) ? projectAssemblyPath : fallback);
            return ToPortablePath(Path.GetRelativePath(outputRoot, managedAssembly));
        }

        private static string? ResolveArtifactManagedAssemblyPath(string managedRoot, string? assemblyPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                return null;
            }

            string candidate = Path.Join(managedRoot, Path.GetFileName(assemblyPath));
            return File.Exists(candidate) ? candidate : null;
        }

        private static string GetApplicationRoot(BuildContext context)
        {
            return Path.GetFullPath(context.OutputDir);
        }

        private static string GetLibraryRoot(BuildContext context)
        {
            return Path.GetFullPath(Path.Join(context.ProjectDir, "lib"));
        }

        private static string ProjectName(BuildContext context)
        {
            return context.Config.Name ?? context.Config.Module.Prefix;
        }

        private static string ResolveQtVersion(string qtDir)
        {
            string[] segments = qtDir.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            return segments
                .Where(static segment => Version.TryParse(segment, out _))
                .Select(static segment => Version.Parse(segment).ToString())
                .FirstOrDefault() ?? "unknown";
        }

        private static string GetQmlRelativePath(BuildContext context, string sourcePath)
        {
            string? relativePath = TryGetRelativePathAfterSegment(sourcePath, "qml");
            if (relativePath is not null)
            {
                return relativePath;
            }

            return Path.Join(ModuleMetadataPaths.GetModuleRelativeDirectory(context.Config.Module.Prefix), Path.GetFileName(sourcePath));
        }

        private static string GetSchemaRelativePath(string sourcePath)
        {
            return TryGetRelativePathAfterSegment(sourcePath, "schemas") ?? Path.GetFileName(sourcePath);
        }

        private static string GetSourceMapRelativePath(string sourcePath)
        {
            return TryGetRelativePathAfterSegment(sourcePath, "source-maps") ?? Path.GetFileName(sourcePath);
        }

        private static string GetAssetRelativePath(string sourcePath)
        {
            return TryGetRelativePathAfterSegment(sourcePath, "assets") ?? Path.GetFileName(sourcePath);
        }

        private static string? TryGetRelativePathAfterSegment(string sourcePath, string segmentName)
        {
            string fullPath = Path.GetFullPath(sourcePath);
            char[] separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            string[] segments = fullPath.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            for (int index = segments.Length - 2; index >= 0; index--)
            {
                if (!string.Equals(segments[index], segmentName, PathComparison))
                {
                    continue;
                }

                return Path.Join(segments[(index + 1)..]);
            }

            return null;
        }

        private static ImmutableArray<string> NormalizePaths(ImmutableArray<string> paths)
        {
            return (paths.IsDefault ? ImmutableArray<string>.Empty : paths)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Select(static path => Path.GetFullPath(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static ImmutableArray<string> EnumerateFiles(string root, string pattern)
        {
            if (!Directory.Exists(root))
            {
                return ImmutableArray<string>.Empty;
            }

            return Directory
                .EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static bool HasAnyFile(string directory, string searchPattern)
        {
            return Directory.Exists(directory) &&
                Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories).Any();
        }

        private static bool TryReadString(JsonElement element, string propertyName, out string? value)
        {
            if (element.TryGetProperty(propertyName, out JsonElement property) &&
                property.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(property.GetString()))
            {
                value = property.GetString();
                return true;
            }

            value = null;
            return false;
        }

        private static void AddOutputFile(
            string outputPath,
            ImmutableArray<string>.Builder outputFiles,
            ref long totalBytes)
        {
            if (File.Exists(outputPath))
            {
                outputFiles.Add(Path.GetFullPath(outputPath));
                totalBytes += new FileInfo(outputPath).Length;
            }
        }

        private static bool IsDevelopmentMode(string buildMode)
        {
            return string.Equals(buildMode, "development", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSamePath(string left, string right)
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), PathComparison);
        }

        private static string ToPortablePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string NormalizeJson(string json)
        {
            string normalized = json.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            return normalized.EndsWith("\n", StringComparison.Ordinal) ? normalized : normalized + "\n";
        }

        private static BuildDiagnostic CreateDiagnostic(string code, string message, string? filePath)
        {
            return new BuildDiagnostic(
                code,
                BuildDiagnosticSeverity.Error,
                message,
                BuildPhase.OutputAssembly,
                filePath);
        }

        private static bool IsBlockingDiagnostic(BuildDiagnostic diagnostic)
        {
            return diagnostic.Severity is BuildDiagnosticSeverity.Error or BuildDiagnosticSeverity.Fatal;
        }

        private static StringComparison PathComparison =>
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        private sealed record SchemaMetadata(
            ImmutableArray<string> Modules,
            ImmutableArray<string> ViewModels);

        private sealed record ProductManifestJson(
            string ProjectName,
            string Version,
            string BuildMode,
            DateTimeOffset BuildTimestamp,
            string QtVersion,
            string DotNetVersion,
            ImmutableArray<string> QmlModules,
            ImmutableArray<string> ViewModels,
            IReadOnlyDictionary<string, string> FileHashes,
            string NativeLib,
            string ManagedAssembly);
    }
}
