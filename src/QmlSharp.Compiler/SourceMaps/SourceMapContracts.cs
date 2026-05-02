using System.Text;
using System.Text.Json;

#pragma warning disable MA0048

namespace QmlSharp.Compiler
{
    /// <summary>Creates, serializes, and queries compiler source maps.</summary>
    public interface ISourceMapManager
    {
        /// <summary>Creates a builder for one generated QML output file.</summary>
        ISourceMapBuilder CreateBuilder(string sourceFilePath, string outputFilePath);

        /// <summary>Serializes a source map to JSON.</summary>
        string Serialize(SourceMap sourceMap);

        /// <summary>Deserializes a source map from JSON.</summary>
        SourceMap Deserialize(string json);

        /// <summary>Finds the original source location for a QML output position.</summary>
        SourceLocation? FindSourceLocation(SourceMap sourceMap, string outputFilePath, int outputLine, int outputColumn);

        /// <summary>Finds a QML output location for an original source position.</summary>
        QmlLocation? FindQmlLocation(SourceMap sourceMap, string sourceFilePath, int sourceLine, int sourceColumn);
    }

    /// <summary>Builds a source map incrementally.</summary>
    public interface ISourceMapBuilder
    {
        /// <summary>Adds one mapping.</summary>
        void AddMapping(SourceMapMapping mapping);

        /// <summary>Builds the immutable source map.</summary>
        SourceMap Build();
    }

    /// <summary>Public source-map contract with 1-based line and column positions.</summary>
    public sealed record SourceMap(
        string SchemaVersion,
        string SourceFilePath,
        string OutputFilePath,
        ImmutableArray<SourceMapMapping> Mappings)
    {
        /// <summary>Gets an empty source map.</summary>
        public static SourceMap Empty(string sourceFilePath, string outputFilePath)
        {
            return new SourceMap("1.0", sourceFilePath, outputFilePath, ImmutableArray<SourceMapMapping>.Empty);
        }
    }

    /// <summary>A 1-based generated QML output location.</summary>
    public sealed record QmlLocation
    {
        /// <summary>Initializes a new QML location.</summary>
        public QmlLocation(string filePath, int line, int column)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            SourceMapValidation.ValidatePositive(line, nameof(line));
            SourceMapValidation.ValidatePositive(column, nameof(column));

            FilePath = filePath;
            Line = line;
            Column = column;
        }

        /// <summary>Gets the generated QML file path.</summary>
        public string FilePath { get; init; }

        /// <summary>Gets the 1-based generated QML line.</summary>
        public int Line { get; init; }

