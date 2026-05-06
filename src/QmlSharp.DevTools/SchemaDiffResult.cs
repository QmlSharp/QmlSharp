namespace QmlSharp.DevTools
{
    /// <summary>
    /// Dev-tools-owned restart analysis derived from comparing previous and current schema payloads.
    /// </summary>
    /// <param name="HasStructuralChanges">True when any State, Command, or Effect member was added or removed.</param>
    /// <param name="AffectedViewModels">ViewModels affected by the structural change.</param>
    /// <param name="Reasons">Human-readable reasons for the structural change result.</param>
    public sealed record SchemaDiffResult(
        bool HasStructuralChanges,
        IReadOnlyList<string> AffectedViewModels,
        IReadOnlyList<string> Reasons);
}
