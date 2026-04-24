using QmlSharp.Registry.Diagnostics;
using QmlSharp.Registry.Querying;
using QmlSharp.Registry.Tests.Helpers;
using QmlSharp.Tools.GenerateRegistrySnapshot;

namespace QmlSharp.Registry.Tests.Tools
{
    public sealed class RegistrySnapshotGeneratorCommandTests
    {
        [Fact]
        public void Run_throws_for_null_arguments()
        {
            using StringWriter standardOutput = new();
            using StringWriter standardError = new();

            _ = Assert.Throws<ArgumentNullException>(() => RegistrySnapshotGeneratorCommand.Run(null!, standardOutput, standardError));
            _ = Assert.Throws<ArgumentNullException>(() => RegistrySnapshotGeneratorCommand.Run([], null!, standardError));
            _ = Assert.Throws<ArgumentNullException>(() => RegistrySnapshotGeneratorCommand.Run([], standardOutput, null!));
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void Run_help_writes_usage_to_stdout_and_returns_zero(string helpArgument)
        {
            using StringWriter standardOutput = new();
            using StringWriter standardError = new();

            int exitCode = RegistrySnapshotGeneratorCommand.Run([helpArgument], standardOutput, standardError);

            Assert.Equal(0, exitCode);
            Assert.Contains("Usage:", standardOutput.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, standardError.ToString());
        }

        [Fact]
        public void Run_missing_qt_dir_returns_one_and_writes_usage()
        {
            using StringWriter standardOutput = new();
            using StringWriter standardError = new();

            int exitCode = RegistrySnapshotGeneratorCommand.Run(["--output", "registry.snapshot"], standardOutput, standardError);

            Assert.Equal(1, exitCode);
            Assert.Equal(string.Empty, standardOutput.ToString());
            Assert.Contains("Missing required --qt-dir option.", standardError.ToString(), StringComparison.Ordinal);
            Assert.Contains("Usage:", standardError.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void Run_missing_output_returns_one_and_writes_usage()
        {
            using StringWriter standardOutput = new();
            using StringWriter standardError = new();

            int exitCode = RegistrySnapshotGeneratorCommand.Run(["--qt-dir", @"C:\Qt"], standardOutput, standardError);

            Assert.Equal(1, exitCode);
            Assert.Equal(string.Empty, standardOutput.ToString());
            Assert.Contains("Missing required --output option.", standardError.ToString(), StringComparison.Ordinal);
            Assert.Contains("Usage:", standardError.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void Run_option_without_value_returns_one_and_writes_usage()
        {
            using StringWriter standardOutput = new();
            using StringWriter standardError = new();

            int exitCode = RegistrySnapshotGeneratorCommand.Run(["--qt-dir"], standardOutput, standardError);

            Assert.Equal(1, exitCode);
            Assert.Equal(string.Empty, standardOutput.ToString());
            Assert.Contains("The --qt-dir option requires a value.", standardError.ToString(), StringComparison.Ordinal);
            Assert.Contains("Usage:", standardError.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void Run_inline_empty_option_value_returns_one_and_writes_usage()
        {
            using StringWriter standardOutput = new();
            using StringWriter standardError = new();

            int exitCode = RegistrySnapshotGeneratorCommand.Run(["--qt-dir=C:\\Qt", "--output="], standardOutput, standardError);

            Assert.Equal(1, exitCode);
            Assert.Equal(string.Empty, standardOutput.ToString());
            Assert.Contains("The --output option requires a non-empty value.", standardError.ToString(), StringComparison.Ordinal);
            Assert.Contains("Usage:", standardError.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void Run_unknown_argument_returns_one_and_writes_usage()
        {
            using StringWriter standardOutput = new();
            using StringWriter standardError = new();

            int exitCode = RegistrySnapshotGeneratorCommand.Run(["--bogus"], standardOutput, standardError);

            Assert.Equal(1, exitCode);
            Assert.Equal(string.Empty, standardOutput.ToString());
            Assert.Contains("Unknown argument '--bogus'.", standardError.ToString(), StringComparison.Ordinal);
            Assert.Contains("Usage:", standardError.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void Run_successful_build_writes_summary_and_passes_distinct_module_filters()
        {
            using TemporaryDirectory temporaryDirectory = new();
            using StringWriter standardOutput = new();
            using StringWriter standardError = new();
            QmlRegistry registry = RegistryFixtures.CreateQueryFixture();
            ImmutableArray<RegistryDiagnostic> diagnostics =
            [
                new RegistryDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.QmldirUnknownDirective, "warning text", null, null, null),
            ];

            string? capturedQtDir = null;
            string? capturedOutputPath = null;
            ImmutableArray<string> capturedFilters = default;
            string relativeOutputPath = Path.Join(temporaryDirectory.Path, "artifacts", "registry.snapshot.bin");

            int exitCode = RegistrySnapshotGeneratorCommand.Run(
                [
                    "--qt-dir", @"C:\Qt\6.11.0",
                    "--output", relativeOutputPath,
                    "--module-filter", "QtQuick, QtQuick.Controls",
                    "--module-filter", "qtquick",
                ],
                standardOutput,
                standardError,
                (qtDir, outputPath, moduleFilter) =>
                {
                    capturedQtDir = qtDir;
                    capturedOutputPath = outputPath;
                    capturedFilters = moduleFilter;

                    return new BuildResult(
                        new TestTypeRegistry(registry),
                        new RegistryQuery(registry),
                        diagnostics);
                });

            Assert.Equal(0, exitCode);
            Assert.Equal(@"C:\Qt\6.11.0", capturedQtDir);
            Assert.Equal(Path.GetFullPath(relativeOutputPath), capturedOutputPath);
            Assert.Equal(["QtQuick", "QtQuick.Controls"], capturedFilters.ToArray());
            Assert.Equal(string.Empty, standardError.ToString());

            string output = standardOutput.ToString();
            Assert.Contains($"Qt version: {registry.QtVersion}", output, StringComparison.Ordinal);
            Assert.Contains($"Registry format version: {registry.FormatVersion}", output, StringComparison.Ordinal);
            Assert.Contains($"Module count: {registry.Modules.Length}", output, StringComparison.Ordinal);
            Assert.Contains($"Type count: {registry.GetLookupIndexes().AllTypes.Length}", output, StringComparison.Ordinal);
            Assert.Contains("Warning count: 1", output, StringComparison.Ordinal);
            Assert.Contains("Error count: 0", output, StringComparison.Ordinal);
            Assert.Contains($"Output: {Path.GetFullPath(relativeOutputPath)}", output, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_failed_build_writes_sorted_diagnostics_deletes_output_and_returns_one()
        {
            using TemporaryDirectory temporaryDirectory = new();
            using StringWriter standardOutput = new();
            using StringWriter standardError = new();

            string outputPath = Path.Join(temporaryDirectory.Path, "registry.snapshot.bin");
            File.WriteAllText(outputPath, "partial snapshot");

            ImmutableArray<RegistryDiagnostic> diagnostics =
            [
                new RegistryDiagnostic(DiagnosticSeverity.Warning, DiagnosticCodes.QmldirUnknownDirective, "warn", null, null, null),
                new RegistryDiagnostic(DiagnosticSeverity.Error, DiagnosticCodes.MetatypesMissingField, "second error", null, null, null),
                new RegistryDiagnostic(DiagnosticSeverity.Error, DiagnosticCodes.QmltypesSyntaxError, "first error", null, null, null),
            ];

            int exitCode = RegistrySnapshotGeneratorCommand.Run(
                ["--qt-dir", @"C:\Qt", "--output", outputPath],
                standardOutput,
                standardError,
                (_, _, _) => new BuildResult(null, null, diagnostics));

            Assert.Equal(1, exitCode);
            Assert.Equal(string.Empty, standardOutput.ToString());
            Assert.False(File.Exists(outputPath));

            string output = standardError.ToString();
            Assert.True(output.IndexOf("Error: REG010: first error", StringComparison.Ordinal) < output.IndexOf("Error: REG031: second error", StringComparison.Ordinal));
            Assert.True(output.IndexOf("Error: REG031: second error", StringComparison.Ordinal) < output.IndexOf("Warning: REG021: warn", StringComparison.Ordinal));
            Assert.Contains("Warning count: 1", output, StringComparison.Ordinal);
            Assert.Contains("Error count: 2", output, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(typeof(IOException), "disk full")]
        [InlineData(typeof(UnauthorizedAccessException), "access denied")]
        [InlineData(typeof(ArgumentException), "bad path")]
        [InlineData(typeof(NotSupportedException), "unsupported path")]
        public void Run_builder_exceptions_are_reported_and_return_one(Type exceptionType, string message)
        {
            using TemporaryDirectory temporaryDirectory = new();
            using StringWriter standardOutput = new();
            using StringWriter standardError = new();

            int exitCode = RegistrySnapshotGeneratorCommand.Run(
                ["--qt-dir", @"C:\Qt", "--output", Path.Join(temporaryDirectory.Path, "registry.snapshot.bin")],
                standardOutput,
                standardError,
                (_, _, _) => throw CreateBuildException(exceptionType, message));

            Assert.Equal(1, exitCode);
            Assert.Equal(string.Empty, standardOutput.ToString());
            Assert.Contains($"Failed to generate registry snapshot: {message}", standardError.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void Program_main_routes_to_the_command_entry_point()
        {
            using StringWriter standardOutput = new();
            using StringWriter standardError = new();

            TextWriter originalOutput = Console.Out;
            TextWriter originalError = Console.Error;

            try
            {
                Console.SetOut(standardOutput);
                Console.SetError(standardError);

                Type programType = typeof(RegistrySnapshotGeneratorCommand).Assembly
                    .GetType("QmlSharp.Tools.GenerateRegistrySnapshot.Program", throwOnError: true)!;
                MethodInfo mainMethod = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;

                int exitCode = (int)mainMethod.Invoke(null, new object?[] { new[] { "--help" } })!;

                Assert.Equal(0, exitCode);
                Assert.Contains("Usage:", standardOutput.ToString(), StringComparison.Ordinal);
                Assert.Equal(string.Empty, standardError.ToString());
            }
            finally
            {
                Console.SetOut(originalOutput);
                Console.SetError(originalError);
            }
        }

        private static Exception CreateBuildException(Type exceptionType, string message)
        {
            if (exceptionType == typeof(IOException))
            {
                return new IOException(message);
            }

            if (exceptionType == typeof(UnauthorizedAccessException))
            {
                return new UnauthorizedAccessException(message);
            }

            if (exceptionType == typeof(ArgumentException))
            {
                return new ArgumentException(message);
            }

            if (exceptionType == typeof(NotSupportedException))
            {
                return new NotSupportedException(message);
            }

            throw new ArgumentOutOfRangeException(nameof(exceptionType), exceptionType, "Unsupported exception type.");
        }

        private sealed class TestTypeRegistry : ITypeRegistry
        {
            private readonly QmlRegistry registry;

            public TestTypeRegistry(QmlRegistry registry)
            {
                this.registry = registry;
            }

            public QmlRegistry Registry => registry;

            public IReadOnlyList<QmlModule> Modules => registry.Modules;

            public IReadOnlyList<QmlType> Types => registry.GetLookupIndexes().AllTypes;

            public string QtVersion => registry.QtVersion;

            public int FormatVersion => registry.FormatVersion;
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = Directory.CreateTempSubdirectory("qmlsharp-generate-snapshot-").FullName;
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
        }
    }
}
