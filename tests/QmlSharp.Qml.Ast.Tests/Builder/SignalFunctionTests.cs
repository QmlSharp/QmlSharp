using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Tests.Helpers;

namespace QmlSharp.Qml.Ast.Tests.Builder
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class SignalFunctionTests
    {
        [Fact]
        public void BS_01_Declare_parameterless_signal()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .SignalDeclaration("clicked")
                .Build();

            _ = Assert.Single(obj.Members);
            SignalDeclarationNode signal = Assert.IsType<SignalDeclarationNode>(obj.Members[0]);
            Assert.Equal("clicked", signal.Name);
            Assert.Empty(signal.Parameters);
        }

        [Fact]
        public void BS_02_Declare_signal_with_typed_parameters()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .SignalDeclaration("positionChanged",
                    new ParameterDeclaration("x", "int"),
                    new ParameterDeclaration("y", "int"))
                .Build();

            SignalDeclarationNode signal = Assert.IsType<SignalDeclarationNode>(obj.Members[0]);
            Assert.Equal(2, signal.Parameters.Length);
            Assert.Equal("x", signal.Parameters[0].Name);
            Assert.Equal("int", signal.Parameters[0].TypeName);
            Assert.Equal("y", signal.Parameters[1].Name);
            Assert.Equal("int", signal.Parameters[1].TypeName);
        }

        [Fact]
        public void BS_03_Add_expression_signal_handler()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .SignalHandler("onClicked", SignalHandlerForm.Expression, "console.log(\"clicked\")")
                .Build();

            SignalHandlerNode handler = Assert.IsType<SignalHandlerNode>(obj.Members[0]);
            Assert.Equal("onClicked", handler.HandlerName);
            Assert.Equal(SignalHandlerForm.Expression, handler.Form);
            Assert.Equal("console.log(\"clicked\")", handler.Code);
        }

        [Fact]
        public void BS_04_Add_block_signal_handler()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .SignalHandler("onClicked", SignalHandlerForm.Block, "{ console.log(\"a\"); console.log(\"b\"); }")
                .Build();

            SignalHandlerNode handler = Assert.IsType<SignalHandlerNode>(obj.Members[0]);
            Assert.Equal(SignalHandlerForm.Block, handler.Form);
            Assert.Equal("{ console.log(\"a\"); console.log(\"b\"); }", handler.Code);
        }

        [Fact]
        public void BS_05_Add_arrow_signal_handler_with_parameters()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .SignalHandler("onPositionChanged", SignalHandlerForm.Arrow, "{ console.log(x, y); }", ["x", "y"])
                .Build();

            SignalHandlerNode handler = Assert.IsType<SignalHandlerNode>(obj.Members[0]);
            Assert.Equal(SignalHandlerForm.Arrow, handler.Form);
            _ = Assert.NotNull(handler.Parameters);
            Assert.Equal(2, handler.Parameters.Value.Length);
            Assert.Equal("x", handler.Parameters.Value[0]);
            Assert.Equal("y", handler.Parameters.Value[1]);
        }

        [Fact]
        public void BS_06_Declare_function_with_parameters_and_return_type()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .FunctionDeclaration("compute", "{ return x + y; }", "int",
                    new ParameterDeclaration("x", "int"),
                    new ParameterDeclaration("y", "int"))
                .Build();

            FunctionDeclarationNode func = Assert.IsType<FunctionDeclarationNode>(obj.Members[0]);
            Assert.Equal("compute", func.Name);
            Assert.Equal("{ return x + y; }", func.Body);
            Assert.Equal("int", func.ReturnType);
            Assert.Equal(2, func.Parameters.Length);
        }

        [Fact]
        public void BS_07_Declare_function_without_return_type()
        {
            ObjectDefinitionNode obj = new QmlObjectBuilder("Item")
                .FunctionDeclaration("doSomething", "{ console.log(\"done\"); }")
                .Build();

            FunctionDeclarationNode func = Assert.IsType<FunctionDeclarationNode>(obj.Members[0]);
            Assert.Null(func.ReturnType);
            Assert.Empty(func.Parameters);
        }
    }
}
