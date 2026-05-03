using System.Runtime.InteropServices;

namespace QmlSharp.Host.Tests.Fixtures
{
    internal static class NativeTestLibrary
    {
        public static string Resolve()
        {
            string repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
            string libraryFileName = GetLibraryFileName();
            string[] candidateDirectories =
            [
                Path.Combine(repositoryRoot, "build", "debug", "bin"),
                Path.Combine(repositoryRoot, "build", "windows-ci", "bin"),
                Path.Combine(repositoryRoot, "build", "linux-ci", "bin"),
                Path.Combine(repositoryRoot, "build", "macos-ci", "bin"),
                Path.Combine(repositoryRoot, "build", "release", "bin")
            ];

            foreach (string candidateDirectory in candidateDirectories)
            {
                string candidate = Path.Combine(candidateDirectory, libraryFileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            string candidates = string.Join(Environment.NewLine, candidateDirectories.Select(
                static directory => $"  - {Path.Combine(directory, GetLibraryFileName())}"));
            throw new FileNotFoundException(
                "The native host library was not found. Build the native target before running RequiresNative tests." +
                Environment.NewLine +
                candidates);
        }

        private static string FindRepositoryRoot(string startDirectory)
        {
            DirectoryInfo? current = new(startDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "QmlSharp.slnx")))
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
