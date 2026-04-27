using System.Collections.Immutable;

namespace QmlSharp.Qml.Ast.Validation
{
    /// <summary>
    /// Validates QML AST documents.
    /// </summary>
    public sealed class QmlAstValidator : IQmlAstValidator
    {
        private const int MaximumObjectNestingDepth = 20;

        /// <inheritdoc/>
        public ImmutableArray<AstDiagnostic> ValidateStructure(QmlDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            StructuralValidationContext context = new();
            ValidateImportNodes(document, context);
            ValidateObject(document.RootObject, context, objectDepth: 1);

            return context.ToImmutable();
        }

        /// <inheritdoc/>
        public ImmutableArray<AstDiagnostic> ValidateSemantic(QmlDocument document, ITypeChecker typeChecker)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(typeChecker);

            throw new NotSupportedException("Semantic validation is implemented in Step 02.07.");
        }

        private static void ValidateImportNodes(QmlDocument document, StructuralValidationContext context)
        {
            foreach (ImportNode importNode in document.Imports)
            {
                ValidateImport(importNode, context);
            }
        }

        private static void ValidateImport(ImportNode importNode, StructuralValidationContext context)
        {
            bool isValid = importNode.ImportKind switch
            {
                ImportKind.Module => IsNonEmpty(importNode.ModuleUri)
                    && importNode.Path is null
                    && IsValidOptionalQualifier(importNode.Qualifier),
                ImportKind.Directory => IsNonEmpty(importNode.Path)
                    && importNode.ModuleUri is null
                    && importNode.Version is null
                    && IsValidOptionalQualifier(importNode.Qualifier),
                ImportKind.JavaScript => IsNonEmpty(importNode.Path)
                    && IsNonEmpty(importNode.Qualifier)
                    && importNode.ModuleUri is null
                    && importNode.Version is null,
                _ => false,
            };

            if (!isValid)
            {
                context.Add(
                    DiagnosticCode.E007_InvalidImport,
                    "Invalid import. Module imports require a module URI, directory imports require a path, and JavaScript imports require a path and qualifier.",
                    importNode);
            }
        }

        private static void ValidateObject(ObjectDefinitionNode objectNode, StructuralValidationContext context, int objectDepth)
        {
            if (objectDepth > MaximumObjectNestingDepth)
            {
                context.Add(
                    DiagnosticCode.E010_ExcessiveNestingDepth,
                    $"Object nesting depth exceeds the supported maximum of {MaximumObjectNestingDepth}.",
                    objectNode);
            }

            ObjectScope scope = new();
            foreach (AstNode member in objectNode.Members)
            {
                ValidateObjectMember(member, context, scope);
            }

            foreach (AstNode member in objectNode.Members)
            {
                ValidateNestedContent(member, context, objectDepth);
            }
        }

        private static void ValidateObjectMember(AstNode member, StructuralValidationContext context, ObjectScope scope)
        {
            switch (member)
            {
                case IdAssignmentNode idAssignmentNode:
                    ValidateIdAssignment(idAssignmentNode, context);
                    break;

                case PropertyDeclarationNode propertyDeclarationNode:
                    ValidatePropertyDeclaration(propertyDeclarationNode, context, scope);
                    break;

                case PropertyAliasNode propertyAliasNode:
                    scope.ValidatePropertyName(propertyAliasNode.Name, propertyAliasNode, context);
                    break;

                case BindingNode bindingNode:
                    scope.ValidatePropertyName(bindingNode.PropertyName, bindingNode, context);
                    break;

                case ArrayBindingNode arrayBindingNode:
                    scope.ValidatePropertyName(arrayBindingNode.PropertyName, arrayBindingNode, context);
                    break;

                case BehaviorOnNode behaviorOnNode:
                    scope.ValidatePropertyName(behaviorOnNode.PropertyName, behaviorOnNode, context);
                    break;

                case SignalDeclarationNode signalDeclarationNode:
                    scope.ValidateSignalName(signalDeclarationNode.Name, signalDeclarationNode, context);
                    break;

                case SignalHandlerNode signalHandlerNode:
                    ValidateSignalHandler(signalHandlerNode, context);
                    break;

                case EnumDeclarationNode enumDeclarationNode:
                    scope.ValidateEnum(enumDeclarationNode, context);
                    break;

                case InlineComponentNode inlineComponentNode:
                    ValidateInlineComponent(inlineComponentNode, context);
                    break;
            }
        }

