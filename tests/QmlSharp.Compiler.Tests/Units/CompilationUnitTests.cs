using QmlSharp.Compiler.Tests.Fixtures;
using QmlSharp.Qml.Ast;

namespace QmlSharp.Compiler.Tests.Units
{
    public sealed class CompilationUnitTests
    {
        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CompilationUnit_CU01_SuccessfulUnitHasAllFieldsPopulated()
        {
            CompilationUnit unit = SuccessfulUnit();

            Assert.True(unit.Success);
            Assert.Equal("CounterView.cs", unit.SourceFilePath);
            Assert.Equal("CounterView", unit.ViewClassName);
            Assert.Equal("CounterViewModel", unit.ViewModelClassName);
            Assert.False(string.IsNullOrWhiteSpace(unit.QmlText));
            Assert.NotNull(unit.Schema);
            Assert.NotNull(unit.Document);
            Assert.NotNull(unit.SourceMap);
            Assert.Empty(unit.Diagnostics);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CompilationUnit_CU02_FailedUnitHasDiagnostics()
        {
            CompilerDiagnostic diagnostic = new(
                DiagnosticCodes.EmitFailed,
                DiagnosticSeverity.Error,
                "QML emission failed.",
                new SourceLocation("BrokenView.cs", 3, 1),
                "EmittingQml");

            CompilationUnit unit = SuccessfulUnit() with
            {
                SourceFilePath = "BrokenView.cs",
                QmlText = null,
                Diagnostics = ImmutableArray.Create(diagnostic),
            };

            Assert.False(unit.Success);
            Assert.Null(unit.QmlText);
            Assert.Equal(DiagnosticCodes.EmitFailed, unit.Diagnostics.Single().Code);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CompilationResult_CU03_SuccessTrueWhenNoErrors()
        {
            CompilationResult result = CompilationResult.FromUnits(ImmutableArray.Create(SuccessfulUnit()));

            Assert.True(result.Success);
            Assert.Equal(1, result.Stats.TotalFiles);
            Assert.Equal(1, result.Stats.SuccessfulFiles);
            Assert.Equal(0, result.Stats.FailedFiles);
            Assert.Equal(0, result.Stats.Errors);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void CompilationResult_CU04_SuccessFalseWithErrorsAndAggregatesDiagnostics()
        {
            CompilerDiagnostic warning = new(DiagnosticCodes.ImportConflict, DiagnosticSeverity.Warning, "warning", new SourceLocation("A.cs", 1, 1));
            CompilerDiagnostic error = new(DiagnosticCodes.EmitFailed, DiagnosticSeverity.Error, "error", new SourceLocation("B.cs", 2, 1));
            CompilationUnit failedUnit = SuccessfulUnit() with
            {
                SourceFilePath = "B.cs",
                Diagnostics = ImmutableArray.Create(error),
            };

            CompilationResult result = CompilationResult.FromUnits(
                ImmutableArray.Create(SuccessfulUnit() with { Diagnostics = ImmutableArray.Create(warning) }, failedUnit));

            Assert.False(result.Success);
            Assert.Equal(2, result.Stats.TotalFiles);
            Assert.Equal(1, result.Stats.SuccessfulFiles);
            Assert.Equal(1, result.Stats.FailedFiles);
            Assert.Equal(1, result.Stats.Warnings);
            Assert.Equal(1, result.Stats.Errors);
            Assert.Equal(new[] { DiagnosticCodes.ImportConflict, DiagnosticCodes.EmitFailed }, result.Diagnostics.Select(static diagnostic => diagnostic.Code).ToArray());
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void EventBindings_CU05_AggregatesAllCommands()
        {
            EventBindingsBuilder builder = new();

            EventBindingsIndex index = builder.Build(ImmutableArray.Create(
                CompilerTestFixtures.CreateTodoSchema(),
                CompilerTestFixtures.CreateCounterSchema()));

            Assert.Equal(4, index.Commands.Length);
            Assert.Equal(new[] { "decrement", "increment", "addItem", "removeItem" }, index.Commands.Select(static command => command.CommandName).ToArray());
            Assert.Contains(index.Commands, static command => command.CommandId == 14 && command.ParameterTypes.SequenceEqual(["int"]));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void EventBindings_CU06_AggregatesAllEffects()
        {
            EventBindingsBuilder builder = new();

            EventBindingsIndex index = builder.Build(ImmutableArray.Create(
                CompilerTestFixtures.CreateCounterSchema(),
                CompilerTestFixtures.CreateTodoSchema()));

            EffectBindingEntry effect = Assert.Single(index.Effects);
            Assert.Equal("TodoViewModel", effect.ViewModelClass);
            Assert.Equal("TodoView::__qmlsharp_vm0", effect.CompilerSlotKey);
            Assert.Equal("showToast", effect.EffectName);
            Assert.Equal("string", effect.PayloadType);
        }

        private static CompilationUnit SuccessfulUnit()
        {
            SourceMap sourceMap = SourceMap.Empty("CounterView.cs", "CounterView.qml");
            QmlDocument document = CompilerTestFixtures.CreateCounterAstFixture();

            return new CompilationUnit
            {
                SourceFilePath = "CounterView.cs",
                ViewClassName = "CounterView",
                ViewModelClassName = "CounterViewModel",
                QmlText = "Column {\n}\n",
                Schema = CompilerTestFixtures.CreateCounterSchema(),
                Document = document,
                SourceMap = sourceMap,
                Stats = new CompilationUnitStats
                {
                    ElapsedMilliseconds = 4,
                    QmlBytes = 11,
                },
            };
        }
    }
}
