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
        SourceLocation? FindQmlLocation(SourceMap sourceMap, string sourceFilePath, int sourceLine, int sourceColumn);
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
        ImmutableArray<SourceMapMapping> Mappings);

    /// <summary>One source-map mapping entry.</summary>
    public sealed record SourceMapMapping(
        int OutputLine,
        int OutputColumn,
        string SourceFilePath,
        int SourceLine,
        int SourceColumn,
        string? Symbol = null,
        string? NodeKind = null);
}

#pragma warning restore MA0048
