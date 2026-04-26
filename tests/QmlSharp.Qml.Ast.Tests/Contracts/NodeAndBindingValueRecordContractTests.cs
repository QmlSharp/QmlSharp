using QmlSharp.Qml.Ast.Tests.Helpers;
using QmlSharp.Qml.Ast.Values;

namespace QmlSharp.Qml.Ast.Tests.Contracts
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class NodeAndBindingValueRecordContractTests
    {
        [Fact]
        public void ASTD_01_Every_NodeKind_is_represented_by_a_concrete_AstNode_record()
        {
            NodeKind[] expected = [.. Enum.GetValues<NodeKind>().OrderBy(static kind => (int)kind)];
            NodeKind[] actual = [.. CreateAllNodes().Select(static node => node.Kind).Distinct().OrderBy(static kind => (int)kind)];

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ASTD_02_Every_BindingValueKind_is_represented_by_a_concrete_value_record()
        {
            BindingValueKind[] expected = [.. Enum.GetValues<BindingValueKind>().OrderBy(static kind => (int)kind)];
            BindingValueKind[] actual = [.. CreateAllValues().Select(static value => value.Kind).Distinct().OrderBy(static kind => (int)kind)];

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ASTD_03_All_node_records_support_pattern_matching_dispatch()
        {
            string[] dispatchedKinds =
            [
                .. CreateAllNodes().Select(static node => node switch
                {
                    QmlDocument => nameof(NodeKind.Document),
                    ImportNode => nameof(NodeKind.Import),
                    PragmaNode => nameof(NodeKind.Pragma),
                    ObjectDefinitionNode => nameof(NodeKind.ObjectDefinition),
                    InlineComponentNode => nameof(NodeKind.InlineComponent),
                    PropertyDeclarationNode => nameof(NodeKind.PropertyDeclaration),
                    PropertyAliasNode => nameof(NodeKind.PropertyAlias),
                    BindingNode => nameof(NodeKind.Binding),
                    GroupedBindingNode => nameof(NodeKind.GroupedBinding),
                    AttachedBindingNode => nameof(NodeKind.AttachedBinding),
                    ArrayBindingNode => nameof(NodeKind.ArrayBinding),
                    BehaviorOnNode => nameof(NodeKind.BehaviorOn),
                    SignalDeclarationNode => nameof(NodeKind.SignalDeclaration),
                    SignalHandlerNode => nameof(NodeKind.SignalHandler),
                    FunctionDeclarationNode => nameof(NodeKind.FunctionDeclaration),
                    EnumDeclarationNode => nameof(NodeKind.EnumDeclaration),
                    IdAssignmentNode => nameof(NodeKind.IdAssignment),
                    CommentNode => nameof(NodeKind.Comment),
                    _ => throw new InvalidOperationException("Unknown node type."),
                })
            ];

            Assert.Equal(Enum.GetValues<NodeKind>().Length, dispatchedKinds.Length);
            Assert.All(dispatchedKinds, static kindName => Assert.True(Enum.TryParse(kindName, out NodeKind _)));
        }

        [Fact]
        public void ASTD_04_All_value_records_support_pattern_matching_dispatch()
        {
            string[] dispatchedKinds =
            [
                .. CreateAllValues().Select(static value => value switch
                {
                    NumberLiteral => nameof(BindingValueKind.NumberLiteral),
                    StringLiteral => nameof(BindingValueKind.StringLiteral),
                    BooleanLiteral => nameof(BindingValueKind.BooleanLiteral),
                    NullLiteral => nameof(BindingValueKind.NullLiteral),
                    EnumReference => nameof(BindingValueKind.EnumReference),
                    ScriptExpression => nameof(BindingValueKind.ScriptExpression),
                    ScriptBlock => nameof(BindingValueKind.ScriptBlock),
                    ObjectValue => nameof(BindingValueKind.ObjectValue),
                    ArrayValue => nameof(BindingValueKind.ArrayValue),
                    _ => throw new InvalidOperationException("Unknown binding value type."),
                })
            ];

            Assert.Equal(Enum.GetValues<BindingValueKind>().Length, dispatchedKinds.Length);
            Assert.All(dispatchedKinds, static kindName => Assert.True(Enum.TryParse(kindName, out BindingValueKind _)));
        }

        [Fact]
        public void ASTD_05_Node_records_use_value_equality_and_include_base_fields()
        {
            CommentNode comment = new() { Text = "// trailing" };
            SourceSpan span = new(new SourcePosition(1, 1, 0), new SourcePosition(1, 10, 9));
            ImmutableArray<CommentNode> leadingComments = [comment];
            BindingNode left = new()
            {
                PropertyName = "width",
                Value = new NumberLiteral(42),
                Span = span,
                LeadingComments = leadingComments,
                TrailingComment = comment,
            };

            BindingNode equal = new()
            {
                PropertyName = "width",
                Value = new NumberLiteral(42),
                Span = span,
                LeadingComments = leadingComments,
                TrailingComment = comment,
            };

            BindingNode different = equal with { Span = null };

            Assert.Equal(left, equal);
            Assert.NotEqual(left, different);
        }

        [Fact]
        public void ASTD_06_Value_records_use_value_equality()
        {
            ImmutableArray<BindingValue> elements = [new NumberLiteral(1), new StringLiteral("a")];
            ArrayValue left = new(elements);
            ArrayValue equal = new(elements);
            ArrayValue different = new(elements.SetItem(0, new NumberLiteral(2)));

            Assert.Equal(left, equal);
            Assert.NotEqual(left, different);
        }

        [Fact]
        public void ASTD_07_Node_with_expression_creates_immutable_copy()
        {
            PropertyDeclarationNode original = new()
            {
                Name = "count",
                TypeName = "int",
                InitialValue = new NumberLiteral(1),
            };

            PropertyDeclarationNode updated = original with
            {
                IsRequired = true,
                InitialValue = new NumberLiteral(2),
            };

            Assert.False(original.IsRequired);
            Assert.Equal(new NumberLiteral(1), original.InitialValue);
            Assert.True(updated.IsRequired);
            Assert.Equal(new NumberLiteral(2), updated.InitialValue);
        }

        [Fact]
        public void ASTD_08_Value_with_expression_creates_immutable_copy()
        {
            ScriptExpression original = new("parent.width");
            ScriptExpression updated = original with { Code = "parent.width * 0.5" };

            Assert.Equal("parent.width", original.Code);
            Assert.Equal("parent.width * 0.5", updated.Code);
        }

        [Fact]
        public void ASTD_09_ImmutableArray_members_are_immutable_by_construction()
        {
            ObjectDefinitionNode objectNode = new()
            {
                TypeName = "Item",
                Members = [new IdAssignmentNode { Id = "root" }],
            };

            ObjectDefinitionNode appended = objectNode with
            {
                Members = objectNode.Members.Add(new BindingNode { PropertyName = "width", Value = new NumberLiteral(100) }),
            };

            _ = Assert.Single(objectNode.Members);
            Assert.Equal(2, appended.Members.Length);
        }

        [Fact]
        public void ASTD_10_ImmutableArray_values_are_immutable_by_construction()
        {
            ArrayValue value = new([new NumberLiteral(1)]);
            ArrayValue appended = value with { Elements = value.Elements.Add(new NumberLiteral(2)) };

            _ = Assert.Single(value.Elements);
            Assert.Equal(2, appended.Elements.Length);
        }

        [Fact]
        public void VF_01_Direct_NumberLiteral_from_integer_matches_expected_shape()
        {
            NumberLiteral value = new(42);

            Assert.Equal(BindingValueKind.NumberLiteral, value.Kind);
            Assert.Equal(42.0, value.Value);
        }

        [Fact]
        public void VF_02_Direct_NumberLiteral_from_decimal_matches_expected_shape()
        {
            NumberLiteral value = new(3.14);

            Assert.Equal(BindingValueKind.NumberLiteral, value.Kind);
            Assert.Equal(3.14, value.Value);
        }

        [Fact]
        public void VF_03_Direct_StringLiteral_matches_expected_shape()
        {
            StringLiteral value = new("hello");

            Assert.Equal(BindingValueKind.StringLiteral, value.Kind);
            Assert.Equal("hello", value.Value);
        }

        [Fact]
        public void VF_04_Direct_BooleanLiteral_true_matches_expected_shape()
        {
            BooleanLiteral value = new(true);

            Assert.Equal(BindingValueKind.BooleanLiteral, value.Kind);
            Assert.True(value.Value);
        }

        [Fact]
        public void VF_05_Direct_BooleanLiteral_false_matches_expected_shape()
        {
            BooleanLiteral value = new(false);

            Assert.Equal(BindingValueKind.BooleanLiteral, value.Kind);
            Assert.False(value.Value);
        }

        [Fact]
        public void VF_06_Direct_NullLiteral_uses_shared_instance()
        {
            NullLiteral value = NullLiteral.Instance;

            Assert.Equal(BindingValueKind.NullLiteral, value.Kind);
            Assert.Same(NullLiteral.Instance, value);
        }

        [Fact]
        public void VF_07_Direct_EnumReference_matches_expected_shape()
        {
            EnumReference value = new("Image", "Stretch");

            Assert.Equal(BindingValueKind.EnumReference, value.Kind);
            Assert.Equal("Image", value.TypeName);
            Assert.Equal("Stretch", value.MemberName);
        }

        [Fact]
        public void VF_08_Direct_ScriptExpression_matches_expected_shape()
        {
            ScriptExpression value = new("parent.width * 0.5");

            Assert.Equal(BindingValueKind.ScriptExpression, value.Kind);
            Assert.Equal("parent.width * 0.5", value.Code);
        }

        [Fact]
        public void VF_09_Direct_ScriptBlock_matches_expected_shape()
        {
            ScriptBlock value = new("{ var x = 1; return x; }");

            Assert.Equal(BindingValueKind.ScriptBlock, value.Kind);
            Assert.Equal("{ var x = 1; return x; }", value.Code);
        }

        [Fact]
        public void VF_10_Direct_ObjectValue_wraps_object_definition()
        {
            ObjectValue value = new(new ObjectDefinitionNode { TypeName = "Rectangle" });

            Assert.Equal(BindingValueKind.ObjectValue, value.Kind);
            Assert.Equal("Rectangle", value.Object.TypeName);
        }

        [Fact]
        public void VF_11_Direct_ArrayValue_preserves_order()
        {
            ArrayValue value = new([new NumberLiteral(1), new StringLiteral("two"), NullLiteral.Instance]);

            Assert.Equal(BindingValueKind.ArrayValue, value.Kind);
            Assert.Equal(3, value.Elements.Length);
            _ = Assert.IsType<NumberLiteral>(value.Elements[0]);
            _ = Assert.IsType<StringLiteral>(value.Elements[1]);
            _ = Assert.IsType<NullLiteral>(value.Elements[2]);
        }

        private static ImmutableArray<AstNode> CreateAllNodes()
        {
            ObjectDefinitionNode root = new() { TypeName = "Item" };
            CommentNode comment = new() { Text = "// note" };

            return
            [
                new QmlDocument
                {
                    Pragmas = [new PragmaNode { Name = PragmaName.Singleton }],
                    Imports = [new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtQuick", Version = "2.15" }],
                    RootObject = root,
                },
                new ImportNode { ImportKind = ImportKind.Directory, Path = "./controls" },
                new PragmaNode { Name = PragmaName.ComponentBehavior, Value = "Bound" },
                root,
                new InlineComponentNode { Name = "Badge", Body = new ObjectDefinitionNode { TypeName = "Rectangle" } },
                new PropertyDeclarationNode
                {
                    Name = "count",
                    TypeName = "int",
                    InitialValue = new NumberLiteral(0),
                },
                new PropertyAliasNode { Name = "labelText", Target = "label.text" },
                new BindingNode { PropertyName = "width", Value = new NumberLiteral(100) },
                new GroupedBindingNode
                {
                    GroupName = "font",
                    Bindings = [new BindingNode { PropertyName = "pixelSize", Value = new NumberLiteral(14) }],
                },
                new AttachedBindingNode
                {
                    AttachedTypeName = "Layout",
                    Bindings = [new BindingNode { PropertyName = "fillWidth", Value = new BooleanLiteral(true) }],
                },
                new ArrayBindingNode
                {
                    PropertyName = "states",
                    Elements = [new ObjectValue(new ObjectDefinitionNode { TypeName = "State" })],
                },
                new BehaviorOnNode
                {
                    PropertyName = "x",
                    Animation = new ObjectDefinitionNode { TypeName = "NumberAnimation" },
                },
                new SignalDeclarationNode
                {
                    Name = "clicked",
                    Parameters = [new ParameterDeclaration("x", "int")],
                },
                new SignalHandlerNode
                {
                    HandlerName = "onClicked",
                    Form = SignalHandlerForm.Arrow,
                    Code = "x => x + 1",
                    Parameters = ["x"],
                },
                new FunctionDeclarationNode
                {
                    Name = "compute",
                    Parameters = [new ParameterDeclaration("x", "int")],
                    ReturnType = "int",
                    Body = "{ return x + 1; }",
                },
                new EnumDeclarationNode
                {
                    Name = "Status",
                    Members = [new EnumMember("Active", null), new EnumMember("Inactive", 1)],
                },
                new IdAssignmentNode { Id = "root" },
                comment,
            ];
        }

        private static ImmutableArray<BindingValue> CreateAllValues()
        {
            return
            [
                new NumberLiteral(1),
                new StringLiteral("hello"),
                new BooleanLiteral(true),
                NullLiteral.Instance,
                new EnumReference("Image", "Stretch"),
                new ScriptExpression("parent.width * 0.5"),
                new ScriptBlock("{ var x = 1; return x; }"),
                new ObjectValue(new ObjectDefinitionNode { TypeName = "Rectangle" }),
                new ArrayValue([new NumberLiteral(1), new StringLiteral("two")]),
            ];
        }
    }
}
