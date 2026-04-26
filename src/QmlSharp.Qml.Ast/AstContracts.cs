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

        /// <summary>
        /// Gets pragmas declared at the top of the file.
        /// </summary>
        public ImmutableArray<PragmaNode> Pragmas { get; init; } = ImmutableArray<PragmaNode>.Empty;

        /// <summary>
        /// Gets import statements in declaration order.
        /// </summary>
        public ImmutableArray<ImportNode> Imports { get; init; } = ImmutableArray<ImportNode>.Empty;

        /// <summary>
        /// Gets the single root object.
        /// </summary>
        public required ObjectDefinitionNode RootObject { get; init; }
    }

    /// <summary>
    /// Import statement node.
    /// </summary>
    public sealed record ImportNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.Import;

        /// <summary>
        /// Gets the import form.
        /// </summary>
        public required ImportKind ImportKind { get; init; }

        /// <summary>
        /// Gets module URI for module imports.
        /// </summary>
        public string? ModuleUri { get; init; }

        /// <summary>
        /// Gets major.minor module version.
        /// </summary>
        public string? Version { get; init; }

        /// <summary>
        /// Gets filesystem path for directory or JavaScript imports.
        /// </summary>
        public string? Path { get; init; }

        /// <summary>
        /// Gets optional import qualifier.
        /// </summary>
        public string? Qualifier { get; init; }
    }

    /// <summary>
    /// Pragma directive node.
    /// </summary>
    public sealed record PragmaNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.Pragma;

        /// <summary>
        /// Gets pragma name.
        /// </summary>
        public required PragmaName Name { get; init; }

        /// <summary>
        /// Gets optional pragma value.
        /// </summary>
        public string? Value { get; init; }
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
    /// Object definition node.
    /// </summary>
    public sealed record ObjectDefinitionNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.ObjectDefinition;

        /// <summary>
        /// Gets QML type name.
        /// </summary>
        public required string TypeName { get; init; }

        /// <summary>
        /// Gets ordered object members.
        /// </summary>
        public ImmutableArray<AstNode> Members { get; init; } = ImmutableArray<AstNode>.Empty;
    }

    /// <summary>
    /// Inline component declaration node.
    /// </summary>
    public sealed record InlineComponentNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.InlineComponent;

        /// <summary>
        /// Gets inline component name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets component body root object.
        /// </summary>
        public required ObjectDefinitionNode Body { get; init; }
    }

    /// <summary>
    /// Id assignment node.
    /// </summary>
    public sealed record IdAssignmentNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.IdAssignment;

        /// <summary>
        /// Gets id value.
        /// </summary>
        public required string Id { get; init; }
    }

    /// <summary>
    /// Property declaration node.
    /// </summary>
    public sealed record PropertyDeclarationNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.PropertyDeclaration;

        /// <summary>
        /// Gets property name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets property type name.
        /// </summary>
        public required string TypeName { get; init; }

        /// <summary>
        /// Gets a value indicating whether this is a default property.
        /// </summary>
        public bool IsDefault { get; init; }

        /// <summary>
        /// Gets a value indicating whether this is a required property.
        /// </summary>
        public bool IsRequired { get; init; }

        /// <summary>
        /// Gets a value indicating whether this is a readonly property.
        /// </summary>
        public bool IsReadonly { get; init; }

        /// <summary>
        /// Gets optional initial value.
        /// </summary>
        public BindingValue? InitialValue { get; init; }
    }

    /// <summary>
    /// Property alias declaration node.
    /// </summary>
    public sealed record PropertyAliasNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.PropertyAlias;

        /// <summary>
        /// Gets alias name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets alias target path.
        /// </summary>
        public required string Target { get; init; }

        /// <summary>
        /// Gets a value indicating whether this is a default alias.
        /// </summary>
        public bool IsDefault { get; init; }
    }

    /// <summary>
    /// Property binding node.
    /// </summary>
    public sealed record BindingNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.Binding;

        /// <summary>
        /// Gets bound property name.
        /// </summary>
        public required string PropertyName { get; init; }

        /// <summary>
        /// Gets bound value.
        /// </summary>
        public required BindingValue Value { get; init; }
    }

    /// <summary>
    /// Grouped binding node.
    /// </summary>
    public sealed record GroupedBindingNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.GroupedBinding;

        /// <summary>
        /// Gets group name.
        /// </summary>
        public required string GroupName { get; init; }

        /// <summary>
        /// Gets bindings in this group.
        /// </summary>
        public ImmutableArray<BindingNode> Bindings { get; init; } = ImmutableArray<BindingNode>.Empty;
    }

    /// <summary>
    /// Attached binding node.
    /// </summary>
    public sealed record AttachedBindingNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.AttachedBinding;

        /// <summary>
        /// Gets attached type name.
        /// </summary>
        public required string AttachedTypeName { get; init; }

        /// <summary>
        /// Gets bindings on the attached type.
        /// </summary>
        public ImmutableArray<BindingNode> Bindings { get; init; } = ImmutableArray<BindingNode>.Empty;
    }

    /// <summary>
    /// Array binding node.
    /// </summary>
    public sealed record ArrayBindingNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.ArrayBinding;

        /// <summary>
        /// Gets bound property name.
        /// </summary>
        public required string PropertyName { get; init; }

        /// <summary>
        /// Gets array elements.
        /// </summary>
        public ImmutableArray<BindingValue> Elements { get; init; } = ImmutableArray<BindingValue>.Empty;
    }

    /// <summary>
    /// Behavior-on binding node.
    /// </summary>
    public sealed record BehaviorOnNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.BehaviorOn;

        /// <summary>
        /// Gets target property name.
        /// </summary>
        public required string PropertyName { get; init; }

        /// <summary>
        /// Gets behavior animation object.
        /// </summary>
        public required ObjectDefinitionNode Animation { get; init; }
    }

    /// <summary>
    /// Signal declaration node.
    /// </summary>
    public sealed record SignalDeclarationNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.SignalDeclaration;

        /// <summary>
        /// Gets signal name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets signal parameters.
        /// </summary>
        public ImmutableArray<ParameterDeclaration> Parameters { get; init; } = ImmutableArray<ParameterDeclaration>.Empty;
    }

    /// <summary>
    /// Signal handler node.
    /// </summary>
    public sealed record SignalHandlerNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.SignalHandler;

        /// <summary>
        /// Gets handler name.
        /// </summary>
        public required string HandlerName { get; init; }

        /// <summary>
        /// Gets syntax form for this handler.
        /// </summary>
        public required SignalHandlerForm Form { get; init; }

        /// <summary>
        /// Gets handler JavaScript code.
        /// </summary>
        public required string Code { get; init; }

        /// <summary>
        /// Gets parameters for arrow form handlers.
        /// </summary>
        public ImmutableArray<string>? Parameters { get; init; }
    }

    /// <summary>
    /// Function declaration node.
    /// </summary>
    public sealed record FunctionDeclarationNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.FunctionDeclaration;

        /// <summary>
        /// Gets function name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets function parameters.
        /// </summary>
        public ImmutableArray<ParameterDeclaration> Parameters { get; init; } = ImmutableArray<ParameterDeclaration>.Empty;

        /// <summary>
        /// Gets optional return type.
        /// </summary>
        public string? ReturnType { get; init; }

        /// <summary>
        /// Gets JavaScript function body.
        /// </summary>
        public required string Body { get; init; }
    }

    /// <summary>
    /// Enum declaration node.
    /// </summary>
    public sealed record EnumDeclarationNode : AstNode
    {
        /// <inheritdoc/>
        public override NodeKind Kind => NodeKind.EnumDeclaration;

        /// <summary>
        /// Gets enum name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets enum members in source order.
        /// </summary>
        public required ImmutableArray<EnumMember> Members { get; init; }
    }

    /// <summary>
    /// Function or signal parameter declaration.
    /// </summary>
    /// <param name="Name">Parameter name.</param>
    /// <param name="TypeName">Parameter type name.</param>
    public sealed record ParameterDeclaration(string Name, string TypeName);

    /// <summary>
    /// Enum member declaration.
    /// </summary>
    /// <param name="Name">Member name.</param>
    /// <param name="Value">Optional explicit member value.</param>
    public sealed record EnumMember(string Name, int? Value);

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
