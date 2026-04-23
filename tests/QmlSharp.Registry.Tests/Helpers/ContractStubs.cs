using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Normalization;
using QmlSharp.Registry.Parsing;
using QmlSharp.Registry.Querying;
using QmlSharp.Registry.Scanning;
using QmlSharp.Registry.Snapshots;

namespace QmlSharp.Registry.Tests.Helpers
{
    internal sealed class StubQtTypeScanner : IQtTypeScanner
    {
        public string? InferModuleUri(string qmldirPath, string qmlRootDir)
        {
            return "QtQuick";
        }

        public ScanResult Scan(ScannerConfig config)
        {
            return new ScanResult(
                QmltypesPaths: [@"C:\Qt\qml\QtQuick\plugins.qmltypes"],
                QmldirPaths: [@"C:\Qt\qml\QtQuick\qmldir"],
                MetatypesPaths: [@"C:\Qt\lib\metatypes\qtquick_metatypes.json"],
                Diagnostics: ImmutableArray<RegistryDiagnostic>.Empty);
        }

        public ScanValidation ValidateQtDir(string qtDir)
        {
            return new ScanValidation(IsValid: true, QtVersion: "6.11.0", ErrorMessage: null);
        }
    }

    internal sealed class StubQmltypesParser : IQmltypesParser
    {
        public ParseResult<RawQmltypesFile> Parse(string filePath)
        {
            return new ParseResult<RawQmltypesFile>(RawAstFixtures.CreateQmltypesFile(), ImmutableArray<RegistryDiagnostic>.Empty);
        }

        public ParseResult<RawQmltypesFile> ParseContent(string content, string sourcePath)
        {
            return new ParseResult<RawQmltypesFile>(RawAstFixtures.CreateQmltypesFile(), ImmutableArray<RegistryDiagnostic>.Empty);
        }
    }

    internal sealed class StubQmldirParser : IQmldirParser
    {
        public ParseResult<RawQmldirFile> Parse(string filePath)
        {
            return new ParseResult<RawQmldirFile>(RawAstFixtures.CreateQmldirFile(), ImmutableArray<RegistryDiagnostic>.Empty);
        }

        public ParseResult<RawQmldirFile> ParseContent(string content, string sourcePath)
        {
            return new ParseResult<RawQmldirFile>(RawAstFixtures.CreateQmldirFile(), ImmutableArray<RegistryDiagnostic>.Empty);
        }
    }

    internal sealed class StubMetatypesParser : IMetatypesParser
    {
        public ParseResult<RawMetatypesFile> Parse(string filePath)
        {
            return new ParseResult<RawMetatypesFile>(RawAstFixtures.CreateMetatypesFile(), ImmutableArray<RegistryDiagnostic>.Empty);
        }

        public ParseResult<RawMetatypesFile> ParseContent(string content, string sourcePath)
        {
            return new ParseResult<RawMetatypesFile>(RawAstFixtures.CreateMetatypesFile(), ImmutableArray<RegistryDiagnostic>.Empty);
        }
    }

    internal sealed class StubTypeNameMapper : ITypeNameMapper
    {
        private readonly IReadOnlyDictionary<string, string> customMappings;

        public StubTypeNameMapper()
            : this(new Dictionary<string, string>(StringComparer.Ordinal))
        {
        }

        private StubTypeNameMapper(IReadOnlyDictionary<string, string> customMappings)
        {
            this.customMappings = customMappings;
        }

        public IReadOnlyDictionary<string, string> GetAllMappings()
        {
            return customMappings;
        }

        public bool HasMapping(string cppTypeName)
        {
            return customMappings.ContainsKey(cppTypeName);
        }

        public string ToCppName(string qmlTypeName)
        {
            return customMappings
                .FirstOrDefault(pair => StringComparer.Ordinal.Equals(pair.Value, qmlTypeName))
                .Key ?? qmlTypeName;
        }

        public string ToQmlName(string cppTypeName)
        {
            return customMappings.TryGetValue(cppTypeName, out string? qmlName)
                ? qmlName
                : cppTypeName;
        }

        public ITypeNameMapper WithCustomMappings(IReadOnlyDictionary<string, string> newMappings)
        {
            return new StubTypeNameMapper(new Dictionary<string, string>(newMappings, StringComparer.Ordinal));
        }
    }

