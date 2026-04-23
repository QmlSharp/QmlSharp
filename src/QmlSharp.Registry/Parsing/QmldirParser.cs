using QmlSharp.Registry.Diagnostics;

namespace QmlSharp.Registry.Parsing
{
    public sealed class QmldirParser : IQmldirParser
    {
        public ParseResult<RawQmldirFile> Parse(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string content = File.ReadAllText(filePath);
            return ParseContent(content, filePath);
        }

        public ParseResult<RawQmldirFile> ParseContent(string content, string sourcePath)
        {
            ArgumentNullException.ThrowIfNull(content);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

            RawQmldirFile file = new Parser(content, sourcePath).Parse();
            return new ParseResult<RawQmldirFile>(file, file.Diagnostics);
        }

        private sealed class Parser
        {
            private readonly string content;
            private readonly List<RegistryDiagnostic> diagnostics = [];
            private readonly List<RawQmldirImport> depends = [];
            private readonly List<string> designersupported = [];
            private readonly List<RawQmldirImport> imports = [];
            private readonly List<RawQmldirPlugin> plugins = [];
            private readonly string sourcePath;
            private readonly List<RawQmldirTypeEntry> typeEntries = [];
            private string? classname;
            private string? module;
            private string? typeinfo;

            public Parser(string content, string sourcePath)
            {
                this.content = content;
                this.sourcePath = sourcePath;
            }

            public RawQmldirFile Parse()
            {
                using StringReader reader = new(content);
                int lineNumber = 0;

                while (reader.ReadLine() is { } line)
                {
                    lineNumber++;
                    ParseLine(line, lineNumber);
                }

                ImmutableArray<RegistryDiagnostic> immutableDiagnostics = diagnostics.ToImmutableArray();

                return new RawQmldirFile(
                    SourcePath: sourcePath,
                    Module: module,
                    Plugins: plugins.ToImmutableArray(),
                    Classname: classname,
                    Imports: imports.ToImmutableArray(),
                    Depends: depends.ToImmutableArray(),
                    TypeEntries: typeEntries.ToImmutableArray(),
                    Designersupported: designersupported.ToImmutableArray(),
                    Typeinfo: typeinfo,
                    Diagnostics: immutableDiagnostics);
            }

            private static int GetColumn(string line)
            {
                for (int index = 0; index < line.Length; index++)
                {
                    if (!char.IsWhiteSpace(line[index]))
                    {
                        return index + 1;
                    }
                }

                return 1;
            }

            private static bool IsTypeEntryKeyword(string keyword)
            {
                return !string.IsNullOrEmpty(keyword)
                    && char.IsUpper(keyword[0]);
            }

            private void ParseInternalTypeEntry(string[] parts, int lineNumber, int column)
            {
                if (parts.Length == 4)
                {
                    typeEntries.Add(CreateTypeEntry(parts[1], parts[2], parts[3], isSingleton: false, isInternal: true));
                    return;
                }

                if (parts.Length == 3)
                {
                    typeEntries.Add(CreateTypeEntry(parts[1], string.Empty, parts[2], isSingleton: false, isInternal: true));
                    return;
                }

                ReportSyntaxError("The 'internal' directive requires a type name and file path, with an optional version.", lineNumber, column);
            }

            private void ParseModuleDirective(string[] parts, int lineNumber, int column)
            {
                if (parts.Length != 2)
                {
                    ReportSyntaxError("The 'module' directive requires exactly one module URI.", lineNumber, column);
                    return;
                }

                module = parts[1];
            }

            private void ParsePluginDirective(string[] parts, int lineNumber, int column)
            {
                if (parts.Length < 2 || parts.Length > 3)
                {
                    ReportSyntaxError("The 'plugin' directive requires a plugin name and an optional path.", lineNumber, column);
                    return;
                }

                plugins.Add(new RawQmldirPlugin(parts[1], parts.Length == 3 ? parts[2] : null));
            }

            private void ParseClassnameDirective(string[] parts, int lineNumber, int column)
            {
                if (parts.Length != 2)
                {
                    ReportSyntaxError("The 'classname' directive requires exactly one class name.", lineNumber, column);
                    return;
                }

                classname = parts[1];
            }

            private void ParseImportDirective(string[] parts, int lineNumber, int column)
            {
                if (parts.Length < 2 || parts.Length > 3)
                {
                    ReportSyntaxError("The 'import' directive requires a module name and an optional version.", lineNumber, column);
                    return;
                }

                imports.Add(new RawQmldirImport(parts[1], parts.Length == 3 ? parts[2] : null));
            }

