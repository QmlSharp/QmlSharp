using System.Runtime.InteropServices;
using QmlSharp.Build.Tests.Infrastructure;

namespace QmlSharp.Build.Tests
{
    public sealed class PlatformDistributorTests
    {
        [Fact]
        public void PD01_GetNativeLibExtension_Windows_ReturnsDll()
        {
            PlatformDistributor distributor = new();

            string extension = distributor.GetNativeLibExtension(PlatformTarget.WindowsX64);

            Assert.Equal(".dll", extension);
        }

        [Fact]
        public void PD02_GetNativeLibExtension_Linux_ReturnsSo()
        {
            PlatformDistributor distributor = new();

            string extension = distributor.GetNativeLibExtension(PlatformTarget.LinuxX64);

            Assert.Equal(".so", extension);
        }

        [Fact]
        public void PD03_GetNativeLibExtension_MacOs_ReturnsDylib()
        {
            PlatformDistributor distributor = new();

            string armExtension = distributor.GetNativeLibExtension(PlatformTarget.MacOsArm64);
            string x64Extension = distributor.GetNativeLibExtension(PlatformTarget.MacOsX64);

            Assert.Equal(".dylib", armExtension);
            Assert.Equal(".dylib", x64Extension);
        }

        [Fact]
        public void PD04_GetQtRuntimeDependencies_WindowsQtQuick_ReturnsCoreQmlQuick()
        {
            PlatformDistributor distributor = new();

            ImmutableArray<string> dependencies = distributor.GetQtRuntimeDependencies(
                PlatformTarget.WindowsX64,
                ImmutableArray.Create("QtQuick"));

            Assert.Contains("Qt6Core.dll", dependencies);
            Assert.Contains("Qt6Qml.dll", dependencies);
            Assert.Contains("Qt6Quick.dll", dependencies);
            Assert.Contains("Qt6Gui.dll", dependencies);
            Assert.True(dependencies.SequenceEqual(dependencies.OrderBy(static item => item, StringComparer.Ordinal)));
        }

        [Fact]
        public void PD05_PackageIncludesRequiredFilesAndTotalSize()
        {
            using TempDirectory project = BuildTestFixtures.CreateFixtureProject("pd05-package");
            BuildContext context = CreateDistributionFixture(project.Path);
            BuildResult buildResult = BuildTestFixtures.CreateSuccessfulBuildResult() with
            {
                Artifacts = new BuildArtifacts
                {
                    AssemblyPath = Path.Join(context.OutputDir, "managed", "MyApp.dll"),
                    NativeLibraryPath = Path.Join(context.OutputDir, "native", GetNativeLibraryFileName()),
                },
            };
            PlatformDistributor distributor = new();

            DistributionResult result = distributor.Package(buildResult, context);

            Assert.True(result.Success);
            Assert.True(Directory.Exists(result.OutputPath));
            Assert.Contains("managed/MyApp.dll", result.IncludedFiles);
            Assert.Contains("native/" + GetNativeLibraryFileName(), result.IncludedFiles);
            Assert.Contains("qml/QmlSharp/MyApp/CounterView.qml", result.IncludedFiles);
            Assert.Contains("assets/icon.png", result.IncludedFiles);
            Assert.True(result.IncludedFiles.SequenceEqual(result.IncludedFiles.OrderBy(static item => item, StringComparer.Ordinal)));
            Assert.Equal(ComputeSize(result.OutputPath, result.IncludedFiles), result.TotalSizeBytes);
        }

        private static BuildContext CreateDistributionFixture(string projectDir)
        {
            QmlSharpConfig config = BuildTestFixtures.CreateDefaultConfig() with
            {
                OutDir = Path.Join(projectDir, "dist"),
                Qt = new QtConfig
                {
                    Dir = Path.Join(projectDir, "Qt"),
                    Modules = ImmutableArray.Create("QtQuick"),
                },
            };
            BuildContext context = BuildTestFixtures.CreateDefaultContext(projectDir) with
            {
                Config = config,
                OutputDir = config.OutDir,
                QtDir = config.Qt.Dir ?? string.Empty,
            };
            WriteFile(Path.Join(context.OutputDir, "managed", "MyApp.dll"), "managed");
            WriteFile(Path.Join(context.OutputDir, "native", GetNativeLibraryFileName()), "native");
            WriteFile(Path.Join(context.OutputDir, "qml", "QmlSharp", "MyApp", "CounterView.qml"), "Item {}\n");
            WriteFile(Path.Join(context.OutputDir, "schemas", "CounterViewModel.schema.json"), "{}\n");
            WriteFile(Path.Join(context.OutputDir, "assets", "icon.png"), "asset");
            WriteFile(Path.Join(context.OutputDir, "manifest.json"), "{}\n");
            WriteFile(Path.Join(context.OutputDir, "event-bindings.json"), "{}\n");
            WriteFile(Path.Join(context.OutputDir + "-" + GetCurrentTargetMoniker(), "stale.txt"), "stale");
            return context;
        }

        private static void WriteFile(string path, string content)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, content);
        }

        private static long ComputeSize(string outputPath, ImmutableArray<string> includedFiles)
        {
            return includedFiles
                .Select(path => Path.Join(outputPath, path.Replace('/', Path.DirectorySeparatorChar)))
                .Sum(static path => new FileInfo(path).Length);
        }

        private static string GetNativeLibraryFileName()
        {
            if (OperatingSystem.IsWindows())
            {
                return "qmlsharp_native.dll";
            }

            if (OperatingSystem.IsMacOS())
            {
                return "libqmlsharp_native.dylib";
            }

            return "libqmlsharp_native.so";
        }

        private static string GetCurrentTargetMoniker()
        {
            if (OperatingSystem.IsWindows())
            {
                return "windows-x64";
            }

            if (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                return "macos-arm64";
            }

            if (OperatingSystem.IsMacOS())
            {
                return "macos-x64";
            }

            return "linux-x64";
        }
    }
}
