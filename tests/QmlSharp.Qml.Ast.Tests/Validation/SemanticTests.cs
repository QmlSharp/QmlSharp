using System.Xml.Linq;
using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Tests.Helpers;

namespace QmlSharp.Qml.Ast.Tests.Validation
{
    public sealed class SemanticTests
    {
        [Fact]
        public void SM_01_Unknown_type_name_reports_E100()
        {
            QmlDocument document = AstFixtures.MinimalDocument("MissingWidget");

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E100_UnknownType, diagnostic.Code);
            Assert.Contains("MissingWidget", diagnostic.Message, StringComparison.Ordinal);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            _ = Assert.IsType<ObjectDefinitionNode>(diagnostic.Node);
        }

        [Fact]
        public void SM_02_Unknown_property_on_known_type_reports_E101()
        {
            QmlDocument document = DocumentWithMembers(new BindingNode
            {
                PropertyName = "doesNotExist",
                Value = Values.Number(1),
            });

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E101_UnknownProperty, diagnostic.Code);
            Assert.Contains("Item", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("doesNotExist", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SM_03_Unknown_signal_on_known_type_reports_E102()
        {
            QmlDocument document = DocumentWithMembers(new SignalHandlerNode
            {
                HandlerName = "onDoesNotExist",
                Form = SignalHandlerForm.Expression,
                Code = "true",
            });

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E102_UnknownSignal, diagnostic.Code);
        }

        [Fact]
        public void SM_04_Unknown_attached_type_reports_E103()
        {
            QmlDocument document = DocumentWithMembers(new AttachedBindingNode
            {
                AttachedTypeName = "MissingAttached",
                Bindings = [new BindingNode { PropertyName = "enabled", Value = Values.Boolean(true) }],
            });

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E103_UnknownAttachedType, diagnostic.Code);
            Assert.Contains("MissingAttached", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SM_05_Required_property_declaration_without_initializer_reports_E104()
        {
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Text",
                    Members =
                    [
                        new PropertyDeclarationNode
                        {
                            Name = "text",
                            TypeName = "string",
                            IsRequired = true,
                        },
                    ],
                },
            };

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E104_RequiredPropertyNotSet, diagnostic.Code);
            Assert.Contains("text", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SM_06_Readonly_property_bound_reports_E105()
        {
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Image",
                    Members =
                    [
                        new BindingNode
                        {
                            PropertyName = "sourceSize",
                            Value = Values.Number(24),
                        },
                    ],
                },
            };

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E105_ReadonlyPropertyBound, diagnostic.Code);
        }

        [Fact]
        public void SM_07_Invalid_enum_reference_reports_E106()
        {
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Image",
                    Members =
                    [
                        new BindingNode
                        {
                            PropertyName = "fillMode",
                            Value = Values.Enum("Image", "Tile"),
                        },
                    ],
                },
            };

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E106_InvalidEnumReference, diagnostic.Code);
        }

        [Fact]
        public void SM_08_Unknown_module_import_reports_E107()
        {
            QmlDocument document = new()
            {
                Imports =
                [
                    new ImportNode
                    {
                        ImportKind = ImportKind.Module,
                        ModuleUri = "Missing.Module",
                    },
                ],
                RootObject = new ObjectDefinitionNode { TypeName = "Item" },
            };

            AstDiagnostic diagnostic = SingleDiagnostic(document);

            Assert.Equal(DiagnosticCode.E107_UnknownModule, diagnostic.Code);
            Assert.Contains("Missing.Module", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SM_09_Valid_document_with_known_types_passes_semantic_validation()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddModuleImport("QtQuick", "6.11")
                .SetRootObject("Rectangle", root =>
                {
                    _ = root.Binding("width", Values.Number(100))
                        .Binding("color", Values.String("red"))
                        .SignalHandler("onWidthChanged", SignalHandlerForm.Expression, "console.log(width)")
                        .AttachedBinding("Layout", layout => _ = layout.Binding("fillWidth", Values.Boolean(true)))
                        .Child("Text", text => _ = text.Binding("text", Values.String("Hello")));
                })
                .Build();

            ImmutableArray<AstDiagnostic> diagnostics = Validate(document);

            Assert.Empty(diagnostics);
        }

        [Fact]
        public void SM_10_Multiple_semantic_errors_in_same_document_are_all_reported()
        {
            QmlDocument document = new()
            {
                RootObject = new ObjectDefinitionNode
                {
                    TypeName = "Item",
                    Members =
                    [
                        new BindingNode { PropertyName = "missingProperty", Value = Values.Enum("Image", "Tile") },
                        new SignalHandlerNode
                        {
                            HandlerName = "onMissingSignal",
                            Form = SignalHandlerForm.Block,
                            Code = "console.log('missing')",
                        },
                        new AttachedBindingNode
                        {
                            AttachedTypeName = "MissingAttached",
                            Bindings = [new BindingNode { PropertyName = "enabled", Value = Values.Boolean(true) }],
                        },
                        new ObjectDefinitionNode { TypeName = "MissingChild" },
                    ],
                },
            };

            ImmutableArray<AstDiagnostic> diagnostics = Validate(document);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E101_UnknownProperty);
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E102_UnknownSignal);
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E103_UnknownAttachedType);
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E106_InvalidEnumReference);
            Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.E100_UnknownType);
        }

        [Fact]
        public void SM_11_Unused_import_reports_W001_warning_without_error()
        {
            QmlDocument document = new QmlDocumentBuilder()
                .AddModuleImport("QtQuick", "6.11")
                .AddModuleImport("QtQuick.Controls", "6.11")
                .SetRootObject("Rectangle", root => _ = root.Binding("width", Values.Number(100)))
                .Build();

            ImmutableArray<AstDiagnostic> diagnostics = Validate(document);

            AstDiagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Equal(DiagnosticCode.W001_UnusedImport, diagnostic.Code);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.DoesNotContain(diagnostics, static item => item.Severity == DiagnosticSeverity.Error);
        }

        [Fact]
        public void SMD_01_QmlAst_project_has_no_registry_project_reference()
        {
            string projectPath = Path.Combine(FindRepositoryRoot(), "src", "QmlSharp.Qml.Ast", "QmlSharp.Qml.Ast.csproj");
            XDocument project = XDocument.Load(projectPath);

            IEnumerable<XElement> projectReferences = project
                .Descendants()
                .Where(static element => element.Name.LocalName == "ProjectReference");

            Assert.DoesNotContain(
                projectReferences,
                static reference => ((string?)reference.Attribute("Include"))?.Contains("QmlSharp.Registry", StringComparison.OrdinalIgnoreCase) == true);
        }

        private static ImmutableArray<AstDiagnostic> Validate(QmlDocument document)
        {
            QmlAstValidator validator = new();
            TestTypeChecker typeChecker = new();
            return validator.ValidateSemantic(document, typeChecker);
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

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "QmlSharp.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not find repository root from the test output directory.");
        }
    }
}
