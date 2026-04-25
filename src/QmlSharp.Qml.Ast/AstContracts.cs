using System.Collections.Immutable;
using System.Runtime.InteropServices;

#pragma warning disable MA0048

namespace QmlSharp.Qml.Ast
{
    /// <summary>
    /// All AST node kinds. Used for exhaustive switching and serialization.
    /// </summary>
    public enum NodeKind
    {
        Document,
        Import,
        Pragma,
        ObjectDefinition,
        InlineComponent,
        PropertyDeclaration,
        PropertyAlias,
        Binding,
        GroupedBinding,
        AttachedBinding,
        ArrayBinding,
        BehaviorOn,
        SignalDeclaration,
        SignalHandler,
        FunctionDeclaration,
        EnumDeclaration,
        IdAssignment,
        Comment,
    }

    /// <summary>
    /// All BindingValue kinds. Used by emitter contracts to determine formatting.
    /// </summary>
    public enum BindingValueKind
    {
        NumberLiteral,
        StringLiteral,
        BooleanLiteral,
        NullLiteral,
        EnumReference,
        ScriptExpression,
        ScriptBlock,
        ObjectValue,
        ArrayValue,
    }

    /// <summary>
    /// Qt 6.11 pragma names.
    /// </summary>
    public enum PragmaName
    {
        Singleton,
        ComponentBehavior,
        ListPropertyAssignBehavior,
        FunctionSignatureBehavior,
        NativeMethodBehavior,
        ValueTypeBehavior,
        NativeTextRendering,
        Translator,
    }

    /// <summary>
    /// Import statement kinds.
    /// </summary>
    public enum ImportKind
    {
        Module,
        Directory,
        JavaScript,
    }

    /// <summary>
    /// Signal handler code forms.
    /// </summary>
    public enum SignalHandlerForm
    {
        Expression,
        Block,
        Arrow,
    }

    /// <summary>
    /// Source position within a QML document.
    /// </summary>
    /// <param name="Line">1-based line index.</param>
    /// <param name="Column">1-based column index.</param>
    /// <param name="Offset">0-based offset in source text.</param>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct SourcePosition(int Line, int Column, int Offset);

    /// <summary>
    /// Source span for a range in source text.
    /// </summary>
    /// <param name="Start">Start position.</param>
    /// <param name="End">End position.</param>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct SourceSpan(SourcePosition Start, SourcePosition End);

    /// <summary>
    /// Abstract base for all AST nodes.
    /// </summary>
    public abstract record AstNode
    {
        /// <summary>
        /// Gets the node discriminator.
        /// </summary>
        public abstract NodeKind Kind { get; }

        /// <summary>
        /// Gets the source span in original QML text when known.
        /// </summary>
        public SourceSpan? Span { get; init; }

        /// <summary>
        /// Gets comments that precede this node in source order.
        /// </summary>
        public ImmutableArray<CommentNode> LeadingComments { get; init; } = ImmutableArray<CommentNode>.Empty;

        /// <summary>
        /// Gets a same-line trailing comment when present.
        /// </summary>
        public CommentNode? TrailingComment { get; init; }
    }

    /// <summary>
    /// Root node representing a complete QML document.
    /// </summary>
    public sealed record QmlDocument : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.Document;
    }

    /// <summary>
    /// Comment node contract.
    /// </summary>
    public sealed record CommentNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.Comment;

        /// <summary>
        /// Gets the comment text including delimiters.
        /// </summary>
        public required string Text { get; init; }

        /// <summary>
        /// Gets a value indicating whether the comment is a block comment.
        /// </summary>
        public bool IsBlock { get; init; }
    }

    /// <summary>
    /// Abstract base for binding value contracts.
    /// </summary>
    public abstract record BindingValue
    {
        /// <summary>
        /// Gets the binding value discriminator.
        /// </summary>
        public abstract BindingValueKind Kind { get; }
    }
}

#pragma warning restore MA0048
