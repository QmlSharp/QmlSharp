using Microsoft.CodeAnalysis.CSharp;
using QmlSharp.Qml.Ast;

namespace QmlSharp.Compiler.Tests.Fixtures
{
    internal static class CompilerTestFixtures
    {
        public static CompilerOptions DefaultOptions { get; } = new()
        {
            ProjectPath = "TestApp.csproj",
            OutputDir = "dist",
            ModuleUriPrefix = "QmlSharp.TestApp",
        };

        public static ProjectContext CreateCounterContext()
        {
            CSharpCompilation compilation = RoslynTestHelper.CreateCompilation(
                CompilerSourceFixtures.CounterViewModelSource,
                CompilerSourceFixtures.CounterViewSource);

            return new ProjectContext(
                compilation,
                ImmutableArray.Create("CounterViewModel.cs", "CounterView.cs"),
                DefaultOptions,
                new DiagnosticReporter());
        }

        public static ProjectContext CreateMultiViewModelContext()
        {
            ImmutableArray<(string FileName, string Source)> sources = CompilerSourceFixtures.MultiFileProject();
            CSharpCompilation compilation = RoslynTestHelper.CreateCompilation(sources);

            return new ProjectContext(
                compilation,
                sources.Select(source => source.FileName).ToImmutableArray(),
                DefaultOptions,
                new DiagnosticReporter());
        }

        public static ProjectContext CreateNFileContext(int count)
        {
            ImmutableArray<(string FileName, string Source)> sources = CompilerSourceFixtures.SyntheticProject(count);
            CSharpCompilation compilation = RoslynTestHelper.CreateCompilation(sources);

            return new ProjectContext(
                compilation,
                sources.Select(source => source.FileName).ToImmutableArray(),
                DefaultOptions,
                new DiagnosticReporter());
        }

        public static CompilerOptions CreateCounterOptions()
        {
            return DefaultOptions with
            {
                ProjectPath = "CounterApp.csproj",
                OutputDir = "dist/counter",
            };
        }

        public static CompilerOptions CreateNFileOptions(int count)
        {
            return DefaultOptions with
            {
                ProjectPath = $"Synthetic{count}.csproj",
                OutputDir = $"dist/synthetic-{count}",
            };
        }

        public static ViewModelSchema CreateCounterSchema()
        {
            return new ViewModelSchema(
                SchemaVersion: "1.0",
                ClassName: "CounterViewModel",
                ModuleName: "TestApp",
                ModuleUri: "QmlSharp.TestApp",
                ModuleVersion: new QmlVersion(1, 0),
                Version: 2,
                CompilerSlotKey: "CounterView::__qmlsharp_vm0",
                Properties: ImmutableArray.Create(new StateEntry("count", "int", "0", ReadOnly: false, MemberId: 1, SourceName: "Count")),
                Commands: ImmutableArray.Create(new CommandEntry("increment", ImmutableArray<ParameterEntry>.Empty, CommandId: 2, SourceName: "Increment")),
                Effects: ImmutableArray<EffectEntry>.Empty,
                Lifecycle: new LifecycleInfo(OnMounted: false, OnUnmounting: false, HotReload: true));
        }

        public static ViewModelSchema CreateTodoSchema()
        {
            return new ViewModelSchema(
                SchemaVersion: "1.0",
                ClassName: "TodoViewModel",
                ModuleName: "TestApp",
                ModuleUri: "QmlSharp.TestApp",
                ModuleVersion: new QmlVersion(1, 0),
                Version: 2,
                CompilerSlotKey: "TodoView::__qmlsharp_vm0",
                Properties: ImmutableArray.Create(
                    new StateEntry("title", "string", "\"\"", ReadOnly: false, MemberId: 11, SourceName: "Title"),
                    new StateEntry("itemCount", "int", "0", ReadOnly: true, MemberId: 12, SourceName: "ItemCount")),
                Commands: ImmutableArray.Create(
                    new CommandEntry("addItem", ImmutableArray<ParameterEntry>.Empty, CommandId: 13, SourceName: "AddItem"),
                    new CommandEntry("removeItem", ImmutableArray.Create(new ParameterEntry("index", "int")), CommandId: 14, SourceName: "RemoveItem")),
                Effects: ImmutableArray.Create(new EffectEntry("showToast", "string", EffectId: 15, ImmutableArray.Create(new ParameterEntry("message", "string")), SourceName: "ShowToast")),
                Lifecycle: new LifecycleInfo(OnMounted: false, OnUnmounting: false, HotReload: true));
        }

        public static QmlDocument CreateCounterAstFixture()
        {
            return new QmlDocument
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Column",
                    Members = ImmutableArray.Create<AstNode>(
                        new ObjectDefinitionNode
                        {
                            TypeName = "Text",
                            Members = ImmutableArray.Create<AstNode>(
                                new BindingNode
                                {
                                    PropertyName = "text",
                                    Value = new ScriptExpression("count.toString()"),
                                }),
                        }),
                },
            };
        }

        public static DslCallNode CreateSimpleRectangleDsl()
        {
            return new DslCallNode(
                "Rectangle",
                ImmutableArray.Create(new DslPropertyCall("Width", 100)),
                ImmutableArray<DslBindingCall>.Empty,
                ImmutableArray<DslSignalHandlerCall>.Empty,
                ImmutableArray<DslGroupedCall>.Empty,
                ImmutableArray<DslAttachedCall>.Empty,
                ImmutableArray<DslCallNode>.Empty);
        }
    }
}
