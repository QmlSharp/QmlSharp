using System.Text.Json;
using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.SourceMaps
{
    public sealed class SourceMapEmissionTests
    {
        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SM_01_SimpleDocument_CreatesEntriesForEmittedNodes()
        {
            QmlDocument document = SourceMappedDocument();
            IQmlEmitter emitter = new QmlEmitter();

            EmitResult result = emitter.EmitWithSourceMap(document);

            Assert.Equal(emitter.Emit(document), result.Text);
            Assert.Contains(result.SourceMap.Entries, entry => ReferenceEquals(entry.Node, document.RootObject));
            Assert.Contains(result.SourceMap.Entries, entry => ReferenceEquals(entry.Node, document.Imports[0]));
            Assert.Contains(result.SourceMap.Entries, entry => ReferenceEquals(entry.Node, document.Pragmas[0]));
            Assert.Contains(result.SourceMap.Entries, entry => ReferenceEquals(entry.Node, document.RootObject.Members[0]));
            Assert.Contains(result.SourceMap.Entries, entry => entry.NodeKind == NodeKind.Comment.ToString());
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SM_02_RootObjectSpan_IsOneBasedAndSlicesGeneratedObject()
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
                        },
                    ],
                },
            };
            IQmlEmitter emitter = new QmlEmitter();

            EmitResult result = emitter.EmitWithSourceMap(document);
            OutputSpan? nullableSpan = result.SourceMap.GetOutputSpan(document.RootObject);

            Assert.NotNull(nullableSpan);
            OutputSpan span = nullableSpan;
            SourceMapAssert.ValidSpan(span);
            Assert.Equal(1, span.StartLine);
            Assert.Equal(1, span.StartColumn);
            Assert.Equal("Item {\n    width: 100\n}", SourceMapAssert.Slice(result.Text, span));
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SM_03_BindingSpan_MatchesGeneratedLineAndPreservesSourceSpan()
        {
            SourceSpan sourceSpan = Span(12, 9, 128, 12, 19, 138);
            BindingNode binding = new()
            {
                PropertyName = "width",
                Value = Values.Number(100),
                Span = sourceSpan,
            };
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members = [binding],
                },
            };
            IQmlEmitter emitter = new QmlEmitter();

            EmitResult result = emitter.EmitWithSourceMap(document);
            SourceMapEntry entry = Assert.Single(result.SourceMap.Entries, item => ReferenceEquals(item.Node, binding));

            Assert.Equal(2, entry.OutputSpan.StartLine);
            Assert.Equal(1, entry.OutputSpan.StartColumn);
            Assert.Equal("    width: 100", SourceMapAssert.Slice(result.Text, entry.OutputSpan));
            Assert.Equal(sourceSpan, entry.SourceSpan);
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SM_04_GetNodesAtLine_ReturnsNodesEmittedOnRequestedLine()
        {
            QmlDocument document = SourceMappedDocument();
            IQmlEmitter emitter = new QmlEmitter();

            EmitResult result = emitter.EmitWithSourceMap(document);
            IReadOnlyList<AstNode> lineOneNodes = result.SourceMap.GetNodesAtLine(1);

            Assert.Contains(document.Pragmas[0], lineOneNodes);
            Assert.DoesNotContain(document.RootObject, lineOneNodes);
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SM_05_GetNodesAtLineForMissingLine_ReturnsEmpty()
        {
            QmlDocument document = SourceMappedDocument();
            IQmlEmitter emitter = new QmlEmitter();

            EmitResult result = emitter.EmitWithSourceMap(document);

            Assert.Empty(result.SourceMap.GetNodesAtLine(999));
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SM_06_GetInnermostNodeAtBindingPosition_ReturnsBinding()
        {
            BindingNode binding = new()
            {
                PropertyName = "width",
                Value = Values.Number(100),
            };
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members = [binding],
                },
            };
            IQmlEmitter emitter = new QmlEmitter();

            EmitResult result = emitter.EmitWithSourceMap(document);

            Assert.Same(binding, result.SourceMap.GetInnermostNodeAt(2, 5));
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SM_07_GetInnermostNodeAtWhitespaceBetweenNodes_ReturnsNull()
        {
            QmlDocument document = new()
            {
                Imports =
                [
                    new ImportNode
                    {
                        ImportKind = ImportKind.Module,
                        ModuleUri = "QtQuick",
                    },
                ],
                RootObject = new ObjectDefinitionNode { TypeName = "Item" },
            };
            IQmlEmitter emitter = new QmlEmitter();

            EmitResult result = emitter.EmitWithSourceMap(document);

            Assert.Null(result.SourceMap.GetInnermostNodeAt(2, 1));
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SM_08_ToJson_RoundTripsThroughSystemTextJson()
        {
            QmlDocument document = SourceMappedDocument();
            IQmlEmitter emitter = new QmlEmitter();

            SourceMapJson json = emitter.EmitWithSourceMap(document).SourceMap.ToJson();
            string serialized = JsonSerializer.Serialize(json);
            SourceMapJson? roundTrip = JsonSerializer.Deserialize<SourceMapJson>(serialized);

            Assert.NotNull(roundTrip);
            Assert.Equal(json.Entries.Length, roundTrip.Entries.Length);
            Assert.Equal(json.Entries[0].NodeKind, roundTrip.Entries[0].NodeKind);
            Assert.Equal(json.Entries[0].SourceSpan, roundTrip.Entries[0].SourceSpan);
            Assert.Equal(json.Entries[0].Span, roundTrip.Entries[0].Span);
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SourceMap_AllMappedOutputSpans_SliceEmittedQml()
        {
            QmlDocument document = SourceMappedDocument();
            IQmlEmitter emitter = new QmlEmitter();

            EmitResult result = emitter.EmitWithSourceMap(document);

            foreach (SourceMapEntry entry in result.SourceMap.Entries)
            {
                string slice = SourceMapAssert.Slice(result.Text, entry.OutputSpan);

                Assert.False(string.IsNullOrWhiteSpace(slice));
            }
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SourceMap_CrLfNewlineOption_UsesSameLineColumnConvention()
        {
            BindingNode binding = new()
            {
                PropertyName = "width",
                Value = Values.Number(100),
            };
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members = [binding],
                },
            };
            IQmlEmitter emitter = new QmlEmitter();

            EmitResult result = emitter.EmitWithSourceMap(document, new EmitOptions { Newline = NewlineStyle.CrLf });
            OutputSpan? nullableSpan = result.SourceMap.GetOutputSpan(binding);

            Assert.NotNull(nullableSpan);
            OutputSpan span = nullableSpan;
            LineEndingAssert.ContainsOnlyCrLf(result.Text);
            Assert.Equal(2, span.StartLine);
            Assert.Equal(1, span.StartColumn);
            Assert.Equal("    width: 100", SourceMapAssert.Slice(result.Text, span));
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SourceMap_MissingSourceSpan_StillMapsOutputWithNullSourceSpan()
        {
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode { TypeName = "Item" },
            };
            IQmlEmitter emitter = new QmlEmitter();

            EmitResult result = emitter.EmitWithSourceMap(document);
            SourceMapEntry rootEntry = Assert.Single(result.SourceMap.Entries);

            Assert.Same(document.RootObject, rootEntry.Node);
            Assert.Null(rootEntry.SourceSpan);
            Assert.Equal("Item {}", SourceMapAssert.Slice(result.Text, rootEntry.OutputSpan));
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SourceMap_MemberEntries_CoverIdsPropertiesAndSignalsWithSourceSpans()
        {
            IdAssignmentNode id = new()
            {
                Id = "root",
                Span = Span(10, 5, 100, 10, 12, 107),
            };
            PropertyDeclarationNode property = new()
            {
                Name = "count",
                TypeName = "int",
                InitialValue = Values.Number(0),
                Span = Span(11, 5, 109, 11, 26, 130),
            };
            SignalDeclarationNode signal = new()
            {
                Name = "changed",
                Span = Span(12, 5, 132, 12, 20, 147),
            };
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members = [id, property, signal],
                },
            };
            IQmlEmitter emitter = new QmlEmitter();

            EmitResult result = emitter.EmitWithSourceMap(document);

            AssertEntry(result, id, "    id: root", id.Span);
            AssertEntry(result, property, "    property int count: 0", property.Span);
            AssertEntry(result, signal, "    signal changed()", signal.Span);
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SourceMap_AttachedBinding_MapsParentAndChildBindingSpans()
        {
            BindingNode fillWidth = new()
            {
                PropertyName = "fillWidth",
                Value = Values.Boolean(true),
                Span = Span(21, 5, 210, 21, 27, 232),
            };
            BindingNode margins = new()
            {
                PropertyName = "margins",
                Value = Values.Number(10),
                Span = Span(22, 5, 234, 22, 23, 252),
            };
            AttachedBindingNode attached = new()
            {
                AttachedTypeName = "Layout",
                Bindings = [fillWidth, margins],
                Span = Span(21, 5, 210, 22, 23, 252),
            };
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members = [attached],
                },
            };
            IQmlEmitter emitter = new QmlEmitter();

            EmitResult result = emitter.EmitWithSourceMap(document);

            AssertEntry(result, attached, "    Layout.fillWidth: true\n    Layout.margins: 10", attached.Span);
            AssertEntry(result, fillWidth, "    Layout.fillWidth: true", fillWidth.Span);
            AssertEntry(result, margins, "    Layout.margins: 10", margins.Span);
            Assert.Same(fillWidth, result.SourceMap.GetInnermostNodeAt(2, 12));
            Assert.Same(margins, result.SourceMap.GetInnermostNodeAt(3, 12));
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SourceMap_MemberEntries_CoverComplexMemberKindsWithSourceSpans()
        {
            GroupedBindingNode grouped = new()
            {
                GroupName = "font",
                Bindings =
                [
                    new BindingNode { PropertyName = "pixelSize", Value = Values.Number(14) },
                ],
                Span = Span(30, 5, 300, 32, 6, 331),
            };
            ArrayBindingNode array = new()
            {
                PropertyName = "states",
                Elements = [Values.Number(1), Values.Number(2)],
                Span = Span(33, 5, 333, 36, 6, 366),
            };
            ObjectDefinitionNode animation = new()
            {
                TypeName = "NumberAnimation",
                Span = Span(38, 9, 380, 38, 28, 399),
            };
            BehaviorOnNode behavior = new()
            {
                PropertyName = "x",
                Animation = animation,
                Span = Span(37, 5, 370, 39, 6, 406),
            };
            SignalHandlerNode handler = new()
            {
                HandlerName = "onClicked",
                Form = SignalHandlerForm.Expression,
                Code = "run()",
                Span = Span(40, 5, 408, 40, 21, 424),
            };
            FunctionDeclarationNode function = new()
            {
                Name = "run",
                Body = "work()",
                Span = Span(41, 5, 426, 43, 6, 456),
            };
            EnumDeclarationNode enumDeclaration = new()
            {
                Name = "Mode",
                Members = [new EnumMember("On", null)],
                Span = Span(44, 5, 458, 46, 6, 481),
            };
            InlineComponentNode component = new()
            {
                Name = "Badge",
                Body = new ObjectDefinitionNode { TypeName = "Item" },
                Span = Span(47, 5, 483, 47, 27, 505),
            };
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members = [grouped, array, behavior, handler, function, enumDeclaration, component],
                },
            };
            IQmlEmitter emitter = new QmlEmitter();

            EmitResult result = emitter.EmitWithSourceMap(document);

            AssertEntry(result, grouped, "    font {\n        pixelSize: 14\n    }", grouped.Span);
            AssertEntry(result, array, "    states: [\n        1,\n        2\n    ]", array.Span);
            AssertEntry(result, behavior, "    Behavior on x {\n        NumberAnimation {}\n    }", behavior.Span);
            AssertEntry(result, animation, "        NumberAnimation {}", animation.Span);
            AssertEntry(result, handler, "    onClicked: run()", handler.Span);
            AssertEntry(result, function, "    function run() {\n        work()\n    }", function.Span);
            AssertEntry(result, enumDeclaration, "    enum Mode {\n        On\n    }", enumDeclaration.Span);
            AssertEntry(result, component, "    component Badge: Item {}", component.Span);
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SourceMap_InlineObjectValue_MapsNestedObjectSpanWithoutChangingText()
        {
            ObjectDefinitionNode fontObject = new()
            {
                TypeName = "Font",
                Span = Span(20, 11, 200, 20, 33, 222),
                Members =
                [
                    new BindingNode
                    {
                        PropertyName = "pixelSize",
                        Value = Values.Number(14),
                    },
                ],
            };
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Text",
                    Members =
                    [
                        new BindingNode
                        {
                            PropertyName = "font",
                            Value = Values.Object(fontObject),
                        },
                    ],
                },
            };
            IQmlEmitter emitter = new QmlEmitter();

            EmitResult result = emitter.EmitWithSourceMap(document);
            SourceMapEntry entry = Assert.Single(result.SourceMap.Entries, item => ReferenceEquals(item.Node, fontObject));

            Assert.Equal(emitter.Emit(document), result.Text);
            Assert.Equal("Font { pixelSize: 14 }", SourceMapAssert.Slice(result.Text, entry.OutputSpan));
            Assert.Equal(fontObject.Span, entry.SourceSpan);
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SourceMap_EntryOrder_IsDeterministicAcrossRepeatedEmits()
        {
            QmlDocument document = SourceMappedDocument();
            IQmlEmitter emitter = new QmlEmitter();

            ImmutableArray<string> first = DescribeEntries(emitter.EmitWithSourceMap(document).SourceMap);
            ImmutableArray<string> second = DescribeEntries(emitter.EmitWithSourceMap(document).SourceMap);

            Assert.Equal(first.ToArray(), second.ToArray());
        }

        [Fact]
        [Trait("Category", TestCategories.SourceMaps)]
        public void SourceMap_DisabledPlainEmit_ReturnsSameTextWithoutSourceMapWork()
        {
            QmlDocument document = SourceMappedDocument();
            IQmlEmitter emitter = new QmlEmitter();

            string plainText = emitter.Emit(document);
            EmitResult mapped = emitter.EmitWithSourceMap(document);

            Assert.Equal(plainText, mapped.Text);
            Assert.NotEmpty(mapped.SourceMap.Entries);
        }

        private static void AssertEntry(EmitResult result, AstNode node, string expectedSlice, SourceSpan? expectedSourceSpan)
        {
            SourceMapEntry entry = Assert.Single(result.SourceMap.Entries, item => ReferenceEquals(item.Node, node));

            Assert.Equal(expectedSlice, SourceMapAssert.Slice(result.Text, entry.OutputSpan));
            Assert.Equal(expectedSourceSpan, entry.SourceSpan);
        }

        private static ImmutableArray<string> DescribeEntries(ISourceMap sourceMap)
        {
            ImmutableArray<string>.Builder entries = ImmutableArray.CreateBuilder<string>(sourceMap.Entries.Count);

            foreach (SourceMapEntry entry in sourceMap.Entries)
            {
                entries.Add(
                    string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"{entry.NodeKind}:{entry.OutputSpan.StartLine}:{entry.OutputSpan.StartColumn}:{entry.OutputSpan.EndLine}:{entry.OutputSpan.EndColumn}"));
            }

            return entries.ToImmutable();
        }

        private static QmlDocument SourceMappedDocument()
        {
            CommentNode leadingComment = new()
            {
                Text = "// original width",
                Span = Span(4, 1, 40, 4, 17, 56),
            };
            BindingNode binding = new()
            {
                PropertyName = "width",
                Value = Values.Number(100),
                Span = Span(5, 5, 60, 5, 15, 70),
                LeadingComments = [leadingComment],
            };

            return new QmlDocument
            {
                Pragmas =
                [
                    new PragmaNode
                    {
                        Name = PragmaName.Singleton,
                        Span = Span(1, 1, 0, 1, 16, 15),
                    },
                ],
                Imports =
                [
                    new ImportNode
                    {
                        ImportKind = ImportKind.Module,
                        ModuleUri = "QtQuick",
                        Version = "2.15",
                        Span = Span(2, 1, 17, 2, 20, 36),
                    },
                ],
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Span = Span(3, 1, 38, 6, 1, 72),
                    Members = [binding],
                },
            };
        }

        private static SourceSpan Span(int startLine, int startColumn, int startOffset, int endLine, int endColumn, int endOffset)
        {
            return new SourceSpan(
                new SourcePosition(startLine, startColumn, startOffset),
                new SourcePosition(endLine, endColumn, endOffset));
        }
    }
}
