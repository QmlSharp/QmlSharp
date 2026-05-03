using System.Runtime.InteropServices;

namespace QmlSharp.Host.Tests.Fixtures
{
    internal static class NativeTestLibrary
    {
        public static string Resolve()
        {
            string repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
            string libraryFileName = GetLibraryFileName();
            ValidateLibraryFileName(libraryFileName);

            string[] candidateDirectories =
            [
                CombineUnder(repositoryRoot, "build", "debug", "bin"),
                CombineUnder(repositoryRoot, "build", "windows-ci", "bin"),
                CombineUnder(repositoryRoot, "build", "linux-ci", "bin"),
                CombineUnder(repositoryRoot, "build", "macos-ci", "bin"),
                CombineUnder(repositoryRoot, "build", "macos-debug", "bin"),
                CombineUnder(repositoryRoot, "build", "macos-release", "bin"),
                CombineUnder(repositoryRoot, "build", "release", "bin")
            ];

            foreach (string candidateDirectory in candidateDirectories)
            {
                string candidate = Path.Join(candidateDirectory, libraryFileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            string candidates = string.Join(Environment.NewLine, candidateDirectories.Select(
                directory => $"  - {Path.Join(directory, libraryFileName)}"));
            throw new FileNotFoundException(
                "The native host library was not found. Build the native target before running RequiresNative tests." +
                Environment.NewLine +
                candidates);
        }

        private static string CombineUnder(string basePath, params string[] relativeSegments)
        {
            foreach (string segment in relativeSegments)
            {
                if (Path.IsPathRooted(segment))
                {
                    throw new ArgumentException("Native test library candidate path segments must be relative.", nameof(relativeSegments));
                }
            }

            return Path.Join([basePath, .. relativeSegments]);
        }

        private static void ValidateLibraryFileName(string libraryFileName)
        {
            if (Path.IsPathRooted(libraryFileName) || Path.GetFileName(libraryFileName) != libraryFileName)
            {
                throw new InvalidOperationException("Native test library name must be a file name, not a path.");
            }
        }

        private static string FindRepositoryRoot(string startDirectory)
        {
            DirectoryInfo? current = new(startDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Join(current.FullName, "QmlSharp.slnx")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the QmlSharp repository root.");
        }

        private static string GetLibraryFileName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "qmlsharp_native.dll";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "libqmlsharp_native.dylib";
            }

            return "libqmlsharp_native.so";
        }
    }
}
