#pragma warning disable MA0048

using System.Text;
using System.Text.Json;

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Per-project event binding index for validation, diagnostics, and tooling.
    /// </summary>
    public sealed record EventBindingsIndex(
        string SchemaVersion,
        ImmutableArray<CommandBindingEntry> Commands,
        ImmutableArray<EffectBindingEntry> Effects)
    {
        /// <summary>Gets an empty event binding index.</summary>
        public static EventBindingsIndex Empty { get; } =
            new("1.0", ImmutableArray<CommandBindingEntry>.Empty, ImmutableArray<EffectBindingEntry>.Empty);
    }

    /// <summary>A command binding entry in <c>event-bindings.json</c>.</summary>
    public sealed record CommandBindingEntry(
        string ViewModelClass,
        string CompilerSlotKey,
        string CommandName,
        int CommandId,
        ImmutableArray<string> ParameterTypes);

    /// <summary>An effect binding entry in <c>event-bindings.json</c>.</summary>
    public sealed record EffectBindingEntry(
        string ViewModelClass,
        string CompilerSlotKey,
        string EffectName,
        int EffectId,
        string PayloadType);

    /// <summary>Builds event binding indexes from schemas.</summary>
    public interface IEventBindingsBuilder
    {
        /// <summary>Builds one event binding index from schemas.</summary>
        EventBindingsIndex Build(ImmutableArray<ViewModelSchema> schemas);

        /// <summary>Serializes an event binding index to the canonical manifest JSON shape.</summary>
        string Serialize(EventBindingsIndex index);

        /// <summary>Deserializes an event binding manifest JSON document.</summary>
        EventBindingsIndex Deserialize(string json);
    }

    /// <summary>
    /// Builds and serializes deterministic <c>event-bindings.json</c> manifests.
    /// </summary>
    public sealed class EventBindingsBuilder : IEventBindingsBuilder
    {
        private const string CurrentSchemaVersion = "1.0";

        private static readonly JsonWriterOptions WriterOptions = new()
        {
            Indented = true,
        };

        /// <inheritdoc />
        public EventBindingsIndex Build(ImmutableArray<ViewModelSchema> schemas)
        {
            ImmutableArray<ViewModelSchema> normalizedSchemas = schemas.IsDefault
                ? ImmutableArray<ViewModelSchema>.Empty
                : schemas;

            ImmutableArray<CommandBindingEntry> commands = normalizedSchemas
                .SelectMany(static schema => (schema.Commands.IsDefault ? ImmutableArray<CommandEntry>.Empty : schema.Commands)
                    .Select(command => new CommandBindingEntry(
                        schema.ClassName,
                        schema.CompilerSlotKey,
                        command.Name,
                        command.CommandId,
                        (command.Parameters.IsDefault ? ImmutableArray<ParameterEntry>.Empty : command.Parameters)
                            .Select(static parameter => parameter.Type)
                            .ToImmutableArray())))
                .OrderBy(static entry => entry.ViewModelClass, StringComparer.Ordinal)
                .ThenBy(static entry => entry.CommandName, StringComparer.Ordinal)
                .ThenBy(static entry => entry.CommandId)
                .ToImmutableArray();

            ImmutableArray<EffectBindingEntry> effects = normalizedSchemas
                .SelectMany(static schema => (schema.Effects.IsDefault ? ImmutableArray<EffectEntry>.Empty : schema.Effects)
                    .Select(effect => new EffectBindingEntry(
                        schema.ClassName,
                        schema.CompilerSlotKey,
                        effect.Name,
                        effect.EffectId,
                        effect.PayloadType)))
                .OrderBy(static entry => entry.ViewModelClass, StringComparer.Ordinal)
                .ThenBy(static entry => entry.EffectName, StringComparer.Ordinal)
                .ThenBy(static entry => entry.EffectId)
                .ToImmutableArray();

            return new EventBindingsIndex(CurrentSchemaVersion, commands, effects);
        }

        /// <inheritdoc />
        public string Serialize(EventBindingsIndex index)
        {
            ArgumentNullException.ThrowIfNull(index);

            using MemoryStream stream = new();
            using (Utf8JsonWriter writer = new(stream, WriterOptions))
            {
                writer.WriteStartObject();
                writer.WriteString("schemaVersion", index.SchemaVersion);
                WriteCommands(writer, index.Commands);
                WriteEffects(writer, index.Effects);
                writer.WriteEndObject();
            }

            return NormalizeJsonText(Encoding.UTF8.GetString(stream.ToArray()));
        }

        /// <inheritdoc />
        public EventBindingsIndex Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Event bindings JSON is required.", nameof(json));
            }

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string schemaVersion = ReadRequiredString(root, "schemaVersion");

            return new EventBindingsIndex(
                schemaVersion,
                ReadCommands(root.GetProperty("commands")),
                ReadEffects(root.GetProperty("effects")));
        }

        private static void WriteCommands(Utf8JsonWriter writer, ImmutableArray<CommandBindingEntry> commands)
        {
            writer.WritePropertyName("commands");
            writer.WriteStartArray();
            foreach (CommandBindingEntry command in Sort(commands))
            {
                writer.WriteStartObject();
                writer.WriteString("viewModelClass", command.ViewModelClass);
                writer.WriteString("commandName", command.CommandName);
                writer.WriteNumber("commandId", command.CommandId);
                writer.WritePropertyName("parameterTypes");
                writer.WriteStartArray();
                foreach (string parameterType in command.ParameterTypes.IsDefault ? ImmutableArray<string>.Empty : command.ParameterTypes)
                {
                    writer.WriteStringValue(parameterType);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteEffects(Utf8JsonWriter writer, ImmutableArray<EffectBindingEntry> effects)
        {
            writer.WritePropertyName("effects");
            writer.WriteStartArray();
            foreach (EffectBindingEntry effect in Sort(effects))
            {
                writer.WriteStartObject();
                writer.WriteString("viewModelClass", effect.ViewModelClass);
                writer.WriteString("effectName", effect.EffectName);
                writer.WriteNumber("effectId", effect.EffectId);
                writer.WriteString("payloadType", effect.PayloadType);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static ImmutableArray<CommandBindingEntry> ReadCommands(JsonElement element)
        {
            ImmutableArray<CommandBindingEntry>.Builder commands = ImmutableArray.CreateBuilder<CommandBindingEntry>();
            foreach (JsonElement command in element.EnumerateArray())
            {
                commands.Add(new CommandBindingEntry(
                    ReadRequiredString(command, "viewModelClass"),
                    string.Empty,
                    ReadRequiredString(command, "commandName"),
                    command.GetProperty("commandId").GetInt32(),
                    ReadParameterTypes(command.GetProperty("parameterTypes"))));
            }

            return commands.ToImmutable();
        }

        private static ImmutableArray<EffectBindingEntry> ReadEffects(JsonElement element)
        {
            ImmutableArray<EffectBindingEntry>.Builder effects = ImmutableArray.CreateBuilder<EffectBindingEntry>();
            foreach (JsonElement effect in element.EnumerateArray())
            {
                effects.Add(new EffectBindingEntry(
                    ReadRequiredString(effect, "viewModelClass"),
                    string.Empty,
                    ReadRequiredString(effect, "effectName"),
                    effect.GetProperty("effectId").GetInt32(),
                    ReadRequiredString(effect, "payloadType")));
            }

            return effects.ToImmutable();
        }

        private static ImmutableArray<string> ReadParameterTypes(JsonElement element)
        {
            return element.EnumerateArray()
                .Select(static parameterType =>
                {
                    string? value = parameterType.GetString();
                    if (value is null)
                    {
                        throw new JsonException("Parameter type entries must be strings.");
                    }

                    return value;
                })
                .ToImmutableArray();
        }

        private static string ReadRequiredString(JsonElement element, string propertyName)
        {
            string? value = element.GetProperty(propertyName).GetString();
            if (value is null)
            {
                throw new JsonException($"Property '{propertyName}' must be a string.");
            }

            return value;
        }

        private static string NormalizeJsonText(string json)
        {
            return json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        }

        private static IEnumerable<CommandBindingEntry> Sort(ImmutableArray<CommandBindingEntry> commands)
        {
            return (commands.IsDefault ? ImmutableArray<CommandBindingEntry>.Empty : commands)
                .OrderBy(static entry => entry.ViewModelClass, StringComparer.Ordinal)
                .ThenBy(static entry => entry.CommandName, StringComparer.Ordinal)
                .ThenBy(static entry => entry.CommandId);
        }

        private static IEnumerable<EffectBindingEntry> Sort(ImmutableArray<EffectBindingEntry> effects)
        {
            return (effects.IsDefault ? ImmutableArray<EffectBindingEntry>.Empty : effects)
                .OrderBy(static entry => entry.ViewModelClass, StringComparer.Ordinal)
                .ThenBy(static entry => entry.EffectName, StringComparer.Ordinal)
                .ThenBy(static entry => entry.EffectId);
        }
    }
}

#pragma warning restore MA0048
