using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.Pipeline
{
    public sealed class CompilerClosureTests
    {
        private static readonly string RepositoryRoot = FindRepositoryRoot();
        private static readonly string CompilerTestsRoot = Path.Join(RepositoryRoot, "tests", "QmlSharp.Compiler.Tests");

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void CompilerClosure_AllCompilerTestSpecIdsHaveImplementationEvidence()
        {
            ImmutableDictionary<string, string> evidence = ClosureEvidenceByTestId();
            ImmutableHashSet<string> requiredIds = RequiredCompilerTestIds().ToImmutableHashSet(StringComparer.Ordinal);
            ImmutableArray<string> missingIds = RequiredCompilerTestIds()
                .Where(id => !evidence.ContainsKey(id))
                .ToImmutableArray();
            string sourceText = ReadCompilerTestSources();

            Assert.Empty(missingIds);
            foreach ((string id, string token) in evidence.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                Assert.Contains(token, sourceText, StringComparison.Ordinal);
                Assert.True(requiredIds.Contains(id), $"Evidence key is not a required compiler test id: {id}");
            }
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void CompilerClosure_GoldenArtifactsArePresentAndMatchCanonicalContracts()
        {
            string goldenRoot = Path.Join(CompilerTestsRoot, "testdata", "golden");
            string counterQml = RequireGolden(goldenRoot, "CounterView.qml");
            string todoQml = RequireGolden(goldenRoot, "TodoView.qml");
            string counterSchema = RequireGolden(goldenRoot, "CounterViewModel.schema.json");
            string eventBindings = RequireGolden(goldenRoot, "event-bindings.json");

            Assert.Equal(
                ["schemaVersion", "className", "moduleName", "moduleUri", "moduleVersion", "compilerSlotKey", "properties", "commands", "effects", "lifecycle"],
                JsonPropertyNames(counterSchema).ToArray());
            Assert.Equal(["schemaVersion", "commands", "effects"], JsonPropertyNames(eventBindings).ToArray());
            AssertNoForbiddenQmlRuntimeGlue(counterQml);
            AssertNoForbiddenQmlRuntimeGlue(todoQml);
            Assert.DoesNotContain("commandId", counterQml, StringComparison.Ordinal);
            Assert.DoesNotContain("effectId", todoQml, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void CompilerClosure_DiagnosticCatalogCoversCurrentImplementationCodes()
        {
            ImmutableArray<string> codes = typeof(DiagnosticCodes)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(static field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                .Select(static field => (string)field.GetRawConstantValue()!)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();

            Assert.Equal(38, codes.Length);
            Assert.Contains(DiagnosticCodes.QtValidationFailed, codes);
            Assert.Equal(codes.ToArray(), DiagnosticMessageCatalog.AllCodes.ToArray());
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void CompilerClosure_ProductionProjectReferencesOnlyApprovedCompilerDagModules()
        {
            string projectText = File.ReadAllText(Path.Join(RepositoryRoot, "src", "QmlSharp.Compiler", "QmlSharp.Compiler.csproj"));
            ImmutableArray<string> projectReferences = Regex.Matches(projectText, "<ProjectReference Include=\"(?<path>[^\"]+)\"")
                .Select(static match => match.Groups["path"].Value.Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
            ImmutableArray<string> packageReferences = Regex.Matches(projectText, "<PackageReference Include=\"(?<name>[^\"]+)\"")
                .Select(static match => match.Groups["name"].Value)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();

            Assert.Equal(
                [
                    "../QmlSharp.Core/QmlSharp.Core.csproj",
                    "../QmlSharp.Dsl.Generator/QmlSharp.Dsl.Generator.csproj",
                    "../QmlSharp.Dsl/QmlSharp.Dsl.csproj",
                    "../QmlSharp.Qml.Ast/QmlSharp.Qml.Ast.csproj",
                    "../QmlSharp.Qml.Emitter/QmlSharp.Qml.Emitter.csproj",
                    "../QmlSharp.Qt.Tools/QmlSharp.Qt.Tools.csproj",
                    "../QmlSharp.Registry/QmlSharp.Registry.csproj",
                ],
                projectReferences.ToArray());
            Assert.DoesNotContain(projectReferences, static reference =>
                reference.Contains("Host", StringComparison.Ordinal)
                || reference.Contains("Build", StringComparison.Ordinal)
                || reference.Contains("DevTools", StringComparison.Ordinal));
            Assert.DoesNotContain(packageReferences, static reference =>
                reference.Contains("TypeScript", StringComparison.OrdinalIgnoreCase)
                || reference.Contains("Node", StringComparison.OrdinalIgnoreCase)
                || reference.Contains("Bun", StringComparison.OrdinalIgnoreCase)
                || reference.Contains("Rust", StringComparison.OrdinalIgnoreCase)
                || reference.Contains("cxx", StringComparison.OrdinalIgnoreCase));
        }

        private static ImmutableArray<string> RequiredCompilerTestIds()
        {
            return Range("CO", 1, 6)
                .AddRange(Range("CA", 1, 14))
                .AddRange(Range("VE", 1, 16))
                .AddRange(Range("DT", 1, 18))
                .AddRange(Range("ID", 1, 7))
                .AddRange(Range("IM", 1, 8))
                .AddRange(Range("PP", 1, 14))
                .AddRange(Range("CU", 1, 6))
                .AddRange(Range("SM", 1, 7))
                .AddRange(Range("DR", 1, 10))
                .AddRange(Range("IC", 1, 8))
                .AddRange(Range("CP", 1, 10))
                .AddRange(GoldenRange())
                .AddRange(Range("PF", 1, 7));
        }

        private static ImmutableDictionary<string, string> ClosureEvidenceByTestId()
        {
            Dictionary<string, string> evidence = new(StringComparer.Ordinal);

            AddRange(evidence, "CO", 1, 6, static number => $"CompilerOptions_CO{number:00}");
            AddRange(evidence, "CA", 1, 14, static number => $"CSharpAnalyzer_CA{number:00}");
            AddRange(evidence, "VE", 1, 16, static number => $"ViewModelExtractor_VE{number:00}");
            AddRange(evidence, "ID", 1, 7, static number => $"IdAllocator_ID{number:00}");
            AddRange(evidence, "IM", 1, 8, static number => $"IM_{number:00}");
            AddRange(evidence, "PP", 1, 14, static number => $"PP_{number:00}");
            AddRange(evidence, "CU", 1, 6, static number => number <= 2 ? $"CompilationUnit_CU{number:00}" : $"CU{number:00}");
            AddRange(evidence, "SM", 1, 7, static number => $"SourceMap_SM{number:00}");
            AddRange(evidence, "DR", 1, 10, static number => $"DiagnosticReporter_DR{number:00}");
            AddRange(evidence, "PF", 1, 7, static number => $"PF-{number:00}");

            Add(evidence, "DT-01", "DslTransformer_DT01");
            Add(evidence, "DT-02", "DslTransformer_DT02");
            Add(evidence, "DT-03", "DslTransformer_DT03");
            Add(evidence, "DT-04", "DslTransformer_DT04");
            Add(evidence, "DT-05", "DslTransformer_DT05_DT06");
            Add(evidence, "DT-06", "DslTransformer_DT05_DT06");
            Add(evidence, "DT-07", "DslTransformer_DT07");
            Add(evidence, "DT-08", "DslTransformer_DT08_DT09");
            Add(evidence, "DT-09", "DslTransformer_DT08_DT09");
            Add(evidence, "DT-10", "DslTransformer_DT10");
            Add(evidence, "DT-11", "DslTransformer_DT11");
            Add(evidence, "DT-12", "DslTransformer_DT12");
            Add(evidence, "DT-13", "DslTransformer_DT13_DT14");
            Add(evidence, "DT-14", "DslTransformer_DT13_DT14");
            Add(evidence, "DT-15", nameof(DiagnosticCodes.UnknownQmlType));
            Add(evidence, "DT-16", nameof(DiagnosticCodes.InvalidPropertyValue));
            Add(evidence, "DT-17", nameof(DiagnosticCodes.BindExpressionEmpty));
            Add(evidence, "DT-18", nameof(DiagnosticCodes.InvalidCallChain));

            Add(evidence, "IC-01", "IncrementalCompiler_IC01");
            Add(evidence, "IC-02", "IncrementalCompiler_IC02");
            Add(evidence, "IC-03", "IncrementalCompiler_IC03");
            Add(evidence, "IC-04", "IncrementalCompiler_IC04");
            Add(evidence, "IC-05", "GetDependenciesOf(\"CounterView.cs\")");
            Add(evidence, "IC-06", "IncrementalCompiler_IC06");
            Add(evidence, "IC-07", "IncrementalCompiler_IC07");
            Add(evidence, "IC-08", "IncrementalCompiler_IC08");

            AddRange(evidence, "CP", 1, 8, static number => $"CompilerPipeline_CP{number:00}");
            Add(evidence, "CP-09", "CompilerWatcher_CP09");
            Add(evidence, "CP-10", "CompilerWatcher_CP10");
            AddGoldenRange(evidence);

            return evidence.ToImmutableDictionary(StringComparer.Ordinal);
        }

        private static ImmutableArray<string> GoldenRange()
        {
            return Enumerable.Range(1, 4)
                .Select(static number => $"CP-G{number}")
                .ToImmutableArray();
        }

        private static ImmutableArray<string> Range(string prefix, int first, int last)
        {
            return Enumerable.Range(first, last - first + 1)
                .Select(number => $"{prefix}-{number:00}")
                .ToImmutableArray();
        }

        private static void AddGoldenRange(Dictionary<string, string> evidence)
        {
            foreach (int number in Enumerable.Range(1, 4))
            {
                Add(evidence, $"CP-G{number}", $"Golden_CPG{number}");
            }
        }

        private static void AddRange(Dictionary<string, string> evidence, string prefix, int first, int last, Func<int, string> tokenFactory)
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

        private static string ReadCompilerTestSources()
        {
            return string.Join(
                "\n",
                EnumerateCompilerTestSourceFiles()
                    .Order(StringComparer.Ordinal)
                    .Select(File.ReadAllText));
        }

        private static IEnumerable<string> EnumerateCompilerTestSourceFiles()
        {
            return Directory.EnumerateFiles(CompilerTestsRoot, "*.cs", SearchOption.AllDirectories)
                .Where(static path => !StringComparer.Ordinal.Equals(Path.GetFileName(path), nameof(CompilerClosureTests) + ".cs"))
                .Where(static path => !IsBuildOutputPath(path));
        }

        private static bool IsBuildOutputPath(string path)
        {
            return Path.GetRelativePath(CompilerTestsRoot, path)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
                .Any(static segment => StringComparer.Ordinal.Equals(segment, "bin") || StringComparer.Ordinal.Equals(segment, "obj"));
        }

        private static string RequireGolden(string goldenRoot, string fileName)
        {
            string path = Path.Join(goldenRoot, fileName);
            Assert.True(File.Exists(path), $"Missing golden file: {fileName}");
            string text = File.ReadAllText(path);
            Assert.False(string.IsNullOrWhiteSpace(text), $"Golden file is empty: {fileName}");
            Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
            return text;
        }

        private static ImmutableArray<string> JsonPropertyNames(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement
                .EnumerateObject()
                .Select(static property => property.Name)
                .ToImmutableArray();
        }

        private static void AssertNoForbiddenQmlRuntimeGlue(string qml)
        {
            Assert.DoesNotContain("__qmlts", qml, StringComparison.Ordinal);
            Assert.DoesNotContain("contextProperty", qml, StringComparison.Ordinal);
            Assert.DoesNotContain("setContextProperty", qml, StringComparison.Ordinal);
            Assert.DoesNotContain("v1Compat", qml, StringComparison.Ordinal);
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
    }
}
