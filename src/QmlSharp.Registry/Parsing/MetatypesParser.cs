using System.Globalization;
using System.Text;
using System.Text.Json;
using QmlSharp.Registry.Diagnostics;

namespace QmlSharp.Registry.Parsing
{
    public sealed class MetatypesParser : IMetatypesParser
    {
        public ParseResult<RawMetatypesFile> Parse(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string content = File.ReadAllText(filePath);
            return ParseContent(content, filePath);
        }

        public ParseResult<RawMetatypesFile> ParseContent(string content, string sourcePath)
        {
            ArgumentNullException.ThrowIfNull(content);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

            RawMetatypesFile file = new Parser(content, sourcePath).Parse();
            return new ParseResult<RawMetatypesFile>(file, file.Diagnostics);
        }

        private sealed class Parser
        {
            private readonly string content;
            private readonly List<RegistryDiagnostic> diagnostics = [];
            private readonly string sourcePath;

            public Parser(string content, string sourcePath)
            {
                this.content = content;
                this.sourcePath = sourcePath;
            }

            public RawMetatypesFile Parse()
            {
                ImmutableArray<RawMetatypesEntry> entries = ImmutableArray<RawMetatypesEntry>.Empty;

                try
                {
                    using JsonDocument document = JsonDocument.Parse(content);
                    JsonElement root = document.RootElement;

                    if (root.ValueKind != JsonValueKind.Array)
                    {
                        ReportShapeError("The metatypes document must be a top-level JSON array.");
                    }
                    else
                    {
                        entries = ParseEntries(root);
                    }
                }
                catch (JsonException exception)
                {
                    ReportJsonError(exception);
                }

                return new RawMetatypesFile(
                    SourcePath: sourcePath,
                    Entries: entries,
                    Diagnostics: diagnostics.ToImmutableArray());
            }

            private ImmutableArray<RawMetatypesEntry> ParseEntries(JsonElement root)
            {
                ImmutableArray<RawMetatypesEntry>.Builder entries = ImmutableArray.CreateBuilder<RawMetatypesEntry>();
                int index = 0;

                foreach (JsonElement entryElement in root.EnumerateArray())
                {
                    RawMetatypesEntry? entry = ParseEntry(entryElement, $"entry[{index}]");
                    if (entry is not null)
                    {
                        entries.Add(entry);
                    }

                    index++;
                }

                return entries.ToImmutable();
            }

            private RawMetatypesEntry? ParseEntry(JsonElement entryElement, string context)
            {
                if (entryElement.ValueKind != JsonValueKind.Object)
                {
                    ReportShapeError($"{context} must be a JSON object.");
                    return null;
                }

                string? inputFile = GetOptionalString(entryElement, context, "inputFile");
                ImmutableArray<RawMetatypesClass> classes = ParseArray(
                    entryElement,
                    context,
                    propertyName: "classes",
                    required: false,
                    ParseClass);

                return new RawMetatypesEntry(
                    InputFile: inputFile,
                    Classes: classes);
            }

            private RawMetatypesClass? ParseClass(JsonElement classElement, string context)
            {
                if (classElement.ValueKind != JsonValueKind.Object)
                {
                    ReportShapeError($"{context} must be a JSON object.");
                    return null;
                }

                string? className = GetRequiredString(classElement, context, "className");
                if (className is null)
                {
                    return null;
                }

                ImmutableArray<RawMetatypesMethod>.Builder methods = ImmutableArray.CreateBuilder<RawMetatypesMethod>();
                AppendArrayItems(classElement, context, "methods", ParseMethod, methods);
                AppendArrayItems(classElement, context, "slots", ParseMethod, methods);

                return new RawMetatypesClass(
                    ClassName: className,
                    QualifiedClassName: GetOptionalString(classElement, context, "qualifiedClassName"),
                    IsObject: GetOptionalBoolean(classElement, context, defaultValue: false, "object"),
                    IsGadget: GetOptionalBoolean(classElement, context, defaultValue: false, "gadget"),
                    IsNamespace: GetOptionalBoolean(classElement, context, defaultValue: false, "namespace"),
                    SuperClasses: ParseArray(classElement, context, "superClasses", required: false, ParseSuperClass),
                    ClassInfos: ParseArray(classElement, context, "classInfos", required: false, ParseClassInfo),
                    Properties: ParseArray(classElement, context, "properties", required: false, ParseProperty),
                    Signals: ParseArray(classElement, context, "signals", required: false, ParseSignal),
                    Methods: methods.ToImmutable(),
                    Enums: ParseArray(classElement, context, "enums", required: false, ParseEnum));
            }

