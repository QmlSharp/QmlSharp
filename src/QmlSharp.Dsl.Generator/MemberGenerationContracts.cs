#pragma warning disable MA0048

using QmlSharp.Registry;

namespace QmlSharp.Dsl.Generator
{
    /// <summary>Generates C# fluent setter metadata for QML properties.</summary>
    public interface IPropGenerator
    {
        GeneratedProperty Generate(ResolvedProperty property, QmlType ownerType, GenerationContext context);

        ImmutableArray<GeneratedProperty> GenerateAll(
            ResolvedType type,
            GenerationContext context);

        ImmutableArray<GroupedPropertyInfo> DetectGroupedProperties(
            ResolvedType type);
    }

    /// <summary>Generates C# signal handler metadata for QML signals.</summary>
    public interface ISignalGenerator
    {
        GeneratedSignal Generate(ResolvedSignal signal, GenerationContext context);

        ImmutableArray<GeneratedSignal> GenerateAll(
            ImmutableArray<ResolvedSignal> signals,
            GenerationContext context);
    }

    /// <summary>Generates C# method wrapper metadata for QML methods.</summary>
    public interface IMethodGenerator
    {
        GeneratedMethod Generate(ResolvedMethod method, GenerationContext context);

        ImmutableArray<GeneratedMethod> GenerateAll(
            ImmutableArray<ResolvedMethod> methods,
            GenerationContext context);
    }

    /// <summary>Generates C# enum metadata from QML enum definitions.</summary>
    public interface IEnumGenerator
    {
        GeneratedEnum Generate(QmlEnum enumDef, QmlType ownerType, GenerationContext context);

        ImmutableArray<GeneratedEnum> GenerateAll(
            ImmutableArray<QmlEnum> enums,
            QmlType ownerType,
            GenerationContext context);
    }

    /// <summary>Generates callback-builder metadata for QML attached properties.</summary>
    public interface IAttachedPropGenerator
    {
        GeneratedAttachedType Generate(QmlType attachedType, GenerationContext context);

        IReadOnlyList<QmlType> GetAllAttachedTypes(QmlSharp.Registry.Querying.IRegistryQuery registry);
    }

    /// <summary>Analyzes default properties for child/children builder methods.</summary>
    public interface IDefaultPropertyHandler
    {
        DefaultPropertyInfo? Analyze(ResolvedType type);

        ImmutableArray<string> GenerateMethods(DefaultPropertyInfo info, GenerationContext context);
    }

    /// <summary>Generated code metadata for one QML property.</summary>
    public sealed record GeneratedProperty(
        string Name,
        string SetterSignature,
        string? BindSignature,
        string XmlDoc,
        QmlType DeclaredBy,
        bool IsReadOnly,
        bool IsRequired,
        string CSharpType);

    /// <summary>Metadata for a grouped property builder surface.</summary>
    public sealed record GroupedPropertyInfo(
        string GroupName,
        ImmutableArray<ResolvedProperty> SubProperties,
        string BuilderSignature);

    /// <summary>Generated code metadata for one QML signal handler.</summary>
    public sealed record GeneratedSignal(
        string SignalName,
        string HandlerName,
        string HandlerSignature,
        string XmlDoc,
        QmlType DeclaredBy,
        ImmutableArray<GeneratedParameter> Parameters);

    /// <summary>A signal, method, command, or effect parameter mapped to a C# type.</summary>
    public sealed record GeneratedParameter(
        string Name,
        string CSharpType,
        string QmlType);

    /// <summary>Generated code metadata for one QML method.</summary>
    public sealed record GeneratedMethod(
        string Name,
        string Signature,
        ImmutableArray<GeneratedParameter> Parameters,
        string ReturnType,
        string XmlDoc,
        QmlType DeclaredBy,
        bool IsConstructor);

    /// <summary>Generated code metadata for one C# enum.</summary>
    public sealed record GeneratedEnum(
        string Name,
        string? Alias,
        bool IsFlag,
        bool IsScoped,
        ImmutableArray<GeneratedEnumMember> Members,
        string Code,
        QmlType OwnerType);

    /// <summary>A single generated enum member.</summary>
    public sealed record GeneratedEnumMember(
        string Name,
        int? Value);

    /// <summary>Generated code metadata for a QML attached type.</summary>
    public sealed record GeneratedAttachedType(
        string TypeName,
        string MethodName,
        ResolvedType ResolvedType,
        ImmutableArray<GeneratedProperty> Properties,
        ImmutableArray<GeneratedSignal> Signals,
        string BuilderInterfaceName);

    /// <summary>Metadata for a QML type default property.</summary>
    public sealed record DefaultPropertyInfo(
        string PropertyName,
        string ElementType,
        bool IsList,
        bool GenerateChildMethod,
        bool GenerateChildrenMethod);
}

#pragma warning restore MA0048
