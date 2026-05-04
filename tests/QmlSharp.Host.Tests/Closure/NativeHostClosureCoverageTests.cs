using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using QmlSharp.Host.ArtifactValidation;
using QmlSharp.Host.Interop;

namespace QmlSharp.Host.Tests.Closure
{
    public sealed partial class NativeHostClosureCoverageTests
    {
        private static readonly string RepositoryRoot = FindRepositoryRoot();

        [Fact]
        public void Closure_AllNativeHostFunctionalSpecIds_HaveImplementationEvidence()
        {
            ImmutableDictionary<string, string> evidence = ClosureEvidenceByTestId();
            ImmutableHashSet<string> requiredIds = RequiredFunctionalTestIds().ToImmutableHashSet(StringComparer.Ordinal);
            ImmutableArray<string> missingIds = requiredIds
                .Where(id => !evidence.ContainsKey(id))
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
            string sourceText = ReadClosureSource();

            Assert.Empty(missingIds);
            Assert.Equal(97, requiredIds.Count);
            foreach ((string id, string token) in evidence.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                Assert.True(requiredIds.Contains(id), $"Evidence key is not a required native-host test id: {id}");
                Assert.Contains(token, sourceText, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Closure_AllNativeHostPerformanceBudgets_AreMeasured()
        {
            string sourceText = ReadClosureSource();

            foreach (string id in Range("PRF", 1, 8))
            {
                Assert.Contains(id, sourceText, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Closure_CAbiHeaderAndManagedInteropExports_MatchExactly()
        {
            string headerText = File.ReadAllText(Path.Join(RepositoryRoot, "native", "include", "qmlsharp", "qmlsharp_abi.h"));
            string interopText = File.ReadAllText(Path.Join(RepositoryRoot, "src", "QmlSharp.Host", "Interop", "NativeHostLibrary.cs"));
            ImmutableArray<string> headerExports = CAbiExportRegex()
                .Matches(headerText)
                .Select(static match => match.Groups["name"].Value)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
            ImmutableArray<string> managedExports = ManagedExportStringRegex()
                .Matches(interopText)
                .Select(static match => match.Groups["name"].Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();

            Assert.Equal(ExpectedCAbiExports().ToArray(), headerExports.ToArray());
            Assert.Equal(ExpectedCAbiExports().ToArray(), managedExports.ToArray());
            Assert.Contains("#define QMLSHARP_ABI_VERSION 1", headerText, StringComparison.Ordinal);
            Assert.Equal(1, NativeHostAbi.SupportedAbiVersion);
        }

        [Fact]
        public void Closure_NineCAbiRegions_HaveExportCoverage()
        {
            string headerText = File.ReadAllText(Path.Join(RepositoryRoot, "native", "include", "qmlsharp", "qmlsharp_abi.h"));
            ImmutableHashSet<string> headerExports = CAbiExportRegex()
                .Matches(headerText)
                .Select(static match => match.Groups["name"].Value)
                .ToImmutableHashSet(StringComparer.Ordinal);
            ImmutableDictionary<string, ImmutableArray<string>> regions = CAbiRegions();
            ImmutableArray<string> commonUtilities = ["qmlsharp_free_string", "qmlsharp_get_last_error"];
            ImmutableArray<string> coveredExports = regions
                .Values
                .SelectMany(static exports => exports)
                .Concat(commonUtilities)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();

            Assert.Equal(9, regions.Count);
            foreach (KeyValuePair<string, ImmutableArray<string>> region in regions.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                Assert.NotEmpty(region.Value);
                foreach (string exportName in region.Value)
                {
                    Assert.Contains(exportName, headerExports);
                }
            }

            Assert.Equal(ExpectedCAbiExports().ToArray(), coveredExports.ToArray());
        }

        [Fact]
        public void Closure_ArtifactValidationDiagnostics_HaveCodeAndTestCoverage()
        {
            ImmutableArray<string> codes = typeof(ArtifactValidationCodes)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(static field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                .Select(static field => (string)field.GetRawConstantValue()!)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
            string artifactValidatorTests = File.ReadAllText(Path.Join(
                RepositoryRoot,
                "tests",
                "QmlSharp.Host.Tests",
                "ArtifactValidation",
                "ArtifactValidatorTests.cs"));

            Assert.Equal(["AV-001", "AV-002", "AV-003", "AV-004", "AV-005", "AV-006", "AV-007"], codes.ToArray());
            foreach (string codeFieldName in new[]
            {
                nameof(ArtifactValidationCodes.ManifestMissing),
                nameof(ArtifactValidationCodes.NativeLibraryMissing),
                nameof(ArtifactValidationCodes.AbiVersionMismatch),
                nameof(ArtifactValidationCodes.SchemaMissing),
                nameof(ArtifactValidationCodes.EventBindingsMissing),
                nameof(ArtifactValidationCodes.EventBindingCommandMissing),
                nameof(ArtifactValidationCodes.SchemaRegistrationFailed),
            })
            {
                Assert.Contains("ArtifactValidationCodes." + codeFieldName, artifactValidatorTests, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Closure_HostProductionProject_KeepsApprovedDependencyBoundary()
        {
            string projectPath = Path.Join(RepositoryRoot, "src", "QmlSharp.Host", "QmlSharp.Host.csproj");
            XDocument project = XDocument.Load(projectPath);
            ImmutableArray<string> projectReferences = project
                .Descendants("ProjectReference")
                .Select(static reference => GetProjectReferenceName((string?)reference.Attribute("Include") ?? string.Empty))
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
            ImmutableArray<string> packageReferences = project
                .Descendants("PackageReference")
                .Select(static reference => (string?)reference.Attribute("Include") ?? string.Empty)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();

            Assert.Equal(["QmlSharp.Compiler"], projectReferences.ToArray());
            Assert.Empty(packageReferences);

            string sourceText = string.Join(
                "\n",
                Directory.EnumerateFiles(Path.Join(RepositoryRoot, "src", "QmlSharp.Host"), "*.cs", SearchOption.AllDirectories)
                    .Order(StringComparer.Ordinal)
                    .Select(File.ReadAllText));
            string[] forbiddenTerms =
            [
                "QmlSharp.Build",
                "QmlSharp.Cli",
                "QmlSharp.DevTools",
                "TypeScript",
                "node_modules",
                "npm",
                "Bun",
                "Rust",
                "napi-rs",
                "cxx-qt",
            ];

            foreach (string forbiddenTerm in forbiddenTerms)
            {
                Assert.DoesNotContain(forbiddenTerm, sourceText, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Closure_GeneratedNativeFixtures_AreTestOnlyAndProductionGeneratedCppIsNotCommitted()
        {
            string nativeRoot = Path.Join(RepositoryRoot, "native");
            Assert.False(Directory.Exists(Path.Join(nativeRoot, "generated")));

            ImmutableArray<string> fixtureHeaders = Directory
                .EnumerateFiles(Path.Join(nativeRoot, "tests", "fixtures"), "*.h", SearchOption.TopDirectoryOnly)
                .Where(static path => Path.GetFileName(path).StartsWith("Registration", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();

            Assert.NotEmpty(fixtureHeaders);
            foreach (string fixtureHeader in fixtureHeaders)
            {
                Assert.Contains(
                    "Generated-test-only QObject fixture",
                    File.ReadAllText(fixtureHeader),
                    StringComparison.Ordinal);
            }
        }

        private static ImmutableArray<string> RequiredFunctionalTestIds()
        {
            return Range("ENG", 1, 6)
                .AddRange(Range("REG", 1, 5))
                .AddRange(Range("INS", 1, 7))
                .AddRange(Range("SSY", 1, 10))
                .AddRange(Range("CMD", 1, 6))
                .AddRange(Range("EFF", 1, 5))
                .AddRange(Range("HRL", 1, 8))
                .AddRange(Range("PIN", 1, 8))
                .AddRange(Range("IRG", 1, 10))
                .AddRange(Range("SYN", 1, 8))
                .AddRange(Range("CRT", 1, 6))
                .AddRange(Range("ERT", 1, 5))
                .AddRange(Range("HRC", 1, 6))
                .AddRange(Range("INT", 1, 7));
        }

        private static ImmutableDictionary<string, string> ClosureEvidenceByTestId()
        {
            Dictionary<string, string> evidence = new(StringComparer.Ordinal);

            AddRange(evidence, "ENG", 1, 6, static number => $"ENG-{number:00}");
            AddRange(evidence, "REG", 1, 5, static number => $"REG-{number:00}");
            AddRange(evidence, "INS", 1, 7, static number => $"INS-{number:00}");
            AddRange(evidence, "SSY", 1, 10, static number => $"SSY-{number:00}");
            AddRange(evidence, "CMD", 1, 6, static number => $"CMD-{number:00}");
            AddRange(evidence, "EFF", 1, 5, static number => $"EFF-{number:00}");
            AddRange(evidence, "HRL", 1, 2, static number => $"HRL-{number:00}");
            Add(evidence, "HRL-03", "HRL-03/04");
            Add(evidence, "HRL-04", "HRL-03/04");
            AddRange(evidence, "HRL", 5, 8, static number => $"HRL-{number:00}");
            AddRange(evidence, "PIN", 1, 8, static number => $"PIN_{number:00}");

            Add(evidence, "IRG-01", "OnInstanceCreated_ValidRegistration_AddsEntry");
            Add(evidence, "IRG-02", "OnInstanceCreated_ValidRegistration_SetsStateToPending");
            Add(evidence, "IRG-03", "MarkReady_KnownInstance_TransitionsToActive");
            Add(evidence, "IRG-04", "MarkReady_UnknownInstanceId_NoException");
            Add(evidence, "IRG-05", "OnInstanceDestroyed_KnownInstance_RemovesEntry");
            Add(evidence, "IRG-06", "FindBySlotKey_KnownClassAndSlot_ReturnsMatchingInstance");
            Add(evidence, "IRG-07", "FindBySlotKey_UnknownClassAndSlot_ReturnsNull");
            Add(evidence, "IRG-08", "GetAll_MultipleActiveInstances_ReturnsAllActiveInstances");
            Add(evidence, "IRG-09", "FindByClassName_MultipleInstances_ReturnsMatchingInstances");
            Add(evidence, "IRG-10", "CaptureInstanceSnapshots_InstancesWithState_IncludesCurrentPropertyValues");

            Add(evidence, "SYN-01", "Push_IntValue_CallsSyncStateIntAndUpdatesSnapshot");
            Add(evidence, "SYN-02", "Push_DoubleValue_CallsSyncStateDoubleAndUpdatesSnapshot");
            Add(evidence, "SYN-03", "Push_BoolValue_CallsSyncStateBoolAndUpdatesSnapshot");
            Add(evidence, "SYN-04", "Push_StringValue_CallsSyncStateStringAndUpdatesSnapshot");
            Add(evidence, "SYN-05", "Push_ComplexObject_FallsBackToJson");
            Add(evidence, "SYN-06", "PushBatch_MultipleValues_CallsSyncStateBatchAndUpdatesSnapshotOnce");
            Add(evidence, "SYN-07", "Push_FromManagedThread_MarshalsThroughInteropDecisionPoint");
            Add(evidence, "SYN-08", "Push_GenericScalarValues_SelectsTypedFastPaths");

            Add(evidence, "CRT-01", "OnCommand_RegisteredHandler_DispatchesInvocation");
            Add(evidence, "CRT-02", "OnCommand_UnknownInstance_ReturnsStructuredError");
            Add(evidence, "CRT-03", "RegisterCommandHandler_SpecificCommand_RoutesOnlyMatchingCommand");
            Add(evidence, "CRT-04", "OnCommand_PendingInstance_QueuesUntilReady");
            Add(evidence, "CRT-05", "MarkReady_MultipleQueuedCommands_FlushesFifoExactlyOnce");
            Add(evidence, "CRT-06", "OnCommand_DestroyedInstance_ReturnsStructuredUnknownInstance");

            Add(evidence, "ERT-01", "Dispatch_RegisteredEffect_CallsNativeDispatch");
            Add(evidence, "ERT-02", "Dispatch_NullPayload_UsesEmptyObjectJson");
            Add(evidence, "ERT-03", "Broadcast_RegisteredEffect_CallsNativeBroadcast");
            Add(evidence, "ERT-04", "Dispatch_FromManagedThread_MarshalsThroughInteropDecisionPoint");
            Add(evidence, "ERT-05", "Dispatch_ComplexPayload_SerializesValidJson");

            Add(evidence, "HRC-01", "ReloadAsync_SuccessfulReload_ExecutesFourStepsInOrder");
            Add(evidence, "HRC-02", "ReloadAsync_SuccessfulReload_ExecutesFourStepsInOrder");
            Add(evidence, "HRC-03", "ReloadAsync_DisposedInstanceDuringReload_ReportsOrphanedSnapshot");
            Add(evidence, "HRC-04", "ReloadAsync_SuccessAndFailure_UpdateRuntimeMetrics");
            Add(evidence, "HRC-05", "ReloadAsync_NativeReloadFailure_PreservesStableManagedState");
            Add(evidence, "HRC-06", "ReloadAsync_ClassRename_DoesNotPreserveStateAcrossCompositeKeyMismatch");

            AddRange(evidence, "INT", 1, 7, static number => $"INT_{number:00}");

            return evidence.ToImmutableDictionary(StringComparer.Ordinal);
        }

        private static ImmutableArray<string> ExpectedCAbiExports()
        {
            return
            [
                "qmlsharp_broadcast_effect",
                "qmlsharp_capture_snapshot",
                "qmlsharp_dispatch_effect",
                "qmlsharp_engine_exec",
                "qmlsharp_engine_init",
                "qmlsharp_engine_shutdown",
                "qmlsharp_free_string",
                "qmlsharp_get_abi_version",
                "qmlsharp_get_all_instances",
                "qmlsharp_get_instance_info",
                "qmlsharp_get_last_error",
                "qmlsharp_get_metrics",
                "qmlsharp_hide_error",
                "qmlsharp_instance_ready",
                "qmlsharp_post_to_main_thread",
                "qmlsharp_register_module",
                "qmlsharp_register_type",
                "qmlsharp_reload_qml",
                "qmlsharp_restore_snapshot",
                "qmlsharp_set_command_callback",
                "qmlsharp_set_instance_callbacks",
                "qmlsharp_show_error",
                "qmlsharp_sync_state_batch",
                "qmlsharp_sync_state_bool",
                "qmlsharp_sync_state_double",
                "qmlsharp_sync_state_int",
                "qmlsharp_sync_state_json",
                "qmlsharp_sync_state_string",
            ];
        }

        private static ImmutableDictionary<string, ImmutableArray<string>> CAbiRegions()
        {
            return new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
            {
                ["Region 1: Engine Lifecycle"] =
                [
                    "qmlsharp_engine_exec",
                    "qmlsharp_engine_init",
                    "qmlsharp_engine_shutdown",
                    "qmlsharp_get_abi_version",
                    "qmlsharp_post_to_main_thread",
                ],
                ["Region 2: Type Registration"] =
                [
                    "qmlsharp_register_module",
                    "qmlsharp_register_type",
                ],
                ["Region 3: Instance Management"] =
                [
                    "qmlsharp_instance_ready",
                    "qmlsharp_set_instance_callbacks",
                ],
                ["Region 4: State Sync"] =
                [
                    "qmlsharp_sync_state_batch",
                    "qmlsharp_sync_state_bool",
                    "qmlsharp_sync_state_double",
                    "qmlsharp_sync_state_int",
                    "qmlsharp_sync_state_json",
                    "qmlsharp_sync_state_string",
                ],
                ["Region 5: Command Dispatch"] =
                [
                    "qmlsharp_set_command_callback",
                ],
                ["Region 6: Effect Dispatch"] =
                [
                    "qmlsharp_broadcast_effect",
                    "qmlsharp_dispatch_effect",
                ],
                ["Region 7: Hot Reload"] =
                [
                    "qmlsharp_capture_snapshot",
                    "qmlsharp_reload_qml",
                    "qmlsharp_restore_snapshot",
                ],
                ["Region 8: Error Overlay"] =
                [
                    "qmlsharp_hide_error",
                    "qmlsharp_show_error",
                ],
                ["Region 9: Dev Tools / Diagnostics"] =
                [
                    "qmlsharp_get_all_instances",
                    "qmlsharp_get_instance_info",
                    "qmlsharp_get_metrics",
                ],
            }.ToImmutableDictionary(StringComparer.Ordinal);
        }

        private static ImmutableArray<string> Range(string prefix, int first, int last)
        {
            return Enumerable.Range(first, last - first + 1)
                .Select(number => $"{prefix}-{number:00}")
                .ToImmutableArray();
        }

        private static void AddRange(
            Dictionary<string, string> evidence,
            string prefix,
            int first,
            int last,
            Func<int, string> tokenFactory)
        {
            foreach (int number in Enumerable.Range(first, last - first + 1))
            {
                Add(evidence, $"{prefix}-{number:00}", tokenFactory(number));
            }
        }

        private static void Add(Dictionary<string, string> evidence, string id, string token)
        {
            evidence.Add(id, token);
        }

        private static string ReadClosureSource()
        {
            string closureFileName = nameof(NativeHostClosureCoverageTests) + ".cs";
            IEnumerable<string> files = Directory
                .EnumerateFiles(Path.Join(RepositoryRoot, "tests", "QmlSharp.Host.Tests"), "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(Path.Join(RepositoryRoot, "tests", "QmlSharp.Integration.Tests"), "*.cs", SearchOption.AllDirectories))
                .Concat(Directory.EnumerateFiles(Path.Join(RepositoryRoot, "native", "tests"), "*.cpp", SearchOption.AllDirectories))
                .Where(path => !StringComparer.Ordinal.Equals(Path.GetFileName(path), closureFileName))
                .Where(static path => !IsBuildOutputPath(path));

            return string.Join("\n", files.Order(StringComparer.Ordinal).Select(File.ReadAllText));
        }

        private static bool IsBuildOutputPath(string path)
        {
            return Path.GetRelativePath(RepositoryRoot, path)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
                .Any(static segment => StringComparer.Ordinal.Equals(segment, "bin") || StringComparer.Ordinal.Equals(segment, "obj"));
        }

        private static string GetProjectReferenceName(string include)
        {
            string fileName = include
                .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault() ?? string.Empty;

            return Path.GetFileNameWithoutExtension(fileName);
        }

        private static string FindRepositoryRoot()
        {
            string? directory = AppContext.BaseDirectory;
            while (directory is not null)
            {
                if (File.Exists(Path.Join(directory, "QmlSharp.slnx")))
                {
                    return directory;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }

            throw new DirectoryNotFoundException("Unable to locate QmlSharp repository root.");
        }

        [GeneratedRegex("QMLSHARP_API\\s+(?:const\\s+char\\*|int32_t|void\\*|void)\\s+QMLSHARP_CALL\\s+(?<name>qmlsharp_[A-Za-z0-9_]+)")]
        private static partial Regex CAbiExportRegex();

        [GeneratedRegex("\"(?<name>qmlsharp_[A-Za-z0-9_]+)\"")]
        private static partial Regex ManagedExportStringRegex();
    }
}
