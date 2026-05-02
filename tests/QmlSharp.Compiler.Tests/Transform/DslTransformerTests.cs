using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using QmlSharp.Compiler.Tests.Fixtures;
using QmlSharp.Qml.Ast;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Compiler.Tests.Transform
{
    public sealed class DslTransformerTests
    {
        private readonly DslTransformer transformer = new();
        private readonly IRegistryQuery registry = RegistryFixture.CreateQtQuickAndControlsRegistry();

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_DT01_SimpleFactoryCall_ExtractsTypeName()
        {
            DslCallNode node = Extract("Rectangle()");

            Assert.Equal("Rectangle", node.TypeName);
            Assert.Empty(node.Properties);
            Assert.Empty(node.Children);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_DT02_PropertySetter_ExtractsNumericLiteral()
        {
            DslCallNode node = Extract("Rectangle().Width(100)");

            DslPropertyCall property = Assert.Single(node.Properties);
            Assert.Equal("Width", property.Name);
            Assert.Equal(100, property.Value);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_DT03_StringBooleanNullAndSupportedLiterals_LowerToAstValues()
        {
            DslCallNode node = new(
                "Rectangle",
                ImmutableArray.Create(
                    new DslPropertyCall("Color", "red"),
                    new DslPropertyCall("Visible", true),
                    new DslPropertyCall("Gradient", null)),
                ImmutableArray<DslBindingCall>.Empty,
                ImmutableArray<DslSignalHandlerCall>.Empty,
                ImmutableArray<DslGroupedCall>.Empty,
                ImmutableArray<DslAttachedCall>.Empty,
                ImmutableArray<DslCallNode>.Empty);

            ObjectDefinitionNode ast = transformer.ToAstNode(node, registry);

            ImmutableArray<BindingNode> bindings = ast.Members.OfType<BindingNode>().ToImmutableArray();
            Assert.Contains(bindings, static binding => binding.PropertyName == "color" && binding.Value is StringLiteral);
            Assert.Contains(bindings, static binding => binding.PropertyName == "visible" && binding.Value is BooleanLiteral);
            Assert.Contains(bindings, static binding => binding.PropertyName == "gradient" && binding.Value is NullLiteral);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_DT04_BindMethod_ProducesPropertyBinding()
        {
            ObjectDefinitionNode ast = TransformToAst("Rectangle().WidthBind(\"parent.width\")");

            BindingNode binding = Assert.Single(ast.Members.OfType<BindingNode>());
            Assert.Equal("width", binding.PropertyName);
            ScriptExpression expression = Assert.IsType<ScriptExpression>(binding.Value);
            Assert.Equal("parent.width", expression.Code);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_DT05_DT06_ChildAndChildren_ExtractNestedChildren()
        {
            DslCallNode single = Extract("Rectangle().Child(Text())");
            DslCallNode multiple = Extract("Rectangle().Children(Text(), Button())");

            Assert.Equal("Text", Assert.Single(single.Children).TypeName);
            Assert.Equal(["Text", "Button"], multiple.Children.Select(static child => child.TypeName));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_DT07_SignalHandler_LowersToSignalHandlerNode()
        {
            ObjectDefinitionNode ast = TransformToAst("MouseArea().OnClicked(\"console.log('clicked')\")");

            SignalHandlerNode handler = Assert.Single(ast.Members.OfType<SignalHandlerNode>());
            Assert.Equal("onClicked", handler.HandlerName);
            Assert.Equal("console.log('clicked')", handler.Code);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_DT08_DT09_GroupedAndAttachedProperties_LowerToAstNodes()
        {
            ObjectDefinitionNode groupedAst = TransformToAst("Rectangle().Border(b => b.Width(2).Color(\"red\"))");
            ObjectDefinitionNode attachedAst = TransformToAst("Rectangle().Layout(l => l.FillWidth(true))");

            GroupedBindingNode grouped = Assert.Single(groupedAst.Members.OfType<GroupedBindingNode>());
            AttachedBindingNode attached = Assert.Single(attachedAst.Members.OfType<AttachedBindingNode>());
            Assert.Equal("border", grouped.GroupName);
            Assert.Equal(["width", "color"], grouped.Bindings.Select(static binding => binding.PropertyName));
            Assert.Equal("Layout", attached.AttachedTypeName);
            Assert.Equal("fillWidth", Assert.Single(attached.Bindings).PropertyName);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_DT10_NestedChildren_LowersRecursively()
        {
            ObjectDefinitionNode ast = TransformToAst("Rectangle().Child(Button().Child(Text().Text(\"ok\")))");

            ObjectDefinitionNode button = Assert.IsType<ObjectDefinitionNode>(Assert.Single(ast.Members.OfType<ObjectDefinitionNode>()));
            ObjectDefinitionNode text = Assert.IsType<ObjectDefinitionNode>(Assert.Single(button.Members.OfType<ObjectDefinitionNode>()));
            BindingNode textBinding = Assert.Single(text.Members.OfType<BindingNode>());
            Assert.Equal("Button", button.TypeName);
            Assert.Equal("Text", text.TypeName);
            Assert.Equal("text", textBinding.PropertyName);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_DT11_PascalCaseProperties_LowerToCamelCase()
        {
            ObjectDefinitionNode ast = TransformToAst("Rectangle().Width(100).Height(50)");

            Assert.Equal(["width", "height"], ast.Members.OfType<BindingNode>().Select(static binding => binding.PropertyName));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_DT12_ToAstNode_ReturnsObjectDefinition()
        {
            ObjectDefinitionNode ast = transformer.ToAstNode(CompilerTestFixtures.CreateSimpleRectangleDsl(), registry);

            Assert.Equal("Rectangle", ast.TypeName);
            Assert.Contains(ast.Members, static member => member is BindingNode { PropertyName: "width" });
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_DT13_DT14_BindingAndSignalMembers_LowerIndependently()
        {
            ObjectDefinitionNode ast = TransformToAst("Button().TextBind(\"title\").OnClicked(() => Vm.Increment())");

            Assert.Contains(ast.Members, static member => member is BindingNode { PropertyName: "text", Value: ScriptExpression });
            Assert.Contains(ast.Members, static member => member is SignalHandlerNode { HandlerName: "onClicked" });
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_IdAssignment_ExtractsAndLowersWhenSupportedByDslRuntime()
        {
            ObjectDefinitionNode ast = TransformToAst("Rectangle().Id(\"root\")");

            IdAssignmentNode id = Assert.Single(ast.Members.OfType<IdAssignmentNode>());
            Assert.Equal("root", id.Id);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_EnumReference_ExtractsAndLowersToEnumReference()
        {
            ObjectDefinitionNode ast = TransformToAst("Text().HorizontalAlignment(TextEnums.HAlignment.AlignLeft)");

            BindingNode binding = Assert.Single(ast.Members.OfType<BindingNode>());
            EnumReference enumReference = Assert.IsType<EnumReference>(binding.Value);
            Assert.Equal("TextEnums.HAlignment", enumReference.TypeName);
            Assert.Equal("AlignLeft", enumReference.MemberName);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_ViewModelStateAndCommandReferences_AreCaptured()
        {
            DslCallNode stateNode = Extract("Text().Text(Vm.Title)");
            DslCallNode commandNode = Extract("Button().OnClicked(Vm.Increment)");

            DslStateReference stateReference = Assert.IsType<DslStateReference>(Assert.Single(stateNode.Properties).Value);
            DslSignalHandlerCall handler = Assert.Single(commandNode.SignalHandlers);
            Assert.Equal("Vm", stateReference.ReceiverName);
            Assert.Equal("Title", stateReference.MemberName);
            Assert.NotNull(handler.CommandReference);
            Assert.Equal("Increment", handler.CommandReference.MethodName);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_NestedObjectPropertyValue_LowersToObjectValue()
        {
            ObjectDefinitionNode ast = TransformToAst("Rectangle().Gradient(LinearGradient())");

            BindingNode binding = Assert.Single(ast.Members.OfType<BindingNode>());
            ObjectValue objectValue = Assert.IsType<ObjectValue>(binding.Value);
            Assert.Equal("LinearGradient", objectValue.Object.TypeName);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_SourceMappings_CaptureTransformedNodeLocations()
        {
            DslTransformResult result = TransformExpression("Rectangle().Width(100).Child(Text().Text(\"hi\"))");

            Assert.NotEmpty(result.SourceMappings);
            Assert.Contains(result.SourceMappings, static mapping => mapping.NodeKind == NodeKind.ObjectDefinition.ToString() && mapping.Symbol == "Rectangle");
            Assert.All(result.SourceMappings, static mapping =>
            {
                Assert.NotNull(mapping.Source.FilePath);
                Assert.True(mapping.Source.Line > 0);
                Assert.True(mapping.Source.Column > 0);
            });
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_Diagnostics_CoverTransformCodeRange()
        {
            AssertDiagnostic(DiagnosticCodes.UnknownQmlType, new DslCallNode(
                "UnknownWidget",
                ImmutableArray.Create(new DslPropertyCall("Width", 100)),
                ImmutableArray<DslBindingCall>.Empty,
                ImmutableArray<DslSignalHandlerCall>.Empty,
                ImmutableArray<DslGroupedCall>.Empty,
                ImmutableArray<DslAttachedCall>.Empty,
                ImmutableArray<DslCallNode>.Empty));
            AssertDiagnostic(DiagnosticCodes.InvalidPropertyValue, new DslCallNode(
                "Rectangle",
                ImmutableArray.Create(new DslPropertyCall("Missing", 1)),
                ImmutableArray<DslBindingCall>.Empty,
                ImmutableArray<DslSignalHandlerCall>.Empty,
                ImmutableArray<DslGroupedCall>.Empty,
                ImmutableArray<DslAttachedCall>.Empty,
                ImmutableArray<DslCallNode>.Empty));
            AssertDiagnostic(DiagnosticCodes.UnresolvedTypeReference, new DslCallNode(
                "Rectangle",
                ImmutableArray.Create(new DslPropertyCall("Gradient", new DslEnumReference(string.Empty, null, "MissingOwner"))),
                ImmutableArray<DslBindingCall>.Empty,
                ImmutableArray<DslSignalHandlerCall>.Empty,
                ImmutableArray<DslGroupedCall>.Empty,
                ImmutableArray<DslAttachedCall>.Empty,
                ImmutableArray<DslCallNode>.Empty));
            AssertDiagnostic(DiagnosticCodes.InvalidCallChain, DiagnosticCodes.InvalidCallChain);
            AssertDiagnostic(DiagnosticCodes.UnsupportedDslPattern, new DslCallNode(
                "Button",
                ImmutableArray<DslPropertyCall>.Empty,
                ImmutableArray<DslBindingCall>.Empty,
                ImmutableArray.Create(new DslSignalHandlerCall("OnClicked", string.Empty)),
                ImmutableArray<DslGroupedCall>.Empty,
                ImmutableArray<DslAttachedCall>.Empty,
                ImmutableArray<DslCallNode>.Empty));
            AssertDiagnostic(DiagnosticCodes.BindExpressionEmpty, new DslCallNode(
                "Rectangle",
                ImmutableArray<DslPropertyCall>.Empty,
                ImmutableArray.Create(new DslBindingCall("Width", string.Empty)),
                ImmutableArray<DslSignalHandlerCall>.Empty,
                ImmutableArray<DslGroupedCall>.Empty,
                ImmutableArray<DslAttachedCall>.Empty,
                ImmutableArray<DslCallNode>.Empty));
            AssertDiagnostic(DiagnosticCodes.InvalidChildType, new DslCallNode(
                "Rectangle",
                ImmutableArray<DslPropertyCall>.Empty,
                ImmutableArray<DslBindingCall>.Empty,
                ImmutableArray<DslSignalHandlerCall>.Empty,
                ImmutableArray<DslGroupedCall>.Empty,
                ImmutableArray<DslAttachedCall>.Empty,
                ImmutableArray.Create(new DslCallNode(
                    "notObject",
                    ImmutableArray<DslPropertyCall>.Empty,
                    ImmutableArray<DslBindingCall>.Empty,
                    ImmutableArray<DslSignalHandlerCall>.Empty,
                    ImmutableArray<DslGroupedCall>.Empty,
                    ImmutableArray<DslAttachedCall>.Empty,
                    ImmutableArray<DslCallNode>.Empty))));
            AssertDiagnostic(DiagnosticCodes.PropertyTypeMismatch, new DslCallNode(
                "Rectangle",
                ImmutableArray.Create(new DslPropertyCall("Width", "wide")),
                ImmutableArray<DslBindingCall>.Empty,
                ImmutableArray<DslSignalHandlerCall>.Empty,
                ImmutableArray<DslGroupedCall>.Empty,
                ImmutableArray<DslAttachedCall>.Empty,
                ImmutableArray<DslCallNode>.Empty));
            AssertDiagnostic(DiagnosticCodes.UnknownSignal, new DslCallNode(
                "Rectangle",
                ImmutableArray<DslPropertyCall>.Empty,
                ImmutableArray<DslBindingCall>.Empty,
                ImmutableArray.Create(new DslSignalHandlerCall("OnClicked", "doIt()")),
                ImmutableArray<DslGroupedCall>.Empty,
                ImmutableArray<DslAttachedCall>.Empty,
                ImmutableArray<DslCallNode>.Empty));
            AssertDiagnostic(DiagnosticCodes.UnknownAttachedType, new DslCallNode(
                "Rectangle",
                ImmutableArray<DslPropertyCall>.Empty,
                ImmutableArray<DslBindingCall>.Empty,
                ImmutableArray<DslSignalHandlerCall>.Empty,
                ImmutableArray<DslGroupedCall>.Empty,
                ImmutableArray.Create(new DslAttachedCall("UnknownAttached", ImmutableArray<DslPropertyCall>.Empty)),
                ImmutableArray<DslCallNode>.Empty));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_UnknownType_DoesNotCascadeIntoPropertyDiagnostics()
        {
            DslTransformResult result = transformer.TransformCallTree(new DslCallNode(
                "UnknownWidget",
                ImmutableArray.Create(new DslPropertyCall("Width", 100)),
                ImmutableArray<DslBindingCall>.Empty,
                ImmutableArray<DslSignalHandlerCall>.Empty,
                ImmutableArray<DslGroupedCall>.Empty,
                ImmutableArray<DslAttachedCall>.Empty,
                ImmutableArray<DslCallNode>.Empty), registry);

            Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == DiagnosticCodes.UnknownQmlType);
            Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Code == DiagnosticCodes.InvalidPropertyValue);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void DslTransformer_Transform_DiscoveredViewBuildMethod_ReturnsDocumentAndRootCall()
        {
            ProjectContext context = CreateContext("Rectangle().Width(100)");
            CSharpAnalyzer analyzer = new();
            DiscoveredView view = Assert.Single(analyzer.DiscoverViews(context));

            DslTransformResult result = transformer.Transform(view, context, registry);

            Assert.Equal("Rectangle", result.RootCall.TypeName);
            Assert.Equal("Rectangle", result.Document.RootObject.TypeName);
            Assert.Empty(result.Diagnostics);
        }

        private ObjectDefinitionNode TransformToAst(string expression)
        {
            return TransformExpression(expression).Document.RootObject;
        }

        private DslTransformResult TransformExpression(string expression)
        {
            DslCallNode node = Extract(expression);
            return transformer.TransformCallTree(node, registry);
        }

        private DslCallNode Extract(string expression)
        {
            (CSharpCompilation compilation, SemanticModel model, InvocationExpressionSyntax invocation) = CreateInvocation(expression);
            _ = compilation;
            return transformer.ExtractCallTree(invocation, model);
        }

        private static (CSharpCompilation Compilation, SemanticModel Model, InvocationExpressionSyntax Invocation) CreateInvocation(string expression)
        {
            string source = CreateViewSource(expression);
            CSharpCompilation compilation = RoslynTestHelper.CreateCompilation(ImmutableArray.Create(("View.cs", source)));
            SyntaxTree syntaxTree = compilation.SyntaxTrees.Single();
            SemanticModel model = compilation.GetSemanticModel(syntaxTree);
            SyntaxNode root = syntaxTree.GetRoot();
            ReturnStatementSyntax returnStatement = root.DescendantNodes().OfType<ReturnStatementSyntax>().Single();
            InvocationExpressionSyntax invocation = Assert.IsType<InvocationExpressionSyntax>(returnStatement.Expression);
            return (compilation, model, invocation);
        }

        private static ProjectContext CreateContext(string expression)
        {
            string source = CreateViewSource(expression);
            CSharpCompilation compilation = RoslynTestHelper.CreateCompilation(ImmutableArray.Create(("View.cs", source)));
            CSharpAnalyzer analyzer = new();
            return analyzer.CreateInMemoryProjectContext(
                CompilerTestFixtures.DefaultOptions,
                compilation,
                ImmutableArray.Create("View.cs"));
        }

        private void AssertDiagnostic(string expectedCode, DslCallNode node)
        {
            DslTransformResult result = transformer.TransformCallTree(node, registry);

            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
        }

        private static void AssertDiagnostic(string expectedCode, string code)
        {
            CompilerDiagnostic diagnostic = new(
                code,
                DiagnosticSeverity.Error,
                DiagnosticMessageCatalog.FormatMessage(code),
                SourceLocation.LineColumn(1, 1),
                "Transform");

            Assert.Equal(expectedCode, diagnostic.Code);
            Assert.NotEqual("Unknown compiler diagnostic.", diagnostic.Message);
        }

        private static string CreateViewSource(string expression)
        {
            return $$"""
                using System;
                using QmlSharp.Core;
                using QmlSharp.Dsl;
                using QmlSharp.Qml.Ast;

                namespace TestApp;

                [ViewModel]
                public sealed class SampleViewModel
                {
                    [State] public string Title { get; set; } = "";
                    [Command] public void Increment() { }
                }

                public sealed class SampleView : View<SampleViewModel>
                {
                    public override object Build()
                    {
                        return {{expression}};
                    }

                    private static DslBuilder Rectangle() => new("Rectangle");
                    private static DslBuilder Text() => new("Text");
                    private static DslBuilder Button() => new("Button");
                    private static DslBuilder MouseArea() => new("MouseArea");
                    private static DslBuilder LinearGradient() => new("LinearGradient");

                    private sealed class DslBuilder : IObjectBuilder
                    {
                        public DslBuilder(string qmlTypeName)
                        {
                            QmlTypeName = qmlTypeName;
                        }

                        public string QmlTypeName { get; }

                        public IObjectBuilder Id(string id) => this;
                        public IObjectBuilder Child(IObjectBuilder child) => this;
                        public IObjectBuilder Children(params IObjectBuilder[] children) => this;
                        public IObjectBuilder SetProperty(string propertyName, object? value) => this;
                        public IObjectBuilder SetBinding(string propertyName, string value) => this;
                        public IObjectBuilder AddGrouped(string groupName, Action<IPropertyCollector> configure) => this;
                        public IObjectBuilder AddAttached(string attachedTypeName, Action<IPropertyCollector> configure) => this;
                        public IObjectBuilder HandleSignal(string handlerName, string body) => this;
                        public ObjectDefinitionNode Build() => throw new InvalidOperationException();

                        public DslBuilder Width(int value) => this;
                        public DslBuilder Width(string value) => this;
                        public DslBuilder WidthBind(string expression) => this;
                        public DslBuilder Height(int value) => this;
                        public DslBuilder Text(string value) => this;
                        public DslBuilder Text(DslBuilder value) => this;
                        public DslBuilder TextBind(string expression) => this;
                        public DslBuilder Visible(bool value) => this;
                        public DslBuilder Color(string value) => this;
                        public DslBuilder Gradient(DslBuilder value) => this;
                        public DslBuilder HorizontalAlignment(int value) => this;
                        public DslBuilder HorizontalAlignment(object value) => this;
                        public DslBuilder Border(Func<DslBuilder, DslBuilder> configure) => this;
                        public DslBuilder Layout(Func<DslBuilder, DslBuilder> configure) => this;
                        public DslBuilder FillWidth(bool value) => this;
                        public DslBuilder OnClicked(Action action) => this;
                        public DslBuilder OnClicked(string body) => this;
                    }

                    private static class TextEnums
                    {
                        public static class HAlignment
                        {
                            public const int AlignLeft = 1;
                        }
                    }
                }
                """;
        }
    }
}
