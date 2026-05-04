using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class PackageResolverTests
    {
        [Fact]
        public void PR01_ResolveProjectWithQmlSharpQtQuickDependency_ReturnsResolvedPackageWithManifest()
        {
            using TempDirectory project = new("qmlsharp package project");
            string packageRoot = CreateNuGetPackage(
                project.Path,
                "QmlSharp.QtQuick",
                "1.2.3",
                CreateManifest("QmlSharp.QtQuick", "QmlSharp.QtQuick", new QmlVersion(1, 0)));
            WriteProjectAssets(project.Path, packageRoot, "QmlSharp.QtQuick", "1.2.3");
            PackageResolver resolver = new();

            PackageResolutionResult result = resolver.ResolveWithDiagnostics(project.Path);

            ResolvedPackage package = Assert.Single(result.Packages);
            Assert.Empty(result.Diagnostics);
            Assert.Equal("QmlSharp.QtQuick", package.PackageId);
            Assert.Equal("1.2.3", package.Version);
            Assert.NotNull(package.Manifest);
            Assert.Equal("QmlSharp.QtQuick", package.Manifest.ModuleUri);
        }

        [Fact]
        public void PR02_ResolveProjectWithNoQmlSharpDependencies_ReturnsEmptyArray()
        {
            using TempDirectory project = new("qmlsharp no packages");
            PackageResolver resolver = new();

            PackageResolutionResult result = resolver.ResolveWithDiagnostics(project.Path);

            Assert.Empty(result.Packages);
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void PR03_ResolveProjectWithMultipleQmlSharpPackages_ReturnsAllMatchingPackages()
        {
            using TempDirectory project = new("qmlsharp many packages");
            _ = CreateNuGetPackage(
                project.Path,
                "QmlSharp.Controls",
                "1.0.0",
                CreateManifest("QmlSharp.Controls", "QmlSharp.Controls", new QmlVersion(1, 0)));
            _ = CreateNuGetPackage(
                project.Path,
                "QmlSharp.Charts",
                "2.0.0",
                CreateManifest("QmlSharp.Charts", "QmlSharp.Charts", new QmlVersion(2, 0)));
            _ = CreateNonQmlSharpPackage(project.Path, "Newtonsoft.Json", "13.0.4");
            PackageResolver resolver = new();

            ImmutableArray<ResolvedPackage> packages = resolver.Resolve(project.Path);

            Assert.Equal(2, packages.Length);
            Assert.Contains(packages, static package => package.PackageId == "QmlSharp.Charts");
            Assert.Contains(packages, static package => package.PackageId == "QmlSharp.Controls");
            Assert.DoesNotContain(packages, static package => package.PackageId == "Newtonsoft.Json");
        }

        [Fact]
        public void PR04_PackageWithoutQmlsharpModuleJson_ReturnsPackageWithNullManifest()
        {
            using TempDirectory project = new("qmlsharp no manifest");
            _ = CreateNuGetPackage(project.Path, "QmlSharp.NoManifest", "0.1.0", manifest: null);
            PackageResolver resolver = new();

            ResolvedPackage package = Assert.Single(resolver.Resolve(project.Path));

            Assert.Equal("QmlSharp.NoManifest", package.PackageId);
            Assert.Null(package.Manifest);
        }

        [Fact]
        public void PR05_CollectImportPaths_ReturnsQmlImportPathsFromAllManifests()
        {
            using TempDirectory project = new("qmlsharp import paths");
            string firstQmlRoot = Path.Join(
                CreateNuGetPackage(
                    project.Path,
                    "QmlSharp.First",
                    "1.0.0",
                    CreateManifest("QmlSharp.First", "QmlSharp.First", new QmlVersion(1, 0), qmlImportPaths: ["qml"])),
                "qml");
            string secondQmlRoot = Path.Join(
                CreateNuGetPackage(
                    project.Path,
                    "QmlSharp.Second",
                    "1.0.0",
                    CreateManifest("QmlSharp.Second", "QmlSharp.Second", new QmlVersion(1, 0), qmlImportPaths: ["lib/qml"])),
                "lib",
                "qml");
            _ = Directory.CreateDirectory(firstQmlRoot);
            _ = Directory.CreateDirectory(secondQmlRoot);
            PackageResolver resolver = new();
            ImmutableArray<ResolvedPackage> packages = resolver.Resolve(project.Path);

            ImmutableArray<string> importPaths = resolver.CollectImportPaths(packages);

            Assert.Equal(2, importPaths.Length);
            Assert.Contains(firstQmlRoot, importPaths);
            Assert.Contains(secondQmlRoot, importPaths);
        }

        [Fact]
        public void PR06_CollectSchemas_ReturnsSchemaJsonPathsFromAllManifests()
        {
            using TempDirectory project = new("qmlsharp schema files");
            string packageRoot = CreateNuGetPackage(
                project.Path,
                "QmlSharp.Schemas",
                "1.0.0",
                CreateManifest(
                    "QmlSharp.Schemas",
                    "QmlSharp.Schemas",
                    new QmlVersion(1, 0),
                    schemaFiles: ["schemas", "extra/ExtraViewModel.schema.json"]));
            string schemasDir = Path.Join(packageRoot, "schemas");
            string extraDir = Path.Join(packageRoot, "extra");
            _ = Directory.CreateDirectory(schemasDir);
            _ = Directory.CreateDirectory(extraDir);
            string counterSchema = Path.Join(schemasDir, "CounterViewModel.schema.json");
            string ignored = Path.Join(schemasDir, "readme.txt");
            string extraSchema = Path.Join(extraDir, "ExtraViewModel.schema.json");
            File.WriteAllText(counterSchema, "{}");
            File.WriteAllText(ignored, "not a schema");
            File.WriteAllText(extraSchema, "{}");
            PackageResolver resolver = new();

            ImmutableArray<string> schemas = resolver.CollectSchemas(resolver.Resolve(project.Path));

            Assert.Equal(2, schemas.Length);
            Assert.Contains(counterSchema, schemas);
            Assert.Contains(extraSchema, schemas);
        }

        [Fact]
        public void PR07_InvalidManifestJson_ReportsB041AndSkipsPackage()
        {
            using TempDirectory project = new("qmlsharp bad manifest");
            _ = CreateNuGetPackage(project.Path, "QmlSharp.BadManifest", "1.0.0", "{ not-json");
            PackageResolver resolver = new();

            PackageResolutionResult result = resolver.ResolveWithDiagnostics(project.Path);

            Assert.Empty(result.Packages);
            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCode.ManifestParseError, diagnostic.Code);
            Assert.Equal(BuildPhase.DependencyResolution, diagnostic.Phase);
        }

        [Fact]
        public void ResolveWithDiagnostics_MissingProjectDirectory_ReportsB040()
        {
            using TempDirectory project = new("qmlsharp missing project");
            string missingProject = Path.Join(project.Path, "missing project");
            PackageResolver resolver = new();

            PackageResolutionResult result = resolver.ResolveWithDiagnostics(missingProject);

            Assert.Empty(result.Packages);
            BuildDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(BuildDiagnosticCode.PackageResolutionFailed, diagnostic.Code);
        }

        [Fact]
        public async Task Stage04_DependencyResolution_EmitsPackageArtifactsForLaterStages()
        {
            using TempDirectory project = new("qmlsharp stage package paths");
            string packageRoot = CreateNuGetPackage(
                project.Path,
                "QmlSharp.Spacey",
                "1.0.0",
                CreateManifest(
                    "QmlSharp.Spacey",
                    "QmlSharp.Spacey",
                    new QmlVersion(1, 0),
                    qmlImportPaths: ["qml import"],
                    schemaFiles: ["schemas"]));
            string importPath = Path.Join(packageRoot, "qml import");
            string schemasDir = Path.Join(packageRoot, "schemas");
            _ = Directory.CreateDirectory(importPath);
            _ = Directory.CreateDirectory(schemasDir);
            string schemaPath = Path.Join(schemasDir, "SpaceViewModel.schema.json");
            await File.WriteAllTextAsync(schemaPath, "{}");
            BuildContext context = BuildTestFixtures.CreateDefaultContext(project.Path);
            PackageResolutionBuildStage stage = new();

            BuildStageResult result = await stage.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains(importPath, result.Artifacts.QmlImportPaths);
            Assert.Contains(schemaPath, result.Artifacts.ThirdPartySchemaFiles);
            Assert.Contains(packageRoot, result.Artifacts.PackagePaths);
        }

        private static string CreateNuGetPackage(
            string projectDir,
            string packageId,
            string version,
            string? manifest)
        {
            string packageRoot = Path.Join(projectDir, packageId, version);
            _ = Directory.CreateDirectory(packageRoot);
            if (manifest is not null)
            {
                File.WriteAllText(Path.Join(packageRoot, "qmlsharp.module.json"), manifest);
            }

            return packageRoot;
        }

        private static string CreateNonQmlSharpPackage(string projectDir, string packageId, string version)
        {
            string packageRoot = Path.Join(projectDir, packageId, version);
            _ = Directory.CreateDirectory(packageRoot);
            return packageRoot;
        }

        private static string CreateManifest(
            string packageId,
            string moduleUri,
            QmlVersion version,
            string[]? qmlImportPaths = null,
            string[]? schemaFiles = null)
        {
            string qmlPaths = ToJsonArray(qmlImportPaths ?? []);
            string schemas = ToJsonArray(schemaFiles ?? []);
            return $$"""
                {
                  "packageId": "{{packageId}}",
                  "moduleUri": "{{moduleUri}}",
                  "moduleVersion": { "major": {{version.Major}}, "minor": {{version.Minor}} },
                  "qmlImportPaths": {{qmlPaths}},
                  "schemaFiles": {{schemas}}
                }
                """;
        }

        private static string ToJsonArray(string[] values)
        {
            return "[" + string.Join(", ", values.Select(static value => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"")) + "]";
        }

        private static void WriteProjectAssets(string projectDir, string packageRoot, string packageId, string version)
        {
            string packageFolder = Path.GetDirectoryName(Path.GetDirectoryName(packageRoot)!)!;
            string packagePath = $"{packageId.Replace('\\', '/')}/{version}";
            string objDir = Path.Join(projectDir, "obj");
            _ = Directory.CreateDirectory(objDir);
            File.WriteAllText(
                Path.Join(objDir, "project.assets.json"),
                $$"""
                {
                  "version": 3,
                  "packageFolders": {
                    "{{packageFolder.Replace("\\", "\\\\")}}\\": {}
                  },
                  "libraries": {
                    "{{packageId}}/{{version}}": {
                      "type": "package",
                      "path": "{{packagePath}}"
                    }
                  }
                }
                """);
        }
    }
}
