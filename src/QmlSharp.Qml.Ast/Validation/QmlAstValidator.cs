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

            SemanticValidationContext context = new(typeChecker);
            context.ValidateImports(document.Imports);
            ValidateSemanticObject(document.RootObject, context);
            context.ValidateUnusedImports(document.Imports);

            return context.ToImmutable();
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

                case GroupedBindingNode groupedBindingNode:
                    scope.ValidatePropertyName(groupedBindingNode.GroupName, groupedBindingNode, context);
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
                    ValidateObject(inlineComponentNode.Body, context.CreateComponentScope(), objectDepth: 1);
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

        private static void ValidateSemanticObject(ObjectDefinitionNode objectNode, SemanticValidationContext context)
        {
            context.MarkTypeUsed(objectNode.TypeName);
            bool typeExists = context.ValidateObjectType(objectNode, out string ownerTypeName);
            ObjectSemanticScope scope = ObjectSemanticScope.Create(objectNode);

            foreach (AstNode member in objectNode.Members)
            {
                ValidateSemanticObjectMember(ownerTypeName, member, scope, context, typeExists);
            }

            foreach (AstNode member in objectNode.Members)
            {
                ValidateSemanticNestedContent(member, context);
            }
        }

        private static void ValidateSemanticObjectMember(
            string ownerTypeName,
            AstNode member,
            ObjectSemanticScope scope,
            SemanticValidationContext context,
            bool ownerTypeExists)
        {
            switch (member)
            {
                case PropertyDeclarationNode propertyDeclarationNode:
                    context.ValidatePropertyType(propertyDeclarationNode);
                    if (propertyDeclarationNode.InitialValue is not null)
                    {
                        context.ValidateBindingValue(propertyDeclarationNode.InitialValue);
                    }

                    break;

                case AttachedBindingNode attachedBindingNode:
                    context.ValidateAttachedBinding(attachedBindingNode);
                    break;

                case FunctionDeclarationNode functionDeclarationNode:
                    context.ValidateFunctionDeclaration(functionDeclarationNode);
                    break;

                case SignalDeclarationNode signalDeclarationNode:
                    context.ValidateSignalDeclaration(signalDeclarationNode);
                    break;
            }

            if (ownerTypeExists)
            {
                ValidateSemanticOwnerTypeMember(ownerTypeName, member, scope, context);
            }
        }

        private static void ValidateSemanticOwnerTypeMember(
            string ownerTypeName,
            AstNode member,
            ObjectSemanticScope scope,
            SemanticValidationContext context)
        {
            switch (member)
            {
                case PropertyDeclarationNode propertyDeclarationNode:
                    context.ValidateRequiredPropertyDeclaration(ownerTypeName, propertyDeclarationNode);
                    break;

                case BindingNode bindingNode:
                    context.ValidatePropertyBinding(ownerTypeName, bindingNode);
                    break;

                case GroupedBindingNode groupedBindingNode:
                    context.ValidateGroupedBinding(ownerTypeName, groupedBindingNode);
                    break;

                case ArrayBindingNode arrayBindingNode:
                    context.ValidateArrayBinding(ownerTypeName, arrayBindingNode);
                    break;

                case BehaviorOnNode behaviorOnNode:
                    context.ValidateBehaviorOn(ownerTypeName, behaviorOnNode);
                    break;

                case SignalHandlerNode signalHandlerNode:
                    context.ValidateSignalHandler(ownerTypeName, signalHandlerNode, scope);
                    break;
            }
        }

        private static void ValidateSemanticNestedContent(AstNode member, SemanticValidationContext context)
        {
            switch (member)
            {
                case ObjectDefinitionNode childObject:
                    ValidateSemanticObject(childObject, context);
                    break;

                case InlineComponentNode inlineComponentNode:
                    ValidateSemanticObject(inlineComponentNode.Body, context);
                    break;

                case BindingNode bindingNode:
                    context.ValidateBindingValue(bindingNode.Value);
                    break;

                case GroupedBindingNode groupedBindingNode:
                    foreach (BindingNode bindingNode in groupedBindingNode.Bindings)
                    {
                        context.ValidateBindingValue(bindingNode.Value);
                    }

                    break;

                case AttachedBindingNode attachedBindingNode:
                    foreach (BindingNode bindingNode in attachedBindingNode.Bindings)
                    {
                        context.ValidateBindingValue(bindingNode.Value);
                    }

                    break;

                case ArrayBindingNode arrayBindingNode:
                    foreach (BindingValue element in arrayBindingNode.Elements)
                    {
                        context.ValidateBindingValue(element);
                    }

                    break;

                case BehaviorOnNode behaviorOnNode:
                    ValidateSemanticObject(behaviorOnNode.Animation, context);
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
            private readonly ImmutableArray<AstDiagnostic>.Builder _diagnostics;
            private readonly Dictionary<string, IdAssignmentNode> _ids;

            public StructuralValidationContext()
                : this(
                    ImmutableArray.CreateBuilder<AstDiagnostic>(),
                    new Dictionary<string, IdAssignmentNode>(StringComparer.Ordinal))
            {
            }

            private StructuralValidationContext(
                ImmutableArray<AstDiagnostic>.Builder diagnostics,
                Dictionary<string, IdAssignmentNode> ids)
            {
                _diagnostics = diagnostics;
                _ids = ids;
            }

            public StructuralValidationContext CreateComponentScope()
            {
                return new StructuralValidationContext(
                    _diagnostics,
                    new Dictionary<string, IdAssignmentNode>(StringComparer.Ordinal));
            }

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
                foreach (EnumMember member in node.Members.Where(member => !memberNames.Add(member.Name)))
                {
                    context.Add(
                        DiagnosticCode.E008_DuplicateEnumName,
                        $"Duplicate enum member '{member.Name}' in enum '{node.Name}'.",
                        node);
                }
            }
        }

        private sealed class SemanticValidationContext
        {
            private readonly ImmutableArray<AstDiagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<AstDiagnostic>();
            private readonly ITypeChecker _typeChecker;
            private readonly Dictionary<string, string> _moduleByQualifier = new(StringComparer.Ordinal);
            private readonly HashSet<string> _usedTypes = new(StringComparer.Ordinal);

            public SemanticValidationContext(ITypeChecker typeChecker)
            {
                _typeChecker = typeChecker;
            }

            public void ValidateImports(ImmutableArray<ImportNode> imports)
            {
                foreach (ImportNode importNode in imports)
                {
                    string? moduleUri = importNode.ModuleUri;
                    if (importNode.ImportKind == ImportKind.Module
                        && !string.IsNullOrWhiteSpace(moduleUri)
                        && _typeChecker.HasModule(moduleUri))
                    {
                        string? qualifier = importNode.Qualifier;
                        if (!string.IsNullOrWhiteSpace(qualifier))
                        {
                            _moduleByQualifier[qualifier] = moduleUri;
                        }
                    }
                    else if (importNode.ImportKind == ImportKind.Module
                        && !string.IsNullOrWhiteSpace(moduleUri))
                    {
                        AddError(
                            DiagnosticCode.E107_UnknownModule,
                            $"Unknown module '{moduleUri}'.",
                            importNode);
                    }
                }
            }

            public bool ValidateObjectType(ObjectDefinitionNode objectNode, out string resolvedTypeName)
            {
                if (TryResolveTypeName(objectNode.TypeName, out resolvedTypeName))
                {
                    return true;
                }

                resolvedTypeName = objectNode.TypeName;
                AddError(
                    DiagnosticCode.E100_UnknownType,
                    $"Unknown type '{objectNode.TypeName}'.",
                    objectNode);
                return false;
            }

            public void ValidatePropertyType(PropertyDeclarationNode node)
            {
                MarkTypeUsed(node.TypeName);
                if (!HasType(node.TypeName))
                {
                    AddError(
                        DiagnosticCode.E100_UnknownType,
                        $"Unknown property type '{node.TypeName}' for property '{node.Name}'.",
                        node);
                }
            }

            public void ValidateRequiredPropertyDeclaration(string ownerTypeName, PropertyDeclarationNode node)
            {
                if (node.IsRequired
                    && node.InitialValue is null
                    && _typeChecker.IsPropertyRequired(ownerTypeName, node.Name))
                {
                    AddError(
                        DiagnosticCode.E104_RequiredPropertyNotSet,
                        $"Required property '{node.Name}' is not set.",
                        node);
                }
            }

            public void ValidatePropertyBinding(string typeName, BindingNode node)
            {
                if (!_typeChecker.HasProperty(typeName, node.PropertyName))
                {
                    AddError(
                        DiagnosticCode.E101_UnknownProperty,
                        $"Unknown property '{node.PropertyName}' on type '{typeName}'.",
                        node);
                    return;
                }

                if (_typeChecker.IsPropertyReadonly(typeName, node.PropertyName))
                {
                    AddError(
                        DiagnosticCode.E105_ReadonlyPropertyBound,
                        $"Cannot bind readonly property '{node.PropertyName}' on type '{typeName}'.",
                        node);
                }
            }

            public void ValidateGroupedBinding(string typeName, GroupedBindingNode node)
            {
                if (!_typeChecker.HasProperty(typeName, node.GroupName))
                {
                    AddError(
                        DiagnosticCode.E101_UnknownProperty,
                        $"Unknown grouped property '{node.GroupName}' on type '{typeName}'.",
                        node);
                }
            }

            public void ValidateAttachedBinding(AttachedBindingNode node)
            {
                MarkTypeUsed(node.AttachedTypeName);
                if (!_typeChecker.IsAttachedType(node.AttachedTypeName))
                {
                    AddError(
                        DiagnosticCode.E103_UnknownAttachedType,
                        $"Unknown attached type '{node.AttachedTypeName}'.",
                        node);
                    return;
                }

                foreach (BindingNode bindingNode in node.Bindings)
                {
                    ValidatePropertyBinding(node.AttachedTypeName, bindingNode);
                }
            }

            public void ValidateArrayBinding(string typeName, ArrayBindingNode node)
            {
                if (!_typeChecker.HasProperty(typeName, node.PropertyName))
                {
                    AddError(
                        DiagnosticCode.E101_UnknownProperty,
                        $"Unknown array property '{node.PropertyName}' on type '{typeName}'.",
                        node);
                }
            }

            public void ValidateBehaviorOn(string typeName, BehaviorOnNode node)
            {
                if (!_typeChecker.HasProperty(typeName, node.PropertyName))
                {
                    AddError(
                        DiagnosticCode.E101_UnknownProperty,
                        $"Unknown behavior target property '{node.PropertyName}' on type '{typeName}'.",
                        node);
                }
                else if (_typeChecker.IsPropertyReadonly(typeName, node.PropertyName))
                {
                    AddError(
                        DiagnosticCode.E105_ReadonlyPropertyBound,
                        $"Cannot animate readonly property '{node.PropertyName}' on type '{typeName}'.",
                        node);
                }
            }

            public void ValidateSignalHandler(string typeName, SignalHandlerNode node, ObjectSemanticScope scope)
            {
                if (!TryGetSignalNameFromHandler(node.HandlerName, out string signalName))
                {
                    return;
                }

                if (_typeChecker.HasSignal(typeName, signalName))
                {
                    return;
                }

                if (TryGetChangedPropertyName(signalName, out string propertyName)
                    && (scope.HasLocalProperty(propertyName) || _typeChecker.HasProperty(typeName, propertyName)))
                {
                    return;
                }

                AddError(
                    DiagnosticCode.E102_UnknownSignal,
                    $"Unknown signal '{signalName}' for handler '{node.HandlerName}' on type '{typeName}'.",
                    node);
            }

            public void ValidateSignalDeclaration(SignalDeclarationNode node)
            {
                foreach (ParameterDeclaration parameter in node.Parameters)
                {
                    ValidateParameterType(parameter.TypeName, node);
                }
            }

            public void ValidateFunctionDeclaration(FunctionDeclarationNode node)
            {
                foreach (ParameterDeclaration parameter in node.Parameters)
                {
                    ValidateParameterType(parameter.TypeName, node);
                }

                if (node.ReturnType is not null)
                {
                    ValidateParameterType(node.ReturnType, node);
                }
            }

            public void ValidateBindingValue(BindingValue value)
            {
                switch (value)
                {
                    case EnumReference enumReference:
                        MarkTypeUsed(enumReference.TypeName);
                        if (!HasEnumMember(enumReference.TypeName, enumReference.MemberName))
                        {
                            AddError(
                                DiagnosticCode.E106_InvalidEnumReference,
                                $"Invalid enum reference '{enumReference.TypeName}.{enumReference.MemberName}'.",
                                null);
                        }

                        break;

                    case ObjectValue objectValue:
                        ValidateSemanticObject(objectValue.Object, this);
                        break;

                    case ArrayValue arrayValue:
                        foreach (BindingValue element in arrayValue.Elements)
                        {
                            ValidateBindingValue(element);
                        }

                        break;
                }
            }

            public void MarkTypeUsed(string typeName)
            {
                _usedTypes.Add(typeName);
            }

            public void ValidateUnusedImports(ImmutableArray<ImportNode> imports)
            {
                foreach (ImportNode importNode in imports)
                {
                    string? moduleUri = importNode.ModuleUri;
                    if (importNode.ImportKind != ImportKind.Module
                        || string.IsNullOrWhiteSpace(moduleUri)
                        || !_typeChecker.HasModule(moduleUri))
                    {
                        continue;
                    }

                    if (IsImportUsed(importNode, imports))
                    {
                        continue;
                    }

                    AddWarning(
                        DiagnosticCode.W001_UnusedImport,
                        $"Import '{importNode.ModuleUri}' appears unused.",
                        importNode);
                }
            }

            public ImmutableArray<AstDiagnostic> ToImmutable()
            {
                return _diagnostics.ToImmutable();
            }

            private void ValidateParameterType(string typeName, AstNode node)
            {
                MarkTypeUsed(typeName);
                if (!HasType(typeName))
                {
                    AddError(
                        DiagnosticCode.E100_UnknownType,
                        $"Unknown type '{typeName}'.",
                        node);
                }
            }

            private bool IsImportUsed(ImportNode importNode, ImmutableArray<ImportNode> imports)
            {
                string moduleUri = importNode.ModuleUri!;
                string? qualifier = importNode.Qualifier;

                if (!string.IsNullOrWhiteSpace(qualifier))
                {
                    string qualifierPrefix = qualifier + ".";
                    return _usedTypes.Any(usedType =>
                        usedType.StartsWith(qualifierPrefix, StringComparison.Ordinal)
                        && _typeChecker.HasType(moduleUri + usedType[qualifier.Length..]));
                }

                if (_usedTypes.Any(usedType =>
                    IsQualifiedTypeInModule(moduleUri, usedType)
                    || (!IsQualifiedTypeName(usedType) && _typeChecker.HasType(moduleUri + "." + usedType))))
                {
                    return true;
                }

                return imports.Count(IsUnqualifiedModuleImport) == 1
                    && _usedTypes.Any(typeName => !IsQualifiedTypeName(typeName) && _typeChecker.HasType(typeName));
            }

            private bool HasType(string typeName)
            {
                return TryResolveTypeName(typeName, out _);
            }

            private bool HasEnumMember(string typeName, string memberName)
            {
                if (_typeChecker.HasEnumMember(typeName, memberName))
                {
                    return true;
                }

                return TryResolveQualifiedImportType(typeName, out string resolvedTypeName)
                    && _typeChecker.HasEnumMember(resolvedTypeName, memberName);
            }

            private bool TryResolveTypeName(string typeName, out string resolvedTypeName)
            {
                if (_typeChecker.HasType(typeName))
                {
                    resolvedTypeName = typeName;
                    return true;
                }

                return TryResolveQualifiedImportType(typeName, out resolvedTypeName)
                    && _typeChecker.HasType(resolvedTypeName);
            }

            private bool TryResolveQualifiedImportType(string typeName, out string resolvedTypeName)
            {
                resolvedTypeName = string.Empty;
                int qualifierEnd = typeName.IndexOf('.');
                if (qualifierEnd <= 0)
                {
                    return false;
                }

                string qualifier = typeName[..qualifierEnd];
                if (!_moduleByQualifier.TryGetValue(qualifier, out string? moduleUri))
                {
                    return false;
                }

                resolvedTypeName = moduleUri + typeName[qualifierEnd..];
                return true;
            }

            private static bool IsUnqualifiedModuleImport(ImportNode imported)
            {
                return imported.ImportKind == ImportKind.Module
                    && IsNonEmpty(imported.ModuleUri)
                    && string.IsNullOrWhiteSpace(imported.Qualifier);
            }

            private static bool IsQualifiedTypeName(string typeName)
            {
                return typeName.Contains('.', StringComparison.Ordinal);
            }

            private static bool IsQualifiedTypeInModule(string moduleUri, string typeName)
            {
                return typeName.StartsWith(moduleUri + ".", StringComparison.Ordinal);
            }

            private static bool TryGetSignalNameFromHandler(string handlerName, out string signalName)
            {
                signalName = string.Empty;
                if (!handlerName.StartsWith("on", StringComparison.Ordinal) || handlerName.Length <= 2)
                {
                    return false;
                }

                signalName = char.ToLowerInvariant(handlerName[2]) + handlerName[3..];
                return true;
            }

            private static bool TryGetChangedPropertyName(string signalName, out string propertyName)
            {
                const string ChangedSuffix = "Changed";
                propertyName = string.Empty;
                if (!signalName.EndsWith(ChangedSuffix, StringComparison.Ordinal) || signalName.Length == ChangedSuffix.Length)
                {
                    return false;
                }

                propertyName = signalName[..^ChangedSuffix.Length];
                return true;
            }

            private void AddError(DiagnosticCode code, string message, AstNode? node)
            {
                Add(code, DiagnosticSeverity.Error, message, node);
            }

            private void AddWarning(DiagnosticCode code, string message, AstNode? node)
            {
                Add(code, DiagnosticSeverity.Warning, message, node);
            }

            private void Add(DiagnosticCode code, DiagnosticSeverity severity, string message, AstNode? node)
            {
                _diagnostics.Add(new AstDiagnostic
                {
                    Code = code,
                    Message = message,
                    Severity = severity,
                    Span = node?.Span,
                    Node = node,
                });
            }
        }

        private sealed class ObjectSemanticScope
        {
            private readonly HashSet<string> _localProperties;

            private ObjectSemanticScope(HashSet<string> localProperties)
            {
                _localProperties = localProperties;
            }

            public static ObjectSemanticScope Create(ObjectDefinitionNode objectNode)
            {
                HashSet<string> localProperties = new(StringComparer.Ordinal);
                foreach (AstNode member in objectNode.Members)
                {
                    switch (member)
                    {
                        case PropertyDeclarationNode propertyDeclarationNode:
                            _ = localProperties.Add(propertyDeclarationNode.Name);
                            break;

                        case PropertyAliasNode propertyAliasNode:
                            _ = localProperties.Add(propertyAliasNode.Name);
                            break;
                    }
                }

                return new ObjectSemanticScope(localProperties);
            }

            public bool HasLocalProperty(string propertyName)
            {
                return _localProperties.Contains(propertyName);
            }
        }
    }
}