            private RawMetatypesSuperClass? ParseSuperClass(JsonElement superClassElement, string context)
            {
                if (superClassElement.ValueKind != JsonValueKind.Object)
                {
                    ReportShapeError($"{context} must be a JSON object.");
                    return null;
                }

                string? name = GetRequiredString(superClassElement, context, "name");
                if (name is null)
                {
                    return null;
                }

                return new RawMetatypesSuperClass(
                    Name: name,
                    Access: GetOptionalString(superClassElement, context, "access") ?? "public");
            }

            private RawMetatypesClassInfo? ParseClassInfo(JsonElement classInfoElement, string context)
            {
                if (classInfoElement.ValueKind != JsonValueKind.Object)
                {
                    ReportShapeError($"{context} must be a JSON object.");
                    return null;
                }

                string? name = GetRequiredString(classInfoElement, context, "name");
                string? value = GetRequiredString(classInfoElement, context, allowEmpty: true, "value");
                if (name is null || value is null)
                {
                    return null;
                }

                return new RawMetatypesClassInfo(
                    Name: name,
                    Value: value);
            }

            private RawMetatypesProperty? ParseProperty(JsonElement propertyElement, string context)
            {
                if (propertyElement.ValueKind != JsonValueKind.Object)
                {
                    ReportShapeError($"{context} must be a JSON object.");
                    return null;
                }

                string? name = GetRequiredString(propertyElement, context, allowEmpty: true, "name");
                string? type = GetRequiredString(propertyElement, context, "type");
                if (name is null || type is null)
                {
                    return null;
                }

                return new RawMetatypesProperty(
                    Name: name,
                    Type: type,
                    Read: GetOptionalString(propertyElement, context, "read"),
                    Write: GetOptionalString(propertyElement, context, "write"),
                    Notify: GetOptionalString(propertyElement, context, "notify"),
                    BindableProperty: GetOptionalString(propertyElement, context, "bindableProperty", "bindable"),
                    Revision: GetOptionalInt(propertyElement, context, defaultValue: 0, "revision"),
                    Index: GetOptionalInt(propertyElement, context, defaultValue: 0, "index"),
                    IsReadonly: GetOptionalBoolean(propertyElement, context, defaultValue: false, "isReadonly", "readonly", "readOnly"),
                    IsConstant: GetOptionalBoolean(propertyElement, context, defaultValue: false, "isConstant", "constant"),
                    IsFinal: GetOptionalBoolean(propertyElement, context, defaultValue: false, "isFinal", "final"),
                    IsRequired: GetOptionalBoolean(propertyElement, context, defaultValue: false, "isRequired", "required"));
            }

            private RawMetatypesSignal? ParseSignal(JsonElement signalElement, string context)
            {
                if (signalElement.ValueKind != JsonValueKind.Object)
                {
                    ReportShapeError($"{context} must be a JSON object.");
                    return null;
                }

                string? name = GetRequiredString(signalElement, context, "name");
                if (name is null)
                {
                    return null;
                }

                return new RawMetatypesSignal(
                    Name: name,
                    Arguments: ParseArray(signalElement, context, "arguments", required: false, ParseParameter),
                    Revision: GetOptionalInt(signalElement, context, defaultValue: 0, "revision"));
            }

