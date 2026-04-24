using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Parsing;

namespace QmlSharp.Registry.Normalization
{
    internal sealed class TypeNormalizer : ITypeNormalizer
    {
        private const int RegistryFormatVersion = 1;
        private const string BaselineQtVersion = "6.11.0";

        [SuppressMessage("Maintainability", "MA0051:Method is too long", Justification = "The normalizer needs one ordered entry point to merge the three raw source streams deterministically.")]
        public NormalizeResult Normalize(
            IReadOnlyList<RawQmltypesFile> qmltypesFiles,
            IReadOnlyList<(string ModuleUri, RawQmldirFile File)> qmldirFiles,
            IReadOnlyList<RawMetatypesFile> metatypesFiles,
            ITypeNameMapper typeNameMapper)
        {
            ArgumentNullException.ThrowIfNull(qmltypesFiles);
            ArgumentNullException.ThrowIfNull(qmldirFiles);
            ArgumentNullException.ThrowIfNull(metatypesFiles);
            ArgumentNullException.ThrowIfNull(typeNameMapper);

            List<RegistryDiagnostic> diagnostics = [];
            diagnostics.AddRange(qmltypesFiles.SelectMany(file => file.Diagnostics));
            diagnostics.AddRange(qmldirFiles.SelectMany(tuple => tuple.File.Diagnostics));
            diagnostics.AddRange(metatypesFiles.SelectMany(file => file.Diagnostics));

            Dictionary<string, TypeAccumulator> accumulators = new(StringComparer.Ordinal);

            foreach (RawQmltypesFile file in qmltypesFiles.OrderBy(file => file.SourcePath, StringComparer.Ordinal))
            {
                foreach (RawQmltypesComponent component in file.Components.OrderBy(component => component.Name, StringComparer.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(component.Name))
                    {
                        continue;
                    }

                    GetOrCreateAccumulator(accumulators, component.Name)
                        .ApplyQmltypes(component, file.SourcePath, typeNameMapper, diagnostics);
                }
            }

            foreach ((string sourcePath, RawMetatypesClass @class) in EnumerateMetatypesClasses(metatypesFiles))
            {
                string qualifiedName = GetQualifiedName(@class);
                if (string.IsNullOrWhiteSpace(qualifiedName))
                {
                    continue;
                }

                GetOrCreateAccumulator(accumulators, qualifiedName)
                    .ApplyMetatypes(@class, sourcePath, typeNameMapper, diagnostics);
            }

            ApplyQmldirAttribution(accumulators.Values, qmldirFiles);

            Dictionary<string, TypeNameInfo> typeNames = accumulators.Values
                .OrderBy(accumulator => accumulator.QualifiedName, StringComparer.Ordinal)
                .ToDictionary(
                    accumulator => accumulator.QualifiedName,
                    accumulator => accumulator.GetTypeNameInfo(),
                    StringComparer.Ordinal);

            ImmutableDictionary<string, QmlType> typesByQualifiedName = accumulators.Values
                .OrderBy(accumulator => accumulator.QualifiedName, StringComparer.Ordinal)
                .Select(accumulator => accumulator.Build(typeNames, typeNameMapper))
                .ToImmutableDictionary(type => type.QualifiedName, StringComparer.Ordinal);

            DetectUnresolvedReferences(typesByQualifiedName, accumulators, diagnostics);
            DetectCircularInheritance(typesByQualifiedName, accumulators, diagnostics);
            DetectDuplicateExports(typesByQualifiedName.Values, accumulators, diagnostics);

            ImmutableArray<QmlType> builtins = CreateBuiltins(typeNameMapper);
            ImmutableArray<QmlModule> modules = BuildModules(qmldirFiles, typesByQualifiedName.Values);

            QmlRegistry registry = new(
                Modules: modules,
                TypesByQualifiedName: typesByQualifiedName,
                Builtins: builtins,
                FormatVersion: RegistryFormatVersion,
                QtVersion: BaselineQtVersion,
                BuildTimestamp: DateTimeOffset.UtcNow);

            return new NormalizeResult(registry.WithLookupIndexes(), diagnostics.ToImmutableArray());
        }

        private static void ApplyQmldirAttribution(
            IEnumerable<TypeAccumulator> accumulators,
            IReadOnlyList<(string ModuleUri, RawQmldirFile File)> qmldirFiles)
        {
            List<TypeAccumulator> typeAccumulatorList = accumulators
                .OrderBy(accumulator => accumulator.QualifiedName, StringComparer.Ordinal)
                .ToList();

            foreach ((string moduleUri, RawQmldirFile file) in qmldirFiles
                .OrderBy(tuple => ResolveModuleUri(tuple.ModuleUri, tuple.File), StringComparer.Ordinal)
                .ThenBy(tuple => tuple.File.SourcePath, StringComparer.Ordinal))
            {
                string resolvedModuleUri = ResolveModuleUri(moduleUri, file);
                if (string.IsNullOrWhiteSpace(resolvedModuleUri))
                {
                    continue;
                }

                foreach (RawQmldirTypeEntry typeEntry in file.TypeEntries
                    .OrderBy(typeEntry => typeEntry.Name, StringComparer.Ordinal)
                    .ThenBy(typeEntry => typeEntry.Version, StringComparer.Ordinal)
                    .ThenBy(typeEntry => typeEntry.FilePath, StringComparer.Ordinal))
                {
                    List<TypeAccumulator> candidates = FindQmldirCandidates(typeAccumulatorList, resolvedModuleUri, typeEntry);
                    if (candidates.Count == 1)
                    {
                        candidates[0].ApplyQmldirTypeEntry(resolvedModuleUri, typeEntry);
                    }
                }
            }
        }

