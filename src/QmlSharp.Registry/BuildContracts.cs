#pragma warning disable MA0048

using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Registry
{
    /// <summary>Configuration for the full registry build pipeline.</summary>
    public sealed record BuildConfig(
        string QtDir,
        string? SnapshotPath,
        bool ForceRebuild,
        ImmutableArray<string>? ModuleFilter,
        bool IncludeInternal);

    /// <summary>Progress reporting during build.</summary>
    public sealed record BuildProgress(
        BuildPhase Phase,
        int CurrentStep,
        int TotalSteps,
        string? Detail);

    /// <summary>Phases of the registry build pipeline.</summary>
    public enum BuildPhase
    {
        Scanning,
        ParsingQmltypes,
        ParsingQmldir,
        ParsingMetatypes,
        Normalizing,
        SavingSnapshot,
        LoadingSnapshot,
        Complete,
    }

    /// <summary>Generic parse result wrapping a value and diagnostics.</summary>
    public sealed record ParseResult<T>(
        T? Value,
        ImmutableArray<RegistryDiagnostic> Diagnostics)
    {
        public bool IsSuccess =>
            Value is not null
            && !RegistryDiagnosticCollection.HasErrors(Diagnostics);
    }

    /// <summary>Result of normalization.</summary>
    public sealed record NormalizeResult(
        QmlRegistry? Registry,
        ImmutableArray<RegistryDiagnostic> Diagnostics)
    {
        public bool IsSuccess =>
            Registry is not null
            && !RegistryDiagnosticCollection.HasErrors(Diagnostics);
    }

    /// <summary>Result of a full build or snapshot load.</summary>
    public sealed record BuildResult(
        ITypeRegistry? TypeRegistry,
        IRegistryQuery? Query,
        ImmutableArray<RegistryDiagnostic> Diagnostics)
    {
        public bool IsSuccess =>
            TypeRegistry is not null
            && Query is not null
            && !RegistryDiagnosticCollection.HasErrors(Diagnostics);
    }

    internal static class RegistryDiagnosticCollection
    {
        public static bool HasErrors(ImmutableArray<RegistryDiagnostic> diagnostics)
        {
            return !diagnostics.IsDefaultOrEmpty
                && diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        }
    }

    /// <summary>Snapshot file validity info.</summary>
    public sealed record SnapshotValidity(
        bool IsValid,
        int FormatVersion,
        string? QtVersion,
        DateTimeOffset? BuildTimestamp,
        string? ErrorMessage);

    /// <summary>
    /// Orchestrates the full registry build pipeline:
    ///   scan → parse → normalize → snapshot → query.
    /// Supports two modes: full build (from Qt SDK) and cached build (from snapshot).
    /// </summary>
    public interface IRegistryBuilder
    {
        /// <summary>
        /// Builds a QmlRegistry from the Qt SDK installation directory.
        /// Runs the full pipeline: scan → parse → normalize.
        /// Optionally saves a snapshot for future cached builds.
        /// </summary>
        BuildResult Build(BuildConfig config, Action<BuildProgress>? progress = null);

        /// <summary>
        /// Loads a QmlRegistry from a pre-generated snapshot file.
        /// Does not require Qt SDK to be installed.
        /// </summary>
        BuildResult LoadFromSnapshot(string snapshotPath);

        /// <summary>
        /// Builds or loads depending on cache state:
        ///   1. If snapshot exists and matches current config, load from snapshot.
        ///   2. Otherwise, build from Qt SDK and save snapshot.
        /// </summary>
        BuildResult BuildOrLoad(BuildConfig config, Action<BuildProgress>? progress = null);
    }
}

#pragma warning restore MA0048
