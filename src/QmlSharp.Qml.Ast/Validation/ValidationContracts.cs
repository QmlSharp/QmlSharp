using System.Collections.Immutable;

#pragma warning disable MA0048

namespace QmlSharp.Qml.Ast.Validation
{
    /// <summary>
    /// Diagnostic severity levels.
    /// </summary>
    public enum DiagnosticSeverity
    {
        Error,
        Warning,
        Info,
    }

    /// <summary>
    /// Diagnostic codes for structural and semantic AST validation.
    /// </summary>
    public enum DiagnosticCode
    {
        E001_DuplicateId,
        E002_InvalidIdFormat,
        E003_DuplicatePropertyName,
        E004_DuplicateSignalName,
        E005_InvalidHandlerNameFormat,
        E006_ConflictingPropertyModifiers,
        E007_InvalidImport,
        E008_DuplicateEnumName,
        E009_InvalidInlineComponentName,
        E010_ExcessiveNestingDepth,

        E100_UnknownType,
        E101_UnknownProperty,
        E102_UnknownSignal,
        E103_UnknownAttachedType,
        E104_RequiredPropertyNotSet,
        E105_ReadonlyPropertyBound,
        E106_InvalidEnumReference,
        E107_UnknownModule,

        W001_UnusedImport,
    }

    /// <summary>
    /// Validation diagnostic contract.
    /// </summary>
    public sealed record AstDiagnostic
    {
        /// <summary>
        /// Gets the diagnostic code.
        /// </summary>
        public required DiagnosticCode Code { get; init; }

        /// <summary>
        /// Gets the human-readable message.
        /// </summary>
        public required string Message { get; init; }

        /// <summary>
        /// Gets the diagnostic severity.
        /// </summary>
        public required DiagnosticSeverity Severity { get; init; }

        /// <summary>
        /// Gets the source span where the issue was found.
        /// </summary>
        public SourceSpan? Span { get; init; }

        /// <summary>
        /// Gets the node associated with this diagnostic.
        /// </summary>
        public AstNode? Node { get; init; }
    }

    /// <summary>
    /// Lightweight type-checking abstraction for semantic validation.
    /// </summary>
    public interface ITypeChecker
    {
        bool HasType(string typeName);

        bool HasProperty(string typeName, string propertyName);

        bool HasSignal(string typeName, string signalName);

        bool IsAttachedType(string typeName);

        bool IsPropertyRequired(string typeName, string propertyName);

        bool IsPropertyReadonly(string typeName, string propertyName);

        bool HasEnumMember(string typeName, string memberName);

        bool HasModule(string moduleUri);
    }

    /// <summary>
    /// AST validation contract.
    /// </summary>
    public interface IQmlAstValidator
    {
        /// <summary>
        /// Validates structural correctness without external type data.
        /// </summary>
        /// <param name="document">Document to validate.</param>
        /// <returns>Collected structural diagnostics.</returns>
        ImmutableArray<AstDiagnostic> ValidateStructure(QmlDocument document);

        /// <summary>
        /// Validates semantic correctness using an external type checker.
        /// </summary>
        /// <param name="document">Document to validate.</param>
        /// <param name="typeChecker">Type checker implementation.</param>
        /// <returns>Collected semantic diagnostics.</returns>
        ImmutableArray<AstDiagnostic> ValidateSemantic(QmlDocument document, ITypeChecker typeChecker);
    }
}

#pragma warning restore MA0048