    internal sealed class StubTypeNormalizer : ITypeNormalizer
    {
        public NormalizeResult Normalize(
            IReadOnlyList<RawQmltypesFile> qmltypesFiles,
            IReadOnlyList<(string ModuleUri, RawQmldirFile File)> qmldirFiles,
            IReadOnlyList<RawMetatypesFile> metatypesFiles,
            ITypeNameMapper typeNameMapper)
        {
            return new NormalizeResult(RegistryFixtures.CreateMinimalInheritanceFixture(), ImmutableArray<RegistryDiagnostic>.Empty);
        }
    }

    internal sealed class StubTypeRegistry : ITypeRegistry
    {
        public StubTypeRegistry(QmlRegistry registry)
        {
            Registry = registry;
        }

        public int FormatVersion => Registry.FormatVersion;

        public IReadOnlyList<QmlModule> Modules => Registry.Modules;

        public QmlRegistry Registry { get; }

        public string QtVersion => Registry.QtVersion;

        public IReadOnlyList<QmlType> Types => Registry.TypesByQualifiedName.Values.ToImmutableArray();
    }

    internal sealed class StubRegistryQuery : IRegistryQuery
    {
        private readonly FrozenDictionary<string, QmlModule> modulesByUri;
        private readonly QmlRegistry registry;
        private readonly FrozenDictionary<(string ModuleUri, string QmlName), QmlType> typesByModuleAndQmlName;

        public StubRegistryQuery(QmlRegistry registry)
        {
            this.registry = registry;
            modulesByUri = registry.Modules.ToFrozenDictionary(module => module.Uri, StringComparer.Ordinal);
            typesByModuleAndQmlName = registry.TypesByQualifiedName.Values
                .Where(type => type.ModuleUri is not null && type.QmlName is not null)
                .ToFrozenDictionary(
                    type => (type.ModuleUri!, type.QmlName!),
                    type => type,
                    EqualityComparer<(string ModuleUri, string QmlName)>.Default);
        }

        public QmlModule? FindModule(string moduleUri)
        {
            return modulesByUri.GetValueOrDefault(moduleUri);
        }

        public ResolvedProperty? FindProperty(string qualifiedName, string propertyName)
        {
            return GetAllProperties(qualifiedName)
                .FirstOrDefault(property => StringComparer.Ordinal.Equals(property.Property.Name, propertyName));
        }

        public ResolvedSignal? FindSignal(string qualifiedName, string signalName)
        {
            return GetAllSignals(qualifiedName)
                .FirstOrDefault(signal => StringComparer.Ordinal.Equals(signal.Signal.Name, signalName));
        }

        public IReadOnlyList<ResolvedMethod> FindMethods(string qualifiedName, string methodName)
        {
            return GetAllMethods(qualifiedName)
                .Where(method => StringComparer.Ordinal.Equals(method.Method.Name, methodName))
                .ToImmutableArray();
        }

        public QmlType? FindTypeByQualifiedName(string qualifiedName)
        {
            return registry.TypesByQualifiedName.GetValueOrDefault(qualifiedName);
        }

        public QmlType? FindTypeByQmlName(string moduleUri, string qmlName)
        {
            return typesByModuleAndQmlName.GetValueOrDefault((moduleUri, qmlName));
        }

        public IReadOnlyList<QmlType> FindTypes(Func<QmlType, bool> predicate)
        {
            return registry.TypesByQualifiedName.Values.Where(predicate).ToImmutableArray();
        }

        public IReadOnlyList<QmlModule> GetAllModules()
        {
            return registry.Modules;
        }

        public IReadOnlyList<ResolvedMethod> GetAllMethods(string qualifiedName)
        {
            return GetInheritanceChain(qualifiedName)
                .SelectMany((type, index) => type.Methods.Select(method => new ResolvedMethod(method, type, index > 0)))
                .ToImmutableArray();
        }

        public IReadOnlyList<ResolvedProperty> GetAllProperties(string qualifiedName)
        {
            return GetInheritanceChain(qualifiedName)
                .SelectMany((type, index) => type.Properties.Select(property => new ResolvedProperty(property, type, index > 0)))
                .ToImmutableArray();
        }