        [SuppressMessage("Maintainability", "MA0051:Method is too long", Justification = "Module construction needs to merge qmldir metadata with normalized exports in a single deterministic pass.")]
        private static ImmutableArray<QmlModule> BuildModules(
            IReadOnlyList<(string ModuleUri, RawQmldirFile File)> qmldirFiles,
            IEnumerable<QmlType> types)
        {
            List<QmlType> typeList = types
                .OrderBy(type => type.QualifiedName, StringComparer.Ordinal)
                .ToList();
            List<QmlModule> modules = [];

            foreach ((string moduleUri, RawQmldirFile file) in qmldirFiles
                .OrderBy(tuple => ResolveModuleUri(tuple.ModuleUri, tuple.File), StringComparer.Ordinal)
                .ThenBy(tuple => tuple.File.SourcePath, StringComparer.Ordinal))
            {
                string resolvedModuleUri = ResolveModuleUri(moduleUri, file);
                if (string.IsNullOrWhiteSpace(resolvedModuleUri))
                {
                    continue;
                }

                Dictionary<(string Name, QmlVersion Version), QmlModuleType> moduleTypes = new();
                List<QmlVersion> knownVersions = [];

                foreach (QmlType type in typeList)
                {
                    foreach (QmlTypeExport export in type.Exports
                        .Where(export => StringComparer.Ordinal.Equals(export.Module, resolvedModuleUri))
                        .OrderBy(export => export.Name, StringComparer.Ordinal)
                        .ThenBy(export => export.Version.Major)
                        .ThenBy(export => export.Version.Minor))
                    {
                        _ = moduleTypes.TryAdd((export.Name, export.Version), new QmlModuleType(type.QualifiedName, export.Name, export.Version));
                        knownVersions.Add(export.Version);
                    }
                }

                foreach (RawQmldirTypeEntry typeEntry in file.TypeEntries)
                {
                    if (TryParseVersion(typeEntry.Version, out QmlVersion version))
                    {
                        knownVersions.Add(version);
                    }
                }

                QmlVersion moduleVersion = SelectHighestVersion(knownVersions);
                ImmutableArray<QmlModuleType> orderedModuleTypes = moduleTypes.Values
                    .OrderBy(moduleType => moduleType.QmlName, StringComparer.Ordinal)
                    .ThenBy(moduleType => moduleType.ExportVersion.Major)
                    .ThenBy(moduleType => moduleType.ExportVersion.Minor)
                    .ThenBy(moduleType => moduleType.QualifiedName, StringComparer.Ordinal)
                    .ToImmutableArray();

                modules.Add(new QmlModule(
                    Uri: resolvedModuleUri,
                    Version: moduleVersion,
                    Dependencies: file.Depends
                        .Select(import => import.Module)
                        .Where(module => !string.IsNullOrWhiteSpace(module))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(module => module, StringComparer.Ordinal)
                        .ToImmutableArray(),
                    Imports: file.Imports
                        .Select(import => import.Module)
                        .Where(module => !string.IsNullOrWhiteSpace(module))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(module => module, StringComparer.Ordinal)
                        .ToImmutableArray(),
                    Types: orderedModuleTypes));
            }

            return modules
                .OrderBy(module => module.Uri, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static ImmutableArray<QmlType> CreateBuiltins(ITypeNameMapper typeNameMapper)
        {
            HashSet<string> canonicalNames = typeNameMapper.GetAllMappings()
                .Values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.Ordinal);

            canonicalNames.Add("bool");
            canonicalNames.Add("int");
            canonicalNames.Add("double");
            canonicalNames.Add("void");

            return canonicalNames
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => new QmlType(
                    QualifiedName: name,
                    QmlName: name,
                    ModuleUri: null,
                    AccessSemantics: ClassifyBuiltin(name),
                    Prototype: null,
                    DefaultProperty: null,
                    AttachedType: null,
                    Extension: null,
                    IsSingleton: false,
                    IsCreatable: false,
                    Exports: ImmutableArray<QmlTypeExport>.Empty,
                    Properties: ImmutableArray<QmlProperty>.Empty,
                    Signals: ImmutableArray<QmlSignal>.Empty,
                    Methods: ImmutableArray<QmlMethod>.Empty,
                    Enums: ImmutableArray<QmlEnum>.Empty,
                    Interfaces: ImmutableArray<string>.Empty))
                .ToImmutableArray();
        }

        private static AccessSemantics ClassifyBuiltin(string typeName)
        {
            if (string.Equals(typeName, "void", StringComparison.Ordinal))
            {
                return AccessSemantics.None;
            }

            if (string.Equals(typeName, "list", StringComparison.Ordinal)
                || typeName.StartsWith("list<", StringComparison.Ordinal))
            {
                return AccessSemantics.Sequence;
            }

            return AccessSemantics.Value;
        }

        private static void DetectCircularInheritance(
            ImmutableDictionary<string, QmlType> typesByQualifiedName,
            IReadOnlyDictionary<string, TypeAccumulator> accumulators,
            List<RegistryDiagnostic> diagnostics)
        {
            Dictionary<string, VisitState> states = new(StringComparer.Ordinal);
            List<string> stack = [];
            HashSet<string> reportedCycles = new(StringComparer.Ordinal);

            foreach (string qualifiedName in typesByQualifiedName.Keys.OrderBy(typeName => typeName, StringComparer.Ordinal))
            {
                Visit(qualifiedName);
            }

            void Visit(string qualifiedName)
            {
                if (states.TryGetValue(qualifiedName, out VisitState existingState))
                {
                    if (existingState == VisitState.Visiting)
                    {
                        int startIndex = stack.FindIndex(candidate => StringComparer.Ordinal.Equals(candidate, qualifiedName));
                        if (startIndex >= 0)
                        {
                            string[] cycle = stack.Skip(startIndex).Append(qualifiedName).ToArray();
                            string cycleKey = string.Join("->", cycle);
                            if (reportedCycles.Add(cycleKey))
                            {
                                diagnostics.Add(new RegistryDiagnostic(
                                    Severity: DiagnosticSeverity.Error,
                                    Code: DiagnosticCodes.CircularInheritance,
                                    Message: $"Circular inheritance detected: {string.Join(" -> ", cycle)}.",
                                    FilePath: accumulators.TryGetValue(qualifiedName, out TypeAccumulator? accumulator)
                                        ? accumulator.GetDiagnosticSourcePath()
                                        : null,
                                    Line: null,
                                    Column: null));
                            }
                        }

                        return;
                    }

                    if (existingState == VisitState.Visited)
                    {
                        return;
                    }
                }

                if (!typesByQualifiedName.TryGetValue(qualifiedName, out QmlType? type))
                {
                    return;
                }

                states[qualifiedName] = VisitState.Visiting;
                stack.Add(qualifiedName);

                if (type.Prototype is not null && typesByQualifiedName.ContainsKey(type.Prototype))
                {
                    Visit(type.Prototype);
                }

                stack.RemoveAt(stack.Count - 1);
                states[qualifiedName] = VisitState.Visited;
            }
        }

