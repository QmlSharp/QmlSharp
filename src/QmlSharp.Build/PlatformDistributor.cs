using System.Runtime.InteropServices;

namespace QmlSharp.Build
{
    /// <summary>Filesystem-backed current-platform package distributor.</summary>
    public sealed class PlatformDistributor : IPlatformDistributor
    {
        /// <inheritdoc />
        public DistributionResult Package(BuildResult buildResult, BuildContext context)
        {
            ArgumentNullException.ThrowIfNull(buildResult);
            ArgumentNullException.ThrowIfNull(context);

            PlatformTarget target = GetCurrentPlatform();
            string sourceRoot = Path.GetFullPath(context.OutputDir);
            string outputPath = GetDistributionOutputPath(sourceRoot, target);
            if (!buildResult.Success || !Directory.Exists(sourceRoot))
            {
                return new DistributionResult(
                    false,
                    target,
                    outputPath,
                    ImmutableArray<string>.Empty,
                    0);
            }

            ResetDirectory(outputPath);
            CopyDirectory(sourceRoot, outputPath);
            CopyQtRuntimeDependencies(context, target, outputPath);

            ImmutableArray<string> includedFiles = EnumerateIncludedFiles(outputPath);
            long totalSizeBytes = includedFiles
                .Select(relativePath => Path.Join(outputPath, relativePath.Replace('/', Path.DirectorySeparatorChar)))
                .Where(File.Exists)
                .Sum(static path => new FileInfo(path).Length);
            return new DistributionResult(true, target, outputPath, includedFiles, totalSizeBytes);
        }

        /// <inheritdoc />
        public string GetNativeLibExtension(PlatformTarget target)
        {
            return target switch
            {
                PlatformTarget.WindowsX64 => ".dll",
                PlatformTarget.LinuxX64 => ".so",
                PlatformTarget.MacOsArm64 or PlatformTarget.MacOsX64 => ".dylib",
                _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported platform target."),
            };
        }

        /// <inheritdoc />
        public ImmutableArray<string> GetQtRuntimeDependencies(
            PlatformTarget target,
            ImmutableArray<string> qtModules)
        {
            SortedSet<string> libraries = new(StringComparer.Ordinal);
            AddQtLibrary(libraries, "Core");
            foreach (string module in qtModules.OrderBy(static module => module, StringComparer.Ordinal))
            {
                AddModuleDependencies(libraries, module);
            }

            return libraries
                .Select(library => FormatRuntimeDependency(target, library))
                .ToImmutableArray();
        }

        private static PlatformTarget GetCurrentPlatform()
        {
            if (OperatingSystem.IsWindows())
            {
                return PlatformTarget.WindowsX64;
            }

            if (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                return PlatformTarget.MacOsArm64;
            }

            if (OperatingSystem.IsMacOS())
            {
                return PlatformTarget.MacOsX64;
            }

            return PlatformTarget.LinuxX64;
        }

        private static void AddModuleDependencies(SortedSet<string> libraries, string module)
        {
            string normalized = module.Trim();
            if (string.Equals(normalized, "QtQuick.Controls", StringComparison.Ordinal) ||
                string.Equals(normalized, "Quick.Controls", StringComparison.Ordinal))
            {
                AddQtLibrary(libraries, "Core");
                AddQtLibrary(libraries, "Gui");
                AddQtLibrary(libraries, "Network");
                AddQtLibrary(libraries, "Qml");
                AddQtLibrary(libraries, "Quick");
                AddQtLibrary(libraries, "QuickControls2");
                AddQtLibrary(libraries, "QuickTemplates2");
                return;
            }

            string library = normalized.StartsWith("Qt", StringComparison.Ordinal)
                ? normalized[2..]
                : normalized;
            library = library.Replace(".", string.Empty, StringComparison.Ordinal);
            if (string.Equals(library, "Quick", StringComparison.Ordinal))
            {
                AddQtLibrary(libraries, "Core");
                AddQtLibrary(libraries, "Gui");
                AddQtLibrary(libraries, "Network");
                AddQtLibrary(libraries, "Qml");
                AddQtLibrary(libraries, "Quick");
                return;
            }

            if (string.Equals(library, "Qml", StringComparison.Ordinal))
            {
                AddQtLibrary(libraries, "Core");
                AddQtLibrary(libraries, "Network");
                AddQtLibrary(libraries, "Qml");
                return;
            }

            AddQtLibrary(libraries, library);
        }