        public IReadOnlyList<ResolvedSignal> GetAllSignals(string qualifiedName)
        {
            return GetInheritanceChain(qualifiedName)
                .SelectMany((type, index) => type.Signals.Select(signal => new ResolvedSignal(signal, type, index > 0)))
                .ToImmutableArray();
        }

        public IReadOnlyList<QmlType> GetAttachedTypes()
        {
            return registry.TypesByQualifiedName.Values.Where(type => type.AttachedType is not null).ToImmutableArray();
        }

        public IReadOnlyList<QmlType> GetCreatableTypes()
        {
            return registry.TypesByQualifiedName.Values
                .Where(type => type.AccessSemantics == AccessSemantics.Reference && type.Exports.Length > 0)
                .ToImmutableArray();
        }

        public IReadOnlyList<QmlType> GetInheritanceChain(string qualifiedName)
        {
            ImmutableArray<QmlType>.Builder results = ImmutableArray.CreateBuilder<QmlType>();
            QmlType? current = FindTypeByQualifiedName(qualifiedName);

            while (current is not null)
            {
                results.Add(current);
                current = current.Prototype is null ? null : FindTypeByQualifiedName(current.Prototype);
            }

            return results.ToImmutable();
        }

        public IReadOnlyList<QmlType> GetModuleTypes(string moduleUri)
        {
            return registry.TypesByQualifiedName.Values
                .Where(type => StringComparer.Ordinal.Equals(type.ModuleUri, moduleUri))
                .ToImmutableArray();
        }

        public IReadOnlyList<QmlType> GetSequenceTypes()
        {
            return registry.TypesByQualifiedName.Values
                .Where(type => type.AccessSemantics == AccessSemantics.Sequence)
                .ToImmutableArray();
        }

        public IReadOnlyList<QmlType> GetSingletonTypes()
        {
            return registry.TypesByQualifiedName.Values.Where(type => type.IsSingleton).ToImmutableArray();
        }

        public IReadOnlyList<QmlType> GetValueTypes()
        {
            return registry.TypesByQualifiedName.Values
                .Where(type => type.AccessSemantics == AccessSemantics.Value)
                .ToImmutableArray();
        }

        public bool InheritsFrom(string qualifiedName, string baseQualifiedName)
        {
            return GetInheritanceChain(qualifiedName)
                .Any(type => StringComparer.Ordinal.Equals(type.QualifiedName, baseQualifiedName));
        }
    }

    internal sealed class StubRegistrySnapshot : IRegistrySnapshot
    {
        public SnapshotValidity CheckValidity(string filePath)
        {
            return new SnapshotValidity(
                IsValid: true,
                FormatVersion: 1,
                QtVersion: "6.11.0",
                BuildTimestamp: DateTimeOffset.UtcNow,
                ErrorMessage: null);
        }

        public QmlRegistry Deserialize(byte[] data)
        {
            return RegistryFixtures.CreateMinimalInheritanceFixture();
        }

        public QmlRegistry LoadFromFile(string filePath)
        {
            return RegistryFixtures.CreateMinimalInheritanceFixture();
        }

        public void SaveToFile(QmlRegistry registry, string filePath)
        {
        }

        public byte[] Serialize(QmlRegistry registry)
        {
            return [1, 2, 3];
        }
    }

    internal sealed class StubRegistryBuilder : IRegistryBuilder
    {
        public BuildResult Build(BuildConfig config, Action<BuildProgress>? progress = null)
        {
            QmlRegistry registry = RegistryFixtures.CreateMinimalInheritanceFixture();
            return new BuildResult(new StubTypeRegistry(registry), new StubRegistryQuery(registry), ImmutableArray<RegistryDiagnostic>.Empty);
        }

        public BuildResult BuildOrLoad(BuildConfig config, Action<BuildProgress>? progress = null)
        {
            return Build(config, progress);
        }

        public BuildResult LoadFromSnapshot(string snapshotPath)
        {
            QmlRegistry registry = RegistryFixtures.CreateMinimalInheritanceFixture();
            return new BuildResult(new StubTypeRegistry(registry), new StubRegistryQuery(registry), ImmutableArray<RegistryDiagnostic>.Empty);
        }
    }
}