        private static void DetectDuplicateExports(
            IEnumerable<QmlType> types,
            IReadOnlyDictionary<string, TypeAccumulator> accumulators,
            List<RegistryDiagnostic> diagnostics)
        {
            Dictionary<(string Module, string Name, int Major, int Minor), string> seenExports = new();

            foreach (QmlType type in types.OrderBy(type => type.QualifiedName, StringComparer.Ordinal))
            {
                foreach (QmlTypeExport export in type.Exports
                    .OrderBy(export => export.Module, StringComparer.Ordinal)
                    .ThenBy(export => export.Name, StringComparer.Ordinal)
                    .ThenBy(export => export.Version.Major)
                    .ThenBy(export => export.Version.Minor))
                {
                    (string Module, string Name, int Major, int Minor) exportKey = (export.Module, export.Name, export.Version.Major, export.Version.Minor);

                    if (seenExports.TryGetValue(exportKey, out string? existingQualifiedName)
                        && !StringComparer.Ordinal.Equals(existingQualifiedName, type.QualifiedName))
                    {
                        diagnostics.Add(new RegistryDiagnostic(
                            Severity: DiagnosticSeverity.Warning,
                            Code: DiagnosticCodes.DuplicateExport,
                            Message: $"Duplicate export '{export.Module}/{export.Name} {export.Version}' found on '{existingQualifiedName}' and '{type.QualifiedName}'.",
                            FilePath: accumulators.TryGetValue(type.QualifiedName, out TypeAccumulator? accumulator)
                                ? accumulator.GetDiagnosticSourcePath()
                                : null,
                            Line: null,
                            Column: null));
                        continue;
                    }

                    seenExports[exportKey] = type.QualifiedName;
                }
            }
        }

        private static void DetectUnresolvedReferences(
            ImmutableDictionary<string, QmlType> typesByQualifiedName,
            IReadOnlyDictionary<string, TypeAccumulator> accumulators,
            List<RegistryDiagnostic> diagnostics)
        {
            foreach (QmlType type in typesByQualifiedName.Values.OrderBy(type => type.QualifiedName, StringComparer.Ordinal))
            {
                if (type.Prototype is not null && !typesByQualifiedName.ContainsKey(type.Prototype))
                {
                    diagnostics.Add(new RegistryDiagnostic(
                        Severity: DiagnosticSeverity.Warning,
                        Code: DiagnosticCodes.UnresolvedPrototype,
                        Message: $"Type '{type.QualifiedName}' references unresolved prototype '{type.Prototype}'.",
                        FilePath: accumulators.TryGetValue(type.QualifiedName, out TypeAccumulator? accumulator)
                            ? accumulator.GetDiagnosticSourcePath()
                            : null,
                        Line: null,
                        Column: null));
                }

                if (type.AttachedType is not null && !typesByQualifiedName.ContainsKey(type.AttachedType))
                {
                    diagnostics.Add(new RegistryDiagnostic(
                        Severity: DiagnosticSeverity.Warning,
                        Code: DiagnosticCodes.UnresolvedAttachedType,
                        Message: $"Type '{type.QualifiedName}' references unresolved attached type '{type.AttachedType}'.",
                        FilePath: accumulators.TryGetValue(type.QualifiedName, out TypeAccumulator? accumulator)
                            ? accumulator.GetDiagnosticSourcePath()
                            : null,
                        Line: null,
                        Column: null));
                }
            }
        }

        private static List<TypeAccumulator> FindQmldirCandidates(
            IReadOnlyList<TypeAccumulator> accumulators,
            string moduleUri,
            RawQmldirTypeEntry typeEntry)
        {
            List<TypeAccumulator> exactExportMatches = accumulators
                .Where(accumulator => accumulator.HasExport(moduleUri, typeEntry.Name, typeEntry.Version))
                .ToList();

            if (exactExportMatches.Count > 0)
            {
                return exactExportMatches;
            }

            List<TypeAccumulator> sameModuleExportMatches = accumulators
                .Where(accumulator => accumulator.HasExportForModule(moduleUri, typeEntry.Name))
                .ToList();

            if (sameModuleExportMatches.Count > 0)
            {
                return sameModuleExportMatches;
            }

            return accumulators
                .Where(accumulator => accumulator.CanMatchQmldir(moduleUri, typeEntry.Name))
                .ToList();
        }

        private static TypeAccumulator GetOrCreateAccumulator(
            IDictionary<string, TypeAccumulator> accumulators,
            string qualifiedName)
        {
            if (!accumulators.TryGetValue(qualifiedName, out TypeAccumulator? accumulator))
            {
                accumulator = new TypeAccumulator(qualifiedName);
                accumulators.Add(qualifiedName, accumulator);
            }

            return accumulator;
        }

        private static string GetQualifiedName(RawMetatypesClass @class)
        {
            return !string.IsNullOrWhiteSpace(@class.QualifiedClassName)
                ? @class.QualifiedClassName!
                : @class.ClassName;
        }

        private static IEnumerable<(string SourcePath, RawMetatypesClass Class)> EnumerateMetatypesClasses(
            IReadOnlyList<RawMetatypesFile> metatypesFiles)
        {
            foreach (RawMetatypesFile file in metatypesFiles.OrderBy(file => file.SourcePath, StringComparer.Ordinal))
            {
                foreach (RawMetatypesClass @class in file.Entries
                    .SelectMany(entry => entry.Classes)
                    .OrderBy(@class => GetQualifiedName(@class), StringComparer.Ordinal))
                {
                    yield return (file.SourcePath, @class);
                }
            }
        }

