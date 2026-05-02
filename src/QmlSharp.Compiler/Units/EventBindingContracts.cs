#pragma warning disable MA0048

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Per-project event binding index for validation, diagnostics, and tooling.
    /// </summary>
    public sealed record EventBindingsIndex(
        string SchemaVersion,
        ImmutableArray<CommandBindingEntry> Commands,
        ImmutableArray<EffectBindingEntry> Effects)
    {
        /// <summary>Gets an empty event binding index.</summary>
        public static EventBindingsIndex Empty { get; } =
            new("1.0", ImmutableArray<CommandBindingEntry>.Empty, ImmutableArray<EffectBindingEntry>.Empty);
    }

    /// <summary>A command binding entry in <c>event-bindings.json</c>.</summary>
    public sealed record CommandBindingEntry(
        string ViewModelClass,
        string CommandName,
        int CommandId,
        ImmutableArray<string> ParameterTypes);

    /// <summary>An effect binding entry in <c>event-bindings.json</c>.</summary>
    public sealed record EffectBindingEntry(
        string ViewModelClass,
        string EffectName,
        int EffectId,
        string PayloadType);

    /// <summary>Builds event binding indexes from schemas.</summary>
    public interface IEventBindingsBuilder
    {
        /// <summary>Builds one event binding index from schemas.</summary>
        EventBindingsIndex Build(ImmutableArray<ViewModelSchema> schemas);
    }
}

#pragma warning restore MA0048
