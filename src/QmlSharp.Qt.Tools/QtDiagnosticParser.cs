using System.Globalization;
using System.Text.Json;

namespace QmlSharp.Qt.Tools
{
    /// <summary>Default diagnostic parser for Qt tool JSON and stderr output.</summary>
    public sealed class QtDiagnosticParser : IQtDiagnosticParser
    {
        /// <inheritdoc />
        public ImmutableArray<QtDiagnostic> ParseJson(string jsonOutput)
        {
            ArgumentNullException.ThrowIfNull(jsonOutput);

            if (string.IsNullOrWhiteSpace(jsonOutput))
            {
                return [];
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(jsonOutput);
                ImmutableArray<QtDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<QtDiagnostic>();

                ParseJsonElement(document.RootElement, null, diagnostics);

                return diagnostics.ToImmutable();
            }
            catch (JsonException)
            {
                return [];
            }
            catch (InvalidOperationException)
            {
                return [];
            }
        }

        /// <inheritdoc />
        public ImmutableArray<QtDiagnostic> ParseStderr(string stderrText)
        {
            return ParseStderr(stderrText, null);
        }

        /// <inheritdoc />
        public ImmutableArray<QtDiagnostic> ParseStderr(string stderrText, string? filenameOverride)
        {
            ArgumentNullException.ThrowIfNull(stderrText);

            if (string.IsNullOrWhiteSpace(stderrText))
            {
                return [];
            }

            ImmutableArray<QtDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<QtDiagnostic>();

            string[] lines = stderrText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                QtDiagnostic? diagnostic = ParseStandardStderrLine(line, filenameOverride)
                    ?? ParseBracketStderrLine(line, filenameOverride);
                if (diagnostic is not null)
                {
                    diagnostics.Add(diagnostic);
                }
                else if (diagnostics.Count > 0 && char.IsWhiteSpace(rawLine, 0))
                {
                    QtDiagnostic previous = diagnostics[^1];
                    diagnostics[^1] = previous with
                    {
                        Message = string.Concat(previous.Message, Environment.NewLine, line),
                    };
                }
            }

            return diagnostics.ToImmutable();
        }

