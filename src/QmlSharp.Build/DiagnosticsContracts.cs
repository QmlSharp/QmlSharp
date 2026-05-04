#pragma warning disable MA0048

namespace QmlSharp.Build
{
    /// <summary>A structured build diagnostic.</summary>
    /// <param name="Code">Stable diagnostic code.</param>
    /// <param name="Severity">Diagnostic severity.</param>
    /// <param name="Message">Human-readable message.</param>
    /// <param name="Phase">Optional build phase.</param>
    /// <param name="FilePath">Optional related file path.</param>
    public sealed record BuildDiagnostic(
        string Code,
        BuildDiagnosticSeverity Severity,
        string Message,
        BuildPhase? Phase,
        string? FilePath);

    /// <summary>Build diagnostic severity.</summary>
    public enum BuildDiagnosticSeverity
    {
        /// <summary>Informational diagnostic.</summary>
        Info,

        /// <summary>Warning diagnostic.</summary>
        Warning,

        /// <summary>Error diagnostic.</summary>
        Error,

        /// <summary>Fatal diagnostic.</summary>
        Fatal,
    }

    /// <summary>Build-system diagnostic code constants.</summary>
    public static class BuildDiagnosticCode
    {
        /// <summary>qmlsharp.json could not be found.</summary>
        public const string ConfigNotFound = "QMLSHARP-B001";

        /// <summary>qmlsharp.json could not be parsed.</summary>
        public const string ConfigParseError = "QMLSHARP-B002";

        /// <summary>Configuration failed validation.</summary>
        public const string ConfigValidationError = "QMLSHARP-B003";

        /// <summary>Qt SDK directory was not found.</summary>
        public const string QtDirNotFound = "QMLSHARP-B004";

        /// <summary>C# compilation failed.</summary>
        public const string CompilationFailed = "QMLSHARP-B010";

        /// <summary>No ViewModels were found.</summary>
        public const string NoViewModelsFound = "QMLSHARP-B011";

        /// <summary>Schema generation failed.</summary>
        public const string SchemaGenerationFailed = "QMLSHARP-B012";

        /// <summary>C++ code generation failed.</summary>
        public const string CppGenerationFailed = "QMLSHARP-B020";

        /// <summary>A schema type is unsupported by C++ code generation.</summary>
        public const string UnsupportedCppType = "QMLSHARP-B021";

        /// <summary>CMake configuration failed.</summary>
        public const string CMakeConfigureFailed = "QMLSHARP-B022";

        /// <summary>CMake build failed.</summary>
        public const string CMakeBuildFailed = "QMLSHARP-B023";

        /// <summary>qmldir generation failed.</summary>
        public const string QmldirGenerationFailed = "QMLSHARP-B030";

        /// <summary>qmltypes generation failed.</summary>
        public const string QmltypesGenerationFailed = "QMLSHARP-B031";

        /// <summary>Package resolution failed.</summary>
        public const string PackageResolutionFailed = "QMLSHARP-B040";

        /// <summary>Package manifest parsing failed.</summary>
        public const string ManifestParseError = "QMLSHARP-B041";

        /// <summary>An asset could not be found.</summary>
        public const string AssetNotFound = "QMLSHARP-B050";

        /// <summary>An asset could not be copied.</summary>
        public const string AssetCopyFailed = "QMLSHARP-B051";

        /// <summary>QML linting failed.</summary>
        public const string QmlLintError = "QMLSHARP-B060";

        /// <summary>QML formatting failed.</summary>
        public const string QmlFormatError = "QMLSHARP-B061";

        /// <summary>Output assembly failed.</summary>
        public const string OutputAssemblyFailed = "QMLSHARP-B070";

        /// <summary>Manifest writing failed.</summary>
        public const string ManifestWriteFailed = "QMLSHARP-B071";

        /// <summary>Output validation failed.</summary>
        public const string OutputValidationFailed = "QMLSHARP-B072";

        /// <summary>An internal build-system error occurred.</summary>
        public const string InternalError = "QMLSHARP-B090";
    }
}

#pragma warning restore MA0048