        private static void ValidateNestedContent(AstNode member, StructuralValidationContext context, int parentObjectDepth)
        {
            switch (member)
            {
                case ObjectDefinitionNode childObject:
                    ValidateObject(childObject, context, parentObjectDepth + 1);
                    break;

                case InlineComponentNode inlineComponentNode:
                    ValidateObject(inlineComponentNode.Body, context, parentObjectDepth + 1);
                    break;

                case PropertyDeclarationNode propertyDeclarationNode when propertyDeclarationNode.InitialValue is not null:
                    ValidateBindingValue(propertyDeclarationNode.InitialValue, context, parentObjectDepth);
                    break;

                case BindingNode bindingNode:
                    ValidateBindingValue(bindingNode.Value, context, parentObjectDepth);
                    break;

                case GroupedBindingNode groupedBindingNode:
                    foreach (BindingNode bindingNode in groupedBindingNode.Bindings)
                    {
                        ValidateBindingValue(bindingNode.Value, context, parentObjectDepth);
                    }

                    break;

                case AttachedBindingNode attachedBindingNode:
                    foreach (BindingNode bindingNode in attachedBindingNode.Bindings)
                    {
                        ValidateBindingValue(bindingNode.Value, context, parentObjectDepth);
                    }

                    break;

                case ArrayBindingNode arrayBindingNode:
                    foreach (BindingValue element in arrayBindingNode.Elements)
                    {
                        ValidateBindingValue(element, context, parentObjectDepth);
                    }

                    break;

                case BehaviorOnNode behaviorOnNode:
                    ValidateObject(behaviorOnNode.Animation, context, parentObjectDepth + 1);
                    break;
            }
        }

        private static void ValidateBindingValue(BindingValue value, StructuralValidationContext context, int parentObjectDepth)
        {
            switch (value)
            {
                case ObjectValue objectValue:
                    ValidateObject(objectValue.Object, context, parentObjectDepth + 1);
                    break;

                case ArrayValue arrayValue:
                    foreach (BindingValue element in arrayValue.Elements)
                    {
                        ValidateBindingValue(element, context, parentObjectDepth);
                    }

                    break;
            }
        }

        private static void ValidateIdAssignment(IdAssignmentNode idAssignmentNode, StructuralValidationContext context)
        {
            if (!IsValidQmlId(idAssignmentNode.Id))
            {
                context.Add(
                    DiagnosticCode.E002_InvalidIdFormat,
                    $"Invalid id '{idAssignmentNode.Id}'. QML ids must start with a lowercase letter or underscore and contain only letters, digits, or underscores.",
                    idAssignmentNode);
            }

            context.ValidateIdUniqueness(idAssignmentNode);
        }

        private static void ValidatePropertyDeclaration(
            PropertyDeclarationNode propertyDeclarationNode,
            StructuralValidationContext context,
            ObjectScope scope)
        {
            scope.ValidatePropertyName(propertyDeclarationNode.Name, propertyDeclarationNode, context);

            if (propertyDeclarationNode.IsReadonly && propertyDeclarationNode.IsRequired)
            {
                context.Add(
                    DiagnosticCode.E006_ConflictingPropertyModifiers,
                    $"Property '{propertyDeclarationNode.Name}' cannot be both readonly and required.",
                    propertyDeclarationNode);
            }
        }

        private static void ValidateSignalHandler(SignalHandlerNode signalHandlerNode, StructuralValidationContext context)
        {
            if (!IsValidSignalHandlerName(signalHandlerNode.HandlerName))
            {
                context.Add(
                    DiagnosticCode.E005_InvalidHandlerNameFormat,
                    $"Invalid signal handler name '{signalHandlerNode.HandlerName}'. Signal handlers must use the form onSignalName.",
                    signalHandlerNode);
            }
        }

        private static void ValidateInlineComponent(InlineComponentNode inlineComponentNode, StructuralValidationContext context)
        {
            if (!IsValidInlineComponentName(inlineComponentNode.Name))
            {
                context.Add(
                    DiagnosticCode.E009_InvalidInlineComponentName,
                    $"Invalid inline component name '{inlineComponentNode.Name}'. Inline component names must start with an uppercase letter.",
                    inlineComponentNode);
            }
        }

