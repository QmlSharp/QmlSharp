#pragma warning disable MA0048

using QmlSharp.Registry.Diagnostics;

namespace QmlSharp.Registry.Scanning
{
    /// <summary>Configuration for the Qt SDK scanner.</summary>
    public sealed record ScannerConfig(
        string QtDir,
        ImmutableArray<string>? ModuleFilter,
        bool IncludeInternal);

    /// <summary>Result of file scanning.</summary>
    public sealed record ScanResult(
        ImmutableArray<string> QmltypesPaths,
        ImmutableArray<string> QmldirPaths,
        ImmutableArray<string> MetatypesPaths,
        ImmutableArray<RegistryDiagnostic> Diagnostics);

    /// <summary>Validation result for Qt SDK directory.</summary>
    public sealed record ScanValidation(
        bool IsValid,
        string? QtVersion,
        string? ErrorMessage);

    /// <summary>
    /// Discovers .qmltypes, qmldir, and *_metatypes.json files from
    /// the Qt SDK installation directory.
    /// </summary>
    public interface IQtTypeScanner
    {
        ScanResult Scan(ScannerConfig config);

        ScanValidation ValidateQtDir(string qtDir);

        string? InferModuleUri(string qmldirPath, string qmlRootDir);
    }
}

#pragma warning restore MA0048
