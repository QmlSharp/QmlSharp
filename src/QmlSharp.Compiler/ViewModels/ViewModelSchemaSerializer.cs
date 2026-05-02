using System.Globalization;
using System.Text;
using System.Text.Json;

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Serializes ViewModel schemas to the canonical runtime contract JSON shape.
    /// </summary>
    public sealed class ViewModelSchemaSerializer
    {
        private static readonly JsonWriterOptions WriterOptions = new()
        {
            Indented = true,
        };

        /// <summary>
        /// Serializes a schema to stable UTF-8/LF JSON text.
        /// </summary>
        /// <param name="schema">The schema to serialize.</param>
        /// <returns>The canonical JSON text.</returns>
        public string Serialize(ViewModelSchema schema)
        {
            ArgumentNullException.ThrowIfNull(schema);

            using MemoryStream stream = new();
            using (Utf8JsonWriter writer = new(stream, WriterOptions))
            {
                WriteSchema(writer, schema);
            }

            return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
        }

        /// <summary>
        /// Parses a schema JSON document produced by <see cref="Serialize(ViewModelSchema)" />.
        /// </summary>
        /// <param name="json">The schema JSON.</param>
        /// <returns>The parsed schema.</returns>
        public ViewModelSchema Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Schema JSON is required.", nameof(json));
            }

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            string schemaVersion = ReadRequiredString(root, "schemaVersion");
            string className = ReadRequiredString(root, "className");
            string moduleName = ReadRequiredString(root, "moduleName");
            string moduleUri = ReadRequiredString(root, "moduleUri");
            QmlVersion moduleVersion = ReadVersion(root.GetProperty("moduleVersion"));
            string compilerSlotKey = ReadRequiredString(root, "compilerSlotKey");

            return new ViewModelSchema(
                schemaVersion,
                className,
                moduleName,
                moduleUri,
                moduleVersion,
                Version: 2,
                compilerSlotKey,
                ReadProperties(root.GetProperty("properties")),
                ReadCommands(root.GetProperty("commands")),
                ReadEffects(root.GetProperty("effects")),
                ReadLifecycle(root.GetProperty("lifecycle")));
        }

        private static void WriteSchema(Utf8JsonWriter writer, ViewModelSchema schema)
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", schema.SchemaVersion);
            writer.WriteString("className", schema.ClassName);
            writer.WriteString("moduleName", schema.ModuleName);
            writer.WriteString("moduleUri", schema.ModuleUri);
            writer.WritePropertyName("moduleVersion");
            WriteVersion(writer, schema.ModuleVersion);
            writer.WriteString("compilerSlotKey", schema.CompilerSlotKey);
            WriteProperties(writer, schema.Properties);
            WriteCommands(writer, schema.Commands);
            WriteEffects(writer, schema.Effects);
            WriteLifecycle(writer, schema.Lifecycle);
            writer.WriteEndObject();
        }

        private static void WriteVersion(Utf8JsonWriter writer, QmlVersion version)
        {
            writer.WriteStartObject();
            writer.WriteNumber("major", version.Major);
            writer.WriteNumber("minor", version.Minor);
            writer.WriteEndObject();
        }

        private static void WriteProperties(Utf8JsonWriter writer, ImmutableArray<StateEntry> properties)
        {
            writer.WritePropertyName("properties");
            writer.WriteStartArray();
            foreach (StateEntry property in Sort(properties))
            {
                writer.WriteStartObject();
                writer.WriteString("name", property.Name);
                writer.WriteString("type", property.Type);
                if (property.DefaultValue is not null)
                {
                    writer.WriteString("defaultValue", property.DefaultValue);
                }

                writer.WriteBoolean("readOnly", property.ReadOnly);
                writer.WriteNumber("memberId", property.MemberId);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteCommands(Utf8JsonWriter writer, ImmutableArray<CommandEntry> commands)
        {
            writer.WritePropertyName("commands");
            writer.WriteStartArray();
            foreach (CommandEntry command in Sort(commands))
            {
                writer.WriteStartObject();
                writer.WriteString("name", command.Name);
                writer.WritePropertyName("parameters");
                WriteParameters(writer, command.Parameters);
                writer.WriteNumber("commandId", command.CommandId);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteEffects(Utf8JsonWriter writer, ImmutableArray<EffectEntry> effects)
        {
            writer.WritePropertyName("effects");
            writer.WriteStartArray();
            foreach (EffectEntry effect in Sort(effects))
            {
                writer.WriteStartObject();
                writer.WriteString("name", effect.Name);
                writer.WriteString("payloadType", effect.PayloadType);
                writer.WriteNumber("effectId", effect.EffectId);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteParameters(Utf8JsonWriter writer, ImmutableArray<ParameterEntry> parameters)
        {
            writer.WriteStartArray();
            foreach (ParameterEntry parameter in parameters.IsDefault ? ImmutableArray<ParameterEntry>.Empty : parameters)
            {
                writer.WriteStartObject();
                writer.WriteString("name", parameter.Name);
                writer.WriteString("type", parameter.Type);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteLifecycle(Utf8JsonWriter writer, LifecycleInfo lifecycle)
        {
            writer.WritePropertyName("lifecycle");
            writer.WriteStartObject();
            writer.WriteBoolean("onMounted", lifecycle.OnMounted);
            writer.WriteBoolean("onUnmounting", lifecycle.OnUnmounting);
            writer.WriteBoolean("hotReload", lifecycle.HotReload);
            writer.WriteEndObject();
        }

        private static QmlVersion ReadVersion(JsonElement element)
        {
            return new QmlVersion(element.GetProperty("major").GetInt32(), element.GetProperty("minor").GetInt32());
        }

        private static ImmutableArray<StateEntry> ReadProperties(JsonElement element)
        {
            ImmutableArray<StateEntry>.Builder properties = ImmutableArray.CreateBuilder<StateEntry>();
            foreach (JsonElement property in element.EnumerateArray())
            {
                string? defaultValue = property.TryGetProperty("defaultValue", out JsonElement defaultElement)
                    ? defaultElement.GetString()
                    : null;
                properties.Add(new StateEntry(
                    ReadRequiredString(property, "name"),
                    ReadRequiredString(property, "type"),
                    defaultValue,
                    property.GetProperty("readOnly").GetBoolean(),
                    property.GetProperty("memberId").GetInt32()));
            }

            return properties.ToImmutable();
        }

        private static ImmutableArray<CommandEntry> ReadCommands(JsonElement element)
        {
            ImmutableArray<CommandEntry>.Builder commands = ImmutableArray.CreateBuilder<CommandEntry>();
            foreach (JsonElement command in element.EnumerateArray())
            {
                commands.Add(new CommandEntry(
                    ReadRequiredString(command, "name"),
                    ReadParameters(command.GetProperty("parameters")),
                    command.GetProperty("commandId").GetInt32()));
            }

            return commands.ToImmutable();
        }

        private static ImmutableArray<EffectEntry> ReadEffects(JsonElement element)
        {
            ImmutableArray<EffectEntry>.Builder effects = ImmutableArray.CreateBuilder<EffectEntry>();
            foreach (JsonElement effect in element.EnumerateArray())
            {
                string payloadType = ReadRequiredString(effect, "payloadType");
                ImmutableArray<ParameterEntry> parameters = StringComparer.Ordinal.Equals(payloadType, "void")
                    ? ImmutableArray<ParameterEntry>.Empty
                    : ImmutableArray.Create(new ParameterEntry("payload", payloadType));
                effects.Add(new EffectEntry(
                    ReadRequiredString(effect, "name"),
                    payloadType,
                    effect.GetProperty("effectId").GetInt32(),
                    parameters));
            }

            return effects.ToImmutable();
        }

        private static ImmutableArray<ParameterEntry> ReadParameters(JsonElement element)
        {
            ImmutableArray<ParameterEntry>.Builder parameters = ImmutableArray.CreateBuilder<ParameterEntry>();
            foreach (JsonElement parameter in element.EnumerateArray())
            {
                parameters.Add(new ParameterEntry(ReadRequiredString(parameter, "name"), ReadRequiredString(parameter, "type")));
            }

            return parameters.ToImmutable();
        }

        private static LifecycleInfo ReadLifecycle(JsonElement element)
        {
            return new LifecycleInfo(
                element.GetProperty("onMounted").GetBoolean(),
                element.GetProperty("onUnmounting").GetBoolean(),
                element.GetProperty("hotReload").GetBoolean());
        }

        private static string ReadRequiredString(JsonElement element, string propertyName)
        {
            string? value = element.GetProperty(propertyName).GetString();
            if (value is null)
            {
                throw new JsonException(string.Format(CultureInfo.InvariantCulture, "Property '{0}' must be a string.", propertyName));
            }

            return value;
        }

        private static IEnumerable<StateEntry> Sort(ImmutableArray<StateEntry> values)
        {
            return (values.IsDefault ? ImmutableArray<StateEntry>.Empty : values).OrderBy(static value => value.Name, StringComparer.Ordinal);
        }

        private static IEnumerable<CommandEntry> Sort(ImmutableArray<CommandEntry> values)
        {
            return (values.IsDefault ? ImmutableArray<CommandEntry>.Empty : values).OrderBy(static value => value.Name, StringComparer.Ordinal);
        }

        private static IEnumerable<EffectEntry> Sort(ImmutableArray<EffectEntry> values)
        {
            return (values.IsDefault ? ImmutableArray<EffectEntry>.Empty : values).OrderBy(static value => value.Name, StringComparer.Ordinal);
        }
    }
}
