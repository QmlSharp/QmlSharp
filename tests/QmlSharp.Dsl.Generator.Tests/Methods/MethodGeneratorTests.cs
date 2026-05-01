using QmlSharp.Dsl.Generator.Tests.Fixtures;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Methods
{
    public sealed class MethodGeneratorTests
    {
        [Fact]
        public void Generate_MG01_NoParamsNoReturn_ReturnsBuilderSignature()
        {
            QmlType rectangle = CreateType("QQuickRectangle", "Rectangle");
            ResolvedMethod method = new(CreateMethod("forceActiveFocus", "void"), rectangle);
            MethodGenerator generator = new();

            GeneratedMethod generated = generator.Generate(method, rectangle, CreateContext());

            Assert.Equal("ForceActiveFocus", generated.Name);
            Assert.Equal("IRectangleBuilder ForceActiveFocus()", generated.Signature);
            Assert.Equal("void", generated.ReturnType);
        }

        [Fact]
        public void Generate_MG02_MethodWithReturnType_MapsReturnType()
        {
            QmlType item = CreateType("QQuickItem", "Item");
            ResolvedMethod method = new(CreateMethod("childCount", "int"), item);
            MethodGenerator generator = new();

            GeneratedMethod generated = generator.Generate(method, item, CreateContext());

            Assert.Equal("int", generated.ReturnType);
            Assert.Equal("int ChildCount()", generated.Signature);
        }

        [Fact]
        public void Generate_MG03_MethodWithParameters_MapsParameterTypes()
        {
            QmlType item = CreateType("QQuickItem", "Item");
            ResolvedMethod method = new(CreateMethod(
                "childAt",
                "QQuickItem",
                new QmlParameter("x", "real"),
                new QmlParameter("y", "real")), item);
            MethodGenerator generator = new();

            GeneratedMethod generated = generator.Generate(method, item, CreateContext());

            Assert.Equal("QQuickItem ChildAt(double x, double y)", generated.Signature);
            Assert.Equal(["double", "double"], generated.Parameters.Select(parameter => parameter.CSharpType));
        }

        [Fact]
        public void Generate_MG04_MethodNameCollisionWithProperty_AppendsMethodSuffix()
        {
            QmlType rectangle = CreateType("QQuickRectangle", "Rectangle");
            NameRegistry nameRegistry = new();
            _ = nameRegistry.RegisterPropertyName("focus", "IRectangleBuilder");
            ResolvedMethod method = new(CreateMethod("focus", "void"), rectangle);
            MethodGenerator generator = new();

            GeneratedMethod generated = generator.Generate(method, rectangle, CreateContext(nameRegistry: nameRegistry));

            Assert.Equal("FocusMethod", generated.Name);
            Assert.Equal("IRectangleBuilder FocusMethod()", generated.Signature);
        }

        [Fact]
        public void GenerateAll_MG05_ReturnsAllMethods()
        {
            QmlType item = CreateType("QQuickItem", "Item");
            ImmutableArray<ResolvedMethod> methods =
            [
                new ResolvedMethod(CreateMethod("zeta", "void"), item),
                new ResolvedMethod(CreateMethod("alpha", "void"), item),
            ];
            MethodGenerator generator = new();

            ImmutableArray<GeneratedMethod> generated = generator.GenerateAll(CreateResolvedType(item, methods), CreateContext());

            Assert.Equal(2, generated.Length);
            Assert.Equal(["Alpha", "Zeta"], generated.Select(method => method.Name));
        }

        [Fact]
        public void GenerateAll_InheritOnlyType_UsesTargetBuilderForInheritedMethod()
        {
            QmlType item = CreateType("QQuickItem", "Item");
            QmlType customItem = CreateType("QQuickCustomItem", "CustomItem");
            ImmutableArray<ResolvedMethod> methods =
            [
                new ResolvedMethod(CreateMethod("forceActiveFocus", "void"), item),
            ];
            MethodGenerator generator = new();

            GeneratedMethod generated = Assert.Single(generator.GenerateAll(CreateResolvedType(customItem, methods), CreateContext()));

            Assert.Equal("ICustomItemBuilder ForceActiveFocus()", generated.Signature);
            Assert.Equal("QQuickItem", generated.DeclaredBy.QualifiedName);
        }

        [Fact]
        public void Generate_UnsupportedMethodSignature_ThrowsDsl040Diagnostic()
        {
            QmlType item = CreateType("QQuickItem", "Item");
            ResolvedMethod method = new(CreateMethod("bad", "void", new QmlParameter("value", "void")), item);
            MethodGenerator generator = new();

            UnsupportedMethodSignatureException exception = Assert.Throws<UnsupportedMethodSignatureException>(() =>
                generator.Generate(method, item, CreateContext()));

            Assert.Equal(DslDiagnosticCodes.UnsupportedMethodSignature, exception.DiagnosticCode);
            Assert.Equal("bad", exception.MethodName);
        }

        [Fact]
        public void MethodPropertyNameCollisionException_UsesDsl041Diagnostic()
        {
            MethodPropertyNameCollisionException exception = new("focus", "IRectangleBuilder");

            Assert.Equal(DslDiagnosticCodes.MethodPropertyNameCollision, exception.DiagnosticCode);
            Assert.Equal("focus", exception.MethodName);
        }

        [Fact]
        public void Generate_ConstructorLikeMethod_PreservesMetadata()
        {
            QmlType rectangle = CreateType("QQuickRectangle", "Rectangle");
            ResolvedMethod method = new(CreateMethod("Rectangle", "void"), rectangle);
            MethodGenerator generator = new();

            GeneratedMethod generated = generator.Generate(method, rectangle, CreateContext());

            Assert.True(generated.IsConstructor);
        }

        [Fact]
        public void GenerateAll_InheritedMethods_UseTargetBuilderInterface()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();
            QmlType rectangle = registry.FindTypeByQualifiedName("QQuickRectangle")!;
            ResolvedType resolved = new InheritanceResolver().Resolve(rectangle, registry);
            MethodGenerator generator = new();

            ImmutableArray<GeneratedMethod> generated = generator.GenerateAll(resolved, CreateContext(registry: registry));
            GeneratedMethod inherited = Assert.Single(generated.Where(method => method.Name == "ForceActiveFocus"));

            Assert.Equal("IRectangleBuilder ForceActiveFocus()", inherited.Signature);
            Assert.Equal("QQuickItem", inherited.DeclaredBy.QualifiedName);
        }

        private static GenerationContext CreateContext(
            IRegistryQuery? registry = null,
            INameRegistry? nameRegistry = null)
        {
            return new GenerationContext(
                TypeMapper: new QmlSharp.Dsl.Generator.TypeMapper(),
                NameRegistry: nameRegistry ?? new NameRegistry(),
                Registry: registry ?? DslTestFixtures.CreateMinimalFixture(),
                Options: DslTestFixtures.DefaultOptions,
                CurrentModuleUri: "QtQuick");
        }

        private static QmlMethod CreateMethod(string name, string? returnType, params QmlParameter[] parameters)
        {
            return new QmlMethod(name, returnType, parameters.ToImmutableArray());
        }

        private static ResolvedType CreateResolvedType(QmlType type, ImmutableArray<ResolvedMethod> methods)
        {
            return new ResolvedType(
                Type: type,
                InheritanceChain: [type],
                AllProperties: ImmutableArray<ResolvedProperty>.Empty,
                AllSignals: ImmutableArray<ResolvedSignal>.Empty,
                AllMethods: methods,
                AllEnums: ImmutableArray<QmlEnum>.Empty,
                AttachedType: null,
                ExtensionType: null);
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
