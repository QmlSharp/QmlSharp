#pragma warning disable MA0048

using Microsoft.CodeAnalysis.CSharp.Syntax;
using QmlSharp.Qml.Ast;
using QmlSharp.Registry.Querying;

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Transforms C# fluent DSL call chains into QML AST nodes.
    /// </summary>
    public interface IDslTransformer
    {
        /// <summary>Transforms a discovered View into a QML AST document.</summary>
        DslTransformResult Transform(DiscoveredView view, ProjectContext context, IRegistryQuery registry);

        /// <summary>Extracts the Roslyn-specific invocation tree into compiler IR.</summary>
        DslCallNode ExtractCallTree(InvocationExpressionSyntax invocation, Microsoft.CodeAnalysis.SemanticModel semanticModel);

        /// <summary>Converts compiler DSL IR to a QML AST object node.</summary>
        ObjectDefinitionNode ToAstNode(DslCallNode callNode, IRegistryQuery registry);
    }

    /// <summary>Result of transforming one View DSL body.</summary>
    public sealed record DslTransformResult(
        QmlDocument Document,
        DslCallNode RootCall,
        ImmutableArray<SourceMapping> SourceMappings,
        ImmutableArray<CompilerDiagnostic> Diagnostics);

    /// <summary>Intermediate representation for one fluent DSL object call.</summary>
    public sealed record DslCallNode(
        string TypeName,
        ImmutableArray<DslPropertyCall> Properties,
        ImmutableArray<DslBindingCall> Bindings,
        ImmutableArray<DslSignalHandlerCall> SignalHandlers,
        ImmutableArray<DslGroupedCall> GroupedProperties,
        ImmutableArray<DslAttachedCall> AttachedProperties,
        ImmutableArray<DslCallNode> Children,
        SourceLocation? SourceLocation = null);

    /// <summary>A literal property setter call in the DSL IR.</summary>
    public sealed record DslPropertyCall(string Name, object? Value, SourceLocation? SourceLocation = null);

    /// <summary>A binding expression call in the DSL IR.</summary>
    public sealed record DslBindingCall(string Name, string Expression, SourceLocation? SourceLocation = null);

    /// <summary>A signal handler call in the DSL IR.</summary>
    public sealed record DslSignalHandlerCall(string Name, string Body, SourceLocation? SourceLocation = null);

    /// <summary>A grouped property call in the DSL IR.</summary>
    public sealed record DslGroupedCall(string Name, ImmutableArray<DslPropertyCall> Properties, SourceLocation? SourceLocation = null);

    /// <summary>An attached property call in the DSL IR.</summary>
    public sealed record DslAttachedCall(string TypeName, ImmutableArray<DslPropertyCall> Properties, SourceLocation? SourceLocation = null);

    /// <summary>Allocates deterministic IDs and compiler slot keys.</summary>
    public interface IIdAllocator
    {
        /// <summary>Allocates a deterministic state member ID.</summary>
        int AllocateMemberId(string className, string memberName);

        /// <summary>Allocates a deterministic command ID.</summary>
        int AllocateCommandId(string className, string commandName);

        /// <summary>Allocates a deterministic effect ID.</summary>
        int AllocateEffectId(string className, string effectName);

        /// <summary>Generates a deterministic compiler slot key.</summary>
        string GenerateSlotKey(string viewClassName, int slotIndex);

        /// <summary>Computes the underlying deterministic FNV-1a hash.</summary>
        int ComputeHash(string key);
    }

    /// <summary>Resolves C# DSL namespaces to QML imports.</summary>
    public interface IImportResolver
    {
        /// <summary>Resolves one discovered import, or null when it is not a DSL import.</summary>
        ResolvedImport? ResolveSingle(DiscoveredImport import, CompilerOptions options);

        /// <summary>Resolves and deduplicates discovered imports.</summary>
        ImmutableArray<ResolvedImport> Resolve(ImmutableArray<DiscoveredImport> imports, CompilerOptions options);

        /// <summary>Registers a custom C# namespace to QML module mapping.</summary>
        void RegisterMapping(string csharpNamespace, string qmlModuleUri, QmlVersion? version = null, string? alias = null);
    }

    /// <summary>A resolved QML import.</summary>
    public sealed record ResolvedImport(string CSharpNamespace, string QmlModuleUri, QmlVersion Version, string? Alias);

    /// <summary>Post-processes AST output into V2 runtime-ready QML AST.</summary>
    public interface IPostProcessor
    {
        /// <summary>Processes one View AST with schema and imports.</summary>
        PostProcessResult Process(
            QmlDocument document,
            DiscoveredView view,
            ViewModelSchema schema,
            ImmutableArray<ResolvedImport> imports,
            CompilerOptions options);
    }

    /// <summary>Result of V2 post-processing.</summary>
    public sealed record PostProcessResult(
        QmlDocument Document,
        ImmutableArray<InjectedNode> InjectedNodes,
        ImmutableArray<CompilerDiagnostic> Diagnostics);

    /// <summary>Metadata for a node injected by the compiler post-processor.</summary>
    public sealed record InjectedNode(string Kind, string Description, SourceLocation? SourceLocation = null);
}

#pragma warning restore MA0048
