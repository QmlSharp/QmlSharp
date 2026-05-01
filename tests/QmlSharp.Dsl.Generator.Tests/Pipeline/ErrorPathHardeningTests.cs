using System.Reflection;
using QmlSharp.Dsl.Generator.Tests.Fixtures;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Pipeline
{
    public sealed class ErrorPathHardeningTests
    {
        [Fact]
        public async Task Generate_DiagnosticWarnings_PreserveSpecificDslCodes()
        {
            GenerationPipeline pipeline = new();
            GenerationResult result = await pipeline.Generate(CreateDiagnosticFixture(), DslTestFixtures.DefaultOptions with
            {
                Filter = DslTestFixtures.DefaultOptions.Filter with { ExcludeInternal = false },
            });
            string diagnostics = string.Join(
                "\n",
                result.Warnings.Select(static warning => warning.Message)
                    .Concat(result.SkippedTypes.Select(static skipped => skipped.Reason))
                    .Order(StringComparer.Ordinal));

            Assert.Contains(DslDiagnosticCodes.UnmappedQmlType, diagnostics, StringComparison.Ordinal);
            Assert.Contains(DslDiagnosticCodes.AmbiguousTypeMapping, diagnostics, StringComparison.Ordinal);
            Assert.Contains(DslDiagnosticCodes.ReservedWordCollision, diagnostics, StringComparison.Ordinal);
            Assert.Contains(DslDiagnosticCodes.TypeNameCollision, diagnostics, StringComparison.Ordinal);
            Assert.Contains(DslDiagnosticCodes.CrossModuleNameCollision, diagnostics, StringComparison.Ordinal);
            Assert.Contains(DslDiagnosticCodes.UnsupportedPropertyType, diagnostics, StringComparison.Ordinal);
            Assert.Contains(DslDiagnosticCodes.UnsupportedSignalParameter, diagnostics, StringComparison.Ordinal);
            Assert.Contains(DslDiagnosticCodes.UnsupportedMethodSignature, diagnostics, StringComparison.Ordinal);
        }

        [Fact]
        public void DslDiagnosticCodes_AllConstants_HaveStep0513Coverage()
        {
            HashSet<string> coveredCodes =
            [
                DslDiagnosticCodes.UnresolvedBaseType,
                DslDiagnosticCodes.CircularInheritance,
                DslDiagnosticCodes.MaxDepthExceeded,
                DslDiagnosticCodes.UnmappedQmlType,
                DslDiagnosticCodes.AmbiguousTypeMapping,
                DslDiagnosticCodes.UnsupportedPropertyType,
                DslDiagnosticCodes.GroupedPropertyConflict,
                DslDiagnosticCodes.UnsupportedSignalParameter,
                DslDiagnosticCodes.UnsupportedMethodSignature,
                DslDiagnosticCodes.MethodPropertyNameCollision,
                DslDiagnosticCodes.DuplicateEnumMember,
                DslDiagnosticCodes.EnumNameCollision,
                DslDiagnosticCodes.UnresolvedAttachedType,
                DslDiagnosticCodes.ReservedWordCollision,
                DslDiagnosticCodes.TypeNameCollision,
                DslDiagnosticCodes.CrossModuleNameCollision,
                DslDiagnosticCodes.EmitFailure,
                DslDiagnosticCodes.EmptyModule,
                DslDiagnosticCodes.MissingDependency,
                DslDiagnosticCodes.SkippedType,
                DslDiagnosticCodes.DeprecatedType,
            ];

            string[] constants = typeof(DslDiagnosticCodes)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(static field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
                .Select(static field => (string)field.GetRawConstantValue()!)
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(constants, coveredCodes.Order(StringComparer.Ordinal).ToArray());
        }

        private static IRegistryQuery CreateDiagnosticFixture()
        {
            QmlType qtObject = CreateType("QObject", "QtObject", "QtQml", null);
            QmlType reservedType = CreateType("ReservedType", "class", "QtQuick", "QObject");
            QmlType generatedNameCollisionA = CreateType("GeneratedNameCollisionA", "A-B", "QtQuick", "QObject");
            QmlType generatedNameCollisionB = CreateType("GeneratedNameCollisionB", "A B", "QtQuick", "QObject");
            QmlType sharedQuick = CreateType("QQuickShared", "Shared", "QtQuick", "QObject");
            QmlType sharedControls = CreateType("QQuickControlsShared", "Shared", "QtQuick.Controls", "QObject");
            QmlType unknownProperty = CreateType(
                "UnknownPropertyType",
                "UnknownPropertyType",
                "QtQuick",
                "QObject",
                properties: [CreateProperty("mystery", "MissingThing")]);
            QmlType ambiguousProperty = CreateType(
                "AmbiguousPropertyType",
                "AmbiguousPropertyType",
                "QtQuick",
                "QObject",
                properties: [CreateProperty("shared", "Shared")]);
            QmlType unsupportedProperty = CreateType(
                "UnsupportedPropertyType",
                "UnsupportedPropertyType",
                "QtQuick",
                "QObject",
                properties: [CreateProperty("bad", "void")]);
            QmlType unsupportedSignal = CreateType(
                "UnsupportedSignalType",
                "UnsupportedSignalType",
                "QtQuick",
                "QObject",
                signals: [new QmlSignal("badSignal", [new QmlParameter("value", "void")])]);
            QmlType unsupportedMethod = CreateType(
                "UnsupportedMethodType",
                "UnsupportedMethodType",
                "QtQuick",
                "QObject",
                methods: [new QmlMethod("badMethod", "void", [new QmlParameter("value", "void")])]);

            QmlModule qtQml = CreateModule("QtQml", qtObject);
            QmlModule qtQuick = CreateModule(
                "QtQuick",
                reservedType,
                generatedNameCollisionA,
                generatedNameCollisionB,
                sharedQuick,
                unknownProperty,
                ambiguousProperty,
                unsupportedProperty,
                unsupportedSignal,
                unsupportedMethod);
            QmlModule controls = CreateModule("QtQuick.Controls", sharedControls);

            return new TestRegistryQuery(
                [qtQml, qtQuick, controls],
                [
                    qtObject,
                    reservedType,
                    generatedNameCollisionA,
                    generatedNameCollisionB,
                    sharedQuick,
                    sharedControls,
                    unknownProperty,
                    ambiguousProperty,
                    unsupportedProperty,
                    unsupportedSignal,
                    unsupportedMethod,
                ],
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
                    .Select(static type => new QmlModuleType(type.QualifiedName, type.QmlName ?? type.QualifiedName, new QmlVersion(2, 15)))
                    .ToImmutableArray());
        }

        private static QmlType CreateType(
            string qualifiedName,
            string? qmlName,
            string? moduleUri,
            string? prototype,
            ImmutableArray<QmlProperty>? properties = null,
            ImmutableArray<QmlSignal>? signals = null,
            ImmutableArray<QmlMethod>? methods = null)
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
                IsCreatable: true,
                Exports: moduleUri is null || qmlName is null ? ImmutableArray<QmlTypeExport>.Empty : [new QmlTypeExport(moduleUri, qmlName, new QmlVersion(2, 15))],
                Properties: properties ?? ImmutableArray<QmlProperty>.Empty,
                Signals: signals ?? ImmutableArray<QmlSignal>.Empty,
                Methods: methods ?? ImmutableArray<QmlMethod>.Empty,
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