            private RawMetatypesMethod? ParseMethod(JsonElement methodElement, string context)
            {
                if (methodElement.ValueKind != JsonValueKind.Object)
                {
                    ReportShapeError($"{context} must be a JSON object.");
                    return null;
                }

                string? name = GetRequiredString(methodElement, context, "name");
                if (name is null)
                {
                    return null;
                }

                return new RawMetatypesMethod(
                    Name: name,
                    ReturnType: GetOptionalString(methodElement, context, "returnType"),
                    Arguments: ParseArray(methodElement, context, "arguments", required: false, ParseParameter),
                    Revision: GetOptionalInt(methodElement, context, defaultValue: 0, "revision"),
                    IsCloned: GetOptionalBoolean(methodElement, context, defaultValue: false, "isCloned"));
            }

            private RawMetatypesParameter? ParseParameter(JsonElement parameterElement, string context)
            {
                if (parameterElement.ValueKind != JsonValueKind.Object)
                {
                    ReportShapeError($"{context} must be a JSON object.");
                    return null;
                }

                string? type = GetRequiredString(parameterElement, context, "type");
                if (type is null)
                {
                    return null;
                }

                return new RawMetatypesParameter(
                    Name: GetOptionalString(parameterElement, context, "name") ?? string.Empty,
                    Type: type);
            }

            private RawMetatypesEnum? ParseEnum(JsonElement enumElement, string context)
            {
                if (enumElement.ValueKind != JsonValueKind.Object)
                {
                    ReportShapeError($"{context} must be a JSON object.");
                    return null;
                }

                string? name = GetRequiredString(enumElement, context, "name");
                if (name is null)
                {
                    return null;
                }

                return new RawMetatypesEnum(
                    Name: name,
                    Alias: GetOptionalString(enumElement, context, "alias"),
                    IsFlag: GetOptionalBoolean(enumElement, context, defaultValue: false, "isFlag"),
                    IsClass: GetOptionalBoolean(enumElement, context, defaultValue: false, "isClass"),
                    Values: ParseEnumValues(enumElement, context));
            }

            private ImmutableArray<string> ParseEnumValues(JsonElement enumElement, string context)
            {
                if (!TryGetProperty(enumElement, out JsonElement valuesElement, out string propertyName, "values"))
                {
                    return ImmutableArray<string>.Empty;
                }

                switch (valuesElement.ValueKind)
                {
                    case JsonValueKind.Array:
                        {
                            ImmutableArray<string>.Builder values = ImmutableArray.CreateBuilder<string>();
                            int index = 0;

                            foreach (JsonElement valueElement in valuesElement.EnumerateArray())
                            {
                                switch (valueElement.ValueKind)
                                {
                                    case JsonValueKind.String:
                                        values.Add(valueElement.GetString() ?? string.Empty);
                                        break;
                                    case JsonValueKind.Number:
                                    case JsonValueKind.True:
                                    case JsonValueKind.False:
                                        values.Add(valueElement.ToString());
                                        break;
                                    default:
                                        ReportShapeError($"{context}.{propertyName}[{index}] must be a string, number, or boolean value.");
                                        break;
                                }

                                index++;
                            }

                            return values.ToImmutable();
                        }

                    case JsonValueKind.Object:
                        return valuesElement.EnumerateObject()
                            .Select(property => property.Name)
                            .ToImmutableArray();

                    case JsonValueKind.Null:
                        return ImmutableArray<string>.Empty;

                    default:
                        ReportShapeError($"{context} property '{propertyName}' must be a JSON array or object.");
                        return ImmutableArray<string>.Empty;
                }
            }

            private ImmutableArray<T> ParseArray<T>(
                JsonElement ownerElement,
                string context,
                string propertyName,
                bool required,
                Func<JsonElement, string, T?> parser)
                where T : class
            {
                if (!TryGetArray(ownerElement, context, propertyName, required, out JsonElement arrayElement))
                {
                    return ImmutableArray<T>.Empty;
                }

                ImmutableArray<T>.Builder items = ImmutableArray.CreateBuilder<T>();
                int index = 0;

                foreach (JsonElement itemElement in arrayElement.EnumerateArray())
                {
                    T? item = parser(itemElement, $"{context}.{propertyName}[{index}]");
                    if (item is not null)
                    {
                        items.Add(item);
                    }

                    index++;
                }

                return items.ToImmutable();
            }

