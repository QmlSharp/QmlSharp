using QmlSharp.Dsl.Generator.Tests.Fixtures;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Enums
{
    public sealed class EnumGeneratorTests
    {
        [Fact]
        public void Generate_EG01_SimpleEnum_EmitsOwnerPrefixedEnumCode()
        {
            QmlType text = CreateType("QQuickText", "Text");
            QmlEnum enumDef = CreateEnum("ElideMode", isFlag: false, new QmlEnumValue("ElideLeft", 0));
            EnumGenerator generator = new();

            GeneratedEnum generated = generator.Generate(enumDef, text, CreateContext());

            Assert.Equal("TextElideMode", generated.Name);
            Assert.Contains("public enum TextElideMode", generated.Code, StringComparison.Ordinal);
        }

        [Fact]
        public void Generate_EG02_FlagEnum_EmitsFlagsAttributeMetadata()
        {
            QmlType item = CreateType("QQuickItem", "Item");
            QmlEnum enumDef = CreateEnum(
                "Alignment",
                isFlag: true,
                new QmlEnumValue("AlignLeft", 1),
                new QmlEnumValue("AlignRight", 2));
            EnumGenerator generator = new();

            GeneratedEnum generated = generator.Generate(enumDef, item, CreateContext());

            Assert.True(generated.IsFlag);
            Assert.Contains("[Flags]", generated.Code, StringComparison.Ordinal);
        }

        [Fact]
        public void Generate_NonFlagEnumWithPowerOfTwoValues_DoesNotInferFlagsAttribute()
        {
            QmlType text = CreateType("QQuickText", "Text");
            QmlEnum enumDef = CreateEnum(
                "Mode",
                isFlag: false,
                new QmlEnumValue("None", 0),
                new QmlEnumValue("Primary", 1),
                new QmlEnumValue("Secondary", 2));
            EnumGenerator generator = new();

            GeneratedEnum generated = generator.Generate(enumDef, text, CreateContext());

            Assert.False(generated.IsFlag);
            Assert.DoesNotContain("[Flags]", generated.Code, StringComparison.Ordinal);
        }

        [Fact]
        public void Generate_EG03_EnumMemberValues_PreservesExplicitValues()
        {
            QmlType text = CreateType("QQuickText", "Text");
            QmlEnum enumDef = CreateEnum("WrapMode", isFlag: false, new QmlEnumValue("NoWrap", 0), new QmlEnumValue("WordWrap", 10));
            EnumGenerator generator = new();

            GeneratedEnum generated = generator.Generate(enumDef, text, CreateContext());

            Assert.Equal([0, 10], generated.Members.Select(member => member.Value));
            Assert.Contains("WordWrap = 10", generated.Code, StringComparison.Ordinal);
        }

        [Fact]
        public void Generate_EG04_EnumAlias_PreservesAliasMetadata()
        {
            QmlType text = CreateType("QQuickText", "Text");
            QmlEnum enumDef = new(
                Name: "Mode",
                IsFlag: false,
                Values: [new QmlEnumValue("None", 0)],
                Alias: "ElideMode");
            EnumGenerator generator = new();

            GeneratedEnum generated = generator.Generate(enumDef, text, CreateContext());

            Assert.Equal("ElideMode", generated.Alias);
            Assert.Equal("TextElideMode", generated.Name);
        }

        [Fact]
        public void Generate_EG05_ScopedEnum_PreservesScopedMetadataAndOwnerPrefix()
        {
            QmlType text = CreateType("QQuickText", "Text");
            QmlEnum enumDef = new(
                Name: "ElideMode",
                IsFlag: false,
                Values: [new QmlEnumValue("ElideNone", 0)],
                Alias: null,
                IsScoped: true);
            EnumGenerator generator = new();

            GeneratedEnum generated = generator.Generate(enumDef, text, CreateContext());

            Assert.True(generated.IsScoped);
            Assert.Equal("TextElideMode", generated.Name);
        }

        [Fact]
        public void GenerateAll_EG06_MultipleEnums_ReturnsAllEnums()
        {
            QmlType text = CreateType("QQuickText", "Text");
            ImmutableArray<QmlEnum> enums =
            [
                CreateEnum("WrapMode", isFlag: false, new QmlEnumValue("NoWrap", 0)),
                CreateEnum("ElideMode", isFlag: false, new QmlEnumValue("ElideNone", 0)),
            ];
            EnumGenerator generator = new();

            ImmutableArray<GeneratedEnum> generated = generator.GenerateAll(enums, text, CreateContext());

            Assert.Equal(2, generated.Length);
            Assert.Equal(["TextElideMode", "TextWrapMode"], generated.Select(@enum => @enum.Name));
        }

        [Fact]
        public void Generate_DuplicateMemberNames_ThrowsDsl050Diagnostic()
        {
            QmlType text = CreateType("QQuickText", "Text");
            QmlEnum enumDef = CreateEnum(
                "Mode",
                isFlag: false,
                new QmlEnumValue("top-left", 0),
                new QmlEnumValue("topLeft", 1));
            EnumGenerator generator = new();

            DuplicateEnumMemberException exception = Assert.Throws<DuplicateEnumMemberException>(() =>
                generator.Generate(enumDef, text, CreateContext()));

            Assert.Equal(DslDiagnosticCodes.DuplicateEnumMember, exception.DiagnosticCode);
            Assert.Equal("TopLeft", exception.MemberName);
        }

        [Fact]
        public void GenerateAll_EnumNameCollision_ThrowsDsl051Diagnostic()
        {
            QmlType text = CreateType("QQuickText", "Text");
            ImmutableArray<QmlEnum> enums =
            [
                CreateEnum("Mode", isFlag: false, new QmlEnumValue("A", 0)),
                CreateEnum("TextMode", isFlag: false, new QmlEnumValue("B", 0)),
            ];
            EnumGenerator generator = new();

            EnumNameCollisionException exception = Assert.Throws<EnumNameCollisionException>(() =>
                generator.GenerateAll(enums, text, CreateContext()));

            Assert.Equal(DslDiagnosticCodes.EnumNameCollision, exception.DiagnosticCode);
            Assert.Equal("TextMode", exception.EnumName);
        }

        [Fact]
        public void GenerateAll_InheritedEnums_UsesTargetOwnerName()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();
            QmlType rectangle = registry.FindTypeByQualifiedName("QQuickRectangle")!;
            ResolvedType resolved = new InheritanceResolver().Resolve(rectangle, registry);
            EnumGenerator generator = new();

            ImmutableArray<GeneratedEnum> generated = generator.GenerateAll(resolved.AllEnums, rectangle, CreateContext(registry));

            GeneratedEnum inherited = Assert.Single(generated);
            Assert.Equal("RectangleTransformOrigin", inherited.Name);
            Assert.Equal("QQuickRectangle", inherited.OwnerType.QualifiedName);
        }

        [Fact]
        public void Generate_EnumMembers_AreOrderedDeterministicallyByValueThenName()
        {
            QmlType text = CreateType("QQuickText", "Text");
            QmlEnum enumDef = CreateEnum(
                "Mode",
                isFlag: false,
                new QmlEnumValue("Two", 2),
                new QmlEnumValue("Zero", 0),
                new QmlEnumValue("One", 1));
            EnumGenerator generator = new();

            GeneratedEnum generated = generator.Generate(enumDef, text, CreateContext());

            Assert.Equal(["Zero", "One", "Two"], generated.Members.Select(member => member.Name));
        }

        private static GenerationContext CreateContext(IRegistryQuery? registry = null)
        {
            return new GenerationContext(
                TypeMapper: new QmlSharp.Dsl.Generator.TypeMapper(),
                NameRegistry: new NameRegistry(),
                Registry: registry ?? DslTestFixtures.CreateMinimalFixture(),
                Options: DslTestFixtures.DefaultOptions,
                CurrentModuleUri: "QtQuick");
        }

        private static QmlEnum CreateEnum(string name, bool isFlag, params QmlEnumValue[] values)
        {
            return new QmlEnum(name, isFlag, values.ToImmutableArray());
        }

        private static QmlType CreateType(string qualifiedName, string qmlName)
        {
            return new QmlType(
                QualifiedName: qualifiedName,
                QmlName: qmlName,
                ModuleUri: "QtQuick",
                AccessSemantics: AccessSemantics.Reference,
                Prototype: null,
                DefaultProperty: null,
                AttachedType: null,
                Extension: null,
                IsSingleton: false,
                IsCreatable: true,
                Exports: [new QmlTypeExport("QtQuick", qmlName, new QmlVersion(2, 15))],
                Properties: ImmutableArray<QmlProperty>.Empty,
                Signals: ImmutableArray<QmlSignal>.Empty,
                Methods: ImmutableArray<QmlMethod>.Empty,
                Enums: ImmutableArray<QmlEnum>.Empty,
                Interfaces: ImmutableArray<string>.Empty);
        }
    }
}
