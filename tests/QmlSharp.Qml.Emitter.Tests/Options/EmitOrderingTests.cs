using QmlSharp.Qml.Emitter.Tests.Helpers;

namespace QmlSharp.Qml.Emitter.Tests.Options
{
    public sealed class EmitOrderingTests
    {
        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void Ordering_SortImports_UsesOrdinalDeterministicOrder()
        {
            ImportNode controls = new() { ImportKind = ImportKind.Module, ModuleUri = "QtQuick.Controls", Version = "6.0" };
            ImportNode core = new() { ImportKind = ImportKind.Module, ModuleUri = "QtCore" };
            ImportNode quick = new() { ImportKind = ImportKind.Module, ModuleUri = "QtQuick", Version = "6.0" };
            ImmutableArray<ImportNode> imports = [controls, core, quick];

            ImmutableArray<ImportNode> sorted = EmitOrdering.SortImports(imports);

            Assert.Equal(3, sorted.Length);
            Assert.Same(core, sorted[0]);
            Assert.Same(quick, sorted[1]);
            Assert.Same(controls, sorted[2]);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void Ordering_NormalizeMembers_UsesCanonicalMemberCategoriesAndStableOrder()
        {
            BindingNode firstBinding = new() { PropertyName = "width", Value = Values.Number(100) };
            ObjectDefinitionNode child = new() { TypeName = "Rectangle" };
            IdAssignmentNode id = new() { Id = "root" };
            PropertyDeclarationNode property = new() { Name = "count", TypeName = "int" };
            BindingNode secondBinding = new() { PropertyName = "height", Value = Values.Number(200) };
            ImmutableArray<AstNode> members = [firstBinding, child, id, property, secondBinding];

            ImmutableArray<AstNode> normalized = EmitOrdering.NormalizeMembers(members);

            Assert.Equal(5, normalized.Length);
            Assert.Same(id, normalized[0]);
            Assert.Same(property, normalized[1]);
            Assert.Same(firstBinding, normalized[2]);
            Assert.Same(secondBinding, normalized[3]);
            Assert.Same(child, normalized[4]);
        }

        [Fact]
        [Trait("Category", TestCategories.Contract)]
        public void Ordering_NormalizeMembers_AttachesStandaloneCommentsToFollowingMember()
        {
            BindingNode binding = new() { PropertyName = "width", Value = Values.Number(100) };
            CommentNode comment = new() { Text = "// leading", IsBlock = false };
            IdAssignmentNode id = new() { Id = "root" };
            ImmutableArray<AstNode> members = [binding, comment, id];

            ImmutableArray<AstNode> normalized = EmitOrdering.NormalizeMembers(members);

            Assert.Equal(3, normalized.Length);
            Assert.Same(comment, normalized[0]);
            Assert.Same(id, normalized[1]);
            Assert.Same(binding, normalized[2]);
        }
    }
}
