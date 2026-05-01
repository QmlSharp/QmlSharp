using QmlSharp.Dsl.Generator.Tests.Fixtures;
using QmlSharp.Registry;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Dsl.Generator.Tests.Signals
{
    public sealed class SignalGeneratorTests
    {
        [Fact]
        public void Generate_SG01_ClickedSignal_ReturnsOnClickedHandler()
        {
            QmlType button = CreateType("QQuickButton", "Button");
            ResolvedSignal signal = new(CreateSignal("clicked"), button);
            SignalGenerator generator = new();

            GeneratedSignal generated = generator.Generate(signal, button, CreateContext());

            Assert.Equal("OnClicked", generated.HandlerName);
            Assert.Equal("IButtonBuilder OnClicked(Action handler)", generated.HandlerSignature);
        }

        [Fact]
        public void Generate_SG02_SignalWithParameters_MapsParameterTypes()
        {
            QmlType item = CreateType("QQuickItem", "Item");
            ResolvedSignal signal = new(CreateSignal("positionChanged", new QmlParameter("x", "real")), item);
            SignalGenerator generator = new();

            GeneratedSignal generated = generator.Generate(signal, item, CreateContext());

            GeneratedParameter parameter = Assert.Single(generated.Parameters);
            Assert.Equal("x", parameter.Name);
            Assert.Equal("double", parameter.CSharpType);
        }

        [Fact]
        public void Generate_SG03_CamelCaseSignal_UsesOnPascalCaseHandler()
        {
            QmlType button = CreateType("QQuickButton", "Button");
            ResolvedSignal signal = new(CreateSignal("pressAndHold"), button);
            SignalGenerator generator = new();

            GeneratedSignal generated = generator.Generate(signal, button, CreateContext());

            Assert.Equal("OnPressAndHold", generated.HandlerName);
        }

        [Fact]
        public void Generate_SG04_NoArgSignal_UsesSimplifiedActionHandler()
        {
            QmlType button = CreateType("QQuickButton", "Button");
            ResolvedSignal signal = new(CreateSignal("clicked"), button);
            SignalGenerator generator = new();

            GeneratedSignal generated = generator.Generate(signal, button, CreateContext());

            Assert.Equal("IButtonBuilder OnClicked(Action handler)", generated.HandlerSignature);
        }

        [Fact]
        public void Generate_SG05_MultiParamSignal_UsesTypedActionHandler()
        {
            QmlType item = CreateType("QQuickItem", "Item");
            ResolvedSignal signal = new(CreateSignal(
                "positionChanged",
                new QmlParameter("x", "real"),
                new QmlParameter("y", "real")), item);
            SignalGenerator generator = new();

            GeneratedSignal generated = generator.Generate(signal, item, CreateContext());

            Assert.Equal("IItemBuilder OnPositionChanged(Action<double, double> handler)", generated.HandlerSignature);
            Assert.Equal(["x", "y"], generated.Parameters.Select(parameter => parameter.Name));
        }

        [Fact]
        public void Generate_SG06_Signal_ReturnsXmlDocSummary()
        {
            QmlType button = CreateType("QQuickButton", "Button");
            ResolvedSignal signal = new(CreateSignal("clicked"), button);
            SignalGenerator generator = new();

            GeneratedSignal generated = generator.Generate(signal, button, CreateContext());

            Assert.Contains("<summary>", generated.XmlDoc, StringComparison.Ordinal);
            Assert.Contains("clicked", generated.XmlDoc, StringComparison.Ordinal);
        }

        [Fact]
        public void GenerateAll_SG07_ReturnsAllSignals()
        {
            ImmutableArray<ResolvedSignal> signals =
            [
                new ResolvedSignal(CreateSignal("released"), CreateType("QQuickButton", "Button")),
                new ResolvedSignal(CreateSignal("clicked"), CreateType("QQuickButton", "Button")),
            ];
            SignalGenerator generator = new();

            ImmutableArray<GeneratedSignal> generated = generator.GenerateAll(signals, CreateContext());

            Assert.Equal(2, generated.Length);
            Assert.Equal(["OnClicked", "OnReleased"], generated.Select(signal => signal.HandlerName));
        }

        [Fact]
        public void Generate_UnsupportedSignalParameter_ThrowsDsl030Diagnostic()
        {
            QmlType item = CreateType("QQuickItem", "Item");
            ResolvedSignal signal = new(CreateSignal("bad", new QmlParameter("value", "void")), item);
            SignalGenerator generator = new();

            UnsupportedSignalParameterException exception = Assert.Throws<UnsupportedSignalParameterException>(() =>
                generator.Generate(signal, item, CreateContext()));

            Assert.Equal(DslDiagnosticCodes.UnsupportedSignalParameter, exception.DiagnosticCode);
            Assert.Equal("bad", exception.SignalName);
        }

        [Fact]
        public void GenerateAll_InheritedSignals_UseTargetBuilderInterface()
        {
            IRegistryQuery registry = DslTestFixtures.CreateMinimalFixture();
            QmlType rectangle = registry.FindTypeByQualifiedName("QQuickRectangle")!;
            ResolvedType resolved = new InheritanceResolver().Resolve(rectangle, registry);
            SignalGenerator generator = new();

            ImmutableArray<GeneratedSignal> generated = generator.GenerateAll(resolved, CreateContext(registry));
            GeneratedSignal inherited = Assert.Single(generated.Where(signal => signal.HandlerName == "OnWidthChanged"));

            Assert.Equal("IRectangleBuilder OnWidthChanged(Action handler)", inherited.HandlerSignature);
            Assert.Equal("QQuickItem", inherited.DeclaredBy.QualifiedName);
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

        private static QmlSignal CreateSignal(string name, params QmlParameter[] parameters)
        {
            return new QmlSignal(name, parameters.ToImmutableArray());
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
