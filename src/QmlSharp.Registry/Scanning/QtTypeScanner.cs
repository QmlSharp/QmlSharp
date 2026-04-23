using QmlSharp.Registry.Diagnostics;

namespace QmlSharp.Registry.Scanning
{
    public sealed class QtTypeScanner : IQtTypeScanner
    {
        private const string QmlDirectoryName = "qml";
        private const string QmldirFileName = "qmldir";

        public ScanResult Scan(ScannerConfig config)
        {
            if (config is null)
            {
                return CreateInvalidQtDirResult("Scanner configuration is required.", null);
            }

            ScanValidation validation = ValidateQtDir(config.QtDir);

            if (!validation.IsValid)
            {
                string? invalidPath = string.IsNullOrWhiteSpace(config.QtDir)
                    ? null
                    : TryNormalizeAbsolutePath(config.QtDir, out string? normalizedQtDir)
                        ? normalizedQtDir
                        : config.QtDir;

                return CreateInvalidQtDirResult(
                    validation.ErrorMessage ?? "Qt SDK directory is invalid.",
                    invalidPath);
            }

            string qtDir = NormalizeAbsolutePath(config.QtDir);
            string qmlRootDir = Path.Join(qtDir, QmlDirectoryName);
            string metatypesRootDir = GetMetatypesRootDirectory(qtDir);
            ImmutableArray<string> moduleFilter = NormalizeModuleFilter(config.ModuleFilter);

            (ImmutableArray<string> qmltypesPaths, ImmutableArray<string> qmldirPaths) = ScanQmlMetadataPaths(
                qmlRootDir,
                moduleFilter,
                config.IncludeInternal);
            ImmutableArray<string> metatypesPaths = ScanMetatypesPaths(
                metatypesRootDir,
                moduleFilter,
                config.IncludeInternal);
            ImmutableArray<RegistryDiagnostic> diagnostics = CreateScanDiagnostics(qmlRootDir, metatypesRootDir, qmltypesPaths, qmldirPaths, metatypesPaths);

            return new ScanResult(
                QmltypesPaths: qmltypesPaths,
                QmldirPaths: qmldirPaths,
                MetatypesPaths: metatypesPaths,
                Diagnostics: diagnostics);
        }

        public ScanValidation ValidateQtDir(string qtDir)
        {
            if (string.IsNullOrWhiteSpace(qtDir))
            {
                return new ScanValidation(
                    IsValid: false,
                    QtVersion: null,
                    ErrorMessage: "Qt SDK directory path is required.");
            }

            if (!TryNormalizeAbsolutePath(qtDir, out string? normalizedQtDir))
            {
                return new ScanValidation(
                    IsValid: false,
                    QtVersion: null,
                    ErrorMessage: $"Qt SDK directory '{qtDir}' is not a valid path.");
            }

            if (!Directory.Exists(normalizedQtDir))
            {
                return new ScanValidation(
                    IsValid: false,
                    QtVersion: null,
                    ErrorMessage: $"Qt SDK directory '{normalizedQtDir}' does not exist.");
            }

            string qmlRootDir = Path.Join(normalizedQtDir, QmlDirectoryName);
            if (!Directory.Exists(qmlRootDir))
            {
                return new ScanValidation(
                    IsValid: false,
                    QtVersion: null,
                    ErrorMessage: $"Qt SDK directory '{normalizedQtDir}' is missing the '{QmlDirectoryName}' directory.");
            }

            string metatypesRootDir = GetMetatypesRootDirectory(normalizedQtDir);
            if (string.IsNullOrEmpty(metatypesRootDir))
            {
                return new ScanValidation(
                    IsValid: false,
                    QtVersion: null,
                    ErrorMessage: $"Qt SDK directory '{normalizedQtDir}' is missing the 'lib{Path.DirectorySeparatorChar}metatypes' or 'metatypes' directory.");
            }

            return new ScanValidation(
                IsValid: true,
                QtVersion: DetectQtVersion(normalizedQtDir),
                ErrorMessage: null);
        }

