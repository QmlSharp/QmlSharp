using QmlSharp.Qml.Ast.Builders;
using QmlSharp.Qml.Ast.Tests.Helpers;

namespace QmlSharp.Qml.Ast.Tests.Builder
{
    [Trait("Category", TestCategories.Unit)]
    public sealed class DocumentBuilderTests
    {
        [Fact]
        public void BD_01_Build_empty_document_with_root_object_only()
        {
            QmlDocument doc = new QmlDocumentBuilder()
                .SetRootObject("Item", _ => { })
                .Build();

            Assert.Empty(doc.Pragmas);
            Assert.Empty(doc.Imports);
            Assert.NotNull(doc.RootObject);
            Assert.Equal("Item", doc.RootObject.TypeName);
        }

        [Fact]
        public void BD_02_Build_document_with_single_pragma()
        {
            QmlDocument doc = new QmlDocumentBuilder()
                .AddPragma(PragmaName.Singleton)
                .SetRootObject("Item", _ => { })
                .Build();

            _ = Assert.Single(doc.Pragmas);
            Assert.Equal(PragmaName.Singleton, doc.Pragmas[0].Name);
            Assert.Null(doc.Pragmas[0].Value);
        }

        [Fact]
        public void BD_03_Build_document_with_multiple_pragmas_preserves_order()
        {
            QmlDocument doc = new QmlDocumentBuilder()
                .AddPragma(PragmaName.Singleton)
                .AddPragma(PragmaName.ComponentBehavior, "Bound")
                .AddPragma(PragmaName.ListPropertyAssignBehavior, "ReplaceIfNotDefault")
                .SetRootObject("Item", _ => { })
                .Build();

            Assert.Equal(3, doc.Pragmas.Length);
            Assert.Equal(PragmaName.Singleton, doc.Pragmas[0].Name);
            Assert.Equal(PragmaName.ComponentBehavior, doc.Pragmas[1].Name);
            Assert.Equal("Bound", doc.Pragmas[1].Value);
            Assert.Equal(PragmaName.ListPropertyAssignBehavior, doc.Pragmas[2].Name);
        }

        [Fact]
        public void BD_04_Build_document_with_module_import()
        {
            QmlDocument doc = new QmlDocumentBuilder()
                .AddModuleImport("QtQuick", "2.15")
                .SetRootObject("Item", _ => { })
                .Build();

            _ = Assert.Single(doc.Imports);
            Assert.Equal(ImportKind.Module, doc.Imports[0].ImportKind);
            Assert.Equal("QtQuick", doc.Imports[0].ModuleUri);
            Assert.Equal("2.15", doc.Imports[0].Version);
        }

        [Fact]
        public void BD_05_Build_document_with_directory_import()
        {
            QmlDocument doc = new QmlDocumentBuilder()
                .AddDirectoryImport("./components")
                .SetRootObject("Item", _ => { })
                .Build();

            _ = Assert.Single(doc.Imports);
            Assert.Equal(ImportKind.Directory, doc.Imports[0].ImportKind);
            Assert.Equal("./components", doc.Imports[0].Path);
        }

        [Fact]
        public void BD_06_Build_document_with_JavaScript_import()
        {
            QmlDocument doc = new QmlDocumentBuilder()
                .AddJavaScriptImport("utils.js", "Utils")
                .SetRootObject("Item", _ => { })
                .Build();

            _ = Assert.Single(doc.Imports);
            Assert.Equal(ImportKind.JavaScript, doc.Imports[0].ImportKind);
            Assert.Equal("utils.js", doc.Imports[0].Path);
            Assert.Equal("Utils", doc.Imports[0].Qualifier);
        }

        [Fact]
        public void BD_07_Build_document_with_mixed_imports_preserves_order()
        {
            QmlDocument doc = new QmlDocumentBuilder()
                .AddModuleImport("QtQuick", "2.15")
                .AddDirectoryImport("./components")
                .AddJavaScriptImport("utils.js", "Utils")
                .SetRootObject("Item", _ => { })
                .Build();

            Assert.Equal(3, doc.Imports.Length);
            Assert.Equal(ImportKind.Module, doc.Imports[0].ImportKind);
            Assert.Equal(ImportKind.Directory, doc.Imports[1].ImportKind);
            Assert.Equal(ImportKind.JavaScript, doc.Imports[2].ImportKind);
        }

        [Fact]
        public void BD_08_Build_document_with_pragmas_imports_and_root_object()
        {
            QmlDocument doc = new QmlDocumentBuilder()
                .AddPragma(PragmaName.Singleton)
                .AddModuleImport("QtQuick", "2.15")
                .SetRootObject("Rectangle", root =>
                {
                    _ = root.Binding("width", Values.Number(100));
                })
                .Build();

            _ = Assert.Single(doc.Pragmas);
            _ = Assert.Single(doc.Imports);
            Assert.Equal("Rectangle", doc.RootObject.TypeName);
            _ = Assert.Single(doc.RootObject.Members);
        }

        [Fact]
        public void BD_09_Build_without_root_object_throws_InvalidOperationException()
        {
            QmlDocumentBuilder builder = new();

            _ = Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void BD_10_Build_document_with_all_8_pragma_forms()
        {
            QmlDocument doc = new QmlDocumentBuilder()
                .AddPragma(PragmaName.Singleton)
                .AddPragma(PragmaName.ComponentBehavior, "Bound")
                .AddPragma(PragmaName.ListPropertyAssignBehavior, "ReplaceIfNotDefault")
                .AddPragma(PragmaName.FunctionSignatureBehavior, "Enforced")
                .AddPragma(PragmaName.NativeMethodBehavior, "AcceptThisObject")
                .AddPragma(PragmaName.ValueTypeBehavior, "Addressable")
                .AddPragma(PragmaName.NativeTextRendering)
                .AddPragma(PragmaName.Translator, "qsTr")
                .SetRootObject("Item", _ => { })
                .Build();

            Assert.Equal(8, doc.Pragmas.Length);
            PragmaName[] expectedNames = Enum.GetValues<PragmaName>();
            Assert.Equal(expectedNames.Length, doc.Pragmas.Length);
            for (int i = 0; i < expectedNames.Length; i++)
            {
                Assert.Equal(expectedNames[i], doc.Pragmas[i].Name);
            }
        }

        [Fact]
        public void BD_AddImport_adds_raw_ImportNode()
        {
            ImportNode raw = new()
            {
                ImportKind = ImportKind.Module,
                ModuleUri = "QtQuick.Controls",
                Version = "6.5",
                Qualifier = "Controls",
            };

            QmlDocument doc = new QmlDocumentBuilder()
                .AddImport(raw)
                .SetRootObject("Item", _ => { })
                .Build();

            _ = Assert.Single(doc.Imports);
            Assert.Equal(raw, doc.Imports[0]);
        }

        [Fact]
        public void BD_SetRootObject_from_prebuilt_node()
        {
            ObjectDefinitionNode prebuilt = new()
            {
                TypeName = "Rectangle",
                Members = [new IdAssignmentNode { Id = "myRect" }],
            };

            QmlDocument doc = new QmlDocumentBuilder()
                .SetRootObject(prebuilt)
                .Build();

            Assert.Equal("Rectangle", doc.RootObject.TypeName);
            _ = Assert.Single(doc.RootObject.Members);
        }
    }
}
