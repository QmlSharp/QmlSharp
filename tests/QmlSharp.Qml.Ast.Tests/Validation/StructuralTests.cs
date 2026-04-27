using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Tests.Helpers;

namespace QmlSharp.Qml.Ast.Tests.Validation
{
    public sealed class StructuralTests
    {
        [Fact]
        public void VS_01_Valid_document_passes_structural_validation()
        {
            QmlDocument document = AstFixtures.MinimalDocument();

            ImmutableArray<AstDiagnostic> diagnostics = Validate(document);

            Assert.Empty(diagnostics);
        }

        [Fact]
        public void VS_02_Duplicate_id_values_report_E001()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .SetRootObject("Item", root =>
                {
                    _ = root.Id("duplicateId")
                        .Child("Text", child => _ = child.Id("duplicateId"));
                })
                .Build();

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E001_DuplicateId, diagnostic.Code);
            Assert.Contains("duplicateId", diagnostic.Message, StringComparison.Ordinal);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            _ = Assert.IsType<IdAssignmentNode>(diagnostic.Node);
        }

        [Fact]
        public void VS_03_Invalid_id_starting_with_digit_reports_E002_with_span_and_node()
        {
            SourceSpan span = CreateSpan();
            IdAssignmentNode invalidId = new()
            {
                Id = "1bad",
                Span = span,
            };
            QmlDocument document = DocumentWithMembers(invalidId);

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E002_InvalidIdFormat, diagnostic.Code);
            Assert.Contains("1bad", diagnostic.Message, StringComparison.Ordinal);
            Assert.Equal(span, diagnostic.Span);
            Assert.Same(invalidId, diagnostic.Node);
        }

        [Fact]
        public void VS_04_Invalid_id_containing_spaces_reports_E002()
        {
            QmlDocument document = DocumentWithMembers(new IdAssignmentNode { Id = "bad id" });

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E002_InvalidIdFormat, diagnostic.Code);
        }

        [Fact]
        public void VS_05_Duplicate_property_name_in_same_object_reports_E003()
        {
            BindingNode duplicate = new()
            {
                PropertyName = "count",
                Value = Values.Number(1),
            };
            QmlDocument document = DocumentWithMembers(
                new PropertyDeclarationNode { Name = "count", TypeName = "int" },
                duplicate);

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E003_DuplicatePropertyName, diagnostic.Code);
            Assert.Contains("count", diagnostic.Message, StringComparison.Ordinal);
            Assert.Same(duplicate, diagnostic.Node);
        }

        [Fact]
        public void VS_06_Duplicate_signal_name_in_same_object_reports_E004()
        {
            SignalDeclarationNode duplicate = new() { Name = "clicked" };
            QmlDocument document = DocumentWithMembers(
                new SignalDeclarationNode { Name = "clicked" },
                duplicate);

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E004_DuplicateSignalName, diagnostic.Code);
            Assert.Contains("clicked", diagnostic.Message, StringComparison.Ordinal);
            Assert.Same(duplicate, diagnostic.Node);
        }

        [Fact]
        public void VS_07_Signal_handler_name_not_starting_with_on_reports_E005()
        {
            QmlDocument document = DocumentWithMembers(new SignalHandlerNode
            {
                HandlerName = "clicked",
                Form = SignalHandlerForm.Expression,
                Code = "console.log('clicked')",
            });

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E005_InvalidHandlerNameFormat, diagnostic.Code);
        }

        [Fact]
        public void VS_08_Readonly_required_property_reports_E006()
        {
            QmlDocument document = DocumentWithMembers(new PropertyDeclarationNode
            {
                Name = "title",
                TypeName = "string",
                IsReadonly = true,
                IsRequired = true,
            });

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E006_ConflictingPropertyModifiers, diagnostic.Code);
            Assert.Contains("title", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void VS_09_Invalid_import_reports_E007()
        {
            QmlDocument document = new()
            {
                Imports =
                [
                    new ImportNode
                    {
                        ImportKind = ImportKind.Module,
                    },
                ],
                RootObject = new ObjectDefinitionNode { TypeName = "Item" },
            };

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E007_InvalidImport, diagnostic.Code);
        }

        [Fact]
        public void VS_10_Duplicate_enum_name_in_same_object_reports_E008()
        {
            EnumDeclarationNode duplicate = new()
            {
                Name = "Status",
                Members = [new EnumMember("Pending", null)],
            };
            QmlDocument document = DocumentWithMembers(
                new EnumDeclarationNode
                {
                    Name = "Status",
                    Members = [new EnumMember("Active", null)],
                },
                duplicate);

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E008_DuplicateEnumName, diagnostic.Code);
            Assert.Contains("Status", diagnostic.Message, StringComparison.Ordinal);
            Assert.Same(duplicate, diagnostic.Node);
        }

        [Fact]
        public void VS_11_Inline_component_name_not_starting_with_uppercase_reports_E009()
        {
            QmlDocument document = DocumentWithMembers(new InlineComponentNode
            {
                Name = "badge",
                Body = new ObjectDefinitionNode { TypeName = "Rectangle" },
            });

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E009_InvalidInlineComponentName, diagnostic.Code);
            Assert.Contains("badge", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void VS_12_Excessive_nesting_depth_reports_E010()
        {
            QmlDocument document = new()
            {
                RootObject = CreateNestedObjects(21),
            };

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E010_ExcessiveNestingDepth, diagnostic.Code);
        }

        [Fact]
        public void VS_13_Multiple_errors_are_reported_in_one_pass()
        {
            QmlDocument document = new()
            {
                Imports =
                [
                    new ImportNode
                    {
                        ImportKind = ImportKind.JavaScript,
                        Path = "utils.js",
                    },
                ],
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members =
                    [
                        new IdAssignmentNode { Id = "1bad" },
                        new PropertyDeclarationNode { Name = "count", TypeName = "int" },
                        new BindingNode { PropertyName = "count", Value = Values.Number(1) },
                        new SignalDeclarationNode { Name = "clicked" },
                        new SignalDeclarationNode { Name = "clicked" },
                        new InlineComponentNode
                        {
                            Name = "badge",
                            Body = new ObjectDefinitionNode { TypeName = "Rectangle" },
                        },
                    ],
                },
            };

            ImmutableArray<AstDiagnostic> diagnostics = Validate(document);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E007_InvalidImport);
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E002_InvalidIdFormat);
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E003_DuplicatePropertyName);
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E004_DuplicateSignalName);
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E009_InvalidInlineComponentName);
        }

        [Fact]
        public void VS_14_Valid_full_syntax_document_passes_structural_validation()
        {
            QmlDocument document = AstFixtures.FullSyntaxDocument();

            ImmutableArray<AstDiagnostic> diagnostics = Validate(document);

            Assert.Empty(diagnostics);
        }

        [Fact]
        public void VSD_01_All_structural_diagnostic_codes_are_triggerable()
        {
            Dictionary<DiagnosticCode, QmlDocument> triggerDocuments = new()
            {
                [DiagnosticCode.E001_DuplicateId] = new QmlDocumentBuilder()
                    .SetRootObject("Item", root => _ = root.Id("item").Child("Item", child => _ = child.Id("item")))
                    .Build(),
                [DiagnosticCode.E002_InvalidIdFormat] = DocumentWithMembers(new IdAssignmentNode { Id = "bad id" }),
                [DiagnosticCode.E003_DuplicatePropertyName] = DocumentWithMembers(
                    new BindingNode { PropertyName = "width", Value = Values.Number(1) },
                    new BindingNode { PropertyName = "width", Value = Values.Number(2) }),
                [DiagnosticCode.E004_DuplicateSignalName] = DocumentWithMembers(
                    new SignalDeclarationNode { Name = "accepted" },
                    new SignalDeclarationNode { Name = "accepted" }),
                [DiagnosticCode.E005_InvalidHandlerNameFormat] = DocumentWithMembers(new SignalHandlerNode
                {
                    HandlerName = "accepted",
                    Form = SignalHandlerForm.Expression,
                    Code = "true",
                }),
                [DiagnosticCode.E006_ConflictingPropertyModifiers] = DocumentWithMembers(new PropertyDeclarationNode
                {
                    Name = "name",
                    TypeName = "string",
                    IsReadonly = true,
                    IsRequired = true,
                }),
                [DiagnosticCode.E007_InvalidImport] = new QmlDocument
                {
                    Imports = [new ImportNode { ImportKind = ImportKind.Directory }],
                    RootObject = new ObjectDefinitionNode { TypeName = "Item" },
                },
                [DiagnosticCode.E008_DuplicateEnumName] = DocumentWithMembers(new EnumDeclarationNode
                {
                    Name = "Mode",
                    Members =
                    [
                        new EnumMember("Light", null),
                        new EnumMember("Light", 1),
                    ],
                }),
                [DiagnosticCode.E009_InvalidInlineComponentName] = DocumentWithMembers(new InlineComponentNode
                {
                    Name = "badge",
                    Body = new ObjectDefinitionNode { TypeName = "Item" },
                }),
                [DiagnosticCode.E010_ExcessiveNestingDepth] = new QmlDocument
                {
                    RootObject = CreateNestedObjects(21),
                },
            };

            foreach (KeyValuePair<DiagnosticCode, QmlDocument> triggerDocument in triggerDocuments)
            {
                ImmutableArray<AstDiagnostic> diagnostics = Validate(triggerDocument.Value);
                Assert.Contains(diagnostics, diagnostic => diagnostic.Code == triggerDocument.Key);
            }
        }

        [Fact]
        public void VSD_02_Duplicate_enum_members_report_E008()
        {
            QmlDocument document = DocumentWithMembers(new EnumDeclarationNode
            {
                Name = "Status",
                Members =
                [
                    new EnumMember("Active", null),
                    new EnumMember("Active", 1),
                ],
            });

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E008_DuplicateEnumName, diagnostic.Code);
            Assert.Contains("Active", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void VSD_03_Duplicate_names_are_scoped_to_the_containing_object()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .SetRootObject("Item", root =>
                {
                    _ = root.PropertyDeclaration("count", "int")
                        .SignalDeclaration("accepted")
                        .EnumDeclaration("Status", new EnumMember("Active", null))
                        .Child("Item", child =>
                        {
                            _ = child.PropertyDeclaration("count", "int")
                                .SignalDeclaration("accepted")
                                .EnumDeclaration("Status", new EnumMember("Active", null));
                        });
                })
                .Build();

            ImmutableArray<AstDiagnostic> diagnostics = Validate(document);

            Assert.Empty(diagnostics);
        }

        private static ImmutableArray<AstDiagnostic> Validate(QmlDocument document)
        {
            QmlAstValidator validator = new();
            return validator.ValidateStructure(document);
        }

        private static AstDiagnostic SingleDiagnostic(QmlDocument document)
        {
            ImmutableArray<AstDiagnostic> diagnostics = Validate(document);
            return Assert.Single(diagnostics);
        }

        private static QmlDocument DocumentWithMembers(params AstNode[] members)
        {
            return new QmlDocument
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members = [.. members],
                },
            };
        }

        private static ObjectDefinitionNode CreateNestedObjects(int levels)
        {
            if (levels <= 1)
            {
                return new ObjectDefinitionNode { TypeName = "Item" };
            }

            return new ObjectDefinitionNode
            {
                TypeName = "Item",
                Members = [CreateNestedObjects(levels - 1)],
            };
        }

        private static SourceSpan CreateSpan()
        {
            SourcePosition start = new(Line: 3, Column: 5, Offset: 42);
            SourcePosition end = new(Line: 3, Column: 9, Offset: 46);
            return new SourceSpan(start, end);
        }
    }
}
