using QmlSharp.Dsl.Generator.Tests.Fixtures;

namespace QmlSharp.Dsl.Generator.Tests.ViewModels
{
    public sealed class ViewModelIntegrationTests
    {
        public static IEnumerable<object[]> NonObjectSchemaArrayEntryCases()
        {
            yield return
            [
                """{ "className": "BrokenViewModel", "properties": ["bad"] }""",
                "properties[0]",
            ];
            yield return
            [
                """{ "className": "BrokenViewModel", "commands": ["bad"] }""",
                "commands[0]",
            ];
            yield return
            [
                """{ "className": "BrokenViewModel", "effects": ["bad"] }""",
                "effects[0]",
            ];
            yield return
            [
                """
                {
                  "className": "BrokenViewModel",
                  "commands": [
                    { "name": "broken", "parameters": ["bad"] }
                  ]
                }
                """,
                "commands.broken.parameters[0]",
            ];
            yield return
            [
                """
                {
                  "className": "BrokenViewModel",
                  "effects": [
                    { "name": "broken", "parameters": ["bad"] }
                  ]
                }
                """,
                "effects.broken.parameters[0]",
            ];
        }

        [Fact]
        public void AnalyzeSchema_VM01_StateProperties_ReturnsStateMetadata()
        {
            ViewModelIntegration integration = new();

            ViewModelBindingInfo info = integration.AnalyzeSchema(DslTestFixtures.CreateCounterViewModelSchema());

            ViewModelStateInfo state = Assert.Single(info.States);
            Assert.Equal("CounterViewModelProxy", info.ClassName);
            Assert.Equal("Count", state.FieldName);
            Assert.Equal("count", state.QmlPropertyName);
            Assert.Equal("int", state.CSharpType);
            Assert.Equal("int", state.QmlType);
        }

        [Fact]
        public void AnalyzeSchema_VM02_Commands_ReturnsCommandMetadata()
        {
            ViewModelIntegration integration = new();

            ViewModelBindingInfo info = integration.AnalyzeSchema("""
                {
                  "className": "LoginViewModel",
                  "commands": [
                    {
                      "name": "submit",
                      "parameters": [
                        { "name": "userName", "type": "string" },
                        { "name": "remember", "type": "bool" }
                      ]
                    }
                  ]
                }
                """);

            ViewModelCommandInfo command = Assert.Single(info.Commands);
            Assert.Equal("Submit", command.MethodName);
            Assert.Equal("submit", command.QmlMethodName);
            Assert.Equal(["userName", "remember"], command.Parameters.Select(parameter => parameter.Name));
            Assert.Equal(["string", "bool"], command.Parameters.Select(parameter => parameter.CSharpType));
        }

        [Fact]
        public void AnalyzeSchema_VM03_Effects_ReturnsEffectMetadata()
        {
            ViewModelIntegration integration = new();

            ViewModelBindingInfo info = integration.AnalyzeSchema(DslTestFixtures.CreateCounterViewModelSchema());

            ViewModelEffectInfo effect = Assert.Single(info.Effects);
            Assert.Equal("ShowToast", effect.FieldName);
            Assert.Equal("showToast", effect.QmlSignalName);
            GeneratedParameter parameter = Assert.Single(effect.Parameters);
            Assert.Equal("payload", parameter.Name);
            Assert.Equal("string", parameter.CSharpType);
        }

        [Fact]
        public void AnalyzeSchema_VM04_ReadOnlyState_PreservesReadOnlyMetadata()
        {
            ViewModelIntegration integration = new();

            ViewModelBindingInfo info = integration.AnalyzeSchema("""
                {
                  "className": "StatusViewModel",
                  "properties": [
                    { "name": "isReady", "type": "bool", "readOnly": true }
                  ]
                }
                """);

            Assert.True(Assert.Single(info.States).IsReadOnly);
        }

        [Fact]
        public void AnalyzeSchema_VM05_AsyncCommand_PreservesAsyncMetadata()
        {
            ViewModelIntegration integration = new();

            ViewModelBindingInfo info = integration.AnalyzeSchema("""
                {
                  "className": "SaveViewModel",
                  "commands": [
                    { "name": "save", "isAsync": true }
                  ]
                }
                """);

            Assert.True(Assert.Single(info.Commands).IsAsync);
        }

