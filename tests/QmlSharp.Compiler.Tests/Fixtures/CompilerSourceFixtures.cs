using System.Text;

namespace QmlSharp.Compiler.Tests.Fixtures
{
    internal static class CompilerSourceFixtures
    {
        public const string CounterViewModelSource = """
            using QmlSharp.Core;

            namespace TestApp;

            [ViewModel]
            public sealed class CounterViewModel
            {
                [State] public int Count { get; set; }

                [Command]
                public void Increment()
                {
                    Count++;
                }
            }
            """;

        public const string CounterViewSource = """
            using QmlSharp.Core;
            using QmlSharp.Dsl;

            namespace TestApp;

            public sealed class CounterView : View<CounterViewModel>
            {
                public override IObjectBuilder Build()
                {
                    throw new System.NotImplementedException();
                }
            }
            """;

        public const string TodoViewModelSource = """
            using System;
            using System.Collections.Generic;
            using QmlSharp.Core;

            namespace TestApp;

            [ViewModel]
            public sealed class TodoViewModel
            {
                [State] public string Title { get; set; } = "";
                [State(Readonly = true)] public int ItemCount { get; set; }
                [Command] public void AddItem() { }
                [Command] public void RemoveItem(int index) { }
                [Effect] public event Action<string>? ShowToast;
            }
            """;

        public const string TodoViewSource = """
            using QmlSharp.Core;
            using QmlSharp.Dsl;

            namespace TestApp;

            public sealed class TodoView : View<TodoViewModel>
            {
                public override IObjectBuilder Build()
                {
                    throw new System.NotImplementedException();
                }
            }
            """;

        public static ImmutableArray<(string FileName, string Source)> MultiFileProject()
        {
            return ImmutableArray.Create(
                ("CounterViewModel.cs", CounterViewModelSource),
                ("CounterView.cs", CounterViewSource),
                ("TodoViewModel.cs", TodoViewModelSource),
                ("TodoView.cs", TodoViewSource));
        }

        public static ImmutableArray<(string FileName, string Source)> SyntheticProject(int fileCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileCount);

            ImmutableArray<(string FileName, string Source)>.Builder sources = ImmutableArray.CreateBuilder<(string FileName, string Source)>(fileCount * 2);

            for (int index = 0; index < fileCount; index++)
            {
                string name = $"Synthetic{index}";
                sources.Add(($"{name}ViewModel.cs", CreateSyntheticViewModel(name)));
                sources.Add(($"{name}View.cs", CreateSyntheticView(name)));
            }

            return sources.ToImmutable();
        }

        private static string CreateSyntheticViewModel(string name)
        {
            StringBuilder builder = new();
            _ = builder.AppendLine("using QmlSharp.Core;");
            _ = builder.AppendLine("namespace TestApp;");
            _ = builder.AppendLine("[ViewModel]");
            _ = builder.AppendLine($"public sealed class {name}ViewModel");
            _ = builder.AppendLine("{");
            _ = builder.AppendLine("    [State] public int Count { get; set; }");
            _ = builder.AppendLine("    [Command] public void Increment() { Count++; }");
            _ = builder.AppendLine("}");
            return builder.ToString();
        }

        private static string CreateSyntheticView(string name)
        {
            StringBuilder builder = new();
            _ = builder.AppendLine("using QmlSharp.Core;");
            _ = builder.AppendLine("using QmlSharp.Dsl;");
            _ = builder.AppendLine("namespace TestApp;");
            _ = builder.AppendLine($"public sealed class {name}View : View<{name}ViewModel>");
            _ = builder.AppendLine("{");
            _ = builder.AppendLine("    public override IObjectBuilder Build()");
            _ = builder.AppendLine("    {");
            _ = builder.AppendLine("        throw new System.NotImplementedException();");
            _ = builder.AppendLine("    }");
            _ = builder.AppendLine("}");
            return builder.ToString();
        }
    }
}
