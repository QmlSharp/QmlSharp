using System.Text;
using System.Text.Json;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>
    /// Extracts generator-side ViewModel binding metadata from schema JSON fixtures.
    /// </summary>
    public sealed class ViewModelIntegration : IViewModelIntegration
    {
        private readonly ITypeMapper typeMapper;

        public ViewModelIntegration()
            : this(new TypeMapper())
        {
        }

        public ViewModelIntegration(ITypeMapper typeMapper)
        {
            this.typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        }

        public ViewModelBindingInfo AnalyzeSchema(string schemaJson)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(schemaJson);

            using JsonDocument document = ParseSchema(schemaJson);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ViewModelSchemaException("ViewModel schema root must be a JSON object.", "schema");
            }

            string viewModelClassName = ReadRequiredString(root, "className");
            string proxyClassName = $"{viewModelClassName}Proxy";

            return new ViewModelBindingInfo(
                ClassName: proxyClassName,
                States: ReadStates(root),
                Commands: ReadCommands(root),
                Effects: ReadEffects(root));
        }

        public string GenerateProxyType(ViewModelBindingInfo info)
        {
            ArgumentNullException.ThrowIfNull(info);

            StringBuilder builder = new();
            builder.AppendLine($"public sealed class {info.ClassName} : QmlViewModelProxyBase");
            builder.AppendLine("{");

            foreach (ViewModelStateInfo state in info.States.OrderBy(state => state.FieldName, StringComparer.Ordinal))
            {
                builder.AppendLine($"    public string {state.FieldName} => BindState(\"{state.QmlPropertyName}\");");
            }

            foreach (ViewModelCommandInfo command in info.Commands.OrderBy(command => command.MethodName, StringComparer.Ordinal))
            {
                builder.AppendLine($"    public string {command.MethodName}() => Command(\"{command.QmlMethodName}\");");
            }

            foreach (ViewModelEffectInfo effect in info.Effects.OrderBy(effect => effect.FieldName, StringComparer.Ordinal))
            {
                builder.AppendLine($"    public string {effect.FieldName} => Effect(\"{effect.QmlSignalName}\");");
            }

            builder.AppendLine("}");
            return builder.ToString();
        }

        public string GenerateBindingHelpers()
        {
            return """
                public interface IQmlViewModelProxy
                {
                    string BindState(string propertyName);
                    string Command(string commandName);
                    string Effect(string effectName);
                }

                public abstract class QmlViewModelProxyBase : IQmlViewModelProxy
                {
                    public string BindState(string propertyName) => $"__qmlsharp_vm0.{propertyName}";

                    public string Command(string commandName) => $"__qmlsharp_vm0.{commandName}";

                    public string Effect(string effectName) => $"effectDispatched:{effectName}";
                }

                """;
        }

        private ImmutableArray<ViewModelStateInfo> ReadStates(JsonElement root)
        {
            JsonElement properties = ReadOptionalArray(root, "properties");
            ImmutableArray<ViewModelStateInfo>.Builder states = ImmutableArray.CreateBuilder<ViewModelStateInfo>();

            int propertyIndex = 0;
            foreach (JsonElement property in properties.EnumerateArray())
            {
                string propertyPath = $"properties[{propertyIndex}]";
                string qmlName = ReadRequiredString(property, "name", propertyPath);
                string qmlType = ReadRequiredString(property, "type", propertyPath);
                string csharpType = MapSchemaType(qmlType, $"properties.{qmlName}.type");
                bool isReadOnly = ReadOptionalBoolean(property, "readOnly", propertyPath);

                states.Add(new ViewModelStateInfo(
                    FieldName: MemberNameUtilities.ToPascalCase(qmlName),
                    QmlPropertyName: qmlName,
                    CSharpType: csharpType,
                    QmlType: qmlType,
                    IsReadOnly: isReadOnly));
                propertyIndex++;
            }

            return states
                .OrderBy(state => state.FieldName, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private ImmutableArray<ViewModelCommandInfo> ReadCommands(JsonElement root)
        {
            JsonElement commands = ReadOptionalArray(root, "commands");
            ImmutableArray<ViewModelCommandInfo>.Builder commandInfos = ImmutableArray.CreateBuilder<ViewModelCommandInfo>();

            int commandIndex = 0;
            foreach (JsonElement command in commands.EnumerateArray())
            {
                string commandPath = $"commands[{commandIndex}]";
                string qmlName = ReadRequiredString(command, "name", commandPath);
                commandInfos.Add(new ViewModelCommandInfo(
                    MethodName: MemberNameUtilities.ToPascalCase(qmlName),
                    QmlMethodName: qmlName,
                    Parameters: ReadParameters(command, $"commands.{qmlName}.parameters", commandPath),
                    IsAsync: ReadOptionalBoolean(command, "isAsync", commandPath) || ReadOptionalBoolean(command, "async", commandPath)));
                commandIndex++;
            }

            return commandInfos
                .OrderBy(command => command.MethodName, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private ImmutableArray<ViewModelEffectInfo> ReadEffects(JsonElement root)
        {
            JsonElement effects = ReadOptionalArray(root, "effects");
            ImmutableArray<ViewModelEffectInfo>.Builder effectInfos = ImmutableArray.CreateBuilder<ViewModelEffectInfo>();

            int effectIndex = 0;
            foreach (JsonElement effect in effects.EnumerateArray())
            {
                string effectPath = $"effects[{effectIndex}]";
                string qmlName = ReadRequiredString(effect, "name", effectPath);
                ImmutableArray<GeneratedParameter> parameters = ReadEffectParameters(effect, qmlName, effectPath);
                effectInfos.Add(new ViewModelEffectInfo(
                    FieldName: MemberNameUtilities.ToPascalCase(qmlName),
                    QmlSignalName: qmlName,
                    Parameters: parameters));
                effectIndex++;
            }

            return effectInfos
                .OrderBy(effect => effect.FieldName, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private ImmutableArray<GeneratedParameter> ReadEffectParameters(JsonElement effect, string effectName, string effectPath)
        {
            ThrowIfNotObject(effect, effectPath);

            if (effect.TryGetProperty("parameters", out JsonElement parametersElement))
            {
                if (parametersElement.ValueKind != JsonValueKind.Array)
                {
                    throw new ViewModelSchemaException("Schema field 'parameters' must be an array.", $"effects.{effectName}.parameters");
                }

                return ReadParameterArray(parametersElement, $"effects.{effectName}.parameters");
            }

            if (!effect.TryGetProperty("payloadType", out JsonElement payloadTypeElement)
                || payloadTypeElement.ValueKind == JsonValueKind.Null)
            {
                return ImmutableArray<GeneratedParameter>.Empty;
            }

            if (payloadTypeElement.ValueKind != JsonValueKind.String)
            {
                throw new ViewModelSchemaException("Schema field 'payloadType' must be a string.", $"effects.{effectName}.payloadType");
            }

            string? payloadType = payloadTypeElement.GetString();
            if (string.IsNullOrWhiteSpace(payloadType)
                || string.Equals(payloadType, "void", StringComparison.Ordinal))
            {
                return ImmutableArray<GeneratedParameter>.Empty;
            }

            return
            [
                new GeneratedParameter(
                    Name: "payload",
                    CSharpType: MapSchemaType(payloadType, $"effects.{effectName}.payloadType"),
                    QmlType: payloadType),
            ];
        }

        private ImmutableArray<GeneratedParameter> ReadParameters(JsonElement owner, string fieldPath, string ownerPath)
        {
            ThrowIfNotObject(owner, ownerPath);

            if (!owner.TryGetProperty("parameters", out JsonElement parametersElement))
            {
                return ImmutableArray<GeneratedParameter>.Empty;
            }

            if (parametersElement.ValueKind != JsonValueKind.Array)
            {
                throw new ViewModelSchemaException("Schema field 'parameters' must be an array.", fieldPath);
            }

            return ReadParameterArray(parametersElement, fieldPath);
        }

        private ImmutableArray<GeneratedParameter> ReadParameterArray(JsonElement parametersElement, string fieldPath)
        {
            ImmutableArray<GeneratedParameter>.Builder parameters = ImmutableArray.CreateBuilder<GeneratedParameter>();
            int parameterIndex = 0;
            foreach (JsonElement parameter in parametersElement.EnumerateArray())
            {
                string parameterPath = $"{fieldPath}[{parameterIndex}]";
                string parameterName = ReadRequiredString(parameter, "name", parameterPath);
                string qmlType = ReadRequiredString(parameter, "type", parameterPath);
                parameters.Add(new GeneratedParameter(
                    Name: parameterName,
                    CSharpType: MapSchemaType(qmlType, $"{fieldPath}[{parameterIndex}].type"),
                    QmlType: qmlType));
                parameterIndex++;
            }

            return parameters.ToImmutable();
        }

        private string MapSchemaType(string qmlType, string fieldPath)
        {
            if (string.Equals(qmlType, "void", StringComparison.Ordinal))
            {
                throw new ViewModelSchemaException("Schema type 'void' is not supported for ViewModel metadata.", fieldPath);
            }

            return typeMapper.MapToCSharp(qmlType);
        }

        private static JsonDocument ParseSchema(string schemaJson)
        {
            try
            {
                return JsonDocument.Parse(schemaJson);
            }
            catch (JsonException exception)
            {
                throw new ViewModelSchemaException("ViewModel schema JSON is malformed.", "schema", exception);
            }
        }

        private static string ReadRequiredString(JsonElement owner, string propertyName, string ownerPath = "")
        {
            ThrowIfNotObject(owner, ownerPath);

            string fieldPath = CombineFieldPath(ownerPath, propertyName);
            if (!owner.TryGetProperty(propertyName, out JsonElement property))
            {
                throw new ViewModelSchemaException($"Schema field '{propertyName}' is required.", fieldPath);
            }

            if (property.ValueKind != JsonValueKind.String)
            {
                throw new ViewModelSchemaException($"Schema field '{propertyName}' must be a string.", fieldPath);
            }

            string? value = property.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ViewModelSchemaException($"Schema field '{propertyName}' must not be empty.", fieldPath);
            }

            return value;
        }

        private static JsonElement ReadOptionalArray(JsonElement owner, string propertyName, string ownerPath = "")
        {
            ThrowIfNotObject(owner, ownerPath);

            string fieldPath = CombineFieldPath(ownerPath, propertyName);
            if (!owner.TryGetProperty(propertyName, out JsonElement property))
            {
                return EmptyArrayElement.Value;
            }

            if (property.ValueKind != JsonValueKind.Array)
            {
                throw new ViewModelSchemaException($"Schema field '{propertyName}' must be an array.", fieldPath);
            }

            return property;
        }

        private static bool ReadOptionalBoolean(JsonElement owner, string propertyName, string ownerPath = "")
        {
            ThrowIfNotObject(owner, ownerPath);

            string fieldPath = CombineFieldPath(ownerPath, propertyName);
            if (!owner.TryGetProperty(propertyName, out JsonElement property))
            {
                return false;
            }

            if (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False)
            {
                throw new ViewModelSchemaException($"Schema field '{propertyName}' must be a boolean.", fieldPath);
            }

            return property.GetBoolean();
        }

        private static void ThrowIfNotObject(JsonElement owner, string fieldPath)
        {
            if (owner.ValueKind != JsonValueKind.Object)
            {
                throw new ViewModelSchemaException("Schema field must be a JSON object.", string.IsNullOrWhiteSpace(fieldPath) ? "schema" : fieldPath);
            }
        }

        private static string CombineFieldPath(string ownerPath, string propertyName)
        {
            return string.IsNullOrWhiteSpace(ownerPath) ? propertyName : $"{ownerPath}.{propertyName}";
        }

        private static class EmptyArrayElement
        {
            private static readonly JsonDocument Document = JsonDocument.Parse("[]");

            public static JsonElement Value => Document.RootElement;
        }
    }
}
