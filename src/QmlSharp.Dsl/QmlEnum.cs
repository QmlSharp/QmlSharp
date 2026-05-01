using QmlSharp.Qml.Ast;

#pragma warning disable MA0048

namespace QmlSharp.Dsl
{
    /// <summary>
    /// Runtime token that preserves the QML owner and member name for enum-valued bindings.
    /// </summary>
    /// <param name="OwnerTypeName">The QML type that owns the enum.</param>
    /// <param name="EnumName">The enum name in QML metadata.</param>
    /// <param name="MemberName">The QML enum member name.</param>
    public readonly record struct QmlEnumToken(string OwnerTypeName, string EnumName, string MemberName)
    {
        /// <summary>Converts this token to the AST enum reference representation.</summary>
        /// <returns>An enum reference binding value.</returns>
        public EnumReference ToBindingValue()
        {
            return Values.Enum(OwnerTypeName, MemberName);
        }
    }

    /// <summary>
    /// Helper methods used by generated enum-valued DSL methods.
    /// </summary>
    public static class QmlEnum
    {
        /// <summary>Creates a QML enum token.</summary>
        /// <param name="ownerTypeName">The QML type that owns the enum.</param>
        /// <param name="enumName">The QML enum name.</param>
        /// <param name="memberName">The QML member name.</param>
        /// <returns>A token that can be lowered to an AST enum reference.</returns>
        public static QmlEnumToken Create(string ownerTypeName, string enumName, string memberName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerTypeName);
            ArgumentException.ThrowIfNullOrWhiteSpace(enumName);
            ArgumentException.ThrowIfNullOrWhiteSpace(memberName);
            return new QmlEnumToken(ownerTypeName, enumName, memberName);
        }

        /// <summary>Creates a QML enum token from a generated C# enum value.</summary>
        /// <typeparam name="TEnum">Generated enum type.</typeparam>
        /// <param name="ownerTypeName">The QML type that owns the enum.</param>
        /// <param name="enumName">The QML enum name.</param>
        /// <param name="value">Generated enum value.</param>
        /// <returns>A token that can be lowered to an AST enum reference.</returns>
        public static QmlEnumToken From<TEnum>(string ownerTypeName, string enumName, TEnum value)
            where TEnum : struct, Enum
        {
            return Create(ownerTypeName, enumName, value.ToString());
        }
    }
}

#pragma warning restore MA0048
