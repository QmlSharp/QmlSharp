using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Normalization
{
    public sealed class NormalizationTests
    {
        [Fact]
        [Trait("Category", TestCategories.Normalization)]
        public void NR_01_ObjectWithNonCanonicalMembers_NormalizeEmitsCanonicalCategoryOrder()
        {
            QmlDocument document = Document(
                new FunctionDeclarationNode { Name = "run", Body = "work()" },
                new BindingNode { PropertyName = "width", Value = Values.Number(100) },
                new IdAssignmentNode { Id = "root" },
                new PropertyDeclarationNode { Name = "count", TypeName = "int" },
                new SignalDeclarationNode { Name = "ready" },
                new SignalHandlerNode { HandlerName = "onReady", Form = SignalHandlerForm.Expression, Code = "handle()" },
                new ObjectDefinitionNode { TypeName = "Rectangle" },
                new InlineComponentNode { Name = "Foo", Body = new ObjectDefinitionNode { TypeName = "QtObject" } },
                new EnumDeclarationNode { Name = "Status", Members = [new EnumMember("Active", null)] });

            AssertNormalizedOutput(
                document,
                "Item {\n"
                    + "    id: root\n"
                    + "    property int count\n"
                    + "    signal ready()\n"
                    + "    width: 100\n"
                    + "    onReady: handle()\n"
                    + "    function run() {\n"
                    + "        work()\n"
                    + "    }\n"
                    + "    Rectangle {}\n"
                    + "    component Foo: QtObject {}\n"
                    + "    enum Status {\n"
                    + "        Active\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Normalization)]
        public void NR_02_ObjectWithIdAlreadyFirst_NormalizeKeepsIdFirst()
        {
            QmlDocument document = Document(
                new IdAssignmentNode { Id = "root" },
                new BindingNode { PropertyName = "width", Value = Values.Number(100) });

            AssertNormalizedOutput(document, "Item {\n    id: root\n    width: 100\n}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Normalization)]
        public void NR_03_InterleavedBindingsAndProperties_NormalizeGroupsPropertiesBeforeBindings()
        {
            QmlDocument document = Document(
                new BindingNode { PropertyName = "width", Value = Values.Number(100) },
                new PropertyDeclarationNode { Name = "count", TypeName = "int" },
                new BindingNode { PropertyName = "height", Value = Values.Number(200) },
                new PropertyDeclarationNode { Name = "title", TypeName = "string" });

            AssertNormalizedOutput(
                document,
                "Item {\n"
                    + "    property int count\n"
                    + "    property string title\n"
                    + "    width: 100\n"
                    + "    height: 200\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Normalization)]
        public void NR_04_ObjectWithComments_NormalizeKeepsStandaloneCommentsWithFollowingCategoryMember()
        {
            QmlDocument document = Document(
                new BindingNode { PropertyName = "width", Value = Values.Number(100) },
                new CommentNode { Text = "// id comment" },
                new IdAssignmentNode { Id = "root" },
                new CommentNode { Text = "// property comment" },
                new PropertyDeclarationNode { Name = "count", TypeName = "int" });

            AssertNormalizedOutput(
                document,
                "Item {\n"
                    + "    // id comment\n"
                    + "    id: root\n"
                    + "    // property comment\n"
                    + "    property int count\n"
                    + "    width: 100\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Normalization)]
        public void NR_05_NestedObjects_NormalizeAppliesRecursively()
        {
            ObjectDefinitionNode child = new()
            {
                TypeName = "Rectangle",
                Members =
                [
                    new BindingNode { PropertyName = "width", Value = Values.Number(100) },
                    new IdAssignmentNode { Id = "box" },
                ],
            };
            QmlDocument document = Document(child);

            AssertNormalizedOutput(
                document,
                "Item {\n"
                    + "    Rectangle {\n"
                    + "        id: box\n"
                    + "        width: 100\n"
                    + "    }\n"
                    + "}\n");
        }

        [Fact]
        [Trait("Category", TestCategories.Normalization)]
        public void NR_06_ObjectWithAllMemberCategories_NormalizeOrdersEveryCategoryByPriority()
        {
            QmlDocument document = Document(
                new EnumDeclarationNode { Name = "Status", Members = [new EnumMember("Active", null)] },
                new InlineComponentNode { Name = "Foo", Body = new ObjectDefinitionNode { TypeName = "QtObject" } },
                new ObjectDefinitionNode { TypeName = "Rectangle" },
                new FunctionDeclarationNode { Name = "run", Body = "work()" },
                new SignalHandlerNode { HandlerName = "onReady", Form = SignalHandlerForm.Expression, Code = "handle()" },
                new BindingNode { PropertyName = "width", Value = Values.Number(100) },
                new SignalDeclarationNode { Name = "ready" },
                new PropertyAliasNode { Name = "aliasText", Target = "label.text" },
                new IdAssignmentNode { Id = "root" });

            string output = Emit(document, new EmitOptions { Normalize = true });

            Assert.True(output.IndexOf("id: root", StringComparison.Ordinal) < output.IndexOf("property alias aliasText", StringComparison.Ordinal));
            Assert.True(output.IndexOf("property alias aliasText", StringComparison.Ordinal) < output.IndexOf("signal ready()", StringComparison.Ordinal));
            Assert.True(output.IndexOf("signal ready()", StringComparison.Ordinal) < output.IndexOf("width: 100", StringComparison.Ordinal));
            Assert.True(output.IndexOf("width: 100", StringComparison.Ordinal) < output.IndexOf("onReady: handle()", StringComparison.Ordinal));
            Assert.True(output.IndexOf("onReady: handle()", StringComparison.Ordinal) < output.IndexOf("function run()", StringComparison.Ordinal));
            Assert.True(output.IndexOf("function run()", StringComparison.Ordinal) < output.IndexOf("Rectangle {}", StringComparison.Ordinal));
            Assert.True(output.IndexOf("Rectangle {}", StringComparison.Ordinal) < output.IndexOf("component Foo", StringComparison.Ordinal));
            Assert.True(output.IndexOf("component Foo", StringComparison.Ordinal) < output.IndexOf("enum Status", StringComparison.Ordinal));
        }

        [Fact]
        [Trait("Category", TestCategories.Normalization)]
        public void NR_07_SortImportsWithUnsortedImports_EmitsImportsInOrdinalOrder()
        {
            QmlDocument document = new()
            {
                Imports =
                [
                    new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtQuick.Controls", Version = "6.0" },
                    new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtCore" },
                    new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtQuick", Version = "6.0" },
                ],
                RootObject = new ObjectDefinitionNode { TypeName = "Item" },
            };

            string output = Emit(document, new EmitOptions { SortImports = true });

            Assert.Equal("import QtCore\nimport QtQuick 6.0\nimport QtQuick.Controls 6.0\n\nItem {}\n", output);
        }

        [Fact]
        [Trait("Category", TestCategories.Normalization)]
        public void NR_07B_SortImportsWithPathImports_UsesModuleUriOrPathOrdinalOrder()
        {
            QmlDocument document = new()
            {
                Imports =
                [
                    new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtQuick" },
                    new ImportNode { ImportKind = ImportKind.JavaScript, Path = "scripts/utils.js", Qualifier = "Utils" },
                    new ImportNode { ImportKind = ImportKind.Directory, Path = "./components" },
                    new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtCore" },
                ],
                RootObject = new ObjectDefinitionNode { TypeName = "Item" },
            };

            string output = Emit(document, new EmitOptions { SortImports = true });

            Assert.Equal(
                "import \"./components\"\n"
                    + "import QtCore\n"
                    + "import QtQuick\n"
                    + "import \"scripts/utils.js\" as Utils\n"
                    + "\n"
                    + "Item {}\n",
                output);
        }

        [Fact]
        [Trait("Category", TestCategories.Normalization)]
        public void NR_08_SortImportsWithAlreadySortedImports_KeepsStableOrder()
        {
            QmlDocument document = new()
            {
                Imports =
                [
                    new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtCore" },
                    new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtQuick", Version = "6.0" },
                    new ImportNode { ImportKind = ImportKind.Module, ModuleUri = "QtQuick.Controls", Version = "6.0" },
                ],
                RootObject = new ObjectDefinitionNode { TypeName = "Item" },
            };

            string output = Emit(document, new EmitOptions { SortImports = true });

            Assert.Equal("import QtCore\nimport QtQuick 6.0\nimport QtQuick.Controls 6.0\n\nItem {}\n", output);
        }

        private static QmlDocument Document(params AstNode[] members)
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

        private static void AssertNormalizedOutput(QmlDocument document, string expected)
        {
            string output = Emit(document, new EmitOptions { Normalize = true });

            Assert.Equal(expected, output);
            LineEndingAssert.ContainsOnlyLf(output);
        }

        private static string Emit(QmlDocument document, EmitOptions options)
        {
            IQmlEmitter emitter = new QmlEmitter();

            return emitter.Emit(document, options);
        }
    }
}