        private static void AddQtLibrary(SortedSet<string> libraries, string library)
        {
            if (!string.IsNullOrWhiteSpace(library))
            {
                _ = libraries.Add(library);
            }
        }

        private static string FormatRuntimeDependency(PlatformTarget target, string library)
        {
            return target switch
            {
                PlatformTarget.WindowsX64 => "Qt6" + library + ".dll",
                PlatformTarget.LinuxX64 => "libQt6" + library + ".so",
                PlatformTarget.MacOsArm64 or PlatformTarget.MacOsX64 => "Qt" + library + ".framework",
                _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported platform target."),
            };
        }

        private static void CopyQtRuntimeDependencies(BuildContext context, PlatformTarget target, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(context.QtDir) || !Directory.Exists(context.QtDir))
            {
                return;
            }

            PlatformDistributor distributor = new();
            ImmutableArray<string> dependencies = distributor.GetQtRuntimeDependencies(target, context.Config.Qt.Modules);
            string runtimeSourceDir = target is PlatformTarget.WindowsX64
                ? Path.Join(context.QtDir, "bin")
                : Path.Join(context.QtDir, "lib");
            if (!Directory.Exists(runtimeSourceDir))
            {
                return;
            }

            string runtimeOutputDir = Path.Join(outputPath, "Qt6");
            foreach (string dependency in dependencies)
            {
                string sourcePath = ResolveRuntimeDependencyPath(runtimeSourceDir, dependency, target);
                if (File.Exists(sourcePath))
                {
                    _ = Directory.CreateDirectory(runtimeOutputDir);
                    File.Copy(sourcePath, Path.Join(runtimeOutputDir, Path.GetFileName(sourcePath)), overwrite: true);
                }
                else if (Directory.Exists(sourcePath))
                {
                    CopyDirectory(sourcePath, Path.Join(runtimeOutputDir, Path.GetFileName(sourcePath)));
                }
            }
        }

        private static string ResolveRuntimeDependencyPath(
            string runtimeSourceDir,
            string dependency,
            PlatformTarget target)
        {
            string directPath = Path.Join(runtimeSourceDir, dependency);
            if (File.Exists(directPath) || Directory.Exists(directPath) || target is not PlatformTarget.LinuxX64)
            {
                return directPath;
            }

            string? versionedLinuxPath = Directory
                .EnumerateFiles(runtimeSourceDir, dependency + "*", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .FirstOrDefault();
            return versionedLinuxPath ?? directPath;
        }

        private static string GetDistributionOutputPath(string sourceRoot, PlatformTarget target)
        {
            string trimmedSource = sourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string sourceName = Path.GetFileName(trimmedSource);
            string parent = Path.GetDirectoryName(trimmedSource) ?? trimmedSource;
            return Path.Join(parent, sourceName + "-" + GetTargetMoniker(target));
        }

        private static string GetTargetMoniker(PlatformTarget target)
        {
            return target switch
            {
                PlatformTarget.WindowsX64 => "windows-x64",
                PlatformTarget.LinuxX64 => "linux-x64",
                PlatformTarget.MacOsArm64 => "macos-arm64",
                PlatformTarget.MacOsX64 => "macos-x64",
                _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported platform target."),
            };
        }

        private static void ResetDirectory(string outputPath)
        {
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, recursive: true);
            }

            _ = Directory.CreateDirectory(outputPath);
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            _ = Directory.CreateDirectory(destinationDirectory);
            foreach (string directory in Directory
                .EnumerateDirectories(sourceDirectory)
                .OrderBy(static path => path, StringComparer.Ordinal))
            {
                CopyDirectory(directory, Path.Join(destinationDirectory, Path.GetFileName(directory)));
            }

            foreach (string file in Directory
                .EnumerateFiles(sourceDirectory)
                .OrderBy(static path => path, StringComparer.Ordinal))
            {
                File.Copy(file, Path.Join(destinationDirectory, Path.GetFileName(file)), overwrite: true);
            }
        }

        private static ImmutableArray<string> EnumerateIncludedFiles(string outputPath)
        {
            return Directory
                .EnumerateFiles(outputPath, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(outputPath, path).Replace('\\', '/'))
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }
    }
}
