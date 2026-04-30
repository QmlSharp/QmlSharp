#pragma warning disable MA0048

using QmlSharp.Registry;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>Maps QML type names to C# type names.</summary>
    public interface ITypeMapper
    {
        string MapToCSharp(string qmlType);

        TypeMapping? GetMapping(string qmlType);

        string MapListType(string elementType);

        string GetSetterType(QmlProperty property);

        string GetParameterType(QmlParameter parameter);

        string GetReturnType(QmlMethod method);

        void RegisterCustomMapping(TypeMapping mapping);

        IReadOnlyDictionary<string, TypeMapping> GetAllMappings();
    }

    /// <summary>A single QML-to-C# type mapping entry.</summary>
    public sealed record TypeMapping(
        string QmlType,
        string CSharpType,
        bool IsValueType,
        bool IsNullable,
        string? DefaultValue,
        string? RequiresImport);
}

#pragma warning restore MA0048
