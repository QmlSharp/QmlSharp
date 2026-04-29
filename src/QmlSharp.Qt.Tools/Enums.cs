#pragma warning disable MA0048

namespace QmlSharp.Qt.Tools
{
    /// <summary>Diagnostic severity levels.</summary>
    public enum DiagnosticSeverity
    {
        /// <summary>Informational message.</summary>
        Info,

        /// <summary>Warning.</summary>
        Warning,

        /// <summary>Error.</summary>
        Error,

        /// <summary>Hint.</summary>
        Hint,

        /// <summary>Disabled rule.</summary>
        Disabled,
    }

    /// <summary>Quality gate levels.</summary>
    public enum QualityGateLevel
    {
        /// <summary>Syntax check only.</summary>
        Syntax = 0,

        /// <summary>Syntax and lint checks.</summary>
        Lint = 1,

        /// <summary>Syntax, lint, and compile checks.</summary>
        Compile = 2,

        /// <summary>Syntax, lint, compile, and runtime smoke checks.</summary>
        Full = 3,
    }

    /// <summary>QML application type for the qml runner.</summary>
    public enum QmlAppType
    {
        /// <summary>Auto-detect the application type.</summary>
        Auto,

        /// <summary>Window-based QML application.</summary>
        Window,

        /// <summary>Item-based QML application.</summary>
        Item,
    }

    /// <summary>All qmllint warning categories from Qt 6.11.</summary>
    public enum QmlLintCategory
    {
        /// <summary>Internal compiler warnings.</summary>
        Compiler,

        /// <summary>Qt Quick Controls sanity checks.</summary>
        ControlsSanity,

        /// <summary>Deferred property binding issues.</summary>
        Deferred,

        /// <summary>Deprecated QML type or property usage.</summary>
        Deprecated,

        /// <summary>Duplicate property binding.</summary>
        DuplicatePropertyBinding,

        /// <summary>Signal handler naming enforcement.</summary>
        EnforceSignalHandlers,

        /// <summary>Import failure.</summary>
        ImportFailure,

        /// <summary>Inheritance cycle.</summary>
        InheritanceCycle,

        /// <summary>Lint plugin warning.</summary>
        LintPluginWarning,

        /// <summary>Multiline string issue.</summary>
        Multiline,

        /// <summary>Prefixed import type usage.</summary>
        PrefixedImportType,

        /// <summary>Property alias cycle.</summary>
        PropertyAliasCycles,

        /// <summary>Read-only property write.</summary>
        ReadOnlyProperty,

        /// <summary>Required property not set.</summary>
        RequiredProperty,

        /// <summary>Restricted type usage.</summary>
        RestrictedType,

        /// <summary>Signal handler parameter issue.</summary>
        SignalHandlerParameters,

        /// <summary>QML syntax error.</summary>
        Syntax,

        /// <summary>Top-level component issue.</summary>
        TopLevelComponent,

        /// <summary>Uncreatable type instantiation.</summary>
        Uncreateable,

        /// <summary>Unexpected type.</summary>
        UnexpectedType,

        /// <summary>Unqualified property or id access.</summary>
        UnqualifiedAccess,

        /// <summary>Unresolved alias.</summary>
        UnresolvedAlias,

        /// <summary>Unresolved type.</summary>
        UnresolvedType,

        /// <summary>Unused import.</summary>
        UnusedImports,

        /// <summary>with statement usage.</summary>
        WithStatement,

        /// <summary>Singleton access issue.</summary>
        AccessSingleton,

        /// <summary>Attached property reuse issue.</summary>
        AttachedPropertyReuse,

        /// <summary>Case-sensitive type issue.</summary>
        CaseSensitiveType,

        /// <summary>Duplicated name.</summary>
        DuplicatedName,

        /// <summary>Extra parenthesis.</summary>
        ExtraParenthesis,

        /// <summary>Forbidden type usage.</summary>
        ForbiddenType,

        /// <summary>Missing enum entry.</summary>
        MissingEnumEntry,

        /// <summary>Missing property.</summary>
        MissingProperty,

        /// <summary>Missing type.</summary>
        MissingType,

        /// <summary>Array assigned to non-list property.</summary>
        NonListProperty,

        /// <summary>Value used where an object was expected.</summary>
        NotAnObject,

        /// <summary>Overridden signal.</summary>
        OverriddenSignal,

        /// <summary>Invalid property assignment.</summary>
        PropertyAssignment,

        /// <summary>Maximum recursion depth exceeded.</summary>
        RecursionDepthError,

        /// <summary>Property storage optimization issue.</summary>
        StoreProperty,

        /// <summary>Type annotation issue.</summary>
        TypeAnnotation,

        /// <summary>Default value for typed properties.</summary>
        TypePropertiesDefault,

        /// <summary>Unnecessary dot notation.</summary>
        UnnecessaryDot,

        /// <summary>Unused expression value.</summary>
        UnusedValue,

        /// <summary>Variable used before declaration.</summary>
        VarUsedBeforeDeclaration,

        /// <summary>Qt Design Studio exactly-one-top-level-item rule.</summary>
        QdsExactlyOneTopLevelItem,

        /// <summary>Qt Design Studio no-inline-components rule.</summary>
        QdsNoInlineComponents,

        /// <summary>Qt Design Studio no-list-property rule.</summary>
        QdsNoListProperty,

        /// <summary>Qt Design Studio no-property-access-from-other-file rule.</summary>
        QdsNoPropertyAccessFromOtherFile,

        /// <summary>Qt Design Studio no-reference-outside rule.</summary>
        QdsNoReferenceOutside,

        /// <summary>Qt Design Studio no-unsupported-type rule.</summary>
        QdsNoUnsupportedType,

        /// <summary>Invalid anchor combinations.</summary>
        AnchorCombinations,

        /// <summary>Layout children issue.</summary>
        LayoutChildren,

        /// <summary>Grid item missing column specification.</summary>
        MissingGridItemColumns,

        /// <summary>Grid item missing row specification.</summary>
        MissingGridItemRows,

        /// <summary>Negative bitwise shift amount.</summary>
        NegativeShiftAmount,

        /// <summary>Overlapping anchors.</summary>
        OverlappingAnchors,

        /// <summary>PropertyChanges usage issue.</summary>
        PropertyChanges,

        /// <summary>Bitwise shift overflow.</summary>
        ShiftOverflow,
    }
}

#pragma warning restore MA0048
