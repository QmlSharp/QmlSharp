using System.Text.Json;
using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Serialization;
using QmlSharp.Qml.Ast.Tests.Helpers;
using QmlSharp.Qml.Ast.Traversal;

namespace QmlSharp.Qml.Ast.Tests
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class SerializerTests
    {
        private static readonly QmlAstSerializer Serializer = new();

        [Fact]
        public void SZ_01_Round_trip_minimal_document_produces_structurally_equal_ast()
        {
            QmlDocument document = AstFixtures.MinimalDocument();

            QmlDocument roundTripped = Serializer.FromJson(Serializer.ToJson(document));

            AssertSerializedEqual(document, roundTripped);
        }

        [Fact]
        public void SZ_01_Round_trip_full_syntax_document_preserves_structure_order_comments_and_values()
        {
            QmlDocument document = AstFixtures.FullSyntaxDocument();

            QmlDocument roundTripped = Serializer.FromJson(Serializer.ToJson(document));

            AssertSerializedEqual(document, roundTripped);
            Assert.Equal(document.RootObject.Members.Select(member => member.Kind), roundTripped.RootObject.Members.Select(member => member.Kind));
        }

        [Fact]
        public void SZ_02_Serialize_document_with_all_node_kinds_writes_kind_discriminators()
        {
            QmlDocument document = AstFixtures.FullSyntaxDocument();

            string json = Serializer.ToJson(document);
            QmlDocument roundTripped = Serializer.FromJson(json);
            ImmutableArray<NodeKind> visitedKinds = CollectNodeKinds(roundTripped);

            foreach (NodeKind kind in Enum.GetValues<NodeKind>())
            {
                Assert.Contains($"\"kind\":\"{kind}\"", json, StringComparison.Ordinal);
                Assert.Contains(kind, visitedKinds);
            }
        }

        [Fact]
        public void Serialize_document_with_all_binding_value_kinds_round_trips_each_value_kind()
        {
            QmlDocument document = CreateAllBindingValuesDocument();

            string json = Serializer.ToJson(document);
            QmlDocument roundTripped = Serializer.FromJson(json);
            ImmutableArray<BindingValueKind> valueKinds = CollectBindingValueKinds(roundTripped);

            AssertSerializedEqual(document, roundTripped);
            foreach (BindingValueKind kind in Enum.GetValues<BindingValueKind>())
            {
                Assert.Contains($"\"kind\":\"{kind}\"", json, StringComparison.Ordinal);
                Assert.Contains(kind, valueKinds);
            }
        }

        [Fact]
        public void SZ_03_Pretty_print_produces_indented_json()
        {
            QmlDocument document = AstFixtures.MinimalDocument();

            string prettyJson = Serializer.ToPrettyJson(document);

            Assert.Contains(Environment.NewLine, prettyJson, StringComparison.Ordinal);
            Assert.Contains("  \"kind\": \"Document\"", prettyJson, StringComparison.Ordinal);
        }

        [Fact]
        public void SZ_04_Clone_produces_independent_immutable_copy()
        {
            QmlDocument document = AstFixtures.MinimalDocument("Rectangle");

            QmlDocument clone = Serializer.Clone(document);
            QmlDocument changedClone = clone with
            {
                RootObject = clone.RootObject with
                {
                    TypeName = "Item",
                },
            };

            AssertSerializedEqual(document, clone);
            Assert.Equal("Rectangle", document.RootObject.TypeName);
            Assert.Equal("Item", changedClone.RootObject.TypeName);
            Assert.Equal("Rectangle", clone.RootObject.TypeName);
        }

        [Fact]
        public void SZ_05_Deserialize_invalid_json_throws_actionable_serialization_exception()
        {
            QmlAstSerializationException exception = Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson("not json"));

            Assert.Contains("Invalid AST JSON", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SZ_06_Deserialize_json_with_missing_node_discriminator_throws()
        {
            QmlAstSerializationException exception = Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson("{}"));

            Assert.Contains("missing required 'kind' discriminator", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_json_with_missing_binding_value_discriminator_throws()
        {
            string json = "{\"kind\":\"Document\",\"rootObject\":{\"kind\":\"ObjectDefinition\",\"typeName\":\"Item\",\"members\":[{\"kind\":\"Binding\",\"propertyName\":\"width\",\"value\":{\"value\":100}}]}}";

            QmlAstSerializationException exception = Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson(json));

            Assert.Contains("Binding value JSON is missing required 'kind' discriminator", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_json_with_unknown_node_discriminator_throws()
        {
            string json = "{\"kind\":\"NotANode\"}";

            QmlAstSerializationException exception = Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson(json));

            Assert.Contains("Unknown AST node kind 'NotANode'", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_json_with_unknown_binding_value_discriminator_throws()
        {
            string json = "{\"kind\":\"Document\",\"rootObject\":{\"kind\":\"ObjectDefinition\",\"typeName\":\"Item\",\"members\":[{\"kind\":\"Binding\",\"propertyName\":\"width\",\"value\":{\"kind\":\"NotAValue\"}}]}}";

            QmlAstSerializationException exception = Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson(json));

            Assert.Contains("Unknown binding value kind 'NotAValue'", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_document_root_object_with_mismatched_discriminator_throws()
        {
            string json = "{\"kind\":\"Document\",\"rootObject\":{\"kind\":\"Comment\",\"typeName\":\"Item\",\"members\":[],\"text\":\"wrong\"}}";

            QmlAstSerializationException exception = Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson(json));

            Assert.Contains("Property 'rootObject' must be a ObjectDefinitionNode AST node.", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_document_imports_with_mismatched_discriminator_throws()
        {
            string json = "{\"kind\":\"Document\",\"imports\":[{\"kind\":\"Comment\",\"text\":\"wrong\"}],\"rootObject\":{\"kind\":\"ObjectDefinition\",\"typeName\":\"Item\"}}";

            QmlAstSerializationException exception = Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson(json));

            Assert.Contains("Property 'imports' must contain only ImportNode AST nodes.", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_nested_object_value_with_mismatched_discriminator_throws()
        {
            string json = "{\"kind\":\"Document\",\"rootObject\":{\"kind\":\"ObjectDefinition\",\"typeName\":\"Item\",\"members\":[{\"kind\":\"Binding\",\"propertyName\":\"child\",\"value\":{\"kind\":\"ObjectValue\",\"object\":{\"kind\":\"Comment\",\"typeName\":\"Item\",\"members\":[],\"text\":\"wrong\"}}}]}}";

            QmlAstSerializationException exception = Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson(json));

            Assert.Contains("Property 'object' must be a ObjectDefinitionNode AST node.", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_numeric_string_node_discriminator_throws()
        {
            string json = "{\"kind\":\"1\"}";

            QmlAstSerializationException exception = Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson(json));

            Assert.Contains("Unknown AST node kind '1'", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Deserialize_numeric_string_binding_value_discriminator_throws()
        {
            string json = "{\"kind\":\"Document\",\"rootObject\":{\"kind\":\"ObjectDefinition\",\"typeName\":\"Item\",\"members\":[{\"kind\":\"Binding\",\"propertyName\":\"width\",\"value\":{\"kind\":\"1\",\"value\":\"wrong\"}}]}}";

            QmlAstSerializationException exception = Assert.Throws<QmlAstSerializationException>(() => Serializer.FromJson(json));

            Assert.Contains("Unknown binding value kind '1'", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Round_trip_preserves_spans_and_attached_comments()
        {
            CommentNode leadingComment = new()
            {
                Text = "// leading",
                IsBlock = false,
                Span = new SourceSpan(new SourcePosition(1, 1, 0), new SourcePosition(1, 11, 10)),
            };
            CommentNode trailingComment = new()
            {
                Text = "// trailing",
                IsBlock = false,
                Span = new SourceSpan(new SourcePosition(2, 16, 26), new SourcePosition(2, 27, 37)),
            };
            QmlDocument document = new()
            {
                Span = new SourceSpan(new SourcePosition(1, 1, 0), new SourcePosition(3, 2, 40)),
                Imports =
                [
                    new ImportNode
                    {
                        ImportKind = ImportKind.Module,
                        ModuleUri = "QtQuick",
                        Version = "2.15",
                        Span = new SourceSpan(new SourcePosition(1, 1, 0), new SourcePosition(1, 20, 19)),
                    },
                ],
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    LeadingComments = [leadingComment],
                    TrailingComment = trailingComment,
                    Members =
                    [
                        new BindingNode
                        {
                            PropertyName = "width",
                            Value = Values.Number(100),
                            Span = new SourceSpan(new SourcePosition(2, 5, 15), new SourcePosition(2, 15, 25)),
                        },
                    ],
                },
            };

            QmlDocument roundTripped = Serializer.FromJson(Serializer.ToJson(document));

            AssertSerializedEqual(document, roundTripped);
            Assert.Equal(document.Span, roundTripped.Span);
            Assert.Equal(document.Imports[0].Span, roundTripped.Imports[0].Span);
            Assert.Equal("// leading", roundTripped.RootObject.LeadingComments[0].Text);
            Assert.Equal(leadingComment.Span, roundTripped.RootObject.LeadingComments[0].Span);
            Assert.Equal("// trailing", roundTripped.RootObject.TrailingComment?.Text);
            Assert.Equal(trailingComment.Span, roundTripped.RootObject.TrailingComment?.Span);
        }

        private static QmlDocument CreateAllBindingValuesDocument()
        {
            return new QmlDocumentBuilder()
                .SetRootObject("Item", root =>
                {
                    _ = root.PropertyDeclaration("initialCount", "int", Values.Number(1))
                        .Binding("numberValue", Values.Number(42))
                        .Binding("stringValue", Values.String("hello"))
                        .Binding("booleanValue", Values.Boolean(true))
                        .Binding("nullValue", Values.Null())
                        .Binding("enumValue", Values.Enum("Image", "Stretch"))
                        .Binding("expressionValue", Values.Expression("parent.width * 0.5"))
                        .Binding("blockValue", Values.Block("{ return 1; }"))
                        .Binding("objectValue", Values.Object("Font", font =>
                        {
                            _ = font.Binding("pixelSize", Values.Number(12));
                        }))
                        .Binding("arrayValue", Values.Array(
                            Values.String("first"),
                            Values.Object("State", state =>
                            {
                                _ = state.Binding("name", Values.String("active"));
                            }),
                            Values.Array(Values.Boolean(false))));
                })
                .Build();
        }

        private static ImmutableArray<NodeKind> CollectNodeKinds(QmlDocument document)
        {
            ImmutableArray<NodeKind>.Builder kinds = ImmutableArray.CreateBuilder<NodeKind>();
            QmlAstWalker.Walk(
                document,
                enter: (node, _) =>
                {
                    kinds.Add(node.Kind);
                    return true;
                },
                leave: null);
            return kinds.ToImmutable();
        }

        private static ImmutableArray<BindingValueKind> CollectBindingValueKinds(QmlDocument document)
        {
            ImmutableArray<BindingValueKind>.Builder kinds = ImmutableArray.CreateBuilder<BindingValueKind>();
            QmlAstWalker.Walk(
                document,
                enter: (node, _) =>
                {
                    AddBindingValues(node, kinds);
                    return true;
                },
                leave: null);
            return kinds.ToImmutable();
        }

        private static void AddBindingValues(AstNode node, ImmutableArray<BindingValueKind>.Builder kinds)
        {
            switch (node)
            {
                case PropertyDeclarationNode propertyDeclarationNode when propertyDeclarationNode.InitialValue is not null:
                    AddBindingValue(propertyDeclarationNode.InitialValue, kinds);
                    break;

                case BindingNode bindingNode:
                    AddBindingValue(bindingNode.Value, kinds);
                    break;

                case ArrayBindingNode arrayBindingNode:
                    foreach (BindingValue element in arrayBindingNode.Elements)
                    {
                        AddBindingValue(element, kinds);
                    }

                    break;
            }
        }

        private static void AddBindingValue(BindingValue value, ImmutableArray<BindingValueKind>.Builder kinds)
        {
            kinds.Add(value.Kind);
            switch (value)
            {
                case ObjectValue objectValue:
                    QmlAstWalker.Walk(
                        objectValue.Object,
                        enter: (node, _) =>
                        {
                            AddBindingValues(node, kinds);
                            return true;
                        },
                        leave: null);
                    break;

                case ArrayValue arrayValue:
                    foreach (BindingValue element in arrayValue.Elements)
                    {
                        AddBindingValue(element, kinds);
                    }

                    break;
            }
        }

        private static void AssertSerializedEqual(QmlDocument expected, QmlDocument actual)
        {
            using JsonDocument expectedJson = JsonDocument.Parse(Serializer.ToJson(expected));
            using JsonDocument actualJson = JsonDocument.Parse(Serializer.ToJson(actual));

            Assert.True(
                JsonElementDeepEquals(expectedJson.RootElement, actualJson.RootElement),
                $"Expected:{Environment.NewLine}{Serializer.ToPrettyJson(expected)}{Environment.NewLine}Actual:{Environment.NewLine}{Serializer.ToPrettyJson(actual)}");
        }

        private static bool JsonElementDeepEquals(JsonElement left, JsonElement right)
        {
            if (left.ValueKind != right.ValueKind)
            {
                return false;
            }

            return left.ValueKind switch
            {
                JsonValueKind.Object => JsonObjectsDeepEqual(left, right),
                JsonValueKind.Array => JsonArraysDeepEqual(left, right),
                JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
                JsonValueKind.Number => decimal.Equals(left.GetDecimal(), right.GetDecimal()),
                JsonValueKind.True => true,
                JsonValueKind.False => true,
                JsonValueKind.Null => true,
                JsonValueKind.Undefined => true,
                _ => false,
            };
        }

        private static bool JsonObjectsDeepEqual(JsonElement left, JsonElement right)
        {
            List<JsonProperty> leftProperties = [.. left.EnumerateObject()];
            List<JsonProperty> rightProperties = [.. right.EnumerateObject()];
            if (leftProperties.Count != rightProperties.Count)
            {
                return false;
            }

            for (int index = 0; index < leftProperties.Count; index++)
            {
                JsonProperty leftProperty = leftProperties[index];
                JsonProperty rightProperty = rightProperties[index];
                if (!string.Equals(leftProperty.Name, rightProperty.Name, StringComparison.Ordinal)
                    || !JsonElementDeepEquals(leftProperty.Value, rightProperty.Value))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool JsonArraysDeepEqual(JsonElement left, JsonElement right)
        {
            List<JsonElement> leftElements = [.. left.EnumerateArray()];
            List<JsonElement> rightElements = [.. right.EnumerateArray()];
            if (leftElements.Count != rightElements.Count)
            {
                return false;
            }

            for (int index = 0; index < leftElements.Count; index++)
            {
                if (!JsonElementDeepEquals(leftElements[index], rightElements[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