        private static string NormalizeTypeReference(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return string.Empty;
            }

            string normalized = typeName.Trim();
            normalized = normalized.Replace("const ", string.Empty, StringComparison.Ordinal);
            normalized = normalized.Replace("class ", string.Empty, StringComparison.Ordinal);
            normalized = normalized.Replace("struct ", string.Empty, StringComparison.Ordinal);
            normalized = normalized.Replace("&", string.Empty, StringComparison.Ordinal);
            normalized = normalized.Replace("*", string.Empty, StringComparison.Ordinal);
            normalized = normalized.Replace(" ", string.Empty, StringComparison.Ordinal);

            return normalized.Trim();
        }

        private static string CanonicalizeNullableType(string? rawTypeName, ITypeNameMapper typeNameMapper)
        {
            return rawTypeName is null ? string.Empty : CanonicalizeType(rawTypeName, typeNameMapper);
        }

        private static string CanonicalizeType(string rawTypeName, ITypeNameMapper typeNameMapper)
        {
            return typeNameMapper.ToQmlName(NormalizeTypeReference(rawTypeName));
        }

        private static string ResolveModuleUri(string moduleUri, RawQmldirFile file)
        {
            if (!string.IsNullOrWhiteSpace(moduleUri))
            {
                return moduleUri;
            }

            return file.Module ?? string.Empty;
        }

        private static QmlVersion SelectHighestVersion(IEnumerable<QmlVersion> versions)
        {
            return versions
                .OrderByDescending(version => version.Major)
                .ThenByDescending(version => version.Minor)
                .FirstOrDefault() ?? new QmlVersion(0, 0);
        }

        private static bool TryParseExport(string exportValue, out ExportSpec export)
        {
            export = default;

            if (string.IsNullOrWhiteSpace(exportValue))
            {
                return false;
            }

            int versionSeparatorIndex = exportValue.LastIndexOf(' ');
            if (versionSeparatorIndex <= 0 || versionSeparatorIndex == exportValue.Length - 1)
            {
                return false;
            }

            string typeReference = exportValue[..versionSeparatorIndex].Trim();
            string versionPart = exportValue[(versionSeparatorIndex + 1)..].Trim();
            int typeSeparatorIndex = typeReference.LastIndexOf('/');

            if (typeSeparatorIndex <= 0 || typeSeparatorIndex == typeReference.Length - 1)
            {
                return false;
            }

            if (!TryParseVersion(versionPart, out QmlVersion version))
            {
                return false;
            }

            export = new ExportSpec(
                Module: typeReference[..typeSeparatorIndex],
                Name: typeReference[(typeSeparatorIndex + 1)..],
                Version: version);
            return true;
        }