        [Fact]
        public void GenerateProxyType_VM06_ReturnsProxyTypeCodeForStateCommandsAndEffects()
        {
            ViewModelIntegration integration = new();
            ViewModelBindingInfo info = integration.AnalyzeSchema(DslTestFixtures.CreateCounterViewModelSchema());

            string code = integration.GenerateProxyType(info);

            Assert.Contains("public sealed class CounterViewModelProxy", code, StringComparison.Ordinal);
            Assert.Contains("public string Count => BindState(\"count\");", code, StringComparison.Ordinal);
            Assert.Contains("public string Increment() => Command(\"increment\");", code, StringComparison.Ordinal);
            Assert.Contains("public string ShowToast => Effect(\"showToast\");", code, StringComparison.Ordinal);
        }

        [Fact]
        public void GenerateBindingHelpers_VM07_ReturnsSharedBindingHelperCode()
        {
            ViewModelIntegration integration = new();

            string code = integration.GenerateBindingHelpers();

            Assert.Contains("public interface IQmlViewModelProxy", code, StringComparison.Ordinal);
            Assert.Contains("public abstract class QmlViewModelProxyBase", code, StringComparison.Ordinal);
            Assert.Contains("BindState(string propertyName)", code, StringComparison.Ordinal);
            Assert.Contains("Command(string commandName)", code, StringComparison.Ordinal);
        }

        [Fact]
        public void AnalyzeSchema_MissingRequiredField_ThrowsSchemaException()
        {
            ViewModelIntegration integration = new();

            ViewModelSchemaException exception = Assert.Throws<ViewModelSchemaException>(() =>
                integration.AnalyzeSchema("""{ "properties": [] }"""));

            Assert.Equal("className", exception.FieldPath);
        }

        [Fact]
        public void AnalyzeSchema_MalformedJson_ThrowsSchemaException()
        {
            ViewModelIntegration integration = new();

            ViewModelSchemaException exception = Assert.Throws<ViewModelSchemaException>(() =>
                integration.AnalyzeSchema("""{ "className": """));

            Assert.Equal("schema", exception.FieldPath);
        }

        [Fact]
        public void AnalyzeSchema_UnsupportedStateType_ThrowsSchemaException()
        {
            ViewModelIntegration integration = new();

            ViewModelSchemaException exception = Assert.Throws<ViewModelSchemaException>(() =>
                integration.AnalyzeSchema("""
                    {
                      "className": "BrokenViewModel",
                      "properties": [
                        { "name": "broken", "type": "void" }
                      ]
                    }
                    """));

            Assert.Equal("properties.broken.type", exception.FieldPath);
        }

        [Fact]
        public void AnalyzeSchema_MalformedCommandParameters_ThrowsSchemaException()
        {
            ViewModelIntegration integration = new();

            ViewModelSchemaException exception = Assert.Throws<ViewModelSchemaException>(() =>
                integration.AnalyzeSchema("""
                    {
                      "className": "BrokenViewModel",
                      "commands": [
                        { "name": "broken", "parameters": "not-an-array" }
                      ]
                    }
                    """));

            Assert.Equal("commands.broken.parameters", exception.FieldPath);
        }

        [Theory]
        [MemberData(nameof(NonObjectSchemaArrayEntryCases))]
        public void AnalyzeSchema_NonObjectSchemaArrayEntry_ThrowsSchemaException(string schema, string fieldPath)
        {
            ViewModelIntegration integration = new();

            ViewModelSchemaException exception = Assert.Throws<ViewModelSchemaException>(() =>
                integration.AnalyzeSchema(schema));

            Assert.Equal(fieldPath, exception.FieldPath);
        }

        [Fact]
        public void AnalyzeSchema_AsyncKeywordAlias_PreservesAsyncMetadata()
        {
            ViewModelIntegration integration = new();

            ViewModelBindingInfo info = integration.AnalyzeSchema("""
                {
                  "className": "SaveViewModel",
                  "commands": [
                    { "name": "save", "async": true }
                  ]
                }
                """);

            Assert.True(Assert.Single(info.Commands).IsAsync);
        }
    }
}
