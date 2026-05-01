using System.Diagnostics;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>
    /// Runs the deterministic registry-to-package DSL generation pipeline.
    /// </summary>
    public sealed class GenerationPipeline : IGenerationPipeline
    {
        private readonly IPropGenerator propGenerator;
        private readonly ISignalGenerator signalGenerator;
        private readonly IMethodGenerator methodGenerator;
        private readonly IEnumGenerator enumGenerator;
        private readonly IAttachedPropGenerator attachedPropGenerator;
        private readonly IDefaultPropertyHandler defaultPropertyHandler;
        private readonly IModulePackager modulePackager;
        private readonly IModuleMapper moduleMapper;
        private readonly List<Action<GenerationProgress>> progressCallbacks = [];

        public GenerationPipeline()
            : this(
                new PropGenerator(),
                new SignalGenerator(),
                new MethodGenerator(),
                new EnumGenerator(),
                new AttachedPropGenerator(),
                new DefaultPropertyHandler(),
                new ModulePackager(),
                new ModuleMapper())
        {
        }

        public GenerationPipeline(
            IPropGenerator propGenerator,
            ISignalGenerator signalGenerator,
            IMethodGenerator methodGenerator,
            IEnumGenerator enumGenerator,
            IAttachedPropGenerator attachedPropGenerator,
            IDefaultPropertyHandler defaultPropertyHandler,
            IModulePackager modulePackager,
            IModuleMapper moduleMapper)
        {
            this.propGenerator = propGenerator ?? throw new ArgumentNullException(nameof(propGenerator));
            this.signalGenerator = signalGenerator ?? throw new ArgumentNullException(nameof(signalGenerator));
            this.methodGenerator = methodGenerator ?? throw new ArgumentNullException(nameof(methodGenerator));
            this.enumGenerator = enumGenerator ?? throw new ArgumentNullException(nameof(enumGenerator));
            this.attachedPropGenerator = attachedPropGenerator ?? throw new ArgumentNullException(nameof(attachedPropGenerator));
            this.defaultPropertyHandler = defaultPropertyHandler ?? throw new ArgumentNullException(nameof(defaultPropertyHandler));
            this.modulePackager = modulePackager ?? throw new ArgumentNullException(nameof(modulePackager));
            this.moduleMapper = moduleMapper ?? throw new ArgumentNullException(nameof(moduleMapper));
        }

        public Task<GenerationResult> Generate(IRegistryQuery registry, GenerationOptions options)
        {
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentNullException.ThrowIfNull(options);

            Stopwatch stopwatch = Stopwatch.StartNew();
            PipelineRun run = new(registry, options, ReportProgress);
            IReadOnlyList<QmlModule> modules = registry.GetAllModules()
                .OrderBy(module => moduleMapper.GetPriority(module.Uri))
                .ThenBy(module => module.Uri, StringComparer.Ordinal)
                .ToArray();
            CollectNameCollisionWarnings(modules, registry, run);
            IReadOnlyDictionary<string, GeneratedTypeCode> generatedTypes = GenerateTypesForModules(modules, run);
            ImmutableArray<GeneratedPackage> packages = PackageModules(modules, generatedTypes, run);
            stopwatch.Stop();

            ReportProgress(GenerationPhase.Writing, 11, 12, "Writing is handled by IModulePackager.WritePackage.");
            ReportProgress(GenerationPhase.Done, 12, 12, "Generation completed.");

            GenerationStats stats = CreateStats(packages, stopwatch.Elapsed);
            GenerationResult result = new(
                Packages: packages,
                Stats: stats,
                Warnings: run.GetWarnings(),
                SkippedTypes: run.GetSkippedTypes());

            return Task.FromResult(result);
        }

        public Task<GeneratedPackage> GenerateModule(
            IRegistryQuery registry,
            string moduleUri,
            GenerationOptions options)
        {
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleUri);
            ArgumentNullException.ThrowIfNull(options);

            QmlModule module = registry.FindModule(moduleUri)
                ?? throw new DslGenerationException(
                    $"Module '{moduleUri}' was not found in the registry.",
                    DslDiagnosticCodes.EmptyModule,
                    moduleUri: moduleUri);

            PipelineRun run = new(registry, options, ReportProgress);
            CollectNameCollisionWarnings([module], registry, run);
            IReadOnlyDictionary<string, GeneratedTypeCode> generatedTypes = GenerateTypesForModules([module], run);
            ImmutableArray<GeneratedPackage> packages = PackageModules([module], generatedTypes, run);
            ReportProgress(GenerationPhase.Writing, 11, 12, "Writing is handled by IModulePackager.WritePackage.");
            ReportProgress(GenerationPhase.Done, 12, 12, $"Generated module {moduleUri}.");

            if (packages.IsDefaultOrEmpty)
            {
                throw new DslGenerationException(
                    $"Module '{moduleUri}' does not contain generated types.",
                    DslDiagnosticCodes.EmptyModule,
                    moduleUri: moduleUri);
            }

            return Task.FromResult(packages[0]);
        }

        public Task<GeneratedTypeCode> GenerateType(
            IRegistryQuery registry,
            string qualifiedName,
            GenerationOptions options)
        {
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedName);
            ArgumentNullException.ThrowIfNull(options);

            QmlType type = registry.FindTypeByQualifiedName(qualifiedName)
                ?? registry.FindTypes(candidate =>
                        string.Equals(candidate.QmlName, qualifiedName, StringComparison.Ordinal))
                    .OrderBy(candidate => candidate.QualifiedName, StringComparer.Ordinal)
                    .FirstOrDefault()
                ?? throw new DslGenerationException(
                    $"Type '{qualifiedName}' was not found in the registry.",
                    DslDiagnosticCodes.SkippedType,
                    typeName: qualifiedName);

            PipelineRun run = new(registry, options, ReportProgress);
            if (!ShouldGenerateType(type, run))
            {
                throw new DslGenerationException(
                    $"Type '{qualifiedName}' was skipped by generation filters.",
                    DslDiagnosticCodes.SkippedType,
                    type.QualifiedName,
                    type.ModuleUri);
            }

            IReadOnlyDictionary<(string ModuleUri, string QmlName), string> generatedNames = PrecomputeGeneratedTypeNames(registry);
            GeneratedTypeCode generatedType = GenerateTypeCore(type, run, generatedNames);
            ReportProgress(GenerationPhase.Done, 12, 12, $"Generated type {type.QualifiedName}.");
            return Task.FromResult(generatedType);
        }

        public void OnProgress(Action<GenerationProgress> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            progressCallbacks.Add(callback);
        }

        private IReadOnlyDictionary<string, GeneratedTypeCode> GenerateTypesForModules(
            IReadOnlyList<QmlModule> modules,
            PipelineRun run)
        {
            Dictionary<string, GeneratedTypeCode> generatedTypes = new(StringComparer.Ordinal);
            IReadOnlyDictionary<(string ModuleUri, string QmlName), string> generatedNames = PrecomputeGeneratedTypeNames(run.Registry);
            foreach (QmlModule module in modules)
            {
                foreach (QmlType type in module.Types
                             .OrderBy(moduleType => moduleType.QmlName, StringComparer.Ordinal)
                             .ThenBy(moduleType => moduleType.QualifiedName, StringComparer.Ordinal)
                             .Select(moduleType => run.Registry.FindTypeByQualifiedName(moduleType.QualifiedName)
                                 ?? run.Registry.FindTypeByQmlName(module.Uri, moduleType.QmlName))
                             .Where(static type => type is not null)
                             .Select(static type => type!))
                {
                    if (!ShouldGenerateType(type, run))
                    {
                        continue;
                    }

                    try
                    {
                        generatedTypes[type.QualifiedName] = GenerateTypeCore(type, run, generatedNames);
                    }
                    catch (DslGenerationException exception)
                    {
                        run.Warn(MapWarningCode(exception), exception.Message, type.QualifiedName, type.ModuleUri);
                        run.Skip(type, exception.Message, MapDiagnosticCode(exception));
                    }
                }
            }

            return generatedTypes
                .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal);
        }

        private GeneratedTypeCode GenerateTypeCore(
            QmlType type,
            PipelineRun run,
            IReadOnlyDictionary<(string ModuleUri, string QmlName), string>? generatedNames = null)
        {
            string moduleUri = type.ModuleUri ?? string.Empty;
            ITypeMapper typeMapper = run.Options.TypeMapper ?? new TypeMapper();
            INameRegistry nameRegistry = new NameRegistry();
            GenerationContext context = new(
                typeMapper,
                nameRegistry,
                run.Registry,
                run.Options,
                moduleUri);

            ReportProgress(GenerationPhase.ResolvingInheritance, 1, 12, type.QualifiedName);
            ResolvedType resolvedType = new InheritanceResolver(run.Options.Inheritance).Resolve(type, run.Registry);

            ReportProgress(GenerationPhase.MappingTypes, 2, 12, type.QualifiedName);
            CollectTypeReferenceWarnings(resolvedType, run, typeMapper);

            ReportProgress(GenerationPhase.GeneratingProperties, 3, 12, type.QualifiedName);
            ImmutableArray<GeneratedProperty> properties = propGenerator.GenerateAll(resolvedType, context);

            ReportProgress(GenerationPhase.GeneratingSignals, 4, 12, type.QualifiedName);
            ImmutableArray<GeneratedSignal> signals = signalGenerator.GenerateAll(resolvedType, context);

            ReportProgress(GenerationPhase.GeneratingMethods, 5, 12, type.QualifiedName);
            ImmutableArray<GeneratedMethod> methods = methodGenerator.GenerateAll(resolvedType, context);

            ReportProgress(GenerationPhase.GeneratingEnums, 6, 12, type.QualifiedName);
            ImmutableArray<GeneratedEnum> enums = enumGenerator.GenerateAll(resolvedType.AllEnums, type, context);

            ReportProgress(GenerationPhase.GeneratingAttachedProps, 7, 12, type.QualifiedName);
            ImmutableArray<GeneratedAttachedType> attachedTypes = GenerateAttachedTypes(resolvedType, context, run);

            ReportProgress(GenerationPhase.GeneratingDefaultProps, 8, 12, type.QualifiedName);
            DefaultPropertyInfo? defaultProperty = defaultPropertyHandler.Analyze(resolvedType);

            ReportProgress(GenerationPhase.GeneratingViewModels, 9, 12, type.QualifiedName);

            string qmlName = type.QmlName ?? type.QualifiedName;
            string factoryName = GetGeneratedTypeName(type, generatedNames, nameRegistry);
            bool isDeprecated = IsDeprecatedType(type);

            ReportProgress(GenerationPhase.EmittingCode, 10, 12, type.QualifiedName);
            return new GeneratedTypeCode(
                QmlName: qmlName,
                ModuleUri: moduleUri,
                FactoryName: factoryName,
                PropsInterfaceName: $"I{factoryName}Props",
                BuilderInterfaceName: $"I{factoryName}Builder",
                FactoryMethodCode: type.IsCreatable
                    ? $"public static I{factoryName}Builder {factoryName}() => ObjectFactory.Create<I{factoryName}Builder>(\"{qmlName}\");"
                    : null,
                Properties: properties,
                Signals: signals,
                Methods: methods,
                Enums: enums,
                AttachedTypes: attachedTypes,
                DefaultProperty: defaultProperty,
                IsCreatable: type.IsCreatable,
                IsDeprecated: isDeprecated);
        }

        private ImmutableArray<GeneratedPackage> PackageModules(
            IReadOnlyList<QmlModule> modules,
            IReadOnlyDictionary<string, GeneratedTypeCode> generatedTypes,
            PipelineRun run)
        {
            ImmutableArray<GeneratedPackage>.Builder packages = ImmutableArray.CreateBuilder<GeneratedPackage>();
            foreach (QmlModule module in modules)
            {
                ReportProgress(GenerationPhase.Packaging, 10, 12, module.Uri);
                bool hasGeneratedTypes = HasGeneratedTypesForModule(module, generatedTypes);
                if (!hasGeneratedTypes)
                {
                    run.Warn(
                        GenerationWarningCode.EmptyModule,
                        $"Module '{module.Uri}' does not contain generated types.",
                        typeName: null,
                        module.Uri);
                    continue;
                }

                try
                {
                    packages.Add(modulePackager.PackageModule(module, generatedTypes, run.Options.Packager));
                }
                catch (DslGenerationException exception)
                    when (string.Equals(exception.DiagnosticCode, DslDiagnosticCodes.EmptyModule, StringComparison.Ordinal))
                {
                    run.Warn(GenerationWarningCode.EmptyModule, exception.Message, exception.TypeName, exception.ModuleUri);
                }
            }

            return packages
                .OrderBy(package => moduleMapper.GetPriority(package.ModuleUri))
                .ThenBy(package => package.ModuleUri, StringComparer.Ordinal)
                .ThenBy(package => package.PackageName, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private ImmutableArray<GeneratedAttachedType> GenerateAttachedTypes(
            ResolvedType resolvedType,
            GenerationContext context,
            PipelineRun run)
        {
            if (resolvedType.AttachedType is null)
            {
                return ImmutableArray<GeneratedAttachedType>.Empty;
            }

            try
            {
                return [attachedPropGenerator.Generate(resolvedType.AttachedType, context)];
            }
            catch (DslGenerationException exception)
            {
                run.Warn(MapWarningCode(exception), exception.Message, resolvedType.Type.QualifiedName, resolvedType.Type.ModuleUri);
                return ImmutableArray<GeneratedAttachedType>.Empty;
            }
        }

        private static bool HasGeneratedTypesForModule(
            QmlModule module,
            IReadOnlyDictionary<string, GeneratedTypeCode> generatedTypes)
        {
            HashSet<string> moduleQualifiedNames = module.Types
                .Select(static type => type.QualifiedName)
                .ToHashSet(StringComparer.Ordinal);

            return generatedTypes.Any(entry => moduleQualifiedNames.Contains(entry.Key)
                || string.Equals(entry.Value.ModuleUri, module.Uri, StringComparison.Ordinal));
        }

        private static bool ShouldGenerateType(QmlType type, PipelineRun run)
        {
            FilterOptions filter = run.Options.Filter;
            if (filter.ExcludeTypes?.Any(excluded => IsTypeMatch(type, excluded)) == true)
            {
                run.Skip(type, "Type explicitly excluded by generation options.", DslDiagnosticCodes.SkippedType);
                return false;
            }

            if (filter.CreatableOnly && !type.IsCreatable)
            {
                run.Skip(type, "Type is not creatable and CreatableOnly is enabled.", DslDiagnosticCodes.SkippedType);
                return false;
            }

            if (filter.ExcludeInternal && IsInternalType(type))
            {
                run.Skip(type, "Type is internal and ExcludeInternal is enabled.", DslDiagnosticCodes.SkippedType);
                return false;
            }

            if (filter.ExcludeDeprecated && IsDeprecatedType(type))
            {
                run.Warn(
                    GenerationWarningCode.DeprecatedType,
                    $"Type '{type.QualifiedName}' is deprecated and was skipped.",
                    type.QualifiedName,
                    type.ModuleUri);
                run.Skip(type, "Type is deprecated and ExcludeDeprecated is enabled.", DslDiagnosticCodes.DeprecatedType);
                return false;
            }

            if (filter.VersionRange is not null && !IsInVersionRange(type, filter.VersionRange))
            {
                run.Skip(type, "Type export version is outside the configured version range.", DslDiagnosticCodes.SkippedType);
                return false;
            }

            return true;
        }

        private static IReadOnlyDictionary<(string ModuleUri, string QmlName), string> PrecomputeGeneratedTypeNames(IRegistryQuery registry)
        {
            NameRegistry nameRegistry = new();
            Dictionary<(string ModuleUri, string QmlName), string> names = new(EqualityComparer<(string ModuleUri, string QmlName)>.Default);
            foreach (QmlType type in registry.FindTypes(static _ => true)
                         .Where(static type => type.ModuleUri is not null)
                         .OrderBy(static type => type.ModuleUri, StringComparer.Ordinal)
                         .ThenBy(static type => type.QmlName ?? type.QualifiedName, StringComparer.Ordinal)
                         .ThenBy(static type => type.QualifiedName, StringComparer.Ordinal))
            {
                string qmlName = type.QmlName ?? type.QualifiedName;
                names[(type.ModuleUri!, qmlName)] = nameRegistry.RegisterTypeName(qmlName, type.ModuleUri!);
            }

            return names;
        }

        private static void CollectNameCollisionWarnings(
            IReadOnlyList<QmlModule> modules,
            IRegistryQuery registry,
            PipelineRun run)
        {
            HashSet<string> moduleUris = modules.Select(static module => module.Uri).ToHashSet(StringComparer.Ordinal);
            HashSet<string> duplicateQmlNames = GetDuplicateQmlNames(registry, moduleUris);
            NameRegistry generatedNameRegistry = new();
            foreach (QmlType type in GetNamedTypesForModules(registry, moduleUris))
            {
                CollectGeneratedNameWarning(type, duplicateQmlNames, generatedNameRegistry, run);
            }

            foreach (IGrouping<string, QmlType> group in registry.FindTypes(type =>
                         type.ModuleUri is not null
                         && moduleUris.Contains(type.ModuleUri)
                         && !string.IsNullOrWhiteSpace(type.QmlName))
                         .GroupBy(static type => type.QmlName!, StringComparer.Ordinal)
                         .Where(static group => group.Select(type => type.ModuleUri).Distinct(StringComparer.Ordinal).Count() > 1)
                         .OrderBy(static group => group.Key, StringComparer.Ordinal))
            {
                foreach (QmlType type in group.OrderBy(static type => type.ModuleUri, StringComparer.Ordinal).ThenBy(static type => type.QualifiedName, StringComparer.Ordinal))
                {
                    run.Warn(
                        GenerationWarningCode.NameCollision,
                        $"{DslDiagnosticCodes.CrossModuleNameCollision}: QML type name '{group.Key}' appears in multiple modules and may require qualified generated names.",
                        type.QualifiedName,
                        type.ModuleUri);
                }
            }
        }

        private static IReadOnlyList<QmlType> GetNamedTypesForModules(IRegistryQuery registry, HashSet<string> moduleUris)
        {
            return registry.FindTypes(type =>
                    type.ModuleUri is not null
                    && moduleUris.Contains(type.ModuleUri)
                    && !string.IsNullOrWhiteSpace(type.QmlName))
                .OrderBy(static type => type.ModuleUri, StringComparer.Ordinal)
                .ThenBy(static type => type.QmlName, StringComparer.Ordinal)
                .ThenBy(static type => type.QualifiedName, StringComparer.Ordinal)
                .ToArray();
        }

        private static HashSet<string> GetDuplicateQmlNames(IRegistryQuery registry, HashSet<string> moduleUris)
        {
            return GetNamedTypesForModules(registry, moduleUris)
                .GroupBy(static type => type.QmlName!, StringComparer.Ordinal)
                .Where(static group => group.Select(type => type.ModuleUri).Distinct(StringComparer.Ordinal).Count() > 1)
                .Select(static group => group.Key)
                .ToHashSet(StringComparer.Ordinal);
        }

        private static void CollectGeneratedNameWarning(
            QmlType type,
            HashSet<string> duplicateQmlNames,
            INameRegistry generatedNameRegistry,
            PipelineRun run)
        {
            string qmlName = type.QmlName!;
            string generatedName = generatedNameRegistry.RegisterTypeName(qmlName, type.ModuleUri!);
            string expectedName = ToPascalCase(qmlName);
            if (generatedName.StartsWith('@'))
            {
                run.Warn(
                    GenerationWarningCode.NameCollision,
                    $"{DslDiagnosticCodes.ReservedWordCollision}: QML type name '{qmlName}' is a C# reserved word and was escaped as '{generatedName}'.",
                    type.QualifiedName,
                    type.ModuleUri);
                return;
            }

            if (string.Equals(generatedName, expectedName, StringComparison.Ordinal))
            {
                return;
            }

            bool hasDeterministicSuffix = generatedName.StartsWith(expectedName, StringComparison.Ordinal)
                && generatedName.Length > expectedName.Length
                && generatedName[expectedName.Length..].All(char.IsDigit);
            if (!duplicateQmlNames.Contains(qmlName) && !hasDeterministicSuffix)
            {
                return;
            }

            string diagnosticCode = duplicateQmlNames.Contains(qmlName)
                ? DslDiagnosticCodes.CrossModuleNameCollision
                : DslDiagnosticCodes.TypeNameCollision;
            run.Warn(
                GenerationWarningCode.NameCollision,
                $"{diagnosticCode}: QML type name '{qmlName}' was generated as '{generatedName}'.",
                type.QualifiedName,
                type.ModuleUri);
        }

        private static string GetGeneratedTypeName(
            QmlType type,
            IReadOnlyDictionary<(string ModuleUri, string QmlName), string>? generatedNames,
            INameRegistry fallbackRegistry)
        {
            string qmlName = type.QmlName ?? type.QualifiedName;
            string moduleUri = type.ModuleUri ?? string.Empty;
            if (generatedNames is not null && generatedNames.TryGetValue((moduleUri, qmlName), out string? generatedName))
            {
                return generatedName;
            }

            return fallbackRegistry.RegisterTypeName(qmlName, string.IsNullOrWhiteSpace(moduleUri) ? "QmlSharp.Generated" : moduleUri);
        }

        private static void CollectTypeReferenceWarnings(
            ResolvedType resolvedType,
            PipelineRun run,
            ITypeMapper typeMapper)
        {
            foreach (string typeName in GetReferencedTypeNames(resolvedType).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                TypeReferenceStatus status = GetTypeReferenceStatus(typeName, run.Registry, typeMapper);
                if (status == TypeReferenceStatus.Known)
                {
                    continue;
                }

                string diagnosticCode = status == TypeReferenceStatus.Ambiguous
                    ? DslDiagnosticCodes.AmbiguousTypeMapping
                    : DslDiagnosticCodes.UnmappedQmlType;
                run.Warn(
                    GenerationWarningCode.UnresolvedTypeReference,
                    $"{diagnosticCode}: Type reference '{typeName}' used by '{resolvedType.Type.QualifiedName}' was not found as an unambiguous registry type or built-in mapping.",
                    resolvedType.Type.QualifiedName,
                    resolvedType.Type.ModuleUri);
            }
        }

        private static IEnumerable<string> GetReferencedTypeNames(ResolvedType resolvedType)
        {
            foreach (ResolvedProperty property in resolvedType.AllProperties)
            {
                yield return property.Property.TypeName;
            }

            foreach (ResolvedSignal signal in resolvedType.AllSignals)
            {
                foreach (QmlParameter parameter in signal.Signal.Parameters)
                {
                    yield return parameter.TypeName;
                }
            }

            foreach (ResolvedMethod method in resolvedType.AllMethods)
            {
                if (!string.IsNullOrWhiteSpace(method.Method.ReturnType))
                {
                    yield return method.Method.ReturnType;
                }

                foreach (QmlParameter parameter in method.Method.Parameters)
                {
                    yield return parameter.TypeName;
                }
            }
        }

        private static TypeReferenceStatus GetTypeReferenceStatus(string typeName, IRegistryQuery registry, ITypeMapper typeMapper)
        {
            if (string.IsNullOrWhiteSpace(typeName)
                || string.Equals(typeName, "void", StringComparison.Ordinal))
            {
                return TypeReferenceStatus.Known;
            }

            if (typeMapper.GetMapping(typeName) is not null)
            {
                return TypeReferenceStatus.Known;
            }

            if (TryGetListElementType(typeName, out string elementType))
            {
                return GetTypeReferenceStatus(elementType, registry, typeMapper);
            }

            if (registry.FindTypeByQualifiedName(typeName) is not null)
            {
                return TypeReferenceStatus.Known;
            }

            int matchCount = registry.FindTypes(type => string.Equals(type.QmlName, typeName, StringComparison.Ordinal)).Count;
            return matchCount switch
            {
                1 => TypeReferenceStatus.Known,
                > 1 => TypeReferenceStatus.Ambiguous,
                _ => TypeReferenceStatus.Unknown,
            };
        }

        private static bool TryGetListElementType(string typeName, out string elementType)
        {
            string normalized = typeName.Trim();
            if (normalized.StartsWith("list<", StringComparison.Ordinal)
                && normalized.EndsWith(">", StringComparison.Ordinal)
                && normalized.Length > "list<>".Length)
            {
                elementType = normalized["list<".Length..^1].Trim();
                return elementType.Length > 0;
            }

            elementType = string.Empty;
            return false;
        }

        private static bool IsTypeMatch(QmlType type, string value)
        {
            return string.Equals(type.QualifiedName, value, StringComparison.Ordinal)
                || string.Equals(type.QmlName, value, StringComparison.Ordinal)
                || string.Equals(type.ModuleUri is null ? type.QualifiedName : $"{type.ModuleUri}.{type.QmlName}", value, StringComparison.Ordinal);
        }

        private static bool IsInternalType(QmlType type)
        {
            return type.AccessSemantics == AccessSemantics.None
                || ContainsInternalMarker(type.ModuleUri)
                || ContainsInternalMarker(type.QualifiedName)
                || ContainsInternalMarker(type.QmlName);
        }

        private static bool ContainsInternalMarker(string? value)
        {
            return value?.Contains("private", StringComparison.OrdinalIgnoreCase) == true
                || value?.Contains("internal", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool IsDeprecatedType(QmlType type)
        {
            return ContainsDeprecatedMarker(type.QualifiedName)
                || ContainsDeprecatedMarker(type.QmlName)
                || type.Exports.Any(export => ContainsDeprecatedMarker(export.Name));
        }

        private static bool ContainsDeprecatedMarker(string? value)
        {
            return value?.Contains("deprecated", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool IsInVersionRange(QmlType type, QmlVersionRange range)
        {
            ImmutableArray<QmlVersion> versions = type.Exports
                .Select(static export => export.Version)
                .ToImmutableArray();
            if (versions.IsDefaultOrEmpty)
            {
                return true;
            }

            return versions.Any(version =>
                (range.MinVersion is null || Compare(version, range.MinVersion) >= 0)
                && (range.MaxVersion is null || Compare(version, range.MaxVersion) <= 0));
        }

        private static int Compare(QmlVersion left, QmlVersion right)
        {
            int major = left.Major.CompareTo(right.Major);
            return major != 0 ? major : left.Minor.CompareTo(right.Minor);
        }

        private static GenerationWarningCode MapWarningCode(DslGenerationException exception)
        {
            return exception.DiagnosticCode switch
            {
                DslDiagnosticCodes.UnresolvedBaseType => GenerationWarningCode.UnresolvedBaseType,
                DslDiagnosticCodes.CircularInheritance => GenerationWarningCode.CircularInheritance,
                DslDiagnosticCodes.DeprecatedType => GenerationWarningCode.DeprecatedType,
                DslDiagnosticCodes.UnsupportedPropertyType
                    or DslDiagnosticCodes.UnsupportedSignalParameter
                    or DslDiagnosticCodes.UnsupportedMethodSignature => GenerationWarningCode.UnsupportedType,
                DslDiagnosticCodes.TypeNameCollision
                    or DslDiagnosticCodes.MethodPropertyNameCollision
                    or DslDiagnosticCodes.EnumNameCollision
                    or DslDiagnosticCodes.ReservedWordCollision
                    or DslDiagnosticCodes.CrossModuleNameCollision => GenerationWarningCode.NameCollision,
                _ => GenerationWarningCode.SkippedType,
            };
        }

        private static string MapDiagnosticCode(DslGenerationException exception)
        {
            return exception.DiagnosticCode switch
            {
                DslDiagnosticCodes.DeprecatedType => DslDiagnosticCodes.DeprecatedType,
                _ => exception.DiagnosticCode,
            };
        }

        private static string ToPascalCase(string name)
        {
            string[] parts = name.Split(['.', '-', '_', ':', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                return "_";
            }

            return string.Concat(parts.Select(static part => string.Concat(char.ToUpperInvariant(part[0]).ToString(), part[1..])));
        }

        private static GenerationStats CreateStats(ImmutableArray<GeneratedPackage> packages, TimeSpan elapsed)
        {
            return new GenerationStats(
                TotalPackages: packages.Length,
                TotalTypes: packages.Sum(static package => package.Types),
                TotalFiles: packages.Sum(static package => package.Files.Length),
                TotalBytes: packages.Sum(static package => package.Files.Sum(static file => System.Text.Encoding.UTF8.GetByteCount(file.Content))),
                ElapsedTime: elapsed);
        }

        private void ReportProgress(GenerationPhase phase, int currentStep, int totalSteps, string? detail)
        {
            if (progressCallbacks.Count == 0)
            {
                return;
            }

            GenerationProgress progress = new(phase, currentStep, totalSteps, detail);
            foreach (Action<GenerationProgress> callback in progressCallbacks.ToArray())
            {
                callback(progress);
            }
        }

        private sealed class PipelineRun
        {
            private readonly Action<GenerationPhase, int, int, string?> progress;
            private readonly List<GenerationWarning> warnings = [];
            private readonly List<SkippedType> skippedTypes = [];

            public PipelineRun(
                IRegistryQuery registry,
                GenerationOptions options,
                Action<GenerationPhase, int, int, string?> progress)
            {
                Registry = registry;
                Options = options;
                this.progress = progress;
            }

            public IRegistryQuery Registry { get; }

            public GenerationOptions Options { get; }

            public void Warn(GenerationWarningCode code, string message, string? typeName, string? moduleUri)
            {
                warnings.Add(new GenerationWarning(code, message, typeName, moduleUri));
            }

            public void Skip(QmlType type, string reason, string diagnosticCode)
            {
                string fullReason = $"{diagnosticCode}: {reason}";
                skippedTypes.Add(new SkippedType(type.QualifiedName, type.ModuleUri ?? string.Empty, fullReason));
                if (!warnings.Any(warning =>
                        warning.Code == GenerationWarningCode.SkippedType
                        && string.Equals(warning.TypeName, type.QualifiedName, StringComparison.Ordinal)
                        && string.Equals(warning.ModuleUri, type.ModuleUri, StringComparison.Ordinal)))
                {
                    Warn(GenerationWarningCode.SkippedType, fullReason, type.QualifiedName, type.ModuleUri);
                }
            }

            public ImmutableArray<GenerationWarning> GetWarnings()
            {
                return warnings
                    .Distinct()
                    .OrderBy(static warning => warning.ModuleUri, StringComparer.Ordinal)
                    .ThenBy(static warning => warning.TypeName, StringComparer.Ordinal)
                    .ThenBy(static warning => warning.Code.ToString(), StringComparer.Ordinal)
                    .ThenBy(static warning => warning.Message, StringComparer.Ordinal)
                    .ToImmutableArray();
            }

            public ImmutableArray<SkippedType> GetSkippedTypes()
            {
                return skippedTypes
                    .Distinct()
                    .OrderBy(static skipped => skipped.ModuleUri, StringComparer.Ordinal)
                    .ThenBy(static skipped => skipped.TypeName, StringComparer.Ordinal)
                    .ThenBy(static skipped => skipped.Reason, StringComparer.Ordinal)
                    .ToImmutableArray();
            }

            public void Report(GenerationPhase phase, int currentStep, int totalSteps, string? detail)
            {
                progress(phase, currentStep, totalSteps, detail);
            }
        }

        private enum TypeReferenceStatus
        {
            Known,
            Unknown,
            Ambiguous,
        }
    }
}
