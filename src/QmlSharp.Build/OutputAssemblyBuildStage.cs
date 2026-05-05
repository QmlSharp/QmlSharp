namespace QmlSharp.Build
{
    internal sealed class OutputAssemblyBuildStage : IBuildStage
    {
        private readonly IProductLayout productLayout;

        public OutputAssemblyBuildStage()
            : this(new ProductLayout())
        {
        }

        public OutputAssemblyBuildStage(IProductLayout productLayout)
        {
            ArgumentNullException.ThrowIfNull(productLayout);

            this.productLayout = productLayout;
        }

        public BuildPhase Phase => BuildPhase.OutputAssembly;

        public Task<BuildStageResult> ExecuteAsync(BuildContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            if (context.DryRun)
            {
                return Task.FromResult(BuildStageResult.Succeeded());
            }

            BuildArtifacts discoveredArtifacts = DiscoverArtifacts(context.OutputDir, context);
            if (!HasProductInputs(discoveredArtifacts))
            {
                return Task.FromResult(BuildStageResult.Succeeded());
            }

            ProductAssemblyResult assemblyResult = productLayout.Assemble(context, discoveredArtifacts);
            if (!assemblyResult.Success)
            {
                return Task.FromResult(new BuildStageResult
                {
                    Success = false,
                    Diagnostics = assemblyResult.Diagnostics,
                    Manifest = assemblyResult.Manifest,
                });
            }

            string productRoot = ProductLayout.GetProductRoot(context);
            return Task.FromResult(BuildStageResult.Succeeded(
                artifacts: DiscoverArtifacts(productRoot, context),
                manifest: assemblyResult.Manifest));
        }

        private static BuildArtifacts DiscoverArtifacts(string outputRoot, BuildContext context)
        {
            string root = Path.GetFullPath(outputRoot);
            string qmlRoot = Path.Join(root, "qml");
            string schemasRoot = Path.Join(root, "schemas");
            string sourceMapsRoot = Path.Join(root, "source-maps");
            string assetsRoot = Path.Join(root, "assets");
            string managedRoot = Path.Join(root, "managed");
            string eventBindingsPath = Path.Join(root, "event-bindings.json");
            string nativePath = Path.Join(root, "native", NativeLibraryNames.GetFileName("qmlsharp_native"));

            return new BuildArtifacts
            {
                QmlFiles = EnumerateFiles(qmlRoot, "*.qml"),
                SchemaFiles = EnumerateFiles(schemasRoot, "*.schema.json"),
                EventBindingsFile = File.Exists(eventBindingsPath) ? eventBindingsPath : null,
                SourceMapFiles = EnumerateFiles(sourceMapsRoot, "*.qml.map"),
                ModuleMetadataFiles = EnumerateModuleMetadataFiles(qmlRoot),
                AssetFiles = EnumerateFiles(assetsRoot, "*"),
                NativeLibraryPath = File.Exists(nativePath) ? nativePath : null,
                AssemblyPath = DiscoverManagedAssembly(managedRoot, context),
            };
        }

        private static string? DiscoverManagedAssembly(string managedRoot, BuildContext context)
        {
            if (!Directory.Exists(managedRoot))
            {
                return null;
            }

            string projectAssemblyPath = Path.Join(managedRoot, ProjectName(context) + ".dll");
            if (File.Exists(projectAssemblyPath))
            {
                return projectAssemblyPath;
            }

            return Directory
                .EnumerateFiles(managedRoot, "*.dll", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static bool HasProductInputs(BuildArtifacts artifacts)
        {
            return !artifacts.QmlFiles.IsDefaultOrEmpty ||
                !artifacts.SchemaFiles.IsDefaultOrEmpty ||
                !artifacts.SourceMapFiles.IsDefaultOrEmpty ||
                !artifacts.ModuleMetadataFiles.IsDefaultOrEmpty ||
                !string.IsNullOrWhiteSpace(artifacts.EventBindingsFile) ||
                !string.IsNullOrWhiteSpace(artifacts.NativeLibraryPath) ||
                !string.IsNullOrWhiteSpace(artifacts.AssemblyPath);
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

        private static string ProjectName(BuildContext context)
        {
            return context.Config.Name ?? context.Config.Module.Prefix;
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
                    string.Equals(Path.GetFileName(path), "qmldir", StringComparison.Ordinal) ||
                    path.EndsWith(".qmltypes", StringComparison.Ordinal))
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }
    }
}
