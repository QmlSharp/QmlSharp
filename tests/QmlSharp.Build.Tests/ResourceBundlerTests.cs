using System.Xml.Linq;
using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class ResourceBundlerTests
    {
        [Fact]
        public void RB01_CollectFromDirectoryWithImagesAndFonts_ReturnsAllFilesWithResourceTypes()
        {
            using TempDirectory project = new("qmlsharp assets collect");
            string assetsRoot = CreateAssetsRoot(project.Path);
            string imagePath = WriteAsset(assetsRoot, "images/logo.png", "png");
            string fontPath = WriteAsset(assetsRoot, "fonts/app.ttf", "font");
            ResourceBundler bundler = CreateBundler(project.Path);

            ResourceCollectionResult result = bundler.CollectWithDiagnostics(BuildTestFixtures.CreateDefaultConfig());

            Assert.Empty(result.Diagnostics);
            Assert.Contains(result.Resources, resource => resource.SourcePath == imagePath && resource.Type == ResourceType.Image);
            Assert.Contains(result.Resources, resource => resource.SourcePath == fontPath && resource.Type == ResourceType.Font);
        }

        [Fact]
        public void RB02_CollectFromEmptyDirectory_ReturnsEmptyArray()
        {
            using TempDirectory project = new("qmlsharp empty assets");
            _ = CreateAssetsRoot(project.Path);
            ResourceBundler bundler = CreateBundler(project.Path);

            ResourceCollectionResult result = bundler.CollectWithDiagnostics(BuildTestFixtures.CreateDefaultConfig());

            Assert.Empty(result.Resources);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void RB03_BundleCopiesFilesToOutput()
        {
            using TempDirectory project = new("qmlsharp bundle copies");
            string assetsRoot = CreateAssetsRoot(project.Path);
            _ = WriteAsset(assetsRoot, "images/logo.png", "png");
            string outputDir = Path.Join(project.Path, "dist");
            ResourceBundler bundler = CreateBundler(project.Path);
            ImmutableArray<ResourceEntry> resources = bundler.Collect(BuildTestFixtures.CreateDefaultConfig());

            ResourceBundleResult result = bundler.Bundle(resources, outputDir);

            string outputPath = Path.Join(outputDir, "assets", "images", "logo.png");
            Assert.Empty(result.Diagnostics);
            Assert.True(File.Exists(outputPath));
            Assert.Contains(outputPath, result.OutputPaths);
            Assert.Equal(1, result.FilesCopied);
        }

        [Fact]
        public void RB04_BundlePreservesDirectoryStructure()
        {
            using TempDirectory project = new("qmlsharp nested assets");
            string assetsRoot = CreateAssetsRoot(project.Path);
            _ = WriteAsset(assetsRoot, "fonts/ui/Display.ttf", "font");
            _ = WriteAsset(assetsRoot, "icons/actions/app.ico", "ico");
            string outputDir = Path.Join(project.Path, "dist");
            ResourceBundler bundler = CreateBundler(project.Path);
            ImmutableArray<ResourceEntry> resources = bundler.Collect(BuildTestFixtures.CreateDefaultConfig());

            ResourceBundleResult result = bundler.Bundle(resources, outputDir);

            Assert.Empty(result.Diagnostics);
            Assert.True(File.Exists(Path.Join(outputDir, "assets", "fonts", "ui", "Display.ttf")));
            Assert.True(File.Exists(Path.Join(outputDir, "assets", "icons", "actions", "app.ico")));
        }

        [Fact]
        public void RB05_GenerateQrcProducesValidXml()
        {
            ResourceBundler bundler = CreateBundler(Directory.GetCurrentDirectory());
            ImmutableArray<ResourceEntry> resources = ImmutableArray.Create(
                new ResourceEntry("C:/project/assets/images/logo.png", "images/logo.png", ResourceType.Image, 3),
                new ResourceEntry("C:/project/assets/fonts/app.ttf", "fonts/app.ttf", ResourceType.Font, 4));

            string qrc = bundler.GenerateQrc(resources);

            XDocument document = XDocument.Parse(qrc);
            Assert.Equal("RCC", document.Root?.Name.LocalName);
            Assert.Contains("<file>images/logo.png</file>", qrc, StringComparison.Ordinal);
            Assert.Contains("<file>fonts/app.ttf</file>", qrc, StringComparison.Ordinal);
        }

        [Fact]
        public void RB06_CollectClassifiesFileTypesCorrectly()
        {
            using TempDirectory project = new("qmlsharp classify assets");
            string assetsRoot = CreateAssetsRoot(project.Path);
            _ = WriteAsset(assetsRoot, "image.png", "png");
            _ = WriteAsset(assetsRoot, "font.ttf", "font");
            _ = WriteAsset(assetsRoot, "icon.ico", "ico");
            _ = WriteAsset(assetsRoot, "View.qml", "Item {}\n");
            _ = WriteAsset(assetsRoot, "data.json", "{}");
            ResourceBundler bundler = CreateBundler(project.Path);

            ImmutableDictionary<string, ResourceType> resourceTypes = bundler
                .Collect(BuildTestFixtures.CreateDefaultConfig())
                .ToImmutableDictionary(
                    static resource => resource.RelativePath,
                    static resource => resource.Type,
                    StringComparer.Ordinal);

            Assert.Equal(ResourceType.Image, resourceTypes["image.png"]);
            Assert.Equal(ResourceType.Font, resourceTypes["font.ttf"]);
            Assert.Equal(ResourceType.Icon, resourceTypes["icon.ico"]);
            Assert.Equal(ResourceType.Qml, resourceTypes["View.qml"]);
            Assert.Equal(ResourceType.Other, resourceTypes["data.json"]);
        }

        [Fact]
        public void CollectWithDiagnostics_MissingConfiguredAssetRoot_ReportsB050()
        {
            using TempDirectory project = new("qmlsharp missing assets");
            ResourceBundler bundler = new(
                project.Path,
                ImmutableArray.Create("missing-assets"),
                reportMissingRoots: true,
                generateQrcOnBundle: false);

            ResourceCollectionResult result = bundler.CollectWithDiagnostics(BuildTestFixtures.CreateDefaultConfig());

            Assert.Empty(result.Resources);
            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCode.AssetNotFound, diagnostic.Code);
            Assert.Equal(BuildPhase.AssetBundling, diagnostic.Phase);
        }

        [Fact]
        public void Bundle_CopyFailure_ReportsB051()
        {
            using TempDirectory project = new("qmlsharp copy failure");
            string assetsRoot = CreateAssetsRoot(project.Path);
            string sourcePath = WriteAsset(assetsRoot, "blocked/file.png", "png");
            string outputDir = Path.Join(project.Path, "dist");
            string blockingPath = Path.Join(outputDir, "assets", "blocked");
            _ = Directory.CreateDirectory(Path.GetDirectoryName(blockingPath)!);
            File.WriteAllText(blockingPath, "I am a file, not a directory.");
            ResourceEntry resource = new(sourcePath, "blocked/file.png", ResourceType.Image, new FileInfo(sourcePath).Length);
            ResourceBundler bundler = CreateBundler(project.Path);

            ResourceBundleResult result = bundler.Bundle(ImmutableArray.Create(resource), outputDir);

            Assert.Equal(0, result.FilesCopied);
            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCode.AssetCopyFailed, diagnostic.Code);
        }

        [Theory]
        [InlineData("../escape.png")]
        [InlineData("drive:escape.png")]
        public void Bundle_UnsafeRelativePath_ReportsB051(string relativePath)
        {
            using TempDirectory project = new("qmlsharp unsafe asset path");
            string assetsRoot = CreateAssetsRoot(project.Path);
            string sourcePath = WriteAsset(assetsRoot, "safe.png", "png");
            string outputDir = Path.Join(project.Path, "dist");
            ResourceEntry resource = new(sourcePath, relativePath, ResourceType.Image, new FileInfo(sourcePath).Length);
            ResourceBundler bundler = CreateBundler(project.Path);

            ResourceBundleResult result = bundler.Bundle(ImmutableArray.Create(resource), outputDir);

            Assert.Equal(0, result.FilesCopied);
            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCode.AssetCopyFailed, diagnostic.Code);
        }

        [Fact]
        public void Bundle_RootedRelativePath_ReportsB051()
        {
            using TempDirectory project = new("qmlsharp rooted asset path");
            string assetsRoot = CreateAssetsRoot(project.Path);
            string sourcePath = WriteAsset(assetsRoot, "safe.png", "png");
            string outputDir = Path.Join(project.Path, "dist");
            string rootedRelativePath = Path.GetFullPath(Path.Join(project.Path, "outside.png"));
            ResourceEntry resource = new(sourcePath, rootedRelativePath, ResourceType.Image, new FileInfo(sourcePath).Length);
            ResourceBundler bundler = CreateBundler(project.Path);

            ResourceBundleResult result = bundler.Bundle(ImmutableArray.Create(resource), outputDir);

            Assert.Equal(0, result.FilesCopied);
            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCode.AssetCopyFailed, diagnostic.Code);
        }

        [Fact]
        public async Task Stage05_AssetBundling_ReturnsArtifactsAndStatsForLaterStages()
        {
            using TempDirectory project = new("qmlsharp stage assets");
            string assetsRoot = CreateAssetsRoot(project.Path);
            _ = WriteAsset(assetsRoot, "icons set/app icon.ico", "ico");
            ResourceBundler bundler = new(
                project.Path,
                ImmutableArray.Create("assets"),
                reportMissingRoots: true,
                generateQrcOnBundle: true);
            ResourceBundlingBuildStage stage = new(bundler);
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(1, result.Stats.AssetsCollected);
            string assetOutput = Path.Join(context.OutputDir, "assets", "icons set", "app icon.ico");
            Assert.Contains(assetOutput, result.Artifacts.AssetFiles);
            Assert.NotNull(result.Artifacts.QrcFile);
            Assert.True(File.Exists(result.Artifacts.QrcFile));
        }

        private static ResourceBundler CreateBundler(string projectDir)
        {
            return new ResourceBundler(
                projectDir,
                ImmutableArray.Create("assets"),
                reportMissingRoots: true,
                generateQrcOnBundle: false);
        }

        private static string CreateAssetsRoot(string projectDir)
        {
            string assetsRoot = Path.Join(projectDir, "assets");
            _ = Directory.CreateDirectory(assetsRoot);
            return assetsRoot;
        }

        private static string WriteAsset(string assetsRoot, string relativePath, string content)
        {
            string[] pathSegments = new[] { assetsRoot }
                .Concat(relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
                .ToArray();
            string path = Path.Join(pathSegments);
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, content);
            return path;
        }
    }
}