            private void ParseDependsDirective(string[] parts, int lineNumber, int column)
            {
                if (parts.Length < 2 || parts.Length > 3)
                {
                    ReportSyntaxError("The 'depends' directive requires a module name and an optional version.", lineNumber, column);
                    return;
                }

                depends.Add(new RawQmldirImport(parts[1], parts.Length == 3 ? parts[2] : null));
            }

            private void ParseSingletonDirective(string[] parts, int lineNumber, int column)
            {
                if (parts.Length != 4)
                {
                    ReportSyntaxError("The 'singleton' directive requires a type name, version, and file path.", lineNumber, column);
                    return;
                }

                typeEntries.Add(CreateTypeEntry(parts[1], parts[2], parts[3], isSingleton: true, isInternal: false));
            }

            private void ParseDesignerSupportedDirective(string[] parts, int lineNumber, int column)
            {
                if (parts.Length != 1)
                {
                    ReportSyntaxError("The 'designersupported' directive does not accept additional values.", lineNumber, column);
                    return;
                }

                designersupported.Add("true");
            }

            private void ParseTypeinfoDirective(string[] parts, int lineNumber, int column)
            {
                if (parts.Length != 2)
                {
                    ReportSyntaxError("The 'typeinfo' directive requires exactly one file path.", lineNumber, column);
                    return;
                }

                typeinfo = parts[1];
            }

            private void ParseRegularTypeEntry(string[] parts, int lineNumber, int column)
            {
                if (!IsTypeEntryKeyword(parts[0]))
                {
                    ReportUnknownDirective(parts[0], lineNumber, column);
                    return;
                }

                if (parts.Length != 3)
                {
                    ReportSyntaxError("Type entries require a type name, version, and file path.", lineNumber, column);
                    return;
                }

                typeEntries.Add(CreateTypeEntry(parts[0], parts[1], parts[2], isSingleton: false, isInternal: false));
            }

            private void ParseLine(string line, int lineNumber)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    return;
                }

                int column = GetColumn(line);
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith('#'))
                {
                    return;
                }

                string[] parts = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                string keyword = parts[0];

                switch (keyword)
                {
                    case "module":
                        ParseModuleDirective(parts, lineNumber, column);
                        return;

                    case "plugin":
                        ParsePluginDirective(parts, lineNumber, column);
                        return;

                    case "classname":
                        ParseClassnameDirective(parts, lineNumber, column);
                        return;

                    case "import":
                        ParseImportDirective(parts, lineNumber, column);
                        return;

                    case "depends":
                        ParseDependsDirective(parts, lineNumber, column);
                        return;

                    case "singleton":
                        ParseSingletonDirective(parts, lineNumber, column);
                        return;

                    case "internal":
                        ParseInternalTypeEntry(parts, lineNumber, column);
                        return;

                    case "designersupported":
                        ParseDesignerSupportedDirective(parts, lineNumber, column);
                        return;

                    case "typeinfo":
                        ParseTypeinfoDirective(parts, lineNumber, column);
                        return;
                }

                ParseRegularTypeEntry(parts, lineNumber, column);
            }

            private static string? GetStyleSelector(string filePath)
            {
                if (!filePath.StartsWith('+') || filePath.Length <= 1)
                {
                    return null;
                }

                int separatorIndex = filePath.IndexOfAny(['/', '\\']);
                return separatorIndex > 1
                    ? filePath[1..separatorIndex]
                    : null;
            }

            private RawQmldirTypeEntry CreateTypeEntry(string name, string version, string filePath, bool isSingleton, bool isInternal)
            {
                return new RawQmldirTypeEntry(
                    Name: name,
                    Version: version,
                    FilePath: filePath,
                    IsSingleton: isSingleton,
                    IsInternal: isInternal,
                    StyleSelector: GetStyleSelector(filePath));
            }

            private void ReportSyntaxError(string message, int lineNumber, int column)
            {
                diagnostics.Add(new RegistryDiagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticCodes.QmldirSyntaxError,
                    message,
                    sourcePath,
                    lineNumber,
                    column));
            }

            private void ReportUnknownDirective(string keyword, int lineNumber, int column)
            {
                diagnostics.Add(new RegistryDiagnostic(
                    DiagnosticSeverity.Warning,
                    DiagnosticCodes.QmldirUnknownDirective,
                    $"Unknown qmldir directive '{keyword}'.",
                    sourcePath,
                    lineNumber,
                    column));
            }
        }
    }
}