        /// <summary>Gets the 1-based generated QML column.</summary>
        public int Column { get; init; }
    }

    /// <summary>One source-map mapping entry.</summary>
    public sealed record SourceMapMapping
    {
        /// <summary>Initializes a new source map mapping.</summary>
        public SourceMapMapping(
            int outputLine,
            int outputColumn,
            string sourceFilePath,
            int sourceLine,
            int sourceColumn,
            string? symbol = null,
            string? nodeKind = null)
        {
            SourceMapValidation.ValidatePositive(outputLine, nameof(outputLine));
            SourceMapValidation.ValidatePositive(outputColumn, nameof(outputColumn));
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
            SourceMapValidation.ValidatePositive(sourceLine, nameof(sourceLine));
            SourceMapValidation.ValidatePositive(sourceColumn, nameof(sourceColumn));

            OutputLine = outputLine;
            OutputColumn = outputColumn;
            SourceFilePath = sourceFilePath;
            SourceLine = sourceLine;
            SourceColumn = sourceColumn;
            Symbol = symbol;
            NodeKind = nodeKind;
        }

        /// <summary>Gets the 1-based generated QML line.</summary>
        public int OutputLine { get; init; }

        /// <summary>Gets the 1-based generated QML column.</summary>
        public int OutputColumn { get; init; }

        /// <summary>Gets the original source file path.</summary>
        public string SourceFilePath { get; init; }

        /// <summary>Gets the 1-based original source line.</summary>
        public int SourceLine { get; init; }

        /// <summary>Gets the 1-based original source column.</summary>
        public int SourceColumn { get; init; }

        /// <summary>Gets optional source symbol metadata.</summary>
        public string? Symbol { get; init; }

        /// <summary>Gets optional generated node kind metadata.</summary>
        public string? NodeKind { get; init; }
    }

    /// <summary>Default implementation of source-map creation, serialization, and lookup.</summary>
    public sealed class SourceMapManager : ISourceMapManager
    {
        private const string CurrentSchemaVersion = "1.0";

        private static readonly JsonWriterOptions WriterOptions = new()
        {
            Indented = true,
        };

        /// <inheritdoc />
        public ISourceMapBuilder CreateBuilder(string sourceFilePath, string outputFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);

            return new SourceMapBuilder(sourceFilePath, outputFilePath);
        }

        /// <inheritdoc />
        public string Serialize(SourceMap sourceMap)
        {
            ArgumentNullException.ThrowIfNull(sourceMap);

            using MemoryStream stream = new();
            using (Utf8JsonWriter writer = new(stream, WriterOptions))
            {
                writer.WriteStartObject();
                writer.WriteString("schemaVersion", sourceMap.SchemaVersion);
                writer.WriteString("sourceFilePath", sourceMap.SourceFilePath);
                writer.WriteString("outputFilePath", sourceMap.OutputFilePath);
                writer.WritePropertyName("mappings");
                writer.WriteStartArray();
                foreach (SourceMapMapping mapping in Sort(sourceMap.Mappings))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("outputLine", mapping.OutputLine);
                    writer.WriteNumber("outputColumn", mapping.OutputColumn);
                    writer.WriteString("sourceFilePath", mapping.SourceFilePath);
                    writer.WriteNumber("sourceLine", mapping.SourceLine);
                    writer.WriteNumber("sourceColumn", mapping.SourceColumn);
                    if (mapping.Symbol is not null)
                    {
                        writer.WriteString("symbol", mapping.Symbol);
                    }

                    if (mapping.NodeKind is not null)
                    {
                        writer.WriteString("nodeKind", mapping.NodeKind);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            return NormalizeJsonText(Encoding.UTF8.GetString(stream.ToArray()));
        }

        /// <inheritdoc />
        public SourceMap Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Source map JSON is required.", nameof(json));
            }

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            return new SourceMap(
                ReadRequiredString(root, "schemaVersion"),
                ReadRequiredString(root, "sourceFilePath"),
                ReadRequiredString(root, "outputFilePath"),
                ReadMappings(root.GetProperty("mappings")));
        }

        /// <inheritdoc />
        public SourceLocation? FindSourceLocation(SourceMap sourceMap, string outputFilePath, int outputLine, int outputColumn)
        {
            ArgumentNullException.ThrowIfNull(sourceMap);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
            SourceMapValidation.ValidatePositive(outputLine, nameof(outputLine));
            SourceMapValidation.ValidatePositive(outputColumn, nameof(outputColumn));

            if (!StringComparer.Ordinal.Equals(sourceMap.OutputFilePath, outputFilePath))
            {
                return null;
            }

            SourceMapMapping? mapping = Sort(sourceMap.Mappings)
                .Where(candidate => candidate.OutputLine == outputLine)
                .OrderBy(candidate => Math.Abs(candidate.OutputColumn - outputColumn))
                .ThenBy(static candidate => candidate.OutputColumn)
                .FirstOrDefault();

            return mapping is null
                ? null
                : new SourceLocation(mapping.SourceFilePath, mapping.SourceLine, mapping.SourceColumn);
        }

        /// <inheritdoc />
        public QmlLocation? FindQmlLocation(SourceMap sourceMap, string sourceFilePath, int sourceLine, int sourceColumn)
        {
            ArgumentNullException.ThrowIfNull(sourceMap);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);
            SourceMapValidation.ValidatePositive(sourceLine, nameof(sourceLine));
            SourceMapValidation.ValidatePositive(sourceColumn, nameof(sourceColumn));

            SourceMapMapping? mapping = Sort(sourceMap.Mappings)
                .Where(candidate => candidate.SourceLine == sourceLine && StringComparer.Ordinal.Equals(candidate.SourceFilePath, sourceFilePath))
                .OrderBy(candidate => Math.Abs(candidate.SourceColumn - sourceColumn))
                .ThenBy(static candidate => candidate.SourceColumn)
                .FirstOrDefault();

            return mapping is null
                ? null
                : new QmlLocation(sourceMap.OutputFilePath, mapping.OutputLine, mapping.OutputColumn);
        }

        private static ImmutableArray<SourceMapMapping> ReadMappings(JsonElement element)
        {
            ImmutableArray<SourceMapMapping>.Builder mappings = ImmutableArray.CreateBuilder<SourceMapMapping>();
            foreach (JsonElement mapping in element.EnumerateArray())
            {
                string? symbol = mapping.TryGetProperty("symbol", out JsonElement symbolElement)
                    ? symbolElement.GetString()
                    : null;
                string? nodeKind = mapping.TryGetProperty("nodeKind", out JsonElement nodeKindElement)
                    ? nodeKindElement.GetString()
                    : null;
                mappings.Add(new SourceMapMapping(
                    mapping.GetProperty("outputLine").GetInt32(),
                    mapping.GetProperty("outputColumn").GetInt32(),
                    ReadRequiredString(mapping, "sourceFilePath"),
                    mapping.GetProperty("sourceLine").GetInt32(),
                    mapping.GetProperty("sourceColumn").GetInt32(),
                    symbol,
                    nodeKind));
            }

            return Sort(mappings.ToImmutable()).ToImmutableArray();
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

        private static IEnumerable<SourceMapMapping> Sort(ImmutableArray<SourceMapMapping> mappings)
        {
            return (mappings.IsDefault ? ImmutableArray<SourceMapMapping>.Empty : mappings)
                .OrderBy(static mapping => mapping.OutputLine)
                .ThenBy(static mapping => mapping.OutputColumn)
                .ThenBy(static mapping => mapping.SourceFilePath, StringComparer.Ordinal)
                .ThenBy(static mapping => mapping.SourceLine)
                .ThenBy(static mapping => mapping.SourceColumn)
                .ThenBy(static mapping => mapping.Symbol, StringComparer.Ordinal)
                .ThenBy(static mapping => mapping.NodeKind, StringComparer.Ordinal);
        }

        private sealed class SourceMapBuilder : ISourceMapBuilder
        {
            private readonly string sourceFilePath;
            private readonly string outputFilePath;
            private readonly List<SourceMapMapping> mappings = [];

            public SourceMapBuilder(string sourceFilePath, string outputFilePath)
            {
                this.sourceFilePath = sourceFilePath;
                this.outputFilePath = outputFilePath;
            }

            public void AddMapping(SourceMapMapping mapping)
            {
                ArgumentNullException.ThrowIfNull(mapping);

                mappings.Add(mapping);
            }

            public SourceMap Build()
            {
                return new SourceMap(
                    CurrentSchemaVersion,
                    sourceFilePath,
                    outputFilePath,
                    Sort(mappings.ToImmutableArray()).ToImmutableArray());
            }
        }
    }

    internal static class SourceMapValidation
    {
        public static void ValidatePositive(int value, string parameterName)
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Source maps use 1-based positions.");
            }
        }
    }
}

#pragma warning restore MA0048
