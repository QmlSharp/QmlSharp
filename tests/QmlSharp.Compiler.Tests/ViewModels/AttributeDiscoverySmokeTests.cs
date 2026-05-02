using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.ViewModels
{
    public sealed class AttributeDiscoverySmokeTests
    {
        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void RoslynSemanticModel_DiscoversViewModelAttributes()
        {
            CSharpCompilation compilation = RoslynTestHelper.CreateCompilation(CompilerSourceFixtures.TodoViewModelSource);
            INamedTypeSymbol? viewModelSymbol = compilation
                .GlobalNamespace
                .GetNamespaceMembers()
                .Single(namespaceSymbol => namespaceSymbol.Name == "TestApp")
                .GetTypeMembers("TodoViewModel")
                .SingleOrDefault();

            Assert.NotNull(viewModelSymbol);
            Assert.Contains(viewModelSymbol.GetAttributes(), AttributeClassNameEquals("QmlSharp.Core.ViewModelAttribute"));

            IPropertySymbol titleProperty = viewModelSymbol.GetMembers("Title").OfType<IPropertySymbol>().Single();
            IPropertySymbol countProperty = viewModelSymbol.GetMembers("ItemCount").OfType<IPropertySymbol>().Single();
            IMethodSymbol addItemMethod = viewModelSymbol.GetMembers("AddItem").OfType<IMethodSymbol>().Single();
            IEventSymbol showToastEvent = viewModelSymbol.GetMembers("ShowToast").OfType<IEventSymbol>().Single();

            Assert.Contains(titleProperty.GetAttributes(), AttributeClassNameEquals("QmlSharp.Core.StateAttribute"));
            AttributeData readonlyState = countProperty.GetAttributes()
                .Single(attribute => StringComparer.Ordinal.Equals(attribute.AttributeClass?.ToDisplayString(), "QmlSharp.Core.StateAttribute"));
            Assert.Contains(readonlyState.NamedArguments, argument => argument.Key == "Readonly" && true.Equals(argument.Value.Value));
            Assert.Contains(addItemMethod.GetAttributes(), AttributeClassNameEquals("QmlSharp.Core.CommandAttribute"));
            Assert.Contains(showToastEvent.GetAttributes(), AttributeClassNameEquals("QmlSharp.Core.EffectAttribute"));
        }

        [Fact]
        [Trait("Category", TestCategories.Smoke)]
        public void RoslynSemanticModel_BindsViewBaseGenericType()
        {
            CSharpCompilation compilation = RoslynTestHelper.CreateCompilation(
                CompilerSourceFixtures.CounterViewModelSource,
                CompilerSourceFixtures.CounterViewSource);

            INamedTypeSymbol viewSymbol = compilation
                .GlobalNamespace
                .GetNamespaceMembers()
                .Single(namespaceSymbol => namespaceSymbol.Name == "TestApp")
                .GetTypeMembers("CounterView")
                .Single();

            INamedTypeSymbol? baseType = viewSymbol.BaseType;

            Assert.NotNull(baseType);
            Assert.Equal("QmlSharp.Core.View<TestApp.CounterViewModel>", baseType.ToDisplayString());
        }

        private static Predicate<AttributeData> AttributeClassNameEquals(string expectedName)
        {
            return attribute => StringComparer.Ordinal.Equals(attribute.AttributeClass?.ToDisplayString(), expectedName);
        }
    }
}