        private static bool IsValidQmlId(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            char first = value[0];
            if (first != '_' && !IsAsciiLowercase(first))
            {
                return false;
            }

            for (int index = 1; index < value.Length; index++)
            {
                char current = value[index];
                if (current != '_' && !IsAsciiLetter(current) && !char.IsDigit(current))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidSignalHandlerName(string value)
        {
            return value.Length > 2
                && value.StartsWith("on", StringComparison.Ordinal)
                && IsAsciiUppercase(value[2])
                && IsAsciiIdentifierTail(value, startIndex: 3);
        }

        private static bool IsValidInlineComponentName(string value)
        {
            return value.Length > 0
                && IsAsciiUppercase(value[0])
                && IsAsciiIdentifierTail(value, startIndex: 1);
        }

        private static bool IsAsciiIdentifierTail(string value, int startIndex)
        {
            for (int index = startIndex; index < value.Length; index++)
            {
                char current = value[index];
                if (current != '_' && !IsAsciiLetter(current) && !char.IsDigit(current))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAsciiLetter(char value)
        {
            return IsAsciiLowercase(value) || IsAsciiUppercase(value);
        }

        private static bool IsAsciiLowercase(char value)
        {
            return value is >= 'a' and <= 'z';
        }

        private static bool IsAsciiUppercase(char value)
        {
            return value is >= 'A' and <= 'Z';
        }

        private static bool IsNonEmpty(string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool IsValidOptionalQualifier(string? value)
        {
            return value is null || IsNonEmpty(value);
        }

        private sealed class StructuralValidationContext
        {
            private readonly ImmutableArray<AstDiagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<AstDiagnostic>();
            private readonly Dictionary<string, IdAssignmentNode> _ids = new(StringComparer.Ordinal);

            public void ValidateIdUniqueness(IdAssignmentNode idAssignmentNode)
            {
                if (!_ids.TryAdd(idAssignmentNode.Id, idAssignmentNode))
                {
                    Add(
                        DiagnosticCode.E001_DuplicateId,
                        $"Duplicate id '{idAssignmentNode.Id}'. QML ids must be unique within a document.",
                        idAssignmentNode);
                }
            }

            public void Add(DiagnosticCode code, string message, AstNode node)
            {
                _diagnostics.Add(new AstDiagnostic
                {
                    Code = code,
                    Message = message,
                    Severity = DiagnosticSeverity.Error,
                    Span = node.Span,
                    Node = node,
                });
            }

            public ImmutableArray<AstDiagnostic> ToImmutable()
            {
                return _diagnostics.ToImmutable();
            }
        }

        private sealed class ObjectScope
        {
            private readonly HashSet<string> _propertyNames = new(StringComparer.Ordinal);
            private readonly HashSet<string> _signalNames = new(StringComparer.Ordinal);
            private readonly HashSet<string> _enumNames = new(StringComparer.Ordinal);

            public void ValidatePropertyName(string propertyName, AstNode node, StructuralValidationContext context)
            {
                if (!_propertyNames.Add(propertyName))
                {
                    context.Add(
                        DiagnosticCode.E003_DuplicatePropertyName,
                        $"Duplicate property name '{propertyName}' in the same object.",
                        node);
                }
            }

            public void ValidateSignalName(string signalName, SignalDeclarationNode node, StructuralValidationContext context)
            {
                if (!_signalNames.Add(signalName))
                {
                    context.Add(
                        DiagnosticCode.E004_DuplicateSignalName,
                        $"Duplicate signal name '{signalName}' in the same object.",
                        node);
                }
            }

            public void ValidateEnum(EnumDeclarationNode node, StructuralValidationContext context)
            {
                if (!_enumNames.Add(node.Name))
                {
                    context.Add(
                        DiagnosticCode.E008_DuplicateEnumName,
                        $"Duplicate enum name '{node.Name}' in the same object.",
                        node);
                }

                HashSet<string> memberNames = new(StringComparer.Ordinal);
                foreach (EnumMember member in node.Members)
                {
                    if (!memberNames.Add(member.Name))
                    {
                        context.Add(
                            DiagnosticCode.E008_DuplicateEnumName,
                            $"Duplicate enum member '{member.Name}' in enum '{node.Name}'.",
                            node);
                    }
                }
            }
        }
    }
}