            private void AppendArrayItems<T>(
                JsonElement ownerElement,
                string context,
                string propertyName,
                Func<JsonElement, string, T?> parser,
                ImmutableArray<T>.Builder items)
                where T : class
            {
                if (!TryGetArray(ownerElement, context, propertyName, required: false, out JsonElement arrayElement))
                {
                    return;
                }

                int index = 0;

                foreach (JsonElement itemElement in arrayElement.EnumerateArray())
                {
                    T? item = parser(itemElement, $"{context}.{propertyName}[{index}]");
                    if (item is not null)
                    {
                        items.Add(item);
                    }

                    index++;
                }
            }

            private string? GetOptionalString(JsonElement objectElement, string context, params string[] propertyNames)
            {
                if (!TryGetProperty(objectElement, out JsonElement propertyElement, out string propertyName, propertyNames))
                {
                    return null;
                }

                if (propertyElement.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }

                if (propertyElement.ValueKind == JsonValueKind.String)
                {
                    return propertyElement.GetString();
                }

                ReportShapeError($"{context} property '{propertyName}' must be a string.");
                return null;
            }

            private string? GetRequiredString(JsonElement objectElement, string context, params string[] propertyNames)
            {
                return GetRequiredString(objectElement, context, allowEmpty: false, propertyNames);
            }

            private string? GetRequiredString(JsonElement objectElement, string context, bool allowEmpty, params string[] propertyNames)
            {
                if (!TryGetProperty(objectElement, out JsonElement propertyElement, out string propertyName, propertyNames))
                {
                    ReportShapeError($"{context} is missing required string property '{propertyNames[0]}'.");
                    return null;
                }

                if (propertyElement.ValueKind != JsonValueKind.String)
                {
                    ReportShapeError($"{context} property '{propertyName}' must be a string.");
                    return null;
                }

                string? value = propertyElement.GetString();
                if (!allowEmpty && string.IsNullOrWhiteSpace(value))
                {
                    ReportShapeError($"{context} property '{propertyName}' must be a non-empty string.");
                    return null;
                }

                return value;
            }

            private bool GetOptionalBoolean(JsonElement objectElement, string context, bool defaultValue, params string[] propertyNames)
            {
                if (!TryGetProperty(objectElement, out JsonElement propertyElement, out string propertyName, propertyNames))
                {
                    return defaultValue;
                }

                switch (propertyElement.ValueKind)
                {
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return propertyElement.GetBoolean();
                    case JsonValueKind.String:
                        {
                            string? value = propertyElement.GetString();
                            if (bool.TryParse(value, out bool parsedBoolean))
                            {
                                return parsedBoolean;
                            }

                            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInteger))
                            {
                                return parsedInteger != 0;
                            }

                            break;
                        }

                    case JsonValueKind.Number when propertyElement.TryGetInt32(out int integerValue):
                        return integerValue != 0;
                }

                ReportShapeError($"{context} property '{propertyName}' must be a boolean.");
                return defaultValue;
            }

            private int GetOptionalInt(JsonElement objectElement, string context, int defaultValue, params string[] propertyNames)
            {
                if (!TryGetProperty(objectElement, out JsonElement propertyElement, out string propertyName, propertyNames))
                {
                    return defaultValue;
                }

                switch (propertyElement.ValueKind)
                {
                    case JsonValueKind.Number when propertyElement.TryGetInt32(out int integerValue):
                        return integerValue;
                    case JsonValueKind.String:
                        {
                            string? value = propertyElement.GetString();
                            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInteger))
                            {
                                return parsedInteger;
                            }

                            break;
                        }

