using System.Reflection;
using System.Xml.Linq;
using QmlSharp.Dsl.Generator.Tests.Fixtures;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

#pragma warning disable IDE0058

namespace QmlSharp.Dsl.Generator.Tests.Closure
{
    public sealed class DslGeneratorClosureTests
    {
        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void TestSpecIds_AllEnumeratedDslGeneratorIds_MapToImplementedTests()
        {
            IReadOnlyDictionary<string, string> idToTestMethod = CreateTestSpecMap();
            HashSet<string> implementedMethods = GetImplementedTestMethodNames();

            string[] missingMethods = idToTestMethod
                .Where(pair => !implementedMethods.Contains(pair.Value))
                .Select(static pair => $"{pair.Key} -> {pair.Value}")
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Empty(missingMethods);
            Assert.Equal(114, idToTestMethod.Keys.Count(static id => !id.StartsWith("CE-G", StringComparison.Ordinal)));
            Assert.Equal(3, idToTestMethod.Keys.Count(static id => id.StartsWith("CE-G", StringComparison.Ordinal)));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void PublicGeneratorInterfaces_AllMethods_AreRepresentedInClosureMap()
        {
            string[] expected =
            [
                "IAttachedPropGenerator.Generate",
                "IAttachedPropGenerator.GetAllAttachedTypes",
                "ICodeEmitter.EmitCommonTypes",
                "ICodeEmitter.EmitIndexFile",
                "ICodeEmitter.EmitProjectFile",
                "ICodeEmitter.EmitReadme",
                "ICodeEmitter.EmitTypeFile",
                "ICodeEmitter.EmitViewModelHelpers",
                "IDefaultPropertyHandler.Analyze",
                "IDefaultPropertyHandler.GenerateMethods",
                "IEnumGenerator.Generate",
                "IEnumGenerator.GenerateAll",
                "IGenerationPipeline.Generate",
                "IGenerationPipeline.GenerateModule",
                "IGenerationPipeline.GenerateType",
                "IGenerationPipeline.OnProgress",
                "IInheritanceResolver.GetDirectSubtypes",
                "IInheritanceResolver.GetInheritanceChain",
                "IInheritanceResolver.IsSubtypeOf",
                "IInheritanceResolver.Resolve",
                "IInheritanceResolver.ResolveModule",
                "IMethodGenerator.Generate",
                "IMethodGenerator.GenerateAll",
                "IModuleMapper.GetAllMappings",
                "IModuleMapper.GetPriority",
                "IModuleMapper.ToModuleUri",
                "IModuleMapper.ToNamespace",
                "IModuleMapper.ToPackageName",
                "IModulePackager.PackageAll",
                "IModulePackager.PackageModule",
                "IModulePackager.WritePackage",
                "INameRegistry.IsReservedWord",
                "INameRegistry.RegisterEnumName",
                "INameRegistry.RegisterMethodName",
                "INameRegistry.RegisterPropertyName",
                "INameRegistry.RegisterTypeName",
                "INameRegistry.ToSafeIdentifier",
                "IPropGenerator.DetectGroupedProperties",
                "IPropGenerator.Generate",
                "IPropGenerator.GenerateAll",
                "ISignalGenerator.Generate",
                "ISignalGenerator.GenerateAll",
                "ITypeMapper.GetAllMappings",
                "ITypeMapper.GetMapping",
                "ITypeMapper.GetParameterType",
                "ITypeMapper.GetReturnType",
                "ITypeMapper.GetSetterType",
                "ITypeMapper.MapListType",
                "ITypeMapper.MapToCSharp",
                "ITypeMapper.RegisterCustomMapping",
                "IViewModelIntegration.AnalyzeSchema",
                "IViewModelIntegration.GenerateBindingHelpers",
                "IViewModelIntegration.GenerateProxyType",
            ];

            string[] actual = typeof(ITypeMapper).Assembly
                .GetExportedTypes()
                .Where(static type => type is { IsInterface: true, Namespace: "QmlSharp.Dsl.Generator" })
                .SelectMany(static type => type.GetMethods().Select(method => $"{type.Name}.{method.Name}"))
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected, actual);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void GoldenFiles_AllDslClosureGoldens_ArePresentAndNonEmpty()
        {
            string goldenRoot = Path.Combine(AppContext.BaseDirectory, "testdata", "golden");

            foreach (string fileName in new[] { "Button.cs", "Rectangle.cs", "Text.cs" })
            {
                string path = Path.Combine(goldenRoot, fileName);

                Assert.True(File.Exists(path), $"Missing golden file: {fileName}");
                Assert.True(new FileInfo(path).Length > 0, $"Golden file is empty: {fileName}");
            }
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void ProductionProjectReferences_MatchStep0500Boundary()
        {
            string root = FindRepositoryRoot();

            AssertProjectReferences(
                Path.Combine(root, "src", "QmlSharp.Dsl", "QmlSharp.Dsl.csproj"),
                ["QmlSharp.Core", "QmlSharp.Qml.Ast"]);
            AssertProjectReferences(
                Path.Combine(root, "src", "QmlSharp.Dsl.Generator", "QmlSharp.Dsl.Generator.csproj"),
                ["QmlSharp.Qml.Ast", "QmlSharp.Registry"]);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public async Task GenerationPipeline_PublicEntryPoints_ValidateNullAndBlankArguments()
        {
            GenerationPipeline pipeline = new();
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();
            GenerationOptions options = DslTestFixtures.DefaultOptions;

            Assert.Throws<ArgumentNullException>(() => new GenerationPipeline(
                null!,
                new SignalGenerator(),
                new MethodGenerator(),
                new EnumGenerator(),
                new AttachedPropGenerator(),
                new DefaultPropertyHandler(),
                new ModulePackager(),
                new ModuleMapper()));
            Assert.Throws<ArgumentNullException>(() => new GenerationPipeline(
                new PropGenerator(),
                null!,
                new MethodGenerator(),
                new EnumGenerator(),
                new AttachedPropGenerator(),
                new DefaultPropertyHandler(),
                new ModulePackager(),
                new ModuleMapper()));
            Assert.Throws<ArgumentNullException>(() => new GenerationPipeline(
                new PropGenerator(),
                new SignalGenerator(),
                null!,
                new EnumGenerator(),
                new AttachedPropGenerator(),
                new DefaultPropertyHandler(),
                new ModulePackager(),
                new ModuleMapper()));
            Assert.Throws<ArgumentNullException>(() => new GenerationPipeline(
                new PropGenerator(),
                new SignalGenerator(),
                new MethodGenerator(),
                null!,
                new AttachedPropGenerator(),
                new DefaultPropertyHandler(),
                new ModulePackager(),
                new ModuleMapper()));
            Assert.Throws<ArgumentNullException>(() => new GenerationPipeline(
                new PropGenerator(),
                new SignalGenerator(),
                new MethodGenerator(),
                new EnumGenerator(),
                null!,
                new DefaultPropertyHandler(),
                new ModulePackager(),
                new ModuleMapper()));
            Assert.Throws<ArgumentNullException>(() => new GenerationPipeline(
                new PropGenerator(),
                new SignalGenerator(),
                new MethodGenerator(),
                new EnumGenerator(),
                new AttachedPropGenerator(),
                null!,
                new ModulePackager(),
                new ModuleMapper()));
            Assert.Throws<ArgumentNullException>(() => new GenerationPipeline(
                new PropGenerator(),
                new SignalGenerator(),
                new MethodGenerator(),
                new EnumGenerator(),
                new AttachedPropGenerator(),
                new DefaultPropertyHandler(),
                null!,
                new ModuleMapper()));
            Assert.Throws<ArgumentNullException>(() => new GenerationPipeline(
                new PropGenerator(),
                new SignalGenerator(),
                new MethodGenerator(),
                new EnumGenerator(),
                new AttachedPropGenerator(),
                new DefaultPropertyHandler(),
                new ModulePackager(),
                null!));
            await Assert.ThrowsAsync<ArgumentNullException>(() => pipeline.Generate(null!, options));
            await Assert.ThrowsAsync<ArgumentNullException>(() => pipeline.Generate(registry, null!));
            await Assert.ThrowsAsync<ArgumentNullException>(() => pipeline.GenerateModule(null!, "QtQuick", options));
            await Assert.ThrowsAsync<ArgumentException>(() => pipeline.GenerateModule(registry, "", options));
            await Assert.ThrowsAsync<ArgumentNullException>(() => pipeline.GenerateModule(registry, "QtQuick", null!));
            await Assert.ThrowsAsync<ArgumentNullException>(() => pipeline.GenerateType(null!, "QQuickRectangle", options));
            await Assert.ThrowsAsync<ArgumentException>(() => pipeline.GenerateType(registry, "", options));
            await Assert.ThrowsAsync<ArgumentNullException>(() => pipeline.GenerateType(registry, "QQuickRectangle", null!));
            Assert.Throws<ArgumentNullException>(() => pipeline.OnProgress(null!));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public async Task GenerationPipeline_MissingAndFilteredItems_ReportDeterministicDiagnostics()
        {
            GenerationPipeline pipeline = new();
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();
            GenerationOptions excludeEverything = DslTestFixtures.DefaultOptions with
            {
                Filter = DslTestFixtures.DefaultOptions.Filter with
                {
                    VersionRange = new QmlVersionRange(new QmlVersion(99, 0), null),
                },
            };

            DslGenerationException missingModule = await Assert.ThrowsAsync<DslGenerationException>(
                () => pipeline.GenerateModule(registry, "QtQuick.Missing", DslTestFixtures.DefaultOptions));
            DslGenerationException filteredModule = await Assert.ThrowsAsync<DslGenerationException>(
                () => pipeline.GenerateModule(registry, "QtQuick", excludeEverything));
            DslGenerationException missingType = await Assert.ThrowsAsync<DslGenerationException>(
                () => pipeline.GenerateType(registry, "MissingType", DslTestFixtures.DefaultOptions));
            DslGenerationException filteredType = await Assert.ThrowsAsync<DslGenerationException>(
                () => pipeline.GenerateType(registry, "QQuickRectangle", excludeEverything));

            Assert.Equal(DslDiagnosticCodes.EmptyModule, missingModule.DiagnosticCode);
            Assert.Equal(DslDiagnosticCodes.EmptyModule, filteredModule.DiagnosticCode);
            Assert.Equal(DslDiagnosticCodes.SkippedType, missingType.DiagnosticCode);
            Assert.Equal(DslDiagnosticCodes.SkippedType, filteredType.DiagnosticCode);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void CodeEmitter_InvalidMetadataAndFallbacks_AreCoveredForClosure()
        {
            CodeEmitter emitter = new();
            GeneratedTypeCode rectangle = DslTestFixtures.CreateGeneratedRectangleMetadata();
            QmlType ownerWithoutModule = CreateType("QQuickNoModule", "NoModule", null);
            GeneratedEnum generatedEnum = new(
                "NoModuleEnum",
                null,
                false,
                false,
                [new GeneratedEnumMember("Value", 1)],
                "public enum NoModuleEnum { Value = 1 }",
                ownerWithoutModule);

            Assert.Throws<DslGenerationException>(() => emitter.EmitTypeFile(rectangle with { ModuleUri = "" }, DslTestFixtures.DefaultEmitOptions));
            Assert.Throws<DslGenerationException>(() => emitter.EmitTypeFile(rectangle with { BuilderInterfaceName = "" }, DslTestFixtures.DefaultEmitOptions));

            string defaultHeader = emitter.EmitTypeFile(rectangle, DslTestFixtures.DefaultEmitOptions with { HeaderComment = null });
            string enumOnlyIndex = emitter.EmitIndexFile([], [generatedEnum]);

            Assert.StartsWith("// <auto-generated />", defaultHeader, StringComparison.Ordinal);
            Assert.Contains("namespace QmlSharp.Generated;", enumOnlyIndex, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void ModuleMapper_FallbacksAndValidation_AreCoveredForClosure()
        {
            ModuleMapper mapper = new("QmlSharp");

            Assert.Equal("Custom.Module", mapper.ToModuleUri("QmlSharp.Custom.Module"));
            Assert.Equal("External.Package", mapper.ToModuleUri("External.Package"));
            Assert.Equal("QmlSharp._", mapper.ToNamespace("..."));
            Assert.Equal("QmlSharp._123.Class", mapper.ToNamespace("123.class"));
            Assert.Throws<ArgumentException>(() => mapper.ToPackageName(""));
            Assert.Throws<ArgumentException>(() => mapper.ToModuleUri(""));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void MemberGenerators_UnsupportedBlankTypes_ProduceDocumentedDiagnostics()
        {
            GenerationContext context = CreateContext(DslTestFixtures.CreateMinimalFixture());
            QmlType owner = CreateType("QQuickBad", "Bad", "QtQuick");
            ResolvedType propertyType = new(
                owner,
                [owner],
                [new ResolvedProperty(new QmlProperty("bad", "", false, false, false, null, null), owner, false)],
                [],
                [],
                [],
                null,
                null);
            ResolvedType signalType = propertyType with
            {
                AllProperties = [],
                AllSignals = [new ResolvedSignal(new QmlSignal("badSignal", [new QmlParameter("value", "")]), owner)],
            };

            Assert.Throws<UnsupportedPropertyTypeException>(() => new PropGenerator().GenerateAll(propertyType, context));
            Assert.Throws<UnsupportedSignalParameterException>(() => new SignalGenerator().GenerateAll(signalType, context));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void TypeMapper_CustomMappingValidationAndListFallbacks_AreCoveredForClosure()
        {
            Assert.Throws<ArgumentNullException>(() => new QmlSharp.Dsl.Generator.TypeMapper(null!));
            Assert.Throws<ArgumentException>(() => new QmlSharp.Dsl.Generator.TypeMapper(
                [new TypeMapping("", "CustomValue", false, true, "null", null)]));
            Assert.Throws<ArgumentException>(() => new QmlSharp.Dsl.Generator.TypeMapper(
                [new TypeMapping("custom", "", false, true, "null", null)]));

            QmlSharp.Dsl.Generator.TypeMapper mapper = new(
                [new TypeMapping("custom", "CustomValue", false, true, "null", "Test.Namespace")]);

            Assert.Equal("CustomValue", mapper.MapToCSharp(" custom "));
            Assert.Equal("IReadOnlyList<CustomValue>", mapper.MapToCSharp(" list< custom > "));
            Assert.NotNull(mapper.GetMapping("list<custom>"));
            Assert.Null(mapper.GetMapping("missing"));
            Assert.Throws<ArgumentNullException>(() => mapper.GetSetterType(null!));
            Assert.Throws<ArgumentNullException>(() => mapper.GetParameterType(null!));
            Assert.Throws<ArgumentNullException>(() => mapper.GetReturnType(null!));
            Assert.Throws<ArgumentException>(() => mapper.RegisterCustomMapping(
                new TypeMapping("custom-invalid", "", false, true, "null", null)));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public async Task ModulePackager_InvalidInputsAndUnsafePaths_AreCoveredForClosure()
        {
            using GeneratedOutputTempDirectory tempDirectory = DslTestFixtures.CreateGeneratedOutputTempDirectory();
            ModulePackager packager = new();
            QmlModule module = DslTestFixtures.CreateMinimalFixture().FindModule("QtQuick")!;
            Dictionary<string, GeneratedTypeCode> generatedTypes = new(StringComparer.Ordinal)
            {
                ["QQuickRectangle"] = DslTestFixtures.CreateGeneratedRectangleMetadata(),
            };
            GeneratedPackage package = packager.PackageModule(module, generatedTypes, DslTestFixtures.DefaultOptions.Packager);

            Assert.Throws<ArgumentNullException>(() => new ModulePackager(null!, new ModuleMapper()));
            Assert.Throws<ArgumentNullException>(() => new ModulePackager(new CodeEmitter(), null!));
            Assert.Throws<ArgumentNullException>(() => packager.PackageModule(null!, generatedTypes, DslTestFixtures.DefaultOptions.Packager));
            Assert.Throws<ArgumentNullException>(() => packager.PackageModule(module, null!, DslTestFixtures.DefaultOptions.Packager));
            Assert.Throws<ArgumentNullException>(() => packager.PackageModule(module, generatedTypes, null!));
            Assert.Throws<ArgumentException>(() => packager.PackageModule(
                module,
                generatedTypes,
                DslTestFixtures.DefaultOptions.Packager with { PackageVersion = "" }));
            await Assert.ThrowsAsync<ArgumentNullException>(() => packager.WritePackage(null!, tempDirectory.Path));
            await Assert.ThrowsAsync<ArgumentException>(() => packager.WritePackage(package, ""));
            Assert.Throws<ArgumentNullException>(() => packager.PackageAll(null!, generatedTypes, DslTestFixtures.DefaultOptions.Packager));
            Assert.Throws<ArgumentNullException>(() => packager.PackageAll(DslTestFixtures.CreateMinimalFixture(), null!, DslTestFixtures.DefaultOptions.Packager));
            Assert.Throws<ArgumentNullException>(() => packager.PackageAll(DslTestFixtures.CreateMinimalFixture(), generatedTypes, null!));

            GeneratedPackage unsafePackage = package with
            {
                Files =
                [
                    new GeneratedFile("", "// empty path\n", GeneratedFileKind.TypeFile),
                ],
            };
            await Assert.ThrowsAsync<IOException>(() => packager.WritePackage(unsafePackage, tempDirectory.Path));

            GeneratedPackage parentEscapePackage = package with
            {
                Files =
                [
                    new GeneratedFile("../Escape.cs", "// escape\n", GeneratedFileKind.TypeFile),
                ],
            };
            await Assert.ThrowsAsync<IOException>(() => packager.WritePackage(parentEscapePackage, tempDirectory.Path));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void ModulePackager_CustomPackagePrefixAndStats_AreCoveredForClosure()
        {
            ModulePackager packager = new();
            QmlModule module = new(
                Uri: "QtQuick.Controls",
                Version: new QmlVersion(2, 15),
                Dependencies: ["QtQuick"],
                Imports: ImmutableArray<string>.Empty,
                Types:
                [
                    new QmlModuleType("QQuickButton", "Button", new QmlVersion(2, 15)),
                ]);
            Dictionary<string, GeneratedTypeCode> generatedTypes = new(StringComparer.Ordinal)
            {
                ["QQuickButton"] = DslTestFixtures.CreateGeneratedButtonMetadata() with
                {
                    IsCreatable = false,
                    Enums =
                    [
                        new GeneratedEnum(
                            "ButtonState",
                            null,
                            false,
                            false,
                            [new GeneratedEnumMember("Normal", 0)],
                            "public enum ButtonState { Normal = 0 }",
                            CreateType("QQuickButton", "Button", "QtQuick.Controls")),
                    ],
                },
            };

            GeneratedPackage package = packager.PackageModule(
                module,
                generatedTypes,
                DslTestFixtures.DefaultOptions.Packager with
                {
                    PackagePrefix = "Company.Qml",
                    GenerateProjectFile = false,
                    GenerateReadme = false,
                });

            Assert.Equal("Company.Qml.QtQuick.Controls", package.PackageName);
            Assert.Contains("Company.Qml.QtQuick", package.Dependencies);
            Assert.DoesNotContain(package.Files, file => file.Kind is GeneratedFileKind.ProjectFile or GeneratedFileKind.ReadmeFile);
            Assert.Equal(1, package.Stats.TotalTypes);
            Assert.Equal(0, package.Stats.CreatableTypes);
            Assert.Equal(1, package.Stats.NonCreatableTypes);
            Assert.Equal(1, package.Stats.EnumCount);
            Assert.True(package.Stats.TotalLinesOfCode > 0);
            Assert.True(package.Stats.TotalFileSize > 0);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void ViewModelIntegration_InvalidSchemas_ReportFieldSpecificErrors()
        {
            ViewModelIntegration integration = new();

            Assert.Throws<ArgumentException>(() => integration.AnalyzeSchema(""));
            Assert.Throws<ArgumentNullException>(() => new ViewModelIntegration(null!));
            Assert.Throws<ViewModelSchemaException>(() => integration.AnalyzeSchema("[]"));
            Assert.Throws<ViewModelSchemaException>(() => integration.AnalyzeSchema("{"));
            Assert.Throws<ViewModelSchemaException>(() => integration.AnalyzeSchema("""{"className":1}"""));
            Assert.Throws<ViewModelSchemaException>(() => integration.AnalyzeSchema("""{"className":"Vm","properties":{}}"""));
            Assert.Throws<ViewModelSchemaException>(() => integration.AnalyzeSchema("""{"className":"Vm","properties":[1]}"""));
            Assert.Throws<ViewModelSchemaException>(() => integration.AnalyzeSchema("""{"className":"Vm","properties":[{"name":"bad","type":"void"}]}"""));
            Assert.Throws<ViewModelSchemaException>(() => integration.AnalyzeSchema("""{"className":"Vm","commands":[{"name":"run","parameters":{}}]}"""));
            Assert.Throws<ViewModelSchemaException>(() => integration.AnalyzeSchema("""{"className":"Vm","commands":[{"name":"run","parameters":[1]}]}"""));
            Assert.Throws<ViewModelSchemaException>(() => integration.AnalyzeSchema("""{"className":"Vm","commands":[{"name":"run","async":1}]}"""));
            Assert.Throws<ViewModelSchemaException>(() => integration.AnalyzeSchema("""{"className":"Vm","effects":[{"name":"done","parameters":{}}]}"""));
            Assert.Throws<ViewModelSchemaException>(() => integration.AnalyzeSchema("""{"className":"Vm","effects":[{"name":"done","payloadType":1}]}"""));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void MemberGenerators_FallbacksAndCollisionPaths_AreCoveredForClosure()
        {
            QmlType owner = CreateType("QQuickThing", "Thing", "QtQuick");
            GenerationContext context = CreateContext(DslTestFixtures.CreateMinimalFixture()) with
            {
                Options = DslTestFixtures.DefaultOptions with
                {
                    Signals = DslTestFixtures.DefaultOptions.Signals with { SimplifyNoArgHandlers = false },
                },
            };
            ResolvedType emptyType = new(owner, [owner], [], [], [], [], null, null);
            ResolvedSignal noArgSignal = new(new QmlSignal("ready", []), owner);
            ResolvedMethod constructorByQmlName = new(new QmlMethod("Thing", null, []), owner);
            ResolvedMethod constructorByQualifiedName = new(new QmlMethod("QQuickThing", null, []), owner);
            ResolvedMethod constructorByFactoryName = new(new QmlMethod("createThing", null, []), owner);
            MethodGenerator methodGenerator = new();

            Assert.Empty(new PropGenerator().GenerateAll(emptyType, context));
            Assert.Empty(new PropGenerator().DetectGroupedProperties(emptyType));
            Assert.Empty(new SignalGenerator().GenerateAll(emptyType, context));
            Assert.Empty(methodGenerator.GenerateAll(emptyType, context));
            Assert.Empty(new EnumGenerator().GenerateAll(ImmutableArray<QmlEnum>.Empty, owner, context));
            Assert.Equal("IThingBuilder OnReady(Action<object> handler)", new SignalGenerator().Generate(noArgSignal, owner, context).HandlerSignature);
            Assert.True(methodGenerator.Generate(constructorByQmlName, owner, context).IsConstructor);
            Assert.True(methodGenerator.Generate(constructorByQualifiedName, owner, context).IsConstructor);
            Assert.True(methodGenerator.Generate(constructorByFactoryName, owner, context).IsConstructor);
            Assert.Throws<UnsupportedMethodSignatureException>(() => methodGenerator.Generate(
                new ResolvedMethod(new QmlMethod("bad", null, [new QmlParameter("value", "void")]), owner),
                owner,
                context));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void DefaultProperties_UnknownAndMalformedListDefaults_AreCoveredForClosure()
        {
            QmlType owner = CreateType("QQuickPane", "Pane", "QtQuick") with { DefaultProperty = "content" };
            QmlType visualOwner = CreateType("QQuickVisual", "Visual", "QtQuick") with { DefaultProperty = "children" };
            QmlType malformedListOwner = CreateType("QQuickMalformed", "Malformed", "QtQuick") with { DefaultProperty = "items" };
            DefaultPropertyHandler handler = new();

            DefaultPropertyInfo unknownDefault = handler.Analyze(new ResolvedType(owner, [owner], [], [], [], [], null, null))!;
            DefaultPropertyInfo inferredVisualDefault = handler.Analyze(new ResolvedType(visualOwner, [visualOwner], [], [], [], [], null, null))!;
            DefaultPropertyInfo malformedListDefault = handler.Analyze(new ResolvedType(
                malformedListOwner,
                [malformedListOwner],
                [new ResolvedProperty(new QmlProperty("items", "list<>", false, true, false, null, null), malformedListOwner, false)],
                [],
                [],
                [],
                null,
                null))!;

            Assert.Equal("object", unknownDefault.ElementType);
            Assert.False(unknownDefault.IsList);
            Assert.Equal("Item", inferredVisualDefault.ElementType);
            Assert.True(inferredVisualDefault.IsList);
            Assert.Equal("list<>", malformedListDefault.ElementType);
            Assert.True(malformedListDefault.IsList);
            Assert.Empty(handler.GenerateMethods(
                new DefaultPropertyInfo("none", "object", false, GenerateChildMethod: false, GenerateChildrenMethod: false),
                CreateContext(DslTestFixtures.CreateMinimalFixture())));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void NameRegistry_IdentifierFallbacksAndMemberCollisionPaths_AreCoveredForClosure()
        {
            NameRegistry registry = new();

            Assert.Equal("_123", registry.ToSafeIdentifier("123"));
            Assert.Equal("_", registry.ToSafeIdentifier("..."));
            Assert.Equal("@class", registry.ToSafeIdentifier("class"));
            Assert.False(registry.IsReservedWord(""));
            Assert.Equal("QmlString", registry.RegisterTypeName("String", "QtQuick"));
            Assert.Equal("QtQuickControlsString", registry.RegisterTypeName("String", "QtQuick.Controls"));
            Assert.Equal("_", registry.RegisterTypeName("!!!", "Only.Symbols"));
            Assert.Equal("Run", registry.RegisterMethodName("run", "Owner"));
            Assert.Equal("RunProperty", registry.RegisterPropertyName("run", "Owner"));
            Assert.Equal("Run2", registry.RegisterMethodName("run", "Owner"));
            Assert.Equal("State", registry.RegisterEnumName("state", "Owner"));
            Assert.Equal("State2", registry.RegisterPropertyName("state", "Owner"));
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void AttachedPropGenerator_ResolutionAndNoBindFallbacks_AreCoveredForClosure()
        {
            QmlType attachedType = CreateType("QQuickMenuAttached", "MenuAttached", "QtQuick") with
            {
                IsCreatable = false,
                Properties =
                [
                    new QmlProperty("enabled", "bool", IsReadonly: true, IsList: false, IsRequired: false, DefaultValue: null, NotifySignal: null),
                    new QmlProperty("shortcut", "string", IsReadonly: false, IsList: false, IsRequired: false, DefaultValue: null, NotifySignal: null),
                ],
                Signals =
                [
                    new QmlSignal("activated", []),
                ],
            };
            QmlType moduleOwner = CreateType("QQuickMenu", "Menu", "QtQuick") with { AttachedType = "MenuAttached" };
            QmlType globalOwner = CreateType("QQuickGlobalMenu", "GlobalMenu", null) with { AttachedType = "MenuAttached" };
            IRegistryQuery registry = CreateQuery([attachedType, moduleOwner, globalOwner]);
            GenerationContext context = CreateContext(registry) with
            {
                Options = DslTestFixtures.DefaultOptions with
                {
                    Properties = DslTestFixtures.DefaultOptions.Properties with { GenerateBindMethods = false },
                    Signals = DslTestFixtures.DefaultOptions.Signals with { SimplifyNoArgHandlers = false },
                },
            };
            AttachedPropGenerator generator = new();

            IReadOnlyList<QmlType> attachedTypes = generator.GetAllAttachedTypes(registry);
            GeneratedAttachedType generated = generator.Generate(attachedType, context);

            Assert.Equal(["QQuickMenuAttached"], attachedTypes.Select(type => type.QualifiedName).ToArray());
            Assert.Equal("Menu", generated.MethodName);
            Assert.Equal("IMenuAttachedBuilder", generated.BuilderInterfaceName);
            Assert.Contains(generated.Properties, property => property.Name == "Enabled" && property.SetterSignature == string.Empty && property.BindSignature is null);
            Assert.Contains(generated.Properties, property => property.Name == "Shortcut" && property.BindSignature is null);
            Assert.Contains(generated.Signals, signal => signal.HandlerSignature == "IMenuAttachedBuilder OnActivated(Action<object> handler)");
        }

        private static IReadOnlyDictionary<string, string> CreateTestSpecMap()
        {
            return new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["AP-01"] = "Generate_AP01_LayoutAttachedType_ReturnsLayoutBuilderMetadata",
                ["AP-02"] = "Generate_AP02_KeysAttachedType_ReturnsKeysMethodName",
                ["AP-03"] = "Generate_AP03_LayoutAttachedProperties_ReturnsFluentProperties",
                ["AP-04"] = "Generate_AP04_KeysAttachedSignals_ReturnsSignalHandlers",
                ["AP-05"] = "GetAllAttachedTypes_AP05_EnumeratesAttachedSurfacesFromRegistryDeclarations",
                ["AP-06"] = "Generate_AP06_DerivesMethodNameFromQualifiedAttachedTypeName",
                ["CE-01"] = "EmitTypeFile_Rectangle_EmitsNamespaceInterfacesBuilderAndFactory",
                ["CE-02"] = "EmitTypeFile_GenerateXmlDocTrue_EmitsXmlDocs",
                ["CE-03"] = "EmitTypeFile_GenerateXmlDocFalse_OmitsXmlDocs",
                ["CE-04"] = "EmitTypeFile_DeprecatedTypeAndMarkDeprecatedTrue_EmitsObsoleteAttribute",
                ["CE-05"] = "EmitTypeFile_HeaderCommentSet_EmitsHeaderFirst",
                ["CE-06"] = "EmitIndexFile_CreatableTypes_EmitsSortedFactoryAggregation",
                ["CE-07"] = "EmitProjectFile_PackageMetadata_EmitsValidProjectXml",
                ["CE-08"] = "EmitCommonTypes_ReturnsForwardingSourceWithCoreValueTypeNames",
                ["CE-09"] = "EmitViewModelHelpers_ViewModelBindingInfo_EmitsProxyBaseClass",
                ["CE-10"] = "EmitTypeFile_NonCreatableType_OmitsFactoryMethod",
                ["CE-G1"] = "EmitTypeFile_Rectangle_MatchesGoldenFile",
                ["CE-G2"] = "EmitTypeFile_Text_MatchesGoldenFile",
                ["CE-G3"] = "EmitTypeFile_Button_MatchesGoldenFile",
                ["DP-01"] = "Analyze_DP01_ListDefaultProperty_GeneratesChildAndChildrenMetadata",
                ["DP-02"] = "Analyze_DP02_SingleDefaultProperty_GeneratesOnlyChildMetadata",
                ["DP-03"] = "Analyze_DP03_NoDefaultProperty_ReturnsNull",
                ["DP-04"] = "Analyze_DP04_ListDefaultProperty_IdentifiesElementType",
                ["DP-05"] = "GenerateMethods_DP05_ListDefaultProperty_ReturnsChildAndChildrenSignatures",
                ["EG-01"] = "Generate_EG01_SimpleEnum_EmitsOwnerPrefixedEnumCode",
                ["EG-02"] = "Generate_EG02_FlagEnum_EmitsFlagsAttributeMetadata",
                ["EG-03"] = "Generate_EG03_EnumMemberValues_PreservesExplicitValues",
                ["EG-04"] = "Generate_EG04_EnumAlias_PreservesAliasMetadata",
                ["EG-05"] = "Generate_EG05_ScopedEnum_PreservesScopedMetadataAndOwnerPrefix",
                ["EG-06"] = "GenerateAll_EG06_MultipleEnums_ReturnsAllEnums",
                ["GP-01"] = "GenerateType_Rectangle_ReturnsGeneratedTypeCode",
                ["GP-02"] = "GenerateModule_QtQuick_ReturnsDeterministicPackage",
                ["GP-03"] = "Generate_P0Modules_ReturnsFourPackages",
                ["GP-04"] = "Generate_ProgressCallback_ObservesEveryPhase",
                ["GP-05"] = "Generate_CreatableOnly_ExcludesNonCreatableTypes",
                ["GP-06"] = "Generate_ExcludeDeprecated_AddsWarningAndSkippedType",
                ["GP-07"] = "Generate_ExcludeInternal_ExcludesInternalTypes",
                ["GP-08"] = "Generate_ExplicitExcludeType_ExcludesNamedType",
                ["GP-09"] = "Generate_UnresolvedBaseType_ProducesWarningAndSkipsType",
                ["GP-10"] = "Generate_EmptyModule_ProducesWarning",
                ["GP-11"] = "Generate_Stats_ArePopulated",
                ["GP-12"] = "FullGeneration_P0GeneratedPackageProjects_Compile",
                ["IR-01"] = "Resolve_IR01_TypeWithNoParent_ReturnsSelfOnlyChain",
                ["IR-02"] = "Resolve_IR02_SimpleChain_ReturnsTypeToRootOrder",
                ["IR-03"] = "Resolve_IR03_DeepChain_ReturnsAllAncestors",
                ["IR-04"] = "Resolve_IR04_AllProperties_IncludesInheritedProperties",
                ["IR-05"] = "Resolve_IR05_PropertyOverride_UsesChildPropertyAndMarksOverride",
                ["IR-06"] = "Resolve_IR06_AllSignals_IncludesInheritedSignals",
                ["IR-07"] = "Resolve_IR07_AllMethods_IncludesInheritedMethods",
                ["IR-08"] = "Resolve_IR08_AllEnums_IncludesInheritedEnums",
                ["IR-09"] = "Resolve_IR09_CircularInheritance_ThrowsCircularInheritanceException",
                ["IR-10"] = "Resolve_IR10_MaxDepthExceeded_ThrowsDsl003Diagnostic",
                ["IR-11"] = "Resolve_IR11_AttachedType_PopulatesAttachedType",
                ["IR-12"] = "ResolveModule_IR12_ReturnsAllResolvableModuleTypesKeyedByQmlName",
                ["IR-13"] = "IsSubtypeOf_IR13_TransitiveBase_ReturnsTrue",
                ["MG-01"] = "Generate_MG01_NoParamsNoReturn_ReturnsBuilderSignature",
                ["MG-02"] = "Generate_MG02_MethodWithReturnType_MapsReturnType",
                ["MG-03"] = "Generate_MG03_MethodWithParameters_MapsParameterTypes",
                ["MG-04"] = "Generate_MG04_MethodNameCollisionWithProperty_AppendsMethodSuffix",
                ["MG-05"] = "GenerateAll_MG05_ReturnsAllMethods",
                ["MP-01"] = "ToPackageName_P0AndP1Modules_ReturnsReadmeMapping",
                ["MP-02"] = "ToPackageName_DottedModuleUri_PreservesDottedPackageNesting",
                ["MP-03"] = "ToModuleUri_KnownPackageName_ReturnsQtModuleUri",
                ["MP-04"] = "PackageModule_QtQuick_ProducesNuGetShapedFileStructure",
                ["MP-05"] = "PackageModule_Dependencies_IncludesCoreDslAndMappedModuleDependencies",
                ["MP-06"] = "PackageAll_P0Fixture_GeneratesAllModulesWithGeneratedTypes",
                ["MP-07"] = "WritePackage_OutputPathWithDotsAndSpaces_WritesExpectedFilesAndCountsBytes",
                ["NR-01"] = "RegisterPropertyName_NR01_DefaultKeyword_ReturnsEscapedIdentifier",
                ["NR-02"] = "RegisterPropertyName_NR02_ClassKeyword_ReturnsEscapedIdentifier",
                ["NR-03"] = "RegisterTypeName_NR03_CSharpBuiltInTypeCollision_PrefixesQml",
                ["NR-04"] = "RegisterMethodName_NR04_PropertyCollision_AppendsMethodSuffix",
                ["NR-05"] = "RegisterTypeName_NR05_CrossModuleCollision_ReturnsModuleQualifiedIdentifier",
                ["NR-06"] = "IsReservedWord_NR06_AllCSharpKeywords_ReturnTrue",
                ["PG-01"] = "Generate_PG01_IntProperty_ReturnsBuilderSetterSignature",
                ["PG-02"] = "Generate_PG02_StringProperty_UsesStringParameter",
                ["PG-03"] = "Generate_PG03_ColorProperty_UsesQmlColorParameter",
                ["PG-04"] = "Generate_PG04_BindableProperty_ReturnsBindSignature",
                ["PG-05"] = "Generate_PG05_ReadOnlyProperty_DoesNotEmitSetterOrBindMethod",
                ["PG-06"] = "Generate_PG06_RequiredProperty_PreservesMetadata",
                ["PG-07"] = "Generate_PG07_PascalCaseName_ConvertsFromQmlName",
                ["PG-08"] = "DetectGroupedProperties_PG08_BorderSubProperties_ReturnsBorderGroup",
                ["PG-09"] = "DetectGroupedProperties_PG09_BorderGroup_ReturnsCallbackBuilderSignature",
                ["PG-10"] = "Generate_PG10_Property_ReturnsXmlDocSummary",
                ["PG-11"] = "GenerateAll_PG11_ResolvedRectangle_ReturnsAllResolvedProperties",
                ["PG-12"] = "Generate_PG12_BindMethodsDisabled_RemovesBindOnly",
                ["SG-01"] = "Generate_SG01_ClickedSignal_ReturnsOnClickedHandler",
                ["SG-02"] = "Generate_SG02_SignalWithParameters_MapsParameterTypes",
                ["SG-03"] = "Generate_SG03_CamelCaseSignal_UsesOnPascalCaseHandler",
                ["SG-04"] = "Generate_SG04_NoArgSignal_UsesSimplifiedActionHandler",
                ["SG-05"] = "Generate_SG05_MultiParamSignal_UsesTypedActionHandler",
                ["SG-06"] = "Generate_SG06_Signal_ReturnsXmlDocSummary",
                ["SG-07"] = "GenerateAll_SG07_ReturnsAllSignals",
                ["TM-01"] = "MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType",
                ["TM-02"] = "MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType",
                ["TM-03"] = "MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType",
                ["TM-04"] = "MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType",
                ["TM-05"] = "MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType",
                ["TM-06"] = "MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType",
                ["TM-07"] = "MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType",
                ["TM-08"] = "MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType",
                ["TM-09"] = "MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType",
                ["TM-10"] = "MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType",
                ["TM-11"] = "MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType",
                ["TM-12"] = "MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType",
                ["TM-13"] = "MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType",
                ["TM-14"] = "MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType",
                ["TM-15"] = "MapListType_TM15_ListElement_ReturnsReadOnlyListOfMappedElement",
                ["TM-16"] = "MapToCSharp_TM01ThroughTM14AndTM16_BuiltInTypes_ReturnsExpectedCSharpType",
                ["TM-17"] = "MapToCSharp_TM17_UnknownType_ReturnsPassThrough",
                ["TM-18"] = "RegisterCustomMapping_TM18_CustomMapping_OverridesBuiltInMapping",
                ["VM-01"] = "AnalyzeSchema_VM01_StateProperties_ReturnsStateMetadata",
                ["VM-02"] = "AnalyzeSchema_VM02_Commands_ReturnsCommandMetadata",
                ["VM-03"] = "AnalyzeSchema_VM03_Effects_ReturnsEffectMetadata",
                ["VM-04"] = "AnalyzeSchema_VM04_ReadOnlyState_PreservesReadOnlyMetadata",
                ["VM-05"] = "AnalyzeSchema_VM05_AsyncCommand_PreservesAsyncMetadata",
                ["VM-06"] = "GenerateProxyType_VM06_ReturnsProxyTypeCodeForStateCommandsAndEffects",
                ["VM-07"] = "GenerateBindingHelpers_VM07_ReturnsSharedBindingHelperCode",
            };
        }

        private static HashSet<string> GetImplementedTestMethodNames()
        {
            return typeof(DslGeneratorClosureTests).Assembly
                .GetTypes()
                .SelectMany(static type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                .Where(static method => method.GetCustomAttributes().Any(static attribute => attribute.GetType().Name is "FactAttribute" or "TheoryAttribute"))
                .Select(static method => method.Name)
                .ToHashSet(StringComparer.Ordinal);
        }

        private static void AssertProjectReferences(string projectPath, string[] expectedProjectNames)
        {
            XDocument document = XDocument.Load(projectPath);
            string[] actual = document
                .Descendants("ProjectReference")
                .Select(static reference => reference.Attribute("Include")?.Value)
                .Where(static include => include is not null)
                .Select(static include => Path.GetFileNameWithoutExtension(include!))
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expectedProjectNames.Order(StringComparer.Ordinal).ToArray(), actual);
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? current = new(AppContext.BaseDirectory);

            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "QmlSharp.slnx")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new InvalidOperationException("Could not locate QmlSharp.slnx from the test output directory.");
        }

        private static GenerationContext CreateContext(IRegistryQuery registry)
        {
            return new GenerationContext(
                new QmlSharp.Dsl.Generator.TypeMapper(),
                new NameRegistry(),
                registry,
                DslTestFixtures.DefaultOptions,
                "QtQuick");
        }

        private static QmlType CreateType(string qualifiedName, string? qmlName, string? moduleUri)
        {
            return new QmlType(
                QualifiedName: qualifiedName,
                QmlName: qmlName,
                ModuleUri: moduleUri,
                AccessSemantics: AccessSemantics.Reference,
                Prototype: null,
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: moduleUri is null || qmlName is null ? [] : [new QmlTypeExport(moduleUri, qmlName, new QmlVersion(2, 15))],
                Properties: [],
                Signals: [],
                Methods: [],
                Enums: [],
                Interfaces: []);
        }

        private static IRegistryQuery CreateQuery(IReadOnlyList<QmlType> types)
        {
            QmlModule[] modules = types
                .Where(static type => type.ModuleUri is not null)
                .GroupBy(static type => type.ModuleUri!, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .Select(static group => new QmlModule(
                    group.Key,
                    new QmlVersion(2, 15),
                    ImmutableArray<string>.Empty,
                    ImmutableArray<string>.Empty,
                    group
                        .Where(static type => type.QmlName is not null)
                        .OrderBy(static type => type.QmlName, StringComparer.Ordinal)
                        .Select(static type => new QmlModuleType(type.QualifiedName, type.QmlName!, new QmlVersion(2, 15)))
                        .ToImmutableArray()))
                .ToArray();

            return new TestRegistryQuery(modules, types.OrderBy(static type => type.QualifiedName, StringComparer.Ordinal).ToArray(), "6.11.0");
        }
    }
}

#pragma warning restore IDE0058
