using System.Text;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>
    /// Builds deterministic NuGet-shaped package directories from generated DSL metadata.
    /// </summary>
    public sealed class ModulePackager : IModulePackager
    {
        private static readonly ImmutableArray<string> RequiredDependencies =
        [
            "QmlSharp.Core",
            "QmlSharp.Dsl",
        ];

        private readonly ICodeEmitter emitter;
        private readonly IModuleMapper mapper;
        private readonly bool usePackagePrefixOption;

        public ModulePackager()
            : this(new CodeEmitter(), new ModuleMapper(), usePackagePrefixOption: true)
        {
        }

        public ModulePackager(ICodeEmitter emitter, IModuleMapper mapper)
            : this(emitter, mapper, usePackagePrefixOption: false)
        {
        }

        private ModulePackager(ICodeEmitter emitter, IModuleMapper mapper, bool usePackagePrefixOption)
        {
            this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
            this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            this.usePackagePrefixOption = usePackagePrefixOption;
        }

        public GeneratedPackage PackageModule(
            QmlModule module,
            IReadOnlyDictionary<string, GeneratedTypeCode> resolvedTypes,
            PackagerOptions options)
        {
            ArgumentNullException.ThrowIfNull(module);
            ArgumentNullException.ThrowIfNull(resolvedTypes);
            ThrowIfInvalidOptions(options);

            ImmutableArray<GeneratedTypeCode> moduleTypes = GetModuleTypes(module, resolvedTypes);
            if (moduleTypes.IsDefaultOrEmpty)
            {
                throw new DslGenerationException(
                    $"Module '{module.Uri}' does not contain generated types.",
                    DslDiagnosticCodes.EmptyModule,
                    moduleUri: module.Uri);
            }

            string packageName = CreateMapper(options).ToPackageName(module.Uri);
            ImmutableArray<string> dependencies = GetDependencies(module, packageName, options);
            ImmutableArray<GeneratedFile> files = CreatePackageFiles(packageName, module.Uri, options.PackageVersion, moduleTypes, dependencies, options);

            return new GeneratedPackage(
                PackageName: packageName,
                ModuleUri: module.Uri,
                PackageVersion: options.PackageVersion,
                Files: files,
                Types: moduleTypes.Length,
                Dependencies: dependencies,
                Stats: CreateStats(moduleTypes, files));
        }

        public async Task<WrittenPackageInfo> WritePackage(GeneratedPackage package, string outputDir)
        {
            ArgumentNullException.ThrowIfNull(package);
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                throw new ArgumentException("Output directory must not be blank.", nameof(outputDir));
            }

            ThrowIfMissingRequiredDependencies(package);

            string outputRoot = Path.GetFullPath(outputDir);
            string packagePath = Path.GetFullPath(Path.Join(outputRoot, ToSafePathSegment(package.PackageName)));
            if (Directory.Exists(packagePath))
            {
                Directory.Delete(packagePath, recursive: true);
            }

            Directory.CreateDirectory(packagePath);

            long totalBytes = 0;
            int fileCount = 0;
            foreach (GeneratedFile file in package.Files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal))
            {
                string destinationPath = GetSafeDestinationPath(packagePath, file.RelativePath);
                string? directoryPath = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                await File.WriteAllTextAsync(destinationPath, file.Content, Encoding.UTF8).ConfigureAwait(false);
                totalBytes += Encoding.UTF8.GetByteCount(file.Content);
                fileCount++;
            }

            return new WrittenPackageInfo(package.PackageName, packagePath, fileCount, totalBytes);
        }

        public ImmutableArray<GeneratedPackage> PackageAll(
            IRegistryQuery registry,
            IReadOnlyDictionary<string, GeneratedTypeCode> allTypes,
            PackagerOptions options)
        {
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentNullException.ThrowIfNull(allTypes);
            ThrowIfInvalidOptions(options);

            IModuleMapper activeMapper = CreateMapper(options);
            ImmutableArray<GeneratedPackage>.Builder packages = ImmutableArray.CreateBuilder<GeneratedPackage>();
            foreach (QmlModule module in registry.GetAllModules()
                         .OrderBy(module => activeMapper.GetPriority(module.Uri))
                         .ThenBy(static module => module.Uri, StringComparer.Ordinal))
            {
                if (GetModuleTypes(module, allTypes).IsDefaultOrEmpty)
                {
                    continue;
                }

                packages.Add(PackageModule(module, allTypes, options));
            }

            return packages.ToImmutable();
        }

        private ImmutableArray<GeneratedFile> CreatePackageFiles(
            string packageName,
            string moduleUri,
            string packageVersion,
            ImmutableArray<GeneratedTypeCode> moduleTypes,
            ImmutableArray<string> dependencies,
            PackagerOptions options)
        {
            ImmutableArray<GeneratedFile>.Builder files = ImmutableArray.CreateBuilder<GeneratedFile>();

            foreach (GeneratedTypeCode type in moduleTypes
                         .OrderBy(static type => type.QmlName, StringComparer.Ordinal)
                         .ThenBy(static type => type.FactoryName, StringComparer.Ordinal))
            {
                files.Add(new GeneratedFile(
                    RelativePath: $"{ToSafePathSegment(type.FactoryName)}.cs",
                    Content: emitter.EmitTypeFile(type, CreateDefaultEmitOptions(options)),
                    Kind: GeneratedFileKind.TypeFile));
            }

            files.Add(new GeneratedFile(
                RelativePath: "Index.cs",
                Content: emitter.EmitIndexFile(moduleTypes, moduleTypes.SelectMany(static type => type.Enums).ToImmutableArray()),
                Kind: GeneratedFileKind.IndexFile));

            GeneratedPackage packageShell = new(
                PackageName: packageName,
                ModuleUri: moduleUri,
                PackageVersion: packageVersion,
                Files: ImmutableArray<GeneratedFile>.Empty,
                Types: moduleTypes.Length,
                Dependencies: dependencies,
                Stats: new PackageStats(
                    TotalTypes: moduleTypes.Length,
                    CreatableTypes: moduleTypes.Count(static type => type.IsCreatable),
                    NonCreatableTypes: moduleTypes.Count(static type => !type.IsCreatable),
                    EnumCount: moduleTypes.Sum(static type => type.Enums.Length),
                    AttachedTypeCount: moduleTypes.Sum(static type => type.AttachedTypes.Length),
                    TotalLinesOfCode: 0,
                    TotalFileSize: 0));

            if (options.GenerateProjectFile)
            {
                files.Add(new GeneratedFile(
                    RelativePath: $"{ToSafePathSegment(packageName)}.csproj",
                    Content: emitter.EmitProjectFile(packageShell),
                    Kind: GeneratedFileKind.ProjectFile));
            }

            if (options.GenerateReadme)
            {
                files.Add(new GeneratedFile(
                    RelativePath: "README.md",
                    Content: emitter.EmitReadme(packageShell),
                    Kind: GeneratedFileKind.ReadmeFile));
            }

            return files
                .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private IModuleMapper CreateMapper(PackagerOptions options)
        {
            if (!usePackagePrefixOption
                || string.IsNullOrWhiteSpace(options.PackagePrefix)
                || string.Equals(options.PackagePrefix, "QmlSharp", StringComparison.Ordinal))
            {
                return mapper;
            }

            return new ModuleMapper(options.PackagePrefix);
        }

        private static CodeEmitOptions CreateDefaultEmitOptions(PackagerOptions options)
        {
            _ = options;
            return new CodeEmitOptions(
                GenerateXmlDoc: true,
                MarkDeprecated: true,
                HeaderComment: "// <auto-generated />");
        }

        private ImmutableArray<string> GetDependencies(QmlModule module, string packageName, PackagerOptions options)
        {
            IModuleMapper activeMapper = CreateMapper(options);
            ImmutableArray<string> moduleDependencies = module.Dependencies
                .Concat(module.Imports)
                .Where(static dependency => !string.IsNullOrWhiteSpace(dependency))
                .Select(activeMapper.ToPackageName)
                .Where(dependency => !string.Equals(dependency, packageName, StringComparison.Ordinal))
                .ToImmutableArray();

            return RequiredDependencies
                .Concat(moduleDependencies)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static ImmutableArray<GeneratedTypeCode> GetModuleTypes(
            QmlModule module,
            IReadOnlyDictionary<string, GeneratedTypeCode> resolvedTypes)
        {
            HashSet<string> moduleQualifiedNames = module.Types
                .Select(static type => type.QualifiedName)
                .ToHashSet(StringComparer.Ordinal);

            return resolvedTypes
                .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                .Where(entry => moduleQualifiedNames.Contains(entry.Key)
                    || string.Equals(entry.Value.ModuleUri, module.Uri, StringComparison.Ordinal))
                .Select(static entry => entry.Value)
                .OrderBy(static type => type.QmlName, StringComparer.Ordinal)
                .ThenBy(static type => type.FactoryName, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static PackageStats CreateStats(ImmutableArray<GeneratedTypeCode> moduleTypes, ImmutableArray<GeneratedFile> files)
        {
            return new PackageStats(
                TotalTypes: moduleTypes.Length,
                CreatableTypes: moduleTypes.Count(static type => type.IsCreatable),
                NonCreatableTypes: moduleTypes.Count(static type => !type.IsCreatable),
                EnumCount: moduleTypes.Sum(static type => type.Enums.Length),
                AttachedTypeCount: moduleTypes.Sum(static type => type.AttachedTypes.Length),
                TotalLinesOfCode: files.Sum(static file => CountLines(file.Content)),
                TotalFileSize: files.Sum(static file => Encoding.UTF8.GetByteCount(file.Content)));
        }

        private static int CountLines(string content)
        {
            if (content.Length == 0)
            {
                return 0;
            }

            return 1 + content.Where(static character => character == '\n').Count();
        }

        private static void ThrowIfInvalidOptions(PackagerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (string.IsNullOrWhiteSpace(options.PackageVersion))
            {
                throw new ArgumentException("Package version must not be blank.", nameof(options));
            }
        }

        private static void ThrowIfMissingRequiredDependencies(GeneratedPackage package)
        {
            foreach (string dependency in RequiredDependencies
                         .Where(dependency => !package.Dependencies.Contains(dependency, StringComparer.Ordinal)))
            {
                throw new DslGenerationException(
                    $"Package '{package.PackageName}' is missing required dependency '{dependency}'.",
                    DslDiagnosticCodes.MissingDependency,
                    moduleUri: package.ModuleUri);
            }
        }

        private static string GetSafeDestinationPath(string packagePath, string relativePath)
        {
            string normalizedRelativePath = NormalizeRelativePath(relativePath);
            string destinationPath = Path.GetFullPath(Path.Join(packagePath, normalizedRelativePath));
            if (!destinationPath.StartsWith(packagePath + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !string.Equals(destinationPath, packagePath, StringComparison.Ordinal))
            {
                throw new IOException($"Generated file path escapes package output directory: {relativePath}");
            }

            return destinationPath;
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new IOException("Generated file path must not be blank.");
            }

            if (Path.IsPathRooted(relativePath))
            {
                throw new IOException($"Generated file path must be relative: {relativePath}");
            }

            string[] segments = relativePath
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length == 0)
            {
                throw new IOException($"Generated file path must include a file name: {relativePath}");
            }

            if (segments.Where(static segment => segment is "." or "..").Any())
            {
                throw new IOException($"Generated file path contains an unsafe segment: {relativePath}");
            }

            return Path.Join(segments);
        }

        private static string ToSafePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "_";
            }

            HashSet<char> invalidCharacters = Path.GetInvalidFileNameChars()
                .Concat(['/', '\\'])
                .ToHashSet();
            StringBuilder builder = new(value.Length);
            foreach (char character in value.Trim())
            {
                builder.Append(invalidCharacters.Contains(character) ? '_' : character);
            }

            string segment = builder.ToString().Trim('.');
            return string.IsNullOrWhiteSpace(segment) ? "_" : segment;
        }
    }
}
