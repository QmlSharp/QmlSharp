using System.Runtime.InteropServices;
using QmlSharp.Compiler;
using QmlSharp.Host.Engine;

namespace QmlSharp.Integration.Tests.Fixtures
{
    internal sealed class NativeRoundTripFixture : IDisposable
    {
        private const string NativeLibraryToken = "__QMLSHARP_NATIVE_LIBRARY__";
        // Qt keeps QML type registrations process-global, including native type factories.
        private static readonly Lazy<SharedArtifacts> Shared = new(
            CreateSharedArtifacts,
            LazyThreadSafetyMode.ExecutionAndPublication);

        private readonly NativeFixtureRegistrations registrations;
        private bool disposed;

        private NativeRoundTripFixture(
            string distDirectory,
            string mainQmlPath,
            string reloadQmlPath,
            string quitQmlPath,
            string nativeLibraryPath,
            IReadOnlyList<ViewModelSchema> schemas,
            NativeFixtureRegistrations registrations)
        {
            DistDirectory = distDirectory;
            MainQmlPath = mainQmlPath;
            ReloadQmlPath = reloadQmlPath;
            QuitQmlPath = quitQmlPath;
            NativeLibraryPath = nativeLibraryPath;
            Schemas = schemas;
            this.registrations = registrations;
        }

        public string DistDirectory { get; }

        public string MainQmlPath { get; }

        public string ReloadQmlPath { get; }

        public string QuitQmlPath { get; }

        public string NativeLibraryPath { get; }

        public IReadOnlyList<ViewModelSchema> Schemas { get; }

        public static NativeRoundTripFixture Create()
        {
            ConfigureQtEnvironment();
            SharedArtifacts artifacts = Shared.Value;

            return new NativeRoundTripFixture(
                artifacts.DistDirectory,
                artifacts.MainQmlPath,
                artifacts.ReloadQmlPath,
                artifacts.QuitQmlPath,
                artifacts.NativeLibraryPath,
                artifacts.Schemas,
                artifacts.Registrations);
        }

        public QmlSharpEngine CreateEngine()
        {
            ThrowIfDisposed();
            return new QmlSharpEngine(
                NativeLibraryPath,
                registrations.RegisterRegistrationCounterViewModel);
        }

        public void QueueApplicationQuit()
        {
            ThrowIfDisposed();
            registrations.QueueApplicationQuit();
        }

        public void Dispose()
        {
            disposed = true;
        }

        private static SharedArtifacts CreateSharedArtifacts()
        {
            string repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
            string fixtureRoot = Path.Join(repositoryRoot, "tests", "fixtures", "native-host", "integration");
            string nativeLibrarySource = ResolveBuiltIntegrationLibrary(repositoryRoot);
            string tempRoot = Path.Join(Path.GetTempPath(), "qmlsharp-integration-" + Guid.NewGuid().ToString("N"));
            string distDirectory = Path.Join(tempRoot, "dist");
            string schemasDirectory = Path.Join(distDirectory, "schemas");
            string qmlDirectory = Path.Join(distDirectory, "qml", "QmlSharp", "Integration", "Tests");
            string nativeDirectory = Path.Join(distDirectory, "native");
            string managedDirectory = Path.Join(distDirectory, "managed");

            _ = Directory.CreateDirectory(schemasDirectory);
            _ = Directory.CreateDirectory(qmlDirectory);
            _ = Directory.CreateDirectory(nativeDirectory);
            _ = Directory.CreateDirectory(managedDirectory);

            CopyFixtureFile(fixtureRoot, "RegistrationCounterViewModel.schema.json", schemasDirectory);
            CopyFixtureFile(fixtureRoot, "event-bindings.json", distDirectory);
            CopyFixtureFile(fixtureRoot, "Main.qml", qmlDirectory);
            CopyFixtureFile(fixtureRoot, "Reload.qml", qmlDirectory);
            CopyFixtureFile(fixtureRoot, "Quit.qml", qmlDirectory);
            CopyFixtureFile(fixtureRoot, "qmldir", qmlDirectory);

            string nativeLibraryFileName = GetNativeLibraryFileName();
            string artifactNativeLibraryPath = Path.Join(nativeDirectory, nativeLibraryFileName);
            File.Copy(nativeLibrarySource, artifactNativeLibraryPath, overwrite: true);

            string managedAssemblyPath = Path.Join(managedDirectory, "QmlSharp.Integration.Tests.dll");
            File.Copy(typeof(NativeRoundTripFixture).Assembly.Location, managedAssemblyPath, overwrite: true);

            string manifestTemplate = File.ReadAllText(Path.Join(fixtureRoot, "manifest.json"));
            string manifest = manifestTemplate.Replace(NativeLibraryToken, nativeLibraryFileName, StringComparison.Ordinal);
            File.WriteAllText(Path.Join(distDirectory, "manifest.json"), manifest);

            ViewModelSchemaSerializer serializer = new();
            string schemaJson = File.ReadAllText(Path.Join(schemasDirectory, "RegistrationCounterViewModel.schema.json"));
            NativeFixtureRegistrations registrations = NativeFixtureRegistrations.ForLibrary(artifactNativeLibraryPath);

            return new SharedArtifacts(
                distDirectory,
                Path.Join(qmlDirectory, "Main.qml"),
                Path.Join(qmlDirectory, "Reload.qml"),
                Path.Join(qmlDirectory, "Quit.qml"),
                artifactNativeLibraryPath,
                [serializer.Deserialize(schemaJson)],
                registrations);
        }

