using System.Collections.Immutable;

#pragma warning disable MA0048
#pragma warning disable MA0049

namespace QmlSharp.Qml.Ast.Values
{
    /// <summary>
    /// Numeric literal binding value.
    /// </summary>
    /// <param name="Value">Numeric value.</param>
    public sealed record NumberLiteral(double Value) : BindingValue
    {
        /// <inheritdoc/>
        public override BindingValueKind Kind => BindingValueKind.NumberLiteral;
    }

    /// <summary>
    /// String literal binding value.
    /// </summary>
    /// <param name="Value">Raw string value without quotes.</param>
    public sealed record StringLiteral(string Value) : BindingValue
    {
        /// <inheritdoc/>
        public override BindingValueKind Kind => BindingValueKind.StringLiteral;
    }

    /// <summary>
    /// Boolean literal binding value.
    /// </summary>
    /// <param name="Value">Boolean value.</param>
    public sealed record BooleanLiteral(bool Value) : BindingValue
    {
        /// <inheritdoc/>
        public override BindingValueKind Kind => BindingValueKind.BooleanLiteral;
    }

    /// <summary>
    /// Null literal binding value.
    /// </summary>
    public sealed record NullLiteral : BindingValue
    {
        /// <inheritdoc/>
        public override BindingValueKind Kind => BindingValueKind.NullLiteral;

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static NullLiteral Instance { get; } = new();
    }

    /// <summary>
    /// Enum reference binding value.
    /// </summary>
    /// <param name="TypeName">Enum type name.</param>
    /// <param name="MemberName">Enum member name.</param>
    public sealed record EnumReference(string TypeName, string MemberName) : BindingValue
    {
        /// <inheritdoc/>
        public override BindingValueKind Kind => BindingValueKind.EnumReference;
    }

    /// <summary>
    /// JavaScript expression binding value.
    /// </summary>
    /// <param name="Code">Raw expression text.</param>
    public sealed record ScriptExpression(string Code) : BindingValue
    {
        /// <inheritdoc/>
        public override BindingValueKind Kind => BindingValueKind.ScriptExpression;
    }

    /// <summary>
    /// JavaScript block binding value.
    /// </summary>
    /// <param name="Code">Raw block text.</param>
    public sealed record ScriptBlock(string Code) : BindingValue
    {
        /// <inheritdoc/>
        public override BindingValueKind Kind => BindingValueKind.ScriptBlock;
    }

    /// <summary>
    /// Inline object binding value.
    /// </summary>
    /// <param name="Object">Object definition.</param>
    public sealed record ObjectValue(ObjectDefinitionNode Object) : BindingValue
    {
        /// <inheritdoc/>
        public override BindingValueKind Kind => BindingValueKind.ObjectValue;
    }

    /// <summary>
    /// Array binding value.
    /// </summary>
    /// <param name="Elements">Array elements.</param>
    public sealed record ArrayValue(ImmutableArray<BindingValue> Elements) : BindingValue
    {
        /// <inheritdoc/>
        public override BindingValueKind Kind => BindingValueKind.ArrayValue;
    }

    /// <summary>
    /// Contract placeholder for value factory API introduced in later steps.
    /// </summary>
    public static class Values
    {
    }
}

#pragma warning restore MA0049
#pragma warning restore MA0048
