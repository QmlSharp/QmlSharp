#pragma warning disable MA0048

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Compiler diagnostic severity.
    /// </summary>
    public enum DiagnosticSeverity
    {
        /// <summary>Informational diagnostic.</summary>
        Info,

        /// <summary>Warning diagnostic.</summary>
        Warning,

        /// <summary>Error diagnostic for user-correctable invalid input.</summary>
        Error,

        /// <summary>Fatal compiler diagnostic that aborts the pipeline.</summary>
        Fatal,
    }

    /// <summary>
    /// Source location with 1-based public line and column positions.
    /// </summary>
    public sealed record SourceLocation(string? FilePath = null, int? Line = null, int? Column = null);

    /// <summary>
    /// Source-to-output mapping captured during compiler transforms.
    /// </summary>
    public sealed record SourceMapping(SourceLocation Source, SourceLocation Output, string? Symbol = null, string? NodeKind = null);

    /// <summary>
    /// Structured compiler diagnostic.
    /// </summary>
    public sealed record CompilerDiagnostic(
        string Code,
        DiagnosticSeverity Severity,
        string Message,
        SourceLocation? Location = null,
        string? Phase = null);

    /// <summary>
    /// Accumulates and formats compiler diagnostics.
    /// </summary>
    public interface IDiagnosticReporter
    {
        /// <summary>Reports one diagnostic.</summary>
        void Report(CompilerDiagnostic diagnostic);

        /// <summary>Gets all diagnostics in deterministic order.</summary>
        ImmutableArray<CompilerDiagnostic> GetDiagnostics();

        /// <summary>Gets diagnostics matching a severity.</summary>
        ImmutableArray<CompilerDiagnostic> GetDiagnostics(DiagnosticSeverity severity);

        /// <summary>Gets a value indicating whether error or fatal diagnostics exist.</summary>
        bool HasErrors { get; }

        /// <summary>Formats a diagnostic for human-readable output.</summary>
        string Format(CompilerDiagnostic diagnostic);

        /// <summary>Clears all diagnostics.</summary>
        void Clear();
    }

    /// <summary>
    /// Stable QmlSharp compiler diagnostic code constants.
    /// </summary>
    public static class DiagnosticCodes
    {
        /// <summary>No ViewModel attribute was found where one was required.</summary>
        public const string MissingViewModelAttribute = "QMLSHARP-A001";

        /// <summary>A state property is invalid.</summary>
        public const string InvalidStateProperty = "QMLSHARP-A002";

        /// <summary>A command method is invalid.</summary>
        public const string InvalidCommandMethod = "QMLSHARP-A003";

        /// <summary>An effect event is invalid.</summary>
        public const string InvalidEffectEvent = "QMLSHARP-A004";

        /// <summary>A ViewModel contains duplicate state names.</summary>
        public const string DuplicateStateName = "QMLSHARP-A005";

        /// <summary>A ViewModel contains duplicate command names.</summary>
        public const string DuplicateCommandName = "QMLSHARP-A006";

        /// <summary>A ViewModel contains duplicate effect names.</summary>
        public const string DuplicateEffectName = "QMLSHARP-A007";

        /// <summary>A state type is unsupported.</summary>
        public const string UnsupportedStateType = "QMLSHARP-A008";

        /// <summary>A command must return void or Task.</summary>
        public const string CommandMustBeVoid = "QMLSHARP-A009";

        /// <summary>An effect marker must be on an event.</summary>
        public const string EffectMustBeEvent = "QMLSHARP-A010";

        /// <summary>A View is missing its Build method.</summary>
        public const string ViewMissingBuildMethod = "QMLSHARP-A011";

        /// <summary>A bound ViewModel is missing its attribute.</summary>
        public const string ViewModelMissingAttribute = "QMLSHARP-A012";

        /// <summary>A static member was marked with a ViewModel compiler attribute.</summary>
        public const string StaticMemberNotAllowed = "QMLSHARP-A013";

        /// <summary>A View declares multiple ViewModel bindings.</summary>
        public const string MultipleViewModelBindings = "QMLSHARP-A014";

        /// <summary>An unknown QML type was referenced.</summary>
        public const string UnknownQmlType = "QMLSHARP-T001";

        /// <summary>A DSL property value is invalid.</summary>
        public const string InvalidPropertyValue = "QMLSHARP-T002";

        /// <summary>A type reference could not be resolved.</summary>
        public const string UnresolvedTypeReference = "QMLSHARP-T003";

        /// <summary>A DSL call chain is invalid.</summary>
        public const string InvalidCallChain = "QMLSHARP-T004";

        /// <summary>A DSL pattern is unsupported.</summary>
        public const string UnsupportedDslPattern = "QMLSHARP-T005";

        /// <summary>A binding expression is empty.</summary>
        public const string BindExpressionEmpty = "QMLSHARP-T006";

        /// <summary>A child type is invalid for its parent.</summary>
        public const string InvalidChildType = "QMLSHARP-T007";

        /// <summary>A property value type does not match the QML property type.</summary>
        public const string PropertyTypeMismatch = "QMLSHARP-T008";

        /// <summary>A signal name is unknown.</summary>
        public const string UnknownSignal = "QMLSHARP-T009";

        /// <summary>An attached type name is unknown.</summary>
        public const string UnknownAttachedType = "QMLSHARP-T010";

        /// <summary>A ViewModel instance conflicts with generated V2 injection.</summary>
        public const string ViewModelInstanceConflict = "QMLSHARP-P001";

        /// <summary>A QML import conflicts with generated imports.</summary>
        public const string ImportConflict = "QMLSHARP-P002";

        /// <summary>A state binding target is missing from the schema.</summary>
        public const string BindingTargetNotFound = "QMLSHARP-P003";

        /// <summary>A command target is missing from the schema.</summary>
        public const string CommandTargetNotFound = "QMLSHARP-P004";

        /// <summary>An effect handler conflicts with generated routing.</summary>
        public const string EffectHandlerConflict = "QMLSHARP-P005";

        /// <summary>A compiler slot key collision was detected.</summary>
        public const string SlotKeyCollision = "QMLSHARP-P006";

        /// <summary>QML emission failed.</summary>
        public const string EmitFailed = "QMLSHARP-C001";

        /// <summary>Writing compiler output failed.</summary>
        public const string OutputWriteFailed = "QMLSHARP-C002";

        /// <summary>Schema serialization failed.</summary>
        public const string SchemaSerializationFailed = "QMLSHARP-C003";

        /// <summary>Writing a source map failed.</summary>
        public const string SourceMapWriteFailed = "QMLSHARP-C004";

        /// <summary>An unexpected compiler internal error occurred.</summary>
        public const string InternalError = "QMLSHARP-G001";

        /// <summary>Roslyn compilation failed.</summary>
        public const string RoslynCompilationFailed = "QMLSHARP-G002";

        /// <summary>Project loading failed.</summary>
        public const string ProjectLoadFailed = "QMLSHARP-G003";
    }
}

#pragma warning restore MA0048