                    case JsonValueKind.Null:
                        return defaultValue;
                }

                ReportShapeError($"{context} property '{propertyName}' must be an integer.");
                return defaultValue;
            }

            private static bool TryGetProperty(JsonElement objectElement, out JsonElement propertyElement, out string propertyName, params string[] propertyNames)
            {
                (bool found, string name, JsonElement element) = propertyNames
                    .Select(candidateName => (
                        found: objectElement.TryGetProperty(candidateName, out JsonElement candidateElement),
                        name: candidateName,
                        element: candidateElement))
                    .Where(candidate => candidate.found)
                    .FirstOrDefault();

                if (found)
                {
                    propertyElement = element;
                    propertyName = name;
                    return true;
                }

                propertyElement = default;
                propertyName = propertyNames[0];
                return false;
            }

            private bool TryGetArray(JsonElement objectElement, string context, string propertyName, bool required, out JsonElement arrayElement)
            {
                if (!objectElement.TryGetProperty(propertyName, out arrayElement))
                {
                    if (required)
                    {
                        ReportShapeError($"{context} is missing required array property '{propertyName}'.");
                    }

                    return false;
                }

                if (arrayElement.ValueKind == JsonValueKind.Null)
                {
                    if (required)
                    {
                        ReportShapeError($"{context} property '{propertyName}' must be a JSON array.");
                    }

                    return false;
                }

                if (arrayElement.ValueKind != JsonValueKind.Array)
                {
                    ReportShapeError($"{context} property '{propertyName}' must be a JSON array.");
                    return false;
                }

                return true;
            }

            private void ReportJsonError(JsonException exception)
            {
                int? line = ToClampedOneBasedPosition(exception.LineNumber);
                int? column = GetJsonErrorColumn(exception);

                diagnostics.Add(new RegistryDiagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.MetatypesJsonError,
                    $"Invalid metatypes JSON: {exception.Message}",
                    sourcePath,
                    line,
                    column));
            }

            private int? GetJsonErrorColumn(JsonException exception)
            {
                if (exception.BytePositionInLine is not long bytePositionInLine)
                {
                    return null;
                }

                if (bytePositionInLine >= int.MaxValue)
                {
                    return int.MaxValue;
                }

                if (exception.LineNumber is long lineNumber and >= 0 and < int.MaxValue)
                {
                    return GetOneBasedCharacterColumn(lineNumber, bytePositionInLine);
                }

                return ToClampedOneBasedPosition(bytePositionInLine);
            }

            private int GetOneBasedCharacterColumn(long zeroBasedLineNumber, long bytePositionInLine)
            {
                ReadOnlySpan<char> line = GetLine(zeroBasedLineNumber);
                if (line.IsEmpty)
                {
                    return ToClampedOneBasedPosition(bytePositionInLine)!.Value;
                }

                long utf8BytesSeen = 0;
                int charIndex = 0;
                int characterIndex = 0;

                while (charIndex < line.Length && utf8BytesSeen < bytePositionInLine)
                {
                    int charsInCharacter = char.IsHighSurrogate(line[charIndex])
                        && charIndex + 1 < line.Length
                        && char.IsLowSurrogate(line[charIndex + 1])
                            ? 2
                            : 1;

                    utf8BytesSeen += Encoding.UTF8.GetByteCount(line.Slice(charIndex, charsInCharacter));
                    charIndex += charsInCharacter;
                    characterIndex++;
                }

                return characterIndex == int.MaxValue
                    ? int.MaxValue
                    : characterIndex + 1;
            }

            private ReadOnlySpan<char> GetLine(long zeroBasedLineNumber)
            {
                int lineStart = 0;
                long currentLineNumber = 0;

                for (int index = 0; index < content.Length; index++)
                {
                    if (content[index] != '\r' && content[index] != '\n')
                    {
                        continue;
                    }

                    if (currentLineNumber == zeroBasedLineNumber)
                    {
                        return content.AsSpan(lineStart, index - lineStart);
                    }

                    if (content[index] == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                    {
                        index++;
                    }

                    lineStart = index + 1;
                    currentLineNumber++;
                }

                return currentLineNumber == zeroBasedLineNumber
                    ? content.AsSpan(lineStart)
                    : [];
            }

            private static int? ToClampedOneBasedPosition(long? position)
            {
                if (position is not long value)
                {
                    return null;
                }

                if (value < 0)
                {
                    return null;
                }

                if (value >= int.MaxValue)
                {
                    return int.MaxValue;
                }

                return (int)value + 1;
            }

            private void ReportShapeError(string message)
            {
                diagnostics.Add(new RegistryDiagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.MetatypesMissingField,
                    message,
                    sourcePath,
                    Line: null,
                    Column: null));
            }
        }
    }
}
