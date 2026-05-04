#pragma warning disable MA0048

using QmlSharp.Compiler;

namespace QmlSharp.Build
{
    /// <summary>Generates C++ QObject subclass code from ViewModel schemas.</summary>
    public interface ICppCodeGenerator
    {
        /// <summary>Generates all C++ files for the given schemas.</summary>
        CppGenerationResult Generate(
            ImmutableArray<ViewModelSchema> schemas,
            CppGenerationOptions options);

        /// <summary>Generates a single ViewModel header file.</summary>
        string GenerateHeader(ViewModelSchema schema, CppGenerationOptions options);

        /// <summary>Generates a single ViewModel implementation file.</summary>
        string GenerateImplementation(ViewModelSchema schema, CppGenerationOptions options);

        /// <summary>Generates CMakeLists.txt for the generated native project.</summary>
        string GenerateCMakeLists(
            ImmutableArray<ViewModelSchema> schemas,
            CppGenerationOptions options);

        /// <summary>Generates type_registration.cpp for QML type registration.</summary>
        string GenerateTypeRegistration(
            ImmutableArray<ViewModelSchema> schemas,
            CppGenerationOptions options);
    }

    /// <summary>Options for C++ code generation.</summary>
    public sealed record CppGenerationOptions
    {
        /// <summary>Output directory for generated C++ files.</summary>
        public required string OutputDir { get; init; }

        /// <summary>Path to the Qt SDK.</summary>
        public required string QtDir { get; init; }

        /// <summary>CMake project name.</summary>
        public string ProjectName { get; init; } = "qmlsharp_native";

        /// <summary>CMake preset for the build.</summary>
        public string CmakePreset { get; init; } = "default";

        /// <summary>Qt modules to link against.</summary>
        public ImmutableArray<string> QtModules { get; init; } =
            ImmutableArray.Create("Qt6::Quick", "Qt6::Qml");

        /// <summary>Path to handwritten native ABI sources from 07-native-host.</summary>
        public required string AbiSourceDir { get; init; }
    }

    /// <summary>Result of C++ code generation.</summary>
    public sealed record CppGenerationResult
    {
        /// <summary>Generated file paths keyed to file content.</summary>
        public required ImmutableDictionary<string, string> Files { get; init; }

        /// <summary>Generated header file paths.</summary>
        public required ImmutableArray<string> HeaderFiles { get; init; }

        /// <summary>Generated implementation file paths.</summary>
        public required ImmutableArray<string> ImplementationFiles { get; init; }

        /// <summary>Generated CMakeLists.txt path.</summary>
        public required string CMakeListsPath { get; init; }

        /// <summary>Generated type_registration.cpp path.</summary>
        public required string TypeRegistrationPath { get; init; }

        /// <summary>Generation diagnostics.</summary>
        public ImmutableArray<BuildDiagnostic> Diagnostics { get; init; } =
            ImmutableArray<BuildDiagnostic>.Empty;
    }

    /// <summary>Orchestrates the CMake build process for the native library.</summary>
    public interface ICMakeBuilder
    {
        /// <summary>Runs cmake configure with a preset.</summary>
        Task<CMakeStepResult> ConfigureAsync(string buildDir, string preset);

        /// <summary>Runs cmake build for the native library.</summary>
        Task<CMakeStepResult> BuildAsync(string buildDir);

        /// <summary>Returns the expected output path for the native library.</summary>
        string GetOutputLibraryPath(string buildDir);
    }

    /// <summary>Result of a CMake step.</summary>
    /// <param name="Success">True when the step succeeded.</param>
    /// <param name="Stdout">Captured stdout.</param>
    /// <param name="Stderr">Captured stderr.</param>
    /// <param name="Duration">Step duration.</param>
    /// <param name="ExitCode">Process exit code.</param>
    public sealed record CMakeStepResult(
        bool Success,
        string Stdout,
        string Stderr,
        TimeSpan Duration,
        int ExitCode);

    /// <summary>Generates qmldir files for QML modules.</summary>
    public interface IQmldirGenerator
    {
        /// <summary>Generates qmldir content for a QML module.</summary>
        string Generate(
            string moduleUri,
            QmlVersion version,
            ImmutableArray<ViewModelSchema> schemas);
    }

    /// <summary>Generates .qmltypes files for QML modules.</summary>
    public interface IQmltypesGenerator
    {
        /// <summary>Generates qmltypes content for a QML module.</summary>
        string Generate(
            string moduleUri,
            QmlVersion version,
            ImmutableArray<ViewModelSchema> schemas);
    }
}

#pragma warning restore MA0048
