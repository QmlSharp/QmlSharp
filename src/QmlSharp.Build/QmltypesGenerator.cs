using System.Globalization;
using System.Text;
using QmlSharp.Compiler;

namespace QmlSharp.Build
{
    /// <summary>Generates deterministic Qt tooling metadata from module schemas.</summary>
    public sealed class QmltypesGenerator : IQmltypesGenerator
    {
        /// <inheritdoc />
        public string Generate(
            string moduleUri,
            QmlVersion version,
            ImmutableArray<ViewModelSchema> schemas)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleUri);
            ArgumentNullException.ThrowIfNull(version);

            StringBuilder builder = new();
            builder.Append("import QtQuick.tooling 1.2\n\n");
            builder.Append("Module {\n");
            foreach (ViewModelSchema schema in SortSchemas(schemas))
            {
                AppendComponent(builder, moduleUri, version, schema);
            }

            builder.Append("}\n");
            return builder.ToString();
        }

        private static void AppendComponent(
            StringBuilder builder,
            string moduleUri,
            QmlVersion version,
            ViewModelSchema schema)
        {
            string versionText = string.Create(
                CultureInfo.InvariantCulture,
                $"{version.Major}.{version.Minor}");

            builder.Append("    Component {\n");
            AppendQuotedProperty(builder, "name", schema.ClassName, 8);
            AppendQuotedProperty(builder, "prototype", "QObject", 8);
            builder.Append("        exports: [\"");
            builder.Append(Escape($"{moduleUri}/{schema.ClassName} {versionText}"));
            builder.Append("\"]\n");
            builder.Append("        exportMetaObjectRevisions: [0]\n");

            foreach (StateEntry property in SortProperties(schema.Properties))
            {
                AppendProperty(builder, property);
            }

            foreach (CommandEntry command in SortCommands(schema.Commands))
            {
                AppendMethod(builder, command);
            }

            if (!schema.Effects.IsDefaultOrEmpty)
            {
                AppendEffectSignal(builder);
            }

            builder.Append("    }\n");
        }

        private static void AppendProperty(StringBuilder builder, StateEntry property)
        {
            builder.Append("        Property { name: \"");
            builder.Append(Escape(property.Name));
            builder.Append("\"; type: \"");
            builder.Append(MapQmlToolingType(property.Type));
            builder.Append('"');
            if (property.ReadOnly)
            {
                builder.Append("; isReadonly: true");
            }

            builder.Append(" }\n");
        }

        private static void AppendMethod(StringBuilder builder, CommandEntry command)
        {
            ImmutableArray<ParameterEntry> parameters = command.Parameters.IsDefault
                ? ImmutableArray<ParameterEntry>.Empty
                : command.Parameters;
            if (parameters.IsEmpty)
            {
                builder.Append("        Method { name: \"");
                builder.Append(Escape(command.Name));
                builder.Append("\" }\n");
                return;
            }

            builder.Append("        Method {\n");
            AppendQuotedProperty(builder, "name", command.Name, 12);
            foreach (ParameterEntry parameter in parameters)
            {
                AppendParameter(builder, parameter, 12);
            }

            builder.Append("        }\n");
        }

        private static void AppendEffectSignal(StringBuilder builder)
        {
            builder.Append("        Signal {\n");
            AppendQuotedProperty(builder, "name", "effectDispatched", 12);
            AppendParameter(builder, new ParameterEntry("effectName", "string"), 12);
            AppendParameter(builder, new ParameterEntry("payloadJson", "string"), 12);
            builder.Append("        }\n");
        }

        private static void AppendParameter(StringBuilder builder, ParameterEntry parameter, int spaces)
        {
            builder.Append(' ', spaces);
            builder.Append("Parameter { name: \"");
            builder.Append(Escape(parameter.Name));
            builder.Append("\"; type: \"");
            builder.Append(MapQmlToolingType(parameter.Type));
            builder.Append("\" }\n");
        }

        private static void AppendQuotedProperty(StringBuilder builder, string name, string value, int spaces)
        {
            builder.Append(' ', spaces);
            builder.Append(name);
            builder.Append(": \"");
            builder.Append(Escape(value));
            builder.Append("\"\n");
        }

        private static string MapQmlToolingType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                return "var";
            }

            string normalized = type.Trim();
            if (normalized.StartsWith("list<", StringComparison.Ordinal) ||
                normalized.StartsWith("List<", StringComparison.Ordinal))
            {
                return "var";
            }

            return normalized switch
            {
                "bool" or "boolean" or "Boolean" => "bool",
                "int" or "Int32" or "long" or "Int64" => "int",
                "double" or "float" or "real" or "number" or "Single" or "Double" => "real",
                "string" or "String" => "string",
                _ => "var",
            };
        }

        private static string Escape(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        private static IEnumerable<ViewModelSchema> SortSchemas(ImmutableArray<ViewModelSchema> schemas)
        {
            return (schemas.IsDefault ? ImmutableArray<ViewModelSchema>.Empty : schemas)
                .OrderBy(static schema => schema.ClassName, StringComparer.Ordinal)
                .ThenBy(static schema => schema.CompilerSlotKey, StringComparer.Ordinal);
        }

        private static IEnumerable<StateEntry> SortProperties(ImmutableArray<StateEntry> properties)
        {
            return (properties.IsDefault ? ImmutableArray<StateEntry>.Empty : properties)
                .OrderBy(static property => property.Name, StringComparer.Ordinal);
        }

        private static IEnumerable<CommandEntry> SortCommands(ImmutableArray<CommandEntry> commands)
        {
            return (commands.IsDefault ? ImmutableArray<CommandEntry>.Empty : commands)
                .OrderBy(static command => command.Name, StringComparer.Ordinal);
        }
    }
}
