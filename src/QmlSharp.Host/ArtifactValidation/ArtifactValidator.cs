using System.Globalization;
using System.Text.Json;
using QmlSharp.Host.Interop;

namespace QmlSharp.Host.ArtifactValidation
{
    /// <summary>Default validator for canonical QmlSharp runtime artifact directories.</summary>
    public sealed class ArtifactValidator : IArtifactValidator
    {
        private const string CurrentSchemaVersion = "1.0";
        private readonly IArtifactAbiVersionReader abiVersionReader;

        public ArtifactValidator()
            : this(new NativeArtifactAbiVersionReader())
        {
        }

        public ArtifactValidator(IArtifactAbiVersionReader abiVersionReader)
        {
            ArgumentNullException.ThrowIfNull(abiVersionReader);
            this.abiVersionReader = abiVersionReader;
        }

        public ArtifactValidationResult Validate(string distDirectory)
        {
            return Validate(new ArtifactValidationRequest(distDirectory));
        }

        public ArtifactValidationResult Validate(ArtifactValidationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.DistDirectory))
            {
                throw new ArgumentException("A dist directory is required.", nameof(request));
            }

            List<ArtifactDiagnostic> diagnostics = [];
            string distDirectory = Path.GetFullPath(request.DistDirectory);
            string manifestPath = Path.Join(distDirectory, "manifest.json");
            ProductManifest? manifest = TryReadManifest(manifestPath, diagnostics);
            if (manifest is null)
            {
                return CreateResult(diagnostics);
            }

            ValidateNativeLibrary(distDirectory, manifest, diagnostics);
            ValidateManagedAssembly(distDirectory, manifest, diagnostics);
            IReadOnlyList<SchemaInfo> schemas = ReadSchemas(distDirectory, diagnostics);
            ValidateDeclaredSchemas(distDirectory, manifest, schemas, diagnostics);
            ValidateQmlImportPath(distDirectory, diagnostics);
            ValidateRootQmlFile(distDirectory, request.RootQmlFilePath, diagnostics);
            EventBindingsInfo? eventBindings = TryReadEventBindings(distDirectory, diagnostics);
            if (eventBindings is not null)
            {
                ValidateEventBindings(eventBindings, schemas, diagnostics);
            }

