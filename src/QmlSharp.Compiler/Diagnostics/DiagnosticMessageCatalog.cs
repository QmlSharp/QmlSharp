namespace QmlSharp.Compiler
{
    /// <summary>
    /// Provides message templates for QmlSharp compiler diagnostics.
    /// </summary>
    public static class DiagnosticMessageCatalog
    {
        private static readonly ImmutableDictionary<string, string> Templates =
            ImmutableDictionary.CreateRange(
                StringComparer.Ordinal,
                new KeyValuePair<string, string>[]
                {
                    new(DiagnosticCodes.InvalidStateAttribute, "State attribute usage is invalid."),
                    new(DiagnosticCodes.InvalidCommandAttribute, "Command attribute usage is invalid."),
                    new(DiagnosticCodes.InvalidEffectAttribute, "Effect attribute usage is invalid."),
                    new(DiagnosticCodes.ViewModelNotFound, "Referenced ViewModel type could not be found."),
                    new(DiagnosticCodes.DuplicateStateName, "Duplicate state property name was found."),
                    new(DiagnosticCodes.DuplicateCommandName, "Duplicate command name was found."),
                    new(DiagnosticCodes.DuplicateEffectName, "Duplicate effect name was found."),
                    new(DiagnosticCodes.UnsupportedStateType, "State property type is not supported by the schema contract."),
                    new(DiagnosticCodes.CommandMustBeVoid, "Command methods must return void or Task."),
                    new(DiagnosticCodes.EffectMustBeEvent, "Effect attributes can only be applied to events."),
                    new(DiagnosticCodes.ViewMissingBuildMethod, "View type is missing a Build method."),
                    new(DiagnosticCodes.ViewModelMissingAttribute, "The ViewModel bound to this View is missing the ViewModel attribute."),
                    new(DiagnosticCodes.StaticMemberNotAllowed, "Static members cannot be exposed through ViewModel compiler attributes."),
                    new(DiagnosticCodes.MultipleViewModelBindings, "A View can bind to only one ViewModel in the current V2 baseline."),
                    new(DiagnosticCodes.UnknownQmlType, "QML type could not be resolved."),
                    new(DiagnosticCodes.InvalidPropertyValue, "DSL property value is invalid."),
                    new(DiagnosticCodes.UnresolvedTypeReference, "Type reference could not be resolved."),
                    new(DiagnosticCodes.InvalidCallChain, "DSL call chain is invalid."),
                    new(DiagnosticCodes.UnsupportedDslPattern, "DSL pattern is not supported by the compiler."),
                    new(DiagnosticCodes.BindExpressionEmpty, "Binding expression cannot be empty."),
                    new(DiagnosticCodes.InvalidChildType, "Child object type is invalid for the target parent."),
                    new(DiagnosticCodes.PropertyTypeMismatch, "DSL property value does not match the expected QML property type."),
                    new(DiagnosticCodes.UnknownSignal, "Signal name could not be resolved."),
                    new(DiagnosticCodes.UnknownAttachedType, "Attached type name could not be resolved."),
                    new(DiagnosticCodes.ViewModelInstanceConflict, "Existing ViewModel instance conflicts with generated V2 injection."),
                    new(DiagnosticCodes.ImportConflict, "QML import conflicts with generated imports."),
                    new(DiagnosticCodes.BindingTargetNotFound, "State binding target was not found in the ViewModel schema."),
                    new(DiagnosticCodes.CommandTargetNotFound, "Command target was not found in the ViewModel schema."),
                    new(DiagnosticCodes.EffectHandlerConflict, "Effect handler conflicts with generated effect routing."),
                    new(DiagnosticCodes.SlotKeyCollision, "Compiler slot key collision was detected."),
                    new(DiagnosticCodes.EmitFailed, "QML emission failed."),
                    new(DiagnosticCodes.OutputWriteFailed, "Writing compiler output failed."),
                    new(DiagnosticCodes.SchemaSerializationFailed, "Schema serialization failed."),
                    new(DiagnosticCodes.SourceMapWriteFailed, "Writing source map output failed."),
                    new(DiagnosticCodes.InternalError, "An internal compiler error occurred."),
                    new(DiagnosticCodes.RoslynCompilationFailed, "Roslyn compilation failed."),
                    new(DiagnosticCodes.ProjectLoadFailed, "Project loading failed."),
                });

        /// <summary>Gets every diagnostic code that has a message template.</summary>
        public static ImmutableArray<string> AllCodes { get; } = Templates.Keys.Order(StringComparer.Ordinal).ToImmutableArray();

        /// <summary>
        /// Gets the message template for a diagnostic code.
        /// </summary>
        /// <param name="code">The diagnostic code.</param>
        /// <returns>The message template.</returns>
        public static string GetTemplate(string code)
        {
            if (Templates.TryGetValue(code, out string? template))
            {
                return template;
            }

            return "Unknown compiler diagnostic.";
        }

        /// <summary>
        /// Formats a diagnostic message from a code and optional additional context.
        /// </summary>
        /// <param name="code">The diagnostic code.</param>
        /// <param name="details">Optional context appended to the template.</param>
        /// <returns>The formatted diagnostic message.</returns>
        public static string FormatMessage(string code, string? details = null)
        {
            string template = GetTemplate(code);
            if (string.IsNullOrWhiteSpace(details))
            {
                return template;
            }

            return $"{template} {details}";
        }
    }
}
