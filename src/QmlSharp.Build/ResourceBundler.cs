using System.Security;
using System.Text;

namespace QmlSharp.Build
{
    /// <summary>Filesystem-backed resource bundler for application-local assets.</summary>
    public sealed class ResourceBundler : IResourceBundler
    {
        private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

        private static readonly ImmutableHashSet<string> ImageExtensions =
            ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg");

        private static readonly ImmutableHashSet<string> FontExtensions =
            ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, ".ttf", ".otf", ".woff", ".woff2");

        private static readonly ImmutableHashSet<string> IconExtensions =
            ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, ".ico", ".icns");

        private readonly string _projectDir;
        private readonly ImmutableArray<string> _assetRoots;
        private readonly bool _reportMissingRoots;
        private readonly bool _generateQrcOnBundle;

        /// <summary>Create a bundler using the current directory and conventional assets root.</summary>
        public ResourceBundler()
            : this(Directory.GetCurrentDirectory())
        {
        }

        /// <summary>Create a bundler for a project directory using the conventional assets root.</summary>
        public ResourceBundler(string projectDir)
            : this(projectDir, ImmutableArray.Create("assets"), reportMissingRoots: false, generateQrcOnBundle: false)
        {
        }

        /// <summary>Create a bundler with explicit asset roots.</summary>
        public ResourceBundler(
            string projectDir,
            ImmutableArray<string> assetRoots,
            bool reportMissingRoots,
            bool generateQrcOnBundle)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectDir);

            _projectDir = Path.GetFullPath(projectDir);
            _assetRoots = assetRoots.IsDefaultOrEmpty ? ImmutableArray<string>.Empty : assetRoots;
            _reportMissingRoots = reportMissingRoots;
            _generateQrcOnBundle = generateQrcOnBundle;
        }

        /// <inheritdoc />
        public ImmutableArray<ResourceEntry> Collect(QmlSharpConfig config)
        {
            return CollectWithDiagnostics(config).Resources;
        }

        /// <summary>Collects resources and returns diagnostics alongside entries.</summary>
        public ResourceCollectionResult CollectWithDiagnostics(QmlSharpConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            ImmutableArray<ResourceEntry>.Builder resources = ImmutableArray.CreateBuilder<ResourceEntry>();
            ImmutableArray<BuildDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<BuildDiagnostic>();

            foreach (string assetRoot in _assetRoots)
            {
                string normalizedRoot = NormalizeAssetRoot(assetRoot);
                if (!Directory.Exists(normalizedRoot))
                {
                    if (_reportMissingRoots)
                    {
                        diagnostics.Add(CreateDiagnostic(
                            BuildDiagnosticCode.AssetNotFound,
                            $"Asset root '{normalizedRoot}' does not exist.",
                            normalizedRoot));
                    }

                    continue;
                }

                AddResources(normalizedRoot, resources, diagnostics);
            }

            ImmutableArray<ResourceEntry> collectedResources = resources
                .OrderBy(static resource => resource.RelativePath, StringComparer.Ordinal)
                .ToImmutableArray();
            return new ResourceCollectionResult(collectedResources, diagnostics.ToImmutable());
        }

        /// <inheritdoc />
        public ResourceBundleResult Bundle(ImmutableArray<ResourceEntry> resources, string outputDir)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputDir);

            ImmutableArray<string>.Builder outputPaths = ImmutableArray.CreateBuilder<string>();
            ImmutableArray<BuildDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<BuildDiagnostic>();
            string assetsOutputDir = Path.Join(Path.GetFullPath(outputDir), "assets");
            int filesCopied = 0;
            long totalBytes = 0;

            foreach (ResourceEntry resource in resources
                .Where(static resource => !string.IsNullOrWhiteSpace(resource.RelativePath))
                .OrderBy(static resource => resource.RelativePath, StringComparer.Ordinal))
            {
                if (TryCopyResource(resource, assetsOutputDir, diagnostics, out string outputPath))
                {
                    outputPaths.Add(outputPath);
                    filesCopied++;
                    totalBytes += resource.SizeBytes;
                }
            }

            string? qrcPath = WriteQrcIfRequested(resources, assetsOutputDir, outputPaths, diagnostics);

            return new ResourceBundleResult(
                filesCopied,
                totalBytes,
                outputPaths
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static path => path, StringComparer.Ordinal)
                    .ToImmutableArray(),
                qrcPath)
            {
                Diagnostics = diagnostics.ToImmutable(),
            };
        }

        private static bool TryCopyResource(
            ResourceEntry resource,
            string assetsOutputDir,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics,
            out string outputPath)
        {
            outputPath = string.Empty;
            if (!File.Exists(resource.SourcePath))
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.AssetNotFound,
                    $"Asset '{resource.SourcePath}' does not exist.",
                    resource.SourcePath));
                return false;
            }

            try
            {
                outputPath = ResolveOutputPath(assetsOutputDir, resource.RelativePath);
                string? outputDirectory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(outputDirectory))
                {
                    _ = Directory.CreateDirectory(outputDirectory);
                }

                File.Copy(resource.SourcePath, outputPath, overwrite: true);
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.AssetCopyFailed,
                    $"Asset '{resource.SourcePath}' could not be copied to '{outputPath}': {exception.Message}",
                    resource.SourcePath));
                return false;
            }
        }

        private string? WriteQrcIfRequested(
            ImmutableArray<ResourceEntry> resources,
            string assetsOutputDir,
            ImmutableArray<string>.Builder outputPaths,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics)
        {
            if (!_generateQrcOnBundle)
            {
                return null;
            }

            string qrcPath = Path.Join(assetsOutputDir, "resources.qrc");
            try
            {
                _ = Directory.CreateDirectory(assetsOutputDir);
                File.WriteAllText(qrcPath, GenerateQrc(resources), Utf8NoBom);
                outputPaths.Add(qrcPath);
                return qrcPath;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.AssetCopyFailed,
                    $"QRC file could not be written: {exception.Message}",
                    qrcPath));
                return null;
            }
        }

        /// <inheritdoc />
        public string GenerateQrc(ImmutableArray<ResourceEntry> resources)
        {
            StringBuilder builder = new();
            builder.Append("<RCC>\n");
            builder.Append("  <qresource prefix=\"/\">\n");
            foreach (ResourceEntry resource in resources
                .OrderBy(static resource => resource.RelativePath, StringComparer.Ordinal))
            {
                string qmlPath = ModuleMetadataPaths.ToQmlRelativePath(resource.RelativePath);
                builder.Append("    <file>");
                builder.Append(SecurityElement.Escape(qmlPath));
                builder.Append("</file>\n");
            }

            builder.Append("  </qresource>\n");
            builder.Append("</RCC>\n");
            return builder.ToString();
        }

        private void AddResources(
            string assetRoot,
            ImmutableArray<ResourceEntry>.Builder resources,
            ImmutableArray<BuildDiagnostic>.Builder diagnostics)
        {
            try
            {
                foreach (string filePath in Directory
                    .EnumerateFiles(assetRoot, "*", SearchOption.AllDirectories)
                    .OrderBy(static path => path, StringComparer.Ordinal))
                {
                    FileInfo fileInfo = new(filePath);
                    string relativePath = ModuleMetadataPaths.ToQmlRelativePath(Path.GetRelativePath(assetRoot, filePath));
                    resources.Add(new ResourceEntry(
                        fileInfo.FullName,
                        relativePath,
                        Classify(fileInfo.Extension),
                        fileInfo.Length));
                }
            }
            catch (IOException exception)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.AssetCopyFailed,
                    $"Asset root '{assetRoot}' could not be scanned: {exception.Message}",
                    assetRoot));
            }
            catch (UnauthorizedAccessException exception)
            {
                diagnostics.Add(CreateDiagnostic(
                    BuildDiagnosticCode.AssetCopyFailed,
                    $"Asset root '{assetRoot}' could not be scanned: {exception.Message}",
                    assetRoot));
            }
        }

        private string NormalizeAssetRoot(string assetRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(assetRoot);

            string trimmed = assetRoot.Trim();
            if (Path.IsPathRooted(trimmed))
            {
                return Path.GetFullPath(trimmed);
            }

            return Path.GetFullPath(Path.Join(_projectDir, trimmed));
        }

        private static string ResolveOutputPath(string assetsOutputDir, string relativePath)
        {
            string normalizedRelativePath = relativePath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            string[] segments = normalizedRelativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(static segment => segment is "." or ".."))
            {
                throw new IOException("Asset relative paths must not contain traversal segments.");
            }

            string outputPath = Path.GetFullPath(Path.Join(assetsOutputDir, Path.Combine(segments)));
            string normalizedRoot = Path.GetFullPath(assetsOutputDir);
            string rootPrefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
                ? normalizedRoot
                : normalizedRoot + Path.DirectorySeparatorChar;
            if (!outputPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Asset output path resolved outside the assets output directory.");
            }

            return outputPath;
        }

        private static ResourceType Classify(string extension)
        {
            if (ImageExtensions.Contains(extension))
            {
                return ResourceType.Image;
            }

            if (FontExtensions.Contains(extension))
            {
                return ResourceType.Font;
            }

            if (IconExtensions.Contains(extension))
            {
                return ResourceType.Icon;
            }

            if (string.Equals(extension, ".qml", StringComparison.OrdinalIgnoreCase))
            {
                return ResourceType.Qml;
            }

            return ResourceType.Other;
        }

        private static BuildDiagnostic CreateDiagnostic(string code, string message, string path)
        {
            return new BuildDiagnostic(
                code,
                BuildDiagnosticSeverity.Error,
                message,
                BuildPhase.AssetBundling,
                path);
        }
    }
}