            return CreateResult(diagnostics);
        }

        public ArtifactValidationResult ValidateSchemaRegistrationResult(string schemaFilePath, int nativeResultCode, string? nativeError)
        {
            if (nativeResultCode == 0)
            {
                return new ArtifactValidationResult(IsValid: true, Array.Empty<ArtifactDiagnostic>());
            }

            string message = string.IsNullOrWhiteSpace(nativeError)
                ? string.Format(CultureInfo.InvariantCulture, "Schema registration failed with native error code {0}.", nativeResultCode)
                : string.Format(CultureInfo.InvariantCulture, "Schema registration failed with native error code {0}: {1}", nativeResultCode, nativeError);

            ArtifactDiagnostic diagnostic = new(
                ArtifactDiagnosticSeverity.Error,
                ArtifactValidationCodes.SchemaRegistrationFailed,
                message,
                string.IsNullOrWhiteSpace(schemaFilePath) ? null : Path.GetFullPath(schemaFilePath));
            return new ArtifactValidationResult(IsValid: false, new[] { diagnostic });
        }

        private static ArtifactValidationResult CreateResult(IReadOnlyList<ArtifactDiagnostic> diagnostics)
        {
            bool isValid = diagnostics.All(static diagnostic => diagnostic.Severity != ArtifactDiagnosticSeverity.Error);
            return new ArtifactValidationResult(isValid, diagnostics.ToArray());
        }

        private ProductManifest? TryReadManifest(string manifestPath, List<ArtifactDiagnostic> diagnostics)
        {
            if (!File.Exists(manifestPath))
            {
                AddDiagnostic(
                    diagnostics,
                    ArtifactDiagnosticSeverity.Error,
                    ArtifactValidationCodes.ManifestMissing,
                    "manifest.json was not found.",
                    manifestPath);
                return null;
            }

            try
            {
                using FileStream stream = File.OpenRead(manifestPath);
                using JsonDocument document = JsonDocument.Parse(stream);
                JsonElement root = RequireObject(document.RootElement, "manifest root");
                return new ProductManifest(
                    ReadRequiredString(root, "nativeLib"),
                    ReadRequiredString(root, "managedAssembly"),
                    ReadRequiredStringArray(root, "viewModels"));
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or IOException or UnauthorizedAccessException)
            {
                AddDiagnostic(
                    diagnostics,
                    ArtifactDiagnosticSeverity.Error,
                    ArtifactValidationCodes.ManifestMissing,
                    "manifest.json is missing required startup fields or is not valid JSON: " + exception.Message,
                    manifestPath);
                return null;
            }
        }

        private void ValidateNativeLibrary(string distDirectory, ProductManifest manifest, List<ArtifactDiagnostic> diagnostics)
        {
            string nativeLibraryPath = ResolveDistPath(distDirectory, manifest.NativeLib);
            if (!File.Exists(nativeLibraryPath))
            {
                AddDiagnostic(
                    diagnostics,
                    ArtifactDiagnosticSeverity.Error,
                    ArtifactValidationCodes.NativeLibraryMissing,
                    "The native library referenced by manifest.nativeLib was not found.",
                    nativeLibraryPath);
                return;
            }

            try
            {
                int actualVersion = abiVersionReader.ReadAbiVersion(nativeLibraryPath);
                if (actualVersion != NativeHostAbi.SupportedAbiVersion)
                {
                    AddDiagnostic(
                        diagnostics,
                        ArtifactDiagnosticSeverity.Error,
                        ArtifactValidationCodes.AbiVersionMismatch,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "The native ABI version is {0}; QmlSharp.Host requires {1}.",
                            actualVersion,
                            NativeHostAbi.SupportedAbiVersion),
                        nativeLibraryPath);
                }
            }
            catch (Exception exception) when (exception is BadImageFormatException or DllNotFoundException or EntryPointNotFoundException or FileLoadException or InvalidOperationException)
            {
                AddDiagnostic(
                    diagnostics,
                    ArtifactDiagnosticSeverity.Error,
                    ArtifactValidationCodes.AbiVersionMismatch,
                    "The native ABI version could not be read: " + exception.Message,
                    nativeLibraryPath);
            }
        }

        private static void ValidateManagedAssembly(string distDirectory, ProductManifest manifest, List<ArtifactDiagnostic> diagnostics)
        {
            string managedAssemblyPath = ResolveDistPath(distDirectory, manifest.ManagedAssembly);
            if (!File.Exists(managedAssemblyPath))
            {
                AddDiagnostic(
                    diagnostics,
                    ArtifactDiagnosticSeverity.Error,
                    ArtifactValidationCodes.ManifestMissing,
                    "The managed assembly referenced by manifest.managedAssembly was not found.",
                    managedAssemblyPath);
            }
        }

        private static IReadOnlyList<SchemaInfo> ReadSchemas(string distDirectory, List<ArtifactDiagnostic> diagnostics)
        {
            string schemasDirectory = Path.Join(distDirectory, "schemas");
            if (!Directory.Exists(schemasDirectory))
            {
                AddDiagnostic(
                    diagnostics,
                    ArtifactDiagnosticSeverity.Error,
                    ArtifactValidationCodes.SchemaMissing,
                    "The canonical schemas directory was not found.",
                    schemasDirectory);
                return Array.Empty<SchemaInfo>();
            }

            string[] schemaFiles = Directory.GetFiles(schemasDirectory, "*.schema.json", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (schemaFiles.Length == 0)
            {
                AddDiagnostic(
                    diagnostics,
                    ArtifactDiagnosticSeverity.Error,
                    ArtifactValidationCodes.SchemaMissing,
                    "The canonical schemas directory contains no .schema.json files.",
                    schemasDirectory);
                return Array.Empty<SchemaInfo>();
            }

            List<SchemaInfo> schemas = [];
            foreach (string schemaFile in schemaFiles)
            {
                SchemaInfo? schema = TryReadSchema(schemaFile, diagnostics);
                if (schema is not null)
                {
                    schemas.Add(schema);
                }
            }

            foreach (IGrouping<string, SchemaInfo> duplicateGroup in schemas.GroupBy(static schema => schema.ClassName, StringComparer.Ordinal)
                         .Where(static group => group.Count() > 1))
            {
                string files = string.Join(", ", duplicateGroup.Select(static schema => schema.FilePath));
                AddDiagnostic(
                    diagnostics,
                    ArtifactDiagnosticSeverity.Error,
                    ArtifactValidationCodes.SchemaMissing,
                    string.Format(CultureInfo.InvariantCulture, "Duplicate schema id/className '{0}' was found in: {1}", duplicateGroup.Key, files),
                    duplicateGroup.First().FilePath);
            }

            return schemas;
        }

        private static SchemaInfo? TryReadSchema(string schemaFile, List<ArtifactDiagnostic> diagnostics)
        {
            try
            {
                using FileStream stream = File.OpenRead(schemaFile);
                using JsonDocument document = JsonDocument.Parse(stream);
                JsonElement root = RequireObject(document.RootElement, "schema root");
                string schemaVersion = ReadRequiredString(root, "schemaVersion");
                if (!StringComparer.Ordinal.Equals(schemaVersion, CurrentSchemaVersion))
                {
                    throw new JsonException(string.Format(CultureInfo.InvariantCulture, "Unsupported schemaVersion '{0}'. Expected '{1}'.", schemaVersion, CurrentSchemaVersion));
                }

                string className = ReadRequiredString(root, "className");
                _ = ReadRequiredString(root, "moduleName");
                _ = ReadRequiredString(root, "moduleUri");
                JsonElement moduleVersion = RequireObject(root.GetProperty("moduleVersion"), "moduleVersion");
                _ = moduleVersion.GetProperty("major").GetInt32();
                _ = moduleVersion.GetProperty("minor").GetInt32();
                string compilerSlotKey = ReadRequiredString(root, "compilerSlotKey");
                if (!compilerSlotKey.Contains("::__qmlsharp_vm", StringComparison.Ordinal))
                {
                    throw new JsonException("compilerSlotKey must use the '{ViewName}::__qmlsharp_vm{index}' format.");
                }

                ValidateProperties(root.GetProperty("properties"));
                IReadOnlyList<SchemaCommandInfo> commands = ReadCommands(root.GetProperty("commands"));
                ValidateEffects(root.GetProperty("effects"));
                ValidateLifecycle(root.GetProperty("lifecycle"));

                return new SchemaInfo(schemaFile, className, commands);
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or IOException or UnauthorizedAccessException)
            {
                AddDiagnostic(
                    diagnostics,
                    ArtifactDiagnosticSeverity.Error,
                    ArtifactValidationCodes.SchemaMissing,
                    "Schema JSON is missing required fields or is not valid JSON: " + exception.Message,
                    schemaFile);
                return null;
            }
        }

        private static void ValidateDeclaredSchemas(string distDirectory, ProductManifest manifest, IReadOnlyList<SchemaInfo> schemas, List<ArtifactDiagnostic> diagnostics)
        {
            HashSet<string> schemaClassNames = schemas.Select(static schema => schema.ClassName).ToHashSet(StringComparer.Ordinal);
            foreach (string viewModel in manifest.ViewModels)
            {
                if (!schemaClassNames.Contains(viewModel))
                {
                    AddDiagnostic(
                        diagnostics,
                        ArtifactDiagnosticSeverity.Error,
                        ArtifactValidationCodes.SchemaMissing,
                        string.Format(CultureInfo.InvariantCulture, "Schema for manifest-declared ViewModel '{0}' was not found.", viewModel),
                        Path.Join(distDirectory, "schemas", viewModel + ".schema.json"));
                }
            }
        }

        private static void ValidateQmlImportPath(string distDirectory, List<ArtifactDiagnostic> diagnostics)
        {
            string qmlDirectory = Path.Join(distDirectory, "qml");
            if (!Directory.Exists(qmlDirectory))
            {
                AddDiagnostic(
                    diagnostics,
                    ArtifactDiagnosticSeverity.Error,
                    ArtifactValidationCodes.SchemaMissing,
                    "The canonical QML import directory was not found.",
                    qmlDirectory);
                return;
            }

            bool hasQmldir = Directory.EnumerateFiles(qmlDirectory, "qmldir", SearchOption.AllDirectories).Any();
            if (!hasQmldir)
            {
                AddDiagnostic(
                    diagnostics,
                    ArtifactDiagnosticSeverity.Error,
                    ArtifactValidationCodes.SchemaMissing,
                    "The QML import directory does not contain a qmldir file.",
                    qmlDirectory);
            }
        }

        private static void ValidateRootQmlFile(string distDirectory, string? rootQmlFilePath, List<ArtifactDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(rootQmlFilePath))
            {
                return;
            }

            string resolvedRootQmlPath = Path.IsPathRooted(rootQmlFilePath)
                ? Path.GetFullPath(rootQmlFilePath)
                : Path.GetFullPath(Path.Join(distDirectory, rootQmlFilePath));
            if (!File.Exists(resolvedRootQmlPath))
            {
                AddDiagnostic(
                    diagnostics,
                    ArtifactDiagnosticSeverity.Error,
                    ArtifactValidationCodes.SchemaMissing,
                    "The caller-provided root QML file was not found.",
                    resolvedRootQmlPath);
            }
        }

        private static EventBindingsInfo? TryReadEventBindings(string distDirectory, List<ArtifactDiagnostic> diagnostics)
        {
            string eventBindingsPath = Path.Join(distDirectory, "event-bindings.json");
            if (!File.Exists(eventBindingsPath))
            {
                AddDiagnostic(
                    diagnostics,
                    ArtifactDiagnosticSeverity.Warning,
                    ArtifactValidationCodes.EventBindingsMissing,
                    "event-bindings.json was not found; command/effect validation is skipped.",
                    eventBindingsPath);
                return null;
            }

            try
            {
                using FileStream stream = File.OpenRead(eventBindingsPath);
                using JsonDocument document = JsonDocument.Parse(stream);
                JsonElement root = RequireObject(document.RootElement, "event-bindings root");
                string schemaVersion = ReadRequiredString(root, "schemaVersion");
                if (!StringComparer.Ordinal.Equals(schemaVersion, CurrentSchemaVersion))
                {
                    throw new JsonException(string.Format(CultureInfo.InvariantCulture, "Unsupported schemaVersion '{0}'. Expected '{1}'.", schemaVersion, CurrentSchemaVersion));
                }

                IReadOnlyList<EventCommandInfo> commands = ReadEventCommands(root.GetProperty("commands"));
                ValidateEventEffects(root.GetProperty("effects"));
                return new EventBindingsInfo(eventBindingsPath, commands);
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or IOException or UnauthorizedAccessException)
            {
                AddDiagnostic(
                    diagnostics,
                    ArtifactDiagnosticSeverity.Warning,
                    ArtifactValidationCodes.EventBindingsMissing,
                    "event-bindings.json is invalid; command/effect validation is skipped: " + exception.Message,
                    eventBindingsPath);
                return null;
            }
        }

        private static void ValidateEventBindings(EventBindingsInfo eventBindings, IReadOnlyList<SchemaInfo> schemas, List<ArtifactDiagnostic> diagnostics)
        {
            HashSet<SchemaCommandInfo> schemaCommands = schemas
                .SelectMany(static schema => schema.Commands.Select(command => new SchemaCommandInfo(schema.ClassName, command.Name, command.CommandId)))
                .ToHashSet();

            foreach (EventCommandInfo command in eventBindings.Commands)
            {
                SchemaCommandInfo schemaCommand = new(command.ViewModelClass, command.CommandName, command.CommandId);
                if (!schemaCommands.Contains(schemaCommand))
                {
                    AddDiagnostic(
                        diagnostics,
                        ArtifactDiagnosticSeverity.Warning,
                        ArtifactValidationCodes.EventBindingCommandMissing,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "event-bindings commandId {0} for {1}.{2} has no matching schema command.",
                            command.CommandId,
                            command.ViewModelClass,
                            command.CommandName),
                        eventBindings.FilePath);
                }
            }
        }

        private static void ValidateProperties(JsonElement properties)
        {
            RequireArray(properties, "properties");
            foreach (JsonElement property in properties.EnumerateArray())
            {
                _ = ReadRequiredString(property, "name");
                _ = ReadRequiredString(property, "type");
                if (property.TryGetProperty("defaultValue", out JsonElement defaultValue) && defaultValue.ValueKind != JsonValueKind.String)
                {
                    throw new JsonException("Property defaultValue must be a string when present.");
                }

                _ = property.GetProperty("readOnly").GetBoolean();
                _ = property.GetProperty("memberId").GetInt32();
            }
        }

        private static IReadOnlyList<SchemaCommandInfo> ReadCommands(JsonElement commands)
        {
            RequireArray(commands, "commands");
            List<SchemaCommandInfo> result = [];
            foreach (JsonElement command in commands.EnumerateArray())
            {
                string name = ReadRequiredString(command, "name");
                RequireArray(command.GetProperty("parameters"), "parameters");
                _ = command.GetProperty("commandId").GetInt32();
                result.Add(new SchemaCommandInfo(string.Empty, name, command.GetProperty("commandId").GetInt32()));
            }

            return result;
        }

        private static void ValidateEffects(JsonElement effects)
        {
            RequireArray(effects, "effects");
            foreach (JsonElement effect in effects.EnumerateArray())
            {
                _ = ReadRequiredString(effect, "name");
                _ = ReadRequiredString(effect, "payloadType");
                _ = effect.GetProperty("effectId").GetInt32();
            }
        }

        private static void ValidateLifecycle(JsonElement lifecycle)
        {
            JsonElement lifecycleObject = RequireObject(lifecycle, "lifecycle");
            _ = lifecycleObject.GetProperty("onMounted").GetBoolean();
            _ = lifecycleObject.GetProperty("onUnmounting").GetBoolean();
            _ = lifecycleObject.GetProperty("hotReload").GetBoolean();
        }

        private static IReadOnlyList<EventCommandInfo> ReadEventCommands(JsonElement commands)
        {
            RequireArray(commands, "commands");
            List<EventCommandInfo> result = [];
            foreach (JsonElement command in commands.EnumerateArray())
            {
                string viewModelClass = ReadRequiredString(command, "viewModelClass");
                string commandName = ReadRequiredString(command, "commandName");
                int commandId = command.GetProperty("commandId").GetInt32();
                RequireArray(command.GetProperty("parameterTypes"), "parameterTypes");
                foreach (JsonElement parameterType in command.GetProperty("parameterTypes").EnumerateArray())
                {
                    if (parameterType.ValueKind != JsonValueKind.String)
                    {
                        throw new JsonException("parameterTypes entries must be strings.");
                    }
                }

                result.Add(new EventCommandInfo(viewModelClass, commandName, commandId));
            }

            return result;
        }

        private static void ValidateEventEffects(JsonElement effects)
        {
            RequireArray(effects, "effects");
            foreach (JsonElement effect in effects.EnumerateArray())
            {
                _ = ReadRequiredString(effect, "viewModelClass");
                _ = ReadRequiredString(effect, "effectName");
                _ = effect.GetProperty("effectId").GetInt32();
                _ = ReadRequiredString(effect, "payloadType");
            }
        }

        private static JsonElement RequireObject(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException(name + " must be a JSON object.");
            }

            return element;
        }

        private static void RequireArray(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException(name + " must be a JSON array.");
            }
        }

        private static string ReadRequiredString(JsonElement element, string propertyName)
        {
            string? value = element.GetProperty(propertyName).GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new JsonException(string.Format(CultureInfo.InvariantCulture, "Property '{0}' must be a non-empty string.", propertyName));
            }

            return value;
        }

        private static IReadOnlyList<string> ReadRequiredStringArray(JsonElement element, string propertyName)
        {
            JsonElement arrayElement = element.GetProperty(propertyName);
            RequireArray(arrayElement, propertyName);
            List<string> values = [];
            foreach (JsonElement item in arrayElement.EnumerateArray())
            {
                string? value = item.GetString();
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new JsonException(string.Format(CultureInfo.InvariantCulture, "Entries in '{0}' must be non-empty strings.", propertyName));
                }

                values.Add(value);
            }

            return values;
        }

        private static string ResolveDistPath(string distDirectory, string relativePath)
        {
            return Path.IsPathRooted(relativePath)
                ? Path.GetFullPath(relativePath)
                : Path.GetFullPath(Path.Join(distDirectory, relativePath));
        }

        private static void AddDiagnostic(
            List<ArtifactDiagnostic> diagnostics,
            ArtifactDiagnosticSeverity severity,
            string code,
            string message,
            string? filePath)
        {
            diagnostics.Add(new ArtifactDiagnostic(
                severity,
                code,
                message,
                string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFullPath(filePath)));
        }

        private sealed record ProductManifest(string NativeLib, string ManagedAssembly, IReadOnlyList<string> ViewModels);

        private sealed record SchemaInfo(string FilePath, string ClassName, IReadOnlyList<SchemaCommandInfo> Commands);

        private sealed record SchemaCommandInfo(string ViewModelClass, string Name, int CommandId);

        private sealed record EventBindingsInfo(string FilePath, IReadOnlyList<EventCommandInfo> Commands);

        private sealed record EventCommandInfo(string ViewModelClass, string CommandName, int CommandId);
    }
}
