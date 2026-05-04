#pragma warning disable MA0048

namespace QmlSharp.Build
{
    /// <summary>Packages the build product for target platforms.</summary>
    public interface IPlatformDistributor
    {
        /// <summary>Packages the build output for the current platform.</summary>
        DistributionResult Package(BuildResult buildResult, BuildContext context);

        /// <summary>Returns the native library file extension for the target platform.</summary>
        string GetNativeLibExtension(PlatformTarget target);

        /// <summary>Returns Qt runtime libraries to include for distribution.</summary>
        ImmutableArray<string> GetQtRuntimeDependencies(
            PlatformTarget target,
            ImmutableArray<string> qtModules);
    }

    /// <summary>Target platform for distribution.</summary>
    public enum PlatformTarget
    {
        /// <summary>Windows x64.</summary>
        WindowsX64,

        /// <summary>Linux x64.</summary>
        LinuxX64,

        /// <summary>macOS arm64.</summary>
        MacOsArm64,

        /// <summary>macOS x64.</summary>
        MacOsX64,
    }

    /// <summary>Result of platform distribution.</summary>
    /// <param name="Success">True when packaging succeeded.</param>
    /// <param name="Target">Target platform.</param>
    /// <param name="OutputPath">Distribution output path.</param>
    /// <param name="IncludedFiles">Included files.</param>
    /// <param name="TotalSizeBytes">Total output size in bytes.</param>
    public sealed record DistributionResult(
        bool Success,
        PlatformTarget Target,
        string OutputPath,
        ImmutableArray<string> IncludedFiles,
        long TotalSizeBytes);
}

#pragma warning restore MA0048