        private static void ParseJsonElement(
            JsonElement element,
            string? fallbackFile,
            ImmutableArray<QtDiagnostic>.Builder diagnostics)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    ParseJsonElement(item, fallbackFile, diagnostics);
                }

                return;
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (TryGetArray(element, "files", out JsonElement files))
            {
                ParseJsonFiles(files, fallbackFile, diagnostics);
                return;
            }

            if (TryGetArray(element, "diagnostics", out JsonElement diagnosticsElement))
            {
                ParseJsonElement(diagnosticsElement, fallbackFile, diagnostics);
                return;
            }

            if (TryGetArray(element, "warnings", out JsonElement warningsElement))
            {
                ParseJsonElement(warningsElement, fallbackFile, diagnostics);
                return;
            }

            QtDiagnostic? diagnostic = ParseJsonDiagnosticObject(element, fallbackFile);
            if (diagnostic is not null)
            {
                diagnostics.Add(diagnostic);
            }
        }

        private static void ParseJsonFiles(
            JsonElement files,
            string? fallbackFile,
            ImmutableArray<QtDiagnostic>.Builder diagnostics)
        {
            foreach (JsonElement fileElement in files.EnumerateArray())
            {
                string? file = ReadString(fileElement, "filename")
                    ?? ReadString(fileElement, "file")
                    ?? ReadString(fileElement, "path")
                    ?? fallbackFile;

                ParseJsonFileArray(fileElement, "warnings", file, diagnostics);
                ParseJsonFileArray(fileElement, "diagnostics", file, diagnostics);
                ParseJsonFileArray(fileElement, "messages", file, diagnostics);
            }
        }

        private static void ParseJsonFileArray(
            JsonElement fileElement,
            string propertyName,
            string? file,
            ImmutableArray<QtDiagnostic>.Builder diagnostics)
        {
            if (TryGetArray(fileElement, propertyName, out JsonElement array))
            {
                ParseJsonElement(array, file, diagnostics);
            }
        }

        private static QtDiagnostic? ParseJsonDiagnosticObject(JsonElement element, string? fallbackFile)
        {
            string? message = ReadString(element, "message")
                ?? ReadString(element, "text")
                ?? ReadString(element, "description");
            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            return new QtDiagnostic
            {
                File = ReadString(element, "filename")
                    ?? ReadString(element, "file")
                    ?? ReadString(element, "path")
                    ?? fallbackFile,
                Line = ReadInt32(element, "line"),
                Column = ReadInt32(element, "column"),
                Severity = MapSeverity(
                    ReadString(element, "type")
                    ?? ReadString(element, "severity")
                    ?? ReadString(element, "level")),
                Message = message.Trim(),
                Category = ReadString(element, "category")
                    ?? ReadString(element, "id")
                    ?? ReadString(element, "code"),
                Suggestion = ReadSuggestion(element),
            };
        }

        private static QtDiagnostic? ParseStandardStderrLine(string line, string? filenameOverride)
        {
            ParsedMessageLine? parsedMessage = SplitMessageLine(line);
            if (parsedMessage is null)
            {
                return null;
            }

            ParsedLocation? location = ParseLocation(parsedMessage.Prefix, filenameOverride);
            if (location is null)
            {
                return null;
            }

            return new QtDiagnostic
            {
                File = location.File,
                Line = location.Line,
                Column = location.Column,
                Severity = MapSeverity(parsedMessage.Severity),
                Message = parsedMessage.Message,
            };
        }

        private static QtDiagnostic? ParseBracketStderrLine(string line, string? filenameOverride)
        {
            if (line[0] != '[')
            {
                return null;
            }

            int lastBracket = line.LastIndexOf(']');
            if (lastBracket < 0 || lastBracket + 1 >= line.Length)
            {
                return null;
            }

            string tail = line[(lastBracket + 1)..].Trim();
            int separator = tail.IndexOf(':');
            if (separator <= 0)
            {
                return null;
            }

            string severity = tail[..separator].Trim();
            string message = tail[(separator + 1)..].Trim();
            if (message.Length == 0 || !IsKnownSeverity(severity))
            {
                return null;
            }

            return new QtDiagnostic
            {
                File = filenameOverride,
                Severity = MapSeverity(severity),
                Message = message,
            };
        }

        private static ParsedMessageLine? SplitMessageLine(string line)
        {
            for (int index = 0; index < line.Length; index++)
            {
                if (line[index] != ':')
                {
                    continue;
                }

                int cursor = index + 1;
                while (cursor < line.Length && char.IsWhiteSpace(line, cursor))
                {
                    cursor++;
                }

                foreach (string severity in KnownSeverityStrings)
                {
                    if (!StartsWithSeverity(line, cursor, severity))
                    {
                        continue;
                    }

                    int afterSeverity = cursor + severity.Length;
                    if (afterSeverity >= line.Length || line[afterSeverity] != ':')
                    {
                        continue;
                    }

                    string prefix = line[..index].Trim();
                    string message = line[(afterSeverity + 1)..].Trim();
                    if (prefix.Length == 0 || message.Length == 0)
                    {
                        continue;
                    }

                    return new ParsedMessageLine(prefix, severity, message);
                }
            }

            return null;
        }

        private static ParsedLocation? ParseLocation(string prefix, string? filenameOverride)
        {
            string[] segments = prefix.Split(':');
            if (segments.Length == 0)
            {
                return null;
            }

            string last = segments[^1].Trim();
            string? secondLast = segments.Length >= 2 ? segments[^2].Trim() : null;

            if (secondLast is not null && IsDecimal(secondLast) && IsDecimal(last))
            {
                string file = string.Join(':', segments[..^2]).Trim();
                if (file.Length == 0)
                {
                    return new ParsedLocation(filenameOverride, int.Parse(secondLast, CultureInfo.InvariantCulture), int.Parse(last, CultureInfo.InvariantCulture));
                }

                return new ParsedLocation(file, int.Parse(secondLast, CultureInfo.InvariantCulture), int.Parse(last, CultureInfo.InvariantCulture));
            }

            if (IsDecimal(last))
            {
                string file = string.Join(':', segments[..^1]).Trim();
                if (file.Length == 0)
                {
                    return new ParsedLocation(filenameOverride, int.Parse(last, CultureInfo.InvariantCulture), null);
                }

                return new ParsedLocation(file, int.Parse(last, CultureInfo.InvariantCulture), null);
            }

            return null;
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property))
            {
                return null;
            }

            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => null,
            };
        }

        private static int? ReadInt32(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property))
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int number))
            {
                return number;
            }

            if (property.ValueKind == JsonValueKind.String
                && int.TryParse(property.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            return null;
        }

        private static string? ReadSuggestion(JsonElement element)
        {
            string? directSuggestion = ReadString(element, "suggestion");
            if (!string.IsNullOrWhiteSpace(directSuggestion))
            {
                return directSuggestion;
            }

            if (!TryGetArray(element, "suggestions", out JsonElement suggestions))
            {
                return null;
            }

            foreach (JsonElement suggestion in suggestions.EnumerateArray())
            {
                if (suggestion.ValueKind == JsonValueKind.String)
                {
                    return suggestion.GetString();
                }

                if (suggestion.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                string? message = ReadString(suggestion, "message")
                    ?? ReadString(suggestion, "description")
                    ?? ReadString(suggestion, "replacement");
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message;
                }
            }

            return null;
        }

        private static bool TryGetArray(JsonElement element, string propertyName, out JsonElement array)
        {
            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(propertyName, out JsonElement property)
                && property.ValueKind == JsonValueKind.Array)
            {
                array = property;
                return true;
            }

            array = default;
            return false;
        }

        private static DiagnosticSeverity MapSeverity(string? severity)
        {
            if (string.IsNullOrWhiteSpace(severity))
            {
                return DiagnosticSeverity.Warning;
            }

            return severity.Trim().ToLowerInvariant() switch
            {
                "error" or "fatal" or "critical" => DiagnosticSeverity.Error,
                "warning" or "warn" => DiagnosticSeverity.Warning,
                "info" or "information" => DiagnosticSeverity.Info,
                "hint" or "note" => DiagnosticSeverity.Hint,
                "disabled" or "disable" or "off" => DiagnosticSeverity.Disabled,
                _ => DiagnosticSeverity.Warning,
            };
        }

        private static bool IsKnownSeverity(string severity)
        {
            foreach (string knownSeverity in KnownSeverityStrings)
            {
                if (string.Equals(severity, knownSeverity, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool StartsWithSeverity(string line, int startIndex, string severity)
        {
            return startIndex + severity.Length < line.Length
                && string.Compare(line, startIndex, severity, 0, severity.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static bool IsDecimal(string value)
        {
            if (value.Length == 0)
            {
                return false;
            }

            foreach (char character in value)
            {
                if (!char.IsAsciiDigit(character))
                {
                    return false;
                }
            }

            return true;
        }

        private static readonly ImmutableArray<string> KnownSeverityStrings =
        [
            "error",
            "fatal",
            "critical",
            "warning",
            "warn",
            "info",
            "information",
            "hint",
            "note",
            "disabled",
            "disable",
            "off",
        ];

        private sealed record ParsedMessageLine(string Prefix, string Severity, string Message);

        private sealed record ParsedLocation(string? File, int? Line, int? Column);
    }
}
