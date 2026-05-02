using System.Text;

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Writes compiler artifacts to the canonical output layout.
    /// </summary>
    public sealed class CompilerOutputWriter
    {
        private const string PhaseName = "WritingArtifacts";
        private const string EventBindingsFileName = "event-bindings.json";

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        private readonly IViewModelSchemaSerializer schemaSerializer;
        private readonly IEventBindingsBuilder eventBindingsBuilder;
        private readonly ISourceMapManager sourceMapManager;

        /// <summary>
        /// Initializes a new output writer with the canonical serializers.
        /// </summary>
        public CompilerOutputWriter()
            : this(new ViewModelSchemaSerializer(), new EventBindingsBuilder(), new SourceMapManager())
        {
        }

        /// <summary>
        /// Initializes a new output writer with explicit serializers.
        /// </summary>
        /// <param name="schemaSerializer">The schema serializer.</param>
        /// <param name="eventBindingsBuilder">The event bindings serializer.</param>
        /// <param name="sourceMapManager">The source map serializer.</param>
        public CompilerOutputWriter(
            IViewModelSchemaSerializer schemaSerializer,
            IEventBindingsBuilder eventBindingsBuilder,
            ISourceMapManager sourceMapManager)
        {
            this.schemaSerializer = schemaSerializer ?? throw new ArgumentNullException(nameof(schemaSerializer));
            this.eventBindingsBuilder = eventBindingsBuilder ?? throw new ArgumentNullException(nameof(eventBindingsBuilder));
            this.sourceMapManager = sourceMapManager ?? throw new ArgumentNullException(nameof(sourceMapManager));
        }

        /// <summary>
        /// Writes all artifacts represented by a compilation result.
        /// </summary>
        /// <param name="result">The prebuilt compilation result.</param>
        /// <param name="options">The compiler options controlling output layout.</param>
        /// <returns>The files written and any output diagnostics.</returns>
        public OutputResult WriteOutput(CompilationResult result, CompilerOptions options)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(options);

            CompilerOptions normalizedOptions = options.ValidateAndNormalize();
            ImmutableArray<string>.Builder qmlFiles = ImmutableArray.CreateBuilder<string>();
            ImmutableArray<string>.Builder schemaFiles = ImmutableArray.CreateBuilder<string>();
            ImmutableArray<string>.Builder sourceMapFiles = ImmutableArray.CreateBuilder<string>();
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<CompilerDiagnostic>();
            long totalBytes = 0;
            string? eventBindingsFile = null;

            OutputLayout layout = OutputLayout.Create(normalizedOptions);
            if (!TryCreateDirectories(layout, diagnostics))
            {
                return CreateResult(qmlFiles, schemaFiles, eventBindingsFile, sourceMapFiles, totalBytes, diagnostics);
            }

            foreach (CompilationUnit unit in OrderedUnits(result.Units))
            {
                WriteUnitArtifacts(unit, normalizedOptions, layout, qmlFiles, schemaFiles, sourceMapFiles, diagnostics, ref totalBytes);
            }

            string eventPath = Path.Join(layout.OutputDir, EventBindingsFileName);
            if (TrySerializeEventBindings(result.EventBindings, eventPath, diagnostics, out string eventBindingsJson))
            {
                if (TryWriteArtifact(eventPath, eventBindingsJson, DiagnosticCodes.OutputWriteFailed, diagnostics, ref totalBytes))
                {
                    eventBindingsFile = eventPath;
                }
            }

            return CreateResult(qmlFiles, schemaFiles, eventBindingsFile, sourceMapFiles, totalBytes, diagnostics);
        }

        private void WriteUnitArtifacts(
            CompilationUnit unit,
            CompilerOptions options,
            OutputLayout layout,
            ImmutableArray<string>.Builder qmlFiles,
            ImmutableArray<string>.Builder schemaFiles,
            ImmutableArray<string>.Builder sourceMapFiles,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ref long totalBytes)
        {
            WriteSchemaArtifact(unit, layout, schemaFiles, diagnostics, ref totalBytes);
            WriteQmlArtifact(unit, layout, qmlFiles, diagnostics, ref totalBytes);
            WriteSourceMapArtifact(unit, options, layout, sourceMapFiles, diagnostics, ref totalBytes);
        }

        private void WriteSchemaArtifact(
            CompilationUnit unit,
            OutputLayout layout,
            ImmutableArray<string>.Builder schemaFiles,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ref long totalBytes)
        {
            if (unit.Schema is null)
            {
                return;
            }

            string? schemaPath = TryBuildArtifactPath(layout.SchemaDir, unit.Schema.ClassName, ".schema.json", diagnostics);
            if (schemaPath is not null && TrySerializeSchema(unit.Schema, schemaPath, diagnostics, out string schemaJson))
            {
                _ = TryWriteArtifact(schemaPath, schemaJson, DiagnosticCodes.OutputWriteFailed, diagnostics, schemaFiles, ref totalBytes);
            }
        }

        private static void WriteQmlArtifact(
            CompilationUnit unit,
            OutputLayout layout,
            ImmutableArray<string>.Builder qmlFiles,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ref long totalBytes)
        {
            if (!unit.Success || string.IsNullOrEmpty(unit.QmlText))
            {
                return;
            }

            string? qmlPath = TryBuildArtifactPath(layout.QmlModuleDir, unit.ViewClassName, ".qml", diagnostics);
            if (qmlPath is not null)
            {
                _ = TryWriteArtifact(qmlPath, unit.QmlText, DiagnosticCodes.OutputWriteFailed, diagnostics, qmlFiles, ref totalBytes);
            }
        }

        private void WriteSourceMapArtifact(
            CompilationUnit unit,
            CompilerOptions options,
            OutputLayout layout,
            ImmutableArray<string>.Builder sourceMapFiles,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ref long totalBytes)
        {
            if (!options.GenerateSourceMaps || unit.SourceMap is null)
            {
                return;
            }

            string mapBaseName = string.IsNullOrWhiteSpace(unit.ViewClassName)
                ? Path.GetFileNameWithoutExtension(unit.SourceMap.OutputFilePath)
                : unit.ViewClassName;
            string? sourceMapPath = TryBuildArtifactPath(layout.SourceMapDir, mapBaseName, ".qml.map", diagnostics);
            if (sourceMapPath is not null && TrySerializeSourceMap(unit.SourceMap, sourceMapPath, diagnostics, out string sourceMapJson))
            {
                _ = TryWriteArtifact(sourceMapPath, sourceMapJson, DiagnosticCodes.SourceMapWriteFailed, diagnostics, sourceMapFiles, ref totalBytes);
            }
        }

        private static IEnumerable<CompilationUnit> OrderedUnits(ImmutableArray<CompilationUnit> units)
        {
            return (units.IsDefault ? ImmutableArray<CompilationUnit>.Empty : units)
                .OrderBy(static unit => unit.SourceFilePath, StringComparer.Ordinal)
                .ThenBy(static unit => unit.ViewClassName, StringComparer.Ordinal)
                .ThenBy(static unit => unit.ViewModelClassName, StringComparer.Ordinal);
        }

        private static bool TryCreateDirectories(OutputLayout layout, ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            try
            {
                Directory.CreateDirectory(layout.OutputDir);
                Directory.CreateDirectory(layout.QmlModuleDir);
                Directory.CreateDirectory(layout.SchemaDir);
                Directory.CreateDirectory(layout.SourceMapDir);
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                diagnostics.Add(CreateDiagnostic(
                    DiagnosticCodes.OutputWriteFailed,
                    layout.OutputDir,
                    "Creating output directories failed.",
                    exception));
                return false;
            }
        }

        private bool TrySerializeSchema(
            ViewModelSchema schema,
            string targetPath,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            out string json)
        {
            try
            {
                json = NormalizeText(schemaSerializer.Serialize(schema));
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                diagnostics.Add(CreateDiagnostic(
                    DiagnosticCodes.SchemaSerializationFailed,
                    targetPath,
                    "Schema serialization failed.",
                    exception));
                json = string.Empty;
                return false;
            }
        }

        private bool TrySerializeEventBindings(
            EventBindingsIndex index,
            string targetPath,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            out string json)
        {
            try
            {
                json = NormalizeText(eventBindingsBuilder.Serialize(index));
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                diagnostics.Add(CreateDiagnostic(
                    DiagnosticCodes.OutputWriteFailed,
                    targetPath,
                    "Event bindings serialization failed.",
                    exception));
                json = string.Empty;
                return false;
            }
        }

        private bool TrySerializeSourceMap(
            SourceMap sourceMap,
            string targetPath,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            out string json)
        {
            try
            {
                json = NormalizeText(sourceMapManager.Serialize(sourceMap));
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                diagnostics.Add(CreateDiagnostic(
                    DiagnosticCodes.SourceMapWriteFailed,
                    targetPath,
                    "Source map serialization failed.",
                    exception));
                json = string.Empty;
                return false;
            }
        }

        private static bool TryWriteArtifact(
            string path,
            string text,
            string diagnosticCode,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ImmutableArray<string>.Builder writtenFiles,
            ref long totalBytes)
        {
            if (TryWriteArtifact(path, text, diagnosticCode, diagnostics, ref totalBytes))
            {
                writtenFiles.Add(path);
                return true;
            }

            return false;
        }

        private static bool TryWriteArtifact(
            string path,
            string text,
            string diagnosticCode,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics,
            ref long totalBytes)
        {
            try
            {
                byte[] bytes = Utf8NoBom.GetBytes(NormalizeText(text));
                WriteAtomically(path, bytes);
                totalBytes += bytes.LongLength;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                diagnostics.Add(CreateDiagnostic(
                    diagnosticCode,
                    path,
                    "Writing compiler output failed.",
                    exception));
                return false;
            }
        }

        private static void WriteAtomically(string path, byte[] bytes)
        {
            string? directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new IOException("Artifact path must include a directory.");
            }

            Directory.CreateDirectory(directory);
            string tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllBytes(tempPath, bytes);
                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private static string? TryBuildArtifactPath(
            string directory,
            string fileNameWithoutExtension,
            string extension,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            if (!IsSafeFileNameStem(fileNameWithoutExtension))
            {
                string targetPath = Path.Join(directory, $"{fileNameWithoutExtension}{extension}");
                diagnostics.Add(new CompilerDiagnostic(
                    DiagnosticCodes.OutputWriteFailed,
                    DiagnosticSeverity.Error,
                    $"Artifact file name '{fileNameWithoutExtension}' is not safe for deterministic output.",
                    SourceLocation.FileOnly(targetPath),
                    PhaseName));
                return null;
            }

            return Path.Join(directory, $"{fileNameWithoutExtension}{extension}");
        }

        private static bool IsSafeFileNameStem(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value))
            {
                return false;
            }

            return value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && !value.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !value.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
        }

        private static CompilerDiagnostic CreateDiagnostic(string code, string path, string message, Exception exception)
        {
            return new CompilerDiagnostic(
                code,
                DiagnosticSeverity.Error,
                $"{message} {exception.GetType().Name}: {exception.Message}",
                SourceLocation.FileOnly(path),
                PhaseName);
        }

        private static string NormalizeText(string text)
        {
            string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            return normalized.EndsWith('\n') ? normalized : normalized + "\n";
        }

        private static OutputResult CreateResult(
            ImmutableArray<string>.Builder qmlFiles,
            ImmutableArray<string>.Builder schemaFiles,
            string? eventBindingsFile,
            ImmutableArray<string>.Builder sourceMapFiles,
            long totalBytes,
            ImmutableArray<CompilerDiagnostic>.Builder diagnostics)
        {
            return new OutputResult(
                qmlFiles.OrderBy(static path => path, StringComparer.Ordinal).ToImmutableArray(),
                schemaFiles.OrderBy(static path => path, StringComparer.Ordinal).ToImmutableArray(),
                eventBindingsFile,
                sourceMapFiles.OrderBy(static path => path, StringComparer.Ordinal).ToImmutableArray(),
                totalBytes)
            {
                Diagnostics = diagnostics
                    .OrderBy(static diagnostic => diagnostic.Location?.FilePath ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
                    .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal)
                    .ToImmutableArray(),
            };
        }

        private sealed record OutputLayout(string OutputDir, string QmlModuleDir, string SchemaDir, string SourceMapDir)
        {
            public static OutputLayout Create(CompilerOptions options)
            {
                string outputDir = options.OutputDir;
                string qmlModuleDir = Path.Join(outputDir, "qml", options.ModuleUriPrefix.Replace('.', Path.DirectorySeparatorChar));
                string schemaDir = Path.Join(outputDir, "schemas");
                string sourceMapDir = options.SourceMapDir ?? Path.Join(outputDir, "source-maps");

                return new OutputLayout(outputDir, qmlModuleDir, schemaDir, sourceMapDir);
            }
        }
    }
}
