using System.Globalization;
using System.Text;
using QmlSharp.Registry.Diagnostics;

namespace QmlSharp.Registry.Parsing
{
    internal sealed class QmltypesParser : IQmltypesParser
    {
        public ParseResult<RawQmltypesFile> Parse(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string content = File.ReadAllText(filePath);
            return ParseContent(content, filePath);
        }

        public ParseResult<RawQmltypesFile> ParseContent(string content, string sourcePath)
        {
            ArgumentNullException.ThrowIfNull(content);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

            TokenizedDocument tokenizedDocument = new Tokenizer(content, sourcePath).Tokenize();
            Parser parser = new(tokenizedDocument.Tokens, sourcePath, tokenizedDocument.Diagnostics);
            ImmutableArray<RawQmltypesComponent> components = parser.ParseComponents();
            ImmutableArray<RegistryDiagnostic> diagnostics = parser.GetDiagnostics();
            RawQmltypesFile file = new(sourcePath, components, diagnostics);

            return new ParseResult<RawQmltypesFile>(file, diagnostics);
        }

        private sealed class Parser
        {
            private readonly List<RegistryDiagnostic> diagnostics;
            private readonly string sourcePath;
            private readonly IReadOnlyList<Token> tokens;
            private int position;

            public Parser(IReadOnlyList<Token> tokens, string sourcePath, IReadOnlyList<RegistryDiagnostic> tokenizerDiagnostics)
            {
                this.tokens = tokens;
                this.sourcePath = sourcePath;
                diagnostics = [.. tokenizerDiagnostics];
            }

            public ImmutableArray<RawQmltypesComponent> ParseComponents()
            {
                ImmutableArray<RawQmltypesComponent>.Builder components = ImmutableArray.CreateBuilder<RawQmltypesComponent>();

                while (Current.Type != TokenKind.EndOfFile)
                {
                    if (IsIdentifier("import"))
                    {
                        SkipCurrentLine();
                        continue;
                    }

                    if (Current.Type == TokenKind.Identifier && Peek(1).Type == TokenKind.LeftBrace)
                    {
                        ParsedBlock block = ParseBlock();
                        CollectComponents(block, components);
                        continue;
                    }

                    ReportUnexpectedToken(Current, "top-level declaration");
                    Advance();
                }

                return components.ToImmutable();
            }

            public ImmutableArray<RegistryDiagnostic> GetDiagnostics()
            {
                return diagnostics.ToImmutableArray();
            }

            private Token Current => Peek(0);

            private void Advance()
            {
                if (position < tokens.Count - 1)
                {
                    position++;
                }
            }

            private void CollectComponents(ParsedBlock block, ImmutableArray<RawQmltypesComponent>.Builder components)
            {
                if (string.Equals(block.BlockType, "Component", StringComparison.Ordinal))
                {
                    components.Add(ConvertComponent(block));
                    return;
                }

                if (!string.Equals(block.BlockType, "Module", StringComparison.Ordinal))
                {
                    return;
                }

                foreach (ParsedBlock child in block.Children.Where(child => string.Equals(child.BlockType, "Component", StringComparison.Ordinal)))
                {
                    components.Add(ConvertComponent(child));
                }
            }

            private static RawQmltypesComponent ConvertComponent(ParsedBlock block)
            {
                return new RawQmltypesComponent(
                    Name: GetString(block.Properties, "name") ?? string.Empty,
                    AccessSemantics: GetString(block.Properties, "accessSemantics"),
                    Prototype: GetString(block.Properties, "prototype"),
                    DefaultProperty: GetString(block.Properties, "defaultProperty"),
                    AttachedType: GetString(block.Properties, "attachedType"),
                    Extension: GetString(block.Properties, "extension"),
                    IsSingleton: GetBool(block.Properties, "isSingleton"),
                    IsCreatable: GetBool(block.Properties, "isCreatable"),
                    Exports: GetStringArray(block.Properties, "exports"),
                    ExportMetaObjectRevisions: GetIntArray(block.Properties, "exportMetaObjectRevisions"),
                    Interfaces: GetStringArray(block.Properties, "interfaces"),
                    Properties: block.Children
                        .Where(child => string.Equals(child.BlockType, "Property", StringComparison.Ordinal))
                        .Select(ConvertProperty)
                        .ToImmutableArray(),
                    Signals: block.Children
                        .Where(child => string.Equals(child.BlockType, "Signal", StringComparison.Ordinal))
                        .Select(ConvertSignal)
                        .ToImmutableArray(),
                    Methods: block.Children
                        .Where(child => string.Equals(child.BlockType, "Method", StringComparison.Ordinal))
                        .Select(ConvertMethod)
                        .ToImmutableArray(),
                    Enums: block.Children
                        .Where(child => string.Equals(child.BlockType, "Enum", StringComparison.Ordinal))
                        .Select(ConvertEnum)
                        .ToImmutableArray());
            }

