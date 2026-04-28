using System.Collections.Immutable;
using QmlSharp.Qml.Ast;

namespace QmlSharp.Qml.Emitter
{
    internal static class EmitOrdering
    {
        internal static ImmutableArray<ImportNode> SortImports(ImmutableArray<ImportNode> imports)
        {
            return imports
                .Select((Import, Index) => new ImportSortEntry(Import, Index))
                .OrderBy(static entry => GetImportSortKey(entry.Import), StringComparer.Ordinal)
                .ThenBy(static entry => entry.Index)
                .Select(static entry => entry.Import)
                .ToImmutableArray();
        }

        internal static ImmutableArray<AstNode> NormalizeMembers(ImmutableArray<AstNode> members)
        {
            ImmutableArray<MemberSortEntry>.Builder entries = ImmutableArray.CreateBuilder<MemberSortEntry>();
            ImmutableArray<AstNode>.Builder pendingComments = ImmutableArray.CreateBuilder<AstNode>();
            int index = 0;

            foreach (AstNode member in members)
            {
                if (member is CommentNode)
                {
                    pendingComments.Add(member);
                    continue;
                }

                entries.Add(new MemberSortEntry(
                    member,
                    pendingComments.ToImmutable(),
                    GetMemberCategory(member),
                    index));
                pendingComments.Clear();
                index++;
            }

            ImmutableArray<AstNode> trailingComments = pendingComments.ToImmutable();
            ImmutableArray<MemberSortEntry> sortedEntries = entries
                .OrderBy(static entry => entry.Category)
                .ThenBy(static entry => entry.Index)
                .ToImmutableArray();

            ImmutableArray<AstNode>.Builder normalized = ImmutableArray.CreateBuilder<AstNode>();
            foreach (MemberSortEntry entry in sortedEntries)
            {
                normalized.AddRange(entry.LeadingComments);
                normalized.Add(entry.Member);
            }

            normalized.AddRange(trailingComments);
            return normalized.ToImmutable();
        }

        internal static int GetMemberCategory(AstNode member)
        {
            return member switch
            {
                IdAssignmentNode => 0,
                PropertyDeclarationNode => 1,
                PropertyAliasNode => 1,
                SignalDeclarationNode => 2,
                BindingNode => 3,
                GroupedBindingNode => 3,
                AttachedBindingNode => 3,
                ArrayBindingNode => 3,
                BehaviorOnNode => 3,
                SignalHandlerNode => 4,
                FunctionDeclarationNode => 5,
                ObjectDefinitionNode => 6,
                InlineComponentNode => 7,
                EnumDeclarationNode => 8,
                CommentNode => -1,
                _ => 99,
            };
        }

        private static string GetImportSortKey(ImportNode import)
        {
            string primary = import.ModuleUri ?? import.Path ?? string.Empty;
            string version = import.Version ?? string.Empty;
            string qualifier = import.Qualifier ?? string.Empty;

            return string.Concat(primary, "\u001F", version, "\u001F", qualifier, "\u001F", import.ImportKind.ToString());
        }

        private sealed record ImportSortEntry(ImportNode Import, int Index);

        private sealed record MemberSortEntry(
            AstNode Member,
            ImmutableArray<AstNode> LeadingComments,
            int Category,
            int Index);
    }
}