        public string? InferModuleUri(string qmldirPath, string qmlRootDir)
        {
            if (string.IsNullOrWhiteSpace(qmldirPath) || string.IsNullOrWhiteSpace(qmlRootDir))
            {
                return null;
            }

            string[] qmldirSegments = SplitPathSegments(qmldirPath);
            string[] rootSegments = SplitPathSegments(qmlRootDir);

            if (qmldirSegments.Length == 0 || rootSegments.Length == 0)
            {
                return null;
            }

            if (!string.Equals(qmldirSegments[^1], QmldirFileName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            int rootIndex = FindPathSegmentSequence(qmldirSegments, rootSegments);
            if (rootIndex < 0)
            {
                return null;
            }

            int moduleStartIndex = rootIndex + rootSegments.Length;
            int moduleSegmentCount = qmldirSegments.Length - moduleStartIndex - 1;

            if (moduleSegmentCount <= 0)
            {
                return null;
            }

            return string.Join('.', qmldirSegments.Skip(moduleStartIndex).Take(moduleSegmentCount));
        }

        private static string DetectQtVersion(string qtDir)
        {
            string[] segments = SplitPathSegments(qtDir);
            return segments
                .Reverse()
                .Where(segment => Version.TryParse(segment, out _))
                .FirstOrDefault() ?? "unknown";
        }

        private static int FindPathSegmentSequence(IReadOnlyList<string> haystack, IReadOnlyList<string> needle)
        {
            for (int start = 0; start <= haystack.Count - needle.Count; start++)
            {
                bool matched = true;

                for (int index = 0; index < needle.Count; index++)
                {
                    if (!string.Equals(haystack[start + index], needle[index], StringComparison.OrdinalIgnoreCase))
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return start;
                }
            }

            return -1;
        }

        private static string GetMetatypesRootDirectory(string qtDir)
        {
            string legacyPath = Path.Join(qtDir, "lib", "metatypes");
            if (Directory.Exists(legacyPath))
            {
                return legacyPath;
            }

            string currentPath = Path.Join(qtDir, "metatypes");
            if (Directory.Exists(currentPath))
            {
                return currentPath;
            }

            return string.Empty;
        }

        private static bool IsInternalModule(string moduleUri)
        {
            return moduleUri
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(segment => string.Equals(segment, "private", StringComparison.OrdinalIgnoreCase));
        }

        private static bool MatchesModuleFilter(string moduleUri, ImmutableArray<string> moduleFilter)
        {
            if (moduleFilter.IsDefaultOrEmpty)
            {
                return true;
            }

            return moduleFilter.Any(filter =>
                string.Equals(moduleUri, filter, StringComparison.OrdinalIgnoreCase)
                || moduleUri.StartsWith($"{filter}.", StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeAbsolutePath(string path)
        {
            return TryNormalizeAbsolutePath(path, out string? normalizedPath)
                ? normalizedPath
                : path;
        }

        private static ImmutableArray<string> NormalizeModuleFilter(ImmutableArray<string>? moduleFilter)
        {
            if (!moduleFilter.HasValue || moduleFilter.Value.IsDefaultOrEmpty)
            {
                return ImmutableArray<string>.Empty;
            }

            return moduleFilter.Value
                .Where(filter => !string.IsNullOrWhiteSpace(filter))
                .Select(filter => filter.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray();
        }

        private static string NormalizeModuleToken(string value)
        {
            return new string(value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private static string NormalizeMetatypesModuleToken(string value)
        {
            string normalizedValue = NormalizeModuleToken(value);
            if (!normalizedValue.StartsWith("qt", StringComparison.Ordinal) || normalizedValue.Length <= 2)
            {
                return normalizedValue;
            }

            int index = 2;
            while (index < normalizedValue.Length && char.IsDigit(normalizedValue[index]))
            {
                index++;
            }

            return index > 2
                ? string.Concat("qt", normalizedValue.AsSpan(index))
                : normalizedValue;
        }

        private static bool TryNormalizeAbsolutePath(string path, out string normalizedPath)
        {
            try
            {
                normalizedPath = Path.GetFullPath(NormalizePathSeparators(path));
                return true;
            }
            catch (ArgumentException)
            {
                normalizedPath = path;
                return false;
            }
            catch (NotSupportedException)
            {
                normalizedPath = path;
                return false;
            }
            catch (PathTooLongException)
            {
                normalizedPath = path;
                return false;
            }
        }

        private static string NormalizePathSeparators(string path)
        {
            return path
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
        }

        private static ScanResult CreateInvalidQtDirResult(string message, string? filePath)
        {
            return new ScanResult(
                QmltypesPaths: ImmutableArray<string>.Empty,
                QmldirPaths: ImmutableArray<string>.Empty,
                MetatypesPaths: ImmutableArray<string>.Empty,
                Diagnostics:
                [
                    new RegistryDiagnostic(
                        DiagnosticSeverity.Error,
                        DiagnosticCodes.InvalidQtDir,
                        message,
                        filePath,
                        null,
                        null),
                ]);
        }

        private static ImmutableArray<RegistryDiagnostic> CreateScanDiagnostics(
            string qmlRootDir,
            string metatypesRootDir,
            ImmutableArray<string> qmltypesPaths,
            ImmutableArray<string> qmldirPaths,
            ImmutableArray<string> metatypesPaths)
        {
            List<RegistryDiagnostic> diagnostics = [];

            AddMissingFileDiagnostic(
                diagnostics,
                qmltypesPaths,
                DiagnosticCodes.NoQmltypesFound,
                "No .qmltypes files matched the current scan filters under the Qt qml directory.",
                qmlRootDir);
            AddMissingFileDiagnostic(
                diagnostics,
                qmldirPaths,
                DiagnosticCodes.NoQmldirFound,
                "No qmldir files matched the current scan filters under the Qt qml directory.",
                qmlRootDir);
            AddMissingFileDiagnostic(
                diagnostics,
                metatypesPaths,
                DiagnosticCodes.NoMetatypesFound,
                "No *_metatypes.json files matched the current scan filters under the Qt metatypes directory.",
                metatypesRootDir);

            return diagnostics.ToImmutableArray();
        }

        private static void AddMissingFileDiagnostic(
            ICollection<RegistryDiagnostic> diagnostics,
            ImmutableArray<string> paths,
            string code,
            string message,
            string directoryPath)
        {
            if (!paths.IsEmpty)
            {
                return;
            }

            diagnostics.Add(new RegistryDiagnostic(
                DiagnosticSeverity.Warning,
                code,
                message,
                NormalizeAbsolutePath(directoryPath),
                null,
                null));
        }

        private static ImmutableArray<string> ScanMetatypesPaths(string metatypesRootDir, ImmutableArray<string> moduleFilter, bool includeInternal)
        {
            return Directory
                .EnumerateFiles(metatypesRootDir, "*_metatypes.json", SearchOption.AllDirectories)
                .Where(path => ShouldIncludeMetatypesPath(path, moduleFilter, includeInternal))
                .Select(NormalizeAbsolutePath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static (ImmutableArray<string> QmltypesPaths, ImmutableArray<string> QmldirPaths) ScanQmlMetadataPaths(
            string qmlRootDir,
            ImmutableArray<string> moduleFilter,
            bool includeInternal)
        {
            string[] qmlMetadataPaths = Directory
                .EnumerateFiles(qmlRootDir, "*", SearchOption.AllDirectories)
                .Where(IsQmlMetadataPath)
                .Where(path => ShouldIncludeQmlModulePath(path, qmlRootDir, moduleFilter, includeInternal))
                .Select(NormalizeAbsolutePath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            return (
                qmlMetadataPaths.Where(IsQmltypesPath).ToImmutableArray(),
                qmlMetadataPaths.Where(IsQmldirPath).ToImmutableArray());
        }

        private static bool ShouldIncludeMetatypesPath(string path, ImmutableArray<string> moduleFilter, bool includeInternal)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            string moduleToken = fileName.EndsWith("_metatypes", StringComparison.OrdinalIgnoreCase)
                ? fileName[..^"_metatypes".Length]
                : fileName;
            string normalizedModuleToken = NormalizeMetatypesModuleToken(moduleToken);

            bool isInternal = normalizedModuleToken.Contains("private", StringComparison.OrdinalIgnoreCase);
            if (!includeInternal && isInternal)
            {
                return false;
            }

            if (moduleFilter.IsDefaultOrEmpty)
            {
                return true;
            }

            return moduleFilter.Any(filter =>
            {
                string normalizedFilter = NormalizeMetatypesModuleToken(filter);
                return string.Equals(normalizedModuleToken, normalizedFilter, StringComparison.Ordinal)
                    || normalizedModuleToken.StartsWith(normalizedFilter, StringComparison.Ordinal);
            });
        }

        private static bool ShouldIncludeQmlModulePath(string path, string qmlRootDir, ImmutableArray<string> moduleFilter, bool includeInternal)
        {
            string? moduleUri = InferModuleUriFromPath(path, qmlRootDir);
            if (moduleUri is null)
            {
                return false;
            }

            if (!includeInternal && IsInternalModule(moduleUri))
            {
                return false;
            }

            return MatchesModuleFilter(moduleUri, moduleFilter);
        }

        private static bool IsQmlMetadataPath(string path)
        {
            return IsQmltypesPath(path) || IsQmldirPath(path);
        }

        private static bool IsQmltypesPath(string path)
        {
            return path.EndsWith(".qmltypes", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsQmldirPath(string path)
        {
            return string.Equals(Path.GetFileName(path), QmldirFileName, StringComparison.OrdinalIgnoreCase);
        }

        private static string[] SplitPathSegments(string path)
        {
            return path
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static string? InferModuleUriFromPath(string path, string qmlRootDir)
        {
            string directory = Path.GetDirectoryName(NormalizeAbsolutePath(path)) ?? string.Empty;
            string[] directorySegments = SplitPathSegments(directory);
            string[] rootSegments = SplitPathSegments(NormalizeAbsolutePath(qmlRootDir));
            int rootIndex = FindPathSegmentSequence(directorySegments, rootSegments);

            if (rootIndex < 0)
            {
                return null;
            }

            string[] moduleSegments = directorySegments[(rootIndex + rootSegments.Length)..];
            if (moduleSegments.Length == 0)
            {
                return null;
            }

            return string.Join('.', moduleSegments);
        }
    }
}
