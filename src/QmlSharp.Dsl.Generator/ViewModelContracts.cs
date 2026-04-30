#pragma warning disable MA0048

namespace QmlSharp.Dsl.Generator
{
    /// <summary>Analyzes ViewModel schemas and generates binding helper metadata.</summary>
    public interface IViewModelIntegration
    {
        ViewModelBindingInfo AnalyzeSchema(string schemaJson);

        string GenerateProxyType(ViewModelBindingInfo info);

        string GenerateBindingHelpers();
    }

    /// <summary>Binding information extracted from a ViewModel schema.</summary>
    public sealed record ViewModelBindingInfo(
        string ClassName,
        ImmutableArray<ViewModelStateInfo> States,
        ImmutableArray<ViewModelCommandInfo> Commands,
        ImmutableArray<ViewModelEffectInfo> Effects);

    /// <summary>A single observable state property from a ViewModel.</summary>
    public sealed record ViewModelStateInfo(
        string FieldName,
        string QmlPropertyName,
        string CSharpType,
        string QmlType,
        bool IsReadOnly);

    /// <summary>A command method from a ViewModel.</summary>
    public sealed record ViewModelCommandInfo(
        string MethodName,
        string QmlMethodName,
        ImmutableArray<GeneratedParameter> Parameters,
        bool IsAsync);

    /// <summary>An effect binding from a ViewModel field to a QML signal.</summary>
    public sealed record ViewModelEffectInfo(
        string FieldName,
        string QmlSignalName,
        ImmutableArray<GeneratedParameter> Parameters);
}

#pragma warning restore MA0048
