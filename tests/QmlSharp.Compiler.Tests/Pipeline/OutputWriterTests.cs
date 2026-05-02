using System.Text.Json;
using QmlSharp.Compiler.Tests.Fixtures;

namespace QmlSharp.Compiler.Tests.Pipeline
{
    public sealed class OutputWriterTests
    {
        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void OutputWriter_CP03_WriteOutputCreatesAllArtifactTypes()
        {
            using TempOutputDirectory temp = new();
            CompilerOptions options = CreateOptions(temp.Path);
            CompilationResult compilation = CompilationResult.FromUnits(
                ImmutableArray.Create(CreateCounterUnit()),
                eventBindings: new EventBindingsBuilder().Build(ImmutableArray.Create(CompilerTestFixtures.CreateCounterSchema())));

            OutputResult result = new CompilerOutputWriter().WriteOutput(compilation, options);

            Assert.True(result.Success);
            Assert.Empty(result.Diagnostics);
            string qmlPath = Path.Join(temp.Path, "qml", "QmlSharp", "TestApp", "CounterView.qml");
            string schemaPath = Path.Join(temp.Path, "schemas", "CounterViewModel.schema.json");
            string eventBindingsPath = Path.Join(temp.Path, "event-bindings.json");
            string sourceMapPath = Path.Join(temp.Path, "source-maps", "CounterView.qml.map");
            Assert.Equal(new[] { qmlPath }, result.QmlFiles);
            Assert.Equal(new[] { schemaPath }, result.SchemaFiles);
            Assert.Equal(eventBindingsPath, result.EventBindingsFile);
            Assert.Equal(new[] { sourceMapPath }, result.SourceMapFiles);
            Assert.True(File.Exists(qmlPath));
            Assert.True(File.Exists(schemaPath));
            Assert.True(File.Exists(eventBindingsPath));
            Assert.True(File.Exists(sourceMapPath));
            Assert.True(result.TotalBytes > 0);

            using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
            Assert.Equal("CounterViewModel", schema.RootElement.GetProperty("className").GetString());
            using JsonDocument eventBindings = JsonDocument.Parse(File.ReadAllText(eventBindingsPath));
            Assert.Equal("1.0", eventBindings.RootElement.GetProperty("schemaVersion").GetString());
            using JsonDocument sourceMap = JsonDocument.Parse(File.ReadAllText(sourceMapPath));
            Assert.Equal("CounterView.cs", sourceMap.RootElement.GetProperty("sourceFilePath").GetString());
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void OutputWriter_SkipsQmlForFailedUnitsButWritesSuccessfulUnits()
        {
            using TempOutputDirectory temp = new();
            CompilerOptions options = CreateOptions(temp.Path);
            CompilationUnit failedUnit = CreateCounterUnit() with
            {
                SourceFilePath = "BrokenView.cs",
                ViewClassName = "BrokenView",
                Schema = CompilerTestFixtures.CreateCounterSchema() with { ClassName = "BrokenViewModel" },
                Diagnostics = ImmutableArray.Create(new CompilerDiagnostic(
                    DiagnosticCodes.EmitFailed,
                    DiagnosticSeverity.Error,
                    "QML emission failed.",
                    SourceLocation.FileOnly("BrokenView.cs"),
                    "EmittingQml")),
            };
            CompilationResult compilation = CompilationResult.FromUnits(ImmutableArray.Create(CreateCounterUnit(), failedUnit));

            OutputResult result = new CompilerOutputWriter().WriteOutput(compilation, options);

            Assert.True(result.Success);
            Assert.Equal(new[] { Path.Join(temp.Path, "qml", "QmlSharp", "TestApp", "CounterView.qml") }, result.QmlFiles);
            Assert.False(File.Exists(Path.Join(temp.Path, "qml", "QmlSharp", "TestApp", "BrokenView.qml")));
            Assert.Equal(2, result.SchemaFiles.Length);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void OutputWriter_FailedUnitDeletesStaleQmlAndSourceMapArtifacts()
        {
            using TempOutputDirectory temp = new();
            CompilerOptions options = CreateOptions(temp.Path);
            string qmlPath = Path.Join(temp.Path, "qml", "QmlSharp", "TestApp", "BrokenView.qml");
            string sourceMapPath = Path.Join(temp.Path, "source-maps", "BrokenView.qml.map");
            _ = Directory.CreateDirectory(Path.GetDirectoryName(qmlPath)!);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(sourceMapPath)!);
            File.WriteAllText(qmlPath, "stale qml");
            File.WriteAllText(sourceMapPath, "stale map");
            CompilationUnit failedUnit = CreateCounterUnit() with
            {
                SourceFilePath = "BrokenView.cs",
                ViewClassName = "BrokenView",
                ViewModelClassName = "BrokenViewModel",
                QmlText = "partial qml must not survive\n",
                Schema = null,
                Diagnostics = ImmutableArray.Create(new CompilerDiagnostic(
                    DiagnosticCodes.UnknownQmlType,
                    DiagnosticSeverity.Error,
                    "Unknown type.",
                    SourceLocation.FileOnly("BrokenView.cs"),
                    "TransformingDsl")),
            };
            CompilationResult compilation = CompilationResult.FromUnits(ImmutableArray.Create(failedUnit));

            OutputResult result = new CompilerOutputWriter().WriteOutput(compilation, options);

            Assert.True(result.Success);
            Assert.Empty(result.QmlFiles);
            Assert.Empty(result.SourceMapFiles);
            Assert.False(File.Exists(qmlPath));
            Assert.False(File.Exists(sourceMapPath));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void OutputWriter_FailedDuplicateViewClassDoesNotDeleteSuccessfulArtifacts()
        {
            using TempOutputDirectory temp = new();
            CompilerOptions options = CreateOptions(temp.Path);
            CompilationUnit successfulUnit = CreateCounterUnit() with
            {
                SourceFilePath = "A/CounterView.cs",
                QmlText = "Item { objectName: \"successful\" }\n",
            };
            CompilationUnit failedUnit = CreateCounterUnit() with
            {
                SourceFilePath = "Z/CounterView.cs",
                ViewClassName = "CounterView",
                ViewModelClassName = "BrokenCounterViewModel",
                QmlText = "partial qml must not delete the successful artifact\n",
                Schema = null,
                SourceMap = null,
                Diagnostics = ImmutableArray.Create(new CompilerDiagnostic(
                    DiagnosticCodes.UnknownQmlType,
                    DiagnosticSeverity.Error,
                    "Unknown type.",
                    SourceLocation.FileOnly("Z/CounterView.cs"),
                    "TransformingDsl")),
            };
            CompilationResult compilation = CompilationResult.FromUnits(ImmutableArray.Create(successfulUnit, failedUnit));

            OutputResult result = new CompilerOutputWriter().WriteOutput(compilation, options);

            string qmlPath = Path.Join(temp.Path, "qml", "QmlSharp", "TestApp", "CounterView.qml");
            string sourceMapPath = Path.Join(temp.Path, "source-maps", "CounterView.qml.map");
            Assert.True(result.Success);
            Assert.Empty(result.Diagnostics);
            Assert.Equal(new[] { qmlPath }, result.QmlFiles);
            Assert.Equal(new[] { sourceMapPath }, result.SourceMapFiles);
            Assert.True(File.Exists(qmlPath));
            Assert.True(File.Exists(sourceMapPath));
            Assert.Contains("successful", File.ReadAllText(qmlPath), StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void OutputWriter_WritesSchemaOnlyUnitWithoutQmlArtifact()
        {
            using TempOutputDirectory temp = new();
            CompilerOptions options = CreateOptions(temp.Path);
            CompilationUnit unit = CreateCounterUnit() with
            {
                QmlText = string.Empty,
                SourceMap = null,
            };
            CompilationResult compilation = CompilationResult.FromUnits(ImmutableArray.Create(unit));

            OutputResult result = new CompilerOutputWriter().WriteOutput(compilation, options);

            Assert.Empty(result.QmlFiles);
            _ = Assert.Single(result.SchemaFiles);
            Assert.True(File.Exists(Path.Join(temp.Path, "schemas", "CounterViewModel.schema.json")));
            Assert.False(File.Exists(Path.Join(temp.Path, "qml", "QmlSharp", "TestApp", "CounterView.qml")));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void OutputWriter_UsesDeterministicCanonicalFilePaths()
        {
            using TempOutputDirectory temp = new();
            CompilerOptions options = CreateOptions(temp.Path);
            CompilationUnit todoUnit = CreateTodoUnit();
            CompilationUnit counterUnit = CreateCounterUnit();
            CompilationResult compilation = CompilationResult.FromUnits(
                ImmutableArray.Create(todoUnit, counterUnit),
                eventBindings: new EventBindingsBuilder().Build(ImmutableArray.Create(todoUnit.Schema!, counterUnit.Schema!)));

            OutputResult first = new CompilerOutputWriter().WriteOutput(compilation, options);
            OutputResult second = new CompilerOutputWriter().WriteOutput(compilation, options);

            string[] expectedQml =
            [
                Path.Join(temp.Path, "qml", "QmlSharp", "TestApp", "CounterView.qml"),
                Path.Join(temp.Path, "qml", "QmlSharp", "TestApp", "TodoView.qml"),
            ];
            string[] expectedSchemas =
            [
                Path.Join(temp.Path, "schemas", "CounterViewModel.schema.json"),
                Path.Join(temp.Path, "schemas", "TodoViewModel.schema.json"),
            ];
            Assert.Equal(expectedQml, first.QmlFiles);
            Assert.Equal(expectedSchemas, first.SchemaFiles);
            Assert.Equal(first.QmlFiles.ToArray(), second.QmlFiles.ToArray());
            Assert.Equal(first.SchemaFiles.ToArray(), second.SchemaFiles.ToArray());
            Assert.Equal(first.SourceMapFiles.ToArray(), second.SourceMapFiles.ToArray());
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void OutputWriter_RejectsUnsafeModuleUriBeforeWritingArtifacts()
        {
            using TempOutputDirectory temp = new();
            CompilerOptions options = CreateOptions(temp.Path) with
            {
                ModuleUriPrefix = "../escape",
            };
            CompilationResult compilation = CompilationResult.FromUnits(ImmutableArray.Create(CreateCounterUnit()));

            OutputResult result = new CompilerOutputWriter().WriteOutput(compilation, options);

            CompilerDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.False(result.Success);
            Assert.Equal(DiagnosticCodes.OutputWriteFailed, diagnostic.Code);
            Assert.Contains("Module URI prefix", diagnostic.Message, StringComparison.Ordinal);
            Assert.Empty(result.QmlFiles);
            Assert.Empty(result.SchemaFiles);
            Assert.Empty(result.SourceMapFiles);
            Assert.False(File.Exists(Path.Join(temp.Path, "event-bindings.json")));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void OutputWriter_DetectsArtifactPathCollisionsBeforeWritingArtifacts()
        {
            using TempOutputDirectory temp = new();
            CompilerOptions options = CreateOptions(temp.Path);
            CompilationUnit first = CreateCounterUnit() with
            {
                SourceFilePath = "A/CounterView.cs",
                QmlText = "Item { objectName: \"first\" }\n",
                Schema = CompilerTestFixtures.CreateCounterSchema() with { ClassName = "FirstViewModel" },
                SourceMap = null,
            };
            CompilationUnit second = CreateCounterUnit() with
            {
                SourceFilePath = "B/CounterView.cs",
                QmlText = "Item { objectName: \"second\" }\n",
                Schema = CompilerTestFixtures.CreateCounterSchema() with { ClassName = "SecondViewModel" },
                SourceMap = null,
            };
            CompilationResult compilation = CompilationResult.FromUnits(ImmutableArray.Create(first, second));

            OutputResult result = new CompilerOutputWriter().WriteOutput(compilation, options);

            CompilerDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.False(result.Success);
            Assert.Equal(DiagnosticCodes.OutputWriteFailed, diagnostic.Code);
            Assert.Contains("Artifact path collision", diagnostic.Message, StringComparison.Ordinal);
            Assert.Empty(result.QmlFiles);
            Assert.Empty(result.SchemaFiles);
            Assert.False(File.Exists(Path.Join(temp.Path, "qml", "QmlSharp", "TestApp", "CounterView.qml")));
            Assert.False(File.Exists(Path.Join(temp.Path, "schemas", "FirstViewModel.schema.json")));
            Assert.False(File.Exists(Path.Join(temp.Path, "schemas", "SecondViewModel.schema.json")));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void OutputWriter_PreservesStableLfLineEndings()
        {
            using TempOutputDirectory temp = new();
            CompilerOptions options = CreateOptions(temp.Path);
            CompilationUnit unit = CreateCounterUnit() with
            {
                QmlText = "Column {\r\n    Text {}\r\n}",
            };

            _ = new CompilerOutputWriter().WriteOutput(CompilationResult.FromUnits(ImmutableArray.Create(unit)), options);

            string qml = File.ReadAllText(Path.Join(temp.Path, "qml", "QmlSharp", "TestApp", "CounterView.qml"));
            Assert.DoesNotContain("\r", qml, StringComparison.Ordinal);
            Assert.EndsWith("\n", qml, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void OutputWriter_DoesNotWriteSourceMapsWhenDisabled()
        {
            using TempOutputDirectory temp = new();
            CompilerOptions options = CreateOptions(temp.Path) with { GenerateSourceMaps = false };
            CompilationResult compilation = CompilationResult.FromUnits(ImmutableArray.Create(CreateCounterUnit()));

            OutputResult result = new CompilerOutputWriter().WriteOutput(compilation, options);

            Assert.Empty(result.SourceMapFiles);
            Assert.False(File.Exists(Path.Join(temp.Path, "source-maps", "CounterView.qml.map")));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void OutputWriter_C002_ReportsOutputWriteFailureWithErrorDetails()
        {
            using TempOutputDirectory temp = new();
            string outputFile = Path.Join(temp.Path, "dist-as-file");
            File.WriteAllText(outputFile, "not a directory");
            CompilerOptions options = CreateOptions(outputFile);
            CompilationResult compilation = CompilationResult.FromUnits(ImmutableArray.Create(CreateCounterUnit()));

            OutputResult result = new CompilerOutputWriter().WriteOutput(compilation, options);

            CompilerDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.False(result.Success);
            Assert.Equal(DiagnosticCodes.OutputWriteFailed, diagnostic.Code);
            Assert.Equal("WritingArtifacts", diagnostic.Phase);
            Assert.Contains(nameof(IOException), diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains(outputFile, diagnostic.Location?.FilePath, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void OutputWriter_C003_ReportsSchemaSerializationFailure()
        {
            using TempOutputDirectory temp = new();
            CompilerOptions options = CreateOptions(temp.Path);
            CompilationUnit unit = CreateCounterUnit();
            CompilerOutputWriter writer = new(
                new ThrowingSchemaSerializer(),
                new EventBindingsBuilder(),
                new SourceMapManager());

            OutputResult result = writer.WriteOutput(CompilationResult.FromUnits(ImmutableArray.Create(unit)), options);

            CompilerDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.False(result.Success);
            Assert.Equal(DiagnosticCodes.SchemaSerializationFailed, diagnostic.Code);
            Assert.Contains("Schema serialization failed.", diagnostic.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Join(temp.Path, "schemas", "CounterViewModel.schema.json")));
        }

        [Fact]
        [Trait("Category", TestCategories.Unit)]
        public void OutputWriter_C004_ReportsSourceMapWriteFailure()
        {
            using TempOutputDirectory temp = new();
            CompilerOptions options = CreateOptions(temp.Path);
            CompilationUnit unit = CreateCounterUnit();
            CompilerOutputWriter writer = new(
                new ViewModelSchemaSerializer(),
                new EventBindingsBuilder(),
                new ThrowingSourceMapManager());

            OutputResult result = writer.WriteOutput(CompilationResult.FromUnits(ImmutableArray.Create(unit)), options);

            CompilerDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.False(result.Success);
            Assert.Equal(DiagnosticCodes.SourceMapWriteFailed, diagnostic.Code);
            Assert.Contains("Source map serialization failed.", diagnostic.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Join(temp.Path, "source-maps", "CounterView.qml.map")));
        }

        private static CompilerOptions CreateOptions(string outputDir)
        {
            return CompilerTestFixtures.DefaultOptions with
            {
                OutputDir = outputDir,
                SourceMapDir = Path.Join(outputDir, "source-maps"),
            };
        }

        private static CompilationUnit CreateCounterUnit()
        {
            SourceMap sourceMap = new SourceMapManager()
                .CreateBuilder("CounterView.cs", "CounterView.qml")
                .Build();

            return new CompilationUnit
            {
                SourceFilePath = "CounterView.cs",
                ViewClassName = "CounterView",
                ViewModelClassName = "CounterViewModel",
                QmlText = "Column {\n    Text {}\n}\n",
                Schema = CompilerTestFixtures.CreateCounterSchema(),
                Document = CompilerTestFixtures.CreateCounterAstFixture(),
                SourceMap = sourceMap,
                Stats = new CompilationUnitStats
                {
                    ElapsedMilliseconds = 3,
                    QmlBytes = 22,
                },
            };
        }

        private static CompilationUnit CreateTodoUnit()
        {
            return new CompilationUnit
            {
                SourceFilePath = "TodoView.cs",
                ViewClassName = "TodoView",
                ViewModelClassName = "TodoViewModel",
                QmlText = "Column {\n}\n",
                Schema = CompilerTestFixtures.CreateTodoSchema(),
                Document = CompilerTestFixtures.CreateCounterAstFixture(),
                SourceMap = SourceMap.Empty("TodoView.cs", "TodoView.qml"),
            };
        }

        private sealed class ThrowingSchemaSerializer : IViewModelSchemaSerializer
        {
            public string Serialize(ViewModelSchema schema)
            {
                throw new InvalidOperationException("schema boom");
            }

            public ViewModelSchema Deserialize(string json)
            {
                throw new InvalidOperationException("schema boom");
            }
        }

        private sealed class ThrowingSourceMapManager : ISourceMapManager
        {
            public ISourceMapBuilder CreateBuilder(string sourceFilePath, string outputFilePath)
            {
                return new SourceMapManager().CreateBuilder(sourceFilePath, outputFilePath);
            }

            public string Serialize(SourceMap sourceMap)
            {
                throw new InvalidOperationException("source map boom");
            }

            public SourceMap Deserialize(string json)
            {
                throw new InvalidOperationException("source map boom");
            }

            public SourceLocation? FindSourceLocation(SourceMap sourceMap, string outputFilePath, int outputLine, int outputColumn)
            {
                return null;
            }

            public QmlLocation? FindQmlLocation(SourceMap sourceMap, string sourceFilePath, int sourceLine, int sourceColumn)
            {
                return null;
            }
        }
    }
}
