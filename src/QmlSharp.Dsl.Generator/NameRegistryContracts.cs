#pragma warning disable MA0048

namespace QmlSharp.Dsl.Generator
{
    /// <summary>Tracks generated C# names and resolves naming conflicts.</summary>
    public interface INameRegistry
    {
        string RegisterTypeName(string qmlName, string moduleUri);

        string RegisterPropertyName(string propertyName, string ownerType);

        string RegisterMethodName(string methodName, string ownerType);

        string RegisterEnumName(string enumName, string ownerType);

        bool IsReservedWord(string name);

        string ToSafeIdentifier(string name);
    }
}

#pragma warning restore MA0048
