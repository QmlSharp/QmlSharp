using System.Text;
using QmlSharp.Compiler;
using CompilerQmlVersion = QmlSharp.Compiler.QmlVersion;

namespace QmlSharp.Build
{
    internal sealed class ModuleMetadataBuildStage : IBuildStage
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly IQmldirGenerator qmldirGenerator;
        private readonly IQmltypesGenerator qmltypesGenerator;
        private readonly IViewModelSchemaSerializer schemaSerializer;

        public ModuleMetadataBuildStage()
            : this(new QmldirGenerator(), new QmltypesGenerator(), new ViewModelSchemaSerializer())
        {
        }

        public ModuleMetadataBuildStage(
            IQmldirGenerator qmldirGenerator,
            IQmltypesGenerator qmltypesGenerator,
            IViewModelSchemaSerializer schemaSerializer)
        {
            this.qmldirGenerator = qmldirGenerator;
            this.qmltypesGenerator = qmltypesGenerator;
            this.schemaSerializer = schemaSerializer;
        }

        public BuildPhase Phase => BuildPhase.ModuleMetadata;

        public async Task<BuildStageResult> ExecuteAsync(
            BuildContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();

            string schemaRoot = Path.Combine(context.OutputDir, "schemas");
            if (!Directory.Exists(schemaRoot) || context.DryRun)
            {
                return BuildStageResult.Succeeded();
            }

            ImmutableArray<SchemaFile> schemas;
            try
            {
                schemas = LoadSchemas(schemaRoot);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return Failed(BuildDiagnosticCode.QmltypesGenerationFailed, schemaRoot, exception);
            }

            foreach (IGrouping<string, SchemaFile> moduleGroup in schemas
                .GroupBy(static schema => schema.Schema.ModuleUri, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal))
            {
                BuildStageResult? moduleResult = await ProcessModuleAsync(
                        context,
                        moduleGroup,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (moduleResult is not null)
                {
                    return moduleResult;
                }
            }

            return BuildStageResult.Succeeded();
        }

        private async Task<BuildStageResult?> ProcessModuleAsync(
            BuildContext context,
            IGrouping<string, SchemaFile> moduleGroup,
            CancellationToken cancellationToken)
        {
            ImmutableArray<ViewModelSchema> moduleSchemas = moduleGroup
                .Select(static schema => schema.Schema)
                .OrderBy(static schema => schema.ClassName, StringComparer.Ordinal)
                .ThenBy(static schema => schema.CompilerSlotKey, StringComparer.Ordinal)
                .ToImmutableArray();
            string moduleUri = moduleGroup.Key;
            QmlVersion moduleVersion = ResolveVersion(moduleSchemas, context.Config.Module.Version);
            string moduleDirectory = ModuleMetadataPaths.GetModuleDirectory(context.OutputDir, moduleUri);
            ImmutableArray<string> qmlFiles = DiscoverQmlFiles(moduleDirectory);
            string qmldirPath = Path.Combine(moduleDirectory, "qmldir");
            string qmltypesPath = Path.Combine(moduleDirectory, ModuleMetadataPaths.GetQmltypesFileName(moduleUri));

            string qmldirContent;
            try
            {
                qmldirContent = GenerateQmldir(moduleUri, moduleVersion, moduleSchemas, qmlFiles);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return Failed(BuildDiagnosticCode.QmldirGenerationFailed, qmldirPath, exception);
            }

            string qmltypesContent;
            try
            {
                qmltypesContent = qmltypesGenerator.Generate(moduleUri, moduleVersion, moduleSchemas);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return Failed(BuildDiagnosticCode.QmltypesGenerationFailed, qmltypesPath, exception);
            }

            return await WriteModuleFilesAsync(
                    moduleDirectory,
                    qmldirPath,
                    qmltypesPath,
                    qmldirContent,
                    qmltypesContent,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private string GenerateQmldir(
            string moduleUri,
            QmlVersion moduleVersion,
            ImmutableArray<ViewModelSchema> moduleSchemas,
            ImmutableArray<string> qmlFiles)
        {
            return qmldirGenerator is QmldirGenerator generator
                ? generator.Generate(moduleUri, moduleVersion, moduleSchemas, qmlFiles)
                : qmldirGenerator.Generate(moduleUri, moduleVersion, moduleSchemas);
        }

        private static async Task<BuildStageResult?> WriteModuleFilesAsync(
            string moduleDirectory,
            string qmldirPath,
            string qmltypesPath,
            string qmldirContent,
            string qmltypesContent,
            CancellationToken cancellationToken)
        {
            try
            {
                _ = Directory.CreateDirectory(moduleDirectory);
                await File.WriteAllTextAsync(qmldirPath, qmldirContent, Utf8NoBom, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return Failed(BuildDiagnosticCode.QmldirGenerationFailed, qmldirPath, exception);
            }

            try
            {
                await File.WriteAllTextAsync(qmltypesPath, qmltypesContent, Utf8NoBom, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return Failed(BuildDiagnosticCode.QmltypesGenerationFailed, qmltypesPath, exception);
            }

            return null;
        }

        private ImmutableArray<SchemaFile> LoadSchemas(string schemaRoot)
        {
            ImmutableArray<SchemaFile>.Builder builder = ImmutableArray.CreateBuilder<SchemaFile>();
            foreach (string schemaPath in Directory
                .EnumerateFiles(schemaRoot, "*.schema.json", SearchOption.AllDirectories)
                .OrderBy(static path => path, StringComparer.Ordinal))
            {
                string json = File.ReadAllText(schemaPath, Utf8NoBom);
                ViewModelSchema schema = schemaSerializer.Deserialize(json);
                builder.Add(new SchemaFile(schemaPath, schema));
            }

            return builder.ToImmutable();
        }

        private static ImmutableArray<string> DiscoverQmlFiles(string moduleDirectory)
        {
            if (!Directory.Exists(moduleDirectory))
            {
                return ImmutableArray<string>.Empty;
            }

            return Directory
                .EnumerateFiles(moduleDirectory, "*.qml", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => Path.GetFileName(path), StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static QmlVersion ResolveVersion(
            ImmutableArray<ViewModelSchema> schemas,
            QmlVersion fallbackVersion)
        {
            if (schemas.IsDefaultOrEmpty)
            {
                return fallbackVersion;
            }

            CompilerQmlVersion version = schemas[0].ModuleVersion;
            return new QmlVersion(version.Major, version.Minor);
        }

        private static BuildStageResult Failed(string code, string filePath, Exception exception)
        {
            BuildDiagnostic diagnostic = new(
                code,
                BuildDiagnosticSeverity.Error,
                $"Module metadata generation failed: {exception.Message}",
                BuildPhase.ModuleMetadata,
                filePath);
            return BuildStageResult.Failed(diagnostic);
        }

        private sealed record SchemaFile(string Path, ViewModelSchema Schema);
    }
}