        private static void ConfigureQtEnvironment()
        {
            Environment.SetEnvironmentVariable("QT_QPA_PLATFORM", "offscreen");
            string qtDir = Environment.GetEnvironmentVariable("QT_DIR")
                ?? throw new InvalidOperationException("QT_DIR must point to a real Qt SDK for native integration tests.");
            string qtBin = Path.Join(qtDir, "bin");
            if (!Directory.Exists(qtBin))
            {
                throw new DirectoryNotFoundException("QT_DIR does not contain a bin directory: " + qtBin);
            }

            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string[] pathEntries = currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (!pathEntries.Any(path => string.Equals(Path.GetFullPath(path), Path.GetFullPath(qtBin), StringComparison.OrdinalIgnoreCase)))
            {
                Environment.SetEnvironmentVariable("PATH", qtBin + Path.PathSeparator + currentPath);
            }
        }

        private static string ResolveBuiltIntegrationLibrary(string repositoryRoot)
        {
            string libraryFileName = GetNativeLibraryFileName();
            string[] candidateDirectories =
            [
                Path.Join(repositoryRoot, "build", "debug", "bin", "integration"),
                Path.Join(repositoryRoot, "build", "windows-ci", "bin", "integration"),
                Path.Join(repositoryRoot, "build", "linux-ci", "bin", "integration"),
                Path.Join(repositoryRoot, "build", "macos-ci", "bin", "integration"),
                Path.Join(repositoryRoot, "build", "macos-debug", "bin", "integration"),
                Path.Join(repositoryRoot, "build", "macos-release", "bin", "integration"),
                Path.Join(repositoryRoot, "build", "release", "bin", "integration")
            ];

            string? existingCandidate = candidateDirectories
                .Select(directory => Path.Join(directory, libraryFileName))
                .FirstOrDefault(File.Exists);
            if (existingCandidate is not null)
            {
                return existingCandidate;
            }

            string candidates = string.Join(
                Environment.NewLine,
                candidateDirectories.Select(directory => "  - " + Path.Join(directory, libraryFileName)));
            throw new FileNotFoundException(
                "The native integration fixture library was not found. Build the native target before running RequiresNative tests." +
                Environment.NewLine +
                candidates);
        }

        private static void CopyFixtureFile(string fixtureRoot, string fileName, string targetDirectory)
        {
            string sourcePath = Path.Join(fixtureRoot, fileName);
            string targetPath = Path.Join(targetDirectory, fileName);
            File.Copy(sourcePath, targetPath, overwrite: true);
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

        private static string GetNativeLibraryFileName()
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

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        private sealed record SharedArtifacts(
            string DistDirectory,
            string MainQmlPath,
            string ReloadQmlPath,
            string QuitQmlPath,
            string NativeLibraryPath,
            IReadOnlyList<ViewModelSchema> Schemas,
            NativeFixtureRegistrations Registrations);
    }
}
