using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using QmlSharp.Registry;
using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Tools.GenerateRegistrySnapshot
{
    public static class RegistrySnapshotGeneratorCommand
    {
        public static int Run(IReadOnlyList<string> args, TextWriter standardOutput, TextWriter standardError)
        {
            return Run(
                args,
                standardOutput,
                standardError,
                static (qtDir, outputPath, moduleFilter) => new RegistryBuilder().Build(new BuildConfig(
                    QtDir: qtDir,
                    SnapshotPath: outputPath,
                    ForceRebuild: true,
                    ModuleFilter: moduleFilter.IsEmpty ? null : moduleFilter,
                    IncludeInternal: false)));
        }

        internal static int Run(
            IReadOnlyList<string> args,
            TextWriter standardOutput,
            TextWriter standardError,
            Func<string, string, ImmutableArray<string>, BuildResult> buildSnapshot)
        {
            ArgumentNullException.ThrowIfNull(args);
            ArgumentNullException.ThrowIfNull(standardOutput);
            ArgumentNullException.ThrowIfNull(standardError);
            ArgumentNullException.ThrowIfNull(buildSnapshot);

            ParseArgumentsResult parseResult = ParseArguments(args, standardOutput, standardError);
            if (parseResult.ExitCode.HasValue)
            {
                return parseResult.ExitCode.Value;
            }

            GenerateRegistrySnapshotOptions options = parseResult.Options!;
            string outputPath;

            BuildResult result;
            try
            {
                outputPath = Path.GetFullPath(options.OutputPath);
                result = buildSnapshot(options.QtDir, outputPath, options.ModuleFilter);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                standardError.WriteLine($"Failed to generate registry snapshot: {exception.Message}");
                return 1;
            }

            int warningCount = result.Diagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);
            int errorCount = result.Diagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

            if (!result.IsSuccess || result.TypeRegistry is null)
            {
                TryDeleteFile(outputPath, standardError);

                foreach (RegistryDiagnostic diagnostic in result.Diagnostics
                    .OrderByDescending(diagnostic => diagnostic.Severity)
                    .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal))
                {
                    standardError.WriteLine($"{diagnostic.Severity}: {diagnostic.Code}: {diagnostic.Message}");
                }

                standardError.WriteLine($"Warning count: {warningCount}");
                standardError.WriteLine($"Error count: {errorCount}");
                return 1;
            }

            ITypeRegistry typeRegistry = result.TypeRegistry;
            standardOutput.WriteLine($"Qt version: {typeRegistry.QtVersion}");
            standardOutput.WriteLine($"Registry format version: {typeRegistry.FormatVersion}");
            standardOutput.WriteLine($"Module count: {typeRegistry.Modules.Count}");
            standardOutput.WriteLine($"Type count: {typeRegistry.Types.Count}");
            standardOutput.WriteLine($"Warning count: {warningCount}");
            standardOutput.WriteLine($"Error count: {errorCount}");
            standardOutput.WriteLine($"Output: {outputPath}");

            return 0;
        }

        [SuppressMessage("Maintainability", "MA0051:Method is too long", Justification = "The command-line parser keeps the supported CLI contract in one deterministic place.")]
        private static ParseArgumentsResult ParseArguments(
            IReadOnlyList<string> args,
            TextWriter standardOutput,
            TextWriter standardError)
        {
            string? qtDir = null;
            string? outputPath = null;
            ImmutableArray<string>.Builder moduleFilters = ImmutableArray.CreateBuilder<string>();

            for (int index = 0; index < args.Count; index++)
            {
                string argument = args[index];

                if (string.Equals(argument, "--help", StringComparison.Ordinal)
                    || string.Equals(argument, "-h", StringComparison.Ordinal))
                {
                    WriteUsage(standardOutput);
                    return new ParseArgumentsResult(ExitCode: 0, Options: null);
                }

                if (TryReadOptionValue(argument, "--qt-dir", args, ref index, out string? qtDirValue, out string? parseError))
                {
                    if (parseError is not null)
                    {
                        standardError.WriteLine(parseError);
                        WriteUsage(standardError);
                        return new ParseArgumentsResult(ExitCode: 1, Options: null);
                    }

                    qtDir = qtDirValue;
                    continue;
                }

                if (TryReadOptionValue(argument, "--output", args, ref index, out string? outputValue, out parseError))
                {
                    if (parseError is not null)
                    {
                        standardError.WriteLine(parseError);
                        WriteUsage(standardError);
                        return new ParseArgumentsResult(ExitCode: 1, Options: null);
                    }

                    outputPath = outputValue;
                    continue;
                }

                if (TryReadOptionValue(argument, "--module-filter", args, ref index, out string? moduleFilterValue, out parseError))
                {
                    if (parseError is not null)
                    {
                        standardError.WriteLine(parseError);
                        WriteUsage(standardError);
                        return new ParseArgumentsResult(ExitCode: 1, Options: null);
                    }

                    foreach (string filter in moduleFilterValue!
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(filter => !string.IsNullOrWhiteSpace(filter)))
                    {
                        moduleFilters.Add(filter);
                    }

                    continue;
                }

                standardError.WriteLine($"Unknown argument '{argument}'.");
                WriteUsage(standardError);
                return new ParseArgumentsResult(ExitCode: 1, Options: null);
            }

            if (string.IsNullOrWhiteSpace(qtDir))
            {
                standardError.WriteLine("Missing required --qt-dir option.");
                WriteUsage(standardError);
                return new ParseArgumentsResult(ExitCode: 1, Options: null);
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                standardError.WriteLine("Missing required --output option.");
                WriteUsage(standardError);
                return new ParseArgumentsResult(ExitCode: 1, Options: null);
            }

            ImmutableArray<string> distinctFilters = moduleFilters
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray();

            return new ParseArgumentsResult(
                ExitCode: null,
                Options: new GenerateRegistrySnapshotOptions(qtDir, outputPath, distinctFilters));
        }

        private static bool TryReadOptionValue(
            string argument,
            string optionName,
            IReadOnlyList<string> args,
            ref int index,
            out string? value,
            out string? error)
        {
            string inlinePrefix = optionName + "=";
            if (argument.StartsWith(inlinePrefix, StringComparison.Ordinal))
            {
                value = argument[inlinePrefix.Length..];
                error = string.IsNullOrWhiteSpace(value)
                    ? $"The {optionName} option requires a non-empty value."
                    : null;
                return true;
            }

            if (!string.Equals(argument, optionName, StringComparison.Ordinal))
            {
                value = null;
                error = null;
                return false;
            }

            if (index + 1 >= args.Count)
            {
                value = null;
                error = $"The {optionName} option requires a value.";
                return true;
            }

            index++;
            value = args[index];
            error = string.IsNullOrWhiteSpace(value)
                ? $"The {optionName} option requires a non-empty value."
                : null;
            return true;
        }

        private static void TryDeleteFile(string filePath, TextWriter standardError)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (IOException exception)
            {
                standardError.WriteLine($"Warning: Failed to delete file '{filePath}': {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                standardError.WriteLine($"Warning: Failed to delete file '{filePath}': {exception.Message}");
            }
        }

        private static void WriteUsage(TextWriter writer)
        {
            writer.WriteLine("Usage:");
            writer.WriteLine("  dotnet run --project tools/GenerateRegistrySnapshot -- --qt-dir <path> --output <file> [--module-filter <uri[,uri...]>]");
            writer.WriteLine();
            writer.WriteLine("Options:");
            writer.WriteLine("  --qt-dir         Absolute path to the Qt SDK root.");
            writer.WriteLine("  --output         Path to the registry snapshot file to write.");
            writer.WriteLine("  --module-filter  Optional module URI filter. Repeat the option or pass a comma-separated list.");
        }

        private sealed record GenerateRegistrySnapshotOptions(
            string QtDir,
            string OutputPath,
            ImmutableArray<string> ModuleFilter);

        private sealed record ParseArgumentsResult(
            int? ExitCode,
            GenerateRegistrySnapshotOptions? Options);
    }
}