        private static bool TryParseVersion(string? versionText, out QmlVersion version)
        {
            version = new QmlVersion(0, 0);

            if (string.IsNullOrWhiteSpace(versionText))
            {
                return false;
            }

            string[] parts = versionText.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major)
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minor))
            {
                return false;
            }

            version = new QmlVersion(major, minor);
            return true;
        }

        private enum VisitState
        {
            Visiting,
            Visited,
        }

        private readonly record struct ExportSpec(string Module, string Name, QmlVersion Version);

        private readonly record struct TypeNameInfo(string QualifiedName, string? QmlName, string? ModuleUri);

        private sealed class TypeAccumulator
        {
            private readonly Dictionary<string, EnumSeed> enumsByName = new(StringComparer.Ordinal);
            private readonly HashSet<string> interfaceSet = new(StringComparer.Ordinal);
            private readonly HashSet<string> methodKeys = new(StringComparer.Ordinal);
            private readonly List<MethodSeed> methods = [];
            private readonly Dictionary<string, PropertySeed> propertiesByName = new(StringComparer.Ordinal);
            private readonly List<PropertySeed> propertyOrder = [];
            private readonly HashSet<string> signalKeys = new(StringComparer.Ordinal);
            private readonly List<SignalSeed> signals = [];
            private readonly List<ExportSpec> exports = [];
            private AccessSemantics? accessSemantics;
            private string? attachedType;
            private string? defaultProperty;
            private bool explicitCreatableIsSet;
            private bool explicitCreatableValue;
            private string? extension;
            private bool isSingleton;
            private string? metatypesSourcePath;
            private string? moduleUri;
            private string? primarySourcePath;
            private string? prototype;
            private string? qmlName;

            public TypeAccumulator(string qualifiedName)
            {
                QualifiedName = qualifiedName;
            }

            public string QualifiedName { get; }

            [SuppressMessage("Maintainability", "MA0051:Method is too long", Justification = "Metatypes supplementation merges member collections and conflict detection in one deterministic pass.")]
            public void ApplyMetatypes(
                RawMetatypesClass @class,
                string sourcePath,
                ITypeNameMapper typeNameMapper,
                List<RegistryDiagnostic> diagnostics)
            {
                metatypesSourcePath ??= sourcePath;

                string? elementName = GetClassInfoValue(@class, "QML.Element");
                string? derivedPrototype = @class.SuperClasses
                    .FirstOrDefault(superClass => string.Equals(superClass.Access, "public", StringComparison.OrdinalIgnoreCase))
                    ?.Name
                    ?? @class.SuperClasses.FirstOrDefault()?.Name;

                ApplySupplementalScalar(
                    currentValue: qmlName,
                    incomingValue: string.IsNullOrWhiteSpace(elementName) ? null : elementName,
                    assign: value => qmlName = value,
                    description: "QML element name",
                    sourcePath: sourcePath,
                    diagnostics: diagnostics);
                ApplySupplementalScalar(
                    currentValue: accessSemantics,
                    incomingValue: DeriveAccessSemantics(@class, QualifiedName, typeNameMapper),
                    assign: value => accessSemantics = value,
                    description: "access semantics",
                    sourcePath: sourcePath,
                    diagnostics: diagnostics);
                ApplySupplementalScalar(
                    currentValue: prototype,
                    incomingValue: NormalizeNullableTypeReference(derivedPrototype),
                    assign: value => prototype = value,
                    description: "prototype",
                    sourcePath: sourcePath,
                    diagnostics: diagnostics);
                ApplySupplementalScalar(
                    currentValue: attachedType,
                    incomingValue: NormalizeNullableTypeReference(GetClassInfoValue(@class, "QML.Attached")),
                    assign: value => attachedType = value,
                    description: "attached type",
                    sourcePath: sourcePath,
                    diagnostics: diagnostics);

                if (!explicitCreatableIsSet && TryGetCreatableValue(@class, out bool creatable))
                {
                    explicitCreatableIsSet = true;
                    explicitCreatableValue = creatable;
                }

                if (TryGetSingletonValue(@class, out bool singleton))
                {
                    isSingleton |= singleton;
                }

                foreach (RawMetatypesProperty property in @class.Properties.OrderBy(property => property.Index).ThenBy(property => property.Name, StringComparer.Ordinal))
                {
                    PropertySeed incoming = new(
                        Name: property.Name,
                        RawTypeName: property.Type,
                        IsReadonly: property.IsReadonly || property.IsConstant,
                        IsList: false,
                        IsRequired: property.IsRequired,
                        DefaultValue: null,
                        NotifySignal: property.Notify);
                    MergeSupplementalProperty(incoming, sourcePath, typeNameMapper, diagnostics);
                }

                foreach (RawMetatypesSignal signal in @class.Signals.OrderBy(signal => signal.Name, StringComparer.Ordinal))
                {
                    MergeSupplementalSignal(new SignalSeed(
                        Name: signal.Name,
                        Parameters: signal.Arguments.Select(argument => new ParameterSeed(argument.Name, argument.Type)).ToImmutableArray()), typeNameMapper);
                }

                foreach (RawMetatypesMethod method in @class.Methods
                    .Where(method => !method.IsCloned)
                    .OrderBy(method => method.Name, StringComparer.Ordinal)
                    .ThenBy(method => method.Revision))
                {
                    MergeSupplementalMethod(new MethodSeed(
                        Name: method.Name,
                        RawReturnType: method.ReturnType,
                        Parameters: method.Arguments.Select(argument => new ParameterSeed(argument.Name, argument.Type)).ToImmutableArray()), typeNameMapper);
                }

                foreach (RawMetatypesEnum @enum in @class.Enums.OrderBy(@enum => @enum.Name, StringComparer.Ordinal))
                {
                    MergeSupplementalEnum(new EnumSeed(@enum.Name, @enum.IsFlag, @enum.Values), sourcePath, diagnostics);
                }
            }

            public void ApplyQmldirTypeEntry(
                string resolvedModuleUri,
                RawQmldirTypeEntry typeEntry)
            {
                if (string.IsNullOrWhiteSpace(moduleUri))
                {
                    moduleUri = resolvedModuleUri;
                }

                if (string.IsNullOrWhiteSpace(qmlName))
                {
                    qmlName = typeEntry.Name;
                }

                isSingleton |= typeEntry.IsSingleton;

                bool alreadyHasExportForModule = exports.Any(export => StringComparer.Ordinal.Equals(export.Module, resolvedModuleUri));
                if (!alreadyHasExportForModule && TryParseVersion(typeEntry.Version, out QmlVersion version))
                {
                    AddExport(new ExportSpec(resolvedModuleUri, typeEntry.Name, version));
                }
            }

            [SuppressMessage("Maintainability", "MA0051:Method is too long", Justification = "Qmltypes ingestion preserves source ordering while applying primary-source merge rules.")]
            public void ApplyQmltypes(
                RawQmltypesComponent component,
                string sourcePath,
                ITypeNameMapper typeNameMapper,
                List<RegistryDiagnostic> diagnostics)
            {
                primarySourcePath ??= sourcePath;

                AccessSemantics? componentAccessSemantics = ParseAccessSemantics(component.AccessSemantics);
                if (componentAccessSemantics is not null)
                {
                    accessSemantics = componentAccessSemantics;
                }

                prototype = PreferPrimary(prototype, NormalizeNullableTypeReference(component.Prototype));
                defaultProperty = PreferPrimary(defaultProperty, component.DefaultProperty);
                attachedType = PreferPrimary(attachedType, NormalizeNullableTypeReference(component.AttachedType));
                extension = PreferPrimary(extension, NormalizeNullableTypeReference(component.Extension));
                isSingleton |= component.IsSingleton;

                if (component.IsCreatable)
                {
                    explicitCreatableIsSet = true;
                    explicitCreatableValue = true;
                }

                foreach (string exportValue in component.Exports)
                {
                    if (TryParseExport(exportValue, out ExportSpec export))
                    {
                        AddExport(export);
                    }
                }

                ExportSpec? bestExport = GetBestExport();
                if (bestExport is not null)
                {
                    qmlName = PreferPrimary(qmlName, bestExport.Value.Name);
                    moduleUri = PreferPrimary(moduleUri, bestExport.Value.Module);
                }

                foreach (string @interface in component.Interfaces.Where(@interface => !string.IsNullOrWhiteSpace(@interface)))
                {
                    interfaceSet.Add(@interface);
                }

                foreach (RawQmltypesProperty property in component.Properties)
                {
                    PropertySeed seed = new(
                        Name: property.Name,
                        RawTypeName: property.Type,
                        IsReadonly: property.IsReadonly,
                        IsList: property.IsList,
                        IsRequired: property.IsRequired,
                        DefaultValue: null,
                        NotifySignal: property.Notify);
                    MergePrimaryProperty(seed, sourcePath, typeNameMapper, diagnostics);
                }

                foreach (RawQmltypesSignal signal in component.Signals)
                {
                    MergePrimarySignal(new SignalSeed(
                        Name: signal.Name,
                        Parameters: signal.Parameters.Select(parameter => new ParameterSeed(parameter.Name, parameter.Type)).ToImmutableArray()), typeNameMapper);
                }

                foreach (RawQmltypesMethod method in component.Methods)
                {
                    MergePrimaryMethod(new MethodSeed(
                        Name: method.Name,
                        RawReturnType: method.ReturnType,
                        Parameters: method.Parameters.Select(parameter => new ParameterSeed(parameter.Name, parameter.Type)).ToImmutableArray()), typeNameMapper);
                }

                foreach (RawQmltypesEnum @enum in component.Enums)
                {
                    MergePrimaryEnum(new EnumSeed(@enum.Name, @enum.IsFlag, @enum.Values), sourcePath, diagnostics);
                }
            }

            public QmlType Build(IReadOnlyDictionary<string, TypeNameInfo> typeNames, ITypeNameMapper typeNameMapper)
            {
                ExportSpec? bestExport = GetBestExport();
                string? resolvedQmlName = qmlName ?? bestExport?.Name;
                string? resolvedModuleUri = moduleUri ?? bestExport?.Module;
                AccessSemantics resolvedAccessSemantics = accessSemantics ?? DeriveDefaultAccessSemantics(QualifiedName, typeNameMapper);
                bool resolvedIsCreatable = explicitCreatableIsSet
                    ? explicitCreatableValue
                    : resolvedAccessSemantics == AccessSemantics.Reference
                        && exports.Count > 0
                        && !isSingleton;

                return new QmlType(
                    QualifiedName: QualifiedName,
                    QmlName: resolvedQmlName,
                    ModuleUri: resolvedModuleUri,
                    AccessSemantics: resolvedAccessSemantics,
                    Prototype: prototype,
                    DefaultProperty: defaultProperty,
                    AttachedType: attachedType,
                    Extension: extension,
                    IsSingleton: isSingleton,
                    IsCreatable: resolvedIsCreatable,
                    Exports: exports
                        .OrderBy(export => export.Module, StringComparer.Ordinal)
                        .ThenBy(export => export.Name, StringComparer.Ordinal)
                        .ThenBy(export => export.Version.Major)
                        .ThenBy(export => export.Version.Minor)
                        .Select(export => new QmlTypeExport(export.Module, export.Name, export.Version))
                        .ToImmutableArray(),
                    Properties: propertyOrder
                        .Select(property => property.Build(typeNames, typeNameMapper))
                        .ToImmutableArray(),
                    Signals: signals
                        .Select(signal => signal.Build(typeNames, typeNameMapper))
                        .ToImmutableArray(),
                    Methods: methods
                        .Select(method => method.Build(typeNames, typeNameMapper))
                        .ToImmutableArray(),
                    Enums: enumsByName.Values
                        .OrderBy(@enum => @enum.Name, StringComparer.Ordinal)
                        .Select(@enum => @enum.Build())
                        .ToImmutableArray(),
                    Interfaces: interfaceSet.OrderBy(@interface => @interface, StringComparer.Ordinal).ToImmutableArray());
            }

            public TypeNameInfo GetTypeNameInfo()
            {
                ExportSpec? bestExport = GetBestExport();
                return new TypeNameInfo(
                    QualifiedName: QualifiedName,
                    QmlName: qmlName ?? bestExport?.Name,
                    ModuleUri: moduleUri ?? bestExport?.Module);
            }

            public string? GetDiagnosticSourcePath()
            {
                return primarySourcePath ?? metatypesSourcePath;
            }

            public bool CanMatchQmldir(string module, string name)
            {
                if (!string.IsNullOrWhiteSpace(moduleUri)
                    && !StringComparer.Ordinal.Equals(moduleUri, module))
                {
                    return false;
                }

                string? candidateQmlName = qmlName ?? GetBestExport()?.Name;
                return StringComparer.Ordinal.Equals(candidateQmlName, name);
            }

            public bool HasExport(string module, string name, string version)
            {
                return exports.Any(export => StringComparer.Ordinal.Equals(export.Module, module)
                    && StringComparer.Ordinal.Equals(export.Name, name)
                    && StringComparer.Ordinal.Equals(export.Version.ToString(), version));
            }

            public bool HasExportForModule(string module, string name)
            {
                return exports.Any(export => StringComparer.Ordinal.Equals(export.Module, module)
                    && StringComparer.Ordinal.Equals(export.Name, name));
            }

            private static AccessSemantics DeriveAccessSemantics(
                RawMetatypesClass @class,
                string qualifiedName,
                ITypeNameMapper typeNameMapper)
            {
                if (@class.IsObject)
                {
                    return AccessSemantics.Reference;
                }

                if (@class.IsGadget)
                {
                    return AccessSemantics.Value;
                }

                if (@class.IsNamespace)
                {
                    return AccessSemantics.None;
                }

                string mappedTypeName = typeNameMapper.ToQmlName(NormalizeTypeReference(qualifiedName));
                if (string.Equals(mappedTypeName, "list", StringComparison.Ordinal)
                    || mappedTypeName.StartsWith("list<", StringComparison.Ordinal))
                {
                    return AccessSemantics.Sequence;
                }

                return AccessSemantics.Reference;
            }

            private static AccessSemantics DeriveDefaultAccessSemantics(string qualifiedName, ITypeNameMapper typeNameMapper)
            {
                string mappedTypeName = typeNameMapper.ToQmlName(NormalizeTypeReference(qualifiedName));
                if (string.Equals(mappedTypeName, "list", StringComparison.Ordinal)
                    || mappedTypeName.StartsWith("list<", StringComparison.Ordinal))
                {
                    return AccessSemantics.Sequence;
                }

                return AccessSemantics.Reference;
            }

            private static string? GetClassInfoValue(RawMetatypesClass @class, string classInfoName)
            {
                return @class.ClassInfos
                    .FirstOrDefault(classInfo => StringComparer.Ordinal.Equals(classInfo.Name, classInfoName))
                    ?.Value;
            }

            private static string BuildMethodKey(MethodSeed method, ITypeNameMapper typeNameMapper)
            {
                return $"{method.Name}({string.Join(",", method.Parameters.Select(parameter => TypeNormalizer.CanonicalizeType(parameter.RawTypeName, typeNameMapper)))})=>{TypeNormalizer.CanonicalizeNullableType(method.RawReturnType, typeNameMapper)}";
            }

            private static string BuildSignalKey(SignalSeed signal, ITypeNameMapper typeNameMapper)
            {
                return $"{signal.Name}({string.Join(",", signal.Parameters.Select(parameter => TypeNormalizer.CanonicalizeType(parameter.RawTypeName, typeNameMapper)))})";
            }

            private static PropertySeed ChooseConflictWinner(PropertySeed existing, PropertySeed incoming)
            {
                return existing with
                {
                    NotifySignal = existing.NotifySignal ?? incoming.NotifySignal,
                };
            }

            private static string? NormalizeNullableTypeReference(string? rawTypeName)
            {
                return string.IsNullOrWhiteSpace(rawTypeName)
                    ? null
                    : NormalizeTypeReference(rawTypeName);
            }

            private static AccessSemantics? ParseAccessSemantics(string? accessSemanticsValue)
            {
                if (string.IsNullOrWhiteSpace(accessSemanticsValue))
                {
                    return null;
                }

                return accessSemanticsValue switch
                {
                    "reference" => AccessSemantics.Reference,
                    "value" => AccessSemantics.Value,
                    "sequence" => AccessSemantics.Sequence,
                    "none" => AccessSemantics.None,
                    _ => null,
                };
            }

            private static string? PreferPrimary(string? currentValue, string? incomingValue)
            {
                return string.IsNullOrWhiteSpace(currentValue) ? incomingValue : currentValue;
            }

            private static void ReportConflict(
                string qualifiedName,
                string description,
                string existingValue,
                string incomingValue,
                string? filePath,
                List<RegistryDiagnostic> diagnostics)
            {
                diagnostics.Add(new RegistryDiagnostic(
                    Severity: DiagnosticSeverity.Warning,
                    Code: DiagnosticCodes.TypeConflict,
                    Message: $"Type '{qualifiedName}' has a conflicting {description}; qmltypes value '{existingValue}' wins over metatypes value '{incomingValue}'.",
                    FilePath: filePath,
                    Line: null,
                    Column: null));
            }

            private static bool TryGetCreatableValue(RawMetatypesClass @class, out bool creatable)
            {
                creatable = default;

                string? explicitCreatable = GetClassInfoValue(@class, "QML.Creatable");
                if (explicitCreatable is not null && bool.TryParse(explicitCreatable, out creatable))
                {
                    return true;
                }

                if (GetClassInfoValue(@class, "QML.Uncreatable") is not null)
                {
                    creatable = false;
                    return true;
                }

                return false;
            }

            private static bool TryGetSingletonValue(RawMetatypesClass @class, out bool singleton)
            {
                singleton = default;

                string? value = GetClassInfoValue(@class, "QML.Singleton");
                if (value is null)
                {
                    return false;
                }

                singleton = string.IsNullOrWhiteSpace(value) || bool.TryParse(value, out bool parsed) && parsed;
                return true;
            }

            private void AddExport(ExportSpec export)
            {
                if (exports.Any(existing => StringComparer.Ordinal.Equals(existing.Module, export.Module)
                    && StringComparer.Ordinal.Equals(existing.Name, export.Name)
                    && existing.Version == export.Version))
                {
                    return;
                }

                exports.Add(export);
                qmlName ??= export.Name;
                moduleUri ??= export.Module;
            }

            private void ApplySupplementalScalar<T>(
                T? currentValue,
                T? incomingValue,
                Action<T?> assign,
                string description,
                string sourcePath,
                List<RegistryDiagnostic> diagnostics)
                where T : struct
            {
                if (incomingValue is null)
                {
                    return;
                }

                if (currentValue is null)
                {
                    assign(incomingValue);
                    return;
                }

                if (!EqualityComparer<T>.Default.Equals(currentValue.Value, incomingValue.Value))
                {
                    ReportConflict(QualifiedName, description, currentValue.Value.ToString()!, incomingValue.Value.ToString()!, sourcePath, diagnostics);
                }
            }

            private void ApplySupplementalScalar(
                string? currentValue,
                string? incomingValue,
                Action<string?> assign,
                string description,
                string sourcePath,
                List<RegistryDiagnostic> diagnostics)
            {
                if (string.IsNullOrWhiteSpace(incomingValue))
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(currentValue))
                {
                    assign(incomingValue);
                    return;
                }

                if (!StringComparer.Ordinal.Equals(currentValue, incomingValue))
                {
                    ReportConflict(QualifiedName, description, currentValue, incomingValue, sourcePath, diagnostics);
                }
            }

            private ExportSpec? GetBestExport()
            {
                return exports
                    .OrderByDescending(export => export.Version.Major)
                    .ThenByDescending(export => export.Version.Minor)
                    .ThenBy(export => export.Module, StringComparer.Ordinal)
                    .ThenBy(export => export.Name, StringComparer.Ordinal)
                    .Cast<ExportSpec?>()
                    .FirstOrDefault();
            }

            private void MergePrimaryEnum(EnumSeed incoming, string sourcePath, List<RegistryDiagnostic> diagnostics)
            {
                if (enumsByName.TryGetValue(incoming.Name, out EnumSeed? existing))
                {
                    if (!existing.Equals(incoming))
                    {
                        ReportConflict(QualifiedName, $"enum '{incoming.Name}'", existing.Name, incoming.Name, sourcePath, diagnostics);
                    }

                    return;
                }

                enumsByName[incoming.Name] = incoming;
            }

            private void MergePrimaryMethod(MethodSeed incoming, ITypeNameMapper typeNameMapper)
            {
                string key = BuildMethodKey(incoming, typeNameMapper);
                if (methodKeys.Add(key))
                {
                    methods.Add(incoming);
                }
            }

            private void MergePrimaryProperty(
                PropertySeed incoming,
                string sourcePath,
                ITypeNameMapper typeNameMapper,
                List<RegistryDiagnostic> diagnostics)
            {
                if (propertiesByName.TryGetValue(incoming.Name, out PropertySeed? existing))
                {
                    if (!existing.IsEquivalentTo(incoming, typeNameMapper))
                    {
                        ReportConflict(
                            QualifiedName,
                            $"property '{incoming.Name}'",
                            TypeNormalizer.CanonicalizeType(existing.RawTypeName, typeNameMapper),
                            TypeNormalizer.CanonicalizeType(incoming.RawTypeName, typeNameMapper),
                            sourcePath,
                            diagnostics);
                    }

                    propertiesByName[incoming.Name] = ChooseConflictWinner(existing, incoming);
                    return;
                }

                propertiesByName[incoming.Name] = incoming;
                propertyOrder.Add(incoming);
            }

            private void MergePrimarySignal(SignalSeed incoming, ITypeNameMapper typeNameMapper)
            {
                string key = BuildSignalKey(incoming, typeNameMapper);
                if (signalKeys.Add(key))
                {
                    signals.Add(incoming);
                }
            }

            private void MergeSupplementalEnum(EnumSeed incoming, string sourcePath, List<RegistryDiagnostic> diagnostics)
            {
                if (enumsByName.TryGetValue(incoming.Name, out EnumSeed? existing))
                {
                    if (!existing.Equals(incoming))
                    {
                        ReportConflict(QualifiedName, $"enum '{incoming.Name}'", existing.Name, incoming.Name, sourcePath, diagnostics);
                    }

                    return;
                }

                enumsByName[incoming.Name] = incoming;
            }

            private void MergeSupplementalMethod(MethodSeed incoming, ITypeNameMapper typeNameMapper)
            {
                string key = BuildMethodKey(incoming, typeNameMapper);
                if (methodKeys.Add(key))
                {
                    methods.Add(incoming);
                }
            }

            private void MergeSupplementalProperty(
                PropertySeed incoming,
                string sourcePath,
                ITypeNameMapper typeNameMapper,
                List<RegistryDiagnostic> diagnostics)
            {
                if (propertiesByName.TryGetValue(incoming.Name, out PropertySeed? existing))
                {
                    if (!existing.IsEquivalentTo(incoming, typeNameMapper))
                    {
                        ReportConflict(
                            QualifiedName,
                            $"property '{incoming.Name}'",
                            TypeNormalizer.CanonicalizeType(existing.RawTypeName, typeNameMapper),
                            TypeNormalizer.CanonicalizeType(incoming.RawTypeName, typeNameMapper),
                            sourcePath,
                            diagnostics);
                    }

                    return;
                }

                propertiesByName[incoming.Name] = incoming;
                propertyOrder.Add(incoming);
            }

            private void MergeSupplementalSignal(SignalSeed incoming, ITypeNameMapper typeNameMapper)
            {
                string key = BuildSignalKey(incoming, typeNameMapper);
                if (signalKeys.Add(key))
                {
                    signals.Add(incoming);
                }
            }
        }

        private sealed record EnumSeed(
            string Name,
            bool IsFlag,
            ImmutableArray<string> Values)
        {
            public QmlEnum Build()
            {
                return new QmlEnum(
                    Name,
                    IsFlag,
                    Values.Select((value, index) => new QmlEnumValue(value, index)).ToImmutableArray());
            }
        }

        private sealed record MethodSeed(
            string Name,
            string? RawReturnType,
            ImmutableArray<ParameterSeed> Parameters)
        {
            public QmlMethod Build(IReadOnlyDictionary<string, TypeNameInfo> typeNames, ITypeNameMapper typeNameMapper)
            {
                return new QmlMethod(
                    Name,
                    RawReturnType is null ? null : ParameterSeed.ResolveTypeName(RawReturnType, typeNames, typeNameMapper),
                    Parameters.Select(parameter => parameter.Build(typeNames, typeNameMapper)).ToImmutableArray());
            }
        }

        private sealed record ParameterSeed(string Name, string RawTypeName)
        {
            public QmlParameter Build(IReadOnlyDictionary<string, TypeNameInfo> typeNames, ITypeNameMapper typeNameMapper)
            {
                return new QmlParameter(Name, ResolveTypeName(RawTypeName, typeNames, typeNameMapper));
            }

            public static string ResolveTypeName(
                string rawTypeName,
                IReadOnlyDictionary<string, TypeNameInfo> typeNames,
                ITypeNameMapper typeNameMapper)
            {
                string normalizedTypeName = NormalizeTypeReference(rawTypeName);
                if (typeNames.TryGetValue(normalizedTypeName, out TypeNameInfo typeNameInfo)
                    && !string.IsNullOrWhiteSpace(typeNameInfo.QmlName))
                {
                    return typeNameInfo.QmlName!;
                }

                return typeNameMapper.ToQmlName(normalizedTypeName);
            }
        }

        private sealed record PropertySeed(
            string Name,
            string RawTypeName,
            bool IsReadonly,
            bool IsList,
            bool IsRequired,
            string? DefaultValue,
            string? NotifySignal)
        {
            public QmlProperty Build(IReadOnlyDictionary<string, TypeNameInfo> typeNames, ITypeNameMapper typeNameMapper)
            {
                return new QmlProperty(
                    Name,
                    ParameterSeed.ResolveTypeName(RawTypeName, typeNames, typeNameMapper),
                    IsReadonly,
                    IsList,
                    IsRequired,
                    DefaultValue,
                    NotifySignal);
            }

            public bool IsEquivalentTo(PropertySeed other, ITypeNameMapper typeNameMapper)
            {
                return StringComparer.Ordinal.Equals(Name, other.Name)
                    && StringComparer.Ordinal.Equals(TypeNormalizer.CanonicalizeType(RawTypeName, typeNameMapper), TypeNormalizer.CanonicalizeType(other.RawTypeName, typeNameMapper))
                    && IsReadonly == other.IsReadonly
                    && IsList == other.IsList
                    && IsRequired == other.IsRequired
                    && StringComparer.Ordinal.Equals(NotifySignal, other.NotifySignal);
            }
        }

        private sealed record SignalSeed(
            string Name,
            ImmutableArray<ParameterSeed> Parameters)
        {
            public QmlSignal Build(IReadOnlyDictionary<string, TypeNameInfo> typeNames, ITypeNameMapper typeNameMapper)
            {
                return new QmlSignal(
                    Name,
                    Parameters.Select(parameter => parameter.Build(typeNames, typeNameMapper)).ToImmutableArray());
            }
        }
    }
}
