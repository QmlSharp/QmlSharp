using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Closure
{
    [Trait("Category", TestCategories.Contract)]
    public sealed class EmitterClosureCoverageTests
    {
        private static readonly ImmutableArray<string> SpecIds = BuildSpecIds();

        [Fact]
        public void Closure_AllEmitterTestSpecIds_AreNamedByImplementedTests()
        {
            ImmutableHashSet<string> methodNames = GetImplementedTestMethodNames()
                .ToImmutableHashSet(StringComparer.Ordinal);

            ImmutableArray<string> missingIds = SpecIds
                .Where(id => !methodNames.Any(name => name.Contains(id, StringComparison.Ordinal)))
                .ToImmutableArray();

            Assert.Empty(missingIds);
            Assert.Equal(146, SpecIds.Length);
        }

        [Fact]
        public void Closure_GoldenFixtureSet_CoversEveryBindingValueKindAndNodeKind()
        {
            ImmutableHashSet<BindingValueKind> bindingKinds = GoldenFixtureBuilder.All()
                .SelectMany(static fixture => CollectBindingValueKinds(fixture.Document))
                .ToImmutableHashSet();
            ImmutableHashSet<NodeKind> nodeKinds = GoldenFixtureBuilder.All()
                .SelectMany(static fixture => CollectNodeKinds(fixture.Document))
                .ToImmutableHashSet();

            Assert.Equal(Enum.GetValues<BindingValueKind>().Order().ToArray(), bindingKinds.Order().ToArray());
            Assert.Equal(Enum.GetValues<NodeKind>().Order().ToArray(), nodeKinds.Order().ToArray());
        }

        [Fact]
        public void Closure_EveryPublicEmitOption_HasAContractOrBehaviorTestReference()
        {
            ImmutableDictionary<string, string> optionCoverage = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [nameof(EmitOptions.IndentStyle)] = "OPT_01",
                [nameof(EmitOptions.IndentSize)] = "OPT_02_OPT_03",
                [nameof(EmitOptions.Newline)] = "OPT_04_OPT_05",
                [nameof(EmitOptions.MaxLineWidth)] = "Options_MaxLineWidth_IsResolvedAsAdvisoryValue",
                [nameof(EmitOptions.InsertBlankLinesBetweenSections)] = "EB_14_BlankLineBetweenSectionsEnabled",
                [nameof(EmitOptions.InsertBlankLinesBetweenObjects)] = "EB_20_BlankLinesBetweenSiblingObjectsEnabled",
                [nameof(EmitOptions.InsertBlankLinesBetweenFunctions)] = "MM_23_BlankLinesBetweenFunctionsEnabled",
                [nameof(EmitOptions.QuoteStyle)] = "OPT_06_OPT_07_OPT_08",
                [nameof(EmitOptions.EmitComments)] = "OPT_09",
                [nameof(EmitOptions.EmitGeneratedHeader)] = "OPT_10",
                [nameof(EmitOptions.GeneratedHeaderText)] = "EB_23_GeneratedHeaderWithCustomText",
                [nameof(EmitOptions.TrailingNewline)] = "OPT_11",
                [nameof(EmitOptions.Normalize)] = "OPT_12",
                [nameof(EmitOptions.SortImports)] = "OPT_13",
                [nameof(EmitOptions.SingleLineEmptyObjects)] = "EB_02_EmptyDocument_SingleLineEmptyObjectsTrue",
                [nameof(EmitOptions.SemicolonRule)] = "OPT_14_OPT_15_OPT_16",
            }.ToImmutableDictionary(StringComparer.Ordinal);
            ImmutableArray<string> publicOptions = typeof(EmitOptions)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(static property => property.Name)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();

            Assert.Equal(publicOptions.ToArray(), optionCoverage.Keys.Order(StringComparer.Ordinal).ToArray());
            Assert.All(optionCoverage.Values, AssertTestReferenceExists);
        }

        [Fact]
        public void Closure_SourceMapDtoContract_RoundTripsThroughSystemTextJson()
        {
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members =
                    [
                        new BindingNode
                        {
                            PropertyName = "width",
                            Value = Values.Number(100),
                            Span = new SourceSpan(
                                new SourcePosition(10, 5, 100),
                                new SourcePosition(10, 15, 110)),
                        },
                    ],
                },
            };
            SourceMapJson json = new QmlEmitter().EmitWithSourceMap(document).SourceMap.ToJson();

            string serialized = JsonSerializer.Serialize(json);
            SourceMapJson? roundTrip = JsonSerializer.Deserialize<SourceMapJson>(serialized);

            Assert.NotNull(roundTrip);
            Assert.Equal(json.Entries.Length, roundTrip.Entries.Length);
            Assert.Equal(json.Entries.Select(static entry => entry.NodeKind), roundTrip.Entries.Select(static entry => entry.NodeKind));
            Assert.Equal(json.Entries.Select(static entry => entry.SourceSpan), roundTrip.Entries.Select(static entry => entry.SourceSpan));
            Assert.Equal(json.Entries.Select(static entry => entry.Span), roundTrip.Entries.Select(static entry => entry.Span));
        }

        [Fact]
        public void Closure_GoldenFiles_AreCommittedExactDiffArtifacts()
        {
            string repositoryRoot = FindRepositoryRoot();
            string attributesText = File.ReadAllText(Path.Join(repositoryRoot, ".gitattributes"));

            Assert.Contains(
                "tests/QmlSharp.Qml.Emitter.Tests/Fixtures/Golden/*.qml text eol=lf linguist-language=QML",
                attributesText,
                StringComparison.Ordinal);

            foreach (string fileName in GoldenFixtureBuilder.ExpectedFileNames)
            {
                GoldenFixture fixture = GoldenFixtureBuilder.Build(fileName);
                string committed = GoldenFileLoader.Load(fileName);
                string emitted = new QmlEmitter().Emit(fixture.Document, fixture.Options);

                Assert.Equal(committed, emitted);
            }
        }

        [Fact]
        public void Closure_EmitterProjectHasNoForbiddenModuleOrRuntimeDependencies()
        {
            string repositoryRoot = FindRepositoryRoot();
            string projectPath = Path.Join(repositoryRoot, "src", "QmlSharp.Qml.Emitter", "QmlSharp.Qml.Emitter.csproj");
            XDocument project = XDocument.Load(projectPath);
            ImmutableArray<string> projectReferences = project
                .Descendants("ProjectReference")
                .Select(static reference => GetProjectReferenceName((string?)reference.Attribute("Include") ?? string.Empty))
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
            ImmutableArray<string> packageReferences = project
                .Descendants("PackageReference")
                .Select(static reference => ((string?)reference.Attribute("Include") ?? string.Empty).ToLowerInvariant())
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();

            Assert.Equal(["QmlSharp.Qml.Ast"], projectReferences.ToArray());
            Assert.Empty(packageReferences);

            string[] forbiddenTerms =
            [
                "QmlSharp.Compiler",
                "QmlSharp.Dsl",
                "QmlSharp.Build",
                "QmlSharp.DevTools",
                "QmlSharp.Host",
                "QmlSharp.Qt.Tools",
                "TypeScript",
                "node_modules",
                "npm",
                "Bun",
                "Rust",
                "qmlformat",
                "qmllint",
            ];
            string sourceRoot = Path.Join(repositoryRoot, "src", "QmlSharp.Qml.Emitter");

            foreach (string text in Directory
                .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText))
            {
                foreach (string forbiddenTerm in forbiddenTerms)
                {
                    Assert.DoesNotContain(forbiddenTerm, text, StringComparison.Ordinal);
                }
            }
        }

        [Fact]
        public async Task Closure_PipelineBatch_EdgeFailuresRemainPerDocumentAndDeterministic()
        {
            EmitPipelineConfig config = new()
            {
                EnableFormat = false,
                EnableLint = false,
            };
            EmitPipeline pipeline = new(new QmlEmitter(), config);

            ImmutableArray<NamedPipelineResult> empty = await pipeline.ProcessBatchAsync(ImmutableArray<NamedDocument>.Empty);
            ImmutableArray<NamedPipelineResult> failures = await pipeline.ProcessBatchAsync(
                [
                    null!,
                    new NamedDocument
                    {
                        Name = "MissingDocument.qml",
                        Document = null!,
                    },
                    new NamedDocument
                    {
                        Name = "Valid.qml",
                        Document = AstFixtureFactory.MinimalDocument("Item"),
                    },
                ]);

            Assert.Empty(empty);
            Assert.Equal(3, failures.Length);
            Assert.False(failures[0].Result.Succeeded);
            Assert.False(failures[1].Result.Succeeded);
            Assert.True(failures[2].Result.Succeeded);
            Assert.Equal("PIPELINE-DOCUMENT", Assert.Single(failures[0].Result.Diagnostics).Code);
            Assert.Equal("Named document cannot be null.", failures[0].Result.Diagnostics[0].Message);
            Assert.Equal("PIPELINE-DOCUMENT", Assert.Single(failures[1].Result.Diagnostics).Code);
            Assert.Equal("MissingDocument.qml", failures[1].Result.Diagnostics[0].File);
        }

        [Fact]
        public void Closure_EdgeEmissionPaths_AreCoveredForStableErrorAndFragmentBehavior()
        {
            QmlEmitter emitter = new();

            Assert.Equal("component Badge: Item {}\n", emitter.EmitFragment(new InlineComponentNode { Name = "Badge", Body = new ObjectDefinitionNode { TypeName = "Item" } }, new FragmentEmitOptions { IncludeTrailingNewline = true }));
            Assert.Equal("enum Mode {\n    Ready\n}\n", emitter.EmitFragment(new EnumDeclarationNode { Name = "Mode", Members = [new EnumMember("Ready", null)] }, new FragmentEmitOptions { IncludeTrailingNewline = true }));
            Assert.Equal("parent.width\n    + 1", emitter.EmitFragment(Values.Expression("parent.width\n+ 1")));
            Assert.Equal("Font {}\n", emitter.EmitFragment(Values.Object(new ObjectDefinitionNode { TypeName = "Font" }), new FragmentEmitOptions { IncludeTrailingNewline = true }));
            Assert.Equal("[Font {}]\n", emitter.EmitFragment(Values.Array(Values.Object(new ObjectDefinitionNode { TypeName = "Font" })), new FragmentEmitOptions { IncludeTrailingNewline = true }));

            Assert.Contains("Unsupported import kind", Assert.Throws<NotSupportedException>(() => emitter.EmitFragment(new ImportNode { ImportKind = (ImportKind)999 })).Message, StringComparison.Ordinal);
            Assert.Contains("Module imports require a module URI", Assert.Throws<InvalidOperationException>(() => emitter.EmitFragment(new ImportNode { ImportKind = ImportKind.Module })).Message, StringComparison.Ordinal);
            Assert.Contains("require a path", Assert.Throws<InvalidOperationException>(() => emitter.EmitFragment(new ImportNode { ImportKind = ImportKind.Directory })).Message, StringComparison.Ordinal);
            Assert.Contains("Unsupported signal handler form", Assert.Throws<NotSupportedException>(() => emitter.EmitFragment(new SignalHandlerNode { HandlerName = "onClicked", Form = (SignalHandlerForm)999, Code = "noop()" })).Message, StringComparison.Ordinal);
            Assert.Contains("Unsupported binding value kind", Assert.Throws<NotSupportedException>(() => emitter.EmitFragment(new UnsupportedBindingValue())).Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Closure_MultilineAndEmptyMemberEdges_EmitStableQml()
        {
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members =
                    [
                        new GroupedBindingNode { GroupName = "font" },
                        new BindingNode { PropertyName = "emptyExpression", Value = Values.Expression(string.Empty) },
                        new BindingNode { PropertyName = "emptyBlock", Value = Values.Block(string.Empty) },
                        new BindingNode { PropertyName = "font", Value = Values.Object(new ObjectDefinitionNode { TypeName = "Font" }) },
                        new BindingNode
                        {
                            PropertyName = "complexFont",
                            Value = Values.Object(
                                new ObjectDefinitionNode
                                {
                                    TypeName = "Font",
                                    Members =
                                    [
                                        new BindingNode { PropertyName = "pixelSize", Value = Values.Number(12) },
                                        new BindingNode { PropertyName = "bold", Value = Values.Boolean(true) },
                                    ],
                                }),
                        },
                        new BindingNode { PropertyName = "emptyArray", Value = Values.Array() },
                        new BindingNode { PropertyName = "mixedArray", Value = Values.Array(Values.Object(new ObjectDefinitionNode { TypeName = "Font" }), Values.Expression("a\n+ b")) },
                        new FunctionDeclarationNode { Name = "emptyFunction", Body = string.Empty },
                        new InlineComponentNode
                        {
                            Name = "Panel",
                            Body = new ObjectDefinitionNode
                            {
                                TypeName = "Item",
                                Members =
                                [
                                    new FunctionDeclarationNode { Name = "a", Body = "one()" },
                                    new FunctionDeclarationNode { Name = "b", Body = "two()" },
                                ],
                            },
                        },
                    ],
                },
            };

            string qml = new QmlEmitter().Emit(document);

            Assert.Contains("    font {}", qml, StringComparison.Ordinal);
            Assert.Contains("    emptyExpression: ", qml, StringComparison.Ordinal);
            Assert.Contains("    emptyBlock: { }", qml, StringComparison.Ordinal);
            Assert.Contains("    font: Font {}", qml, StringComparison.Ordinal);
            Assert.Contains("    complexFont: Font {\n        pixelSize: 12\n        bold: true\n    }", qml, StringComparison.Ordinal);
            Assert.Contains("    emptyArray: []", qml, StringComparison.Ordinal);
            Assert.Contains("        a\n            + b", qml, StringComparison.Ordinal);
            Assert.Contains("    function emptyFunction() {\n    }", qml, StringComparison.Ordinal);
            Assert.Contains("        function a() {\n            one()\n        }\n\n        function b()", qml, StringComparison.Ordinal);
        }

        [Fact]
        public void Closure_InternalWriterEscaperOrderingAndSourceMapEdges_AreCovered()
        {
            ResolvedEmitOptions options = ResolvedEmitOptions.From(new EmitOptions { SemicolonRule = SemicolonRule.Always });
            QmlWriter writer = new(options, initialIndentLevel: 1);

            writer.WriteInvariant($"{3.5}");
            writer.WriteLine();
            OutputSpan emptySpan = writer.GetSpanFrom(writer.GetPosition());

            Assert.Equal(1, writer.IndentLevel);
            Assert.Equal("3.5\n", writer.GetOutput());
            Assert.Equal(new OutputSpan { StartLine = 2, StartColumn = 1, EndLine = 2, EndColumn = 1 }, emptySpan);
            Assert.Equal(";", new EmitContext(options).OptionalSemicolonSuffix);
            ArgumentOutOfRangeException negativeIndentError =
                Assert.Throws<ArgumentOutOfRangeException>(() => new QmlWriter(options, initialIndentLevel: -1));
            Assert.Equal("line\\r\\n\\t\\b\\f\\u001F", StringLiteralEscaper.Escape("line\r\n\t\b\f\u001F", '"'));

            ImmutableArray<AstNode> normalized = EmitOrdering.NormalizeMembers(
                [
                    new GroupedBindingNode { GroupName = "font" },
                    new AttachedBindingNode { AttachedTypeName = "Layout" },
                    new ArrayBindingNode { PropertyName = "states" },
                    new BehaviorOnNode { PropertyName = "x", Animation = new ObjectDefinitionNode { TypeName = "NumberAnimation" } },
                    new CommentNode { Text = "// trailing" },
                    new UnsupportedAstNode(),
                ]);

            Assert.Equal(
                [NodeKind.GroupedBinding, NodeKind.AttachedBinding, NodeKind.ArrayBinding, NodeKind.BehaviorOn, NodeKind.Comment, (NodeKind)999],
                normalized.Select(static node => node.Kind).ToArray());

            AssertInvalidSourceMapSpan(new OutputSpan { StartLine = 0, StartColumn = 1, EndLine = 1, EndColumn = 1 });
            AssertInvalidSourceMapSpan(new OutputSpan { StartLine = 1, StartColumn = 0, EndLine = 1, EndColumn = 1 });
            AssertInvalidSourceMapSpan(new OutputSpan { StartLine = 1, StartColumn = 1, EndLine = 0, EndColumn = 1 });
            AssertInvalidSourceMapSpan(new OutputSpan { StartLine = 1, StartColumn = 1, EndLine = 1, EndColumn = 0 });
            AssertInvalidSourceMapSpan(new OutputSpan { StartLine = 2, StartColumn = 1, EndLine = 1, EndColumn = 1 });
            AssertInvalidSourceSpan(new SourceSpan(new SourcePosition(0, 1, 0), new SourcePosition(1, 1, 0)));
            AssertInvalidSourceSpan(new SourceSpan(new SourcePosition(1, 1, 0), new SourcePosition(0, 1, 0)));
            Assert.Equal("initialIndentLevel", negativeIndentError.ParamName);
        }

        private static void AssertTestReferenceExists(string reference)
        {
            ImmutableArray<string> names = GetImplementedTestMethodNames();

            Assert.Contains(names, name => name.Contains(reference, StringComparison.Ordinal));
        }

        private static ImmutableArray<string> GetImplementedTestMethodNames()
        {
            return typeof(EmitterClosureCoverageTests).Assembly
                .GetTypes()
                .SelectMany(static type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Where(static method =>
                    method.GetCustomAttribute<FactAttribute>() is not null ||
                    method.GetCustomAttribute<TheoryAttribute>() is not null)
                .Select(static method => method.Name)
                .ToImmutableArray();
        }

        private static string GetProjectReferenceName(string include)
        {
            string fileName = include
                .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault() ?? string.Empty;

            return Path.GetFileNameWithoutExtension(fileName);
        }

        private static void AssertInvalidSourceMapSpan(OutputSpan span)
        {
            SourceMapImpl sourceMap = new();

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() => sourceMap.AddEntry(new ObjectDefinitionNode { TypeName = "Item" }, span));

            Assert.Contains("output", exception.Message, StringComparison.Ordinal);
        }

        private static void AssertInvalidSourceSpan(SourceSpan sourceSpan)
        {
            SourceMapImpl sourceMap = new();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => sourceMap.AddEntry(
                    new ObjectDefinitionNode
                    {
                        TypeName = "Item",
                        Span = sourceSpan,
                    },
                    new OutputSpan
                    {
                        StartLine = 1,
                        StartColumn = 1,
                        EndLine = 1,
                        EndColumn = 4,
                    }));

            Assert.Contains("source", exception.Message, StringComparison.Ordinal);
        }

        private static ImmutableArray<string> BuildSpecIds()
        {
            ImmutableArray<string>.Builder ids = ImmutableArray.CreateBuilder<string>();

            AddRange(ids, "EB", 25);
            AddRange(ids, "BV", 27);
            AddRange(ids, "MM", 34);
            AddRange(ids, "OPT", 16);
            AddRange(ids, "FE", 7);
            AddRange(ids, "SM", 8);
            AddRange(ids, "PL", 10);
            AddRange(ids, "DT", 4);
            AddRange(ids, "NR", 8);
            AddRange(ids, "EH", 2);
            AddRange(ids, "PF", 5);

            return ids.ToImmutable();
        }

        private static void AddRange(ImmutableArray<string>.Builder ids, string prefix, int count)
        {
            for (int index = 1; index <= count; index++)
            {
                ids.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{prefix}_{index:00}"));
            }
        }

        private static IEnumerable<NodeKind> CollectNodeKinds(AstNode node)
        {
            yield return node.Kind;

            switch (node)
            {
                case QmlDocument document:
                    foreach (PragmaNode pragma in document.Pragmas)
                    {
                        foreach (NodeKind kind in CollectNodeKinds(pragma))
                        {
                            yield return kind;
                        }
                    }

                    foreach (ImportNode import in document.Imports)
                    {
                        foreach (NodeKind kind in CollectNodeKinds(import))
                        {
                            yield return kind;
                        }
                    }

                    foreach (NodeKind kind in CollectNodeKinds(document.RootObject))
                    {
                        yield return kind;
                    }

                    break;
                case ObjectDefinitionNode obj:
                    foreach (AstNode member in obj.Members)
                    {
                        foreach (NodeKind kind in CollectNodeKinds(member))
                        {
                            yield return kind;
                        }
                    }

                    break;
                case InlineComponentNode component:
                    foreach (NodeKind kind in CollectNodeKinds(component.Body))
                    {
                        yield return kind;
                    }

                    break;
                case GroupedBindingNode grouped:
                    foreach (BindingNode binding in grouped.Bindings)
                    {
                        foreach (NodeKind kind in CollectNodeKinds(binding))
                        {
                            yield return kind;
                        }
                    }

                    break;
                case AttachedBindingNode attached:
                    foreach (BindingNode binding in attached.Bindings)
                    {
                        foreach (NodeKind kind in CollectNodeKinds(binding))
                        {
                            yield return kind;
                        }
                    }

                    break;
                case BehaviorOnNode behavior:
                    foreach (NodeKind kind in CollectNodeKinds(behavior.Animation))
                    {
                        yield return kind;
                    }

                    break;
            }
        }

        private static IEnumerable<BindingValueKind> CollectBindingValueKinds(QmlDocument document)
        {
            foreach (BindingValue value in CollectBindingValues(document.RootObject))
            {
                yield return value.Kind;
            }
        }

        private static IEnumerable<BindingValue> CollectBindingValues(AstNode node)
        {
            switch (node)
            {
                case ObjectDefinitionNode obj:
                    foreach (AstNode member in obj.Members)
                    {
                        foreach (BindingValue value in CollectBindingValues(member))
                        {
                            yield return value;
                        }
                    }

                    break;
                case InlineComponentNode component:
                    foreach (BindingValue value in CollectBindingValues(component.Body))
                    {
                        yield return value;
                    }

                    break;
                case PropertyDeclarationNode property when property.InitialValue is not null:
                    yield return property.InitialValue;
                    foreach (BindingValue nested in CollectNestedBindingValues(property.InitialValue))
                    {
                        yield return nested;
                    }

                    break;
                case BindingNode binding:
                    yield return binding.Value;
                    foreach (BindingValue nested in CollectNestedBindingValues(binding.Value))
                    {
                        yield return nested;
                    }

                    break;
                case GroupedBindingNode grouped:
                    foreach (BindingNode binding in grouped.Bindings)
                    {
                        foreach (BindingValue value in CollectBindingValues(binding))
                        {
                            yield return value;
                        }
                    }

                    break;
                case AttachedBindingNode attached:
                    foreach (BindingNode binding in attached.Bindings)
                    {
                        foreach (BindingValue value in CollectBindingValues(binding))
                        {
                            yield return value;
                        }
                    }

                    break;
                case ArrayBindingNode array:
                    foreach (BindingValue element in array.Elements)
                    {
                        yield return element;
                        foreach (BindingValue nested in CollectNestedBindingValues(element))
                        {
                            yield return nested;
                        }
                    }

                    break;
                case BehaviorOnNode behavior:
                    foreach (BindingValue value in CollectBindingValues(behavior.Animation))
                    {
                        yield return value;
                    }

                    break;
            }
        }

        private static IEnumerable<BindingValue> CollectNestedBindingValues(BindingValue value)
        {
            switch (value)
            {
                case ObjectValue objectValue:
                    foreach (BindingValue nested in CollectBindingValues(objectValue.Object))
                    {
                        yield return nested;
                    }

                    break;
                case ArrayValue arrayValue:
                    foreach (BindingValue element in arrayValue.Elements)
                    {
                        yield return element;
                        foreach (BindingValue nested in CollectNestedBindingValues(element))
                        {
                            yield return nested;
                        }
                    }

                    break;
            }
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);

            while (directory is not null)
            {
                string solutionPath = Path.Join(directory.FullName, "QmlSharp.slnx");
                if (File.Exists(solutionPath))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not locate QmlSharp repository root.");
        }

        private sealed record UnsupportedAstNode : AstNode
        {
            public override NodeKind Kind => (NodeKind)999;
        }

        private sealed record UnsupportedBindingValue : BindingValue
        {
            public override BindingValueKind Kind => (BindingValueKind)999;
        }
    }
}