            private static RawQmltypesProperty ConvertProperty(ParsedBlock block)
            {
                return new RawQmltypesProperty(
                    Name: GetString(block.Properties, "name") ?? string.Empty,
                    Type: GetString(block.Properties, "type") ?? string.Empty,
                    IsReadonly: GetBool(block.Properties, "isReadonly"),
                    IsList: GetBool(block.Properties, "isList"),
                    IsPointer: GetBool(block.Properties, "isPointer"),
                    IsRequired: GetBool(block.Properties, "isRequired"),
                    Read: GetString(block.Properties, "read"),
                    Write: GetString(block.Properties, "write"),
                    Notify: GetString(block.Properties, "notify"),
                    BindableProperty: GetString(block.Properties, "bindableProperty") ?? GetString(block.Properties, "bindable"),
                    Revision: GetInt(block.Properties, "revision"));
            }

            private static RawQmltypesSignal ConvertSignal(ParsedBlock block)
            {
                return new RawQmltypesSignal(
                    Name: GetString(block.Properties, "name") ?? string.Empty,
                    Parameters: block.Children
                        .Where(child => string.Equals(child.BlockType, "Parameter", StringComparison.Ordinal))
                        .Select(ConvertParameter)
                        .ToImmutableArray(),
                    Revision: GetInt(block.Properties, "revision"));
            }

            private static RawQmltypesMethod ConvertMethod(ParsedBlock block)
            {
                return new RawQmltypesMethod(
                    Name: GetString(block.Properties, "name") ?? string.Empty,
                    ReturnType: GetString(block.Properties, "type"),
                    Parameters: block.Children
                        .Where(child => string.Equals(child.BlockType, "Parameter", StringComparison.Ordinal))
                        .Select(ConvertParameter)
                        .ToImmutableArray(),
                    Revision: GetInt(block.Properties, "revision"));
            }

            private static RawQmltypesParameter ConvertParameter(ParsedBlock block)
            {
                return new RawQmltypesParameter(
                    Name: GetString(block.Properties, "name") ?? string.Empty,
                    Type: GetString(block.Properties, "type") ?? string.Empty);
            }

            private static RawQmltypesEnum ConvertEnum(ParsedBlock block)
            {
                return new RawQmltypesEnum(
                    Name: GetString(block.Properties, "name") ?? string.Empty,
                    Alias: GetString(block.Properties, "alias"),
                    IsFlag: GetBool(block.Properties, "isFlag"),
                    Values: GetStringArray(block.Properties, "values"));
            }

            private static bool GetBool(IReadOnlyDictionary<string, object?> properties, string key)
            {
                return properties.TryGetValue(key, out object? value)
                    && value is bool boolValue
                    && boolValue;
            }

            private static int GetInt(IReadOnlyDictionary<string, object?> properties, string key)
            {
                if (!properties.TryGetValue(key, out object? value))
                {
                    return 0;
                }

                return value switch
                {
                    int intValue => intValue,
                    string stringValue when int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue) => parsedValue,
                    _ => 0,
                };
            }

            private static ImmutableArray<int> GetIntArray(IReadOnlyDictionary<string, object?> properties, string key)
            {
                if (!properties.TryGetValue(key, out object? value) || value is not List<object?> items)
                {
                    return ImmutableArray<int>.Empty;
                }

                return items
                    .Select(GetIntArrayValue)
                    .Where(item => item.HasValue)
                    .Select(item => item!.Value)
                    .ToImmutableArray();
            }

            private static int? GetIntArrayValue(object? item)
            {
                return item switch
                {
                    int intValue => intValue,
                    string stringValue when int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue) => parsedValue,
                    _ => null,
                };
            }

            private static string? GetString(IReadOnlyDictionary<string, object?> properties, string key)
            {
                return properties.TryGetValue(key, out object? value) && value is string stringValue
                    ? stringValue
                    : null;
            }

            private static ImmutableArray<string> GetStringArray(IReadOnlyDictionary<string, object?> properties, string key)
            {
                if (!properties.TryGetValue(key, out object? value) || value is not List<object?> items)
                {
                    return ImmutableArray<string>.Empty;
                }

                return items
                    .OfType<string>()
                    .ToImmutableArray();
            }

