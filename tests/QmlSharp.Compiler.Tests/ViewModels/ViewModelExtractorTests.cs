using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.ViewModels
{
    public sealed class ViewModelExtractorTests
    {
        private readonly CSharpAnalyzer analyzer = new();
        private readonly ViewModelExtractor extractor = new();

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE01_ExtractsIntStateProperty()
        {
            ViewModelSchema schema = ExtractSingle("""
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class CounterViewModel
                {
                    [State] public int Count { get; set; }
                }
                """);

            StateEntry property = Assert.Single(schema.Properties);
            Assert.Equal("count", property.Name);
            Assert.Equal("int", property.Type);
            Assert.Equal("0", property.DefaultValue);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE02_ExtractsStringStateProperty()
        {
            ViewModelSchema schema = ExtractSingle("""
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class TitleViewModel
                {
                    [State] public string Title { get; set; } = "Ready";
                }
                """);

            StateEntry property = Assert.Single(schema.Properties);
            Assert.Equal("title", property.Name);
            Assert.Equal("string", property.Type);
            Assert.Equal("Ready", property.DefaultValue);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE03_ExtractsStateReadOnlyFlag()
        {
            ViewModelSchema schema = ExtractSingle("""
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class CounterViewModel
                {
                    [State(Readonly = true)] public int Count { get; set; }
                }
                """);

            Assert.True(Assert.Single(schema.Properties).ReadOnly);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE04_ExtractsStateDeferredFlag()
        {
            ViewModelSchema schema = ExtractSingle("""
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class CounterViewModel
                {
                    [State(Deferred = true)] public int Count { get; set; }
                }
                """);

            Assert.True(Assert.Single(schema.Properties).Deferred);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE05_ExtractsVoidCommand()
        {
            ViewModelSchema schema = ExtractSingle("""
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class CounterViewModel
                {
                    [Command] public void Increment() { }
                }
                """);

            CommandEntry command = Assert.Single(schema.Commands);
            Assert.Equal("increment", command.Name);
            Assert.False(command.Async);
            Assert.Empty(command.Parameters);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE06_SupportsAsyncTaskCommand()
        {
            ViewModelSchema schema = ExtractSingle("""
                using System.Threading.Tasks;
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class LoginViewModel
                {
                    [Command] public Task Login() => Task.CompletedTask;
                }
                """);

            Assert.True(Assert.Single(schema.Commands).Async);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE07_ExtractsCommandParameters()
        {
            ViewModelSchema schema = ExtractSingle("""
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class LoginViewModel
                {
                    [Command] public void Login(string username, bool rememberMe, int retryCount) { }
                }
                """);

            CommandEntry command = Assert.Single(schema.Commands);
            Assert.Equal(
                [new ParameterEntry("username", "string"), new ParameterEntry("rememberMe", "bool"), new ParameterEntry("retryCount", "int")],
                command.Parameters.ToArray());
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE08_ExtractsEffectEvent()
        {
            ViewModelSchema schema = ExtractSingle("""
                using System;
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class LoginViewModel
                {
                    [Effect] public event Action? LoggedIn;
                }
                """);

            EffectEntry effect = Assert.Single(schema.Effects);
            Assert.Equal("loggedIn", effect.Name);
            Assert.Equal("void", effect.PayloadType);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE09_ExtractsEffectPayloadParameter()
        {
            ViewModelSchema schema = ExtractSingle("""
                using System;
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class LoginViewModel
                {
                    [Effect] public event Action<string>? LoginFailed;
                }
                """);

            EffectEntry effect = Assert.Single(schema.Effects);
            Assert.Equal("string", effect.PayloadType);
            Assert.Equal(new ParameterEntry("payload", "string"), Assert.Single(effect.Parameters));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE10_ExtractsOnMountedLifecycleMethod()
        {
            ViewModelSchema schema = ExtractSingle("""
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class LifecycleViewModel
                {
                    public void OnMounted() { }
                }
                """);

            Assert.True(schema.Lifecycle.OnMounted);
            Assert.False(schema.Lifecycle.OnUnmounting);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE11_ExtractsOnUnmountingLifecycleMethod()
        {
            ViewModelSchema schema = ExtractSingle("""
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class LifecycleViewModel
                {
                    public void OnUnmounting() { }
                }
                """);

            Assert.False(schema.Lifecycle.OnMounted);
            Assert.True(schema.Lifecycle.OnUnmounting);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE12_ConvertsPascalCaseMembersToCamelCaseQmlNames()
        {
            ViewModelSchema schema = ExtractSingle("""
                using System;
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class NamingViewModel
                {
                    [State] public int ItemCount { get; set; }
                    [Command] public void AddItem() { }
                    [Effect] public event Action? ItemAdded;
                }
                """);

            Assert.Equal("itemCount", Assert.Single(schema.Properties).Name);
            Assert.Equal("addItem", Assert.Single(schema.Commands).Name);
            Assert.Equal("itemAdded", Assert.Single(schema.Effects).Name);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE13_CommandReturningValue_ReportsA009()
        {
            ImmutableArray<CompilerDiagnostic> diagnostics = ValidateSingle("""
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class BadCommandViewModel
                {
                    [Command] public int Increment() => 1;
                }
                """);

            Assert.Contains(diagnostics, HasCode(DiagnosticCodes.CommandMustBeVoid));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE14_StaticState_ReportsA013()
        {
            ImmutableArray<CompilerDiagnostic> diagnostics = ValidateSingle("""
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class BadStateViewModel
                {
                    [State] public static int Count { get; set; }
                }
                """);

            Assert.Contains(diagnostics, HasCode(DiagnosticCodes.StaticMemberNotAllowed));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE15_DuplicateStateName_ReportsA005()
        {
            ImmutableArray<CompilerDiagnostic> diagnostics = ValidateSingle("""
                using QmlSharp.Core;
                namespace TestApp;
                public class BaseViewModel
                {
                    [State] public int Count { get; set; }
                }

                [ViewModel]
                public sealed class DuplicateStateViewModel : BaseViewModel
                {
                    [State] public string count { get; set; } = "";
                }
                """);

            Assert.Contains(diagnostics, HasCode(DiagnosticCodes.DuplicateStateName));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_VE16_ExtractAll_ReturnsAllViewModelsInStableOrder()
        {
            ProjectContext context = CompilerTestFixtures.CreateMultiViewModelContext();
            ImmutableArray<DiscoveredViewModel> viewModels = analyzer.DiscoverViewModels(context);

            ImmutableArray<ViewModelSchema> schemas = extractor.ExtractAll(viewModels, context, new IdAllocator());

            Assert.Equal(["CounterViewModel", "TodoViewModel"], schemas.Select(static schema => schema.ClassName).ToArray());
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_ExtractsListAndJsonSchemaTypes()
        {
            ViewModelSchema schema = ExtractSingle("""
                using System.Collections.Generic;
                using System.Text.Json;
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class TypesViewModel
                {
                    [State] public IReadOnlyList<string> Items { get; set; } = [];
                    [State] public JsonElement Payload { get; set; }
                }
                """);

            Assert.Equal(["items:list<string>", "payload:json"], schema.Properties.Select(static property => $"{property.Name}:{property.Type}").ToArray());
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_UnsupportedStateType_ReportsA008()
        {
            ImmutableArray<CompilerDiagnostic> diagnostics = ValidateSingle("""
                using System;
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class BadStateViewModel
                {
                    [State] public DateTime CreatedAt { get; set; }
                }
                """);

            Assert.Contains(diagnostics, HasCode(DiagnosticCodes.UnsupportedStateType));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_InvalidPrivateState_ReportsA001()
        {
            ImmutableArray<CompilerDiagnostic> diagnostics = ValidateSingle("""
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class BadStateViewModel
                {
                    [State] private int Count { get; set; }
                }
                """);

            Assert.Contains(diagnostics, HasCode(DiagnosticCodes.InvalidStateAttribute));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_InvalidPrivateCommand_ReportsA002()
        {
            ImmutableArray<CompilerDiagnostic> diagnostics = ValidateSingle("""
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class BadCommandViewModel
                {
                    [Command] private void Save() { }
                }
                """);

            Assert.Contains(diagnostics, HasCode(DiagnosticCodes.InvalidCommandAttribute));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_UnsupportedMultiPayloadEffect_ReportsA003()
        {
            ImmutableArray<CompilerDiagnostic> diagnostics = ValidateSingle("""
                using System;
                using QmlSharp.Core;
                namespace TestApp;
                [ViewModel]
                public sealed class BadEffectViewModel
                {
                    [Effect] public event Action<string, int>? Toast;
                }
                """);

            Assert.Contains(diagnostics, HasCode(DiagnosticCodes.InvalidEffectAttribute));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_EffectWithNonActionDelegate_ReportsA010()
        {
            ImmutableArray<CompilerDiagnostic> diagnostics = ValidateSingle("""
                using QmlSharp.Core;
                namespace TestApp;
                public delegate void ToastDelegate(string message);
                [ViewModel]
                public sealed class BadEffectViewModel
                {
                    [Effect] public event ToastDelegate? Toast;
                }
                """);

            Assert.Contains(diagnostics, HasCode(DiagnosticCodes.EffectMustBeEvent));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_DuplicateCommandAndEffectNames_ReportA006AndA007()
        {
            ImmutableArray<CompilerDiagnostic> diagnostics = ValidateSingle("""
                using System;
                using QmlSharp.Core;
                namespace TestApp;
                public class BaseViewModel
                {
                    [Command] public void Save() { }
                    [Effect] public event Action? Saved;
                }

                [ViewModel]
                public sealed class DuplicateViewModel : BaseViewModel
                {
                    [Command] public void save() { }
                    [Effect] public event Action? saved;
                }
                """);

            Assert.Contains(diagnostics, HasCode(DiagnosticCodes.DuplicateCommandName));
            Assert.Contains(diagnostics, HasCode(DiagnosticCodes.DuplicateEffectName));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void ViewModelExtractor_A00RangeDiagnostics_AllHaveCatalogTemplates()
        {
            string[] analyzerCodes =
            [
                DiagnosticCodes.InvalidStateAttribute,
                DiagnosticCodes.InvalidCommandAttribute,
                DiagnosticCodes.InvalidEffectAttribute,
                DiagnosticCodes.ViewModelNotFound,
                DiagnosticCodes.DuplicateStateName,
                DiagnosticCodes.DuplicateCommandName,
                DiagnosticCodes.DuplicateEffectName,
                DiagnosticCodes.UnsupportedStateType,
                DiagnosticCodes.CommandMustBeVoid,
                DiagnosticCodes.EffectMustBeEvent,
                DiagnosticCodes.ViewMissingBuildMethod,
                DiagnosticCodes.ViewModelMissingAttribute,
                DiagnosticCodes.StaticMemberNotAllowed,
                DiagnosticCodes.MultipleViewModelBindings,
            ];

            Assert.All(analyzerCodes, code => Assert.NotEqual("Unknown compiler diagnostic.", DiagnosticMessageCatalog.GetTemplate(code)));
        }

        private ViewModelSchema ExtractSingle(string source)
        {
            ProjectContext context = CreateContext(source);
            DiscoveredViewModel viewModel = Assert.Single(analyzer.DiscoverViewModels(context));
            return extractor.Extract(viewModel, context, new IdAllocator());
        }

        private ImmutableArray<CompilerDiagnostic> ValidateSingle(string source)
        {
            ProjectContext context = CreateContext(source);
            DiscoveredViewModel viewModel = Assert.Single(analyzer.DiscoverViewModels(context));
            return extractor.Validate(viewModel, context);
        }

        private ProjectContext CreateContext(string source)
        {
            return analyzer.CreateInMemoryProjectContext(
                CompilerTestFixtures.DefaultOptions,
                RoslynTestHelper.CreateCompilation(source),
                ImmutableArray.Create("Source0.cs"));
        }

        private static Predicate<CompilerDiagnostic> HasCode(string code)
        {
            return diagnostic => StringComparer.Ordinal.Equals(diagnostic.Code, code);
        }
    }
}
