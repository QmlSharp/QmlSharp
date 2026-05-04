using System.Globalization;
using System.Text.Json;

namespace QmlSharp.Build
{
    /// <summary>Filesystem-backed NuGet package resolver for QmlSharp packages.</summary>
    public sealed class PackageResolver : IPackageResolver
    {
        private const string ManifestFileName = "qmlsharp.module.json";

        /// <inheritdoc />
        public ImmutableArray<ResolvedPackage> Resolve(string projectDir)
        {
            return ResolveWithDiagnostics(projectDir).Packages;
        }

        /// <summary>Scans NuGet package locations and returns diagnostics alongside resolved packages.</summary>
        public PackageResolutionResult ResolveWithDiagnostics(string projectDir)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectDir);

            ImmutableArray<PackageCandidate>.Builder candidates =
                ImmutableArray.CreateBuilder<PackageCandidate>();
            ImmutableArray<BuildDiagnostic>.Builder diagnostics =
                ImmutableArray.CreateBuilder<BuildDiagnostic>();

            string normalizedProjectDir;
            try
            {
                normalizedProjectDir = Path.GetFullPath(projectDir);
            }
            catch (ArgumentException exception)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.PackageResolutionFailed,
                    $"Package resolution failed: {exception.Message}",
                    projectDir));
                return new PackageResolutionResult(ImmutableArray<ResolvedPackage>.Empty, diagnostics.ToImmutable());
            }
            catch (NotSupportedException exception)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.PackageResolutionFailed,
                    $"Package resolution failed: {exception.Message}",
                    projectDir));
                return new PackageResolutionResult(ImmutableArray<ResolvedPackage>.Empty, diagnostics.ToImmutable());
            }
            catch (PathTooLongException exception)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.PackageResolutionFailed,
                    $"Package resolution failed: {exception.Message}",
                    projectDir));
                return new PackageResolutionResult(ImmutableArray<ResolvedPackage>.Empty, diagnostics.ToImmutable());
            }

            if (!Directory.Exists(normalizedProjectDir))
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.PackageResolutionFailed,
                    $"Package resolution failed: project directory '{normalizedProjectDir}' does not exist.",
                    normalizedProjectDir));
                return new PackageResolutionResult(ImmutableArray<ResolvedPackage>.Empty, diagnostics.ToImmutable());
            }

            AddProjectAssetsCandidates(normalizedProjectDir, candidates, diagnostics);
            AddDirectPackageCandidates(normalizedProjectDir, candidates, diagnostics);

            ImmutableArray<ResolvedPackage> packages = ResolveCandidates(
                candidates.ToImmutable(),
                diagnostics);
            return new PackageResolutionResult(packages, diagnostics.ToImmutable());
        }

        /// <inheritdoc />
        public ImmutableArray<string> CollectImportPaths(ImmutableArray<ResolvedPackage> packages)
        {
            return packages
                .Where(static package => package.Manifest is not null)
                .SelectMany(static package => package.Manifest!.QmlImportPaths.Select(path =>
                    ResolvePackagePath(package.PackagePath, path)))
                .Where(Directory.Exists)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        /// <inheritdoc />
        public ImmutableArray<string> CollectSchemas(ImmutableArray<ResolvedPackage> packages)
        {
            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
            foreach (ResolvedPackage package in packages.Where(static package => package.Manifest is not null))
            {
                foreach (string schemaPath in package.Manifest!.SchemaFiles)
                {
                    string resolvedPath = ResolvePackagePath(package.PackagePath, schemaPath);
                    if (File.Exists(resolvedPath))
                    {
                        builder.Add(resolvedPath);
                    }
                    else if (Directory.Exists(resolvedPath))
                    {
                        builder.AddRange(Directory
                            .EnumerateFiles(resolvedPath, "*.schema.json", SearchOption.AllDirectories)
                            .OrderBy(static path => path, StringComparer.Ordinal));
                    }
                }
            }

            return builder
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static void AddProjectAssetsCandidates(
            string projectDir,
            ImmutableArray<PackageCandidate>.Builder candidates,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics)
        {
            string assetsPath = Path.Join(projectDir, "obj", "project.assets.json");
            if (!File.Exists(assetsPath))
            {
                return;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(assetsPath));
                JsonElement root = document.RootElement;
                ImmutableArray<string> packageFolders = ReadPackageFolders(root);
                if (!root.TryGetProperty("libraries", out JsonElement libraries) ||
                    libraries.ValueKind != JsonValueKind.Object)
                {
                    return;
                }

                AddLibraryCandidates(libraries, packageFolders, candidates);
            }
            catch (JsonException exception)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.PackageResolutionFailed,
                    $"Package resolution failed while parsing project.assets.json: {exception.Message}",
                    assetsPath));
            }
            catch (IOException exception)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.PackageResolutionFailed,
                    $"Package resolution failed while reading project.assets.json: {exception.Message}",
                    assetsPath));
            }
            catch (UnauthorizedAccessException exception)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.PackageResolutionFailed,
                    $"Package resolution failed while reading project.assets.json: {exception.Message}",
                    assetsPath));
            }
        }

        private static void AddLibraryCandidates(
            JsonElement libraries,
            ImmutableArray<string> packageFolders,
            ImmutableArray<PackageCandidate>.Builder candidates)
        {
            foreach (JsonProperty library in libraries.EnumerateObject().OrderBy(
                static property => property.Name,
                StringComparer.Ordinal))
            {
                if (!TryGetPackageLibrary(library, out string packageId, out string version))
                {
                    continue;
                }

                string packageRelativePath = ReadPackageRelativePath(library.Value, packageId, version);
                foreach (string packageFolder in packageFolders)
                {
                    string packagePath = Path.GetFullPath(Path.Join(packageFolder, packageRelativePath));
                    if (Directory.Exists(packagePath))
                    {
                        candidates.Add(new PackageCandidate(packageId, version, packagePath));
                    }
                }
            }
        }

        private static bool TryGetPackageLibrary(JsonProperty library, out string packageId, out string version)
        {
            if (!TryParseLibraryName(library.Name, out packageId, out version) ||
                !IsQmlSharpPackage(packageId))
            {
                return false;
            }

            return !library.Value.TryGetProperty("type", out JsonElement typeElement) ||
                typeElement.ValueKind != JsonValueKind.String ||
                string.Equals(typeElement.GetString(), "package", StringComparison.OrdinalIgnoreCase);
        }

        private static ImmutableArray<string> ReadPackageFolders(JsonElement root)
        {
            if (!root.TryGetProperty("packageFolders", out JsonElement packageFoldersElement) ||
                packageFoldersElement.ValueKind != JsonValueKind.Object)
            {
                return ImmutableArray<string>.Empty;
            }

            return packageFoldersElement
                .EnumerateObject()
                .Select(static property => Path.GetFullPath(property.Name))
                .Where(Directory.Exists)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static string ReadPackageRelativePath(JsonElement library, string packageId, string version)
        {
            if (library.TryGetProperty("path", out JsonElement pathElement) &&
                pathElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(pathElement.GetString()))
            {
                return pathElement.GetString()!;
            }

            return string.Create(
                CultureInfo.InvariantCulture,
                $"{packageId.ToLowerInvariant()}/{version.ToLowerInvariant()}");
        }

        private static bool TryParseLibraryName(string libraryName, out string packageId, out string version)
        {
            int separator = libraryName.LastIndexOf('/');
            if (separator <= 0 || separator == libraryName.Length - 1)
            {
                packageId = string.Empty;
                version = string.Empty;
                return false;
            }

            packageId = libraryName[..separator];
            version = libraryName[(separator + 1)..];
            return true;
        }

        private static void AddDirectPackageCandidates(
            string projectDir,
            ImmutableArray<PackageCandidate>.Builder candidates,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics)
        {
            foreach (string packageRoot in GetPackageSearchRoots(projectDir))
            {
                if (!Directory.Exists(packageRoot))
                {
                    continue;
                }

                try
                {
                    AddDirectPackageCandidatesFromRoot(packageRoot, candidates);
                }
                catch (IOException exception)
                {
                    diagnostics.Add(CreateDiagnostic(
                        BuildDiagnosticCode.PackageResolutionFailed,
                        $"Package resolution failed while scanning '{packageRoot}': {exception.Message}",
                        packageRoot));
                }
                catch (UnauthorizedAccessException exception)
                {
                    diagnostics.Add(CreateDiagnostic(
                        BuildDiagnosticCode.PackageResolutionFailed,
                        $"Package resolution failed while scanning '{packageRoot}': {exception.Message}",
                        packageRoot));
                }
            }
        }

        private static ImmutableArray<string> GetPackageSearchRoots(string projectDir)
        {
            ImmutableArray<string>.Builder roots = ImmutableArray.CreateBuilder<string>();
            roots.Add(Path.Join(projectDir, "packages"));
            roots.Add(Path.Join(projectDir, ".nuget", "packages"));
            return roots
                .Select(static path => Path.GetFullPath(path))
                .Distinct(StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static void AddDirectPackageCandidatesFromRoot(
            string packageRoot,
            ImmutableArray<PackageCandidate>.Builder candidates)
        {
            foreach (string packageDirectory in Directory
                .EnumerateDirectories(packageRoot)
                .OrderBy(static path => path, StringComparer.Ordinal))
            {
                string packageId = Path.GetFileName(packageDirectory);
                if (!IsQmlSharpPackage(packageId))
                {
                    continue;
                }

                ImmutableArray<string> versionDirectories = Directory
                    .EnumerateDirectories(packageDirectory)
                    .OrderBy(static path => path, StringComparer.Ordinal)
                    .ToImmutableArray();
                if (versionDirectories.IsDefaultOrEmpty &&
                    File.Exists(Path.Join(packageDirectory, ManifestFileName)))
                {
                    candidates.Add(new PackageCandidate(packageId, "0.0.0", packageDirectory));
                    continue;
                }

                foreach (string versionDirectory in versionDirectories)
                {
                    string version = Path.GetFileName(versionDirectory);
                    candidates.Add(new PackageCandidate(packageId, version, versionDirectory));
                }
            }
        }

        private static ImmutableArray<ResolvedPackage> ResolveCandidates(
            ImmutableArray<PackageCandidate> candidates,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics)
        {
            ImmutableArray<ResolvedPackage>.Builder packages = ImmutableArray.CreateBuilder<ResolvedPackage>();
            HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);
            foreach (PackageCandidate candidate in candidates
                .OrderBy(static candidate => candidate.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static candidate => candidate.Version, StringComparer.Ordinal)
                .ThenBy(static candidate => candidate.PackagePath, StringComparer.OrdinalIgnoreCase))
            {
                string packagePath = Path.GetFullPath(candidate.PackagePath);
                if (!seenPaths.Add(packagePath))
                {
                    continue;
                }

                string manifestPath = Path.Join(packagePath, ManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    packages.Add(new ResolvedPackage(
                        candidate.PackageId,
                        candidate.Version,
                        packagePath,
                        null));
                    continue;
                }

                try
                {
                    PackageManifest manifest = ReadManifest(candidate.PackageId, manifestPath);
                    packages.Add(new ResolvedPackage(
                        candidate.PackageId,
                        candidate.Version,
                        packagePath,
                        manifest));
                }
                catch (JsonException exception)
                {
                    AddManifestParseDiagnostic(diagnostics, manifestPath, exception);
                }
                catch (FormatException exception)
                {
                    AddManifestParseDiagnostic(diagnostics, manifestPath, exception);
                }
                catch (InvalidOperationException exception)
                {
                    AddManifestParseDiagnostic(diagnostics, manifestPath, exception);
                }
                catch (IOException exception)
                {
                    AddManifestParseDiagnostic(diagnostics, manifestPath, exception);
                }
                catch (UnauthorizedAccessException exception)
                {
                    AddManifestParseDiagnostic(diagnostics, manifestPath, exception);
                }
            }

            return packages.ToImmutable();
        }

        private static void AddManifestParseDiagnostic(
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            string manifestPath,
            Exception exception)
        {
            diagnostics.Add(CreateDiagnostic(
                BuildDiagnosticCode.ManifestParseError,
                $"Package manifest '{manifestPath}' could not be parsed: {exception.Message}",
                manifestPath));
        }

        private static PackageManifest ReadManifest(string fallbackPackageId, string manifestPath)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("Manifest root must be a JSON object.");
            }

            string packageId = ReadOptionalString(root, "packageId") ?? fallbackPackageId;
            string moduleUri = ReadRequiredString(root, "moduleUri");
            QmlVersion moduleVersion = ReadQmlVersion(root);
            ImmutableArray<string> qmlImportPaths = ReadStringArray(root, "qmlImportPaths");
            ImmutableArray<string> schemaFiles = ReadStringArray(root, "schemaFiles");

            return new PackageManifest(packageId, moduleUri, moduleVersion, qmlImportPaths, schemaFiles);
        }

        private static string ReadRequiredString(JsonElement root, string propertyName)
        {
            string? value = ReadOptionalString(root, propertyName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new FormatException($"{propertyName} is required and must be a non-empty string.");
            }

            return value;
        }

        private static string? ReadOptionalString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement value) ||
                value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                throw new FormatException($"{propertyName} must be a string.");
            }

            return value.GetString();
        }

        private static QmlVersion ReadQmlVersion(JsonElement root)
        {
            if (!root.TryGetProperty("moduleVersion", out JsonElement versionElement) ||
                versionElement.ValueKind == JsonValueKind.Null)
            {
                return new QmlVersion(1, 0);
            }

            if (versionElement.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("moduleVersion must be an object.");
            }

            int major = ReadOptionalInt(versionElement, "major") ?? 1;
            int minor = ReadOptionalInt(versionElement, "minor") ?? 0;
            if (major < 0 || minor < 0)
            {
                throw new FormatException("moduleVersion values must be non-negative.");
            }

            return new QmlVersion(major, minor);
        }

        private static int? ReadOptionalInt(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement value))
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result))
            {
                throw new FormatException($"{propertyName} must be an integer.");
            }

            return result;
        }

        private static ImmutableArray<string> ReadStringArray(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement value) ||
                value.ValueKind == JsonValueKind.Null)
            {
                return ImmutableArray<string>.Empty;
            }

            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException($"{propertyName} must be an array.");
            }

            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
            int index = 0;
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                {
                    throw new FormatException(
                        string.Create(CultureInfo.InvariantCulture, $"{propertyName}[{index}] must be a non-empty string."));
                }

                builder.Add(NormalizeManifestRelativePath(item.GetString()!));
                index++;
            }

            return builder.ToImmutable();
        }

        private static string NormalizeManifestRelativePath(string path)
        {
            string trimmed = path.Trim();
            if (Path.IsPathRooted(trimmed))
            {
                throw new FormatException("Manifest paths must be relative to the package root.");
            }

            string normalized = trimmed.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            string[] segments = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(static segment => segment is "." or ".."))
            {
                throw new FormatException("Manifest paths must not contain traversal segments.");
            }

            return normalized;
        }

        private static string ResolvePackagePath(string packagePath, string relativePath)
        {
            string normalizedPackagePath = Path.GetFullPath(packagePath);
            string normalizedRelativePath = NormalizeManifestRelativePath(relativePath);
            string resolved = Path.GetFullPath(Path.Join(normalizedPackagePath, normalizedRelativePath));
            if (!IsPathUnderDirectory(resolved, normalizedPackagePath))
            {
                throw new InvalidOperationException("Manifest path resolved outside the package root.");
            }

            return resolved;
        }

        private static bool IsPathUnderDirectory(string path, string directory)
        {
            string normalizedDirectory = Path.GetFullPath(directory);
            if (string.Equals(path, normalizedDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string prefix = normalizedDirectory.EndsWith(Path.DirectorySeparatorChar)
                ? normalizedDirectory
                : normalizedDirectory + Path.DirectorySeparatorChar;
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsQmlSharpPackage(string packageId)
        {
            return packageId.StartsWith("QmlSharp.", StringComparison.OrdinalIgnoreCase);
        }

        private static BuildDiagnostic CreateDiagnostic(string code, string message, string path)
        {
            return new BuildDiagnostic(
                code,
                BuildDiagnosticSeverity.Error,
                message,
                BuildPhase.DependencyResolution,
                path);
        }

        private sealed record PackageCandidate(string PackageId, string Version, string PackagePath);
    }
}
