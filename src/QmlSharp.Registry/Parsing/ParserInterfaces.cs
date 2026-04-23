#pragma warning disable MA0048

namespace QmlSharp.Registry.Parsing
{
    /// <summary>
    /// Parses Qt .qmltypes files into raw AST representations.
    /// The .qmltypes format is a Qt-specific declarative syntax (not JSON, not QML).
    /// </summary>
    public interface IQmltypesParser
    {
        ParseResult<RawQmltypesFile> Parse(string filePath);

        ParseResult<RawQmltypesFile> ParseContent(string content, string sourcePath);
    }

    /// <summary>
    /// Parses Qt qmldir files into raw AST representations.
    /// The qmldir format is line-oriented plain text with directives.
    /// </summary>
    public interface IQmldirParser
    {
        ParseResult<RawQmldirFile> Parse(string filePath);

        ParseResult<RawQmldirFile> ParseContent(string content, string sourcePath);
    }

    /// <summary>
    /// Parses Qt *_metatypes.json files into raw AST representations.
    /// These files use standard JSON format and are parsed with System.Text.Json.
    /// </summary>
    public interface IMetatypesParser
    {
        ParseResult<RawMetatypesFile> Parse(string filePath);

        ParseResult<RawMetatypesFile> ParseContent(string content, string sourcePath);
    }
}

#pragma warning restore MA0048
