using System.Globalization;

namespace QmlSharp.Build
{
    /// <summary>Filesystem-backed implementation of the init command service.</summary>
    public sealed class InitService : IInitService
    {
        private const string AppTemplateDirectoryName = "qmlsharp-app";
        private const string LibraryTemplateDirectoryName = "qmlsharp-library";
        private readonly string _templateRoot;
        private readonly string? _repositoryRoot;

        /// <summary>Create an init service using templates from the current repository or build output.</summary>
        public InitService()
            : this(TemplatePathResolver.ResolveTemplateRoot(), TemplatePathResolver.ResolveRepositoryRoot())
        {
        }

        internal InitService(string templateRoot, string? repositoryRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(templateRoot);

            _templateRoot = Path.GetFullPath(templateRoot);
            _repositoryRoot = string.IsNullOrWhiteSpace(repositoryRoot) ? null : Path.GetFullPath(repositoryRoot);
        }

        /// <inheritdoc />
        public Task<CommandServiceResult> InitAsync(
            InitCommandOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                TemplateSelection selection = SelectTemplate(options.Template);
                string targetDirectory = NormalizeTargetDirectory(options.TargetDir);
                CommandServiceResult? safetyResult = ValidateTargetDirectory(targetDirectory);
                if (safetyResult is not null)
                {
                    return Task.FromResult(safetyResult);
                }

                CommandServiceResult? templateResult = ValidateTemplateDirectory(selection, out string sourceTemplateDirectory);
                if (templateResult is not null)
                {
                    return Task.FromResult(templateResult);
                }

                string projectName = CreateProjectName(targetDirectory);
                TemplateValues values = CreateTemplateValues(projectName, targetDirectory, selection);
                _ = Directory.CreateDirectory(targetDirectory);

                int filesCreated = CopyTemplateFiles(
                    sourceTemplateDirectory,
                    targetDirectory,
                    values,
                    cancellationToken);
                CommandServiceResult result = CommandServiceResult.Succeeded(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Initialized {selection.DisplayName} QmlSharp project in '{targetDirectory}' ({filesCreated} files)."));
                return Task.FromResult(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArgumentException ex)
            {
                BuildDiagnostic diagnostic = CommandDiagnostics.CreateCommandDiagnostic("template", ex.Message);
                return Task.FromResult(CommandServiceResult.Failed(
                    CommandResultStatus.ConfigOrCommandError,
                    diagnostic.Message,
                    ImmutableArray.Create(diagnostic)));
            }
            catch (IOException ex)
            {
                return Task.FromResult(CreateIoFailure("init", ex));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Task.FromResult(CreateIoFailure("init", ex));
            }
        }

        private static string NormalizeTargetDirectory(string targetDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
            return Path.GetFullPath(targetDirectory.Trim());
        }

        private static CommandServiceResult? ValidateTargetDirectory(string targetDirectory)
        {
            if (File.Exists(targetDirectory))
            {
                BuildDiagnostic diagnostic = CommandDiagnostics.CreateCommandDiagnostic(
                    "targetDir",
                    $"Target path '{targetDirectory}' is a file. Choose a directory for qmlsharp init.");
                return CommandServiceResult.Failed(
                    CommandResultStatus.ConfigOrCommandError,
                    diagnostic.Message,
                    ImmutableArray.Create(diagnostic));
            }

            if (Directory.Exists(targetDirectory) &&
                Directory.EnumerateFileSystemEntries(targetDirectory).Any())
            {
                BuildDiagnostic diagnostic = CommandDiagnostics.CreateCommandDiagnostic(
                    "targetDir",
                    $"Target directory '{targetDirectory}' is not empty. qmlsharp init does not overwrite existing files.");
                return CommandServiceResult.Failed(
                    CommandResultStatus.ConfigOrCommandError,
                    diagnostic.Message,
                    ImmutableArray.Create(diagnostic));
            }

            return null;
        }

        private CommandServiceResult? ValidateTemplateDirectory(
            TemplateSelection selection,
            out string sourceTemplateDirectory)
        {
            sourceTemplateDirectory = Path.Join(_templateRoot, selection.DirectoryName);
            if (Directory.Exists(sourceTemplateDirectory))
            {
                return null;
            }

            BuildDiagnostic diagnostic = CommandDiagnostics.CreateCommandDiagnostic(
                "template",
                $"Template '{selection.DisplayName}' was not found under '{_templateRoot}'.");
            return CommandServiceResult.Failed(
                CommandResultStatus.ConfigOrCommandError,
                diagnostic.Message,
                ImmutableArray.Create(diagnostic));
        }

        private static TemplateSelection SelectTemplate(string template)
        {
            string normalizedTemplate = string.IsNullOrWhiteSpace(template)
                ? "default"
                : template.Trim().ToLowerInvariant();
            return normalizedTemplate switch
            {
                "default" or "app" => new TemplateSelection(AppTemplateDirectoryName, "default", false),
                "counter" => new TemplateSelection(AppTemplateDirectoryName, "counter", false),
                "library" => new TemplateSelection(LibraryTemplateDirectoryName, "library", true),
                _ => throw new ArgumentException(
                    $"Unknown QmlSharp template '{template}'. Supported templates are default, counter, and library.",
                    nameof(template)),
            };
        }

        private static string CreateProjectName(string targetDirectory)
        {
            string directoryName = Path.GetFileName(targetDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(directoryName))
            {
                return "MyApp";
            }

            string[] parts = directoryName.Split(
                Path.GetInvalidFileNameChars().Concat([' ', '-', '_', '.']).Distinct().ToArray(),
                StringSplitOptions.RemoveEmptyEntries);
            string candidate = string.Concat(parts.Select(ToIdentifierPart));
            if (candidate.Length == 0)
            {
                return "MyApp";
            }

            if (char.IsDigit(candidate[0]))
            {
                candidate = "App" + candidate;
            }

            return candidate;
        }

        private static string ToIdentifierPart(string part)
        {
            if (part.Length == 0)
            {
                return string.Empty;
            }

            string sanitized = new(part.Where(static character => char.IsLetterOrDigit(character)).ToArray());
            if (sanitized.Length == 0)
            {
                return string.Empty;
            }

            return char.ToUpperInvariant(sanitized[0]) + sanitized[1..];
        }

        private TemplateValues CreateTemplateValues(
            string projectName,
            string targetDirectory,
            TemplateSelection selection)
        {
            string repoRoot = _repositoryRoot is null
                ? string.Empty
                : ToPortablePath(Path.GetRelativePath(targetDirectory, _repositoryRoot));
            if (_repositoryRoot is not null && Path.IsPathRooted(repoRoot))
            {
                repoRoot = ToPortablePath(_repositoryRoot);
            }

            Dictionary<string, string> values = new(StringComparer.Ordinal)
            {
                ["{{ProjectName}}"] = projectName,
                ["{{RootNamespace}}"] = projectName,
                ["{{QmlSharpRepoRoot}}"] = repoRoot,
            };

            if (selection.IsLibrary)
            {
                return new TemplateValues(values);
            }

            AddAppTemplateValues(values, selection.DisplayName);
            return new TemplateValues(values);
        }

        private static void AddAppTemplateValues(Dictionary<string, string> values, string displayName)
        {
            if (string.Equals(displayName, "counter", StringComparison.Ordinal))
            {
                values["{{ViewName}}"] = "CounterView";
                values["{{ViewModelName}}"] = "CounterViewModel";
                values["{{StateType}}"] = "int";
                values["{{StateName}}"] = "Count";
                values["{{StateInitialValue}}"] = "0";
                values["{{StateBindingExpression}}"] = "Vm.Count.toString()";
                values["{{CommandName}}"] = "Increment";
                values["{{CommandNameLower}}"] = "increment";
                values["{{CommandBody}}"] = "        Count++;";
                values["{{ButtonText}}"] = "+";
                return;
            }

            values["{{ViewName}}"] = "AppView";
            values["{{ViewModelName}}"] = "AppViewModel";
            values["{{StateType}}"] = "string";
            values["{{StateName}}"] = "Title";
            values["{{StateInitialValue}}"] = "\"Hello from QmlSharp\"";
            values["{{StateBindingExpression}}"] = "Vm.Title";
            values["{{CommandName}}"] = "Refresh";
            values["{{CommandNameLower}}"] = "refresh";
            values["{{CommandBody}}"] = "        Title = \"Hello from QmlSharp\";";
            values["{{ButtonText}}"] = "Refresh";
        }

        private static int CopyTemplateFiles(
            string sourceTemplateDirectory,
            string targetDirectory,
            TemplateValues values,
            CancellationToken cancellationToken)
        {
            int filesCreated = 0;
            foreach (string sourcePath in Directory
                         .EnumerateFiles(sourceTemplateDirectory, "*", SearchOption.AllDirectories)
                         .OrderBy(static path => path, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relativePath = Path.GetRelativePath(sourceTemplateDirectory, sourcePath);
                string targetRelativePath = values.Apply(relativePath);
                string targetPath = Path.Join(targetDirectory, targetRelativePath);
                string? directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    _ = Directory.CreateDirectory(directory);
                }

                string content = values.Apply(File.ReadAllText(sourcePath));
                File.WriteAllText(targetPath, content);
                filesCreated++;
            }

            return filesCreated;
        }

        private static CommandServiceResult CreateIoFailure(string command, Exception exception)
        {
            BuildDiagnostic diagnostic = new(
                BuildDiagnosticCode.OutputAssemblyFailed,
                BuildDiagnosticSeverity.Error,
                $"{command} failed while writing project files: {exception.Message}",
                BuildPhase.OutputAssembly,
                null);
            return CommandServiceResult.Failed(
                CommandResultStatus.BuildError,
                diagnostic.Message,
                ImmutableArray.Create(diagnostic));
        }

        private static string ToPortablePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private sealed record TemplateSelection(string DirectoryName, string DisplayName, bool IsLibrary);

        private sealed class TemplateValues
        {
            private readonly IReadOnlyDictionary<string, string> _values;

            public TemplateValues(IReadOnlyDictionary<string, string> values)
            {
                _values = values;
            }

            public string Apply(string value)
            {
                string result = value;
                foreach (KeyValuePair<string, string> pair in _values)
                {
                    result = result.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
                }

                return result;
            }
        }

        private static class TemplatePathResolver
        {
            public static string ResolveTemplateRoot()
            {
                foreach (string candidate in EnumerateTemplateRootCandidates())
                {
                    if (Directory.Exists(Path.Join(candidate, AppTemplateDirectoryName)) &&
                        Directory.Exists(Path.Join(candidate, LibraryTemplateDirectoryName)))
                    {
                        return candidate;
                    }
                }

                return Path.Join(AppContext.BaseDirectory, "templates");
            }

            public static string? ResolveRepositoryRoot()
            {
                string? environmentRoot = Environment.GetEnvironmentVariable("QMLSHARP_REPO_ROOT");
                if (!string.IsNullOrWhiteSpace(environmentRoot) &&
                    File.Exists(Path.Join(environmentRoot, "QmlSharp.slnx")))
                {
                    return Path.GetFullPath(environmentRoot);
                }

                foreach (string startDirectory in EnumerateRepositorySearchStarts())
                {
                    DirectoryInfo? directory = new(startDirectory);
                    while (directory is not null)
                    {
                        if (File.Exists(Path.Join(directory.FullName, "QmlSharp.slnx")))
                        {
                            return directory.FullName;
                        }

                        directory = directory.Parent;
                    }
                }

                return null;
            }

            private static IEnumerable<string> EnumerateTemplateRootCandidates()
            {
                yield return Path.Join(AppContext.BaseDirectory, "templates");

                string? repositoryRoot = ResolveRepositoryRoot();
                if (repositoryRoot is not null)
                {
                    yield return Path.Join(repositoryRoot, "templates");
                }
            }

            private static IEnumerable<string> EnumerateRepositorySearchStarts()
            {
                yield return AppContext.BaseDirectory;
                yield return Directory.GetCurrentDirectory();
            }
        }
    }
}
