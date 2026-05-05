#pragma warning disable MA0048

namespace QmlSharp.Build
{
    /// <summary>Runs environment diagnostic checks.</summary>
    public interface IDoctor
    {
        /// <summary>Runs all diagnostic checks.</summary>
        Task<ImmutableArray<DoctorCheckResult>> RunAllChecksAsync(QmlSharpConfig? config = null);

        /// <summary>Runs a single diagnostic check by ID.</summary>
        Task<DoctorCheckResult> RunCheckAsync(string checkId);

        /// <summary>Attempts automatic fixes for checks that support it.</summary>
        Task<ImmutableArray<DoctorFixResult>> AutoFixAsync(ImmutableArray<DoctorCheckResult> failedChecks);
    }

    /// <summary>Identifies doctor checks.</summary>
    public static class DoctorCheckId
    {
        /// <summary>Qt SDK installation check.</summary>
        public const string QtInstalled = "qt-installed";

        /// <summary>Qt version check.</summary>
        public const string QtVersion = "qt-version";

        /// <summary>qmlformat availability check.</summary>
        public const string QmlFormatAvailable = "qmlformat-available";

        /// <summary>qmllint availability check.</summary>
        public const string QmlLintAvailable = "qmllint-available";

        /// <summary>qmlcachegen availability check.</summary>
        public const string QmlCachegenAvailable = "qmlcachegen-available";

        /// <summary>.NET SDK version check.</summary>
        public const string DotNetVersion = "dotnet-version";

        /// <summary>CMake availability check.</summary>
        public const string CMakeAvailable = "cmake-available";

        /// <summary>CMake version check.</summary>
        public const string CMakeVersion = "cmake-version";

        /// <summary>MSVC availability check.</summary>
        public const string MsvcAvailable = "msvc-available";

        /// <summary>Clang availability check.</summary>
        public const string ClangAvailable = "clang-available";

        /// <summary>Ninja availability check.</summary>
        public const string NinjaAvailable = "ninja-available";

        /// <summary>Configuration validation check.</summary>
        public const string ConfigValid = "config-valid";

        /// <summary>Native library existence check.</summary>
        public const string NativeLibExists = "native-lib-exists";

        /// <summary>NuGet resolution check.</summary>
        public const string NuGetResolved = "nuget-resolved";

        /// <summary>All stable doctor check identifiers in execution order.</summary>
        public static ImmutableArray<string> All { get; } = ImmutableArray.Create(
            QtInstalled,
            QtVersion,
            QmlFormatAvailable,
            QmlLintAvailable,
            QmlCachegenAvailable,
            DotNetVersion,
            CMakeAvailable,
            CMakeVersion,
            MsvcAvailable,
            ClangAvailable,
            NinjaAvailable,
            ConfigValid,
            NativeLibExists,
            NuGetResolved);
    }

    /// <summary>Result of a single doctor check.</summary>
    /// <param name="CheckId">Check identifier.</param>
    /// <param name="Description">Check description.</param>
    /// <param name="Status">Check status.</param>
    /// <param name="Detail">Optional check detail.</param>
    /// <param name="FixHint">Optional fix hint.</param>
    /// <param name="AutoFixable">Whether the check supports automatic fixing.</param>
    public sealed record DoctorCheckResult(
        string CheckId,
        string Description,
        DoctorCheckStatus Status,
        string? Detail,
        string? FixHint,
        bool AutoFixable);

    /// <summary>Doctor check status.</summary>
    public enum DoctorCheckStatus
    {
        /// <summary>Check passed.</summary>
        Pass,

        /// <summary>Check reported a warning.</summary>
        Warning,

        /// <summary>Check failed.</summary>
        Fail,

        /// <summary>Check was skipped.</summary>
        Skipped,
    }

    /// <summary>Result of an automatic fix attempt.</summary>
    /// <param name="CheckId">Check identifier.</param>
    /// <param name="Fixed">Whether the issue was fixed.</param>
    /// <param name="Detail">Fix detail.</param>
    public sealed record DoctorFixResult(
        string CheckId,
        bool Fixed,
        string Detail);
}

#pragma warning restore MA0048