            private Token Expect(TokenKind expectedKind)
            {
                Token token = Current;

                if (token.Type == expectedKind)
                {
                    Advance();
                    return token;
                }

                diagnostics.Add(new RegistryDiagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.QmltypesSyntaxError,
                    $"Expected {ToDisplayText(expectedKind)} but got {ToDisplayText(token)}.",
                    sourcePath,
                    token.Line,
                    token.Column));

                if (token.Type != TokenKind.EndOfFile)
                {
                    Advance();
                }

                return token;
            }

            private bool IsIdentifier(string value)
            {
                return Current.Type == TokenKind.Identifier
                    && string.Equals(Current.Value, value, StringComparison.Ordinal);
            }

            private ParsedBlock ParseBlock()
            {
                Token blockTypeToken = Expect(TokenKind.Identifier);
                string blockType = blockTypeToken.Value;
                _ = Expect(TokenKind.LeftBrace);

                Dictionary<string, object?> properties = new(StringComparer.Ordinal);
                List<ParsedBlock> children = [];

                while (Current.Type != TokenKind.RightBrace && Current.Type != TokenKind.EndOfFile)
                {
                    if (Current.Type == TokenKind.Identifier && Peek(1).Type == TokenKind.LeftBrace)
                    {
                        children.Add(ParseBlock());
                        continue;
                    }

                    if (Current.Type == TokenKind.Identifier && Peek(1).Type == TokenKind.Colon)
                    {
                        ParseProperty(properties);
                        continue;
                    }

                    if (Current.Type == TokenKind.Identifier)
                    {
                        ReportMissingColonAfterPropertyName(Current, Peek(1));

                        Advance();
                        TrySkipMalformedPropertyValue();

                        continue;
                    }

                    if (Current.Type is TokenKind.Semicolon or TokenKind.Comma)
                    {
                        Advance();
                        continue;
                    }

                    ReportUnexpectedToken(Current, $"{blockType} block");
                    Advance();
                }

                _ = Expect(TokenKind.RightBrace);
                return new ParsedBlock(blockType, properties, children);
            }

            private List<object?> ParseArray(string propertyName)
            {
                _ = Expect(TokenKind.LeftBracket);
                List<object?> values = [];

                while (Current.Type != TokenKind.RightBracket && Current.Type != TokenKind.EndOfFile)
                {
                    if (TryParseScalar(out object? value))
                    {
                        values.Add(value);
                    }
                    else
                    {
                        ReportUnexpectedToken(Current, $"array value for property '{propertyName}'");
                        Advance();
                    }

                    if (Current.Type == TokenKind.Comma)
                    {
                        Advance();
                    }
                }

                _ = Expect(TokenKind.RightBracket);
                return values;
            }

            private void ParseProperty(IDictionary<string, object?> properties)
            {
                Token keyToken = Expect(TokenKind.Identifier);
                string key = keyToken.Value;

                if (Current.Type != TokenKind.Colon)
                {
                    diagnostics.Add(new RegistryDiagnostic(
                        DiagnosticSeverity.Error,
                        DiagnosticCodes.QmltypesSyntaxError,
                        $"Expected colon after property name '{key}'.",
                        sourcePath,
                        Current.Line,
                        Current.Column));

                    TrySkipMalformedPropertyValue();

                    return;
                }

                Advance();
                properties[key] = ParseValue(key);

                if (Current.Type == TokenKind.Semicolon)
                {
                    Advance();
                }
            }

            private object? ParseValue(string propertyName)
            {
                if (Current.Type == TokenKind.LeftBracket)
                {
                    return ParseArray(propertyName);
                }

                if (TryParseScalar(out object? value))
                {
                    return value;
                }

                ReportUnexpectedToken(Current, $"value for property '{propertyName}'");

                if (Current.Type != TokenKind.EndOfFile)
                {
                    Advance();
                }

                return null;
            }

            private Token Peek(int offset)
            {
                int index = Math.Min(position + offset, tokens.Count - 1);
                return tokens[index];
            }

            private void ReportUnexpectedToken(Token token, string context)
            {
                diagnostics.Add(new RegistryDiagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.QmltypesUnexpectedToken,
                    $"Unexpected token '{token.Value}' in {context}.",
                    sourcePath,
                    token.Line,
                    token.Column));
            }

            private void ReportMissingColonAfterPropertyName(Token propertyNameToken, Token unexpectedToken)
            {
                diagnostics.Add(new RegistryDiagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.QmltypesSyntaxError,
                    $"Expected colon after property name '{propertyNameToken.Value}'.",
                    sourcePath,
                    unexpectedToken.Line,
                    unexpectedToken.Column));
            }

            private bool IsRecoverablePropertyValueToken(Token token)
            {
                if (token.Type is TokenKind.String or TokenKind.Number)
                {
                    return true;
                }

                return token.Type == TokenKind.Identifier
                    && Peek(1).Type != TokenKind.LeftBrace;
            }

            private void TrySkipMalformedPropertyValue()
            {
                if (IsRecoverablePropertyValueToken(Current))
                {
                    Advance();
                }
            }

            private void SkipCurrentLine()
            {
                int currentLine = Current.Line;

                while (Current.Type != TokenKind.EndOfFile && Current.Line == currentLine)
                {
                    Advance();
                }
            }

            private static string ToDisplayText(Token token)
            {
                return $"{ToDisplayText(token.Type)} '{token.Value}'";
            }

            private static string ToDisplayText(TokenKind tokenKind)
            {
                return tokenKind switch
                {
                    TokenKind.LeftBrace => "lbrace",
                    TokenKind.RightBrace => "rbrace",
                    TokenKind.LeftBracket => "lbracket",
                    TokenKind.RightBracket => "rbracket",
                    TokenKind.Colon => "colon",
                    TokenKind.Comma => "comma",
                    TokenKind.Semicolon => "semicolon",
                    TokenKind.String => "string",
                    TokenKind.Number => "number",
                    TokenKind.Identifier => "identifier",
                    TokenKind.EndOfFile => "eof",
                    _ => tokenKind.ToString(),
                };
            }

            private bool TryParseScalar(out object? value)
            {
                Token token = Current;

                switch (token.Type)
                {
                    case TokenKind.String:
                        Advance();
                        value = token.Value;
                        return true;
                    case TokenKind.Number:
                        Advance();
                        value = int.TryParse(token.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInteger)
                            ? parsedInteger
                            : token.Value;
                        return true;
                    case TokenKind.Identifier:
                        Advance();
                        value = token.Value switch
                        {
                            "true" => true,
                            "false" => false,
                            _ => token.Value,
                        };
                        return true;
                    default:
                        value = null;
                        return false;
                }
            }
        }

        private sealed class Tokenizer
        {
            private readonly string content;
            private readonly List<RegistryDiagnostic> diagnostics = [];
            private readonly string sourcePath;
            private readonly List<Token> tokens = [];
            private int column = 1;
            private int line = 1;
            private int position;

            public Tokenizer(string content, string sourcePath)
            {
                this.content = content;
                this.sourcePath = sourcePath;
            }

            public TokenizedDocument Tokenize()
            {
                while (true)
                {
                    SkipWhitespaceAndComments();

                    if (IsAtEnd)
                    {
                        tokens.Add(new Token(TokenKind.EndOfFile, string.Empty, line, column));
                        break;
                    }

                    int tokenLine = line;
                    int tokenColumn = column;
                    TokenizeNextToken(tokenLine, tokenColumn);
                }

                return new TokenizedDocument(tokens, diagnostics);
            }

            private void TokenizeNextToken(int tokenLine, int tokenColumn)
            {
                char currentCharacter = Current;

                if (TryTokenizePunctuation(currentCharacter, tokenLine, tokenColumn))
                {
                    return;
                }

                if (IsIdentifierStart(currentCharacter))
                {
                    tokens.Add(new Token(TokenKind.Identifier, ReadIdentifier(), tokenLine, tokenColumn));
                    return;
                }

                if (char.IsDigit(currentCharacter) || (currentCharacter == '-' && char.IsDigit(Peek(1))))
                {
                    tokens.Add(new Token(TokenKind.Number, ReadNumber(), tokenLine, tokenColumn));
                    return;
                }

                diagnostics.Add(new RegistryDiagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.QmltypesUnexpectedToken,
                    $"Unexpected character '{currentCharacter}'.",
                    sourcePath,
                    tokenLine,
                    tokenColumn));
                Advance();
            }

            private bool TryTokenizePunctuation(char currentCharacter, int tokenLine, int tokenColumn)
            {
                if (currentCharacter == '"')
                {
                    tokens.Add(new Token(TokenKind.String, ReadString(tokenLine, tokenColumn), tokenLine, tokenColumn));
                    return true;
                }

                if (!PunctuationTokens.TryGetValue(currentCharacter, out TokenKind tokenKind))
                {
                    return false;
                }

                tokens.Add(new Token(tokenKind, currentCharacter.ToString(CultureInfo.InvariantCulture), tokenLine, tokenColumn));
                Advance();
                return true;
            }

            private char Current => Peek(0);

            private bool IsAtEnd => position >= content.Length;

            private void Advance()
            {
                if (IsAtEnd)
                {
                    return;
                }

                char currentCharacter = content[position];

                if (currentCharacter == '\r')
                {
                    position++;

                    if (!IsAtEnd && content[position] == '\n')
                    {
                        position++;
                    }

                    line++;
                    column = 1;
                    return;
                }

                position++;

                if (currentCharacter == '\n')
                {
                    line++;
                    column = 1;
                    return;
                }

                column++;
            }

            private static bool IsIdentifierPart(char character)
            {
                return char.IsLetterOrDigit(character) || character is '_' or '.';
            }

            private static bool IsIdentifierStart(char character)
            {
                return char.IsLetter(character) || character == '_';
            }

            private static readonly IReadOnlyDictionary<char, TokenKind> PunctuationTokens = new Dictionary<char, TokenKind>
            {
                ['{'] = TokenKind.LeftBrace,
                ['}'] = TokenKind.RightBrace,
                ['['] = TokenKind.LeftBracket,
                [']'] = TokenKind.RightBracket,
                [':'] = TokenKind.Colon,
                [','] = TokenKind.Comma,
                [';'] = TokenKind.Semicolon,
            };

            private char Peek(int offset)
            {
                int index = position + offset;
                return index >= 0 && index < content.Length
                    ? content[index]
                    : '\0';
            }

            private string ReadIdentifier()
            {
                StringBuilder builder = new();

                while (!IsAtEnd && IsIdentifierPart(Current))
                {
                    builder.Append(Current);
                    Advance();
                }

                return builder.ToString();
            }

            private string ReadNumber()
            {
                StringBuilder builder = new();

                if (Current == '-')
                {
                    builder.Append(Current);
                    Advance();
                }

                while (!IsAtEnd && (char.IsDigit(Current) || Current == '.'))
                {
                    builder.Append(Current);
                    Advance();
                }

                return builder.ToString();
            }

            private string ReadString(int tokenLine, int tokenColumn)
            {
                StringBuilder builder = new();
                Advance();

                while (!IsAtEnd)
                {
                    char currentCharacter = Current;

                    if (currentCharacter == '"')
                    {
                        Advance();
                        return builder.ToString();
                    }

                    if (currentCharacter == '\\')
                    {
                        Advance();

                        if (IsAtEnd)
                        {
                            break;
                        }

                        char escapedCharacter = Current;
                        builder.Append(escapedCharacter switch
                        {
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            '"' => '"',
                            '\\' => '\\',
                            _ => escapedCharacter,
                        });
                        Advance();
                        continue;
                    }

                    builder.Append(currentCharacter);
                    Advance();
                }

                diagnostics.Add(new RegistryDiagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.QmltypesSyntaxError,
                    "Unterminated string literal.",
                    sourcePath,
                    tokenLine,
                    tokenColumn));

                return builder.ToString();
            }

            private void SkipWhitespaceAndComments()
            {
                while (!IsAtEnd)
                {
                    if (char.IsWhiteSpace(Current))
                    {
                        Advance();
                        continue;
                    }

                    if (Current == '/' && Peek(1) == '/')
                    {
                        SkipSingleLineComment();
                        continue;
                    }

                    if (Current == '/' && Peek(1) == '*')
                    {
                        SkipBlockComment();
                        continue;
                    }

                    return;
                }
            }

            private void SkipBlockComment()
            {
                int commentLine = line;
                int commentColumn = column;
                Advance();
                Advance();

                while (!IsAtEnd)
                {
                    if (Current == '*' && Peek(1) == '/')
                    {
                        Advance();
                        Advance();
                        return;
                    }

                    Advance();
                }

                diagnostics.Add(new RegistryDiagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.QmltypesSyntaxError,
                    "Unterminated block comment.",
                    sourcePath,
                    commentLine,
                    commentColumn));
            }

            private void SkipSingleLineComment()
            {
                while (!IsAtEnd && Current is not '\r' and not '\n')
                {
                    Advance();
                }
            }
        }

        private sealed record ParsedBlock(
            string BlockType,
            Dictionary<string, object?> Properties,
            List<ParsedBlock> Children);

        private sealed record Token(
            TokenKind Type,
            string Value,
            int Line,
            int Column);

        private sealed record TokenizedDocument(
            IReadOnlyList<Token> Tokens,
            IReadOnlyList<RegistryDiagnostic> Diagnostics);

        private enum TokenKind
        {
            Identifier,
            String,
            Number,
            LeftBrace,
            RightBrace,
            LeftBracket,
            RightBracket,
            Colon,
            Comma,
            Semicolon,
            EndOfFile,
        }
    }
}
