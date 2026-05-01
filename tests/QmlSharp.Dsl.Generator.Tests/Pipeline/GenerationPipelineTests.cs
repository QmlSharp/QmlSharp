using QmlSharp.Dsl.Generator.Tests.Fixtures;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Pipeline
{
    public sealed class GenerationPipelineTests
    {
        [Fact]
        public async Task GenerateType_Rectangle_ReturnsGeneratedTypeCode()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();

            GeneratedTypeCode result = await pipeline.GenerateType(registry, "QQuickRectangle", DslTestFixtures.DefaultOptions);

            Assert.Equal("Rectangle", result.QmlName);
            Assert.Equal("IRectangleBuilder", result.BuilderInterfaceName);
            Assert.NotEmpty(result.Properties);
            Assert.NotEmpty(result.Signals);
            Assert.NotNull(result.FactoryMethodCode);
        }

        [Fact]
        public async Task GenerateModule_QtQuick_ReturnsDeterministicPackage()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();

            GeneratedPackage package = await pipeline.GenerateModule(registry, "QtQuick", DslTestFixtures.DefaultOptions);

            Assert.Equal("QmlSharp.QtQuick", package.PackageName);
            Assert.Equal("QtQuick", package.ModuleUri);
            Assert.Equal(3, package.Types);
            Assert.Equal(package.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray(), package.Files.ToArray());
        }

        [Fact]
        public async Task Generate_ReexportModule_PackagesQualifiedModuleMembers()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            QmlType rectangle = CreateType("QQuickRectangle", "Rectangle", "QtQuick", null, isCreatable: true);
            QmlModule reexportModule = CreateModule("QtQuick.Reexports", rectangle);
            IRegistryQuery registry = new TestRegistryQuery([reexportModule], [rectangle], "6.11.0");

            GenerationResult result = await pipeline.Generate(registry, DslTestFixtures.DefaultOptions);

            GeneratedPackage package = Assert.Single(result.Packages);
            Assert.Equal("QtQuick.Reexports", package.ModuleUri);
            Assert.Equal("QmlSharp.QtQuick.Reexports", package.PackageName);
            Assert.Contains(package.Files, file => file.RelativePath == "Rectangle.cs");
            Assert.DoesNotContain(result.Warnings, warning => warning.Code == GenerationWarningCode.EmptyModule);
        }

        [Fact]
        public async Task GenerateType_CrossModuleNameCollision_UsesPrecomputedQualifiedName()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            QmlType quickButton = CreateType("QQuickButton", "Button", "QtQuick", null, isCreatable: true);
            QmlType controlsButton = CreateType("QQuickControlsButton", "Button", "QtQuick.Controls", null, isCreatable: true);
            IRegistryQuery registry = new TestRegistryQuery(
                [CreateModule("QtQuick", quickButton), CreateModule("QtQuick.Controls", controlsButton)],
                [quickButton, controlsButton],
                "6.11.0");

            GeneratedTypeCode result = await pipeline.GenerateType(registry, "QQuickControlsButton", DslTestFixtures.DefaultOptions);

            Assert.Equal("QtQuickControlsButton", result.FactoryName);
            Assert.Equal("IQtQuickControlsButtonBuilder", result.BuilderInterfaceName);
        }

        [Fact]
        public async Task Generate_P0Modules_ReturnsFourPackages()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            IRegistryQuery registry = DslTestFixtures.CreateP0Fixture();

            GenerationResult result = await pipeline.Generate(registry, DslTestFixtures.DefaultOptions);

            Assert.Equal(4, result.Packages.Length);
            Assert.Equal(
                ["QtQml", "QtQuick", "QtQuick.Controls", "QtQuick.Layouts"],
                result.Packages.Select(package => package.ModuleUri).ToArray());
        }

        [Fact]
        public async Task Generate_ProgressCallback_ObservesEveryPhase()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            List<GenerationPhase> phases = [];
            pipeline.OnProgress(progress => phases.Add(progress.Phase));

            _ = await pipeline.Generate(DslTestFixtures.CreateMinimalFixture(), DslTestFixtures.DefaultOptions);

            foreach (GenerationPhase phase in Enum.GetValues<GenerationPhase>())
            {
                Assert.Contains(phase, phases);
            }
        }

        [Fact]
        public async Task Generate_CreatableOnly_ExcludesNonCreatableTypes()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            IRegistryQuery registry = CreateFilterFixture(CreateType("QQuickHelper", "Helper", "QtQuick", null, isCreatable: false));
            GenerationOptions options = WithFilter(DslTestFixtures.DefaultOptions.Filter with { CreatableOnly = true, ExcludeInternal = false });

            GenerationResult result = await pipeline.Generate(registry, options);

            Assert.DoesNotContain(result.Packages.SelectMany(package => package.Files), file => file.RelativePath == "Helper.cs");
            Assert.Contains(result.SkippedTypes, skipped => skipped.TypeName == "QQuickHelper");
        }

        [Fact]
        public async Task Generate_ExcludeDeprecated_AddsWarningAndSkippedType()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            IRegistryQuery registry = CreateFilterFixture(CreateType("QQuickDeprecatedThing", "DeprecatedThing", "QtQuick", null, isCreatable: true));

            GenerationResult result = await pipeline.Generate(registry, DslTestFixtures.DefaultOptions);

            Assert.Contains(result.Warnings, warning => warning.Code == GenerationWarningCode.DeprecatedType);
            Assert.Contains(result.SkippedTypes, skipped => skipped.Reason.Contains(DslDiagnosticCodes.DeprecatedType, StringComparison.Ordinal));
        }

        [Fact]
        public async Task Generate_ExcludeInternal_ExcludesInternalTypes()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            IRegistryQuery registry = CreateFilterFixture(CreateType("QQuickPrivateHelper", "PrivateHelper", "QtQuick.Private", null, isCreatable: true));

            GenerationResult result = await pipeline.Generate(registry, DslTestFixtures.DefaultOptions);

            Assert.Empty(result.Packages);
            Assert.Contains(result.SkippedTypes, skipped => skipped.TypeName == "QQuickPrivateHelper");
        }

        [Fact]
        public async Task Generate_ExplicitExcludeType_ExcludesNamedType()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            GenerationOptions options = WithFilter(DslTestFixtures.DefaultOptions.Filter with { ExcludeTypes = ["Rectangle"] });

            GenerationResult result = await pipeline.Generate(DslTestFixtures.CreateMinimalFixture(), options);

            Assert.DoesNotContain(result.Packages.SelectMany(package => package.Files), file => file.RelativePath == "Rectangle.cs");
            Assert.Contains(result.SkippedTypes, skipped => skipped.TypeName == "QQuickRectangle");
        }

        [Fact]
        public async Task Generate_VersionRange_ExcludesOutOfRangeType()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            GenerationOptions options = WithFilter(DslTestFixtures.DefaultOptions.Filter with
            {
                ExcludeInternal = false,
                VersionRange = new QmlVersionRange(new QmlVersion(2, 16), null),
            });

            GenerationResult result = await pipeline.Generate(DslTestFixtures.CreateMinimalFixture(), options);

            Assert.Empty(result.Packages);
            Assert.NotEmpty(result.SkippedTypes);
        }

        [Fact]
        public async Task Generate_UnresolvedBaseType_ProducesWarningAndSkipsType()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            IRegistryQuery registry = CreateFilterFixture(CreateType("BrokenType", "Broken", "QtQuick", "MissingBase", isCreatable: true));
            GenerationOptions options = WithFilter(DslTestFixtures.DefaultOptions.Filter with { ExcludeInternal = false });

            GenerationResult result = await pipeline.Generate(registry, options);

            Assert.Contains(result.Warnings, warning => warning.Code == GenerationWarningCode.UnresolvedBaseType);
            Assert.Contains(result.SkippedTypes, skipped => skipped.TypeName == "BrokenType");
        }

        [Fact]
        public async Task Generate_EmptyModule_ProducesWarning()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            IRegistryQuery registry = new TestRegistryQuery(
                [CreateModule("QtQuick.Empty")],
                [],
                "6.11.0");

            GenerationResult result = await pipeline.Generate(registry, DslTestFixtures.DefaultOptions);

            Assert.Empty(result.Packages);
            Assert.Contains(result.Warnings, warning => warning.Code == GenerationWarningCode.EmptyModule);
        }

        [Fact]
        public async Task Generate_Stats_ArePopulated()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();

            GenerationResult result = await pipeline.Generate(DslTestFixtures.CreateMinimalFixture(), DslTestFixtures.DefaultOptions);

            Assert.Equal(1, result.Stats.TotalPackages);
            Assert.True(result.Stats.TotalTypes > 0);
            Assert.True(result.Stats.TotalFiles > 0);
            Assert.True(result.Stats.TotalBytes > 0);
            Assert.True(result.Stats.ElapsedTime >= TimeSpan.Zero);
        }

        [Fact]
        public async Task Generate_RepeatedRuns_AreDeterministic()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            IRegistryQuery registry = DslTestFixtures.CreateP0Fixture();

            GenerationResult first = await pipeline.Generate(registry, DslTestFixtures.DefaultOptions);
            GenerationResult second = await pipeline.Generate(registry, DslTestFixtures.DefaultOptions);

            Assert.Equal(SerializePackages(first.Packages), SerializePackages(second.Packages));
            Assert.Equal(first.SkippedTypes, second.SkippedTypes);
            Assert.Equal(first.Warnings, second.Warnings);
        }

        [Fact]
        public async Task Generate_Warnings_CoverEveryWarningCode()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            IRegistryQuery registry = CreateWarningFixture();
            GenerationOptions options = WithFilter(DslTestFixtures.DefaultOptions.Filter with { ExcludeInternal = false });

            GenerationResult result = await pipeline.Generate(registry, options);

            foreach (GenerationWarningCode code in Enum.GetValues<GenerationWarningCode>())
            {
                Assert.Contains(result.Warnings, warning => warning.Code == code);
            }
        }

        [Fact]
        public async Task Generate_SkippedTypeDiagnostics_IncludeDsl100AndDsl101()
        {
            IGenerationPipeline pipeline = new GenerationPipeline();
            IRegistryQuery registry = CreateWarningFixture();
            GenerationOptions options = WithFilter(DslTestFixtures.DefaultOptions.Filter with
            {
                ExcludeInternal = false,
                ExcludeTypes = ["ExplicitSkip"],
            });

            GenerationResult result = await pipeline.Generate(registry, options);

            Assert.Contains(result.SkippedTypes, skipped => skipped.Reason.Contains(DslDiagnosticCodes.SkippedType, StringComparison.Ordinal));
            Assert.Contains(result.SkippedTypes, skipped => skipped.Reason.Contains(DslDiagnosticCodes.DeprecatedType, StringComparison.Ordinal));
        }

        private static GenerationOptions WithFilter(FilterOptions filter)
        {
            return DslTestFixtures.DefaultOptions with { Filter = filter };
        }

        private static string SerializePackages(ImmutableArray<GeneratedPackage> packages)
        {
            return string.Join(
                "\n---package---\n",
                packages.Select(package =>
                    string.Join(
                        "\n",
                        package.Files.Select(file => $"{package.PackageName}/{file.RelativePath}:{file.Content}"))));
        }

        private static IRegistryQuery CreateFilterFixture(params QmlType[] types)
        {
            ImmutableArray<QmlModuleType> moduleTypes = types
                .Select(type => new QmlModuleType(
                    type.QualifiedName,
                    type.QmlName ?? type.QualifiedName,
                    new QmlVersion(2, 15)))
                .ToImmutableArray();
            QmlModule module = new(
                types.FirstOrDefault()?.ModuleUri ?? "QtQuick",
                new QmlVersion(2, 15),
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty,
                moduleTypes);

            return new TestRegistryQuery([module], types, "6.11.0");
        }

        private static IRegistryQuery CreateWarningFixture()
        {
            QmlType baseType = CreateType("QObject", "QtObject", "QtQml", null, isCreatable: true);
            QmlType duplicateQuick = CreateType("QQuickButton", "Button", "QtQuick", "QObject", isCreatable: true);
            QmlType duplicateControls = CreateType("QQuickControlsButton", "Button", "QtQuick.Controls", "QObject", isCreatable: true);
            QmlType unresolvedBase = CreateType("BrokenType", "Broken", "QtQuick", "MissingBase", isCreatable: true);
            QmlType unresolvedReference = CreateType(
                "UnknownPropertyType",
                "UnknownPropertyType",
                "QtQuick",
                "QObject",
                isCreatable: true,
                properties: [CreateProperty("mystery", "MissingThing")]);
            QmlType unsupported = CreateType(
                "UnsupportedPropertyType",
                "UnsupportedPropertyType",
                "QtQuick",
                "QObject",
                isCreatable: true,
                properties: [CreateProperty("bad", "void")]);
            QmlType deprecated = CreateType("DeprecatedThing", "DeprecatedThing", "QtQuick", "QObject", isCreatable: true);
            QmlType skipped = CreateType("ExplicitSkip", "ExplicitSkip", "QtQuick", "QObject", isCreatable: true);
            QmlType circularA = CreateType("CircularA", "CircularA", "QtQuick.Circular", "CircularB", isCreatable: true);
            QmlType circularB = CreateType("CircularB", "CircularB", "QtQuick.Circular", "CircularA", isCreatable: true);

            QmlModule qtQml = CreateModule("QtQml", baseType);
            QmlModule qtQuick = CreateModule(
                "QtQuick",
                duplicateQuick,
                unresolvedBase,
                unresolvedReference,
                unsupported,
                deprecated,
                skipped);
            QmlModule controls = CreateModule("QtQuick.Controls", duplicateControls);
            QmlModule circular = CreateModule("QtQuick.Circular", circularA, circularB);
            QmlModule empty = CreateModule("QtQuick.Empty");

            return new TestRegistryQuery(
                [qtQml, qtQuick, controls, circular, empty],
                [baseType, duplicateQuick, duplicateControls, unresolvedBase, unresolvedReference, unsupported, deprecated, skipped, circularA, circularB],
                "6.11.0");
        }

        private static QmlModule CreateModule(string uri, params QmlType[] types)
        {
            return new QmlModule(
                uri,
                new QmlVersion(2, 15),
                ImmutableArray<string>.Empty,
                ImmutableArray<string>.Empty,
                types
                    .Select(type => new QmlModuleType(type.QualifiedName, type.QmlName ?? type.QualifiedName, new QmlVersion(2, 15)))
                    .ToImmutableArray());
        }

        private static QmlType CreateType(
            string qualifiedName,
            string? qmlName,
            string? moduleUri,
            string? prototype,
            bool isCreatable,
            ImmutableArray<QmlProperty>? properties = null)
        {
            return new QmlType(
                QualifiedName: qualifiedName,
                QmlName: qmlName,
                ModuleUri: moduleUri,
                AccessSemantics: AccessSemantics.Reference,
                Prototype: prototype,
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: isCreatable,
                Exports: moduleUri is null || qmlName is null ? ImmutableArray<QmlTypeExport>.Empty : [new QmlTypeExport(moduleUri, qmlName, new QmlVersion(2, 15))],
                Properties: properties ?? ImmutableArray<QmlProperty>.Empty,
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }

        private static QmlProperty CreateProperty(string name, string typeName)
        {
            return new QmlProperty(
                Name: name,
                TypeName: typeName,
                IsReadonly: false,
                IsList: false,
                IsRequired: false,
                DefaultValue: null,
                NotifySignal: null);
        }
    }
}
