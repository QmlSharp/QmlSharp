using System.CommandLine;
using System.CommandLine.Parsing;
using QmlSharp.Build;
using BuildShellCommand = QmlSharp.Build.BuildCommand;
using CleanShellCommand = QmlSharp.Build.CleanCommand;
using DevShellCommand = QmlSharp.Build.DevCommand;
using DoctorShellCommand = QmlSharp.Build.DoctorCommand;
using InitShellCommand = QmlSharp.Build.InitCommand;

namespace QmlSharp.Cli
{
    /// <summary>Root command registration for dotnet qmlsharp.</summary>
    public static class QmlSharpCli
    {
        /// <summary>Creates the root command with all Step 08.03 command shells registered.</summary>
        public static RootCommand CreateRootCommand(
            CliCommandServices services,
            TextWriter output,
            TextWriter error)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(error);

            ICommandOutput commandOutput = new TextWriterCommandOutput(output, error);
            RootCommand root = new("QmlSharp command-line tool.");
            root.Add(CreateBuildCommand(services, commandOutput));
            root.Add(CreateDevCommand(services, commandOutput));
            root.Add(CreateDoctorCommand(services, commandOutput));
            root.Add(CreateInitCommand(services, commandOutput));
            root.Add(CreateCleanCommand(services, commandOutput));
            return root;
        }

        /// <summary>Invokes the CLI using default command-shell services.</summary>
        public static Task<int> InvokeAsync(
            string[] args,
            TextWriter output,
            TextWriter error,
            CancellationToken cancellationToken = default)
        {
            return InvokeAsync(args, CliCommandServices.CreateDefault(), output, error, cancellationToken);
        }

        /// <summary>Invokes the CLI using explicit command-shell services.</summary>
        public static async Task<int> InvokeAsync(
            string[] args,
            CliCommandServices services,
            TextWriter output,
            TextWriter error,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(args);
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(error);

            RootCommand root = CreateRootCommand(services, output, error);
            ParseResult parseResult = root.Parse(args);
            if (parseResult.Errors.Count > 0)
            {
                foreach (ParseError parseError in parseResult.Errors)
                {
                    error.WriteLine(parseError.Message);
                }

                return CliExitCode.ConfigOrCommandError;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                error.WriteLine("Command was cancelled.");
                return CliExitCode.Cancelled;
            }

            InvocationConfiguration configuration = new()
            {
                Output = output,
                Error = error,
                EnableDefaultExceptionHandler = false,
            };
            try
            {
                return await parseResult.InvokeAsync(configuration, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                error.WriteLine("Command was cancelled.");
                return CliExitCode.Cancelled;
            }
        }

        private static Command CreateBuildCommand(CliCommandServices services, ICommandOutput output)
        {
            Option<bool> forceOption = new("--force");
            Option<string> filesOption = new("--files");
            Option<bool> dryRunOption = new("--dry-run");
            Option<bool> jsonOption = new("--json");
            Option<bool> libraryOption = new("--library");
            Option<string> projectDirOption = CreateProjectDirOption();
            Command command = new("build", "Build a QmlSharp project.");
            command.Add(forceOption);
            command.Add(filesOption);
            command.Add(dryRunOption);
            command.Add(jsonOption);
            command.Add(libraryOption);
            command.Add(projectDirOption);
            command.SetAction((parseResult, cancellationToken) =>
            {
                BuildCommandOptions options = new()
                {
                    Force = parseResult.GetValue(forceOption),
                    Files = parseResult.GetValue(filesOption),
                    DryRun = parseResult.GetValue(dryRunOption),
                    Json = parseResult.GetValue(jsonOption),
                    Library = parseResult.GetValue(libraryOption),
                    ProjectDir = parseResult.GetValue(projectDirOption) ?? ".",
                };
                BuildShellCommand shell = new(services.ConfigLoader, services.BuildPipeline, output);
                return shell.ExecuteAsync(options, cancellationToken);
            });
            return command;
        }

        private static Command CreateDevCommand(CliCommandServices services, ICommandOutput output)
        {
            Option<bool> headlessOption = new("--headless");
            Option<string> entryOption = new("--entry");
            Option<string> projectDirOption = CreateProjectDirOption();
            Command command = new("dev", "Start a QmlSharp development session.");
            command.Add(headlessOption);
            command.Add(entryOption);
            command.Add(projectDirOption);
            command.SetAction((parseResult, cancellationToken) =>
            {
                DevCommandOptions options = new()
                {
                    Headless = parseResult.GetValue(headlessOption),
                    Entry = parseResult.GetValue(entryOption),
                    ProjectDir = parseResult.GetValue(projectDirOption) ?? ".",
                };
                DevShellCommand shell = new(services.ConfigLoader, services.DevSession, output);
                return shell.ExecuteAsync(options, cancellationToken);
            });
            return command;
        }

        private static Command CreateDoctorCommand(CliCommandServices services, ICommandOutput output)
        {
            Option<bool> fixOption = new("--fix");
            Option<string> checkOption = new("--check");
            Option<string> projectDirOption = CreateProjectDirOption();
            Command command = new("doctor", "Run QmlSharp environment diagnostics.");
            command.Add(fixOption);
            command.Add(checkOption);
            command.Add(projectDirOption);
            command.SetAction((parseResult, cancellationToken) =>
            {
                string projectDir = parseResult.GetValue(projectDirOption) ?? ".";
                DoctorCommandOptions options = new()
                {
                    Fix = parseResult.GetValue(fixOption),
                    CheckId = parseResult.GetValue(checkOption),
                    ProjectDir = projectDir,
                };
                DoctorShellCommand shell = new(services.CreateDoctor(projectDir), output);
                return shell.ExecuteAsync(options, cancellationToken);
            });
            return command;
        }

        private static Command CreateInitCommand(CliCommandServices services, ICommandOutput output)
        {
            Option<string> templateOption = new("--template");
            Option<string> targetDirOption = new("--target-dir");
            Command command = new("init", "Initialize a QmlSharp project.");
            command.Add(templateOption);
            command.Add(targetDirOption);
            command.SetAction((parseResult, cancellationToken) =>
            {
                InitCommandOptions options = new()
                {
                    Template = parseResult.GetValue(templateOption) ?? "default",
                    TargetDir = parseResult.GetValue(targetDirOption) ?? ".",
                };
                InitShellCommand shell = new(services.InitService, output);
                return shell.ExecuteAsync(options, cancellationToken);
            });
            return command;
        }

        private static Command CreateCleanCommand(CliCommandServices services, ICommandOutput output)
        {
            Option<bool> cacheOption = new("--cache");
            Option<string> projectDirOption = CreateProjectDirOption();
            Command command = new("clean", "Clean QmlSharp build artifacts.");
            command.Add(cacheOption);
            command.Add(projectDirOption);
            command.SetAction((parseResult, cancellationToken) =>
            {
                CleanCommandOptions options = new()
                {
                    Cache = parseResult.GetValue(cacheOption),
                    ProjectDir = parseResult.GetValue(projectDirOption) ?? ".",
                };
                CleanShellCommand shell = new(services.CleanService, output);
                return shell.ExecuteAsync(options, cancellationToken);
            });
            return command;
        }

        private static Option<string> CreateProjectDirOption()
        {
            return new Option<string>("--project-dir", "--project")
            {
                Description = "Path to the QmlSharp project directory.",
            };
        }
    }

}
